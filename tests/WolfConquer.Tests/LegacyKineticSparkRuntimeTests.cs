using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyKineticSparkRuntimeTests
{
    private const uint PlayerUid = 1_293_600;
    private const ushort KineticSparkSpellId = 11_590;
    private const byte KineticSparkSort = 67;
    private const int KineticSparkStatusFlag = 0x93;
    private const int KineticSparkDurationSeconds = 2_592_000;
    private const string RuntimeLegacyKineticSparkExpiresAtUtcAttribute = "LegacyKineticSparkExpiresAtUtc";

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    [Fact]
    public async Task HandlerKineticSparkDirectCastAppliesFlagExpiryStatusUpdateAndSelfAnimation()
    {
        var worldData = CreateWorldData(CreateKineticSparkMagicRow());
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        sessions.Set(connection, session);

        var beforeCast = DateTimeOffset.UtcNow;
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: new MonsterRuntimeStateStore()),
            ReadFrame(new MsgAttackPacket(CreateKineticSparkQuery()).ToFrameBytes()));
        var afterCast = DateTimeOffset.UtcNow;

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, KineticSparkStatusFlag));
        AssertLegacyKineticSparkExpiresAround(
            updatedSession.Character.Attributes,
            beforeCast,
            afterCast);

        var statusUpdate = Assert.Single(
            ParseStatusFlagUpdates(sent),
            update => update.Uid == PlayerUid);
        var statusEntry = Assert.Single(statusUpdate.Updates);
        Assert.NotNull(statusEntry.Values);
        Assert.True(IsLegacyStatusFlagSet(statusEntry.Values!, KineticSparkStatusFlag));

        AssertSourceShapedSelfAnimation(Assert.Single(ParseSpellAnimations(sent)));
    }

    [Fact]
    public async Task HandlerKineticSparkDirectCastWithExistingFlagRemovesFlagClearsExpiryAndKeepsSourceAnimation()
    {
        var worldData = CreateWorldData(CreateKineticSparkMagicRow());
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
                [RuntimeLegacyKineticSparkExpiresAtUtcAttribute] = "2026-07-23T00:00:00.0000000+00:00",
            });
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: new MonsterRuntimeStateStore()),
            ReadFrame(new MsgAttackPacket(CreateKineticSparkQuery()).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, KineticSparkStatusFlag));
        Assert.False(updatedSession.Character.Attributes.ContainsKey(RuntimeLegacyKineticSparkExpiresAtUtcAttribute));

        var statusUpdate = Assert.Single(
            ParseStatusFlagUpdates(sent),
            update => update.Uid == PlayerUid);
        var statusEntry = Assert.Single(statusUpdate.Updates);
        Assert.NotNull(statusEntry.Values);
        Assert.False(IsLegacyStatusFlagSet(statusEntry.Values!, KineticSparkStatusFlag));

        AssertSourceShapedSelfAnimation(Assert.Single(ParseSpellAnimations(sent)));
    }

    private static MsgAttackQuery CreateKineticSparkQuery()
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: PlayerUid,
            X: 840,
            Y: 584,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: KineticSparkSpellId);
    }

    private static WorldSession CreateSession(
        IReadOnlyDictionary<string, string>? attributes = null,
        ushort spellLevel = 0,
        byte useSpellSoul = 0)
    {
        return new WorldSession(
            AccountId: 1,
            Username: "caster",
            CreateCharacter(attributes),
            PlayerItemSnapshot.Empty,
            new PlayerSpellSnapshot(
            [
                new PlayerSpellSnapshotEntry(
                    KineticSparkSpellId,
                    Level: spellLevel,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: useSpellSoul),
            ]),
            PlayerProficiencySnapshot.Empty);
    }

    private static CharacterSnapshot CreateCharacter(IReadOnlyDictionary<string, string>? runtimeAttributes)
    {
        var attributes = runtimeAttributes is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(runtimeAttributes, StringComparer.OrdinalIgnoreCase);

        return new CharacterSnapshot(
            Uid: PlayerUid,
            Name: "Caster",
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
            Strength: 0,
            Agility: 0,
            Vitality: 0,
            Spirit: 300,
            AttributePoints: 0,
            MinHitPoints: 5_000,
            MinMana: 500,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 1,
            SourceFile: "fixture://kinetic-spark-runtime",
            Attributes: attributes,
            PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private static WorldDataCatalog CreateWorldData(params MagicType[] magicRows)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-23T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://kinetic-spark-runtime",
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

    private static MagicType CreateKineticSparkMagicRow()
    {
        var fields = Enumerable.Repeat("0", 53).ToArray();
        fields[0] = KineticSparkSpellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = KineticSparkSpellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = KineticSparkSort.ToString(CultureInfo.InvariantCulture);
        fields[3] = "KineticSpark";
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = "0";
        fields[13] = "0";
        fields[14] = "0";
        fields[15] = "0";
        fields[18] = "0";
        fields[20] = "0";
        fields[22] = "0";
        fields[29] = "0";
        fields[33] = "0";
        fields[47] = "0";
        return new MagicType(KineticSparkSpellId, KineticSparkSpellId, "KineticSpark", fields);
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

    private static IReadOnlyList<LegacyMsgUpdatePacket> ParseStatusFlagUpdates(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyMsgUpdatePacket>();
        foreach (var bytes in frames)
        {
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Updates.Any(update => update.Type == LegacyMsgUpdateDataType.StatusFlag))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static void AssertSourceShapedSelfAnimation(LegacySpellAnimationPacket spell)
    {
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(840, spell.X);
        Assert.Equal(584, spell.Y);
        Assert.Equal(KineticSparkSpellId, spell.SpellId);
        Assert.Equal(0, spell.SpellLevel);
        Assert.Equal(0, spell.LevelHu);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
    }

    private static void AssertLegacyKineticSparkExpiresAround(
        IReadOnlyDictionary<string, string> attributes,
        DateTimeOffset beforeCast,
        DateTimeOffset afterCast)
    {
        Assert.True(attributes.TryGetValue(RuntimeLegacyKineticSparkExpiresAtUtcAttribute, out var rawExpiresAt));
        Assert.True(DateTimeOffset.TryParse(
            rawExpiresAt,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var expiresAt));
        Assert.True(expiresAt >= beforeCast.AddSeconds(KineticSparkDurationSeconds));
        Assert.True(expiresAt <= afterCast.AddSeconds(KineticSparkDurationSeconds));
    }

    private static bool HasLegacyStatusFlag(IReadOnlyDictionary<string, string> attributes, int flag)
    {
        return attributes.TryGetValue("StatusFlags", out var raw) &&
            IsLegacyStatusFlagSet(
                raw
                    .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(part => ulong.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                        ? value
                        : 0)
                    .ToArray(),
                flag);
    }

    private static string BuildStatusFlags(params int[] flags)
    {
        var words = new ulong[18];
        foreach (var flag in flags)
        {
            words[flag / 32] |= 1UL << (flag % 32);
        }

        return string.Join('/', words);
    }

    private static bool IsLegacyStatusFlagSet(IReadOnlyList<ulong> values, int flag)
    {
        var wordIndex = flag / 32;
        return wordIndex < values.Count &&
            (values[wordIndex] & (1UL << (flag % 32))) != 0;
    }
}
