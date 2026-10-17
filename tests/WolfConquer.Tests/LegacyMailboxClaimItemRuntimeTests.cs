using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMailboxClaimItemRuntimeTests : IDisposable
{
    private const uint PlayerUid = 1_293_600;
    private const uint MailId = 21;
    private const uint AttachedItemUid = 7_654_321;
    private const uint AttachedItemTypeId = 1_088_001;
    private const uint InboxWarehouseId = 1_000;
    private const int LegacyInventorySlots = 40;

    private readonly string rootDirectory = Path.Combine(
        Path.GetTempPath(),
        "wolf-mail-claim-item-handler-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ClaimItemWithFreeInventoryMovesAttachedWarehouseItemClearsMailAndAcks()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var store = SeedMailboxWithAttachedItem();
        var connection = CreateConnection(sent);
        var session = CreateSession(CreatePlayerItems(
            inventoryCount: 1,
            warehouseItems: [CreateAttachedWarehouseItem()]));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                Sessions: sessions,
                Mailboxes: store),
            ReadFrame(new LegacyMailboxModePacket(
                LegacyMailboxModeType.ClaimItem,
                MailId,
                Items: []).ToFrameBytes()));

        Assert.Equal(2, sent.Count);
        var item = AssertLegacyGameItem(sent[0]);
        Assert.Equal(AttachedItemUid, item.Uid);
        Assert.Equal(AttachedItemTypeId, item.ItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, item.Mode);
        Assert.Equal(0, item.Position);
        AssertMailboxAck(sent[1], LegacyMailboxModeType.ClaimItem, MailId, []);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var inventoryItem = Assert.Single(
            updatedSession.PlayerItems.InventoryItems,
            item => item.Uid == AttachedItemUid);
        Assert.Equal(AttachedItemTypeId, inventoryItem.ItemId);
        Assert.Equal(0U, inventoryItem.WarehouseId);
        Assert.DoesNotContain(
            updatedSession.PlayerItems.WarehouseItems,
            item => item.Uid == AttachedItemUid);

        var persisted = Assert.Single(new LegacyMailboxStore(rootDirectory).Load(PlayerUid));
        Assert.Equal(0U, persisted.ItemId);
    }

    [Fact]
    public async Task ClaimItemWithFullInventorySendsBagFullKeepsItemAndStillAcks()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var store = SeedMailboxWithAttachedItem();
        var connection = CreateConnection(sent);
        var session = CreateSession(CreatePlayerItems(
            inventoryCount: LegacyInventorySlots,
            warehouseItems: [CreateAttachedWarehouseItem()]));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                Sessions: sessions,
                Mailboxes: store),
            ReadFrame(new LegacyMailboxModePacket(
                LegacyMailboxModeType.ClaimItem,
                MailId,
                Items: []).ToFrameBytes()));

        Assert.Equal(2, sent.Count);
        AssertSystemTalk(sent[0], "STR_BAG_FULL@@");
        // Legacy CMsgMailboxMode sends the ClaimItem ack after the full-bag message.
        AssertMailboxAck(sent[1], LegacyMailboxModeType.ClaimItem, MailId, []);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(LegacyInventorySlots, updatedSession.PlayerItems.InventoryItems.Count);
        Assert.Contains(
            updatedSession.PlayerItems.WarehouseItems,
            item => item.Uid == AttachedItemUid && item.WarehouseId == InboxWarehouseId);

        var persisted = Assert.Single(new LegacyMailboxStore(rootDirectory).Load(PlayerUid));
        Assert.Equal(AttachedItemUid, persisted.ItemId);
    }

    [Fact]
    public async Task ClaimAllMovesAttachedWarehouseItemRefreshesMailAndAcks()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var store = SeedMailboxWithAttachedItem();
        var connection = CreateConnection(sent);
        var session = CreateSession(CreatePlayerItems(
            inventoryCount: 1,
            warehouseItems: [CreateAttachedWarehouseItem()]));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                Sessions: sessions,
                Mailboxes: store),
            ReadFrame(new LegacyMailboxModePacket(
                LegacyMailboxModeType.ClaimAll,
                Id: 0,
                Items: [MailId]).ToFrameBytes()));

        Assert.Equal(3, sent.Count);
        AssertLegacyGameItem(sent[0]);
        AssertMailListRefresh(sent[1], mailId: MailId, itemId: 0);
        AssertMailboxAck(sent[2], LegacyMailboxModeType.ClaimAll, 0, [MailId]);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Contains(
            updatedSession.PlayerItems.InventoryItems,
            item => item.Uid == AttachedItemUid && item.WarehouseId == 0);
        Assert.DoesNotContain(
            updatedSession.PlayerItems.WarehouseItems,
            item => item.Uid == AttachedItemUid);

        var persisted = Assert.Single(new LegacyMailboxStore(rootDirectory).Load(PlayerUid));
        Assert.Equal(0U, persisted.ItemId);
    }

    [Fact]
    public async Task ClaimItemThroughLiveHandlerPersistsAttachedWarehouseItemToPlayersItems()
    {
        var usersRoot = Path.Combine(rootDirectory, "Users");
        var itemsRoot = Path.Combine(rootDirectory, "PlayersItems");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(itemsRoot);
        WriteSnapshot(usersRoot);
        new PlayerItemSnapshotLoader(itemsRoot).SaveByCharacterUid(
            PlayerUid,
            CreatePlayerItems(
                inventoryCount: 1,
                warehouseItems: [CreateAttachedWarehouseItem()]));
        SeedMailboxWithAttachedItem();

        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot, itemsRoot), log);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(PlayerUid).ToFrameBytes()));
        var countBeforeClaim = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMailboxModePacket(
                LegacyMailboxModeType.ClaimItem,
                MailId,
                Items: []).ToFrameBytes()));

        var claimFrames = sent.Skip(countBeforeClaim).ToArray();
        Assert.Equal(2, claimFrames.Length);
        var item = AssertLegacyGameItem(claimFrames[0]);
        Assert.Equal(AttachedItemUid, item.Uid);
        Assert.Equal(AttachedItemTypeId, item.ItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, item.Mode);
        AssertMailboxAck(claimFrames[1], LegacyMailboxModeType.ClaimItem, MailId, []);

        var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(PlayerUid);
        Assert.Contains(
            persistedItems.InventoryItems,
            item => item.Uid == AttachedItemUid &&
                item.ItemId == AttachedItemTypeId &&
                item.WarehouseId == 0);
        Assert.DoesNotContain(
            persistedItems.WarehouseItems,
            item => item.Uid == AttachedItemUid);

        var persistedMail = Assert.Single(new LegacyMailboxStore(rootDirectory).Load(PlayerUid));
        Assert.Equal(0U, persistedMail.ItemId);

        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyMailboxMode", output);
        Assert.Contains("type=ClaimItem", output);
        Assert.DoesNotContain("PARITY-GAP category=runtime severity=error gap=player-items-persistence", output);
    }

    public void Dispose()
    {
        if (Directory.Exists(rootDirectory))
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }

    private LegacyMailboxStore SeedMailboxWithAttachedItem()
    {
        var store = new LegacyMailboxStore(rootDirectory);
        store.Add(
            PlayerUid,
            new LegacyMailboxEntry
            {
                Title = "Attached",
                SenderName = "System",
                Content = "Claim the item.",
                Id = MailId,
                ItemId = AttachedItemUid,
                Timer = DateTime.Now.AddHours(1).Ticks,
            });
        store.Save(PlayerUid);
        return store;
    }

    private static WorldSession CreateSession(PlayerItemSnapshot playerItems)
    {
        return new WorldSession(
            AccountId: PlayerUid,
            Username: "guest",
            Character: new CharacterSnapshot(
                Uid: PlayerUid,
                Name: "HeroOne",
                Body: 1008,
                Face: 154,
                Hair: 27,
                Class: 7051,
                FirstClass: 0,
                SecondClass: 7050,
                Reborn: 2,
                Level: 140,
                Experience: 0,
                MapId: 1015,
                X: 835,
                Y: 596,
                PreviousMapId: 1000,
                PreviousMapX: 500,
                PreviousMapY: 650,
                Strength: 0,
                Agility: 36,
                Vitality: 447,
                Spirit: 0,
                AttributePoints: 12,
                MinHitPoints: 55_989,
                MinMana: 5_000,
                Money: 98_602_439_956,
                ConquerPoints: 639_813_200,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 1,
                SourceFile: "",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            playerItems,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty);
    }

    private static PlayerItemSnapshot CreatePlayerItems(
        int inventoryCount,
        IReadOnlyList<PlayerItemSnapshotItem> warehouseItems)
    {
        return new PlayerItemSnapshot(
            Enumerable.Range(0, inventoryCount)
                .Select(index => new PlayerItemSnapshotItem
                {
                    Uid = checked((uint)(10_000 + index)),
                    ItemId = 1_000_000,
                    Durability = 1,
                    MaximumDurability = 1,
                    StackSize = 1,
                })
                .ToArray(),
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems: [],
            RuneItems: [])
        {
            WarehouseItems = warehouseItems,
        };
    }

    private static PlayerItemSnapshotItem CreateAttachedWarehouseItem()
    {
        return new PlayerItemSnapshotItem
        {
            Uid = AttachedItemUid,
            ItemId = AttachedItemTypeId,
            Durability = 1,
            MaximumDurability = 1,
            Position = 0,
            StackSize = 1,
            WarehouseId = InboxWarehouseId,
        };
    }

    private static void WriteSnapshot(string usersRoot)
    {
        File.WriteAllText(
            Path.Combine(usersRoot, PlayerUid + ".ini"),
            $$"""
            [Character]
            UID={{PlayerUid}}
            Name=HeroOne
            Body=1008
            Face=154
            Haire=27
            Class=7051
            FirstClass=0
            SecoundeClass=7050
            Reborn=2
            Level=140
            Experience=0
            Map=1015
            X=835
            Y=596
            PMap=1000
            PMapX=500
            PMapY=650
            Strength=0
            Agility=36
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints=55989
            MinMana=5000
            Money=98602439956
            ConquerPoints=639813200
            BoundConquerPoints=0
            BankCps=0
            PkPoints=0
            Online=1
            """);
    }

    private static ConquerTcpConnection CreateConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
    }

    private static LegacyGameItemPacket AssertLegacyGameItem(byte[] bytes)
    {
        var frame = ReadFrame(bytes);
        Assert.Equal(WorldPacketOpcodes.LegacyGameItem, frame.Header.Opcode);
        Assert.True(LegacyGameItemPacket.TryParse(frame, out var packet));
        return packet;
    }

    private static void AssertSystemTalk(byte[] bytes, string expectedMessage)
    {
        var frame = ReadFrame(bytes);
        Assert.Equal(WorldPacketOpcodes.LegacyMsgMessage, frame.Header.Opcode);
        Assert.True(LegacyMsgMessagePacket.TryParse(frame, out var message));
        Assert.Equal(LegacyMsgMessagePacket.TalkChatType, message.ChatType);
        Assert.Equal(expectedMessage, message.Content[3]);
    }

    private static void AssertMailboxAck(
        byte[] bytes,
        LegacyMailboxModeType expectedType,
        uint expectedId,
        IReadOnlyList<ulong> expectedItems)
    {
        var frame = ReadFrame(bytes);
        Assert.Equal(WorldPacketOpcodes.LegacyMailboxMode, frame.Header.Opcode);
        Assert.True(LegacyMailboxModePacket.TryParse(frame, out var ack));
        Assert.Equal(expectedType, ack.Type);
        Assert.Equal(expectedId, ack.Id);
        Assert.Equal(expectedItems, ack.Items);
    }

    private static void AssertMailListRefresh(byte[] bytes, ulong mailId, ulong itemId)
    {
        var frame = ReadFrame(bytes);
        Assert.Equal(WorldPacketOpcodes.LegacyMailList, frame.Header.Opcode);
        Assert.True(LegacyMailListPacket.TryParse(frame, out var packet));
        Assert.Equal(LegacyMailListType.ClaimAllUpdate, packet.Type);
        Assert.Equal(1, packet.Count);
        var mail = Assert.Single(packet.Mails);
        Assert.Equal(mailId, mail.Id);
        Assert.Equal(itemId, mail.ItemId);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
