using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMelterPacketTests
{
    [Fact]
    public void LegacyMelterOpcodesMatchLegacyFinalize()
    {
        Assert.Equal((ushort)2071, WorldPacketOpcodes.LegacyMelterRankList);
        Assert.Equal((ushort)2232, WorldPacketOpcodes.LegacyMelterOpt);
        Assert.Equal(WorldPacketOpcodes.LegacyMelterRankList, LegacyMelterRankListPacket.Opcode);
        Assert.Equal(WorldPacketOpcodes.LegacyMelterOpt, LegacyMelterOptPacket.Opcode);
    }

    [Fact]
    public void LegacyMelterRankListQueryRoundTrips()
    {
        var packet = LegacyMelterRankListPacket.Query(page: 2);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyMelterRankListPacket.TryParse(frame, out var parsed));
        Assert.Equal(0U, parsed.CountInPage);
        Assert.Equal(2U, parsed.Page);
        Assert.Equal(0U, parsed.Count);
        Assert.Empty(parsed.Entries);
    }

    [Fact]
    public void LegacyMelterRankListRoundTripsEntry()
    {
        var packet = new LegacyMelterRankListPacket(
            CountInPage: 1,
            Page: 3,
            Count: 8,
            Entries:
            [
                new LegacyMelterRankListEntry(
                    Type: 8,
                    ItemId: 721_450,
                    Bound: true,
                    Name: "FixtureMelter",
                    Unknown3: 1_559_853_103),
            ]);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyMelterRankListPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet.CountInPage, parsed.CountInPage);
        Assert.Equal(packet.Page, parsed.Page);
        Assert.Equal(packet.Count, parsed.Count);
        Assert.Equal(packet.Entries, parsed.Entries);
    }

    [Fact]
    public void LegacyMelterOptRoundTripsItems()
    {
        var packet = new LegacyMelterOptPacket(
            Type: 0,
            EntityId: 41_001,
            EntityName: "FixtureMelter",
            Items:
            [
                new LegacyMelterOptItem(
                    Type: 8,
                    Id: 721_450,
                    Bound: true,
                    AnnounceMessage: false),
            ]);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyMelterOptPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet.Type, parsed.Type);
        Assert.Equal(packet.EntityId, parsed.EntityId);
        Assert.Equal(packet.EntityName, parsed.EntityName);
        Assert.Equal(packet.Items, parsed.Items);
    }

    [Fact]
    public void LegacyMelterOptParsesSyntheticPayloadWithUnknownFields()
    {
        // Required fields plus invented unknown fields in both the outer and nested messages.
        byte[] payload = [0x08, 0, 0x22, 8, 0x08, 8, 0x10, 77, 0x18, 1, 0x28, 88, 0x48, 99];
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyMelterOpt, payload.Length);
        payload.CopyTo(writer.Payload);

        Assert.True(LegacyMelterOptPacket.TryParse(ReadFrame(writer.ToArray()), out var parsed));
        Assert.Equal(0U, parsed.Type);
        Assert.Equal(0U, parsed.EntityId);
        Assert.Equal(string.Empty, parsed.EntityName);
        var item = Assert.Single(parsed.Items);
        Assert.Equal(8U, item.Type);
        Assert.Equal(77U, item.Id);
        Assert.True(item.Bound);
        Assert.False(item.AnnounceMessage);
    }

    [Fact]
    public async Task HandlerRepliesToMelterRankListWithEmptyList()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(CreateConnection(sent), log, CreateSession()),
            ReadFrame(LegacyMelterRankListPacket.Query(page: 4).ToFrameBytes()));

        var responseBytes = Assert.Single(sent);
        Assert.True(LegacyMelterRankListPacket.TryParse(ReadFrame(responseBytes), out var response));
        Assert.Equal(4U, response.Page);
        Assert.Equal(0U, response.CountInPage);
        Assert.Equal(0U, response.Count);
        Assert.Empty(response.Entries);
        Assert.Contains("WorldServer handled LegacyMelterRankList", log.ToString());
    }

    [Fact]
    public async Task HandlerConsumesMelterItemAndSendsPrizeFrames()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var connection = CreateConnection(sent);
        var session = CreateSession(new PlayerItemSnapshot(
            InventoryItems:
            [
                new PlayerItemSnapshotItem
                {
                    Uid = 7_000_001,
                    ItemId = 721_450,
                    Durability = 1,
                    MaximumDurability = 1,
                    StackSize = 1,
                },
            ],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems: [],
            RuneItems: []));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var request = new LegacyMelterOptPacket(
            Type: 0,
            EntityId: 0,
            EntityName: string.Empty,
            Items: [new LegacyMelterOptItem(Type: 1, Id: 721_450, Bound: true, AnnounceMessage: false)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: CreateMelterCatalog(),
                Sessions: sessions),
            ReadFrame(request.ToFrameBytes()));

        Assert.Equal(3, sent.Count);
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(sent[0]), out var remove));
        Assert.Equal(7_000_001U, remove.Id);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, remove.Action);

        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[1]), out var prizeItem));
        Assert.Equal(730_001U, prizeItem.ItemId);
        Assert.Equal(1, prizeItem.Bound);
        Assert.Equal(1, prizeItem.StackSize);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, prizeItem.Mode);

        Assert.True(LegacyMelterOptPacket.TryParse(ReadFrame(sent[2]), out var response));
        Assert.Equal(0U, response.Type);
        Assert.Equal(41_001U, response.EntityId);
        Assert.Equal("FixtureMelter", response.EntityName);
        var responseItem = Assert.Single(response.Items);
        Assert.Equal(8U, responseItem.Type);
        Assert.Equal(730_001U, responseItem.Id);
        Assert.True(responseItem.Bound);
        Assert.False(responseItem.AnnounceMessage);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.DoesNotContain(updatedSession!.PlayerItems.InventoryItems, item => item.ItemId == 721_450);
        Assert.Contains(updatedSession.PlayerItems.InventoryItems, item => item.ItemId == 730_001);
        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyMelterOpt", output);
        Assert.Contains("firstItemId=721450", output);
        Assert.Contains("prizeItemId=730001", output);
        Assert.DoesNotContain("WorldServer deferred LegacyMelterOpt", output);
        Assert.DoesNotContain("melting inventory mutation is not modeled", output);
    }

    [Fact]
    public async Task HandlerSkipsMelterOptWhenInventoryItemIsMissing()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var connection = CreateConnection(sent);
        var session = CreateSession(PlayerItemSnapshot.Empty);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var request = new LegacyMelterOptPacket(
            Type: 0,
            EntityId: 0,
            EntityName: string.Empty,
            Items: [new LegacyMelterOptItem(Type: 1, Id: 721_450, Bound: true, AnnounceMessage: false)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: CreateMelterCatalog(),
                Sessions: sessions),
            ReadFrame(request.ToFrameBytes()));

        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.Empty(updatedSession!.PlayerItems.InventoryItems);
        var output = log.ToString();
        Assert.Contains("WorldServer skipped LegacyMelterOpt", output);
        Assert.Contains("inventory item was not found", output);
        Assert.DoesNotContain("WorldServer deferred LegacyMelterOpt", output);
    }

    [Fact]
    public async Task HandlerAcceptsSyntheticMelterOptPayloadWithUnknownFields()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        // Required fields plus invented unknown fields in both the outer and nested messages.
        byte[] payload = [0x08, 0, 0x22, 8, 0x08, 8, 0x10, 77, 0x18, 1, 0x28, 88, 0x48, 99];
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyMelterOpt, payload.Length);
        payload.CopyTo(writer.Payload);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                CreateConnection(sent),
                log,
                CreateSession(),
                WorldData: CreateMelterCatalog()),
            ReadFrame(writer.ToArray()));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains("WorldServer skipped LegacyMelterOpt", output);
        Assert.Contains("firstItemId=77", output);
        Assert.Contains("melting item rule was not loaded", output);
        Assert.DoesNotContain("WorldServer rejected malformed LegacyMelterOpt", output);
    }

    private static WorldSession CreateSession(PlayerItemSnapshot? playerItems = null)
    {
        return new WorldSession(
            AccountId: 41_001,
            Username: "fixture-melter",
            new CharacterSnapshot(
                Uid: 41_001,
                Name: "FixtureMelter",
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
                X: 830,
                Y: 584,
                PreviousMapId: 1000,
                PreviousMapX: 500,
                PreviousMapY: 650,
                Strength: 0,
                Agility: 36,
                Vitality: 447,
                Spirit: 0,
                AttributePoints: 12,
                MinHitPoints: 61_258,
                MinMana: 5_000,
                Money: 98_602_439_956,
                ConquerPoints: 639_778_904,
                BoundConquerPoints: 456,
                BankConquerPoints: 123,
                PkPoints: 0,
                Online: 7,
                SourceFile: "41001.ini",
                Attributes: new Dictionary<string, string>(),
                PaidNobility: new Dictionary<string, string>()),
            playerItems ?? PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty);
    }

    private static WorldDataCatalog CreateMelterCatalog()
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-05T00:00:00Z"),
                "fixture://legacy-melter",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: [CreateItemType(730_001)],
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
            meltingTypeRules:
            [
                new MeltingTypeRule(
                    RuleType: 1,
                    ItemTypeId: 721_450,
                    Parameters: [0, 0, 9],
                    SourceFile: "MeltingType.ini",
                    LineNumber: 1,
                    RawLine: "1 721450 0 0 9",
                    Fields: ["1", "721450", "0", "0", "9"]),
                new MeltingTypeRule(
                    RuleType: 2,
                    ItemTypeId: 9,
                    Parameters: [730_001],
                    SourceFile: "MeltingType.ini",
                    LineNumber: 2,
                    RawLine: "2 9 730001",
                    Fields: ["2", "9", "730001"]),
            ],
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

    private static ItemType CreateItemType(int itemTypeId)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemTypeId.ToString();
        fields[1] = "MelterPrize";
        fields[ItemType.DurabilityFieldIndex] = "1";
        return new ItemType(itemTypeId, "MelterPrize", fields);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData($"fixture://{name}", [], [], [], info: null, pole: null, conductors: []);
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

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
