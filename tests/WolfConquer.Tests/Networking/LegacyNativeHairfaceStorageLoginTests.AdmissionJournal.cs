using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData("unknownField")]
    [InlineData("missingInput")]
    [InlineData("foreignRoot")]
    [InlineData("stringKind")]
    [InlineData("foreignTarget")]
    public async Task OrdinaryJournalRejectsClosedShapeOrPinnedMembershipDriftBeforeRecoveryWrites(string mutation)
    {
        using var input = new LoginInput();
        SeedOwnedOrdinaryParticipants(input);
        using var uids = new AdmissionUidFixture(input);
        await InterruptRealAdmission(input, uids, LegacyOrdinaryAdmissionInterruption.AfterPending);
        uids.Dispose();
        var pending = Path.Combine(input.Users, ".managed-trade-journal", "pending.json");
        var legitimate = File.ReadAllBytes(pending);
        var record = JsonNode.Parse(legitimate)!;
        if (mutation == "unknownField") record["Unowned"] = true;
        else if (mutation == "missingInput") record["AdmissionInputs"]!.AsArray().RemoveAt(0);
        else if (mutation == "foreignRoot") record["AdmissionDomain"]!["Spells"] = Path.Combine(AdmissionRoot(input), "ForeignSpells");
        else if (mutation == "stringKind") record["AdmissionInputs"]![0]!["Kind"] = "Quests";
        else record["Changes"]![0]!["FileId"] = Uid + 1;
        File.WriteAllText(pending, record.ToJsonString());
        var malformed = File.ReadAllBytes(pending);
        var files = CaptureAdmissionFiles(input);
        var state = uids.StateBytes();

        Assert.False(FreshAdmissionRecovery(input).TryEnsureInitialized(out var reason));

        Assert.Contains(LegacyTradeJournal.RecoveryRequired, reason);
        AssertAdmissionFiles(files);
        Assert.Equal(malformed, File.ReadAllBytes(pending));
        Assert.Equal(state, uids.StateBytes());
        File.WriteAllBytes(pending, legitimate);
        Assert.True(FreshAdmissionRecovery(input).TryEnsureInitialized(out reason), reason);
        uids.Open(bootstrap: false);
        Assert.False(LegacyTradeJournal.HasPending(input.Users));
    }

    [Fact]
    public async Task LockedOwnedStagingResidueRetainsTheCommittedJournalUntilRealCleanupCanFinish()
    {
        using var input = new LoginInput();
        SeedOwnedOrdinaryParticipants(input);
        using var uids = new AdmissionUidFixture(input);
        await InterruptRealAdmission(input, uids, LegacyOrdinaryAdmissionInterruption.AfterProficiencies);
        uids.Dispose();
        var pending = Path.Combine(input.Users, ".managed-trade-journal", "pending.json");
        var record = File.ReadAllBytes(pending);
        var transaction = JsonNode.Parse(record)!["TransactionId"]!.GetValue<string>();
        var stage = input.FilePath + ".managed-trade-" + transaction + ".tmp";
        File.WriteAllBytes(stage, [0x39, 0x4a]);
        var files = CaptureAdmissionFiles(input);
        using (var held = new FileStream(stage, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.False(FreshAdmissionRecovery(input).TryEnsureInitialized(out var reason));
            Assert.Contains(LegacyTradeJournal.RecoveryRequired, reason);
            Assert.Equal(record, File.ReadAllBytes(pending));
            Assert.Equal(new byte[] { 0x39, 0x4a }, File.ReadAllBytes(stage));
            AssertAdmissionFiles(files);
        }
        Assert.True(FreshAdmissionRecovery(input).TryEnsureInitialized(out var complete), complete);
        Assert.False(File.Exists(stage));
        Assert.False(LegacyTradeJournal.HasPending(input.Users));
        AssertAdmissionFiles(files);
        uids.Open(bootstrap: false);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, freshBirth) = await ConnectBornAsync(fixture);
            var commitObservations = ObserveRawAdmissionCommit(log, () => AssertAdmissionFiles(files));
            var accepted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            Assert.Equal(1, commitObservations());
            Assert.Equal(new uint[] { 100 }, accepted.PlayerItems.EquipmentItems.Select(row => row.Uid));
            Assert.Equal(1L, accepted.Character.Online);
            Assert.Equal(1L, new CharacterSnapshotLoader(input.Users).TryLoadByAccountIdForAdmission(Uid).Snapshot!.Online);
            Assert.Equal(1U, freshBirth.Connection.NativeGameClient!.Player.Online);
            AssertAdmissionFilesAfterRuntimeHpSave(files, input, hitpoints: 0);
        }, worldData: AdmissionCatalog(), admissionUids: uids.Owner);
    }

    private static async Task InterruptRealAdmission(LoginInput input, AdmissionUidFixture uids,
        LegacyOrdinaryAdmissionInterruption interruption)
    {
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (_, birth) = await ConnectBornAsync(fixture);
            var recovery = AdmissionOwner<LegacyTradeRecoveryState>(handler, "_tradeRecovery");
            Assert.True(recovery.TryEnsureInitialized(out var reason), reason);
            using var operation = await AdmissionOwner<WorldOperationBarrier>(handler, "_worldOperations")
                .EnterExclusiveAsync(fixture.Token);
            Assert.False(AdmissionOwner<WorldSessionStore>(handler, "_sessions").TryCommitOrdinaryAdmission(birth.Connection,
                PrepareRealAdmission(handler, birth.Connection, input, uids.Owner, recovery), out var admitted, out reason, interruption));
            Assert.Null(admitted);
            Assert.True(recovery.IsBlocked);
            Assert.True(LegacyTradeJournal.HasPending(input.Users));
        }, worldData: AdmissionCatalog(), admissionUids: uids.Owner);
    }
}
