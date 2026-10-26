using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Fact]
    public async Task ExactAlreadyAfterOldWorkRetiresOnlyItsRealRangeAndDoesNotAuthorizeNewPayout()
    {
        using var input = new LoginInput();
        PrepareAdmissionInput(input, starter: true);
        using var uids = new AdmissionUidFixture(input);
        var retained = SeedRetiredOrdinaryWork(input, uids, allocated: true, out var after);
        uids.Dispose();
        InstallRetiredAfterFacts(input, after);
        var files = CaptureAdmissionFiles(input);
        uids.Open(bootstrap: false);
        Assert.True(uids.Owner.IsReconciliationComplete);
        var archived = uids.StateBytes();
        Assert.Empty(JsonNode.Parse(archived)!["ProducerIntents"]!.AsArray());
        Assert.Equal(1UL, JsonNode.Parse(archived)!["ArchiveCount"]!.GetValue<ulong>());
        Assert.True(uids.Registry.TryReadProducerIntent(uids.Domain, retained.Request.OperationId, out var proof, out var reason), reason);
        Assert.Equal(retained.Allocation, proof!.Allocation);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, _) = await ConnectBornAsync(fixture);
            var admitted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            Assert.Equal(new uint[] { 2000001, 2000002, 2000003, 2000004 }, admitted.PlayerItems.EquipmentItems.Select(row => row.Uid));
            AssertAdmissionFiles(files);
            Assert.Equal(archived, uids.StateBytes());
        }, admissionUids: uids.Owner);
        // Changed legitimate saved progress after completion is not a new grant
        // entitlement and must not be denied merely for lacking starter gear.
        File.Delete(Path.Combine(AdmissionRoot(input), "PlayersItems", Uid + ".bin"));
        File.WriteAllText(input.FilePath, File.ReadAllText(input.FilePath).Replace("Money=100000\r\n", "Money=0\r\n", StringComparison.Ordinal)
            .Replace("X=450\r\n", "X=451\r\n", StringComparison.Ordinal));
        var changed = CaptureAdmissionFiles(input);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, _) = await ConnectBornAsync(fixture);
            var admitted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            Assert.Empty(admitted.PlayerItems.EquipmentItems);
            Assert.Equal(0L, admitted.Character.Money);
            AssertAdmissionFiles(changed);
            Assert.Equal(archived, uids.StateBytes());
        }, admissionUids: uids.Owner);
    }

    [Fact]
    public async Task MoreThan128DistinctExistingLoginsConsumeNoStarterIntentHistoryOrUidRanges()
    {
        using var input = new LoginInput();
        PrepareAdmissionInput(input, starter: true);
        var saved = File.ReadAllText(input.FilePath);
        var roots = LegacyOrdinaryAdmissionRoots.FromLoader(new CharacterSnapshotLoader(input.Users));
        for (var index = 0; index < 129; index++)
        {
            var account = checked(Uid + (uint)index);
            File.WriteAllText(roots.PathFor(LegacyTradeJournalFileKind.Character, account), saved.Replace(
                "UID=" + Uid.ToString(CultureInfo.InvariantCulture) + "\r\n", "UID=" + account.ToString(CultureInfo.InvariantCulture) + "\r\n", StringComparison.Ordinal)
                .Replace("Name=HairfaceOwner\r\n", "Name=Owned" + index.ToString(CultureInfo.InvariantCulture) + "\r\n", StringComparison.Ordinal), new UTF8Encoding(false));
        }
        using var uids = new AdmissionUidFixture(input);
        var state = uids.StateBytes();
        for (var index = 0; index < 129; index++)
        {
            var account = checked(Uid + (uint)index);
            await WithHandlerAsync(input, async (fixture, handler, log) =>
            {
                var (wire, _) = await ConnectBornAsync(fixture);
                await wire.SendAsync(new LegacyMsgLoginGamePacket(account).ToFrameBytes(), fixture.Token);
                var dispatched = await fixture.NextAsync();
                Assert.Equal(LegacyMsgLoginGamePacket.Opcode, dispatched.Opcode);
                var admitted = Assert.IsType<WorldSession>(dispatched.After);
                Assert.Equal((long)account, admitted.AccountId);
                Assert.Equal((long)account, admitted.Character.Uid);
                Assert.Equal(1000L, admitted.Character.Money);
                Assert.Empty(admitted.PlayerItems.InventoryItems);
                Assert.Empty(admitted.PlayerItems.EquipmentItems);
            }, admissionUids: uids.Owner);
            AssertNoOrdinaryAllocation(uids, state);
            Assert.False(LegacyTradeJournal.HasPending(input.Users));
        }
        uids.Dispose();
        uids.Open(bootstrap: false);
        AssertNoOrdinaryAllocation(uids, state);
        Assert.Empty(Directory.GetFiles(roots.Items!, "*.bin"));
    }
}