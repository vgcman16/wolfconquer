using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    private Task RunOwnedLegacyRuneBagFullCaseAsync(RuneBagFullCase scenario)
    {
        bool accepted = scenario == RuneBagFullCase.ThirtyNine;
        int count = accepted ? 39 : 40;
        uint type = scenario == RuneBagFullCase.OtherItem ? RuneRankStoredId : RuneBagFullItemId;
        ushort position = scenario == RuneBagFullCase.WrongPosition ? (ushort)212 : (ushort)211;
        byte bound = scenario == RuneBagFullCase.BoundFull ? (byte)1 : (byte)0;
        WriteSnapshot(accountId: RuneRankPlayerA, uid: RuneRankPlayerA, mapId: 1015, x: 826, y: 565,
            level: scenario == RuneBagFullCase.UnderAge ? 14 : 140);
        var path = Path.Combine(_usersRoot, RuneRankPlayerA.ToString(CultureInfo.InvariantCulture) + ".ini");
        var text = File.ReadAllText(path); Assert.Contains("Class=7051", text, StringComparison.Ordinal);
        File.WriteAllText(path, text.Replace("Class=7051", "Class=6000", StringComparison.Ordinal));
        var rows = Enumerable.Range(0, count).Select(index => PlayerItemSnapshotLoaderTests.DbItem(
            checked(6_200_000U + (uint)index), RuneBagMergeFragmentId, position: 0,
            durability: 1, maximumDurability: 1, stackSize: 1)).ToList();
        rows.Add(PlayerItemSnapshotLoaderTests.DbItem(RuneRankTargetUid, RuneRankTargetId, position: 102,
            durability: 100, maximumDurability: 100, stackSize: 1));
        rows.Add(PlayerItemSnapshotLoaderTests.DbItem(RuneBagFullUid, type, position: position,
            durability: 1, maximumDurability: 1, stackSize: 1, bound: bound));
        rows.Add(PlayerItemSnapshotLoaderTests.DbItem(RuneRankStoredUid, RuneRankStoredId, position: 212,
            durability: 100, maximumDurability: 100, stackSize: 1));
        WriteItems(RuneRankPlayerA, rows.ToArray()); WriteRuneRankingPlayer(RuneRankPlayerB, targetLevel: 15, material: false);
        var catalog = OwnedLegacyRuneBagCatalog(CreateRuneBagFullInventoryFailureCatalog(
            [RuneRankSeed(RuneRankPlayerA, 10300), RuneRankSeed(RuneRankPlayerB, 10000)], includeReturnedItem: true),
            scenario == RuneBagFullCase.MissingMetadata ? RuneBagFullItemId : null);
        // Missing raw46 is the genuine action prerequisite refusal. Keep the
        // actual native class/raw25 constructor dependency so admission is real.
        return AssertOwnedLegacyRuneBagReturnAsync(catalog, RuneBagFullUid, RuneBagFullUid, accepted, null,
            scenario == RuneBagFullCase.WrongPosition ? 15300U : 9700U, count, position, bound);
    }
}
