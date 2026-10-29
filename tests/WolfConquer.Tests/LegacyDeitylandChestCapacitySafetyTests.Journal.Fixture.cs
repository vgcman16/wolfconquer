using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyDeitylandChestCapacitySafetyTests
{
    private sealed partial class Fixture
    {
        private (LegacyDeityChestPersistence.Prepared Prepared, WorldSession Updated, LegacyDragonSkinInventory After)
            PrepareChestJournal(bool cp)
        {
            Assert.True(LegacyDragonSkinInventory.TryCapture(Session.PlayerItems, out var before, out var reason), reason);
            Assert.True(LegacyDeityChestPersistence.TryPrepare(Session, Items, before,
                KeyItemId, cp ? 0U : 3_306_362U, cp ? 200_000U : 0U, out var prepared, out reason), reason);
            var items = ExpectedDebit();
            var character = Session.Character;
            if (cp) character = character with { ConquerPoints = checked(character.ConquerPoints + 200_000) };
            else items = items with { InventoryItems = items.InventoryItems.Concat(new[]
                { new PlayerItemSnapshotItem { Uid = 5001, ItemId = 3_306_362, StackSize = 1 } }).ToArray() };
            var updated = Session with { Character = character, PlayerItems = items };
            Assert.True(LegacyDragonSkinInventory.TryCapture(items, out var after, out reason), reason);
            return (prepared, updated, after);
        }

        internal void AssertChestJournalRecovery(bool cp, int boundary)
        {
            var plan = PrepareChestJournal(cp);
            using var interruption = LegacyJournalInterruption.StopAfter(boundary);
            Assert.False(LegacyDeityChestPersistence.TryPersist(plan.Prepared, plan.Updated, plan.After,
                interruption, out var reason));
            Assert.StartsWith(LegacyDeityChestPersistence.RecoveryRequired, reason);
            Assert.Equal(Enumerable.Range(0, boundary + 1), interruption.Visited);
            Assert.True(LegacyTradeJournal.HasPending(Users));
            // The direct persistence component never publishes a session or sends.
            Assert.Same(Session, Current);
            Assert.Equal(itemsBefore, PlayerItemSnapshotLoader.ToLegacyBytes(Session.PlayerItems));
            Assert.Empty(Sent);
            var recovery = new LegacyTradeRecoveryState(Users, Items);
            Assert.True(recovery.TryEnsureInitialized(out reason), reason);
            Assert.True(recovery.IsReadyFor(Users, Items));
            Assert.False(LegacyTradeJournal.HasPending(Users));
            Assert.Equal(plan.After.Bytes, File.ReadAllBytes(ItemPath));
            Assert.Equal(plan.After.Bytes, PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshotLoader(Items).LoadByCharacterUid(CharacterUid)));
            if (cp) AssertCharacterCpSaved();
            else Assert.Equal(characterBefore, File.ReadAllBytes(CharacterPath));
            // Reconciliation is repeatable and has no new reward/payment prefix.
            var files = CaptureRequiredChestSavePreimage().Files;
            Assert.True(new LegacyTradeRecoveryState(Users, Items).TryEnsureInitialized(out reason), reason);
            foreach (var file in files) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
            AssertOnlyExpectedFiles();
        }

        internal void AssertPreparedChestFileReplacement(bool cp, string leaf)
        {
            var plan = PrepareChestJournal(cp);
            if (leaf == "account")
            {
                // A valid unrelated exact-byte change still retires the prepared
                // baseline; numeric parsing equivalence cannot bless it.
                File.AppendAllText(CharacterPath, "; late external change" + Environment.NewLine);
            }
            else CorruptRequiredChestSaveBaseline("itemsForeignImage");
            var before = CaptureRequiredChestSavePreimage();
            Assert.False(LegacyDeityChestPersistence.TryPersist(plan.Prepared, plan.Updated, plan.After, out var reason));
            Assert.Contains("Changed", reason);
            Assert.False(LegacyTradeJournal.HasPending(Users));
            AssertRequiredChestSaveRejected(before);
        }

        internal async Task AssertChestRecoveryAdmissionAsync(bool cp, string fault)
        {
            var context = Context;
            LegacyTradeRecoveryState? recovery = fault switch
            {
                "missing" => null,
                "foreign" => new LegacyTradeRecoveryState(Path.Combine(root, "OtherUsers"), Path.Combine(root, "OtherItems")),
                _ => new LegacyTradeRecoveryState(Users, Items),
            };
            if (fault == "foreign")
            {
                Directory.CreateDirectory(Path.Combine(root, "OtherUsers"));
                Directory.CreateDirectory(Path.Combine(root, "OtherItems"));
                Assert.True(recovery!.TryEnsureInitialized(out var reason), reason);
            }
            if (fault == "blocked")
            {
                Assert.True(recovery!.TryEnsureInitialized(out var reason), reason);
                recovery.Block("test-owned-unproven-recovery");
            }
            Assert.False(recovery?.IsReadyFor(Users, Items) ?? false);
            var before = CaptureRequiredChestSavePreimage();
            await InvokeChestOwnerAsync(context with { TradeRecovery = recovery }, Session, cp);
            AssertRequiredChestSaveRejected(before);
        }

        internal async Task AssertChestRetiredAdmissionAsync(bool cp, bool removed)
        {
            var supplied = Session;
            var context = Context;
            WorldSession? replacement = null;
            if (removed) Assert.True(sessions.TryRemove(Connection, out _));
            else
            {
                replacement = Session with { ActiveNpcId = 22_607 };
                sessions.Set(Connection, replacement);
            }
            await InvokeChestOwnerAsync(context, supplied, cp);
            if (removed) Assert.False(sessions.TryGet(Connection, out _));
            else Assert.Same(replacement, Current);
            Assert.Equal(itemsBefore, PlayerItemSnapshotLoader.ToLegacyBytes(supplied.PlayerItems));
            Assert.Equal(itemsBefore, File.ReadAllBytes(ItemPath));
            Assert.Equal(characterBefore, File.ReadAllBytes(CharacterPath));
            AssertOnlyExpectedFiles();
            Assert.Empty(Sent);
            Assert.DoesNotContain("chest item reward", Log.ToString());
            Assert.DoesNotContain("chest CP reward", Log.ToString());
        }

        private static async Task InvokeChestOwnerAsync(WorldPacketHandlerContext context, WorldSession supplied, bool cp)
        {
            var method = typeof(WorldPacketHandlers).GetMethod("TryUnlockLegacyDeitylandChestAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
            object? result;
            try { result = method.Invoke(null, [context, supplied, "component-transaction-post-roll", KeyItemId,
                cp ? 0U : 3_306_362U, cp ? 200_000U : 0U, CancellationToken.None]); }
            catch (TargetInvocationException error) when (error.InnerException is not null)
            { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
            await Assert.IsType<ValueTask>(result);
        }
    }
}
