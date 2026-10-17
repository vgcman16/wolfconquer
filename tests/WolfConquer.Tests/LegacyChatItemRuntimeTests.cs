using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyChatItemRuntimeTests
{
    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> Registry =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    [Fact]
    public async Task D13LinkedTalkThenShowItemReturnsChatModeItemAndExtras()
    {
        const uint itemUid = 700_001;
        var item = CreateItem(itemUid);
        var session = CreateSession("Owner", new PlayerItemSnapshot([item], [], [], [], []));
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var chatItems = new LegacyChatItemStore();
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            Sessions: sessions,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13,
            ChatItems: chatItems);

        await Registry.HandleAsync(
            context,
            ReadFrame(CreateTalk("Spoofed", $"inspect [Item Blade {itemUid} now]").ToFrameBytes()));

        Assert.Empty(sent);
        Assert.Equal(1, chatItems.Count);

        await Registry.HandleAsync(
            context,
            ReadFrame(new MsgItemUsagePacket(
                itemUid,
                DwParam: 0,
                MsgItemUsageAction.ShowItem,
                Timestamp: 0,
                DwParam2: 0,
                DwParam3: 0,
                DwParam4: 0).ToFrameBytes()));

        Assert.Equal(2, sent.Count);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[0]), out var shown));
        Assert.Equal(itemUid, shown.Uid);
        Assert.Equal(item.ItemId, shown.ItemId);
        Assert.Equal(LegacyGameItemPacket.ChatItemMode, shown.Mode);
        Assert.Equal(item.Plus, shown.Plus);
        Assert.Equal(item.OwnerName, shown.OwnerName);
        Assert.True(LegacyItemExtraPacket.TryParse(ReadFrame(sent[1]), out var extra));
        Assert.Single(extra.Purifications);
        Assert.Single(extra.Refineries);
        Assert.Contains("linkedItems=1", log.ToString());
        Assert.Contains("served D13 chat item", log.ToString());
    }

    [Fact]
    public async Task D13TalkOnlyCachesItemsOwnedInInventoryOrEquipment()
    {
        const uint inventoryUid = 700_010;
        const uint equipmentUid = 700_011;
        var session = CreateSession(
            "Owner",
            new PlayerItemSnapshot(
                [CreateItem(inventoryUid)],
                [],
                [],
                [CreateItem(equipmentUid)],
                []));
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var store = new LegacyChatItemStore();
        var context = new WorldPacketHandlerContext(
            connection,
            TextWriter.Null,
            session,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13,
            ChatItems: store);

        await Registry.HandleAsync(
            context,
            ReadFrame(CreateTalk(
                "Owner",
                $"[Item Bag {inventoryUid} x] [Item Gear {equipmentUid} x] [Item Fake 999999 x]")
                .ToFrameBytes()));

        Assert.Equal(2, store.Count);
        Assert.True(store.TryGet(inventoryUid, out _));
        Assert.True(store.TryGet(equipmentUid, out _));
        Assert.False(store.TryGet(999_999, out _));
    }

    [Fact]
    public async Task RootProfileDoesNotServeD13ShowItemAction()
    {
        const uint itemUid = 700_020;
        var session = CreateSession("Owner", new PlayerItemSnapshot([CreateItem(itemUid)], [], [], [], []));
        var store = new LegacyChatItemStore();
        Assert.Equal(1, store.CaptureLinks(session, $"[Item Blade {itemUid} x]", itemsById: null));
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            CreateConnection(sent),
            log,
            session,
            ProtocolProfile: ConquerProtocolProfile.RootD11_7899,
            ChatItems: store);

        await Registry.HandleAsync(
            context,
            ReadFrame(new MsgItemUsagePacket(
                itemUid,
                DwParam: 0,
                MsgItemUsageAction.ShowItem,
                Timestamp: 0,
                DwParam2: 0,
                DwParam3: 0,
                DwParam4: 0).ToFrameBytes()));

        Assert.Empty(sent);
        Assert.Contains("only verified in the VisibleDx8D13 executable", log.ToString());
    }

    [Fact]
    public async Task D13TalkForwardsNearbyMessageWithServerOwnedSenderIdentity()
    {
        var sender = CreateSession("Owner", PlayerItemSnapshot.Empty, x: 430);
        var recipient = CreateSession("Nearby", PlayerItemSnapshot.Empty, x: 431, uid: 1_002);
        var senderSent = new List<byte[]>();
        var recipientSent = new List<byte[]>();
        var senderConnection = CreateConnection(senderSent);
        var recipientConnection = CreateConnection(recipientSent);
        var sessions = new WorldSessionStore();
        sessions.Set(senderConnection, sender);
        sessions.Set(recipientConnection, recipient);
        var context = new WorldPacketHandlerContext(
            senderConnection,
            TextWriter.Null,
            sender,
            Sessions: sessions,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13,
            ChatItems: new LegacyChatItemStore());

        await Registry.HandleAsync(
            context,
            ReadFrame(CreateTalk("Spoofed", "hello").ToFrameBytes()));

        Assert.Empty(senderSent);
        var bytes = Assert.Single(recipientSent);
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var relayed));
        Assert.Equal("Owner", relayed.Content[0]);
        Assert.Equal("hello", relayed.Content[3]);
        Assert.Equal(1003U, relayed.Mesh);
    }

    private static LegacyMsgMessagePacket CreateTalk(string sender, string message)
    {
        return new LegacyMsgMessagePacket(
            LegacyMsgMessagePacket.WhiteColor,
            LegacyMsgMessagePacket.TalkChatType,
            MessageUid1: 0,
            MessageUid2: 0,
            ServerId: 0,
            Mesh: 0,
            UnionType: 0,
            Count: 1,
            New1: 0,
            New2: 0,
            [sender, "ALL", string.Empty, message, string.Empty, string.Empty, string.Empty]);
    }

    private static PlayerItemSnapshotItem CreateItem(uint uid)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = 410_339,
            Durability = 75,
            MaximumDurability = 100,
            Position = 0,
            Plus = 9,
            StackSize = 1,
            PurificationItemId = 820_001,
            PurificationLevel = 3,
            RefineryEffectId = 724_440,
            RefineryEffectLevel = 2,
            RefineryEffectPercent = 10,
            RefineryEffectPercent2 = 20,
            OwnerName = "OriginalOwner",
        };
    }

    private static WorldSession CreateSession(
        string name,
        PlayerItemSnapshot items,
        int x = 430,
        uint uid = 1_001)
    {
        return new WorldSession(
            AccountId: uid,
            Username: name.ToLowerInvariant(),
            Character: new CharacterSnapshot(
                Uid: uid,
                Name: name,
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 10,
                FirstClass: 10,
                SecondClass: 0,
                Reborn: 0,
                Level: 120,
                Experience: 0,
                MapId: 1002,
                X: x,
                Y: 380,
                PreviousMapId: 1002,
                PreviousMapX: x,
                PreviousMapY: 380,
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
                Online: 1,
                SourceFile: uid.ToString(CultureInfo.InvariantCulture) + ".ini",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: items,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static ConquerTcpConnection CreateConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9_958),
            sendAsync: (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(
            new ReadOnlySequence<byte>(bytes),
            out var frame,
            out _,
            out _));
        return frame;
    }
}
