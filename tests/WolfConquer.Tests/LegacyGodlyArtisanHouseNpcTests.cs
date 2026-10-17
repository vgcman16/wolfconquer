using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyGodlyArtisanHouseNpcTests
{
    private const uint GodlyArtisanNpcId = 10_065;
    private const uint HouseAdminNpcId = 30_156;
    private const uint CharacterUid = 1_293_600;
    private const uint RepairItemId = 120_001;

    [Fact]
    public async Task HandlerGodlyArtisanSendsRootAndTalismanDialogs()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(GodlyArtisanNpcId, optionId: 0, interactType: 1).ToFrameBytes()));

        var rootReplies = ParseNpcReplies(sent);
        Assert.Contains(rootReplies, reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
            reply.Text.Contains("repairing maximum durability", StringComparison.Ordinal));
        Assert.Contains(rootReplies, reply => reply.InteractType == LegacyNpcReplyInteractType.Option &&
            reply.OptionId == 20 &&
            reply.Text.Contains("Talisman", StringComparison.Ordinal));
        Assert.Contains("source=NpcHandler.GodlyArtisan", log.ToString());

        sent.Clear();
        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(GodlyArtisanNpcId, optionId: 20, interactType: 1).ToFrameBytes()));

        var talismanReplies = ParseNpcReplies(sent);
        Assert.Contains(talismanReplies, reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
            reply.Text.Contains("Which talisman", StringComparison.Ordinal));
        Assert.Contains(talismanReplies, reply => reply.InteractType == LegacyNpcReplyInteractType.Option &&
            reply.OptionId == 11 &&
            reply.Text.Contains("Attack", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HandlerGodlyArtisanRepairsEquippedMaximumDurabilityFromItemType()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var equipment = Item(uid: 50, itemId: RepairItemId, position: 2, durability: 700, maximumDurability: 700);
        var session = CreateSession(equipmentItems: [equipment]);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldData(itemDurability: 1_200),
                Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(GodlyArtisanNpcId, optionId: 2, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        var repaired = Assert.Single(updated.PlayerItems.EquipmentItems);
        Assert.Equal((ushort)1_200, repaired.Durability);
        Assert.Equal((ushort)1_200, repaired.MaximumDurability);

        var itemPacket = Assert.Single(ParseGameItems(sent));
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, itemPacket.Mode);
        Assert.Equal((ushort)1_200, itemPacket.Durability);
        Assert.Equal((ushort)1_200, itemPacket.MaximumDurability);
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text.Contains("repaired successfully", StringComparison.Ordinal));
        Assert.Contains("WorldServer handled LegacyNpc GodlyArtisan max durability repair", log.ToString());
    }

    [Fact]
    public async Task HandlerGodlyArtisanRejectsNormalDurabilityRepairBeforeMaximumRepair()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            equipmentItems: [Item(uid: 51, itemId: RepairItemId, position: 2, durability: 600, maximumDurability: 700)]);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldData(itemDurability: 1_200),
                Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(GodlyArtisanNpcId, optionId: 2, interactType: 1).ToFrameBytes()));

        Assert.Empty(ParseGameItems(sent));
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text.Contains("shopkeeper to repair this item first", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HandlerHouseAdminClassSixDialogAndTeleportAreDeterministic()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(HouseAdminNpcId, optionId: 20, interactType: 1).ToFrameBytes()));

        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
            reply.Text.Contains("Class 6 House Admin", StringComparison.Ordinal));
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Option &&
            reply.OptionId == 21);

        sent.Clear();
        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(HouseAdminNpcId, optionId: 21, interactType: 1).ToFrameBytes()));

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[0]), out var action));
        Assert.Equal(MsgActionType.Teleport, action.Type);
        Assert.Equal(3_995U, action.DwParam);
        Assert.Equal(127U, action.PositionX);
        Assert.Equal(138U, action.PositionY);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(3_995, updated.Character.MapId);
        Assert.Equal(127, updated.Character.X);
        Assert.Equal(138, updated.Character.Y);
        Assert.Contains("source=NpcHandler.HouseAdmin", log.ToString());
    }

    [Fact]
    public async Task HandlerHouseAdminFencesHouseStateAndMutationBranches()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(HouseAdminNpcId, optionId: 1, interactType: 1).ToFrameBytes()));

        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text.Contains("House management is not available", StringComparison.Ordinal));
        Assert.Contains("PARITY-GAP LegacyNpc HouseAdmin source=NpcHandler.HouseAdmin", log.ToString());
    }

    private static WorldSession CreateSession(IReadOnlyList<PlayerItemSnapshotItem>? equipmentItems = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "godly-house-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "HouseTester",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: 1_002,
                X: 300,
                Y: 300,
                PreviousMapId: 1_002,
                PreviousMapX: 300,
                PreviousMapY: 300,
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
                Attributes: new Dictionary<string, string>(),
                PaidNobility: new Dictionary<string, string>()),
            PlayerItems: new PlayerItemSnapshot([], [], [], equipmentItems ?? [], []),
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static PlayerItemSnapshotItem Item(
        uint uid,
        uint itemId,
        ushort position,
        ushort durability,
        ushort maximumDurability)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Position = position,
            Durability = durability,
            MaximumDurability = maximumDurability,
        };
    }

    private static WorldDataCatalog CreateWorldData(ushort itemDurability)
    {
        var itemFields = Enumerable.Repeat("0", 66).ToArray();
        itemFields[ItemType.DurabilityFieldIndex] = itemDurability.ToString();

        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-20T00:00:00Z"),
                "fixture://godly-artisan-house",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: [new ItemType((int)RepairItemId, "FixtureNecklace", itemFields)],
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
            npcs: [new Npc((int)GodlyArtisanNpcId, 0, 0, 1_002, 301, 300, null)],
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

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData($"fixture://{name}", [], [], [], info: null, pole: null, conductors: []);
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

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
