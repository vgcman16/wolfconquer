using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacySocialCraftingServicesNpcTests
{
    // Genuine public-owner proof, matching the existing class fixture. This does
    // not claim registered ingress, authentication or the future UID-host cutover.
    private sealed class RuneFragmentBoundFixture : IDisposable
    {
        private const uint RewardId = 4_012_001;
        private readonly string root = Path.Combine(Path.GetTempPath(),
            "wolf-rune-bound-npc-" + Guid.NewGuid().ToString("N"));
        private readonly WorldDataCatalog catalog;
        private readonly byte[] baseline;
        internal List<byte[]> Sent { get; } = [];
        internal StringWriter Log { get; } = new();
        internal WorldSessionStore Sessions { get; } = new();
        internal ConquerTcpConnection Connection { get; }
        internal WorldSession Original { get; }

        internal RuneFragmentBoundFixture(PlayerItemSnapshotItem[] rows)
        {
            ValidateOwnedDomain();
            var items = PlayerItemSnapshot.Empty with { InventoryItems = rows };
            Directory.CreateDirectory(ItemsRoot);
            new PlayerItemSnapshotLoader(ItemsRoot).SaveByCharacterUid(CharacterUid, items);
            baseline = File.ReadAllBytes(ItemPath);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(items), baseline);
            Original = CreateSession(conquerPoints: 123_456, level: 140, mapId: 1_002, x: 430, y: 380,
                playerItems: items, characterAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                { ["LegacyRuneFragmentCompleteRuneIndex"] = "0" });
            Connection = CreateSendingConnection(Sent);
            Sessions.Set(Connection, Original);
            catalog = CreateWorldDataCatalog(
                CreateItemType(RuneFragmentItemId, "RedRuneFragment", durability: 1),
                CreateItemType(RewardId, "BoundRune", durability: 610));
        }

        internal WorldSession Current
        {
            get
            {
                Assert.True(Sessions.TryGet(Connection, out var current));
                return Assert.IsType<WorldSession>(current);
            }
        }

        internal async Task ApplyAsync()
        {
            var context = new WorldPacketHandlerContext(Connection, Log, Session: Original,
                Sessions: Sessions, WorldData: catalog, PlayerItemsRoot: ItemsRoot);
            Assert.True(await WorldPacketHandlers.TryHandleLegacySocialCraftingServicesNpcAsync(
                context, Original, RuneFragmentItemId, optionId: 1, input: "", cancellationToken: CancellationToken.None));
        }

        internal PlayerItemSnapshotItem AssertReward()
        {
            var reward = Assert.Single(Current.PlayerItems.InventoryItems, item => item.ItemId == RewardId);
            Assert.True(reward.Uid > 0);
            Assert.DoesNotContain(Original.PlayerItems.InventoryItems, item => item.Uid == reward.Uid);
            AssertPhysicalItemEqual(new PlayerItemSnapshotItem
            {
                Uid = reward.Uid, ItemId = RewardId, Position = 0, StackSize = 1,
                Bound = 1, Durability = 610, MaximumDurability = 610,
            }, reward);
            return reward;
        }

        internal static void AssertPhysicalItemEqual(PlayerItemSnapshotItem expected, PlayerItemSnapshotItem actual)
        {
            Assert.Equal(
                PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty with { InventoryItems = [expected] }),
                PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty with { InventoryItems = [actual] }));
        }

        internal void AssertRewardFrames(PlayerItemSnapshotItem reward, int firstRewardFrame)
        {
            Assert.Equal(firstRewardFrame + 2, Sent.Count);
            Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(reward,
                LegacyGameItemPacket.CreateRuneSpellOverride(reward, catalog.ItemsById),
                LegacyGameItemPacket.AddItemMode).ToFrameBytes(), Sent[firstRewardFrame]);
            Assert.Equal(LegacyMsgMessagePacket.SystemTalk("You have got 1 BoundRune(B).").ToFrameBytes(),
                Sent[firstRewardFrame + 1]);
        }

        internal void AssertCommitted(PlayerItemSnapshotItem[] expectedRows)
        {
            var current = Current;
            Assert.NotSame(Original, current);
            Assert.Same(Original.Character, current.Character);
            Assert.Equal(Original with { PlayerItems = current.PlayerItems }, current);
            var expected = Original.PlayerItems with { InventoryItems = expectedRows };
            var bytes = PlayerItemSnapshotLoader.ToLegacyBytes(expected);
            Assert.Equal(bytes, PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems));
            Assert.Equal(bytes, File.ReadAllBytes(ItemPath));
            Assert.Equal(bytes, PlayerItemSnapshotLoader.ToLegacyBytes(
                new PlayerItemSnapshotLoader(ItemsRoot).LoadByCharacterUid(CharacterUid)));
            Assert.Equal(baseline, PlayerItemSnapshotLoader.ToLegacyBytes(Original.PlayerItems));
            Assert.Contains("handled LegacyNpc RuneFragments complete rune", Log.ToString());
            Assert.DoesNotContain("PARITY-GAP LegacyNpc RuneFragments", Log.ToString());
        }

        internal void AssertUnchangedRefusal()
        {
            Assert.Same(Original, Current);
            Assert.Same(Original.PlayerItems, Current.PlayerItems);
            Assert.Equal(baseline, PlayerItemSnapshotLoader.ToLegacyBytes(Original.PlayerItems));
            Assert.Equal(baseline, File.ReadAllBytes(ItemPath));
            Assert.Equal(baseline, PlayerItemSnapshotLoader.ToLegacyBytes(
                new PlayerItemSnapshotLoader(ItemsRoot).LoadByCharacterUid(CharacterUid)));
            Assert.Equal(LegacyMsgMessagePacket.BoxDialog(
                "Please make sure you have 20 fragments to make a Rune.").ToFrameBytes(), Assert.Single(Sent));
            Assert.Contains("skipped LegacyNpc RuneFragments complete rune", Log.ToString());
            Assert.DoesNotContain("handled LegacyNpc RuneFragments complete rune", Log.ToString());
            Assert.DoesNotContain("PARITY-GAP LegacyNpc RuneFragments", Log.ToString());
        }

        private string ItemsRoot => Path.Combine(root, "PlayersItems");
        private string ItemPath => Path.Combine(ItemsRoot, "1293600.bin");

        private void ValidateOwnedDomain()
        {
            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            Assert.Equal(temp, Path.GetDirectoryName(full));
            const string prefix = "wolf-rune-bound-npc-";
            var name = Path.GetFileName(full);
            Assert.StartsWith(prefix, name);
            Assert.True(Guid.TryParseExact(name[prefix.Length..], "N", out _));
            Assert.Equal(full, Path.GetDirectoryName(Path.GetFullPath(ItemsRoot)));
            Assert.Equal(Path.GetFullPath(ItemsRoot), Path.GetDirectoryName(Path.GetFullPath(ItemPath)));
            for (string? current = ItemPath; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if (File.Exists(current) || Directory.Exists(current))
                    Assert.Equal((FileAttributes)0, File.GetAttributes(current) & FileAttributes.ReparsePoint);
        }

        public void Dispose()
        {
            Log.Dispose();
            ValidateOwnedDomain();
            if (File.Exists(ItemPath)) File.Delete(ItemPath);
            if (Directory.Exists(ItemsRoot)) Directory.Delete(ItemsRoot, recursive: false);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: false);
        }
    }
}
