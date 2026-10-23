using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyShopCapacityTests
{
    private const uint OwnerUid = 1_293_600;
    private const uint RewardId = 1_000_000;
    private sealed class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(),
            "wolf-shop-capacity-" + Guid.NewGuid().ToString("N"));
        private readonly byte[] characterBefore;
        private readonly byte[] itemsBefore;
        private readonly WorldDataCatalog catalog = Catalog();
        private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> Registry =
            PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        internal List<byte[]> Sent { get; } = [];
        internal StringWriter Log { get; } = new();
        internal WorldSessionStore Sessions { get; } = new();
        internal ConquerTcpConnection Connection { get; }
        internal WorldSession Original { get; }

        internal Fixture(int inventoryCount)
        {
            ValidateOwnedPaths();
            Directory.CreateDirectory(Users);
            Directory.CreateDirectory(Items);
            File.WriteAllText(CharacterPath, Snapshot());
            var inventory = Enumerable.Range(0, inventoryCount).Select(index => new PlayerItemSnapshotItem
            {
                Uid = checked(5_700_000U + (uint)index), ItemId = 2_300_001, Position = 0,
                StackSize = 1, Bound = (byte)(index % 2), Durability = 77, MaximumDurability = 100,
                Color = 4, RuneExperience = checked((uint)index),
            }).ToArray();
            new PlayerItemSnapshotLoader(Items).SaveByCharacterUid(OwnerUid,
                PlayerItemSnapshot.Empty with { InventoryItems = inventory });
            var loader = new CharacterSnapshotLoader(Users, Items);
            var character = Assert.IsType<CharacterSnapshot>(loader.LoadByAccountId(OwnerUid));
            var loadedItems = loader.LoadPlayerItemsByCharacterUid(OwnerUid);
            Assert.Equal(inventoryCount, loadedItems.InventoryItems.Count);
            Original = new(OwnerUid, "owned-capacity", character, loadedItems,
                PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty, UsersRoot: Users);
            Connection = new(null, new IPEndPoint(IPAddress.Loopback, 9958), (bytes, token) =>
            {
                token.ThrowIfCancellationRequested();
                Sent.Add(bytes.ToArray());
                return ValueTask.CompletedTask;
            });
            Sessions.Set(Connection, Original);
            characterBefore = File.ReadAllBytes(CharacterPath);
            itemsBefore = File.ReadAllBytes(ItemPath);
        }

        internal WorldSession Current
        {
            get
            {
                Assert.True(Sessions.TryGet(Connection, out var current));
                return Assert.IsType<WorldSession>(current);
            }
        }

        internal async Task BuyAsync(uint shop, uint count)
        {
            var request = new MsgItemUsagePacket(shop, RewardId, MsgItemUsageAction.BuyItem,
                1244, count, shop == 5 ? 2U : 0U, 0);
            var context = new WorldPacketHandlerContext(Connection, Log, Original,
                ArenaStatistics: ArenaStatisticsProvider.Empty, ArenaStates: new ArenaSessionStateStore(),
                WorldData: catalog, Sessions: Sessions, PlayerItemsRoot: Items);
            Assert.True(Registry.Opcodes.Contains(WorldPacketOpcodes.MsgItemUsage));
            await Registry.HandleAsync(context, Frame(request.ToFrameBytes()));
        }

        internal void AssertRefused()
        {
            Assert.Same(Original, Current);
            Assert.Same(Original.PlayerItems, Current.PlayerItems);
            Assert.Empty(Sent);
            Assert.Equal(characterBefore, File.ReadAllBytes(CharacterPath));
            Assert.Equal(itemsBefore, File.ReadAllBytes(ItemPath));
            Assert.Equal(itemsBefore, PlayerItemSnapshotLoader.ToLegacyBytes(Original.PlayerItems));
            var loader = new CharacterSnapshotLoader(Users, Items);
            Assert.Equal(itemsBefore, PlayerItemSnapshotLoader.ToLegacyBytes(loader.LoadPlayerItemsByCharacterUid(OwnerUid)));
            Assert.Contains("inventory capacity is insufficient", Log.ToString());
            Assert.DoesNotContain("WorldServer handled MsgItemUsage buy ", Log.ToString());
            Assert.DoesNotContain("WorldServer persisted MsgItemUsage", Log.ToString());
            Assert.DoesNotContain("PARITY-GAP", Log.ToString());
        }

        internal void AssertCommittedItems(IReadOnlyList<PlayerItemSnapshotItem> added)
        {
            var current = Current;
            var expected = Original.PlayerItems with
            {
                InventoryItems = Original.PlayerItems.InventoryItems.Concat(added).ToArray(),
            };
            Assert.NotSame(Original, current);
            Assert.Equal(Original with { Character = current.Character, PlayerItems = current.PlayerItems }, current);
            Assert.InRange(current.PlayerItems.InventoryItems.Count, 1, 40);
            for (var index = 0; index < Original.PlayerItems.InventoryItems.Count; index++)
                Assert.Same(Original.PlayerItems.InventoryItems[index], current.PlayerItems.InventoryItems[index]);
            var bytes = PlayerItemSnapshotLoader.ToLegacyBytes(expected);
            Assert.Equal(bytes, PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems));
            Assert.Equal(bytes, File.ReadAllBytes(ItemPath));
            Assert.Equal(bytes, PlayerItemSnapshotLoader.ToLegacyBytes(
                new PlayerItemSnapshotLoader(Items).LoadByCharacterUid(OwnerUid)));
            Assert.Equal(itemsBefore, PlayerItemSnapshotLoader.ToLegacyBytes(Original.PlayerItems));
            Assert.DoesNotContain("PARITY-GAP", Log.ToString());
        }

        internal CharacterSnapshot ReloadCharacter() =>
            Assert.IsType<CharacterSnapshot>(new CharacterSnapshotLoader(Users, Items).LoadByAccountId(OwnerUid));

        private string Users => Path.Combine(root, "Users");
        private string Items => Path.Combine(root, "PlayersItems");
        private string CharacterPath => Path.Combine(Users, "1293600.ini");
        private string ItemPath => Path.Combine(Items, "1293600.bin");

        private static string Snapshot() => $$"""
            [Character]
            UID={{OwnerUid}}
            Name=CapacityOwner
            Body=1008
            Face=154
            Haire=27
            Class=10
            FirstClass=0
            SecoundeClass=0
            Reborn=0
            Level=100
            Experience=0
            Map=1002
            X=430
            Y=380
            PMap=1002
            PMapX=430
            PMapY=380
            Strength=10
            Agility=10
            Vitaliti=10
            Spirit=10
            Atributes=0
            MinHitPoints=1000
            MinMana=100
            Money=1000
            ConquerPoints=1000
            BoundConquerPoints=1000
            BankCps=1000
            PkPoints=0
            Online=1
            RacePoints=1000
            ArenaCurrentHonor=1000
            ChampionPoints=1000
            Sentinel=unchanged
            """;

        private void ValidateOwnedPaths()
        {
            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            Assert.Equal(temp, Path.GetDirectoryName(full));
            const string prefix = "wolf-shop-capacity-";
            var name = Path.GetFileName(full);
            Assert.StartsWith(prefix, name);
            Assert.True(Guid.TryParseExact(name[prefix.Length..], "N", out _));
            foreach (var path in new[] { CharacterPath, ItemPath })
            {
                Assert.StartsWith(full + Path.DirectorySeparatorChar, Path.GetFullPath(path),
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
                for (string? current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                    if (File.Exists(current) || Directory.Exists(current))
                        Assert.Equal((FileAttributes)0, File.GetAttributes(current) & FileAttributes.ReparsePoint);
            }
        }

        public void Dispose()
        {
            Log.Dispose();
            ValidateOwnedPaths();
            if (File.Exists(CharacterPath)) File.Delete(CharacterPath);
            if (File.Exists(ItemPath)) File.Delete(ItemPath);
            if (Directory.Exists(Users)) Directory.Delete(Users, recursive: false);
            if (Directory.Exists(Items)) Directory.Delete(Items, recursive: false);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: false);
        }
    }

    private static PacketFrame Frame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
