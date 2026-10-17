using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyAttackStatus20000RuntimeTests
{
    private const uint PlayerUid = 1_293_600;
    private const uint TargetUid = 1_293_601;
    private const ushort VenomSpellId = 20_000;
    private const int LegacyVenomFlag = 0x163;
    private const string RuntimeLegacyVenomDamageAttribute = "Venom";
    private const string RuntimeLegacyVenomExpiresAtUtcAttribute = "LegacyVenomExpiresAtUtc";
    private const long SafeZoneMapId = 10_250;

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    [Fact]
    public async Task HandlerVenomAgainstVisibleSafeZonePlayerAppliesFlagDamageAndSelfAnimation()
    {
        var worldData = CreateWorldData(CreateVenomMagicRow(damageOnHuman: 4_321, level: 3));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            uid: PlayerUid,
            name: "Caster",
            x: 830,
            y: 584,
            stamina: 100,
            spellLevel: 3,
            useSpellSoul: 2,
            mapId: SafeZoneMapId);
        var targetSession = CreateSession(
            uid: TargetUid,
            name: "Target",
            x: 831,
            y: 584,
            mapId: SafeZoneMapId);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);

        var before = DateTimeOffset.UtcNow;
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: new MonsterRuntimeStateStore()),
            ReadFrame(new MsgAttackPacket(CreateVenomQuery(TargetUid, x: 900, y: 901)).ToFrameBytes()));
        var after = DateTimeOffset.UtcNow;

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(490, updatedCasterSession.Character.MinMana);
        Assert.Equal("95", updatedCasterSession.Character.Attributes["Stamina"]);
        Assert.Equal(0, Assert.Single(updatedCasterSession.PlayerSpells.Spells).Experience);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(targetSession.Character.MinHitPoints, updatedTargetSession.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, LegacyVenomFlag));
        Assert.Equal("4321", updatedTargetSession.Character.Attributes[RuntimeLegacyVenomDamageAttribute]);
        Assert.True(DateTimeOffset.TryParse(
            updatedTargetSession.Character.Attributes[RuntimeLegacyVenomExpiresAtUtcAttribute],
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var expiresAt));
        Assert.InRange(expiresAt, before.AddSeconds(5), after.AddSeconds(7));

        var statusUpdate = Assert.Single(
            ParseMsgUpdates(targetSent),
            update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        var statusEntry = Assert.Single(statusUpdate.Updates);
        Assert.NotNull(statusEntry.Values);
        Assert.True(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyVenomFlag));

        foreach (var spell in new[] { Assert.Single(ParseSpellAnimations(casterSent)), Assert.Single(ParseSpellAnimations(targetSent)) })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(PlayerUid, spell.AttackedUid);
            Assert.Equal(0, spell.X);
            Assert.Equal(0, spell.Y);
            Assert.Equal(VenomSpellId, spell.SpellId);
            Assert.Equal(3, spell.SpellLevel);
            Assert.Equal(2, spell.LevelHu);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(PlayerUid, target.Uid);
            Assert.Equal(0U, target.Damage);
            Assert.False(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy AttackStatus Venom magic", output);
        Assert.Contains("source AttackStatus.cs case 20000", output);
        Assert.Contains("does not call CanAttackPlayer.Verified", output);
    }

    [Fact]
    public async Task HandlerVenomWithoutVisiblePlayerConsumesResourcesAndSendsNoAnimation()
    {
        var worldData = CreateWorldData(CreateVenomMagicRow(damageOnHuman: 777));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(stamina: 100);
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: new MonsterRuntimeStateStore()),
            ReadFrame(new MsgAttackPacket(CreateVenomQuery(9_999_999, x: 900, y: 901)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(490, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.DoesNotContain(ParseMsgUpdates(sent), update =>
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));

        var output = log.ToString();
        Assert.Contains("target is absent", output);
        Assert.Contains("after CanUseSpell.Verified", output);
    }

    [Fact]
    public async Task RuntimeLoopExpiresVenomFlagAndClearsVenomAttributes()
    {
        var worldData = CreateWorldData(CreateVenomMagicRow(damageOnHuman: 888));
        using var log = new StringWriter();
        using var cancel = new CancellationTokenSource();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData,
            new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                TimeSpan.FromMilliseconds(10),
                new AnchoredRuntimeTimeProvider(DateTimeOffset.UtcNow.AddSeconds(7))),
            log);
        var casterSession = CreateSession(stamina: 100);
        var targetSession = CreateSession(uid: TargetUid, name: "Target", x: 831, y: 584);
        SetHandlerSession(handler, casterConnection, casterSession);
        SetHandlerSession(handler, targetConnection, targetSession);

        try
        {
            await handler.HandleFrameAsync(
                casterConnection,
                ReadFrame(new MsgAttackPacket(CreateVenomQuery(TargetUid, x: 900, y: 901)).ToFrameBytes()),
                cancel.Token);

            var updatedTargetSession = AssertHandlerSession(handler, targetConnection);
            Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, LegacyVenomFlag));
            Assert.Equal("888", updatedTargetSession.Character.Attributes[RuntimeLegacyVenomDamageAttribute]);

            var runtimeStart = targetSent.Count;
            StartHandlerRuntimeUpdateLoop(handler, targetConnection, updatedTargetSession, cancel.Token);

            await WaitUntilAsync(
                () =>
                {
                    var frames = targetSent.ToArray().Skip(runtimeStart).ToArray();
                    return ParseMsgUpdates(frames)
                        .Any(update => update.Updates.Any(entry =>
                            entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                            entry.Values is not null &&
                            !IsLegacyStatusFlagSet(entry.Values, LegacyVenomFlag)));
                },
                TimeSpan.FromSeconds(1));

            var expiredTargetSession = AssertHandlerSession(handler, targetConnection);
            Assert.False(HasLegacyStatusFlag(expiredTargetSession.Character.Attributes, LegacyVenomFlag));
            Assert.False(expiredTargetSession.Character.Attributes.ContainsKey(RuntimeLegacyVenomDamageAttribute));
            Assert.False(expiredTargetSession.Character.Attributes.ContainsKey(RuntimeLegacyVenomExpiresAtUtcAttribute));
            Assert.Contains("WorldServer expired legacy Venom flags", log.ToString());
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    private static MsgAttackQuery CreateVenomQuery(uint targetUid, ushort x, ushort y)
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: targetUid,
            X: x,
            Y: y,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: VenomSpellId);
    }

    private static WorldSession CreateSession(
        long uid = PlayerUid,
        string name = "Caster",
        long x = 830,
        long y = 584,
        long minMana = 500,
        long minHitPoints = 5_000,
        long spirit = 300,
        ulong? stamina = null,
        ushort spellLevel = 0,
        byte useSpellSoul = 0,
        long mapId = 1_015)
    {
        return new WorldSession(
            AccountId: 1,
            Username: name.ToLowerInvariant(),
            CreateCharacter(uid, name, x, y, minMana, minHitPoints, spirit, stamina, mapId),
            PlayerItemSnapshot.Empty,
            new PlayerSpellSnapshot(
            [
                new PlayerSpellSnapshotEntry(
                    VenomSpellId,
                    Level: spellLevel,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: useSpellSoul),
            ]),
            PlayerProficiencySnapshot.Empty);
    }

    private static CharacterSnapshot CreateCharacter(
        long uid,
        string name,
        long x,
        long y,
        long minMana,
        long minHitPoints,
        long spirit,
        ulong? stamina,
        long mapId)
    {
        var attributes = new Dictionary<string, string>();
        if (stamina is { } value)
        {
            attributes["Stamina"] = value.ToString(CultureInfo.InvariantCulture);
        }

        return new CharacterSnapshot(
            Uid: uid,
            Name: name,
            Body: 1_003,
            Face: 0,
            Hair: 0,
            Class: 142,
            FirstClass: 142,
            SecondClass: 0,
            Reborn: 0,
            Level: 120,
            Experience: 0,
            MapId: mapId,
            X: x,
            Y: y,
            PreviousMapId: mapId,
            PreviousMapX: x,
            PreviousMapY: y,
            Strength: 0,
            Agility: 0,
            Vitality: 0,
            Spirit: spirit,
            AttributePoints: 0,
            MinHitPoints: minHitPoints,
            MinMana: minMana,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 1,
            SourceFile: "fixture://attack-status-20000",
            Attributes: attributes,
            PaidNobility: new Dictionary<string, string>());
    }

    private static WorldDataCatalog CreateWorldData(params MagicType[] magicRows)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-05T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://attack-status-20000",
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

    private static MagicType CreateVenomMagicRow(uint damageOnHuman, byte level = 0)
    {
        var fields = Enumerable.Repeat("0", 53).ToArray();
        fields[0] = VenomSpellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = VenomSpellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = ((byte)LegacyMagicSort.AttackStatus).ToString(CultureInfo.InvariantCulture);
        fields[3] = "Venom";
        fields[8] = level.ToString(CultureInfo.InvariantCulture);
        fields[9] = "10";
        fields[10] = "100";
        fields[13] = "0";
        fields[14] = "0";
        fields[15] = "9";
        fields[18] = "0";
        fields[20] = "0";
        fields[22] = "0";
        fields[29] = "5";
        fields[33] = "0";
        fields[35] = damageOnHuman.ToString(CultureInfo.InvariantCulture);
        fields[47] = "0";
        return new MagicType(VenomSpellId, VenomSpellId, "Venom", fields);
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

    private static void SetHandlerSession(
        WorldServerFrameHandler handler,
        ConquerTcpConnection connection,
        WorldSession session)
    {
        GetHandlerSessions(handler).Set(connection, session);
    }

    private static void StartHandlerRuntimeUpdateLoop(
        WorldServerFrameHandler handler,
        ConquerTcpConnection connection,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var method = typeof(WorldServerFrameHandler).GetMethod(
            "StartLegacyRuntimeUpdateLoop",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(
            handler,
            [connection, session.Character, session.PlayerItems, session.PlayerSpells, cancellationToken]);
    }

    private static WorldSessionStore GetHandlerSessions(WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<WorldSessionStore>(field.GetValue(handler));
    }

    private static WorldSession AssertHandlerSession(
        WorldServerFrameHandler handler,
        ConquerTcpConnection connection)
    {
        Assert.True(GetHandlerSessions(handler).TryGet(connection, out var session));
        Assert.NotNull(session);
        return session;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static List<LegacySpellAnimationPacket> ParseSpellAnimations(IEnumerable<byte[]> frames)
    {
        var spells = new List<LegacySpellAnimationPacket>();
        foreach (var bytes in frames)
        {
            if (LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var spell))
            {
                spells.Add(spell);
            }
        }

        return spells;
    }

    private static List<LegacyMsgUpdatePacket> ParseMsgUpdates(IEnumerable<byte[]> frames)
    {
        var updates = new List<LegacyMsgUpdatePacket>();
        foreach (var bytes in frames)
        {
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update))
            {
                updates.Add(update);
            }
        }

        return updates;
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

    private static bool IsLegacyStatusFlagSet(IReadOnlyList<ulong> values, int flag)
    {
        var wordIndex = flag / 32;
        return wordIndex < values.Count &&
            (values[wordIndex] & (1UL << (flag % 32))) != 0;
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (!predicate())
        {
            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException("Condition was not met before the timeout expired.");
            }

            await Task.Delay(10);
        }
    }
}
