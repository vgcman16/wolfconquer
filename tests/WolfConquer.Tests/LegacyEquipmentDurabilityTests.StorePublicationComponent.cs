using System.Collections.Concurrent;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyEquipmentDurabilityTests
{
    // Component boundary: the REAL fixture Store consumes a REAL v2 writer.
    // This is not injection through the private Frame equipment handler.
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public Task ActualStoreBoundaryEquipmentJournalRetainsDurableMoveWhenRawPublicationProofFails(
        bool unequip, bool invalidateRawMap) => Run(async f =>
    {
        var operations = ReadEquipmentComponentOwner<WorldOperationBarrier>(f.Handler, "_worldOperations");
        var trades = ReadEquipmentComponentOwner<LegacyTradeStateStore>(f.Handler, "_tradeStates");
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
        var storage = Assert.IsType<LegacyNativeSpellStorage>(f.Native.MySpells);
        var nativeBefore = CaptureNative(f);
        var filesBefore = f.Data.Files();
        var characterBefore = File.ReadAllBytes(f.CharacterPath);
        var registryBefore = File.ReadAllBytes(f.Data.State);
        var targetPosition = unequip ? (ushort)0 : (ushort)2;
        var expectedImage = f.ExpectedPositions((EquipmentFixture.GearUid, targetPosition));
        WorldSession? final = null;
        bool? returned = null;
        bool realWriteSucceeded = false, rawInvalidated = false;
        var writerCalls = 0;
        string writerReason = string.Empty;
        Exception? outcome;
        WorldSession before;

        using (await operations.EnterExclusiveAsync())
        lock (trades.SyncRoot)
        {
            before = f.Current;
            Assert.True(f.Recovery.IsReadyFor(f.Data.Users, f.Data.Items));
            Assert.True(f.Recovery.TryRequireLiveJournalClear(out var clearReason), clearReason);
            Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(f.Connection, before));
            Assert.False(trades.Contains(f.Connection));
            Assert.False(before.LegacyTradeActive);
            Assert.Null(f.Native.MyTrade);
            Assert.Same(f.Store, binding.Store);
            Assert.Same(f.Recovery, binding.Recovery);
            Assert.Same(f.Connection, binding.Connection);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.DoesNotContain((ushort)6001, storage.ClientSpells.Keys);

            // Actual parsed graph and a single ordinary physical position move;
            // no UID allocation, saver substitution, owner replacement or grant.
            var parsedAfter = PlayerItemSnapshotLoader.Parse(expectedImage);
            var proposed = before with { PlayerItems = parsedAfter };
            Assert.True(LegacyItemSaveGraph.TryCapture(before.PlayerItems, out var beforeGraph));
            Assert.True(LegacyItemSaveGraph.TryCapture(proposed.PlayerItems, out var afterGraph));

            bool RealWriterThenOptionalRawConflict()
            {
                writerCalls++;
                var result = LegacyEquipmentMovePersistence.TryPersist(before, before, proposed, f.Data.Items,
                    beforeGraph!, afterGraph!, out writerReason);
                realWriteSucceeded = result;
                if (result && invalidateRawMap)
                {
                    // Deliberately invalidate the existing raw escape ONLY AFTER
                    // the actual complete v2 journal and postimage proof returned
                    // true. Return that genuine result, never an accepting stub.
                    rawInvalidated = storage.ClientSpells.TryAdd(6001, new LegacyNativeMsgSpell { ID = 6001, Level = 1 });
                }
                return result;
            }

            outcome = Record.Exception(() =>
            {
                returned = f.Store.TryCommitSession(f.Connection, before, proposed, RealWriterThenOptionalRawConflict, out final);
            });
            Assert.Equal(1, writerCalls);
            Assert.True(realWriteSucceeded, writerReason);
            Assert.Equal(invalidateRawMap, rawInvalidated);
            Assert.Equal(expectedImage, File.ReadAllBytes(f.ItemsPath));
            Assert.Equal(characterBefore, File.ReadAllBytes(f.CharacterPath));
            Assert.Equal(registryBefore, File.ReadAllBytes(f.Data.State));
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            Assert.Empty(f.Sent); // The Store component does not emit game packets.
            // A committed Store publication replaces complete native maps; a
            // failed final proof instead retires/detaches the joined graph.
            // Neither outcome mutates escaped old maps or scalar owners.
            var originalInventory = Assert.IsType<LegacyNativeInventoryRuntime>(nativeBefore.Inventory);
            var originalEquipment = Assert.IsType<LegacyNativeEquipItemStorage>(nativeBefore.Equipment);
            var originalInventoryMap = Assert.IsType<ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>>(nativeBefore.InventoryMap);
            var originalEquipmentMap = Assert.IsType<ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>>(nativeBefore.EquipmentMap);
            Assert.Equal(nativeBefore.InventoryRows, Rows(originalInventoryMap));
            Assert.Equal(nativeBefore.EquipmentRows, Rows(originalEquipmentMap));
            Assert.Same(f.Native, originalInventory.Owner); Assert.True(originalEquipment.IsOwnedBy(f.Native));
            Assert.Same(nativeBefore.Player, f.Native.Player);
            Assert.Equal(nativeBefore.Checked, f.Native.Player.IsCheckedPass);
            Assert.Equal(nativeBefore.Hitpoints, f.Native.Player.HitPoints);
            Assert.Equal(nativeBefore.Mana, f.Native.Player.Mana);

            var expectedFiles = new Dictionary<string, byte[]>(filesBefore, StringComparer.OrdinalIgnoreCase)
            { [Path.GetRelativePath(f.Data.Root, f.ItemsPath)] = expectedImage };
            f.Data.AssertFiles(expectedFiles);
            var saved = f.ReadItems();
            var allSaved = saved.InventoryItems.Concat(saved.EquipmentItems).ToArray();
            var allBefore = before.PlayerItems.InventoryItems.Concat(before.PlayerItems.EquipmentItems).ToArray();
            Assert.Equal(allBefore.Select(row => row.Uid).Order(), allSaved.Select(row => row.Uid).Order());
            Assert.Equal(targetPosition, Assert.Single(allSaved, row => row.Uid == EquipmentFixture.GearUid).Position);

            if (invalidateRawMap)
            {
                var committedFailure = Assert.IsType<LegacyCommittedSpellPublicationException>(outcome);
                Assert.Equal("ordinarySpellRecoveryRequired:durableCommitPublicationUnproven", committedFailure.Message);
                Assert.Null(returned); // Thrown committed outcome, not a false unchanged refusal.
                Assert.Null(final); // Candidate was never published after the failed final proof.
                Assert.True(binding.IsRetired);
                Assert.Same(binding, f.Native.OrdinarySpellBinding);
                Assert.False(binding.Items.PhysicalOwnerIsCurrentPlain());
                Assert.Null(f.Native.Inventory); Assert.Null(f.Native.Equipment);
                Assert.Null(f.Native.Warehouse); Assert.Null(f.Native.MyWardrobe); Assert.Null(f.Native.Rune);
                Assert.Null(f.Native.RelicFuse); Assert.Null(f.Native.GearMythsoulBag);
                Assert.Same(originalInventoryMap, originalInventory.ClientItems);
                Assert.Same(originalEquipmentMap, originalEquipment.ClientItems);
                Assert.Same(originalInventory, binding.Items.Inventory);
                Assert.Same(originalEquipment, binding.Items.Equipment);
                Assert.Null(f.CurrentOrNull);
                Assert.True(f.Recovery.IsBlocked);
                Assert.False(f.Recovery.TryRequireLiveJournalClear(out var fencedReason));
                Assert.Equal(committedFailure.Message, fencedReason);
                Assert.Same(storage, f.Native.MySpells);
                Assert.True(storage.ClientSpells.ContainsKey(6001)); // Quarantine does not conceal the deliberate raw fault.
            }
            else
            {
                Assert.Null(outcome);
                Assert.True(returned == true, writerReason);
                Assert.Same(final, f.Current);
                Assert.NotSame(before, final);
                Assert.Same(f.Current.PlayerItems, binding.ItemSnapshot);
                Assert.Same(f.Current.PlayerItems, binding.Items.Image.FinalSnapshot);
                Assert.Same(originalInventory, f.Native.Inventory); Assert.Same(originalEquipment, f.Native.Equipment);
                AssertEquipmentComponentMap(f.Current.PlayerItems.InventoryItems, originalInventory.ClientItems);
                AssertEquipmentComponentMap(f.Current.PlayerItems.EquipmentItems, originalEquipment.ClientItems);
                var actualItems = originalInventory.ClientItems.Values.Concat(originalEquipment.ClientItems.Values).ToArray();
                Assert.Equal(actualItems.Length, actualItems.Select(item => item.Uid).Distinct().Count());
                Assert.Equal(targetPosition, Assert.Single(actualItems, item => item.Uid == EquipmentFixture.GearUid).Position);
                foreach (var unchanged in nativeBefore.InventoryRows.Concat(nativeBefore.EquipmentRows)
                    .Where(item => item.Uid != EquipmentFixture.GearUid))
                    Assert.Same(unchanged.Row, Assert.Single(actualItems, item => item.Uid == unchanged.Uid));
                Assert.False(binding.IsRetired);
                Assert.False(f.Recovery.IsBlocked);
                Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
                Assert.True(binding.Items.PhysicalOwnerIsCurrentPlain());
                Assert.False(storage.ClientSpells.ContainsKey(6001));
            }
        }

        if (invalidateRawMap)
        {
            var durableFiles = f.Data.Files();
            await f.Dispatch(unequip); // Genuine Frame now observes the real recovery fence.
            Assert.Empty(f.Sent);
            Assert.Null(f.CurrentOrNull);
            f.Data.AssertFiles(durableFiles);
            Assert.Contains("WorldServer trade recovery blocked world admission/actions", f.Log.ToString(), StringComparison.Ordinal);
            Assert.Contains("ordinarySpellRecoveryRequired:durableCommitPublicationUnproven", f.Log.ToString(), StringComparison.Ordinal);
        }
    }, equipped: unequip);

    private static T ReadEquipmentComponentOwner<T>(WorldServerFrameHandler handler, string fieldName) where T : class =>
        Assert.IsType<T>(typeof(WorldServerFrameHandler).GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(handler));

    private static void AssertEquipmentComponentMap(IReadOnlyList<PlayerItemSnapshotItem> expected,
        ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (var row in expected)
        {
            Assert.True(actual.TryGetValue(row.Uid, out var item)); Assert.NotNull(item);
            Assert.Equal(row.Uid, item.Uid); Assert.Equal(row.ItemId, item.ITEM_ID);
            Assert.Equal(row.Position, item.Position); Assert.Equal(row.Durability, item.Durability);
            Assert.Equal(row.MaximumDurability, item.MaximDurability);
            Assert.Equal(row.Bound, item.Bound); Assert.Equal(row.Locked, item.Locked);
        }
    }
}
