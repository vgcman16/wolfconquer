using System.Buffers;
using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Registered-handler admission/file proof, not actual-client, SQL, or ground
// restart durability. Native FloorMap116..205 has no PIN gate.
public sealed partial class LegacyFloorPickupAdmissionTests : IDisposable
{
    private const uint Uid = 1_300_301, FloorUid = 900_301, ItemId = 50_000;
    private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-floor-admission-" + Guid.NewGuid().ToString("N"));
    private string UsersRoot => Path.Combine(root, "Users");
    private string ItemsRoot => Path.Combine(root, "Items");
    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> Handlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }

    [Fact]
    public async Task RealIssuedServerZeroPicksUpMoneyWithoutPinOrCompleteLogin()
    {
        var f = Create(serverId: 0);
        Assert.False(f.Client.Player.IsCheckedPass);
        Assert.False(f.Client.Player.CompleteLogin);
        Assert.False(LegacySecondaryPasswordRuntime.IsAdmitted(f.Connection, f.Initial));

        await Pickup(f.Context);

        AssertMoneyPickup(f, 350);
        Assert.False(f.Client.Player.IsCheckedPass);
        Assert.False(f.Client.Player.CompleteLogin);
    }

    [Theory]
    [InlineData("users-root")]
    [InlineData("items-root")]
    [InlineData("both-roots")]
    [InlineData("session-store")]
    public async Task PhysicalPickupCannotFallBackToUnboundCompatibilityWhenPersistenceOrHolderIsMissing(string missing)
    {
        var f = Create();
        var context = f.Context;
        if (missing is "users-root" or "both-roots")
        {
            var current = Current(f) with { UsersRoot = null };
            f.Sessions.Set(f.Connection, current);
            context = context with { Session = current };
        }
        if (missing is "items-root" or "both-roots") context = context with { PlayerItemsRoot = null };
        if (missing == "session-store") context = context with { Sessions = null };

        await AssertDenied(f, context, missing == "session-store" ? "sessionStoreUnavailable" : "npcSaleRootMissing");

        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
        Assert.False(f.Client.Player.IsCheckedPass); // Persistence is not a new PIN requirement.
    }

    [Fact]
    public async Task RealIssuedOwnServerPicksUpKnownWholeItemWhilePinRemainsUnverified()
    {
        var f = Create(item: true);
        Assert.False(LegacySecondaryPasswordRuntime.IsAdmitted(f.Connection, f.Initial));

        await Pickup(f.Context);

        var current = Current(f);
        var acquired = Assert.Single(current.PlayerItems.InventoryItems, item => item.Uid == f.Entry.Item!.Uid);
        Assert.Equal((ushort)3, acquired.StackSize);
        Assert.Equal((byte)0, acquired.Plus); // Native Send clears ordinary-item Plus.
        Assert.Equal(f.Initial.Character.Money, current.Character.Money);
        Assert.Equal(f.Initial.Character.ConquerPoints, current.Character.ConquerPoints);
        Assert.Equal(2, current.PlayerItems.InventoryItems.Count);
        var expectedItems = f.Initial.PlayerItems with
            { InventoryItems = [.. f.Initial.PlayerItems.InventoryItems, f.Entry.Item!.WithPosition(0)] };
        var expectedBytes = PlayerItemSnapshotLoader.ToLegacyBytes(expectedItems);
        var acquiredOffset = sizeof(int) + f.Initial.PlayerItems.InventoryItems.Count * PlayerItemSnapshotLoader.LegacyDbItemSize;
        // This floor item's maximum is0; native Send repairs it from catalog10.
        expectedBytes[acquiredOffset + 10] = 10;
        expectedBytes[acquiredOffset + 11] = 0;
        expectedBytes[acquiredOffset + 28] = 0;
        Assert.Equal(expectedBytes, PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems));
        Assert.Empty(f.Floor.SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.Equal(3, f.Sent.Count);
        Assert.Contains(f.Sent, bytes => LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Uid == acquired.Uid && packet.StackSize == 3);
        AssertFloorRemoval(f);
        AssertPersistedAssets(f);
        Assert.Equal(f.InitialCharacterBytes, File.ReadAllBytes(f.CharacterPath));
        Assert.False(f.Client.Player.IsCheckedPass);
        Assert.False(f.Client.Player.CompleteLogin);
    }

    [Theory]
    [InlineData(65_536U)]
    [InlineData(uint.MaxValue)]
    public async Task UshortWrappedPhysicalFieldCannotPassFullUintOwnServerComparison(uint serverId)
    {
        var f = Create(serverId);
        Assert.Equal(unchecked((ushort)serverId), f.Client.Player.ServerID);
        await AssertDenied(f, f.Context, "ownServerIdentityUnverifiedOrOwnerInvalid");
    }

    [Theory]
    [InlineData("missing-config")]
    [InlineData("unissued")]
    [InlineData("forged-field")]
    [InlineData("mutated-field")]
    [InlineData("foreign-client")]
    [InlineData("foreign-player-owner")]
    [InlineData("replacement-player")]
    [InlineData("uid-binding")]
    public async Task PublicFieldsAttributesAndForeignOrReplacedOwnersCannotManufacturePickupAdmission(string scenario)
    {
        var f = Create(issue: scenario is not ("unissued" or "forged-field"));
        var context = f.Context;
        var current = Current(f);
        switch (scenario)
        {
            case "missing-config": context = context with { LocalServerIdentity = null }; break;
            case "unissued": break;
            case "forged-field":
                f.Client.Player.ServerID = 42;
                current = current with { Character = current.Character with { Attributes =
                    new Dictionary<string, string>(current.Character.Attributes)
                    { ["ServerID"] = "42", ["OnMyOwnServer"] = "true" } } };
                break;
            case "mutated-field": f.Client.Player.ServerID = 43; break;
            case "foreign-client":
                current = current with { NativeGameClient = new LegacyNativeGameClientRuntime() };
                current.NativeGameClient!.Player.UID = Uid;
                current.NativeGameClient.Player.ServerID = 42;
                break;
            case "foreign-player-owner":
                f.Client.Player = new LegacyNativeGameClientRuntime().Player;
                f.Client.Player.UID = Uid;
                f.Client.Player.ServerID = 42;
                break;
            case "replacement-player":
                f.Client.Player = new LegacyNativePlayerRuntime(f.Client) { UID = Uid, ServerID = 42 };
                break;
            case "uid-binding":
                f.Client.Player.UID = Uid + 1;
                current = current with { Character = current.Character with { Uid = Uid + 1 } };
                break;
        }
        f.Sessions.Set(f.Connection, current);
        await AssertDenied(f, context, "ownServerIdentityUnverifiedOrOwnerInvalid");
        if (scenario == "unissued")
            await AssertDenied(f, context, "ownServerIdentityUnverifiedOrOwnerInvalid", malformed: true);
    }

    [Fact]
    public async Task RemovedCurrentSessionCannotBeResurrectedFromAnEligibleSuppliedContext()
    {
        var f = Create();
        Assert.True(f.Sessions.TryRemove(f.Connection, out _));
        await AssertDenied(f, f.Context, "currentSessionMissing");
        Assert.False(f.Sessions.TryGet(f.Connection, out _));
    }

    [Fact]
    public async Task PickupUsesLatestStoredWalletItemsAndAttributesInsteadOfStaleSuppliedContext()
    {
        var f = Create();
        var attrs = new Dictionary<string, string>(f.Initial.Character.Attributes)
            { ["LatestOwnerState"] = "retained", ["Money"] = "900" };
        var latest = f.Initial with { Character = f.Initial.Character with { Money = 900, Attributes = attrs },
            PlayerItems = f.Initial.PlayerItems with { InventoryItems = f.Initial.PlayerItems.InventoryItems.Append(
                new PlayerItemSnapshotItem { Uid = 600_304, ItemId = ItemId, StackSize = 4 }).ToArray() } };
        f.Sessions.Set(f.Connection, latest);
        // This is the lawful latest disk baseline, not a stale-file bypass.
        File.WriteAllText(f.CharacterPath, File.ReadAllText(f.CharacterPath)
            .Replace("\nMoney=100\n", "\nMoney=900\n", StringComparison.Ordinal) + "\nLatestOwnerState=retained\n");
        new PlayerItemSnapshotLoader(f.ItemsRoot).SaveByCharacterUid(Uid, latest.PlayerItems);
        var stale = f.Initial with { Character = f.Initial.Character with { Money = 1, MapId = 9999 } };

        await Pickup(f.Context with { Session = stale });

        AssertMoneyPickup(f, 1_150);
        Assert.Equal("retained", Current(f).Character.Attributes["LatestOwnerState"]);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(latest.PlayerItems),
            PlayerItemSnapshotLoader.ToLegacyBytes(Current(f).PlayerItems));
    }

    [Theory]
    [InlineData("session-trade", "tradeActive")]
    [InlineData("unresolved-escrow", "tradeEscrowUnresolved")]
    [InlineData("malformed-escrow", "tradeEscrowInvalid:")]
    public async Task CurrentTradeOrEscrowDeniesBeforeFloorConsumptionDespiteStaleEligibleContext(string scenario, string reason)
    {
        var f = Create();
        var current = Current(f);
        var context = f.Context;
        if (scenario == "session-trade") current = current with { LegacyTradeActive = true };
        else if (scenario == "unresolved-escrow")
            current = LegacyTradeEscrowClaims.WithClaim(current, new LegacyTradeEscrowClaim(Guid.NewGuid(), 1, 0));
        else current = current with { Character = current.Character with { Attributes =
            new Dictionary<string, string>(current.Character.Attributes) { [LegacyTradeEscrowClaims.IdKey] = "partial" } } };
        f.Sessions.Set(f.Connection, current);

        await AssertDenied(f, context, reason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LiveTradePairBlocksPickupOnlyWhenItsNativeWindowIsOpen(bool windowOpen)
    {
        var f = Create();
        var trades = new LegacyTradeStateStore();
        lock (trades.SyncRoot)
            trades.Create(f.Connection, Uid, new ConquerTcpConnection(null, null), Uid + 1).WindowOpen = windowOpen;
        var context = f.Context with { Trades = trades };
        if (windowOpen) await AssertDenied(f, context, "tradeActive");
        else
        {
            await Pickup(context);
            AssertMoneyPickup(f, 350);
        }
        Assert.True(trades.Contains(f.Connection)); // Pending request ownership is not itself InTrade.
    }

    [Fact]
    public async Task PopulatedFloorItemStillRequiresNativeCatalogEligibilityBeforeConsumption()
    {
        var f = Create(item: true);
        await AssertDenied(f, f.Context with { WorldData = LegacyItemWorldDataFixture.Create() }, "itemtype data was not loaded");
    }

    private static async Task AssertDenied(Fixture f, WorldPacketHandlerContext context, string reason, bool malformed = false)
    {
        var hadSession = f.Sessions.TryGet(f.Connection, out var before);
        var itemBytes = before is null ? null : PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems);
        var floor = f.Floor.SnapshotVisible(DateTimeOffset.UtcNow).ToArray();
        var characterBytes = File.ReadAllBytes(f.CharacterPath);
        var persistedItems = File.ReadAllBytes(f.ItemPath);
        var first = f.Sent.Count;
        var logStart = f.Log.GetStringBuilder().Length;

        await Pickup(context, malformed);

        Assert.Equal(hadSession, f.Sessions.TryGet(f.Connection, out var after));
        Assert.Same(before, after);
        if (after is not null)
        {
            Assert.Equal(itemBytes, PlayerItemSnapshotLoader.ToLegacyBytes(after.PlayerItems));
            Assert.Equal(before!.Character.Money, after.Character.Money);
            Assert.Equal(before.Character.ConquerPoints, after.Character.ConquerPoints);
        }
        Assert.Equal(floor, f.Floor.SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.True(f.Floor.TryGetVisibleEntry(FloorUid, out var retained));
        Assert.Same(f.Entry, retained);
        Assert.Empty(f.Sent.Skip(first));
        Assert.Equal(characterBytes, File.ReadAllBytes(f.CharacterPath));
        Assert.Equal(persistedItems, File.ReadAllBytes(f.ItemPath));
        var log = f.Log.ToString()[logStart..];
        Assert.Contains(reason, log, StringComparison.Ordinal);
        if (malformed) Assert.DoesNotContain("malformed", log, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertMoneyPickup(Fixture f, long expected)
    {
        Assert.Equal(expected, Current(f).Character.Money);
        Assert.Equal(50, Current(f).Character.ConquerPoints);
        Assert.Empty(f.Floor.SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.Equal(4, f.Sent.Count);
        Assert.Contains(f.Sent, bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Updates.Any(update => update.Type == LegacyMsgUpdateDataType.Money && update.Value == (ulong)expected));
        Assert.Equal(2, f.Sent.Count(bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Updates.Any(update => update.Type == LegacyMsgUpdateDataType.Money && update.Value == (ulong)expected)));
        AssertFloorRemoval(f);
        AssertPersistedAssets(f);
    }

    private static void AssertPersistedAssets(Fixture f)
    {
        var current = Current(f);
        var saved = CharacterSnapshotParser.ParseText(File.ReadAllText(f.CharacterPath));
        Assert.Equal(current.Character.Uid, saved.Uid);
        Assert.Equal(current.Character.Money, saved.Money);
        Assert.Equal(current.Character.ConquerPoints, saved.ConquerPoints);
        var items = PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems);
        Assert.Equal(items, File.ReadAllBytes(f.ItemPath));
        Assert.Equal(items, PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshotLoader(f.ItemsRoot).LoadByCharacterUid(Uid)));
    }

    private static void AssertFloorRemoval(Fixture f) => Assert.Contains(f.Sent, bytes =>
        LegacyFloorItemPacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == FloorUid &&
        packet.DropType == LegacyFloorItemDropType.Remove);

    private static async Task Pickup(WorldPacketHandlerContext context, bool malformed = false)
    {
        var writer = new PacketWriter(LegacyFloorItemPacket.Opcode, malformed ? 0 : sizeof(uint));
        if (!malformed) writer.WriteUInt32(0, FloorUid);
        await Handlers.HandleAsync(context, ReadFrame(writer.ToArray()));
    }

    private static WorldSession Current(Fixture f)
    {
        Assert.True(f.Sessions.TryGet(f.Connection, out var current));
        return Assert.IsType<WorldSession>(current);
    }

    private Fixture Create(uint serverId = 42, bool issue = true, bool item = false)
    {
        Directory.CreateDirectory(UsersRoot);
        Directory.CreateDirectory(ItemsRoot);
        var characterPath = Path.Combine(UsersRoot, Uid + ".ini");
        var itemPath = Path.Combine(ItemsRoot, Uid + ".bin");
        var client = new LegacyNativeGameClientRuntime();
        client.Player.UID = Uid;
        client.Player.SecurityPassword = 111;
        var sent = new ConcurrentQueue<byte[]>();
        var connection = new ConquerTcpConnection(null, null, (bytes, _) =>
        { sent.Enqueue(bytes.ToArray()); return ValueTask.CompletedTask; }, nativeGameClient: client);
        var text = "[Character]\nUID=" + Uid + "\nName=FloorAdmission\n" + string.Join('\n', new[]
        {
            "Body=1008", "Face=154", "Haire=27", "Class=10", "FirstClass=0", "SecoundeClass=0", "Reborn=0",
            "Level=100", "Experience=0", "Map=1002", "X=430", "Y=380", "PMap=1002", "PMapX=430", "PMapY=380",
            "Strength=10", "Agility=10", "Vitaliti=10", "Spirit=10", "Atributes=0", "MinHitPoints=330", "MinMana=100",
            "Money=100", "ConquerPoints=50", "BoundConquerPoints=0", "BankCps=0", "PkPoints=0", "Online=1",
            "WHMoney=25", "SecurityPass=111,0,0,",
        });
        File.WriteAllText(characterPath, text);
        var initial = new WorldSession(Uid, "fixture", CharacterSnapshotParser.ParseText(text),
            new PlayerItemSnapshot([new PlayerItemSnapshotItem { Uid = 600_302, ItemId = ItemId, StackSize = 2 }], [], [], [], []),
            PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty, UsersRoot: UsersRoot) { NativeGameClient = client };
        new PlayerItemSnapshotLoader(ItemsRoot).SaveByCharacterUid(Uid, initial.PlayerItems);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, initial);
        var identity = new LegacyLocalServerIdentity(serverId);
        if (issue) Assert.True(LegacyServerIdentityRuntime.TryInitialize(connection, initial, identity));
        var packet = new LegacyFloorItemPacket(FloorUid, ItemId, 430, 380, 40, LegacyFloorItemDropType.Visible,
            0, 2, Uid, 0, 0, 0, 0, 0, "");
        var entry = item ? FloorItemRuntimeEntry.ForItem(1002, packet,
            new PlayerItemSnapshotItem { Uid = 600_303, ItemId = ItemId, StackSize = 3, Plus = 2 }) :
            FloorItemRuntimeEntry.ForMoney(1002, packet, 250);
        var floor = new FloorItemRuntimeStateStore();
        floor.AddVisible(entry);
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = ItemId.ToString(); fields[1] = "FloorFixture"; fields[ItemType.DurabilityFieldIndex] = "10";
        var data = LegacyItemWorldDataFixture.Create(new ItemType((int)ItemId, fields[1], fields));
        var log = new StringWriter();
        var context = new WorldPacketHandlerContext(connection, log, initial, WorldData: data, Sessions: sessions,
            FloorItems: floor, PlayerItemsRoot: ItemsRoot, ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13) { LocalServerIdentity = identity };
        return new(connection, client, initial, sessions, floor, entry, sent, log, context,
            characterPath, ItemsRoot, itemPath, File.ReadAllBytes(characterPath));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private sealed record Fixture(ConquerTcpConnection Connection, LegacyNativeGameClientRuntime Client,
        WorldSession Initial, WorldSessionStore Sessions, FloorItemRuntimeStateStore Floor, FloorItemRuntimeEntry Entry,
        ConcurrentQueue<byte[]> Sent, StringWriter Log, WorldPacketHandlerContext Context,
        string CharacterPath, string ItemsRoot, string ItemPath, byte[] InitialCharacterBytes);
}
