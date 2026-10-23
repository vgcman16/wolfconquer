using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyDeitylandChestCapacitySafetyTests
{
    private sealed partial class Fixture
    {
        private readonly WorldOperationBarrier requiredChestOperations = new();
        private readonly LegacyTradeStateStore requiredChestTrades = new();
        private LegacyTradeRecoveryState? requiredChestRecovery;

        internal sealed record RequiredChestSavePreimage(
            WorldSession Session, byte[] SessionBytes, byte[] PhysicalItems,
            Dictionary<string, byte[]> Files);

        private void InitializeRequiredChestSaveOwners()
        {
            requiredChestRecovery = new LegacyTradeRecoveryState(Users, Items);
            Assert.True(requiredChestRecovery.TryEnsureInitialized(out var reason), reason);
            Assert.True(requiredChestRecovery.IsReadyFor(Session.UsersRoot, Items));
        }

        internal void AssertRequiredChestSaveOwnersReady()
        {
            var recovery = Assert.IsType<LegacyTradeRecoveryState>(requiredChestRecovery);
            Assert.False(recovery.IsBlocked);
            Assert.True(recovery.IsReadyFor(Session.UsersRoot, Items));
            Assert.Same(requiredChestOperations, Context.TradeOperations);
            Assert.Same(requiredChestTrades, Context.Trades);
            Assert.Same(recovery, Context.TradeRecovery);
        }

        internal void CorruptRequiredChestSaveBaseline(string fault)
        {
            switch (fault)
            {
                case "accountMissing":
                    File.Delete(CharacterPath); // Exact owned temporary account leaf only.
                    break;
                case "accountForeignUid":
                    File.WriteAllText(CharacterPath, Encoding.UTF8.GetString(characterBefore)
                        .Replace("UID=1293600", "UID=301", StringComparison.Ordinal), new UTF8Encoding(false, true));
                    break;
                case "accountAmbiguousFields":
                    File.WriteAllText(CharacterPath, Encoding.UTF8.GetString(characterBefore)
                        .Replace("ConquerPoints=55" + Environment.NewLine,
                            "ConquerPoints=55" + Environment.NewLine + "conquerpoints=55" + Environment.NewLine,
                            StringComparison.Ordinal), new UTF8Encoding(false, true));
                    break;
                case "itemsMissing":
                    File.Delete(ItemPath); // Nonempty live inventory still requires its real baseline.
                    break;
                case "itemsForeignImage":
                    var rows = Session.PlayerItems.InventoryItems.ToArray();
                    rows[0] = Item(901, rows[0].ItemId, rows[0].StackSize).WithBound(rows[0].Bound);
                    File.WriteAllBytes(ItemPath, PlayerItemSnapshotLoader.ToLegacyBytes(
                        Session.PlayerItems with { InventoryItems = rows }));
                    break;
                case "itemsTruncated":
                    File.WriteAllBytes(ItemPath, [1, 2, 3]);
                    break;
                default:
                    throw new ArgumentException("Unknown owned-file fault.", nameof(fault));
            }
        }

        internal RequiredChestSavePreimage CaptureRequiredChestSavePreimage()
        {
            var current = Current;
            return new(current, JsonSerializer.SerializeToUtf8Bytes(current),
                PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems),
                Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                    .ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal));
        }

        internal void AssertRequiredChestSaveRejected(RequiredChestSavePreimage before)
        {
            Assert.Same(before.Session, Current);
            Assert.Equal(before.SessionBytes, JsonSerializer.SerializeToUtf8Bytes(Current));
            Assert.Equal(before.PhysicalItems, PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems));
            Assert.Equal(before.PhysicalItems, PlayerItemSnapshotLoader.ToLegacyBytes(before.Session.PlayerItems));
            Assert.Empty(Sent);
            Assert.Equal(before.Files.Keys.Order(StringComparer.Ordinal),
                Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
            foreach (var file in before.Files)
                Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
            Assert.DoesNotContain("chest item reward", Log.ToString());
            Assert.DoesNotContain("chest CP reward", Log.ToString());
        }

        internal async Task UnlockWithRequiredChestSaveLockAsync(bool cpReward, bool accountFile)
        {
            var lease = new FileStream(accountFile ? CharacterPath : ItemPath,
                FileMode.Open, FileAccess.Read, FileShare.None);
            var errors = new List<Exception>();
            try
            {
                await UnlockSelectedAsync(
                    rewardItemId: cpReward ? 0U : 3_306_362U, conquerPointsReward: cpReward ? 200_000U : 0U);
            }
            catch (Exception error) { errors.Add(error); }
            try { lease.Dispose(); }
            catch (Exception error) { errors.Add(error); }
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException(errors);
        }
    }
}
