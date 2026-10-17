using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacySubClassPacketTests
{
    [Fact]
    public void LegacySubClassOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2033, WorldPacketOpcodes.LegacySubClass);
        Assert.Equal(WorldPacketOpcodes.LegacySubClass, LegacySubClassPacket.Opcode);
    }

    [Fact]
    public void LegacySubClassOpenPacketRoundTrips()
    {
        var packet = new LegacySubClassPacket(LegacySubClassAction.Open, 0);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacySubClassPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacySubClassSwitchSubClassPacketRoundTripsWithNonzeroSubClassId()
    {
        var packet = new LegacySubClassPacket(LegacySubClassAction.SwitchSubClass, 9);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacySubClassPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacySubClassPacketWritesFixedRequestShape()
    {
        var bytes = new LegacySubClassPacket(LegacySubClassAction.Pro, -1).ToFrameBytes();

        Assert.Equal(LegacySubClassPacket.FrameLength, bytes.Length);
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        Assert.Equal(LegacySubClassPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(LegacySubClassPacket.PayloadLength, frame.Payload.Length);
        Assert.True(PacketReader.TryRead(bytes, out var reader));
        Assert.Equal((ushort)LegacySubClassAction.Pro, reader.ReadUInt16(0));
        Assert.Equal(unchecked((byte)-1), reader.Payload[2]);
    }

    [Fact]
    public void LegacySubClassPacketRejectsMalformedShortPayload()
    {
        var writer = new PacketWriter(LegacySubClassPacket.Opcode, LegacySubClassPacket.PayloadLength - 1);
        writer.WriteUInt16(0, (ushort)LegacySubClassAction.Open);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacySubClassPacket.TryParse(frame, out _));
    }

    [Fact]
    public void LegacySubClassPacketRejectsWrongOpcode()
    {
        var writer = new PacketWriter(9999, LegacySubClassPacket.PayloadLength);
        writer.WriteUInt16(0, (ushort)LegacySubClassAction.Open);
        writer.Payload[2] = 0;
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacySubClassPacket.TryParse(frame, out _));
    }

    [Fact]
    public void LegacySubClassPacketIgnoresTrailingBytesLikeSourceReader()
    {
        var writer = new PacketWriter(LegacySubClassPacket.Opcode, LegacySubClassPacket.PayloadLength + 2);
        writer.WriteUInt16(0, (ushort)LegacySubClassAction.Join);
        writer.Payload[2] = 4;
        writer.Payload[3] = 0xAA;
        writer.Payload[4] = 0x55;
        var frame = ReadFrame(writer.ToArray());

        Assert.True(LegacySubClassPacket.TryParse(frame, out var parsed));
        Assert.Equal(new LegacySubClassPacket(LegacySubClassAction.Join, 4), parsed);
    }

    [Fact]
    public void LegacySubClassGuiPacketWritesSourceEmptyShowGuiShape()
    {
        var packet = new LegacySubClassGuiPacket(
            LegacySubClassAction.ShowGUI,
            StudyPoints: 41_000,
            StudyReceive: 0,
            Entries: []);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacySubClass, frame.Header.Opcode);
        Assert.Equal(22, frame.Payload.Length);
        Assert.True(LegacySubClassGuiPacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacySubClassAction.ShowGUI, parsed.Action);
        Assert.Equal(41_000, parsed.StudyPoints);
        Assert.Equal(0, parsed.StudyReceive);
        Assert.Empty(parsed.Entries);
    }

    [Fact]
    public void LegacySubClassUpdatePacketWritesSourceActivateSubClassShape()
    {
        var packet = new LegacySubClassUpdatePacket(
            LegacySubClassAction.ActivateSubClass,
            SubClassId: 9,
            Level: 7,
            StudyReceive: 0);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacySubClass, frame.Header.Opcode);
        Assert.Equal(22, frame.Payload.Length);
        Assert.True(LegacySubClassUpdatePacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Theory]
    [InlineData(LegacySubClassAction.LearnSubClass, 9, 9)]
    [InlineData(LegacySubClassAction.MartialPromoted, 9, 7)]
    [InlineData(LegacySubClassAction.SendUpdate, 9, 8)]
    public void LegacySubClassUpdatePacketWritesSourceMutationShapes(
        LegacySubClassAction action,
        byte subClassId,
        byte levelOrPhrase)
    {
        var packet = new LegacySubClassUpdatePacket(
            action,
            subClassId,
            levelOrPhrase,
            StudyReceive: 0);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(22, frame.Payload.Length);
        Assert.True(LegacySubClassUpdatePacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacySubClassGuiPacketRoundTripsEntriesInSourceOrder()
    {
        var packet = new LegacySubClassGuiPacket(
            LegacySubClassAction.ShowGUI,
            StudyPoints: 6_700,
            StudyReceive: 0,
            Entries:
            [
                new LegacySubClassGuiEntry(Id: 1, Phrase: 8, Level: 9),
                new LegacySubClassGuiEntry(Id: 9, Phrase: 6, Level: 7),
            ]);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(28, frame.Payload.Length);
        Assert.True(LegacySubClassGuiPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet.Action, parsed.Action);
        Assert.Equal(packet.StudyPoints, parsed.StudyPoints);
        Assert.Equal(packet.StudyReceive, parsed.StudyReceive);
        Assert.Equal(packet.Entries, parsed.Entries);
    }

    [Fact]
    public async Task HandlerSendsSubClassGuiForOpenRequest()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var session = CreateSession(new Dictionary<string, string>
        {
            ["SubProfInfo"] = "41000#0#2#1#9#8#9#7#6#",
        });
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, session),
            ReadFrame(new LegacySubClassPacket(LegacySubClassAction.Open, SubClassId: 0).ToFrameBytes()));

        var responseBytes = Assert.Single(sent);
        Assert.True(LegacySubClassGuiPacket.TryParse(ReadFrame(responseBytes), out var response));
        Assert.Equal(LegacySubClassAction.ShowGUI, response.Action);
        Assert.Equal(41_000, response.StudyPoints);
        Assert.Equal(
            [
                new LegacySubClassGuiEntry(Id: 1, Phrase: 8, Level: 9),
                new LegacySubClassGuiEntry(Id: 9, Phrase: 6, Level: 7),
            ],
            response.Entries);
        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacySubClass session=accountId=1293600 username=HeroOne characterUid=1293600 characterName=HeroOne", output);
        Assert.Contains("action=Open subclassId=0 entries=2 studyPoints=41000.", output);
    }

    [Fact]
    public async Task HandlerSwitchesLearnedSubClassAndSendsActivateUpdate()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(new Dictionary<string, string>
        {
            ["SubProfInfo"] = "41000#0#2#1#9#8#9#7#6#",
        });
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, session, Sessions: sessions),
            ReadFrame(new LegacySubClassPacket(LegacySubClassAction.SwitchSubClass, SubClassId: 9).ToFrameBytes()));

        var responseBytes = Assert.Single(sent);
        Assert.True(LegacySubClassUpdatePacket.TryParse(ReadFrame(responseBytes), out var response));
        Assert.Equal(LegacySubClassAction.ActivateSubClass, response.Action);
        Assert.Equal(9, response.SubClassId);
        Assert.Equal(7, response.Level);
        Assert.Equal(0, response.StudyReceive);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal("41000#9#2#1#9#8#9#7#6#", updated.Character.Attributes["SubProfInfo"]);
        Assert.Equal("9", updated.Character.Attributes["ActiveSublass"]);
        Assert.Equal("600000008", updated.Character.Attributes["SubClassHashPoint"]);
        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacySubClass", output);
        Assert.Contains("action=SwitchSubClass subclassId=9 level=7", output);
        Assert.DoesNotContain("WorldServer deferred LegacySubClass", output);
        Assert.DoesNotContain("subclass mutation is not modeled", output);
    }

    [Fact]
    public async Task HandlerPromotesLearnedSubClassPhraseAndSendsMartialPromotedUpdate()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(new Dictionary<string, string>
        {
            ["SubProfInfo"] = "41000#0#2#1#9#8#9#7#6#",
            ["SubClassHashPoint"] = "600000008",
        });
        sessions.Set(connection, session);
        var worldData = CreateSubProfessionCatalog([SubProfession(id: 97, requiredLevel: 120, requiredMetempsychosis: 2)]);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, session, WorldData: worldData, Sessions: sessions),
            ReadFrame(new LegacySubClassPacket(LegacySubClassAction.Pro, SubClassId: 9).ToFrameBytes()));

        Assert.Equal(2, sent.Count);
        var responseBytes = sent[0];
        Assert.True(LegacySubClassUpdatePacket.TryParse(ReadFrame(responseBytes), out var response));
        Assert.Equal(LegacySubClassAction.MartialPromoted, response.Action);
        Assert.Equal(9, response.SubClassId);
        Assert.Equal(7, response.Level);
        Assert.Equal(0, response.StudyReceive);
        Assert.True(LegacyShowEquipmentPacket.TryParse(ReadFrame(sent[1]), out var equipmentRefresh));
        Assert.Equal(LegacyShowEquipmentPacket.ShowAction, equipmentRefresh.Action);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal("41000#0#2#1#9#8#9#7#7#", updated.Character.Attributes["SubProfInfo"]);
        Assert.Equal("700000008", updated.Character.Attributes["SubClassHashPoint"]);
        Assert.False(updated.Character.Attributes.ContainsKey("ActiveSublass"));
        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacySubClass", output);
        Assert.Contains("action=Pro subclassId=9 phrase=7", output);
        Assert.DoesNotContain("WorldServer deferred LegacySubClass", output);
        Assert.DoesNotContain("subclass mutation is not modeled", output);
    }

    [Fact]
    public async Task HandlerUpgradesLearnedSubClassLevelRemovesStudyAndSendsSourceUpdates()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(new Dictionary<string, string>
        {
            ["SubProfInfo"] = "41000#0#2#1#9#8#9#7#6#",
            ["SubClassHashPoint"] = "600000008",
        });
        sessions.Set(connection, session);
        var worldData = CreateSubProfessionCatalog(
            subProfessions: [],
            levelExperience: [LevelExperienceRow(type: 9, level: 7, experience: 800)]);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, session, WorldData: worldData, Sessions: sessions),
            ReadFrame(new LegacySubClassPacket(LegacySubClassAction.Upgrade, SubClassId: 9).ToFrameBytes()));

        Assert.Equal(3, sent.Count);

        Assert.True(LegacyStringPacket.TryParse(ReadFrame(sent[0]), out var effect));
        Assert.Equal(1_293_600U, effect.Uid);
        Assert.Equal(LegacyStringId.Effect, effect.Id);
        Assert.Equal(["zf2-e300"], effect.Strings);

        Assert.True(LegacySubClassGuiPacket.TryParse(ReadFrame(sent[1]), out var studyAnimation));
        Assert.Equal(LegacySubClassAction.Animation, studyAnimation.Action);
        Assert.Equal(40_200, studyAnimation.StudyPoints);
        Assert.Equal(0, studyAnimation.StudyReceive);
        Assert.Empty(studyAnimation.Entries);

        Assert.True(LegacySubClassUpdatePacket.TryParse(ReadFrame(sent[2]), out var response));
        Assert.Equal(LegacySubClassAction.SendUpdate, response.Action);
        Assert.Equal(9, response.SubClassId);
        Assert.Equal(0, response.Level);
        Assert.Equal(0, response.StudyReceive);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal("40200#0#2#1#9#8#9#8#6#", updated.Character.Attributes["SubProfInfo"]);
        Assert.Equal("600000008", updated.Character.Attributes["SubClassHashPoint"]);
        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacySubClass", output);
        Assert.Contains("action=Upgrade subclassId=9 newLevel=8 cost=800 studyPoints=40200", output);
        Assert.DoesNotContain("WorldServer deferred LegacySubClass", output);
        Assert.DoesNotContain("subclass mutation is not modeled", output);
    }

    [Fact]
    public async Task HandlerSkipsUpgradeWhenStudyPointsAreBelowSourceCost()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(new Dictionary<string, string>
        {
            ["SubProfInfo"] = "41000#0#1#9#7#6#",
        });
        sessions.Set(connection, session);
        var worldData = CreateSubProfessionCatalog(
            subProfessions: [],
            levelExperience: [LevelExperienceRow(type: 9, level: 7, experience: 50_000)]);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, session, WorldData: worldData, Sessions: sessions),
            ReadFrame(new LegacySubClassPacket(LegacySubClassAction.Upgrade, SubClassId: 9).ToFrameBytes()));

        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var stored));
        Assert.NotNull(stored);
        Assert.Equal("41000#0#1#9#7#6#", stored.Character.Attributes["SubProfInfo"]);
        var output = log.ToString();
        Assert.Contains("WorldServer skipped LegacySubClass", output);
        Assert.Contains("action=Upgrade subclassId=9", output);
        Assert.Contains("study points 41000 are below required cost 50000", output);
        Assert.DoesNotContain("WorldServer handled LegacySubClass", output);
    }

    private static WorldSession CreateSession(IReadOnlyDictionary<string, string> attributes)
    {
        return new WorldSession(
            AccountId: 1_293_600,
            Username: "HeroOne",
            CreateSnapshot(attributes),
            PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty);
    }

    private static CharacterSnapshot CreateSnapshot(IReadOnlyDictionary<string, string> attributes)
    {
        return new CharacterSnapshot(
            Uid: 1_293_600,
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
            SourceFile: "1293600.ini",
            Attributes: attributes,
            PaidNobility: new Dictionary<string, string>());
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

    private static WorldDataCatalog CreateSubProfessionCatalog(
        IReadOnlyList<SubProfessionEntry> subProfessions,
        IReadOnlyList<LevelExperience>? levelExperience = null)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-05T00:00:00Z"),
                "fixture://legacy-subclass",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: [],
            itemAddBonuses: [],
            levelExperience: levelExperience ?? [],
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
            subProfessions: subProfessions,
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

    private static SubProfessionEntry SubProfession(
        int id,
        int requiredLevel,
        int requiredMetempsychosis)
    {
        return new SubProfessionEntry(
            Id: id,
            Title: "Wrangler",
            Intro: "Max HP +800",
            RequiredLevel: requiredLevel,
            RequiredMetempsychosis: requiredMetempsychosis,
            RequiredProfessions: [],
            Promotion: "",
            Suit: 0,
            LevelLimit: 9,
            RequiredJobLevel: null,
            EffectProportion: null,
            UpLevelEffect: null,
            UpStepEffect: null,
            UpStepScreenEffect: null,
            UpEffect: null,
            SubSpecial: null,
            SuitPower: null,
            BigClass: null,
            AccessionRoute: null,
            StepUpRoute: null,
            AnimationSection: null,
            SourceFile: "SubProfessionInfo.ini",
            LineNumber: id,
            RawSectionName: id.ToString(),
            Entries: []);
    }

    private static LevelExperience LevelExperienceRow(int type, int level, long experience)
    {
        return new LevelExperience(
            Type: type,
            Level: level,
            Experience: experience,
            UpLevTime: 0,
            MentorUpLevTime: 0,
            SourceLine: level,
            RawLine: $"{type} {level} {experience} 0 0",
            Fields:
            [
                type.ToString(CultureInfo.InvariantCulture),
                level.ToString(CultureInfo.InvariantCulture),
                experience.ToString(CultureInfo.InvariantCulture),
                "0",
                "0",
            ]);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
