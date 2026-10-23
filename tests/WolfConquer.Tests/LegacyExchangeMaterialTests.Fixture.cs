using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyExchangeMaterialTests
{
    private sealed class Fixture : IDisposable
    {
        private readonly string parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        internal readonly string Root;
        internal string Users => Path.Combine(Root, "Users");
        internal string Items => Path.Combine(Root, "PlayersItems");
        internal string CharacterFile => Path.Combine(Users, "11.ini");
        internal string ItemFile => Path.Combine(Items, "11.bin");
        internal WorldSession Original { get; }
        internal WorldSessionStore Sessions { get; } = new();
        internal List<byte[]> Sent { get; } = new();
        internal StringWriter Log { get; } = new();
        internal ConquerTcpConnection Connection { get; }
        internal WorldPacketHandlerContext Context { get; }

        internal Fixture(ExchangeShopRow row, PlayerItemSnapshotItem[] inventory,
            bool includeReward = true, uint? purchased = null)
        {
            Root = Path.Combine(parent, "wolf-exchange-materials-" + Guid.NewGuid().ToString("N"));
            Guard();
            Directory.CreateDirectory(Users); Directory.CreateDirectory(Items);
            File.WriteAllText(CharacterFile, CharacterText());
            var items = PlayerItemSnapshot.Empty with { InventoryItems = inventory };
            File.WriteAllBytes(ItemFile, PlayerItemSnapshotLoader.ToLegacyBytes(items));
            var character = CharacterSnapshotParser.ParseText(CharacterText(), CharacterFile);
            Original = new(11, "ExchangeFixture", character, items,
                PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty, UsersRoot: Users,
                LegacyExchangeShopBuyCounts: purchased.HasValue ? new Dictionary<uint, uint> { [444] = purchased.Value } : null);
            Connection = new(null, new IPEndPoint(IPAddress.Loopback, 9958), (bytes, token) =>
            { token.ThrowIfCancellationRequested(); Sent.Add(bytes.ToArray()); return ValueTask.CompletedTask; });
            Sessions.Set(Connection, Original);
            Context = new(Connection, Log, Original, WorldData: ExchangeCatalog([row], includeReward),
                Sessions: Sessions, PlayerItemsRoot: Items);
        }

        internal Task Buy(uint count = 1, uint shop = 23, uint index = 444, CancellationToken token = default) =>
            Dispatch(Context, new LegacyExchangeShopBuyPacket(LegacyExchangeShopBuyType.Goods,
                24702, shop, count, index, []), token);

        internal WorldSession Current()
        {
            Assert.True(Sessions.TryGet(Connection, out var current));
            return Assert.IsType<WorldSession>(current);
        }

        internal void AssertRejected(byte[] character, byte[] items)
        {
            Assert.Same(Original, Current()); Assert.Empty(Sent);
            Assert.Equal(character, File.ReadAllBytes(CharacterFile)); Assert.Equal(items, File.ReadAllBytes(ItemFile));
            Assert.Same(Original.PlayerItems, Current().PlayerItems);
            Assert.Same(Original.Character, Current().Character);
        }

        private static string CharacterText() => """
            [Character]
            UID=11
            Name=ExchangeFixture
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
            BoundConquerPoints=0
            BankCps=0
            PkPoints=0
            Online=1
            Sentinel=unchanged
            """;

        private void Guard()
        {
            Assert.Equal(parent, Path.GetDirectoryName(Path.GetFullPath(Root)));
            const string prefix = "wolf-exchange-materials-";
            Assert.StartsWith(prefix, Path.GetFileName(Root));
            Assert.True(Guid.TryParseExact(Path.GetFileName(Root)[prefix.Length..], "N", out _));
            Ordinary(Root); Ordinary(parent); Ordinary(Users); Ordinary(Items);
        }

        private static void Ordinary(string path)
        {
            for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
                try { Assert.Equal((FileAttributes)0, File.GetAttributes(current) & FileAttributes.ReparsePoint); }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
        }

        public void Dispose()
        {
            Guard();
            foreach (var file in new[] { CharacterFile, ItemFile })
            {
                Ordinary(file);
                Assert.StartsWith(Root + Path.DirectorySeparatorChar, Path.GetFullPath(file), StringComparison.OrdinalIgnoreCase);
                if (File.Exists(file)) File.Delete(file);
            }
            Directory.Delete(Users, false); Directory.Delete(Items, false); Directory.Delete(Root, false);
            Log.Dispose();
        }
    }

    private static Task Dispatch(WorldPacketHandlerContext context, LegacyExchangeShopBuyPacket request,
        CancellationToken token = default) =>
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)])
            .HandleAsync(context, Frame(request.ToFrameBytes()), token).AsTask();

    private static PacketFrame Frame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static PlayerItemSnapshotItem Stock(uint uid, uint itemId, ushort count, byte bound = 0) =>
        new() { Uid = uid, ItemId = itemId, Position = 0, StackSize = count, Bound = bound };
}
