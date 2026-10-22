using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNpcSaleTransactionTests : IDisposable
{
    private const uint NpcUid = 20_499;
    private const uint ItemId = 50_000;
    private const uint SoldUid = 601_010_001;
    private const uint KeptUid = 601_010_002;
    private const long InventoryMoneyCap = 500_000_000_000;
    private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-npc-sale-" + Guid.NewGuid().ToString("N"));
    private readonly List<Player> players = [];
    private readonly NativeLoginBeastMetadataScope beastMetadata;

    public LegacyNpcSaleTransactionTests()
    {
        Directory.CreateDirectory(UsersRoot);
        Directory.CreateDirectory(ItemsRoot);
        // The class already uses the globally nonparallel DeferredMonsterMoneyFloorUid
        // collection (DropVisibility partial), which also isolates this actual cache scope.
        // Native LoadCharacter773 needs this metadata even for zero saved Beast points.
        beastMetadata = new NativeLoginBeastMetadataScope(1000);
    }

    public void Dispose()
    {
        try
        {
            foreach (var player in players) player.Lifetime.Dispose();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
        finally
        {
            beastMetadata.Dispose(); // Restore the exact prior cache even if file cleanup fails.
        }
    }

    private string UsersRoot => Path.Combine(root, "Users");
    private string ItemsRoot => Path.Combine(root, "PlayersItems");
    private string PendingPath => Path.Combine(UsersRoot, ".managed-trade-journal", "pending.json");
    private string UserPath(uint uid) => Path.Combine(UsersRoot, uid.ToString(CultureInfo.InvariantCulture) + ".ini");
    private string ItemPath(uint uid) => Path.Combine(ItemsRoot, uid.ToString(CultureInfo.InvariantCulture) + ".bin");

    private Owner NewOwner(uint goldWorth = 900, LegacyLocalServerIdentity? identity = null,
        ushort stackLimit = 0, uint lifetimeMinutes = 0, uint catalogItemId = ItemId, string catalogDurability = "10")
    {
        var log = new StringWriter();
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(UsersRoot, ItemsRoot),
            handoffService: null, worldData: CreateWorldData(goldWorth, stackLimit, lifetimeMinutes, catalogItemId, catalogDurability), runtimeUpdateLoop: LegacyRuntimeUpdateLoopOptions.Disabled with { LocalServerIdentity = identity },
            log: log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        var field = typeof(WorldServerFrameHandler).GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return new(handler, Assert.IsType<WorldSessionStore>(field.GetValue(handler)), log);
    }

    private static WorldDataCatalog CreateWorldData(uint goldWorth, ushort stackLimit = 0, uint lifetimeMinutes = 0,
        uint catalogItemId = ItemId, string catalogDurability = "10")
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = catalogItemId.ToString(CultureInfo.InvariantCulture);
        fields[1] = "OrdinaryNpcSaleFixture";
        fields[ItemType.GoldWorthFieldIndex] = goldWorth.ToString(CultureInfo.InvariantCulture);
        fields[ItemType.DurabilityFieldIndex] = catalogDurability;
        fields[ItemType.StackSizeFieldIndex] = stackLimit.ToString(CultureInfo.InvariantCulture);
        fields[ItemType.TimeFieldIndex] = lifetimeMinutes.ToString(CultureInfo.InvariantCulture);
        return LegacyItemWorldDataFixture.Create([new ItemType(checked((int)catalogItemId), fields[1], fields)], [],
            npcs: [new Npc((int)NpcUid, Kind: 0, Lookface: 0, MapId: 1002, X: 430, Y: 378, OptionalFlag: null)]);
    }

    private void Seed(uint uid, ushort durability = 10, ushort maximumDurability = 10,
        long money = 1_000, long warehouseMoney = 50, bool warehouseAlias = false)
    {
        File.WriteAllText(UserPath(uid), $$"""
            [Character]
            UID={{uid}}
            Name=Sale{{uid}}
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
            SecurityPass=0,0,0
            WHMoney={{warehouseMoney}}

            """);
        if (warehouseAlias) File.AppendAllText(UserPath(uid), $"WarehouseMoney={warehouseMoney}\n");
        Assert.Equal(uid, CharacterSnapshotParser.ParseText(File.ReadAllText(UserPath(uid))).Uid);
        var sold = new PlayerItemSnapshotItem
        {
            Uid = SoldUid, ItemId = ItemId, Durability = durability, MaximumDurability = maximumDurability,
            StackSize = 1, Color = 5, Plus = 2,
        };
        var kept = new PlayerItemSnapshotItem { Uid = KeptUid, ItemId = ItemId, Durability = 10, MaximumDurability = 10, StackSize = 1 };
        var warehouse = new PlayerItemSnapshotItem { Uid = KeptUid + 1, ItemId = ItemId, WarehouseId = 1000, StackSize = 1 };
        new PlayerItemSnapshotLoader(ItemsRoot).SaveByCharacterUid(uid,
            new PlayerItemSnapshot([sold, kept], [], [], [], []) { WarehouseItems = [warehouse] });
    }

    private Player CreateConnection(uint uid, Action<byte[]>? beforeSend = null)
    {
        var sent = new ConcurrentQueue<byte[]>();
        var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            (bytes, _) =>
            {
                var copy = bytes.ToArray();
                beforeSend?.Invoke(copy);
                sent.Enqueue(copy);
                return ValueTask.CompletedTask;
            }, nativeGameClient: new LegacyNativeGameClientRuntime());
        var player = new Player(uid, connection, new CancellationTokenSource(), sent);
        players.Add(player);
        return player;
    }

    private async Task<Player> Login(Owner owner, uint uid, Action<byte[]>? beforeSend = null)
    {
        var player = CreateConnection(uid, beforeSend);
        await SendLogin(owner, player);
        Assert.True(player.Sent.Any(IsAnswerOk), $"Test world admission failed. Handler log:{Environment.NewLine}{owner.Log}");
        Assert.Equal(uid, Current(owner, player).Character.Uid);
        Assert.True(player.Connection.NativeGameClient!.Player.IsCheckedPass);
        return player;
    }

    private async Task SendLogin(Owner owner, Player player) => await owner.Handler.HandleFrameAsync(player.Connection,
        ReadFrame(new LegacyMsgLoginGamePacket(player.Uid).ToFrameBytes()), player.Lifetime.Token);

    private async Task Sell(Owner owner, Player player) => await owner.Handler.HandleFrameAsync(player.Connection,
        ReadFrame(new MsgItemUsagePacket(NpcUid, SoldUid, MsgItemUsageAction.SellItem, 0, 0, 0, 0).ToFrameBytes()),
        player.Lifetime.Token);

    private static WorldSession Current(Owner owner, Player player)
    {
        Assert.True(owner.Sessions.TryGet(player.Connection, out var session));
        return Assert.IsType<WorldSession>(session);
    }

    private static WorldSession SaleResult(WorldSession original, long money, long warehouseMoney)
    {
        var attributes = new Dictionary<string, string>(original.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["Money"] = money.ToString(CultureInfo.InvariantCulture),
            ["WHMoney"] = warehouseMoney.ToString(CultureInfo.InvariantCulture),
        };
        if (attributes.ContainsKey("WarehouseMoney")) attributes["WarehouseMoney"] = warehouseMoney.ToString(CultureInfo.InvariantCulture);
        return original with
        {
            Character = original.Character with { Money = money, Attributes = attributes },
            PlayerItems = original.PlayerItems with { InventoryItems = original.PlayerItems.InventoryItems.Where(item => item.Uid != SoldUid).ToArray() },
        };
    }

    private void AssertSaved(uint uid, WorldSession expected)
    {
        var saved = CharacterSnapshotParser.ParseText(File.ReadAllText(UserPath(uid)));
        Assert.Equal(expected.Character.Money, saved.Money);
        Assert.Equal(50, saved.ConquerPoints);
        Assert.Equal(expected.Character.Attributes["WHMoney"], saved.Attributes["WHMoney"]);
        if (expected.Character.Attributes.TryGetValue("WarehouseMoney", out var alias)) Assert.Equal(alias, saved.Attributes["WarehouseMoney"]);
        Assert.Equal("0,0,0", saved.Attributes["SecurityPass"]);
        Assert.Equal("333", saved.Attributes["BoundConquerPoints"]);
        Assert.Equal("444", saved.Attributes["BankCps"]);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expected.PlayerItems), File.ReadAllBytes(ItemPath(uid)));
    }

    private async Task AssertDenied(Owner owner, Player player)
    {
        var session = Current(owner, player);
        var characterBytes = File.ReadAllBytes(UserPath(player.Uid));
        var itemBytes = File.ReadAllBytes(ItemPath(player.Uid));
        var first = player.Sent.Count;
        await Sell(owner, player);
        Assert.Same(session, Current(owner, player));
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(player.Uid)));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(player.Uid)));
        Assert.DoesNotContain(player.Sent.Skip(first), IsSaleSuccessFrame);
    }

    private static bool IsSaleSuccessFrame(byte[] bytes) => IsRemoval(bytes) ||
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) && update.Updates.Any(entry =>
            entry.Type is LegacyMsgUpdateDataType.Money or LegacyMsgUpdateDataType.WarehouseMoney);

    private static bool IsRemoval(byte[] bytes) => MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet) &&
        packet.Action == MsgItemUsageAction.RemoveInventory && packet.Id == SoldUid;

    private static bool IsAnswerOk(byte[] bytes) => LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet) &&
        packet.Content.Count > 3 && packet.Content[3] == "ANSWER_OK";

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private sealed record Owner(WorldServerFrameHandler Handler, WorldSessionStore Sessions, StringWriter Log);
    private sealed record Player(uint Uid, ConquerTcpConnection Connection, CancellationTokenSource Lifetime, ConcurrentQueue<byte[]> Sent);
}

public sealed class WindowsNpcSaleTheoryAttribute : TheoryAttribute
{
    public WindowsNpcSaleTheoryAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "Requires Windows file-sharing replacement failures.";
    }
}
