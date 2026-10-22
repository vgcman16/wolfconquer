using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Registry/real-file compatibility proof only, not native transport birth or
// live-client proof. Bound ordinary items avoid unrelated floor placement.
public sealed class LegacyDropSecondaryPasswordCompatibilityTests : IDisposable
{
    private const uint Uid = 1_299_211;
    private const uint DroppedUid = 601_020_001;
    private const uint KeptUid = 601_020_002;
    private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-drop-pin-compat-" + Guid.NewGuid().ToString("N"));
    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> Handlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    public LegacyDropSecondaryPasswordCompatibilityTests()
    {
        Directory.CreateDirectory(UsersRoot);
        Directory.CreateDirectory(ItemsRoot);
    }

    private string UsersRoot => Path.Combine(root, "Users");
    private string ItemsRoot => Path.Combine(root, "PlayersItems");
    private string CharacterPath => Path.Combine(UsersRoot, Uid + ".ini");
    private string ItemsPath => Path.Combine(ItemsRoot, Uid + ".bin");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WhollyUnboundZeroPinKeepsBoundDropBehaviorWithOrWithoutStore(bool withStore)
    {
        var original = CreateSession("0,0,0");
        SaveBaseline(original);
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var publications = 0;
        var sessions = withStore ? new WorldSessionStore((_, _, _) => publications++) : null;
        sessions?.Set(connection, original);
        var beforePublications = publications;
        var characterBytes = File.ReadAllBytes(CharacterPath);
        using var log = new StringWriter();
        Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(connection, original));

        await Dispatch(connection, original, sessions, sent, log);

        AssertRemoval(sent);
        var expected = WithoutDroppedItem(original.PlayerItems);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expected), File.ReadAllBytes(ItemsPath));
        Assert.Equal(characterBytes, File.ReadAllBytes(CharacterPath));
        Assert.Equal(1_000, original.Character.Money);
        Assert.Equal(2, original.PlayerItems.InventoryItems.Count);
        if (sessions is not null)
        {
            Assert.True(sessions.TryGet(connection, out var updated));
            Assert.NotNull(updated);
            Assert.NotSame(original, updated);
            Assert.Same(original.Character, updated.Character);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expected), PlayerItemSnapshotLoader.ToLegacyBytes(updated.PlayerItems));
            // The persisted transaction publishes the holder itself and must
            // not invoke a second ordinary auto-save callback after the WAL.
            Assert.Equal(beforePublications, publications);
        }
        else Assert.Equal(beforePublications, publications);
    }

    [Theory]
    [InlineData("111,0,0", false)]
    [InlineData("111,0,0", true)]
    [InlineData("malformed", false)]
    [InlineData("malformed", true)]
    public async Task WhollyUnboundProtectedOrMalformedPinDeniesWithoutMutation(string pin, bool withStore)
    {
        var original = CreateSession(pin);
        SaveBaseline(original);
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var publications = 0;
        var sessions = withStore ? new WorldSessionStore((_, _, _) => publications++) : null;
        sessions?.Set(connection, original);
        var beforePublications = publications;
        var characterBytes = File.ReadAllBytes(CharacterPath);
        var itemBytes = File.ReadAllBytes(ItemsPath);
        using var log = new StringWriter();
        Assert.False(LegacySecondaryPasswordRuntime.IsAdmitted(connection, original));

        await Dispatch(connection, original, sessions, sent, log);

        Assert.Empty(sent);
        Assert.Equal(beforePublications, publications);
        Assert.Equal(characterBytes, File.ReadAllBytes(CharacterPath));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemsPath));
        Assert.Equal(1_000, original.Character.Money);
        Assert.Equal(2, original.PlayerItems.InventoryItems.Count);
        if (sessions is not null)
        {
            Assert.True(sessions.TryGet(connection, out var current));
            Assert.Same(original, current);
        }
    }

    [Fact]
    public async Task MissingCurrentStoreEntryDoesNotFallBackToOrResurrectStaleZeroPinSession()
    {
        var stale = CreateSession("0,0,0");
        SaveBaseline(stale);
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var publications = 0;
        var sessions = new WorldSessionStore((_, _, _) => publications++);
        sessions.Set(connection, stale);
        Assert.True(sessions.TryRemove(connection, out var removed));
        Assert.Same(stale, removed);
        var beforePublications = publications;
        var characterBytes = File.ReadAllBytes(CharacterPath);
        var itemBytes = File.ReadAllBytes(ItemsPath);
        using var log = new StringWriter();

        await Dispatch(connection, stale, sessions, sent, log);

        Assert.Empty(sent);
        Assert.False(sessions.TryGet(connection, out _));
        Assert.Equal(beforePublications, publications);
        Assert.Equal(characterBytes, File.ReadAllBytes(CharacterPath));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemsPath));
        Assert.Equal(1_000, stale.Character.Money);
        Assert.Equal(2, stale.PlayerItems.InventoryItems.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaleContextUsesCurrentStoredPinItemsAndWallet(bool currentProtected)
    {
        var stale = CreateSession(currentProtected ? "0,0,0" : "111,0,0");
        var current = CreateSession(currentProtected ? "111,0,0" : "0,0,0", money: 900, variation: 1);
        SaveBaseline(current);
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var publications = 0;
        var sessions = new WorldSessionStore((_, _, _) => publications++);
        sessions.Set(connection, current);
        var beforePublications = publications;
        var characterBytes = File.ReadAllBytes(CharacterPath);
        var itemBytes = File.ReadAllBytes(ItemsPath);
        using var log = new StringWriter();

        await Dispatch(connection, stale, sessions, sent, log);

        Assert.True(sessions.TryGet(connection, out var stored));
        Assert.NotNull(stored);
        Assert.Equal(900, stored.Character.Money);
        Assert.Same(current.Character, stored.Character);
        Assert.Equal(characterBytes, File.ReadAllBytes(CharacterPath));
        if (currentProtected)
        {
            Assert.Empty(sent);
            Assert.Same(current, stored);
            Assert.Equal(beforePublications, publications);
            Assert.Equal(itemBytes, File.ReadAllBytes(ItemsPath));
        }
        else
        {
            AssertRemoval(sent);
            Assert.NotSame(current, stored);
            Assert.Equal(beforePublications, publications);
            var expected = WithoutDroppedItem(current.PlayerItems);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expected), PlayerItemSnapshotLoader.ToLegacyBytes(stored.PlayerItems));
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expected), File.ReadAllBytes(ItemsPath));
            Assert.Equal(KeptUid + 1, Assert.Single(stored.PlayerItems.InventoryItems).Uid);
        }
        Assert.Equal(1_000, stale.Character.Money);
        Assert.Equal(KeptUid, stale.PlayerItems.InventoryItems[1].Uid);
        Assert.Equal((ushort)1, stale.PlayerItems.InventoryItems[0].StackSize);
    }

    [Theory]
    [InlineData(0)] // Native connection only; managed session has no native owner.
    [InlineData(1)] // Native session only; connection has no native owner.
    [InlineData(2)] // A same-UID Player physically owned by a foreign client.
    public async Task PartialOrForeignNativeOwnershipNeverUsesZeroPinCompatibilityFallback(int scenario)
    {
        var original = CreateSession("0,0,0");
        var native = new LegacyNativeGameClientRuntime();
        native.Player.UID = Uid;
        native.Player.IsCheckedPass = true;
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent, scenario == 1 ? null : native);
        var session = original with { NativeGameClient = scenario == 0 ? null : native };
        if (scenario == 2)
        {
            Assert.True(LegacySecondaryPasswordState.TryRead(session.Character.Attributes, out var state, out _));
            Assert.True(LegacySecondaryPasswordRuntime.TryInitialize(connection, session, state));
            Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(connection, session));
            var foreignOwner = new LegacyNativeGameClientRuntime();
            native.Player = new LegacyNativePlayerRuntime(foreignOwner)
            {
                UID = Uid,
                SecurityPassword = 0,
                IsCheckedPass = true,
            };
        }
        SaveBaseline(session);
        var publications = 0;
        var sessions = new WorldSessionStore((_, _, _) => publications++);
        sessions.Set(connection, session);
        var beforePublications = publications;
        var characterBytes = File.ReadAllBytes(CharacterPath);
        var itemBytes = File.ReadAllBytes(ItemsPath);
        var physical = native.Player;
        using var log = new StringWriter();
        Assert.False(LegacySecondaryPasswordRuntime.IsAdmitted(connection, session));

        await Dispatch(connection, session, sessions, sent, log);

        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var stored));
        Assert.Same(session, stored);
        Assert.Equal(beforePublications, publications);
        Assert.Same(physical, native.Player);
        Assert.True(physical.IsCheckedPass);
        Assert.Equal(1_000, session.Character.Money);
        Assert.Equal(characterBytes, File.ReadAllBytes(CharacterPath));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemsPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private async Task Dispatch(ConquerTcpConnection connection, WorldSession supplied, WorldSessionStore? sessions,
        List<byte[]> sent, TextWriter log)
    {
        Assert.Empty(sent);
        await Handlers.HandleAsync(new WorldPacketHandlerContext(connection, log, supplied,
                Sessions: sessions, PlayerItemsRoot: ItemsRoot,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13),
            ReadFrame(new MsgItemUsagePacket(DroppedUid, 0, MsgItemUsageAction.DropItem, 0, 0, 0, 0).ToFrameBytes()));
    }

    private static ConquerTcpConnection CreateConnection(List<byte[]> sent, LegacyNativeGameClientRuntime? native = null) =>
        new(null, new IPEndPoint(IPAddress.Loopback, 9958), (bytes, _) =>
        {
            sent.Add(bytes.ToArray());
            return ValueTask.CompletedTask;
        }, nativeGameClient: native);

    private static void AssertRemoval(IReadOnlyList<byte[]> sent)
    {
        var bytes = Assert.Single(sent);
        Assert.Equal(new MsgItemUsagePacket(DroppedUid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes(), bytes);
    }

    private static PlayerItemSnapshot WithoutDroppedItem(PlayerItemSnapshot items) => items with
    {
        InventoryItems = items.InventoryItems.Where(item => item.Uid != DroppedUid).ToArray(),
    };

    private WorldSession CreateSession(string pin, long money = 1_000, uint variation = 0)
    {
        var character = CharacterSnapshotParser.ParseText(CharacterText(pin, money), CharacterPath);
        var dropped = new PlayerItemSnapshotItem
        {
            Uid = DroppedUid, ItemId = 50_000, Bound = 1, StackSize = (ushort)(1 + variation),
            Durability = 10, MaximumDurability = 10, Color = 5, Plus = (byte)variation,
        };
        var kept = new PlayerItemSnapshotItem { Uid = KeptUid + variation, ItemId = 50_000, Bound = 1, StackSize = 1 };
        var warehouse = new PlayerItemSnapshotItem { Uid = KeptUid + 100 + variation, ItemId = 50_000, StackSize = 1, WarehouseId = 1000 };
        return new WorldSession(Uid, "drop-pin-compat", character,
            new PlayerItemSnapshot([dropped, kept], [], [], [], []) { WarehouseItems = [warehouse] },
            PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty, UsersRoot: UsersRoot);
    }

    private void SaveBaseline(WorldSession session)
    {
        File.WriteAllText(CharacterPath, CharacterText(session.Character.Attributes["SecurityPass"], session.Character.Money));
        new PlayerItemSnapshotLoader(ItemsRoot).SaveByCharacterUid(Uid, session.PlayerItems);
    }

    private static string CharacterText(string pin, long money) => $$"""
        [Character]
        UID={{Uid}}
        Name=DropPinCompat
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
        Money={{money}}
        ConquerPoints=50
        BoundConquerPoints=333
        BankCps=444
        PkPoints=0
        Online=1
        SecurityPass={{pin}}
        Sentinel=keep-me

        """;

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
