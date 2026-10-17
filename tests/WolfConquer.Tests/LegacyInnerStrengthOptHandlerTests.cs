using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyInnerStrengthOptHandlerTests
{
    [Fact]
    public async Task HandlerSendsOpenInnerGuiFromInnerPowerRowsWithoutDeferredLog()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var worldData = CreateInnerPowerCatalog([CreateInnerPowerRow()]);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData,
            log);

        await handler.HandleFrameAsync(
            CreateConnection(sent),
            ReadFrame(CreateInnerStrengthOptFrame(LegacyInnerPowerAction.OpenInner, 1_293_600, 0)));

        var bytes = Assert.Single(sent);
        Assert.True(LegacyInnerPowerGuiPacket.TryParse(ReadFrame(bytes), out var gui));
        Assert.Equal(
            [
                new LegacyInnerPowerGuiEntry(11, 100),
                new LegacyInnerPowerGuiEntry(21, 80),
            ],
            gui.Gongs);

        var output = log.ToString();
        Assert.Contains(
            "WorldServer handled LegacyInnerStrengthOpt session=none action=OpenInner parameter=1293600 parameter2=0 gongs=2; sourceName=MsgInnerPower.OpenInner.",
            output);
        Assert.DoesNotContain("WorldServer deferred LegacyInnerStrengthOpt", output);
        Assert.DoesNotContain("WorldServer received opcode 2115", output);
    }

    [Fact]
    public async Task HandlerDefersOpenInnerWhenSourceRowIsMissing()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            CreateInnerPowerCatalog([]),
            log);

        await handler.HandleFrameAsync(
            CreateConnection(sent),
            ReadFrame(CreateInnerStrengthOptFrame(LegacyInnerPowerAction.OpenInner, 1_293_600, 0)));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains(
            "WorldServer deferred LegacyInnerStrengthOpt session=none action=OpenInner parameter=1293600 parameter2=0; InnerPower.txt row for character 1293600 is not loaded.",
            output);
        Assert.DoesNotContain("WorldServer received opcode 2115", output);
    }

    [Fact]
    public async Task HandlerSendsInfoStageAndScoreFromInnerPowerRowsWithoutDeferredLog()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var worldData = CreateInnerPowerCatalog(
            [CreateInnerPowerRow()],
            [CreateNeiGongSecret(1, neiGongCount: 2)],
            [
                CreateNeiGongType(secretId: 1, slot: 1, typeId: 11, maxLevel: 5, attributeTypes: [1, 10], attributeValues: [300, 20]),
                CreateNeiGongType(secretId: 1, slot: 2, typeId: 12, maxLevel: 5, attributeTypes: [2], attributeValues: [250]),
            ]);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData,
            log);

        await handler.HandleFrameAsync(
            CreateConnection(sent),
            ReadFrame(CreateInnerStrengthOptFrame(LegacyInnerPowerAction.InfoStage, 1_293_600, 1)));

        Assert.Equal(2, sent.Count);
        var updateStageFrame = ReadFrame(sent[0]);
        var updateScoreFrame = ReadFrame(sent[1]);
        Assert.True(LegacyInnerPowerStageInfoPacket.TryParse(updateStageFrame, out var updateStage));
        Assert.True(LegacyInnerPowerStageInfoPacket.TryParse(updateScoreFrame, out var updateScore));
        Assert.Equal(LegacyInnerPowerStageInfoAction.UpdateStage, updateStage.Action);
        Assert.Equal(LegacyInnerPowerStageInfoAction.UpdateScore, updateScore.Action);
        Assert.Equal(1_293_600U, updateStage.CharacterId);
        Assert.Equal(150U, updateStage.Score);
        Assert.Equal([new LegacyInnerPowerStageGongInfo(11, 3, 100, Complete: true)], updateStage.Gongs);
        Assert.Equal(
            [
                new LegacyInnerPowerStageAttributeInfo(11, 1, 180),
                new LegacyInnerPowerStageAttributeInfo(11, 10, 12),
            ],
            updateStage.Attributes);
        Assert.Equal(updateStage.CharacterId, updateScore.CharacterId);
        Assert.Equal(updateStage.Score, updateScore.Score);
        Assert.Equal(updateStage.Gongs, updateScore.Gongs);
        Assert.Equal(updateStage.Attributes, updateScore.Attributes);

        var output = log.ToString();
        Assert.Contains(
            "WorldServer handled LegacyInnerStrengthOpt session=none action=InfoStage parameter=1293600 parameter2=1 stageInfoPackets=2; sourceName=MsgInnerPower.InfoStage.",
            output);
        Assert.DoesNotContain("WorldServer deferred LegacyInnerStrengthOpt", output);
        Assert.DoesNotContain("WorldServer received opcode 2115", output);
    }

    [Fact]
    public async Task HandlerUpdatesOneGongFromCurrentSessionAndEmitsSourcePacketSequence()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var worldData = CreateInnerPowerCatalog(
            [CreateUpgradeableInnerPowerRow(potency: 450, level: 1, score: 20, complete: false)],
            [CreateNeiGongSecret(1, neiGongCount: 1)],
            [
                CreateNeiGongType(
                    secretId: 1,
                    slot: 1,
                    typeId: 11,
                    maxLevel: 5,
                    attributeTypes: [1],
                    attributeValues: [500],
                    cultureValues: [100, 125, 150, 175, 200]),
            ]);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData,
            log);
        var connection = CreateConnection(sent);
        AttachSession(handler, connection);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateInnerStrengthOptFrame(LegacyInnerPowerAction.UpdateGong, 11, 0)));

        Assert.Equal(4, sent.Count);
        AssertPotencyUpdate(sent[0], 325);

        Assert.True(LegacyInnerPowerGuiPacket.TryParse(ReadFrame(sent[1]), out var gui));
        Assert.Equal([new LegacyInnerPowerGuiEntry(11, 40)], gui.Gongs);

        Assert.True(LegacyInnerPowerStageInfoPacket.TryParse(ReadFrame(sent[2]), out var updateStage));
        Assert.True(LegacyInnerPowerStageInfoPacket.TryParse(ReadFrame(sent[3]), out var updateScore));
        Assert.Equal(LegacyInnerPowerStageInfoAction.UpdateStage, updateStage.Action);
        Assert.Equal(LegacyInnerPowerStageInfoAction.UpdateScore, updateScore.Action);
        Assert.Equal(40U, updateStage.Score);
        Assert.Equal([new LegacyInnerPowerStageGongInfo(11, 2, 40, Complete: false)], updateStage.Gongs);
        Assert.Equal([new LegacyInnerPowerStageAttributeInfo(11, 1, 200)], updateStage.Attributes);
        Assert.Equal(updateStage.Gongs, updateScore.Gongs);
        Assert.Equal(updateStage.Attributes, updateScore.Attributes);

        var fields = worldData.InnerPowerRanksByCharacterId[1_293_600][0].Fields;
        Assert.Equal("325", fields[2]);
        Assert.Equal("40", fields[8]);
        Assert.Equal("2", fields[10]);
        Assert.Equal("0", fields[11]);

        var output = log.ToString();
        Assert.Contains(
            "action=UpdateGong parameter=11 parameter2=0 upgrades=1 potency=325; sourceName=MsgInnerPower.UpdateGong.",
            output);
        Assert.DoesNotContain("WorldServer deferred LegacyInnerStrengthOpt", output);
        Assert.DoesNotContain("WorldServer received opcode 2115", output);
    }

    [Fact]
    public async Task HandlerUpdatesGongUntilMaxWithSourceBulkCostRule()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var worldData = CreateInnerPowerCatalog(
            [CreateUpgradeableInnerPowerRow(potency: 700, level: 1, score: 20, complete: false)],
            [CreateNeiGongSecret(1, neiGongCount: 1)],
            [
                CreateNeiGongType(
                    secretId: 1,
                    slot: 1,
                    typeId: 11,
                    maxLevel: 5,
                    attributeTypes: [1],
                    attributeValues: [500],
                    cultureValues: [100, 125, 150, 175, 200]),
            ]);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData,
            log);
        var connection = CreateConnection(sent);
        AttachSession(handler, connection);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateInnerStrengthOptFrame(LegacyInnerPowerAction.UpdateGong, 11, 1)));

        Assert.Equal(7, sent.Count);
        AssertPotencyUpdate(sent[0], 575);
        AssertPotencyUpdate(sent[1], 450);
        AssertPotencyUpdate(sent[2], 325);
        AssertPotencyUpdate(sent[3], 200);

        Assert.True(LegacyInnerPowerGuiPacket.TryParse(ReadFrame(sent[4]), out var gui));
        Assert.Equal([new LegacyInnerPowerGuiEntry(11, 100)], gui.Gongs);

        Assert.True(LegacyInnerPowerStageInfoPacket.TryParse(ReadFrame(sent[5]), out var updateStage));
        Assert.True(LegacyInnerPowerStageInfoPacket.TryParse(ReadFrame(sent[6]), out var updateScore));
        Assert.Equal(100U, updateStage.Score);
        Assert.Equal([new LegacyInnerPowerStageGongInfo(11, 5, 100, Complete: true)], updateStage.Gongs);
        Assert.Equal([new LegacyInnerPowerStageAttributeInfo(11, 1, 500)], updateStage.Attributes);
        Assert.Equal(updateStage.Gongs, updateScore.Gongs);
        Assert.Equal(updateStage.Attributes, updateScore.Attributes);

        var fields = worldData.InnerPowerRanksByCharacterId[1_293_600][0].Fields;
        Assert.Equal("200", fields[2]);
        Assert.Equal("100", fields[8]);
        Assert.Equal("5", fields[10]);
        Assert.Equal("1", fields[11]);

        var output = log.ToString();
        Assert.Contains(
            "action=UpdateGong parameter=11 parameter2=1 upgrades=4 potency=200; sourceName=MsgInnerPower.UpdateGong.",
            output);
        Assert.DoesNotContain("WorldServer deferred LegacyInnerStrengthOpt", output);
        Assert.DoesNotContain("WorldServer received opcode 2115", output);
    }

    [Fact]
    public async Task HandlerTreatsInsufficientUpdateGongPotencyAsSourceNoOp()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var worldData = CreateInnerPowerCatalog(
            [CreateUpgradeableInnerPowerRow(potency: 50, level: 1, score: 20, complete: false)],
            [CreateNeiGongSecret(1, neiGongCount: 1)],
            [
                CreateNeiGongType(
                    secretId: 1,
                    slot: 1,
                    typeId: 11,
                    maxLevel: 5,
                    attributeTypes: [1],
                    attributeValues: [500],
                    cultureValues: [100, 125, 150, 175, 200]),
            ]);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData,
            log);
        var connection = CreateConnection(sent);
        AttachSession(handler, connection);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateInnerStrengthOptFrame(LegacyInnerPowerAction.UpdateGong, 11, 0)));

        Assert.Empty(sent);
        var fields = worldData.InnerPowerRanksByCharacterId[1_293_600][0].Fields;
        Assert.Equal("50", fields[2]);
        Assert.Equal("20", fields[8]);
        Assert.Equal("1", fields[10]);
        Assert.Equal("0", fields[11]);

        var output = log.ToString();
        Assert.Contains(
            "action=UpdateGong parameter=11 parameter2=0 upgrades=0 potency=50; sourceName=MsgInnerPower.UpdateGong.",
            output);
        Assert.DoesNotContain("WorldServer deferred LegacyInnerStrengthOpt", output);
        Assert.DoesNotContain("WorldServer received opcode 2115", output);
    }

    [Fact]
    public async Task HandlerDefersUpdateGongWhenSessionIsMissing()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);

        await handler.HandleFrameAsync(
            CreateConnection(sent),
            ReadFrame(CreateInnerStrengthOptFrame(LegacyInnerPowerAction.UpdateGong, 11, 0)));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains(
            "WorldServer deferred LegacyInnerStrengthOpt session=none action=UpdateGong parameter=11 parameter2=0; active world session is not available.",
            output);
        Assert.DoesNotContain("WorldServer received opcode 2115", output);
    }

    private static byte[] CreateInnerStrengthOptFrame(
        LegacyInnerPowerAction action,
        uint parameter,
        uint parameter2)
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyInnerStrengthOpt, LegacyInnerPowerOptPacket.PayloadLength);
        writer.Payload[0] = (byte)action;
        writer.WriteUInt32(1, parameter);
        writer.WriteUInt32(5, parameter2);
        return writer.ToArray();
    }

    private static InnerPowerRankEntry CreateInnerPowerRow()
    {
        string[] fields =
        [
            "Inner",
            "1293600",
            "1234",
            "2",
            "1",
            "1",
            "2",
            "11",
            "100",
            "1",
            "3",
            "1",
            "12",
            "50",
            "0",
            "2",
            "0",
            "2",
            "1",
            "1",
            "21",
            "80",
            "1",
            "4",
            "0",
            "",
        ];

        return new InnerPowerRankEntry(
            CharacterName: fields[0],
            CharacterId: 1_293_600,
            RankingFields: fields.Skip(2).Take(fields.Length - 3).ToArray(),
            SourceFile: "InnerPower.txt",
            LineNumber: 2,
            RawLine: string.Join('/', fields),
            Fields: fields);
    }

    private static InnerPowerRankEntry CreateUpgradeableInnerPowerRow(
        int potency,
        byte level,
        byte score,
        bool complete)
    {
        string[] fields =
        [
            "Inner",
            "1293600",
            potency.ToString(CultureInfo.InvariantCulture),
            "1",
            "1",
            "1",
            "1",
            "11",
            score.ToString(CultureInfo.InvariantCulture),
            "1",
            level.ToString(CultureInfo.InvariantCulture),
            complete ? "1" : "0",
            "",
        ];

        return new InnerPowerRankEntry(
            CharacterName: fields[0],
            CharacterId: 1_293_600,
            RankingFields: fields.Skip(2).Take(fields.Length - 3).ToArray(),
            SourceFile: "InnerPower.txt",
            LineNumber: 2,
            RawLine: string.Join('/', fields),
            Fields: fields);
    }

    private static NeiGongSecret CreateNeiGongSecret(int id, int neiGongCount)
    {
        return new NeiGongSecret(
            id,
            $"Stage{id}",
            $"NeiGong_Stage{id}",
            neiGongCount,
            AttributeTypes: [],
            AttributeValues: [],
            GetWayContent: "",
            SourceFile: "NeiGongInfo.ini",
            LineNumber: id + 1,
            RawSectionName: id.ToString(),
            Fields: new Dictionary<string, string>());
    }

    private static NeiGongType CreateNeiGongType(
        int secretId,
        int slot,
        int typeId,
        int maxLevel,
        IReadOnlyList<int> attributeTypes,
        IReadOnlyList<int> attributeValues,
        IReadOnlyList<int>? cultureValues = null)
    {
        return new NeiGongType(
            secretId,
            slot,
            typeId,
            $"Gong{typeId}",
            $"NeiGong_Gong{typeId}",
            maxLevel,
            attributeTypes,
            attributeValues,
            CultureValues: cultureValues ?? Enumerable.Repeat(100, maxLevel).ToArray(),
            Condition: "",
            RequiredLevel: 0,
            RequiredNeiGongValue: 0,
            RequiredPrerequisiteNeiGong: 0,
            RequiredItemTypeId: 0,
            SourceFile: "NeiGongInfo.ini",
            LineNumber: 10 + slot,
            RawSectionName: $"{secretId}-{slot}",
            Fields: new Dictionary<string, string>());
    }

    private static WorldDataCatalog CreateInnerPowerCatalog(
        IReadOnlyList<InnerPowerRankEntry> innerPowerRanks,
        IReadOnlyList<NeiGongSecret>? neiGongSecrets = null,
        IReadOnlyList<NeiGongType>? neiGongTypes = null)
    {
        var resolvedNeiGongSecrets = neiGongSecrets ?? Array.Empty<NeiGongSecret>();
        var resolvedNeiGongTypes = neiGongTypes ?? Array.Empty<NeiGongType>();

        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-05T00:00:00Z"),
                "fixture://legacy-inner-strength",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: [],
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
            innerPowerRanks: innerPowerRanks,
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
            neiGongHeader: new NeiGongHeader(resolvedNeiGongSecrets.Count, [], "fixture://neigong", 0, "", new Dictionary<string, string>()),
            neiGongSecrets: resolvedNeiGongSecrets,
            neiGongTypes: resolvedNeiGongTypes,
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
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
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

    private static void AttachSession(WorldServerFrameHandler handler, ConquerTcpConnection connection)
    {
        var field = typeof(WorldServerFrameHandler).GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var sessions = Assert.IsType<WorldSessionStore>(field.GetValue(handler));
        sessions.Set(connection, CreateSession());
    }

    private static WorldSession CreateSession()
    {
        return new WorldSession(
            AccountId: 1_293_600,
            Username: "Inner",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "Inner",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 10,
                FirstClass: 10,
                SecondClass: 10,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: 1002,
                X: 300,
                Y: 278,
                PreviousMapId: 1002,
                PreviousMapX: 300,
                PreviousMapY: 278,
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
                SourceFile: "fixture://character",
                Attributes: new Dictionary<string, string>(),
                PaidNobility: new Dictionary<string, string>()),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            CombatGear: CombatGearSnapshot.Empty);
    }

    private static void AssertPotencyUpdate(byte[] bytes, ulong potency)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update));
        Assert.Equal(1_293_600U, update.Uid);
        var entry = Assert.Single(update.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.InnerPowerPotency, entry.Type);
        Assert.Equal(potency, entry.Value);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
