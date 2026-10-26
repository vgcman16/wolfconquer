using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData((int)LegacyOrdinaryAdmissionInterruption.AfterPending)]
    [InlineData((int)LegacyOrdinaryAdmissionInterruption.AfterCharacter)]
    [InlineData((int)LegacyOrdinaryAdmissionInterruption.AfterItems)]
    [InlineData((int)LegacyOrdinaryAdmissionInterruption.AfterSpells)]
    [InlineData((int)LegacyOrdinaryAdmissionInterruption.AfterProficiencies)]
    [InlineData((int)LegacyOrdinaryAdmissionInterruption.AfterJournalComplete)]
    public async Task GenuineInterruptedPreservingAdmissionRecoversAndReconnectsWithoutAnyItemAllocation(int interruption)
    {
        using var input = new LoginInput();
        SeedOwnedOrdinaryParticipants(input);
        using var uids = new AdmissionUidFixture(input);
        var initial = CaptureAdmissionFiles(input);
        var uidBytes = uids.StateBytes();
        var raw = new CharacterSnapshotLoader(input.Users).TryLoadByAccountIdForAdmission(Uid).Snapshot!;
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (_, birth) = await ConnectBornAsync(fixture);
            var native = birth.Connection.NativeGameClient!;
            var before = ObservePrefixPlayer(native.Player);
            var recovery = AdmissionOwner<LegacyTradeRecoveryState>(handler, "_tradeRecovery");
            Assert.True(recovery.TryEnsureInitialized(out var reason), reason);
            using var operation = await AdmissionOwner<WorldOperationBarrier>(handler, "_worldOperations").EnterExclusiveAsync(fixture.Token);
            Assert.False(AdmissionOwner<WorldSessionStore>(handler, "_sessions").TryCommitOrdinaryAdmission(birth.Connection,
                PrepareRealAdmission(handler, birth.Connection, input, uids.Owner, recovery), out var admitted, out reason,
                (LegacyOrdinaryAdmissionInterruption)interruption));
            Assert.Null(admitted);
            Assert.True(recovery.IsBlocked);
            Assert.Equal(before, ObservePrefixPlayer(native.Player));
            Assert.Null(native.Beasts);
            Assert.Null(native.HairfaceStorage);
            Assert.Null(native.RawEquipmentObservation);
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.Equal(interruption != (int)LegacyOrdinaryAdmissionInterruption.AfterJournalComplete, LegacyTradeJournal.HasPending(input.Users));
            // A real UID registry deliberately refuses ordinary queries while
            // the shared journal is pending; inspect its unchanged owned image.
            Assert.Equal(uidBytes, uids.StateBytes());
            if (interruption == (int)LegacyOrdinaryAdmissionInterruption.AfterPending) AssertAdmissionFiles(initial);
        }, admissionUids: uids.Owner);
        uids.Dispose();
        Assert.True(FreshAdmissionRecovery(input).TryEnsureInitialized(out var recovered), recovered);
        Assert.False(LegacyTradeJournal.HasPending(input.Users));
        uids.Open(bootstrap: false);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var committed = CaptureAdmissionFiles(input);
            var commitObservations = ObserveRawAdmissionCommit(log, () => AssertAdmissionFiles(committed));
            var admitted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            Assert.Equal(1, commitObservations());
            AssertOrdinaryBusinessPreserved(raw, admitted.Character, expectedRuntimeHitpoints: 0);
            AssertAdmissionFilesAfterRuntimeHpSave(committed, input, hitpoints: 0);
            Assert.Equal(new uint[] { 100 }, admitted.PlayerItems.EquipmentItems.Select(row => row.Uid));
            Assert.Equal(initial[Path.Combine(AdmissionRoot(input), "PlayersItems", Uid + ".bin")],
                File.ReadAllBytes(Path.Combine(AdmissionRoot(input), "PlayersItems", Uid + ".bin")));
            Assert.Equal(new ushort[] { 8002, 1110 }, admitted.PlayerSpells.Spells.Select(row => row.Id));
            Assert.Equal(123, admitted.PlayerSpells.Spells[0].Experience);
            Assert.Equal(1U, birth.Connection.NativeGameClient!.Player.Online);
            AssertNoOrdinaryAllocation(uids, uidBytes);
            var files = CaptureAdmissionFiles(input);
            Assert.Same(admitted, (await SendLoginAsync(fixture, wire)).After);
            AssertAdmissionFiles(files);
            Assert.False(LegacyTradeJournal.HasPending(input.Users));
        }, admissionUids: uids.Owner);
    }

    [Theory]
    [InlineData((int)LegacyOrdinaryAdmissionInterruption.AfterUidIntent)]
    [InlineData((int)LegacyOrdinaryAdmissionInterruption.AfterUidReservation)]
    public async Task RetiredUidInterruptionCannotCreateAStarterIntentOrChangeSavedState(int interruption)
    {
        using var input = new LoginInput();
        SeedOwnedOrdinaryParticipants(input);
        using var uids = new AdmissionUidFixture(input);
        var initial = CaptureAdmissionFiles(input);
        var uidBytes = uids.StateBytes();
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (_, birth) = await ConnectBornAsync(fixture);
            var recovery = AdmissionOwner<LegacyTradeRecoveryState>(handler, "_tradeRecovery");
            Assert.True(recovery.TryEnsureInitialized(out var reason), reason);
            using var operation = await AdmissionOwner<WorldOperationBarrier>(handler, "_worldOperations").EnterExclusiveAsync(fixture.Token);
            Assert.False(AdmissionOwner<WorldSessionStore>(handler, "_sessions").TryCommitOrdinaryAdmission(birth.Connection,
                PrepareRealAdmission(handler, birth.Connection, input, uids.Owner, recovery), out _, out reason,
                (LegacyOrdinaryAdmissionInterruption)interruption));
            Assert.Equal("ordinaryAdmissionHasNoUidAllocation", reason);
            Assert.False(recovery.IsBlocked);
            Assert.False(LegacyTradeJournal.HasPending(input.Users));
            AssertAdmissionFiles(initial);
            AssertNoOrdinaryAllocation(uids, uidBytes);
            Assert.Equal(0U, birth.Connection.NativeGameClient!.Player.UID);
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
        }, admissionUids: uids.Owner);
    }

    private static LegacyTradeRecoveryState FreshAdmissionRecovery(LoginInput input)
    {
        var roots = LegacyOrdinaryAdmissionRoots.FromLoader(new CharacterSnapshotLoader(input.Users));
        return new(roots.Users, roots.Items, roots.Quests, Path.Combine(AdmissionRoot(input), "PlayersMailList"),
            roots.CombatGear, roots.Spells, roots.Proficiencies);
    }

    private static LegacyOrdinaryAdmissionPlan PrepareRealAdmission(WorldServerFrameHandler handler,
        WolfConquer.Core.Networking.ConquerTcpConnection connection, LoginInput input,
        LegacyOrdinaryAdmissionUidOwner? owner, LegacyTradeRecoveryState recovery)
    {
        var loader = new CharacterSnapshotLoader(input.Users);
        var raw = loader.TryLoadByAccountIdForAdmission(Uid);
        Assert.Equal(CharacterSnapshotLoadStatus.Success, raw.Status);
        Assert.True(LegacyOrdinaryAdmissionPlan.TryPrepare(handler, connection, Uid, "owned", raw.Snapshot!,
            loader, owner, recovery, TimeProvider.System, out var plan, out var reason), reason);
        return Assert.IsType<LegacyOrdinaryAdmissionPlan>(plan);
    }
}
