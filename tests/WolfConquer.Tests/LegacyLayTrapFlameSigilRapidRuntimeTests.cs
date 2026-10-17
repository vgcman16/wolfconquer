using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;
using Xunit;

namespace WolfConquer.Tests;

public sealed class LegacyLayTrapFlameSigilRapidRuntimeTests
{
    private const uint PlayerUid = 1_293_600;
    private const ushort FlameOfDestructionSpellId = 16_540;
    private const int FlameSigilRapidGouyuType = 104;
    private const uint FlameSigilRapidLevel = 9;
    private const int FlameSigilRapidPowerValueIndex = 5;
    private const uint FlameSigilRapidPowerBonus = 80;
    private const uint FixtureStrength = 300;
    private const uint FixtureMonsterDefense = 1;
    private const int FixtureCasterLevel = 120;
    private const int FixtureMonsterLevel = 100;
    private const int FixtureNinjaClass = 5_051;
    private const uint FlameDisplayScaleNumerator = 200;
    private const uint FlameDisplayScaleDenominator = 100;

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    [Fact]
    public async Task RepeatMonsterPulseWithoutFlameSigilRapidKeepsBaseDamageAndDisplayScaling()
    {
        var result = await ExecuteFlameOfDestructionRepeatPulseAsync(
            equippedFlameSigilRapid: false,
            gouyuTypes: []);
        var expectedDamage = CalculateExpectedBaseFlameMonsterDamage();

        Assert.Equal(expectedDamage, result.MonsterHpDelta);
        Assert.Equal(MulDivToUInt32(expectedDamage, FlameDisplayScaleNumerator, FlameDisplayScaleDenominator), result.DisplayDamage);
        Assert.Contains("WorldServer handled legacy FlameofDestruction LayTrap repeat pulse runtime", result.Log);
    }

    [Fact]
    public async Task RepeatMonsterPulseWithFlameSigilRapidAddsCatalogAttackBeforeFlameDisplayScaling()
    {
        var result = await ExecuteFlameOfDestructionRepeatPulseAsync(
            equippedFlameSigilRapid: true,
            gouyuTypes: [CreateFlameSigilRapidGouyuRow()]);
        var expectedBoostedDamage = CalculateExpectedBaseFlameMonsterDamage() + FlameSigilRapidPowerBonus;

        Assert.Equal(expectedBoostedDamage, result.MonsterHpDelta);
        Assert.Equal(
            MulDivToUInt32(expectedBoostedDamage, FlameDisplayScaleNumerator, FlameDisplayScaleDenominator),
            result.DisplayDamage);
        Assert.Contains("WorldServer handled legacy FlameofDestruction LayTrap repeat pulse runtime", result.Log);
    }

    private static async Task<RepeatPulseResult> ExecuteFlameOfDestructionRepeatPulseAsync(
        bool equippedFlameSigilRapid,
        IReadOnlyList<GouyuType> gouyuTypes)
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 842, boundY: 590);
        var worldData = CreateWorldData(
            monsterFamily,
            [spawn],
            [CreateLayTrapMagicRow(FlameOfDestructionSpellId, level: 0, experience: 900_000)],
            gouyuTypes);
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var attributes = new Dictionary<string, string>
        {
            ["GuildID"] = "77",
            ["LegacyPhysicalLuckyStrikeRoll"] = "99",
            ["LegacyPhysicalRefineryRoll"] = "99",
            ["LegacyPhysicalDashRoll"] = "99",
        };
        if (equippedFlameSigilRapid)
        {
            attributes["MsgNinja"] = MsgNinjaWithEquippedItem(
                itemType: FlameSigilRapidGouyuType,
                level: FlameSigilRapidLevel,
                position: 1);
        }

        var session = CreateSession(
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    FlameOfDestructionSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 2),
            ],
            attributes: attributes);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            FloorItems: floorItems);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                opponentUid: 0,
                x: 840,
                y: 590,
                FlameOfDestructionSpellId)).ToFrameBytes()));

        var floorItem = Assert.Single(ParseFloorItems(sent));
        Assert.True(floorItems.TryGetVisibleEntry(floorItem.Uid, out var runtimeEntry));
        Assert.NotNull(runtimeEntry.Effect);

        var repeatAt = runtimeEntry.Effect!.DueAt.AddMilliseconds(
            -LegacyLayTrap.FlameOfDestructionInvokeMilliseconds + LegacyLayTrap.FlameOfDestructionRepeatMilliseconds);
        var repeatProcessedCount = await WorldPacketHandlers.SendDueLegacyLayTrapFloorSpellsAsync(
            context,
            repeatAt,
            CancellationToken.None);

        Assert.Equal(1, repeatProcessedCount);
        var repeatAnimation = Assert.Single(
            ParseSpellAnimations(sent),
            packet => packet.AttackerUid == floorItem.Uid &&
                packet.SpellId == FlameOfDestructionSpellId &&
                packet.BombFlag == 1);
        var repeatTarget = Assert.Single(repeatAnimation.Targets);
        Assert.Equal(monster.Uid, repeatTarget.Uid);
        Assert.True(repeatTarget.Hit);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));

        return new RepeatPulseResult(
            MonsterHpDelta: checked(monster.MaxLife - damagedMonster.Hitpoints),
            DisplayDamage: repeatTarget.Damage,
            Log: log.ToString());
    }

    private static uint CalculateExpectedBaseFlameMonsterDamage()
    {
        var damage = FixtureStrength;
        damage -= FixtureMonsterDefense;
        damage = Math.Max(damage, 1U + (FixtureCasterLevel / 10U));
        damage = checked(damage * 250 / 100);
        damage -= FixtureMonsterDefense;
        return damage;
    }

    private static GouyuType CreateFlameSigilRapidGouyuRow()
    {
        var values = Enumerable.Repeat(0, 10).ToArray();
        values[FlameSigilRapidPowerValueIndex] = checked((int)FlameSigilRapidPowerBonus);
        return new GouyuType(
            Id: 16_540_001,
            Type: FlameSigilRapidGouyuType,
            Level: checked((int)FlameSigilRapidLevel),
            Values: values,
            SourceFile: "fixture://FlameSigilRapid",
            LineNumber: 1,
            RawLine: string.Empty,
            Fields: []);
    }

    private static string MsgNinjaWithEquippedItem(uint itemType, uint level, uint position)
    {
        var fields = Enumerable.Repeat("0", 20)
            .Concat(
            [
                itemType.ToString(CultureInfo.InvariantCulture),
                level.ToString(CultureInfo.InvariantCulture),
                position.ToString(CultureInfo.InvariantCulture),
                "0",
                "0",
            ]);
        return string.Join('/', fields) + "/";
    }

    private static MsgAttackQuery CreateMagicQuery(
        uint opponentUid,
        ushort x,
        ushort y,
        ushort spellId)
    {
        return new MsgAttackQuery(
            PlayerUid,
            opponentUid,
            x,
            y,
            MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            MsgAttackEffect.None,
            Unknown: 0,
            SpellId: spellId);
    }

    private static WorldSession CreateSession(
        IReadOnlyList<PlayerSpellSnapshotEntry> spellEntries,
        IReadOnlyDictionary<string, string> attributes)
    {
        return new WorldSession(
            AccountId: 1,
            Username: "caster",
            new CharacterSnapshot(
                Uid: PlayerUid,
                Name: "Caster",
                Body: 1_003,
                Face: 0,
                Hair: 0,
                Class: FixtureNinjaClass,
                FirstClass: FixtureNinjaClass,
                SecondClass: 0,
                Reborn: 0,
                Level: FixtureCasterLevel,
                Experience: 0,
                MapId: 1_015,
                X: 830,
                Y: 584,
                PreviousMapId: 1_015,
                PreviousMapX: 830,
                PreviousMapY: 584,
                Strength: FixtureStrength,
                Agility: 0,
                Vitality: 0,
                Spirit: 300,
                AttributePoints: 0,
                MinHitPoints: 50_000,
                MinMana: 500,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 1,
                SourceFile: "fixture://laytrap-flamesigilrapid-character",
                Attributes: new Dictionary<string, string>(attributes),
                PaidNobility: new Dictionary<string, string>()),
            PlayerItemSnapshot.Empty,
            new PlayerSpellSnapshot(spellEntries),
            PlayerProficiencySnapshot.Empty);
    }

    private static Monster CreateMonsterFamily()
    {
        return new Monster(
            9_001,
            "TrainingMonster",
            new Dictionary<string, string>
            {
                ["life"] = "100000",
                ["lookface"] = "100",
                ["level"] = FixtureMonsterLevel.ToString(CultureInfo.InvariantCulture),
                ["defense"] = FixtureMonsterDefense.ToString(CultureInfo.InvariantCulture),
                ["defense2"] = "10000",
                ["attack_range"] = "0",
            },
            []);
    }

    private static MonsterSpawn CreateSingleInstanceMonsterSpawn(
        int generatorId = 1,
        int boundX = 842,
        int boundY = 590)
    {
        return new MonsterSpawn(
            GeneratorId: generatorId,
            MapId: 1_015,
            MonsterTypeId: 9_001,
            BoundX: boundX,
            BoundY: boundY,
            BoundWidth: 1,
            BoundHeight: 1,
            MaxNpc: 1,
            RestSeconds: 5,
            MaxPerGenerator: 1,
            TimerBegin: 0,
            TimerEnd: 0,
            BornX: 0,
            BornY: 0,
            SourceMapFolderId: null,
            SourceMonsterName: "TrainingMonster",
            SourceRelativePath: "1015/1.txt",
            Attributes: new Dictionary<string, string>());
    }

    private static LegacyMonsterInstance CreateRuntimeMonsterInstance(
        WorldDataCatalog worldData,
        MonsterSpawn spawn,
        Monster monster)
    {
        return LegacyMonsterInstance
            .FromGenerator(spawn, monster, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .First();
    }

    private static WorldDataCatalog CreateWorldData(
        Monster monster,
        IReadOnlyList<MonsterSpawn> monsterSpawns,
        IReadOnlyList<MagicType> magicRows,
        IReadOnlyList<GouyuType> gouyuTypes)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-22T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://laytrap-flamesigilrapid",
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
            gouyuTypes: gouyuTypes,
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
            magic: magicRows,
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
            monsters: [monster],
            monsterSpawns: monsterSpawns,
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

    private static MagicType CreateLayTrapMagicRow(ushort spellId, byte level, uint experience)
    {
        var fields = Enumerable.Repeat("0", 53).ToArray();
        fields[0] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = ((byte)LegacyMagicSort.LayTrap).ToString(CultureInfo.InvariantCulture);
        fields[3] = "FlameofDestruction";
        fields[8] = level.ToString(CultureInfo.InvariantCulture);
        fields[9] = "7";
        fields[10] = "1000";
        fields[12] = "100";
        fields[13] = "3";
        fields[14] = "4";
        fields[15] = "15";
        fields[18] = experience.ToString(CultureInfo.InvariantCulture);
        fields[22] = "511616601";
        fields[24] = "1032";
        fields[35] = "5000";
        fields[36] = "6000";
        fields[44] = "2535";
        fields[47] = "13000";
        return new MagicType(spellId, spellId, fields[3], fields);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData($"fixture://{name}", [], [], [], info: null, pole: null, conductors: []);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
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
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static List<LegacyFloorItemPacket> ParseFloorItems(IEnumerable<byte[]> frames)
    {
        var floorItems = new List<LegacyFloorItemPacket>();
        foreach (var bytes in frames)
        {
            if (LegacyFloorItemPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                floorItems.Add(packet);
            }
        }

        return floorItems;
    }

    private static List<LegacySpellAnimationPacket> ParseSpellAnimations(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacySpellAnimationPacket>();
        foreach (var bytes in frames)
        {
            if (LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static uint MulDivToUInt32(uint number, uint numerator, uint denominator)
    {
        if (denominator == 0)
        {
            return uint.MaxValue;
        }

        var result = (ulong)number * numerator / denominator;
        return result > uint.MaxValue ? uint.MaxValue : (uint)result;
    }

    private sealed record RepeatPulseResult(
        uint MonsterHpDelta,
        uint DisplayDamage,
        string Log);
}
