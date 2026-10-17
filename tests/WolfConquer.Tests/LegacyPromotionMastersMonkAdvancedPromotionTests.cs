using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersMonkAdvancedPromotionTests : IDisposable
{
    private const uint MonkPromotionNpcId = 8_314;
    private const long CharacterUid = 1_293_601;
    private const byte AdvancedPromotionOption = 22;
    private const uint EmeraldItemId = 1_080_001;
    private const uint MeteorItemId = 1_088_001;
    private const uint MoonBoxItemId = 721_080;
    private const uint DragonBallItemId = 1_088_000;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersMonkAdvancedPromotionTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Option22Class6001AtLevel40PromotesWithoutItem()
    {
        var result = await HandlePromotionAsync(
            caseName: "first-rank",
            classCode: 6_001,
            level: 40);

        Assert.Equal(6_002, result.UpdatedSession.Character.Class);
        Assert.Empty(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.Empty(ParseGameItemPackets(result.Sent));
        Assert.DoesNotContain(ParseMessages(result.Sent), entry => entry.Packet.Content[3] == "You received DragonBall.");
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
                reply.Text == "Excellent! You`ve learned ways of the Monk. Do you feel a different energy in your body? That`s stronger power and battle prowess!");
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar &&
                reply.InputMaxLength == 226);
        AssertPersistedClass(result.UsersRoot, 6_002);
        Assert.Contains("WorldServer handled LegacyNpc PromotionMonk advanced promotion", result.Log);
        Assert.DoesNotContain("consumedItemId=", result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log);
    }

    [Fact]
    public async Task Option22Class6002ConsumesEmeraldAndPromotes()
    {
        var result = await HandlePromotionAsync(
            caseName: "emerald",
            classCode: 6_002,
            level: 70,
            playerItems: Inventory(Item(2_000, EmeraldItemId), Item(2_001, 725_016)));

        Assert.Equal(6_003, result.UpdatedSession.Character.Class);
        Assert.Equal([725_016U], result.UpdatedSession.PlayerItems.InventoryItems.Select(item => item.ItemId).ToArray());
        Assert.Equal([725_016U], new PlayerItemSnapshotLoader(result.ItemsRoot)
            .LoadByCharacterUid(CharacterUid)
            .InventoryItems
            .Select(item => item.ItemId)
            .ToArray());
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
                reply.Text == "Excellent! You`ve learned ways of the Taoist. Do you feel a different energy in your body? That`s stronger power and battle prowess!");
        AssertPersistedClass(result.UsersRoot, 6_003);
        Assert.Contains("consumedItemId=1080001 consumedCount=1", result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log);
    }

    [Fact]
    public async Task Option22Class6003MissingMeteorRejectsWithoutMutation()
    {
        var result = await HandlePromotionAsync(
            caseName: "missing-meteor",
            classCode: 6_003,
            level: 100,
            playerItems: Inventory(Item(2_000, EmeraldItemId)));

        Assert.Equal(6_003, result.UpdatedSession.Character.Class);
        Assert.Equal([EmeraldItemId], result.UpdatedSession.PlayerItems.InventoryItems.Select(item => item.ItemId).ToArray());
        Assert.False(File.Exists(Path.Combine(result.ItemsRoot, CharacterUid + ".bin")));
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
                reply.Text == "I'm sorry but you need to have 1 Meteor at your inventory.");
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            reply => reply.InteractType == LegacyNpcReplyInteractType.Option &&
                reply.Text == "I~see." &&
                reply.OptionId == LegacyNpcReplyPacket.ClosedOptionId);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar &&
                reply.InputMaxLength == 6);
        AssertPersistedClass(result.UsersRoot, 6_003);
        Assert.Contains("WorldServer skipped LegacyNpc PromotionMonk advanced promotion", result.Log);
        Assert.DoesNotContain("consumedItemId=", result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log);
    }

    [Fact]
    public async Task Option22Class6004ConsumesMoonBoxRewardsDragonBallAndPromotes()
    {
        var result = await HandlePromotionAsync(
            caseName: "moonbox",
            classCode: 6_004,
            level: 110,
            playerItems: Inventory(Item(2_000, MoonBoxItemId), Item(2_010, 725_016)),
            worldData: CreateWorldDataCatalog(CreateItemType(DragonBallItemId, durability: 321)));

        Assert.Equal(6_005, result.UpdatedSession.Character.Class);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == MoonBoxItemId);
        var dragonBall = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == DragonBallItemId);
        Assert.Equal(2_011U, dragonBall.Uid);
        Assert.Equal((ushort)321, dragonBall.Durability);
        Assert.Equal((ushort)321, dragonBall.MaximumDurability);

        var persistedItems = new PlayerItemSnapshotLoader(result.ItemsRoot).LoadByCharacterUid(CharacterUid);
        Assert.DoesNotContain(persistedItems.InventoryItems, item => item.ItemId == MoonBoxItemId);
        Assert.Contains(persistedItems.InventoryItems, item => item.ItemId == DragonBallItemId);
        AssertPersistedClass(result.UsersRoot, 6_005);

        var itemPacket = Assert.Single(ParseGameItemPackets(result.Sent));
        Assert.Equal(DragonBallItemId, itemPacket.Packet.ItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, itemPacket.Packet.Mode);
        var message = Assert.Single(ParseMessages(result.Sent), entry => entry.Packet.Content[3] == "You received DragonBall.");
        Assert.Equal(LegacyMsgMessagePacket.TalkChatType, message.Packet.ChatType);
        Assert.True(itemPacket.Index < message.Index);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
                reply.Text == "Excellent!Do you feel a different energy in your body? That`s stronger power and battle prowess!");
        Assert.Contains("consumedItemId=721080 consumedCount=1 rewardItemId=1088000 rewardCount=1", result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<PromotionResult> HandlePromotionAsync(
        string caseName,
        long classCode,
        long level,
        PlayerItemSnapshot? playerItems = null,
        WorldDataCatalog? worldData = null)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, caseName, "Users");
        var itemsRoot = Path.Combine(_root, caseName, "Items");
        Directory.CreateDirectory(itemsRoot);
        CreateMinimalCharacterSnapshot(usersRoot, classCode);
        var session = CreateSession(classCode, level, usersRoot, playerItems ?? PlayerItemSnapshot.Empty);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: worldData,
                Sessions: sessions,
                PlayerItemsRoot: itemsRoot),
            session,
            MonkPromotionNpcId,
            AdvancedPromotionOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        return new PromotionResult(updated, sent, log.ToString(), usersRoot, itemsRoot);
    }

    private static WorldSession CreateSession(
        long classCode,
        long level,
        string usersRoot,
        PlayerItemSnapshot playerItems)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "monk-advanced-promotion-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "MonkHero",
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
            PlayerItems: playerItems,
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

    private static PlayerItemSnapshotItem Item(uint uid, uint itemId)
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

    private static void AssertPersistedClass(string usersRoot, long expectedClass)
    {
        var persistedCharacter = File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini"));
        Assert.Contains("Class=" + expectedClass, persistedCharacter, StringComparison.Ordinal);
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

    private static IReadOnlyList<(int Index, LegacyMsgMessagePacket Packet)> ParseMessages(
        IReadOnlyList<byte[]> sent)
    {
        var packets = new List<(int Index, LegacyMsgMessagePacket Packet)>();
        for (var index = 0; index < sent.Count; index++)
        {
            if (LegacyMsgMessagePacket.TryParse(ReadFrame(sent[index]), out var packet))
            {
                packets.Add((index, packet));
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
                "fixture://monk-advanced-promotion",
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

    private static ItemType CreateItemType(uint itemTypeId, ushort durability)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemTypeId.ToString(CultureInfo.InvariantCulture);
        fields[1] = "DragonBall";
        fields[ItemType.DurabilityFieldIndex] = durability.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)itemTypeId), "DragonBall", fields);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    private sealed record PromotionResult(
        WorldSession UpdatedSession,
        List<byte[]> Sent,
        string Log,
        string UsersRoot,
        string ItemsRoot);
}
