using System.Buffers.Binary;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    private Task RunOwnedLegacyRuneBagStackCaseAsync(RuneBagMergeCase scenario)
    {
        bool accepted = scenario is RuneBagMergeCase.Free or RuneBagMergeCase.Full or RuneBagMergeCase.MultipleFree
            or RuneBagMergeCase.TimedFull or RuneBagMergeCase.OtherFamilyFull;
        bool full = scenario is not (RuneBagMergeCase.Free or RuneBagMergeCase.MultipleFree or RuneBagMergeCase.MissingMetadataFree
            or RuneBagMergeCase.MissingUidFree or RuneBagMergeCase.WrongPositionFree);
        uint type = scenario == RuneBagMergeCase.OtherFamilyFull ? RuneBagMergeOtherFragmentId : RuneBagMergeFragmentId;
        byte bagBound = scenario is RuneBagMergeCase.Free or RuneBagMergeCase.DifferentBoundFull ? (byte)1 : (byte)0;
        byte receiverBound = scenario == RuneBagMergeCase.DifferentBoundFull ? (byte)0 : bagBound;
        ushort receiverStack = scenario == RuneBagMergeCase.OverCapacityFull ? (ushort)9999 : (ushort)3;
        WriteSnapshot(accountId: RuneRankPlayerA, uid: RuneRankPlayerA, mapId: 1015, x: 826, y: 565,
            level: scenario == RuneBagMergeCase.IneligibleFull ? 14 : 140);
        var receiver = PlayerItemSnapshotLoaderTests.DbItem(RuneBagMergeExistingUid, type, position: 0,
            durability: 1, maximumDurability: 1, stackSize: receiverStack, locked: 2, bound: receiverBound);
        receiver = scenario switch
        {
            RuneBagMergeCase.PlusFull => receiver with { Plus = 1 },
            RuneBagMergeCase.MaximumDurabilityFull => receiver with { MaximumDurability = 2 },
            RuneBagMergeCase.PurificationFull => receiver with { PurificationItemId = 2000 },
            RuneBagMergeCase.RefineryFull => receiver with { RefineryEffectId = 2000 },
            RuneBagMergeCase.UnlockTimerFull => receiver with { UnlockTimer = 1 },
            _ => receiver,
        };
        var rows = new List<PlayerItemSnapshotLoaderTests.TestDbItem> { receiver };
        if (scenario == RuneBagMergeCase.MultipleFree)
            rows.Add(PlayerItemSnapshotLoaderTests.DbItem(RuneBagMergeSecondUid, type, position: 0,
                durability: 1, maximumDurability: 1, stackSize: 4, locked: 2, bound: bagBound));
        if (full) for (int index = 0; index < 39; index++) rows.Add(PlayerItemSnapshotLoaderTests.DbItem(
            6_100_000U + (uint)index, type == RuneBagMergeFragmentId ? RuneBagMergeOtherFragmentId : RuneBagMergeFragmentId,
            position: 0, durability: 1, maximumDurability: 1, stackSize: 1));
        rows.Add(PlayerItemSnapshotLoaderTests.DbItem(RuneRankTargetUid, RuneRankTargetId, position: 102,
            durability: 100, maximumDurability: 100, stackSize: 1));
        // The old positives unlocked a Locked2 receiver from a different
        // Locked0 source. These declared compatible inputs retain Locked2 on
        // both roots. Each other mismatch below remains independently real.
        rows.Add(PlayerItemSnapshotLoaderTests.DbItem(RuneBagMergeBagUid, type,
            position: scenario == RuneBagMergeCase.WrongPositionFree ? (ushort)212 : (ushort)211,
            durability: 1, maximumDurability: 1, stackSize: 2, locked: 2, bound: bagBound));
        rows.Add(PlayerItemSnapshotLoaderTests.DbItem(RuneRankStoredUid, RuneRankStoredId, position: 212,
            durability: 100, maximumDurability: 100, stackSize: 1));
        WriteItems(RuneRankPlayerA, rows.ToArray());
        if (scenario is RuneBagMergeCase.ActivateFull or RuneBagMergeCase.ExpirationFull)
        {
            var path = RuneRankingItemsPath(RuneRankPlayerA); var bytes = File.ReadAllBytes(path);
            Assert.Equal(RuneBagMergeExistingUid, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)));
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4 + 44), scenario == RuneBagMergeCase.ActivateFull ? 1U : 0U);
            BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(4 + 168), scenario == RuneBagMergeCase.ExpirationFull ? 640290528000000000L : 0L);
            File.WriteAllBytes(path, bytes); // Exact declared saved input BEFORE bootstrap/admission.
        }
        WriteRuneRankingPlayer(RuneRankPlayerB, targetLevel: 15, material: false);
        var catalog = CreateRuneBagMergeCatalog([RuneRankSeed(RuneRankPlayerA, 10300), RuneRankSeed(RuneRankPlayerB, 10000)],
            includeFragment: scenario != RuneBagMergeCase.MissingMetadataFree,
            fragmentTime: scenario == RuneBagMergeCase.TimedFull ? 1U : 0U);
        return AssertOwnedLegacyRuneBagReturnAsync(catalog, RuneBagMergeBagUid,
            scenario == RuneBagMergeCase.MissingUidFree ? 5_349_999U : RuneBagMergeBagUid,
            accepted, accepted ? RuneBagMergeExistingUid : null,
            scenario == RuneBagMergeCase.WrongPositionFree ? 30000U : 9700U,
            full ? 40 : scenario == RuneBagMergeCase.MultipleFree ? 2 : 1,
            scenario == RuneBagMergeCase.WrongPositionFree ? (ushort)212 : (ushort)211, bagBound);
    }
}
