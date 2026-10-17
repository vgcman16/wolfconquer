using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersNinjaAdvancedPromotionTests : IDisposable
{
    private const uint NinjaPromotionNpcId = 4_972;
    private const long CharacterUid = 1_293_670;
    private const byte AdvancedPromotionOption = 20;
    private const uint EmeraldItemId = 1_080_001;
    private const uint MeteorItemId = 1_088_001;
    private const uint MoonBoxItemId = 721_080;
    private const uint DragonBallItemId = 1_088_000;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersNinjaAdvancedPromotionTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Option20PromotesRankOneAtLevel40WithoutItem()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "Users-rank-one");
        CreateMinimalCharacterSnapshot(usersRoot, classCode: 5_001);
        var session = CreateSession(classCode: 5_001, level: 40, usersRoot: usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions),
            session,
            NinjaPromotionNpcId,
            AdvancedPromotionOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(5_002, updated.Character.Class);
        Assert.Empty(ParseItemUsage(sent));
        Assert.Empty(ParseGameItemPackets(sent));
        Assert.Empty(ParseSystemTalk(sent));

        var replies = ParseNpcReplies(sent);
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
                reply.Text == "Excellent! You`ve learned ways of the Ninja. Do you feel a different energy in your body? That`s stronger power and battle prowess!");
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 203);

        var persistedCharacter = File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini"));
        Assert.Contains("Class=5002", persistedCharacter, StringComparison.Ordinal);
        Assert.DoesNotContain("PARITY-GAP", log.ToString());
    }

    [Fact]
    public async Task Option20ConsumesEmeraldAndPromotesRankTwo()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "Users-emerald");
        var itemsRoot = Path.Combine(_root, "Items-emerald");
        CreateMinimalCharacterSnapshot(usersRoot, classCode: 5_002);
        var emerald = CreateInventoryItem(uid: 2_000, EmeraldItemId);
        var session = CreateSession(
            classCode: 5_002,
            level: 70,
            usersRoot: usersRoot,
            playerItems: CreateInventory(emerald));
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
            NinjaPromotionNpcId,
            AdvancedPromotionOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(5_003, updated.Character.Class);
        Assert.DoesNotContain(updated.PlayerItems.InventoryItems, item => item.ItemId == EmeraldItemId);
        Assert.DoesNotContain(
            new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(CharacterUid).InventoryItems,
            item => item.ItemId == EmeraldItemId);
        Assert.Contains(ParseItemUsage(sent), packet => packet.Id == emerald.Uid && packet.Action == MsgItemUsageAction.RemoveInventory);
        Assert.Empty(ParseGameItemPackets(sent));
        Assert.Empty(ParseSystemTalk(sent));

        var persistedCharacter = File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini"));
        Assert.Contains("Class=5003", persistedCharacter, StringComparison.Ordinal);
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
                reply.Text == "Excellent! You`ve learned ways of the Taoist. Do you feel a different energy in your body? That`s stronger power and battle prowess!");
        Assert.DoesNotContain("PARITY-GAP", log.ToString());
    }

    [Fact]
    public async Task Option20RejectsRankThreeWithoutMeteor()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "Users-missing-meteor");
        var itemsRoot = Path.Combine(_root, "Items-missing-meteor");
        CreateMinimalCharacterSnapshot(usersRoot, classCode: 5_003);
        var session = CreateSession(
            classCode: 5_003,
            level: 100,
            usersRoot: usersRoot,
            playerItems: CreateInventory(CreateInventoryItem(uid: 2_100, EmeraldItemId)));
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
            NinjaPromotionNpcId,
            AdvancedPromotionOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(5_003, updated.Character.Class);
        Assert.Contains(updated.PlayerItems.InventoryItems, item => item.ItemId == EmeraldItemId);
        Assert.False(File.Exists(Path.Combine(itemsRoot, CharacterUid + ".bin")));
        Assert.Empty(ParseItemUsage(sent));
        Assert.Empty(ParseGameItemPackets(sent));
        Assert.Empty(ParseSystemTalk(sent));

        var replies = ParseNpcReplies(sent);
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
                reply.Text == "I'm sorry but you need to have 1 Meteor at your inventory.");
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Option &&
                reply.Text == "I~see." &&
                reply.OptionId == LegacyNpcReplyPacket.ClosedOptionId);
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 6);

        var persistedCharacter = File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini"));
        Assert.Contains("Class=5003", persistedCharacter, StringComparison.Ordinal);
        Assert.Contains("requiredItemId=" + MeteorItemId, log.ToString());
        Assert.DoesNotContain("PARITY-GAP", log.ToString());
    }

    [Fact]
    public async Task Option20ConsumesMoonBoxGrantsDragonBallAndPromotesRankFour()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "Users-moonbox");
        var itemsRoot = Path.Combine(_root, "Items-moonbox");
        CreateMinimalCharacterSnapshot(usersRoot, classCode: 5_004);
        var moonBox = CreateInventoryItem(uid: 3_000, MoonBoxItemId);
        var otherItem = CreateInventoryItem(uid: 4_000, itemId: 725_016);
        var session = CreateSession(
            classCode: 5_004,
            level: 110,
            usersRoot: usersRoot,
            playerItems: CreateInventory(moonBox, otherItem));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldDataCatalog(CreateItemType(DragonBallItemId, "DragonBall", durability: 7)),
                Sessions: sessions,
                PlayerItemsRoot: itemsRoot),
            session,
            NinjaPromotionNpcId,
            AdvancedPromotionOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(5_005, updated.Character.Class);
        Assert.DoesNotContain(updated.PlayerItems.InventoryItems, item => item.ItemId == MoonBoxItemId);
        var dragonBall = Assert.Single(updated.PlayerItems.InventoryItems, item => item.ItemId == DragonBallItemId);
        Assert.Equal(4_001U, dragonBall.Uid);
        Assert.Equal((ushort)7, dragonBall.Durability);
        Assert.Equal((ushort)7, dragonBall.MaximumDurability);

        var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(CharacterUid);
        Assert.DoesNotContain(persistedItems.InventoryItems, item => item.ItemId == MoonBoxItemId);
        Assert.Contains(persistedItems.InventoryItems, item => item.ItemId == DragonBallItemId);
        var persistedCharacter = File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini"));
        Assert.Contains("Class=5005", persistedCharacter, StringComparison.Ordinal);

        Assert.Contains(ParseItemUsage(sent), packet => packet.Id == moonBox.Uid && packet.Action == MsgItemUsageAction.RemoveInventory);
        var gameItems = ParseGameItemPackets(sent);
        var addDragonBall = Assert.Single(gameItems, entry => entry.Packet.ItemId == DragonBallItemId);
        Assert.Equal(4_001U, addDragonBall.Packet.Uid);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, addDragonBall.Packet.Mode);
        Assert.Equal((ushort)7, addDragonBall.Packet.Durability);
        var systemTalk = Assert.Single(ParseSystemTalk(sent), entry => entry.Packet.Content[3] == "You received DragonBall.");
        Assert.True(addDragonBall.Index < systemTalk.Index);

        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
                reply.Text == "Excellent!Do you feel a different energy in your body? That`s stronger power and battle prowess!");
        Assert.DoesNotContain("PARITY-GAP", log.ToString());
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
        string? usersRoot = null,
        PlayerItemSnapshot? playerItems = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "ninja-advanced-promotion-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "NinjaHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: classCode,
                FirstClass: classCode,
                SecondClass: 0,
                Reborn: 0,
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

    private static PlayerItemSnapshot CreateInventory(params PlayerItemSnapshotItem[] items)
    {
        return PlayerItemSnapshot.Empty with
        {
            InventoryItems = items,
        };
    }

    private static PlayerItemSnapshotItem CreateInventoryItem(uint uid, uint itemId)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Durability = 1,
            MaximumDurability = 1,
            Position = 0,
            StackSize = 1,
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

    private static IReadOnlyList<MsgItemUsagePacket> ParseItemUsage(IEnumerable<byte[]> sent)
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

    private static IReadOnlyList<(int Index, LegacyGameItemPacket Packet)> ParseGameItemPackets(
        IReadOnlyList<byte[]> sent)
    {
        var packets = new List<(int Index, LegacyGameItemPacket Packet)>();
        for (var index = 0; index < sent.Count; index++)
        {
            if (LegacyGameItemPacket.TryParse(ReadFrame(sent[index]), out var packet))
            {
                packets.Add((index, packet));
            }
        }

        return packets;
    }

    private static IReadOnlyList<(int Index, LegacyMsgMessagePacket Packet)> ParseSystemTalk(
        IReadOnlyList<byte[]> sent)
    {
        var packets = new List<(int Index, LegacyMsgMessagePacket Packet)>();
        for (var index = 0; index < sent.Count; index++)
        {
            if (LegacyMsgMessagePacket.TryParse(ReadFrame(sent[index]), out var packet) &&
                packet.ChatType == LegacyMsgMessagePacket.TalkChatType)
            {
                packets.Add((index, packet));
            }
        }

        return packets;
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
                "fixture://ninja-advanced-promotion",
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
