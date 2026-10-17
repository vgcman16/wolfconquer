using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyKineticSparkPassiveMonsterGateTests
{
    private const uint PlayerUid = 1_293_600;
    private const int ClubItemType = 480_000;
    private const ushort KineticSparkSpellId = 11_590;
    private const byte KineticSparkSort = 67;
    private const int KineticSparkStatusFlag = 0x93;

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    [Fact]
    public async Task ArcherMonsterHitWithKineticSparkFlagAndRateFailDoesNotEmitPassiveAnimationAndFallsThrough()
    {
        var result = await RunMonsterHitAsync(
            MsgAttackType.Archer,
            hasStatusFlag: true,
            hasLearnedSpell: true,
            hasMagicRow: true,
            forcedRateRoll: 99);

        AssertNoKineticSparkAnimation(result.Sent);
        AssertNormalMonsterCombat(result);
    }

    [Fact]
    public async Task ArcherMonsterHitWithoutKineticSparkFlagDoesNotEmitPassiveAnimationAndFallsThrough()
    {
        var result = await RunMonsterHitAsync(
            MsgAttackType.Archer,
            hasStatusFlag: false,
            hasLearnedSpell: true,
            hasMagicRow: true,
            forcedRateRoll: 0);

        AssertNoKineticSparkAnimation(result.Sent);
        AssertNormalMonsterCombat(result);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ArcherMonsterHitWithMissingKineticSparkLearnedSpellOrMagicRowDoesNotEmitPassiveAnimationAndFallsThrough(
        bool hasLearnedSpell,
        bool hasMagicRow)
    {
        var result = await RunMonsterHitAsync(
            MsgAttackType.Archer,
            hasStatusFlag: true,
            hasLearnedSpell: hasLearnedSpell,
            hasMagicRow: hasMagicRow,
            forcedRateRoll: 0);

        AssertNoKineticSparkAnimation(result.Sent);
        AssertNormalMonsterCombat(result);
    }

    [Fact]
    public async Task PhysicalMonsterHitWithKineticSparkFlagAndRateSuccessDoesNotEmitPassiveAnimationAndFallsThrough()
    {
        var result = await RunMonsterHitAsync(
            MsgAttackType.Physical,
            hasStatusFlag: true,
            hasLearnedSpell: true,
            hasMagicRow: true,
            forcedRateRoll: 0);

        AssertNoKineticSparkAnimation(result.Sent);
        AssertNormalMonsterCombat(result);
    }

    private static async Task<MonsterHitResult> RunMonsterHitAsync(
        MsgAttackType attackType,
        bool hasStatusFlag,
        bool hasLearnedSpell,
        bool hasMagicRow,
        int forcedRateRoll)
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateCatalog(
            items: [CreateClubItemType()],
            monsters: [monsterFamily],
            monsterSpawns: [spawn],
            magic: hasMagicRow ? [CreateKineticSparkMagicRow()] : []);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyKineticSparkPassiveRateRoll"] = forcedRateRoll.ToString(CultureInfo.InvariantCulture),
        };
        if (hasStatusFlag)
        {
            attributes["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag);
        }

        var playerSpells = hasLearnedSpell
            ? new PlayerSpellSnapshot([CreatePlayerSpell(KineticSparkSpellId)])
            : PlayerSpellSnapshot.Empty;
        var session = CreateSession(CreateEquippedClub(), playerSpells, attributes);
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);
        var query = CreateMonsterAttackQuery(monster, attackType);
        var expectedPhysicalDamage = InvokePlayerMonsterPhysicalDamage(
            context,
            session,
            monster,
            monsterFamily,
            query);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(query).ToFrameBytes()));

        return new MonsterHitResult(sent, monsterStates, monster, attackType, expectedPhysicalDamage);
    }

    private static void AssertNoKineticSparkAnimation(IReadOnlyList<byte[]> sent)
    {
        Assert.DoesNotContain(
            ParseSpellAnimations(sent),
            spell => spell.SpellId == KineticSparkSpellId);
    }

    private static void AssertNormalMonsterCombat(MonsterHitResult result)
    {
        var attack = Assert.Single(ParseMonsterCombatAttacks(result.Sent, result.Monster.Uid, result.AttackType));
        Assert.Equal(PlayerUid, attack.Query.Uid);
        Assert.Equal(result.Monster.Uid, attack.Query.OpponentUid);
        Assert.Equal(result.AttackType, attack.Query.AttackType);
        Assert.Equal(checked((int)result.ExpectedPhysicalDamage), attack.Query.Damage);
        Assert.Equal(0, attack.Query.KilledMonster);
        Assert.Equal(result.Monster.X, attack.Query.X);
        Assert.Equal(result.Monster.Y, attack.Query.Y);

        Assert.True(result.MonsterStates.TryGetVisibleInstance(
            result.Monster,
            DateTimeOffset.UtcNow,
            out var damagedMonster));
        Assert.Equal(result.Monster.MaxLife - result.ExpectedPhysicalDamage, damagedMonster.Hitpoints);
    }

    private static uint InvokePlayerMonsterPhysicalDamage(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        MsgAttackQuery query)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "CalculateLegacyPlayerMonsterDamage",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<uint>(method.Invoke(null, [context, session, monster, monsterFamily, query]));
    }

    private static WorldSession CreateSession(
        PlayerItemSnapshot playerItems,
        PlayerSpellSnapshot playerSpells,
        IReadOnlyDictionary<string, string> attributes)
    {
        return new WorldSession(
            AccountId: 1,
            Username: "kinetic-spark-passive",
            new CharacterSnapshot(
                Uid: PlayerUid,
                Name: "KineticSparkPassive",
                Body: 1_003,
                Face: 0,
                Hair: 0,
                Class: 142,
                FirstClass: 142,
                SecondClass: 0,
                Reborn: 0,
                Level: 120,
                Experience: 0,
                MapId: 1_015,
                X: 830,
                Y: 584,
                PreviousMapId: 1_015,
                PreviousMapX: 830,
                PreviousMapY: 584,
                Strength: 1_000,
                Agility: 0,
                Vitality: 0,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: 5_000,
                MinMana: 500,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 1,
                SourceFile: "fixture://kinetic-spark-passive-monster-gate",
                Attributes: new Dictionary<string, string>(attributes, StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            playerItems,
            playerSpells,
            PlayerProficiencySnapshot.Empty,
            CombatGear: CombatGearSnapshot.Empty);
    }

    private static MsgAttackQuery CreateMonsterAttackQuery(LegacyMonsterInstance monster, MsgAttackType attackType)
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: monster.Uid,
            X: monster.X,
            Y: monster.Y,
            AttackType: attackType,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0);
    }

    private static PlayerSpellSnapshotEntry CreatePlayerSpell(ushort spellId, ushort level = 0)
    {
        return new PlayerSpellSnapshotEntry(
            spellId,
            level,
            Experience: 0,
            PreviousLevel: 0,
            SoulLevel: 0,
            UseSpellSoul: 0);
    }

    private static PlayerItemSnapshot CreateEquippedClub()
    {
        return new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                new PlayerItemSnapshotItem
                {
                    ItemId = ClubItemType,
                    Durability = 100,
                    MaximumDurability = 100,
                    Position = 4,
                },
            ],
            RuneItems: []);
    }

    private static ItemType CreateClubItemType()
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = ClubItemType.ToString(CultureInfo.InvariantCulture);
        fields[1] = "SourceClueClub";
        fields[13] = "1000";
        fields[14] = "1000";
        return new ItemType(ClubItemType, "SourceClueClub", fields);
    }

    private static MagicType CreateKineticSparkMagicRow()
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = KineticSparkSpellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = KineticSparkSpellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = KineticSparkSort.ToString(CultureInfo.InvariantCulture);
        fields[3] = "KineticSpark";
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = "100";
        fields[12] = "30";
        fields[13] = "0";
        fields[14] = "1";
        fields[15] = "10";
        fields[18] = "0";
        fields[20] = "1";
        fields[22] = "0";
        fields[29] = "0";
        fields[33] = "0";
        fields[35] = "0";
        fields[36] = "0";
        fields[37] = "0";
        fields[44] = "0";
        fields[47] = "0";
        return new MagicType(KineticSparkSpellId, KineticSparkSpellId, "KineticSpark", fields);
    }

    private static Monster CreateMonsterFamily()
    {
        return new Monster(
            9_001,
            "TrainingMonster",
            new Dictionary<string, string>
            {
                ["life"] = "50000",
                ["lookface"] = "100",
                ["level"] = "120",
                ["defense"] = "100",
                ["defence"] = "100",
                ["defense2"] = "10000",
                ["defence2"] = "10000",
                ["attack_range"] = "0",
            },
            []);
    }

    private static MonsterSpawn CreateMonsterSpawn()
    {
        return new MonsterSpawn(
            GeneratorId: 1,
            MapId: 1_015,
            MonsterTypeId: 9_001,
            BoundX: 832,
            BoundY: 584,
            BoundWidth: 1,
            BoundHeight: 1,
            MaxNpc: 1,
            RestSeconds: 10,
            MaxPerGenerator: 1,
            TimerBegin: 0,
            TimerEnd: 0,
            BornX: 0,
            BornY: 0,
            SourceMapFolderId: 1_015,
            SourceMonsterName: "TrainingMonster",
            SourceRelativePath: Path.Combine("1015", "TrainingMonster", "1.ini"),
            Attributes: new Dictionary<string, string>
            {
                ["id"] = "1",
                ["mapid"] = "1015",
                ["npctype"] = "9001",
            });
    }

    private static string BuildStatusFlags(params int[] flags)
    {
        var words = new ulong[10];
        foreach (var flag in flags)
        {
            words[flag / 32] |= 1UL << (flag % 32);
        }

        return string.Join('/', words) + "/";
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

    private static IReadOnlyList<LegacySpellAnimationPacket> ParseSpellAnimations(IEnumerable<byte[]> frames)
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

    private static IReadOnlyList<MsgAttackPacket> ParseMonsterCombatAttacks(
        IEnumerable<byte[]> frames,
        uint opponentUid,
        MsgAttackType attackType)
    {
        var packets = new List<MsgAttackPacket>();
        foreach (var bytes in frames)
        {
            if (MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Query.AttackType == attackType &&
                packet.Query.OpponentUid == opponentUid)
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static WorldDataCatalog CreateCatalog(
        IReadOnlyList<ItemType> items,
        IReadOnlyList<Monster> monsters,
        IReadOnlyList<MonsterSpawn> monsterSpawns,
        IReadOnlyList<MagicType> magic)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-23T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://kinetic-spark-passive-monster-gate",
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
            magic: magic,
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
            monsters: monsters,
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

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    private sealed record MonsterHitResult(
        IReadOnlyList<byte[]> Sent,
        MonsterRuntimeStateStore MonsterStates,
        LegacyMonsterInstance Monster,
        MsgAttackType AttackType,
        uint ExpectedPhysicalDamage);
}
