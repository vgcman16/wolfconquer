using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyLotteryBoxesNpcTests : IDisposable
{
    private const uint LotteryBoxNpcId = 925;
    private const uint SmallLotteryTicketItemId = 711_504;

    private readonly string _usersRoot = Path.Combine(Path.GetTempPath(), "wolf-lottery-boxes-users-" + Guid.NewGuid());
    private readonly string _itemsRoot = Path.Combine(Path.GetTempPath(), "wolf-lottery-boxes-items-" + Guid.NewGuid());

    [Fact]
    public async Task HandlerLotteryBoxMissingWorldDataPreservesTicketsEntriesAndFiles()
    {
        Directory.CreateDirectory(_usersRoot);
        Directory.CreateDirectory(_itemsRoot);
        WriteSnapshot(accountId: 100, lotteryEntries: 2, addJade: 7);
        var initialItems = new PlayerItemSnapshot(
            [Item(uid: 50, SmallLotteryTicketItemId, stackSize: 5)],
            [],
            [],
            [],
            []);
        new PlayerItemSnapshotLoader(_itemsRoot).SaveByCharacterUid(1_293_600, initialItems);
        var characterPath = Path.Combine(_usersRoot, "100.ini");
        var itemsPath = Path.Combine(_itemsRoot, "1293600.bin");
        var characterBefore = File.ReadAllBytes(characterPath);
        var itemsBefore = File.ReadAllBytes(itemsPath);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(initialItems.InventoryItems, usersRoot: _usersRoot, lotteryEntries: 2, addJade: 7);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerItemsRoot: _itemsRoot),
            ReadFrame(LegacyNpcServerRequestPacket.Script(LotteryBoxNpcId, optionId: 1, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal("2", updated.Character.Attributes["LotteryEntries"]);
        Assert.Equal("7", updated.Character.Attributes["AddJade"]);
        var remainingTicket = Assert.Single(updated.PlayerItems.InventoryItems);
        Assert.Equal(SmallLotteryTicketItemId, remainingTicket.ItemId);
        Assert.Equal(5, remainingTicket.StackSize);

        Assert.Empty(sent);
        Assert.Equal(characterBefore, File.ReadAllBytes(characterPath));
        Assert.Equal(itemsBefore, File.ReadAllBytes(itemsPath));
        Assert.Contains("source=NpcHandler.LotteryBoxes", log.ToString());
        Assert.Contains("reason=worldDataMissing", log.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandlerLotteryBoxMissingWorldDataDoesNotAddOrPersistHeroRewardGoal()
    {
        Directory.CreateDirectory(_usersRoot);
        Directory.CreateDirectory(_itemsRoot);
        WriteSnapshot(accountId: 100, lotteryEntries: 2, addJade: 7, heroRewards: "1/101/1/");
        var initialItems = new PlayerItemSnapshot(
            [Item(uid: 53, SmallLotteryTicketItemId, stackSize: 3)],
            [],
            [],
            [],
            []);
        new PlayerItemSnapshotLoader(_itemsRoot).SaveByCharacterUid(1_293_600, initialItems);
        var characterPath = Path.Combine(_usersRoot, "100.ini");
        var itemsPath = Path.Combine(_itemsRoot, "1293600.bin");
        var characterBefore = File.ReadAllBytes(characterPath);
        var itemsBefore = File.ReadAllBytes(itemsPath);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            initialItems.InventoryItems,
            usersRoot: _usersRoot,
            lotteryEntries: 2,
            addJade: 7,
            heroRewards: "1/101/1/");
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerItemsRoot: _itemsRoot),
            ReadFrame(LegacyNpcServerRequestPacket.Script(LotteryBoxNpcId, optionId: 1, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal("1/101/1/", updated.Character.Attributes["HeroRewards"]);
        Assert.Equal((ushort)3, Assert.Single(updated.PlayerItems.InventoryItems).StackSize);

        Assert.Empty(sent);
        Assert.Equal(characterBefore, File.ReadAllBytes(characterPath));
        Assert.Equal(itemsBefore, File.ReadAllBytes(itemsPath));
        Assert.Contains("reason=worldDataMissing", log.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandlerLotteryBoxKeepsExistingSourceHeroRewardGoalUnchanged()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            [Item(uid: 54, SmallLotteryTicketItemId, stackSize: 3)],
            heroRewards: "2/101/1/306/1/");
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(LotteryBoxNpcId, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal("2/101/1/306/1/", updated.Character.Attributes["HeroRewards"]);
    }

    [Fact]
    public async Task HandlerLotteryBoxMissingTicketsSendsSourceBackedDialogWithoutMutation()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession([Item(uid: 51, SmallLotteryTicketItemId, stackSize: 2)]);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(LotteryBoxNpcId, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal("2", updated.Character.Attributes["LotteryEntries"]);
        Assert.Equal(2, Assert.Single(updated.PlayerItems.InventoryItems).StackSize);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(
            replies,
            reply => reply.Text.Contains("3 Small Lottery Tickets", StringComparison.Ordinal));
        Assert.DoesNotContain(ParseStrings(sent), packet => packet.Strings.Contains("lottery"));
    }

    [Fact]
    public async Task HandlerLotteryBoxOutsideSourceRouteFallsThroughToUnmodeledDiagnostic()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            [Item(uid: 52, SmallLotteryTicketItemId, stackSize: 3)],
            mapId: 1_002);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(LotteryBoxNpcId, optionId: 1, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal("2", updated.Character.Attributes["LotteryEntries"]);
        Assert.Equal(3, Assert.Single(updated.PlayerItems.InventoryItems).StackSize);
        Assert.DoesNotContain(ParseStrings(sent), packet => packet.Strings.Contains("lottery"));
        Assert.Contains("PARITY-GAP", log.ToString());
    }

    private static WorldSession CreateSession(
        IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null,
        string? usersRoot = null,
        long mapId = 700,
        ulong lotteryEntries = 2,
        ulong addJade = 7,
        string? heroRewards = null)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LotteryEntries"] = lotteryEntries.ToString(CultureInfo.InvariantCulture),
            ["AddJade"] = addJade.ToString(CultureInfo.InvariantCulture),
            ["DynamicID"] = "0",
        };
        if (heroRewards is not null)
        {
            attributes["HeroRewards"] = heroRewards;
        }

        return new WorldSession(
            AccountId: 100,
            Username: "lottery-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "Lucky",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: mapId,
                X: 50,
                Y: 50,
                PreviousMapId: mapId,
                PreviousMapX: 50,
                PreviousMapY: 50,
                Strength: 0,
                Agility: 0,
                Vitality: 0,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: 1,
                MinMana: 0,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: attributes,
                PaidNobility: new Dictionary<string, string>()),
            PlayerItems: new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private void WriteSnapshot(long accountId, ulong lotteryEntries, ulong addJade, string heroRewards = "")
    {
        File.WriteAllText(
            Path.Combine(_usersRoot, accountId.ToString(CultureInfo.InvariantCulture) + ".ini"),
            $$"""
            [Character]
            UID=1293600
            Body=1003
            Face=0
            Name=Lucky
            Class=7051
            Map=700
            X=50
            Y=50
            FirstClass=7051
            SecoundeClass=0
            Conquer=0
            Reborn=0
            Level=140
            Haire=0
            Experience=0
            PMap=700
            PMapX=50
            PMapY=50
            Agility=0
            Strength=0
            Vitaliti=0
            Spirit=0
            Atributes=0
            MinHitPoints=1
            MinMana=0
            ConquerPoints=0
            BankCps=0
            BoundConquerPoints=0
            Money=0
            PkPoints=0
            Online=0
            LotteryEntries={{lotteryEntries}}
            AddJade={{addJade}}
            HeroRewards={{heroRewards}}
            DynamicID=0

            [PaidNobility]
            """);
    }

    private static PlayerItemSnapshotItem Item(uint uid, uint itemId, ushort stackSize)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Position = 0,
            StackSize = stackSize,
        };
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static IReadOnlyList<LegacyNpcReplyPacket> ParseNpcReplies(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyNpcReplyPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyGameItemPacket> ParseGameItems(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyGameItemPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyStringPacket> ParseStrings(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyStringPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyStringPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    public void Dispose()
    {
        if (Directory.Exists(_usersRoot))
        {
            Directory.Delete(_usersRoot, recursive: true);
        }

        if (Directory.Exists(_itemsRoot))
        {
            Directory.Delete(_itemsRoot, recursive: true);
        }
    }
}
