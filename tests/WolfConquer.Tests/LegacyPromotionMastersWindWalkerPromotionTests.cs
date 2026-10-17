using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersWindWalkerPromotionTests : IDisposable
{
    private const uint WindWalkerPromotionNpcId = 19_634;
    private const long CharacterUid = 1_293_720;
    private const byte PromotionOption = 50;
    private const uint EmeraldItemId = 1_080_001;
    private const uint MeteorItemId = 1_088_001;
    private const uint MoonBoxItemId = 721_080;
    private const uint DragonBallItemId = 1_088_000;
    private const uint PrideFanItemId = 626_079;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersWindWalkerPromotionTests-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<long, long, string, string, ushort> LevelGateCases => new()
    {
        {
            16_000,
            14,
            "Brother, you are not yet qualified to become [Iron Fan]. You should reach Level 15 to be promoted.",
            "I will work hard to reach Level 15.",
            333
        },
        {
            16_001,
            39,
            "Brother, you are not yet qualified to become [Iron Fan]. You should reach Level 40 to be promoted.",
            "I will work hard to reach Level 40.",
            333
        },
        {
            16_002,
            69,
            "There is much that you must learn before you can accept my training. Slaughter a legion of monsters first! [Level 70+]",
            "I~see.",
            6
        },
        {
            16_003,
            99,
            "There is much that you must learn before you can accept my training. Slaughter a legion of monsters first! [Level 100+]",
            "I~see.",
            6
        },
        {
            16_004,
            109,
            "There is much that you must learn before you can accept my training. Slaughter a legion of monsters first! [Level 110+]",
            "I~see.",
            6
        },
    };

    public static TheoryData<long, long, PlayerItemSnapshot, string, uint> MissingItemCases => new()
    {
        {
            16_002,
            70,
            Inventory(Item(uid: 2_000, itemId: 725_016)),
            "I'm sorry but you need to have 1 Emerald in your inventory. Go kill some Hill Monsters (Desert City) to get one.",
            EmeraldItemId
        },
        {
            16_003,
            100,
            Inventory(Item(uid: 2_000, itemId: MeteorItemId, bound: 1)),
            "I'm sorry but you need to have 1 Meteor in your inventory.",
            MeteorItemId
        },
        {
            16_004,
            110,
            Inventory(Item(uid: 2_000, itemId: MoonBoxItemId, bound: 1)),
            "I'm sorry but you need to have 1 MoonBox in your inventory.",
            MoonBoxItemId
        },
    };

    public static TheoryData<long, long, PlayerItemSnapshot, long, uint, string> ItemPromotionCases => new()
    {
        {
            16_002,
            70,
            Inventory(Item(uid: 3_000, itemId: EmeraldItemId, bound: 1, stackSize: 2)),
            16_003,
            EmeraldItemId,
            "Excellent! You have learned the ways of the Taoist. Do you feel a different energy in your body? That is stronger power and battle prowess!"
        },
        {
            16_003,
            100,
            Inventory(Item(uid: 3_001, itemId: MeteorItemId)),
            16_004,
            MeteorItemId,
            "Excellent! Do you feel a different energy in your body? That is stronger power and battle prowess!"
        },
    };

    [Theory]
    [MemberData(nameof(LevelGateCases))]
    public async Task Option50RejectsLevelGatesWithoutMutation(
        long classCode,
        long level,
        string expectedText,
        string expectedOptionText,
        ushort expectedAvatar)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "users-level-" + classCode.ToString(CultureInfo.InvariantCulture));
        var itemsRoot = Path.Combine(_root, "items-level-" + classCode.ToString(CultureInfo.InvariantCulture));
        CreateMinimalCharacterSnapshot(usersRoot, classCode);
        var session = CreateSession(
            classCode,
            level,
            usersRoot: usersRoot,
            playerItems: Inventory(
                Item(uid: 2_000, itemId: EmeraldItemId),
                Item(uid: 2_001, itemId: MeteorItemId),
                Item(uid: 2_002, itemId: MoonBoxItemId)));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerItemsRoot: itemsRoot),
            session,
            WindWalkerPromotionNpcId,
            PromotionOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(classCode, updated.Character.Class);
        Assert.Equal(session.PlayerItems.InventoryItems.Select(item => item.ItemId), updated.PlayerItems.InventoryItems.Select(item => item.ItemId));
        Assert.Empty(ParseItemUsagePackets(sent));
        Assert.Empty(ParseGameItemPackets(sent));
        Assert.Empty(ParseMessagePackets(sent));

        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == expectedText);
        Assert.Contains(
            replies,
            reply => reply.Text == expectedOptionText &&
                reply.OptionId == LegacyNpcReplyPacket.ClosedOptionId);
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar &&
                reply.InputMaxLength == expectedAvatar);
        Assert.Contains(
            "Class=" + classCode.ToString(CultureInfo.InvariantCulture),
            File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini")),
            StringComparison.Ordinal);

        var logText = log.ToString();
        Assert.Contains("WorldServer skipped LegacyNpc PromotionWindWalker promotion", logText);
        Assert.Contains("source=NpcHandler.PromotionWindWalker", logText);
        Assert.Contains("sourceRange=NpcHandler.cs:46279-46379", logText);
        Assert.Contains("option=50", logText);
        Assert.Contains("source level gate rejected", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
        Assert.DoesNotContain("PromotionMaster mutation unavailable", logText);
    }

    [Fact]
    public async Task Option50PromotesDiscipleToPlainFanGuardAndPersistsClass()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "users-disciple");
        CreateMinimalCharacterSnapshot(usersRoot, classCode: 16_000);
        var session = CreateSession(16_000, level: 15, usersRoot: usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            WindWalkerPromotionNpcId,
            PromotionOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(16_001, updated.Character.Class);
        Assert.Empty(ParseItemUsagePackets(sent));
        Assert.Empty(ParseGameItemPackets(sent));

        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "Excellent! You have learned the ways of the Iron Fan school. Do you feel a different energy in your body? That is stronger power and battle prowess!");
        Assert.Contains(replies, reply => reply.Text == "Thank you, Master." && reply.OptionId == LegacyNpcReplyPacket.ClosedOptionId);
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 333);
        Assert.Contains("Class=16001", File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini")), StringComparison.Ordinal);

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionWindWalker promotion", logText);
        Assert.Contains("consumedItemId=0", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
        Assert.DoesNotContain("PromotionMaster mutation unavailable", logText);
    }

    [Fact]
    public async Task Option50RankFortyRequiresTwoSpacesForNonRebornReward()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "users-space-gate");
        CreateMinimalCharacterSnapshot(usersRoot, classCode: 16_001);
        var fullEnoughInventory = Inventory(Enumerable.Range(0, 39)
            .Select(index => Item((uint)(4_000 + index), itemId: 725_016))
            .ToArray());
        var session = CreateSession(
            16_001,
            level: 40,
            usersRoot: usersRoot,
            playerItems: fullEnoughInventory);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            WindWalkerPromotionNpcId,
            PromotionOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(16_001, updated.Character.Class);
        Assert.Equal(39, updated.PlayerItems.InventoryItems.Count);
        Assert.Empty(ParseGameItemPackets(sent));
        Assert.Empty(ParseMessagePackets(sent));

        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "Please make two more spaces in your inventory.");
        Assert.Contains(replies, reply => reply.Text == "Let me check." && reply.OptionId == LegacyNpcReplyPacket.ClosedOptionId);
        Assert.Contains("Class=16001", File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini")), StringComparison.Ordinal);

        var logText = log.ToString();
        Assert.Contains("source inventory space gate rejected", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task Option50RankFortyGrantsTwoBoundPrideFansTalksAndPersists()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "users-rank-forty");
        var itemsRoot = Path.Combine(_root, "items-rank-forty");
        CreateMinimalCharacterSnapshot(usersRoot, classCode: 16_001);
        var session = CreateSession(
            16_001,
            level: 40,
            usersRoot: usersRoot,
            playerItems: Inventory(Item(uid: 5_000, itemId: 725_016)));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldDataCatalog(CreateItemType(PrideFanItemId, "PrideFan", durability: 3198)),
                Sessions: sessions,
                PlayerItemsRoot: itemsRoot),
            session,
            WindWalkerPromotionNpcId,
            PromotionOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(16_002, updated.Character.Class);
        Assert.Equal(3, updated.PlayerItems.InventoryItems.Count);
        var fans = updated.PlayerItems.InventoryItems.Where(item => item.ItemId == PrideFanItemId).ToArray();
        Assert.Equal(2, fans.Length);
        Assert.All(fans, fan =>
        {
            Assert.Equal((byte)1, fan.Bound);
            Assert.Equal((ushort)3198, fan.Durability);
            Assert.Equal((ushort)3198, fan.MaximumDurability);
        });

        var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(CharacterUid);
        Assert.Equal(2, persistedItems.InventoryItems.Count(item => item.ItemId == PrideFanItemId && item.Bound == 1));
        Assert.Contains("Class=16002", File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini")), StringComparison.Ordinal);

        var gameItems = ParseGameItemPackets(sent);
        Assert.Equal(2, gameItems.Count(packet => packet.ItemId == PrideFanItemId && packet.Mode == LegacyGameItemPacket.AddItemMode));
        var talk = Assert.Single(ParseMessagePackets(sent), packet => packet.Content[3] == "You received (Super)PrideFan*2.");
        Assert.Equal(LegacyMsgMessagePacket.TalkChatType, talk.ChatType);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "Excellent! You have learned the ways of the WindWalker. Do you feel a different energy in your body? That is stronger power and battle prowess!");

        var logText = log.ToString();
        Assert.Contains("WorldServer persisted LegacyNpc PromotionMaster item mutation", logText);
        Assert.Contains("grantedItemId=626079", logText);
        Assert.Contains("bound=1", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task Option50RankFortyRebornPromotesWithoutRewardOrSpaceGate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "users-rank-forty-reborn");
        CreateMinimalCharacterSnapshot(usersRoot, classCode: 16_001);
        var fullInventory = Inventory(Enumerable.Range(0, 40)
            .Select(index => Item((uint)(6_000 + index), itemId: 725_016))
            .ToArray());
        var session = CreateSession(16_001, level: 40, reborn: 1, usersRoot: usersRoot, playerItems: fullInventory);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            WindWalkerPromotionNpcId,
            PromotionOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(16_002, updated.Character.Class);
        Assert.Equal(40, updated.PlayerItems.InventoryItems.Count);
        Assert.Empty(ParseGameItemPackets(sent));
        Assert.Empty(ParseMessagePackets(sent));
        Assert.Contains("Class=16002", File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini")), StringComparison.Ordinal);
        Assert.Contains("rebornSkipReward=1", log.ToString());
        Assert.DoesNotContain("PARITY-GAP", log.ToString());
    }

    [Theory]
    [MemberData(nameof(MissingItemCases))]
    public async Task Option50RejectsMissingOrWrongBoundItemsWithoutMutation(
        long classCode,
        long level,
        PlayerItemSnapshot playerItems,
        string expectedText,
        uint expectedItemId)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "users-missing-" + classCode.ToString(CultureInfo.InvariantCulture));
        var itemsRoot = Path.Combine(_root, "items-missing-" + classCode.ToString(CultureInfo.InvariantCulture));
        CreateMinimalCharacterSnapshot(usersRoot, classCode);
        var session = CreateSession(classCode, level, usersRoot: usersRoot, playerItems: playerItems);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerItemsRoot: itemsRoot),
            session,
            WindWalkerPromotionNpcId,
            PromotionOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(classCode, updated.Character.Class);
        Assert.Equal(playerItems.InventoryItems.Select(item => item.ItemId), updated.PlayerItems.InventoryItems.Select(item => item.ItemId));
        Assert.Empty(ParseItemUsagePackets(sent));
        Assert.Empty(ParseGameItemPackets(sent));

        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == expectedText);
        Assert.Contains(replies, reply => reply.Text == "I~see." && reply.OptionId == LegacyNpcReplyPacket.ClosedOptionId);
        Assert.Contains("Class=" + classCode.ToString(CultureInfo.InvariantCulture), File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini")), StringComparison.Ordinal);

        var logText = log.ToString();
        Assert.Contains("source item gate rejected", logText);
        Assert.Contains("itemId=" + expectedItemId.ToString(CultureInfo.InvariantCulture), logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Theory]
    [MemberData(nameof(ItemPromotionCases))]
    public async Task Option50ConsumesPromotionItemPersistsAndPromotes(
        long classCode,
        long level,
        PlayerItemSnapshot playerItems,
        long expectedClass,
        uint consumedItemId,
        string expectedSuccessText)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "users-item-" + classCode.ToString(CultureInfo.InvariantCulture));
        var itemsRoot = Path.Combine(_root, "items-item-" + classCode.ToString(CultureInfo.InvariantCulture));
        CreateMinimalCharacterSnapshot(usersRoot, classCode);
        var session = CreateSession(classCode, level, usersRoot: usersRoot, playerItems: playerItems);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerItemsRoot: itemsRoot),
            session,
            WindWalkerPromotionNpcId,
            PromotionOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(expectedClass, updated.Character.Class);
        Assert.Contains("Class=" + expectedClass.ToString(CultureInfo.InvariantCulture), File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini")), StringComparison.Ordinal);

        var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(CharacterUid);
        if (consumedItemId == EmeraldItemId)
        {
            var emerald = Assert.Single(updated.PlayerItems.InventoryItems, item => item.ItemId == EmeraldItemId);
            Assert.Equal((byte)1, emerald.Bound);
            Assert.Equal((ushort)1, emerald.StackSize);
            Assert.Equal((ushort)1, Assert.Single(persistedItems.InventoryItems, item => item.ItemId == EmeraldItemId).StackSize);
            var updatePacket = Assert.Single(ParseGameItemPackets(sent), packet => packet.ItemId == EmeraldItemId);
            Assert.Equal(LegacyGameItemPacket.UpdateItemMode, updatePacket.Mode);
        }
        else
        {
            Assert.DoesNotContain(updated.PlayerItems.InventoryItems, item => item.ItemId == consumedItemId);
            Assert.DoesNotContain(persistedItems.InventoryItems, item => item.ItemId == consumedItemId);
            var remove = Assert.Single(ParseItemUsagePackets(sent));
            Assert.Equal(3_001U, remove.Id);
            Assert.Equal(MsgItemUsageAction.RemoveInventory, remove.Action);
        }

        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == expectedSuccessText);
        var logText = log.ToString();
        Assert.Contains("WorldServer persisted LegacyNpc PromotionMaster item mutation", logText);
        Assert.Contains("WorldServer handled LegacyNpc PromotionWindWalker promotion", logText);
        Assert.Contains("consumedItemId=" + consumedItemId.ToString(CultureInfo.InvariantCulture), logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task Option50ConsumesMoonBoxGrantsDragonBallTalksAndPromotes()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "users-moonbox");
        var itemsRoot = Path.Combine(_root, "items-moonbox");
        CreateMinimalCharacterSnapshot(usersRoot, classCode: 16_004);
        var session = CreateSession(
            16_004,
            level: 110,
            usersRoot: usersRoot,
            playerItems: Inventory(
                Item(uid: 7_000, itemId: MoonBoxItemId),
                Item(uid: 7_001, itemId: 725_016)));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldDataCatalog(CreateItemType(DragonBallItemId, "DragonBall", durability: 125)),
                Sessions: sessions,
                PlayerItemsRoot: itemsRoot),
            session,
            WindWalkerPromotionNpcId,
            PromotionOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(16_005, updated.Character.Class);
        Assert.DoesNotContain(updated.PlayerItems.InventoryItems, item => item.ItemId == MoonBoxItemId);
        var dragonBall = Assert.Single(updated.PlayerItems.InventoryItems, item => item.ItemId == DragonBallItemId);
        Assert.Equal((ushort)125, dragonBall.Durability);

        var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(CharacterUid);
        Assert.DoesNotContain(persistedItems.InventoryItems, item => item.ItemId == MoonBoxItemId);
        Assert.Contains(persistedItems.InventoryItems, item => item.ItemId == DragonBallItemId);
        Assert.Contains("Class=16005", File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini")), StringComparison.Ordinal);

        var remove = Assert.Single(ParseItemUsagePackets(sent));
        Assert.Equal(7_000U, remove.Id);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, remove.Action);
        var rewardPacket = Assert.Single(ParseGameItemPackets(sent), packet => packet.ItemId == DragonBallItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, rewardPacket.Mode);
        var talk = Assert.Single(ParseMessagePackets(sent), packet => packet.Content[3] == "You received DragonBall.");
        Assert.Equal(LegacyMsgMessagePacket.TalkChatType, talk.ChatType);
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "Excellent! Do you feel a different energy in your body? That is stronger power and battle prowess!");

        var logText = log.ToString();
        Assert.Contains("consumedItemId=721080", logText);
        Assert.Contains("grantedItemId=1088000", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task Option50SendsNoMorePromotionsForMasteredWindWalker()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "users-no-more");
        CreateMinimalCharacterSnapshot(usersRoot, classCode: 16_005);
        var session = CreateSession(16_005, level: 130, usersRoot: usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            WindWalkerPromotionNpcId,
            PromotionOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(16_005, updated.Character.Class);
        Assert.Empty(ParseItemUsagePackets(sent));
        Assert.Empty(ParseGameItemPackets(sent));

        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "You cannot be promoted anymore. You have mastered your class");
        Assert.Contains(replies, reply => reply.Text == "Thank you, Master." && reply.OptionId == LegacyNpcReplyPacket.ClosedOptionId);
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 6);
        Assert.Contains("Class=16005", File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini")), StringComparison.Ordinal);

        var logText = log.ToString();
        Assert.Contains("no source promotion rank matched", logText);
        Assert.DoesNotContain("PromotionMaster mutation unavailable", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static WorldSession CreateSession(
        long classCode,
        long level,
        long reborn = 0,
        string? usersRoot = null,
        PlayerItemSnapshot? playerItems = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "windwalker-promotion-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "WindWalkerHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: classCode,
                FirstClass: classCode,
                SecondClass: 0,
                Reborn: reborn,
                Level: level,
                Experience: 0,
                MapId: 1_002,
                X: 430,
                Y: 378,
                PreviousMapId: 1_002,
                PreviousMapX: 430,
                PreviousMapY: 378,
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
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: playerItems ?? PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private static PlayerItemSnapshot Inventory(params PlayerItemSnapshotItem[] items)
    {
        return PlayerItemSnapshot.Empty with
        {
            InventoryItems = items,
        };
    }

    private static PlayerItemSnapshotItem Item(uint uid, uint itemId, byte bound = 0, ushort stackSize = 1)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Durability = 1,
            MaximumDurability = 1,
            Position = 0,
            Bound = bound,
            StackSize = stackSize,
        };
    }

    private static void CreateMinimalCharacterSnapshot(string usersRoot, long classCode)
    {
        Directory.CreateDirectory(usersRoot);
        File.WriteAllText(
            Path.Combine(usersRoot, CharacterUid + ".ini"),
            "[Character]" + Environment.NewLine +
            "UID=" + CharacterUid + Environment.NewLine +
            "Class=" + classCode + Environment.NewLine);
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

    private static IReadOnlyList<MsgItemUsagePacket> ParseItemUsagePackets(IEnumerable<byte[]> sent)
    {
        var packets = new List<MsgItemUsagePacket>();
        foreach (var bytes in sent)
        {
            if (MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyGameItemPacket> ParseGameItemPackets(IEnumerable<byte[]> sent)
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

    private static IReadOnlyList<LegacyMsgMessagePacket> ParseMessagePackets(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyMsgMessagePacket>();
        foreach (var bytes in sent)
        {
            if (LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet))
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

    private static WorldDataCatalog CreateWorldDataCatalog(params ItemType[] items)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-05T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://windwalker-promotion",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: items,
            itemAddBonuses: [],
            levelExperience: [],
            runeLevelExperience: [],
            runeStorageAttributes: [],
            itemRefineEffects: [],
            itemRefineEffectEx: [],
            itemRefineUpgrades: [],
            transformInfo: [],
            furniture: [],
            hairColorTypes: [],
            hairFaceStorageTypes: [],
            coatStorageTypes: [],
            coatStorageAttributes: [],
            coatColorRules: [],
            xuanbaoAdditionAttributes: [],
            xuanbaoComposeAttributeLimits: [],
            xuanbaoRandomAttributes: [],
            christmasNpcs: [],
            tutorTypes: [],
            tutorBattleLimitTypes: [],
            beastAttributes: [],
            fateExperience: [],
            spiritRates: [],
            hundredWeapons: [],
            gouyuTypes: [],
            gouyuImmortals: [],
            specialEffectAttributes: [],
            daoqiDictTypes: [],
            syndicateFormationTypes: [],
            syndicateSkillTypes: [],
            disdainRules: [],
            jiangHuAttributes: [],
            jiangHuCultivateStatuses: [],
            refineryBoxes: [],
            refinerySouls: [],
            refineryMaterials: [],
            stateCountFiles: [],
            binarySeedFiles: [],
            emptySeedFiles: [],
            backupSeedFiles: [],
            pokerTables: [],
            rouletteTables: [],
            npcTimeEntries: [],
            activityTasks: new ActivityTaskCatalog(new ActivityTaskMetadata(0, "", 0, []), [], "fixture://activity"),
            meltingTypeRules: [],
            lotteryPrizes: [],
            arenaRecords: [],
            elitePkRecords: [],
            teamElitePkRecords: [],
            skillTeamPkRecords: [],
            classPkWarRecords: [],
            combatGearRanks: [],
            prestigeRanks: [],
            innerPowerRanks: [],
            runeRankings: [],
            staticStatues: [],
            newPlayerRewards: [],
            votes: [],
            couplesPkRecords: [],
            socialAssociations: [],
            guildWarInfo: EmptyGuildWarData("guildwar"),
            superGuildWarInfo: EmptyGuildWarData("superguildwar"),
            classLevelStats: [],
            professionLevelUps: [],
            professionLevelBenefits: [],
            professionTitleBenefits: [],
            questInfo: new QuestInfoMetadata(ActionBase: null, RecommendInterval: null, TotalMission: null),
            questTaskTypes: [],
            questMissions: [],
            quizQuestions: [],
            magic: [],
            magicEffects: [],
            magicTypeOperations: [],
            titleTypes: [],
            combatGear: [],
            traps: [],
            npcs: [],
            sobNpcs: [],
            portals: [],
            maps: [],
            mapNames: [],
            monsters: [],
            monsterSpawns: [],
            lotteryDictionary: [],
            collectionRewardTiers: [],
            taskRewardTypes: [],
            taskRewardTypeIniEntries: [],
            neiGongHeader: new NeiGongHeader(0, [], "fixture://neigong", 0, "", new Dictionary<string, string>()),
            neiGongSecrets: [],
            neiGongTypes: [],
            subProfessions: [],
            syndicateLevels: [],
            stageGoalInfo: new StageGoalInfo(0),
            stageGoalStages: [],
            stageGoalTasks: [],
            shops: [],
            shopItems: [],
            exchangeShopGoods: [],
            exchangeShopGoodsEx: [],
            exchangeShopLevels: [],
            honorShopItems: [],
            honorShopRecommendations: [],
            racePointShopItems: [],
            racePointShopRecommendations: [],
            goldenLeagueShopItems: [],
            goldenLeagueShopRecommendations: [],
            goldenLeagueShopConfig: new Dictionary<string, string>(),
            booths: [],
            boothItems: []);
    }

    private static ItemType CreateItemType(uint itemTypeId, string name, ushort durability)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemTypeId.ToString(CultureInfo.InvariantCulture);
        fields[1] = name;
        fields[ItemType.DurabilityFieldIndex] = durability.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)itemTypeId), name, fields);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }
}
