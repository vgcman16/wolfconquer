using System.Buffers;
using System.Globalization;
using System.Net;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("LegacySobNpcEventRuntime")]
public sealed class LegacyDirectSobNpcPhysicalRuntimeTests
{
    private const uint PlayerUid = 1_293_600;
    private const int ClubItemType = 480_000;
    private const ushort KineticSparkSpellId = 11_590;
    private const ushort ArcherPreProcCrescentShadowSpellId = 18_750;
    private const ushort ArcherPreProcNebulousHuntSpellId = 10_990;
    private const int KineticSparkStatusFlag = 0x93;
    private const string RuntimeLegacyKineticSparkPassiveRateRollAttribute = "LegacyKineticSparkPassiveRateRoll";
    private const string RuntimeLegacyArcherPreProcRateRollAttribute = "LegacyArcherPreProcRateRoll";
    private const string RuntimeLegacyThundercloudSightAttribute = "ThundercloudSight";
    private const string RuntimeLegacyAttackHitAttribute = "AttackHit";
    private const int MapId = 1_015;
    private const int FocusedStatusFlag = 0x15;
    private const int EmperorWarPoleLife = 50_000_000;
    private const int TopPoleLife = 50_000_000;
    private const int WarFightersPoleLife = 50_000_000;
    private const int TrepilWarPoleLife = 250_000_000;
    private const int NobilityPoleLife = 50_000;
    private const int ClassPoleWarPoleLife = 50_000_000;
    private const int ClanWarScorePoleLife = 100_000_000;
    private const int ClanWarPoleLife = 30_000_000;
    private const int UnionWarPoleMapId = 22_348;
    private const int UnionWarPoleLife = 800_000_000;
    private const uint FirstRightCornerChatType = 2_108;
    private const uint ContinueRightCornerChatType = 2_109;
    private const ushort PlayerX = 205;
    private const ushort PlayerY = 230;
    private const byte BombSort = 5;
    private const byte SectorBackSort = 69;
    private const byte KineticSparkSort = 67;

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    [Theory]
    [InlineData(MsgAttackType.Physical)]
    [InlineData(MsgAttackType.Archer)]
    public async Task HandlerDirectSobNpcAttackWithoutPreProcSendsAttackDamagesRuntimeAndSetsReceiveAttributes(
        MsgAttackType attackType)
    {
        var sobNpc = CreateSobNpc(id: 65_610, name: "DirectPole", kind: LegacySobNpcEventRules.PoleKind);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [CreateKineticSparkMagicRow(), CreateArcherPreProcMagicRow(ArcherPreProcCrescentShadowSpellId)],
            sobNpcs: [sobNpc],
            mapIds: [MapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [KineticSparkSpellId, ArcherPreProcCrescentShadowSpellId],
            includeKineticSparkStatus: attackType == MsgAttackType.Archer,
            strength: 1_000,
            archerPreProcRateRolls: new Dictionary<ushort, uint>
            {
                [ArcherPreProcCrescentShadowSpellId] = 99,
            });
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(sobNpc, attackType, damage: 500)).ToFrameBytes()));

        var responses = ParseAttacks(sent);
        var matchingResponses = responses
            .Where(attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == attackType)
            .ToArray();
        Assert.True(
            matchingResponses.Length == 1,
            $"Expected one direct SOB {attackType} MsgAttack response. Matches={matchingResponses.Length} " +
            $"AttackFrames={responses.Count}. Log={log}");
        var response = matchingResponses[0];
        Assert.InRange(response.Query.Damage, 1, sobNpc.Life - 1);

        var visible = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(sobNpc.Life - response.Query.Damage, visible.Life);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(
            sobNpc.Id.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes[RuntimeLegacyThundercloudSightAttribute]);
        Assert.Equal("true", updatedSession.Character.Attributes[RuntimeLegacyAttackHitAttribute]);
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.DoesNotContain("WorldServer handled legacy Archer pre-Kinetic SOB NPC proc", log.ToString());
        Assert.DoesNotContain("WorldServer handled legacy KineticSpark passive SOB NPC proc", log.ToString());
    }

    [Fact]
    public async Task HandlerDirectSobNpcHitUsesFocusedOneAttackBoost()
    {
        var sobNpc = CreateSobNpc(id: 65_615, name: "FocusedPole", kind: LegacySobNpcEventRules.PoleKind);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [MapId]);

        var baselineSent = new List<byte[]>();
        using var baselineLog = new StringWriter();
        var baselineConnection = CreateSendingConnection(baselineSent);
        var baselineSessions = new WorldSessionStore();
        var baselineSobNpcStates = new SobNpcRuntimeStateStore();
        var baselineSession = CreateSession(spellIds: [], strength: 1_000);
        baselineSessions.Set(baselineConnection, baselineSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                baselineConnection,
                baselineLog,
                baselineSession,
                WorldData: worldData,
                Sessions: baselineSessions,
                SobNpcStates: baselineSobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(sobNpc, MsgAttackType.Physical, damage: 500)).ToFrameBytes()));

        var baselineHit = Assert.Single(
            ParseAttacks(baselineSent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);

        var focusedSent = new List<byte[]>();
        using var focusedLog = new StringWriter();
        var focusedConnection = CreateSendingConnection(focusedSent);
        var focusedSessions = new WorldSessionStore();
        var focusedSobNpcStates = new SobNpcRuntimeStateStore();
        var focusedSession = CreateSession(
            spellIds: [],
            strength: 1_000,
            extraCharacterAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(FocusedStatusFlag),
                ["IntensifyDamage"] = "200",
            });
        focusedSessions.Set(focusedConnection, focusedSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                focusedConnection,
                focusedLog,
                focusedSession,
                WorldData: worldData,
                Sessions: focusedSessions,
                SobNpcStates: focusedSobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(sobNpc, MsgAttackType.Physical, damage: 500)).ToFrameBytes()));

        var focusedHit = Assert.Single(
            ParseAttacks(focusedSent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);

        Assert.True(
            focusedHit.Query.Damage > baselineHit.Query.Damage,
            $"Expected Focused SOB hit to exceed baseline. baseline={baselineHit.Query.Damage} focused={focusedHit.Query.Damage}");
        Assert.True(focusedSessions.TryGet(focusedConnection, out var updatedFocusedSession));
        Assert.NotNull(updatedFocusedSession);
        Assert.False(HasStatusFlag(updatedFocusedSession.Character.Attributes, FocusedStatusFlag));
        Assert.False(updatedFocusedSession.Character.Attributes.ContainsKey("doFocus"));
        Assert.False(updatedFocusedSession.Character.Attributes.ContainsKey("DoFocus"));
        Assert.Contains("WorldServer consumed legacy Focused attack", focusedLog.ToString());
    }

    [Fact]
    public async Task HandlerDirectTrainingSobNpcKillSendsDeathAndResetHitpointUpdates()
    {
        var sobNpc = CreateSobNpc(
            id: 65_611,
            name: "TrainingStake",
            kind: LegacySobNpcEventRules.StakeKind,
            mapId: LegacySobNpcEventRules.TrainingMapId,
            x: PlayerX + 1,
            y: PlayerY,
            life: 10_000,
            maxLife: 10_000);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 25_000, maxAttack: 25_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.TrainingMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: LegacySobNpcEventRules.TrainingMapId,
            x: PlayerX,
            y: PlayerY,
            spellIds: [],
            strength: 25_000);
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 25_000)).ToFrameBytes()));

        var attacks = ParseAttacks(sent);
        var directHits = attacks
            .Where(attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical)
            .ToArray();
        Assert.True(
            directHits.Length == 1,
            $"Expected one direct training SOB Physical MsgAttack response. Matches={directHits.Length} " +
            $"AttackFrames={attacks.Count}. Log={log}");
        var directHit = directHits[0];
        Assert.True(directHit.Query.Damage >= sobNpc.Life, $"Direct hit did not kill the training SOB. Log={log}");

        var deaths = attacks
            .Where(attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Death)
            .ToArray();
        Assert.True(
            deaths.Length == 1,
            $"Expected one training SOB Death MsgAttack. Matches={deaths.Length} AttackFrames={attacks.Count}. Log={log}");
        var death = deaths[0];
        Assert.Equal(sobNpc.X, death.Query.X);
        Assert.Equal(sobNpc.Y, death.Query.Y);

        var updates = ParseMsgUpdates(sent).Where(packet => packet.Uid == checked((uint)sobNpc.Id)).ToArray();
        Assert.Contains(
            updates,
            packet => packet.Updates.Count == 1 &&
                packet.Updates[0].Type == LegacyMsgUpdateDataType.Hitpoints &&
                packet.Updates[0].Value == (uint)sobNpc.MaxLife);
        Assert.Contains(
            updates,
            packet => packet.Updates.Count == 1 &&
                packet.Updates[0].Type == LegacyMsgUpdateDataType.MaxHitpoints &&
                packet.Updates[0].Value == (uint)sobNpc.MaxLife);

        var visible = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(sobNpc.MaxLife, visible.Life);
        Assert.Equal(sobNpc.MaxLife, visible.MaxLife);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(
            sobNpc.Id.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes[RuntimeLegacyThundercloudSightAttribute]);
        Assert.Equal("true", updatedSession.Character.Attributes[RuntimeLegacyAttackHitAttribute]);
    }

    [Theory]
    [InlineData(MsgAttackType.Physical)]
    [InlineData(MsgAttackType.Archer)]
    public async Task HandlerDirectSobNpcMap1038NonLethalHitAppliesSourceReturnExperience(
        MsgAttackType attackType)
    {
        const int trainingWarMapId = 1_038;
        var sobNpc = CreateSobNpc(
            id: 65_612,
            name: "TrainingWarPole",
            kind: LegacySobNpcEventRules.PoleKind,
            mapId: trainingWarMapId,
            x: PlayerX + 1,
            y: PlayerY,
            life: 500_000,
            maxLife: 500_000);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 50_000, maxAttack: 50_000)],
            magicRows: [CreateKineticSparkMagicRow(), CreateArcherPreProcMagicRow(ArcherPreProcCrescentShadowSpellId)],
            sobNpcs: [sobNpc],
            mapIds: [trainingWarMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [KineticSparkSpellId, ArcherPreProcCrescentShadowSpellId],
            includeKineticSparkStatus: attackType == MsgAttackType.Archer,
            archerPreProcRateRolls: new Dictionary<ushort, uint>
            {
                [ArcherPreProcCrescentShadowSpellId] = 99,
            },
            mapId: trainingWarMapId,
            x: PlayerX,
            y: PlayerY,
            strength: 50_000);
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                attackType,
                damage: 50_000)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == attackType);
        Assert.InRange((ulong)directHit.Query.Damage, 1UL, checked((ulong)(sobNpc.Life - 1)));
        var expectedReturnExperience = checked((ulong)directHit.Query.Damage) / 1_000;
        var expectedExperience = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            CreateExpectedLegacyExperienceInputs(session, expectedReturnExperience));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(checked((long)expectedExperience.AppliedExperienceGain), updatedSession.Character.Experience);
        Assert.Contains(ParseMsgUpdates(sent), update =>
            update.Uid == PlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.Experience &&
                entry.Value == expectedExperience.AppliedExperienceGain));

        var output = log.ToString();
        Assert.Contains("WorldServer sent legacy direct SOB NPC experience", output);
        Assert.Contains($"returnExperience={expectedReturnExperience}", output);
        Assert.Contains("source=ReceiveAttack.Npc.Execute return -> IncreaseExperience.Up", output);
    }

    [Theory]
    [InlineData(
        LegacySobNpcEventRules.GuildWarLeftGateUid,
        "LeftGate",
        1_008,
        LegacySobNpcEventRules.GuildWarLeftGateDefaultDemolitionMesh,
        LegacyNewSynWarPacket.LeftGateDestroyedDwParam)]
    [InlineData(
        LegacySobNpcEventRules.GuildWarRightGateUid,
        "RightGate",
        1_007,
        LegacySobNpcEventRules.GuildWarRightGateDefaultDemolitionMesh,
        LegacyNewSynWarPacket.RightGateDestroyedDwParam)]
    public async Task HandlerDirectPhysicalSobNpcAttackGuildWarGateDeathSendsNewSynWarAndDemolitionSobNpcUpdate(
        int sobNpcId,
        string sobNpcName,
        int lookface,
        int expectedDemolitionMesh,
        uint expectedDestroyedDwParam)
    {
        var sobNpc = CreateSobNpc(
            id: sobNpcId,
            name: sobNpcName,
            kind: LegacySobNpcEventRules.GateKind,
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            x: PlayerX + 1,
            y: PlayerY,
            life: 1,
            maxLife: 50_000,
            lookface: lookface);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 25_000, maxAttack: 25_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.GuildWarMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var guildWarStates = new LegacyGuildWarRuntimeStateStore();
        const uint gateOwnerGuildId = 88;
        sobNpcStates.SetOwnerGuildId(sobNpc, gateOwnerGuildId);
        guildWarStates.Start();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            x: PlayerX,
            y: PlayerY,
            strength: 25_000,
            extraCharacterAttributes: CreateGuildAttributes());
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates,
                GuildWarStates: guildWarStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 25_000)).ToFrameBytes()));

        var attacks = ParseAttacks(sent);
        var directHit = Assert.Single(
            attacks,
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(directHit.Query.Damage >= sobNpc.Life, $"Direct hit did not kill the GuildWar gate. Log={log}");

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);
        Assert.Equal(expectedDemolitionMesh, visibleSobNpc.Lookface);
        Assert.Equal(gateOwnerGuildId, sobNpcStates.GetOwnerGuildId(sobNpc));

        var spawn = Assert.Single(ParseSobNpcSpawns(sent));
        Assert.Equal(checked((uint)sobNpc.Id), spawn.Uid);
        Assert.Equal((uint)sobNpc.MaxLife, spawn.Hitpoints);
        Assert.Equal((uint)sobNpc.MaxLife, spawn.MaxHitpoints);
        Assert.Equal(expectedDemolitionMesh, spawn.Mesh);
        Assert.Equal(LegacySobNpcEventRules.GateKind, spawn.Type);

        var packet = Assert.Single(ParseNewSynWarPackets(sent));
        Assert.Equal(LegacyNewSynWarPacket.DestroyedType, packet.Type);
        Assert.Equal(expectedDestroyedDwParam, packet.DwParam);
        Assert.Equal(gateOwnerGuildId, packet.SyndicateId);
        Assert.True(packet.IsDestroyed);
        Assert.False(packet.IsOwnershipChanged);

        var output = log.ToString();
        Assert.Contains("WorldServer sent LegacyNewSynWar GuildWar gate destroyed", output);
        Assert.Contains("WorldServer sent LegacySobNpcSpawn GuildWar gate demolition", output);
    }

    [Theory]
    [InlineData(
        LegacySobNpcEventRules.GuildWarExorcimPillarUid,
        "ExorcimPillar",
        LegacySobNpcEventRules.GuildWarExorcimOwnershipChangeDwParam)]
    [InlineData(
        LegacySobNpcEventRules.GuildWarTenacityPillarUid,
        "TenacityPillar",
        LegacySobNpcEventRules.GuildWarTenacityOwnershipChangeDwParam)]
    public async Task HandlerDirectPhysicalSobNpcAttackGuildWarCapturePillarDeathSendsNewSynWarAndCaptureSobNpcUpdate(
        int sobNpcId,
        string sobNpcName,
        uint expectedOwnershipDwParam)
    {
        var sobNpc = CreateSobNpc(
            id: sobNpcId,
            name: sobNpcName,
            kind: LegacySobNpcEventRules.GuildWarCapturePillarKind,
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            x: PlayerX + 1,
            y: PlayerY,
            life: 1,
            maxLife: 50_000);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 25_000, maxAttack: 25_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.GuildWarMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var guildWarStates = new LegacyGuildWarRuntimeStateStore();
        guildWarStates.Start();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            x: PlayerX,
            y: PlayerY,
            strength: 25_000,
            extraCharacterAttributes: CreateGuildAttributes());
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates,
                GuildWarStates: guildWarStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 25_000)).ToFrameBytes()));

        var attacks = ParseAttacks(sent);
        var directHit = Assert.Single(
            attacks,
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(directHit.Query.Damage >= sobNpc.Life, $"Direct hit did not kill the GuildWar pillar. Log={log}");

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        var ownerGuildId = sobNpcStates.GetOwnerGuildId(sobNpc);
        Assert.Equal(77U, ownerGuildId);
        Assert.Equal("SourceGuild", visibleSobNpc.Name);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);
        Assert.Equal(sobNpc.Lookface, visibleSobNpc.Lookface);

        var spawn = Assert.Single(ParseSobNpcSpawns(sent));
        Assert.Equal(checked((uint)sobNpc.Id), spawn.Uid);
        Assert.Equal((uint)sobNpc.MaxLife, spawn.Hitpoints);
        Assert.Equal((uint)sobNpc.MaxLife, spawn.MaxHitpoints);
        Assert.Equal(sobNpc.Lookface, spawn.Mesh);
        Assert.Equal(LegacySobNpcEventRules.GuildWarCapturePillarKind, spawn.Type);
        Assert.Equal("SourceGuild", spawn.Name);

        var packet = Assert.Single(ParseNewSynWarPackets(sent));
        Assert.Equal(LegacyNewSynWarPacket.OwnershipChangedType, packet.Type);
        Assert.Equal(expectedOwnershipDwParam, packet.DwParam);
        Assert.Equal(ownerGuildId, packet.SyndicateId);
        Assert.True(packet.IsOwnershipChanged);
        Assert.False(packet.IsDestroyed);

        var output = log.ToString();
        Assert.Contains("WorldServer sent LegacyNewSynWar GuildWar pillar ownership", output);
        Assert.Contains("WorldServer sent LegacySobNpcSpawn GuildWar pillar capture", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackCaptureTheFlagBaseNonLethalHitRecordsBaseScoreWithoutCapture()
    {
        var sobNpc = CreateSobNpc(
            id: 9_001,
            name: "EnemyBase",
            kind: LegacySobNpcEventRules.PoleKind,
            mapId: 2_057,
            x: PlayerX + 1,
            y: PlayerY,
            life: 50_000,
            maxLife: 50_000);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [2_057]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var attributes = CreateGuildAttributes();
        attributes["CaptureTheFlagBases"] = "9001,206,230,88,0,1";
        var session = CreateSession(
            spellIds: [],
            mapId: 2_057,
            x: PlayerX,
            y: PlayerY,
            strength: 1_000,
            extraCharacterAttributes: attributes);
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));

        var attacks = ParseAttacks(sent);
        var directHit = Assert.Single(
            attacks,
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange((ulong)directHit.Query.Damage, 1UL, checked((ulong)(sobNpc.Life - 1)));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("EnemyBase", visibleSobNpc.Name);
        Assert.Equal(sobNpc.Life - checked((int)directHit.Query.Damage), visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal("9001,206,230,88,0,1", updatedSession.Character.Attributes["CaptureTheFlagBases"]);

        var score = Assert.Single(
            ReadCaptureTheFlagBaseScores(updatedSession, log.ToString()),
            entry => entry.BaseUid == checked((uint)sobNpc.Id) && entry.GuildId == 77);
        Assert.Equal("SourceGuild", score.GuildName);
        Assert.Equal((long)directHit.Query.Damage, (long)score.Score);

        var scoreBase = ParseCaptureTheFlagScoreBasePackets(sent);
        if (scoreBase.Count > 0)
        {
            var packet = Assert.Single(scoreBase);
            Assert.Equal(LegacyCaptureTheFlagUpdateMode.ScoreBase, packet.Mode);
            var entry = Assert.Single(packet.Entries);
            Assert.Equal(0U, entry.Rank);
            Assert.Equal((long)directHit.Query.Damage, (long)entry.Score);
            Assert.Equal("SourceGuild", entry.GuildName);
        }

        Assert.Empty(ParseSobNpcSpawns(sent));
        Assert.DoesNotContain(
            ParseCaptureTheFlagUpdates(sent),
            packet => packet.Mode == LegacyCaptureTheFlagUpdateMode.OccupiedBase);
        var output = log.ToString();
        Assert.Contains("WorldServer updated legacy CaptureTheFlag base score", output);
        Assert.Contains("source MsgCaptureTheFlag.Basse.UpdateScore modeled", output);
        Assert.DoesNotContain("WorldServer sent legacy CaptureTheFlag base capture", output);
        Assert.DoesNotContain("source MsgCaptureTheFlag.UpdateFlagScore killed base branch", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackCaptureTheFlagBaseFollowUpHitAccumulatesBaseScore()
    {
        var sobNpc = CreateSobNpc(
            id: 9_001,
            name: "EnemyBase",
            kind: LegacySobNpcEventRules.PoleKind,
            mapId: 2_057,
            x: PlayerX + 1,
            y: PlayerY,
            life: 50_000,
            maxLife: 50_000);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [2_057]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var attributes = CreateGuildAttributes();
        attributes["CaptureTheFlagBases"] = "9001,206,230,88,0,1";
        var session = CreateSession(
            spellIds: [],
            mapId: 2_057,
            x: PlayerX,
            y: PlayerY,
            strength: 1_000,
            extraCharacterAttributes: attributes);
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));
        var firstHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange((ulong)firstHit.Query.Damage, 1UL, checked((ulong)(sobNpc.Life - 1)));
        Assert.True(sessions.TryGet(connection, out var afterFirstHit));
        Assert.NotNull(afterFirstHit);

        sent.Clear();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                afterFirstHit,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));

        var secondHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange(
            (ulong)secondHit.Query.Damage,
            1UL,
            checked((ulong)(sobNpc.Life - checked((int)firstHit.Query.Damage) - 1)));
        Assert.True(sessions.TryGet(connection, out var afterSecondHit));
        Assert.NotNull(afterSecondHit);

        var score = Assert.Single(
            ReadCaptureTheFlagBaseScores(afterSecondHit, log.ToString()),
            entry => entry.BaseUid == checked((uint)sobNpc.Id) && entry.GuildId == 77);
        Assert.Equal("SourceGuild", score.GuildName);
        Assert.Equal((long)firstHit.Query.Damage + secondHit.Query.Damage, (long)score.Score);

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("EnemyBase", visibleSobNpc.Name);
        Assert.Equal(
            sobNpc.Life - checked((int)firstHit.Query.Damage) - checked((int)secondHit.Query.Damage),
            visibleSobNpc.Life);
        Assert.Equal("9001,206,230,88,0,1", afterSecondHit.Character.Attributes["CaptureTheFlagBases"]);
        Assert.DoesNotContain(
            ParseCaptureTheFlagUpdates(sent),
            packet => packet.Mode == LegacyCaptureTheFlagUpdateMode.OccupiedBase);
        var output = log.ToString();
        Assert.Contains("WorldServer updated legacy CaptureTheFlag base score", output);
        Assert.Contains("source MsgCaptureTheFlag.Basse.UpdateScore modeled", output);
        Assert.DoesNotContain("WorldServer sent legacy CaptureTheFlag base capture", output);
        Assert.DoesNotContain("source MsgCaptureTheFlag.UpdateFlagScore killed base branch", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackSuperGuildWarPoleNonLethalHitRecordsGuildScoreWithoutCapture()
    {
        WorldPacketHandlers.ResetLegacySuperGuildWarStateForTests();
        var sobNpc = CreateSobNpc(
            id: LegacySobNpcEventRules.SuperGuildWarPoleUid,
            name: "Poll",
            kind: LegacySobNpcEventRules.PoleKind,
            mapId: LegacySobNpcEventRules.SuperGuildWarMapId,
            x: PlayerX + 1,
            y: PlayerY,
            life: 50_000,
            maxLife: 50_000);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.SuperGuildWarMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.SuperGuildWarMapId,
            x: PlayerX,
            y: PlayerY,
            strength: 1_000,
            extraCharacterAttributes: CreateSuperGuildWarPoleAttributes());
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange((ulong)directHit.Query.Damage, 1UL, checked((ulong)(sobNpc.Life - 1)));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("Poll", visibleSobNpc.Name);
        Assert.Equal(sobNpc.Life - checked((int)directHit.Query.Damage), visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);
        Assert.Empty(ParseSobNpcSpawns(sent));
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);

        var score = Assert.Single(
            ReadSuperGuildWarScores(updatedSession, log.ToString()),
            entry => entry.GuildId == 77);
        Assert.Equal("SourceGuild", score.GuildName);
        Assert.Equal((long)directHit.Query.Damage, (long)score.Score);

        var rightCornerMessage = Assert.Single(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == FirstRightCornerChatType &&
                packet.Content.Any(content => content.StartsWith("No ", StringComparison.Ordinal)));
        Assert.Contains($"No 1. SourceGuild ({score.Score})", rightCornerMessage.Content);

        var output = log.ToString();
        Assert.Contains("WorldServer updated legacy SuperGuildWar pole score", output);
        Assert.Contains("source MsgSuperGuildWar.UpdateScore modeled", output);
        Assert.DoesNotContain("SuperGuildWar capture", output);
        Assert.DoesNotContain("SuperGuildWar reset", output);
        Assert.DoesNotContain("SuperGuildWar ended", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackSuperGuildWarPoleLethalHitFinishesRoundAndResetsPoleToWinner()
    {
        WorldPacketHandlers.ResetLegacySuperGuildWarStateForTests();
        const int remainingPoleHitPoints = 1_250;
        const int maxPoleHitPoints = 50_000;
        var sobNpc = CreateSobNpc(
            id: LegacySobNpcEventRules.SuperGuildWarPoleUid,
            name: "Poll",
            kind: LegacySobNpcEventRules.PoleKind,
            mapId: LegacySobNpcEventRules.SuperGuildWarMapId,
            x: PlayerX + 1,
            y: PlayerY,
            life: remainingPoleHitPoints,
            maxLife: maxPoleHitPoints);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 50_000, maxAttack: 50_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.SuperGuildWarMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var attributes = CreateSuperGuildWarPoleAttributes();
        SetSuperGuildWarAttributeTriplets(attributes, remainingPoleHitPoints, winnerGuildId: 777);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.SuperGuildWarMapId,
            x: PlayerX,
            y: PlayerY,
            strength: 50_000,
            extraCharacterAttributes: attributes);
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 50_000)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.Equal(remainingPoleHitPoints, directHit.Query.Damage);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);

        Assert.False(
            updatedSession.Character.Attributes.ContainsKey("SuperGuildWarScores"),
            "Expected MsgSuperGuildWar.FinishRound to clear the active score list.");
        Assert.Equal("77", updatedSession.Character.Attributes["SuperGuildWarWinnerGuildID"]);
        Assert.Equal("77", updatedSession.Character.Attributes["SuperGuildWarRoundOwnerGuildID"]);
        Assert.Equal("SourceGuild", updatedSession.Character.Attributes["SuperGuildWarWinnerGuildName"]);
        Assert.Equal("SourceGuild", updatedSession.Character.Attributes["SuperGuildWarPoleOwnerName"]);
        Assert.Equal("Idle", updatedSession.Character.Attributes["SuperGuildWarProcess"]);
        Assert.Equal("3", updatedSession.Character.Attributes["SuperGuildWarStampRoundSeconds"]);
        Assert.Equal(
            remainingPoleHitPoints.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes["SuperGuildWarWinnerScore"]);

        var winMessage = $"Congratulations to SourceGuild, they've won the Super Guild War with a score of {remainingPoleHitPoints}";
        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == FirstRightCornerChatType &&
                packet.Content.Contains($"No 1. SourceGuild ({remainingPoleHitPoints})"));
        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.WhiteColor &&
                packet.ChatType == LegacyMsgMessagePacket.TalkChatType &&
                packet.Content.Contains(winMessage));
        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.RedColor &&
                packet.ChatType == LegacyMsgMessagePacket.CenterChatType &&
                packet.Content.Contains(winMessage));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("SourceGuild", visibleSobNpc.Name);
        Assert.Equal(maxPoleHitPoints, visibleSobNpc.Life);
        Assert.Equal(maxPoleHitPoints, visibleSobNpc.MaxLife);
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);

        var output = log.ToString();
        Assert.Contains("source MsgSuperGuildWar.UpdateScore modeled", output);
        Assert.Contains("source MsgSuperGuildWar.FinishRound modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackSuperGuildWarPoleFollowUpHitAccumulatesGuildScore()
    {
        WorldPacketHandlers.ResetLegacySuperGuildWarStateForTests();
        var sobNpc = CreateSobNpc(
            id: LegacySobNpcEventRules.SuperGuildWarPoleUid,
            name: "Poll",
            kind: LegacySobNpcEventRules.PoleKind,
            mapId: LegacySobNpcEventRules.SuperGuildWarMapId,
            x: PlayerX + 1,
            y: PlayerY,
            life: 50_000,
            maxLife: 50_000);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.SuperGuildWarMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.SuperGuildWarMapId,
            x: PlayerX,
            y: PlayerY,
            strength: 1_000,
            extraCharacterAttributes: CreateSuperGuildWarPoleAttributes());
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));
        var firstHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange((ulong)firstHit.Query.Damage, 1UL, checked((ulong)(sobNpc.Life - 1)));
        Assert.True(sessions.TryGet(connection, out var afterFirstHit));
        Assert.NotNull(afterFirstHit);

        sent.Clear();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                afterFirstHit,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));

        var secondHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange(
            (ulong)secondHit.Query.Damage,
            1UL,
            checked((ulong)(sobNpc.Life - checked((int)firstHit.Query.Damage) - 1)));
        Assert.True(sessions.TryGet(connection, out var afterSecondHit));
        Assert.NotNull(afterSecondHit);

        var score = Assert.Single(
            ReadSuperGuildWarScores(afterSecondHit, log.ToString()),
            entry => entry.GuildId == 77);
        Assert.Equal("SourceGuild", score.GuildName);
        Assert.Equal((long)firstHit.Query.Damage + secondHit.Query.Damage, (long)score.Score);

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("Poll", visibleSobNpc.Name);
        Assert.Equal(
            sobNpc.Life - checked((int)firstHit.Query.Damage) - checked((int)secondHit.Query.Damage),
            visibleSobNpc.Life);
        Assert.Empty(ParseSobNpcSpawns(sent));
        Assert.Empty(ParseNewSynWarPackets(sent));
        var output = log.ToString();
        Assert.Contains("WorldServer updated legacy SuperGuildWar pole score", output);
        Assert.Contains("source MsgSuperGuildWar.UpdateScore modeled", output);
        Assert.DoesNotContain("SuperGuildWar capture", output);
        Assert.DoesNotContain("SuperGuildWar reset", output);
        Assert.DoesNotContain("SuperGuildWar ended", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackSuperGuildWarPoleSecondGuildBroadcastsScoresInDescendingOrder()
    {
        WorldPacketHandlers.ResetLegacySuperGuildWarStateForTests();
        var sobNpc = CreateSobNpc(
            id: LegacySobNpcEventRules.SuperGuildWarPoleUid,
            name: "Poll",
            kind: LegacySobNpcEventRules.PoleKind,
            mapId: LegacySobNpcEventRules.SuperGuildWarMapId,
            x: PlayerX + 1,
            y: PlayerY,
            life: 50_000,
            maxLife: 50_000);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.SuperGuildWarMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sourceSent = new List<byte[]>();
        var rivalSent = new List<byte[]>();
        using var log = new StringWriter();
        var sourceConnection = CreateSendingConnection(sourceSent);
        var sourceSession = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.SuperGuildWarMapId,
            x: PlayerX,
            y: PlayerY,
            strength: 1_000,
            extraCharacterAttributes: CreateSuperGuildWarPoleAttributes());
        sessions.Set(sourceConnection, sourceSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                sourceConnection,
                log,
                sourceSession,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));
        var sourceFirstHit = Assert.Single(
            ParseAttacks(sourceSent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(sessions.TryGet(sourceConnection, out var afterSourceFirstHit));
        Assert.NotNull(afterSourceFirstHit);

        sourceSent.Clear();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                sourceConnection,
                log,
                afterSourceFirstHit,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));
        var sourceSecondHit = Assert.Single(
            ParseAttacks(sourceSent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(sessions.TryGet(sourceConnection, out var afterSourceSecondHit));
        Assert.NotNull(afterSourceSecondHit);

        const uint rivalPlayerUid = PlayerUid + 1;
        var rivalConnection = CreateSendingConnection(rivalSent);
        var rivalAttributes = CreateSuperGuildWarPoleAttributes();
        rivalAttributes["GuildID"] = "88";
        rivalAttributes["GuildName"] = "RivalGuild";
        var rivalSession = CreateSession(
            spellIds: [],
            playerUid: rivalPlayerUid,
            mapId: LegacySobNpcEventRules.SuperGuildWarMapId,
            x: PlayerX,
            y: PlayerY,
            strength: 1_000,
            extraCharacterAttributes: rivalAttributes);
        sessions.Set(rivalConnection, rivalSession);

        sourceSent.Clear();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                rivalConnection,
                log,
                rivalSession,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000,
                playerUid: rivalPlayerUid)).ToFrameBytes()));

        var rivalHit = Assert.Single(
            ParseAttacks(rivalSent),
            attack => attack.Query.Uid == rivalPlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(sessions.TryGet(rivalConnection, out var afterRivalHit));
        Assert.NotNull(afterRivalHit);

        var scores = ReadSuperGuildWarScores(afterRivalHit, log.ToString());
        var sourceScore = Assert.Single(scores, entry => entry.GuildId == 77);
        var rivalScore = Assert.Single(scores, entry => entry.GuildId == 88);
        Assert.Equal("SourceGuild", sourceScore.GuildName);
        Assert.Equal("RivalGuild", rivalScore.GuildName);
        Assert.Equal((long)sourceFirstHit.Query.Damage + sourceSecondHit.Query.Damage, (long)sourceScore.Score);
        Assert.Equal((long)rivalHit.Query.Damage, (long)rivalScore.Score);
        Assert.True(sourceScore.Score > rivalScore.Score);

        AssertSuperGuildWarRightCornerScoresInDescendingOrder(sourceSent, sourceScore, rivalScore);
        AssertSuperGuildWarRightCornerScoresInDescendingOrder(rivalSent, sourceScore, rivalScore);

        var output = log.ToString();
        Assert.Contains("WorldServer updated legacy SuperGuildWar pole score", output);
        Assert.Contains("source MsgSuperGuildWar.UpdateScore modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackEliteGuildWarPoleNonLethalHitRecordsGuildScoreWithoutDeathResetOrCapture()
    {
        WorldPacketHandlers.ResetLegacyEliteGuildWarScoreStateForTests();
        var sobNpc = CreateEliteGuildWarPoleSobNpc(life: 50_000, maxLife: 50_000);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.EliteGuildWarMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.EliteGuildWarMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: CreateEliteGuildWarPoleAttributes());
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange((ulong)directHit.Query.Damage, 1UL, checked((ulong)(sobNpc.Life - 1)));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("EliteGuildWar", visibleSobNpc.Name);
        Assert.Equal(sobNpc.Life - checked((int)directHit.Query.Damage), visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);
        Assert.Empty(ParseSobNpcSpawns(sent));
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);

        var score = Assert.Single(
            ReadEliteGuildWarScores(updatedSession, log.ToString()),
            entry => entry.GuildId == 77);
        Assert.Equal("SourceGuild", score.GuildName);
        Assert.Equal((long)directHit.Query.Damage, (long)score.Score);

        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == FirstRightCornerChatType &&
                packet.Content.Contains($"No 1. SourceGuild ({directHit.Query.Damage})"));

        var output = log.ToString();
        Assert.Contains("WorldServer updated legacy EliteGuildWar pole score", output);
        Assert.Contains("source MsgEliteGuildWar.UpdateScore modeled", output);
        Assert.DoesNotContain("EliteGuildWar capture", output);
        Assert.DoesNotContain("EliteGuildWar reset", output);
        Assert.DoesNotContain("EliteGuildWar ended", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackEliteGuildWarPoleLethalHitFinishesRoundAndResetsPoleToWinner()
    {
        WorldPacketHandlers.ResetLegacyEliteGuildWarScoreStateForTests();
        const int remainingPoleHitPoints = 1_250;
        const int maxPoleHitPoints = 50_000;
        var sobNpc = CreateEliteGuildWarPoleSobNpc(life: remainingPoleHitPoints, maxLife: maxPoleHitPoints);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 50_000, maxAttack: 50_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.EliteGuildWarMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var attributes = CreateEliteGuildWarPoleAttributes();
        SetEliteGuildWarPoleHitPointsAttributePairs(attributes, remainingPoleHitPoints);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.EliteGuildWarMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 50_000,
            extraCharacterAttributes: attributes);
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 50_000)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.Equal(remainingPoleHitPoints, directHit.Query.Damage);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);

        Assert.False(
            updatedSession.Character.Attributes.ContainsKey("EliteGuildWarScores"),
            "Expected MsgEliteGuildWar.FinishRound Reset to clear current score table.");
        Assert.Equal("77", updatedSession.Character.Attributes["EliteGuildWarRoundOwnerGuildID"]);
        Assert.Equal("SourceGuild", updatedSession.Character.Attributes["EliteGuildWarRoundOwnerGuildName"]);
        Assert.Equal(
            remainingPoleHitPoints.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes["EliteGuildWarRoundOwnerScore"]);
        Assert.Equal("SourceGuild", updatedSession.Character.Attributes["EliteGuildWarPoleOwnerName"]);
        Assert.Equal("true", updatedSession.Character.Attributes["EliteGuildWarPoleReset"]);

        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == FirstRightCornerChatType &&
                packet.Content.Contains($"No 1. SourceGuild ({remainingPoleHitPoints})"));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("SourceGuild", visibleSobNpc.Name);
        Assert.Equal(maxPoleHitPoints, visibleSobNpc.Life);
        Assert.Equal(maxPoleHitPoints, visibleSobNpc.MaxLife);
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);

        var output = log.ToString();
        Assert.Contains("source MsgEliteGuildWar.UpdateScore modeled", output);
        Assert.Contains("source MsgEliteGuildWar.FinishRound modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackEliteGuildWarPoleFollowUpHitAccumulatesGuildScore()
    {
        WorldPacketHandlers.ResetLegacyEliteGuildWarScoreStateForTests();
        var sobNpc = CreateEliteGuildWarPoleSobNpc(life: 50_000, maxLife: 50_000);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.EliteGuildWarMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.EliteGuildWarMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: CreateEliteGuildWarPoleAttributes());
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));
        var firstHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange((ulong)firstHit.Query.Damage, 1UL, checked((ulong)(sobNpc.Life - 1)));
        Assert.True(sessions.TryGet(connection, out var afterFirstHit));
        Assert.NotNull(afterFirstHit);

        var firstScore = Assert.Single(
            ReadEliteGuildWarScores(afterFirstHit, log.ToString()),
            entry => entry.GuildId == 77);
        Assert.Equal("SourceGuild", firstScore.GuildName);
        Assert.Equal((long)firstHit.Query.Damage, (long)firstScore.Score);

        sent.Clear();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                afterFirstHit,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));

        var secondHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange(
            (ulong)secondHit.Query.Damage,
            1UL,
            checked((ulong)(sobNpc.Life - checked((int)firstHit.Query.Damage) - 1)));
        Assert.True(sessions.TryGet(connection, out var afterSecondHit));
        Assert.NotNull(afterSecondHit);

        var score = Assert.Single(
            ReadEliteGuildWarScores(afterSecondHit, log.ToString()),
            entry => entry.GuildId == 77);
        Assert.Equal("SourceGuild", score.GuildName);
        Assert.Equal((long)firstHit.Query.Damage + secondHit.Query.Damage, (long)score.Score);

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("EliteGuildWar", visibleSobNpc.Name);
        Assert.Equal(
            sobNpc.Life - checked((int)firstHit.Query.Damage) - checked((int)secondHit.Query.Damage),
            visibleSobNpc.Life);
        Assert.Empty(ParseSobNpcSpawns(sent));
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);
        var output = log.ToString();
        Assert.Contains("WorldServer updated legacy EliteGuildWar pole score", output);
        Assert.Contains("source MsgEliteGuildWar.UpdateScore modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackEliteGuildWarPoleSecondGuildBroadcastsScoresInDescendingOrder()
    {
        WorldPacketHandlers.ResetLegacyEliteGuildWarScoreStateForTests();
        var sobNpc = CreateEliteGuildWarPoleSobNpc(life: 50_000, maxLife: 50_000);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.EliteGuildWarMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sourceSent = new List<byte[]>();
        var rivalSent = new List<byte[]>();
        using var log = new StringWriter();
        var sourceConnection = CreateSendingConnection(sourceSent);
        var sourceSession = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.EliteGuildWarMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: CreateEliteGuildWarPoleAttributes());
        sessions.Set(sourceConnection, sourceSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                sourceConnection,
                log,
                sourceSession,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));
        var sourceFirstHit = Assert.Single(
            ParseAttacks(sourceSent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(sessions.TryGet(sourceConnection, out var afterSourceFirstHit));
        Assert.NotNull(afterSourceFirstHit);

        sourceSent.Clear();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                sourceConnection,
                log,
                afterSourceFirstHit,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));
        var sourceSecondHit = Assert.Single(
            ParseAttacks(sourceSent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(sessions.TryGet(sourceConnection, out var afterSourceSecondHit));
        Assert.NotNull(afterSourceSecondHit);

        const uint rivalPlayerUid = PlayerUid + 1;
        var rivalConnection = CreateSendingConnection(rivalSent);
        var rivalAttributes = CreateEliteGuildWarPoleAttributes();
        rivalAttributes["GuildID"] = "88";
        rivalAttributes["GuildId"] = "88";
        rivalAttributes["GuildName"] = "RivalGuild";
        rivalAttributes["LegacyGuildName"] = "RivalGuild";
        var rivalSession = CreateSession(
            spellIds: [],
            playerUid: rivalPlayerUid,
            mapId: LegacySobNpcEventRules.EliteGuildWarMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: rivalAttributes);
        sessions.Set(rivalConnection, rivalSession);

        sourceSent.Clear();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                rivalConnection,
                log,
                rivalSession,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000,
                playerUid: rivalPlayerUid)).ToFrameBytes()));

        var rivalHit = Assert.Single(
            ParseAttacks(rivalSent),
            attack => attack.Query.Uid == rivalPlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(sessions.TryGet(rivalConnection, out var afterRivalHit));
        Assert.NotNull(afterRivalHit);

        var scores = ReadEliteGuildWarScores(afterRivalHit, log.ToString());
        var sourceScore = Assert.Single(scores, entry => entry.GuildId == 77);
        var rivalScore = Assert.Single(scores, entry => entry.GuildId == 88);
        Assert.Equal("SourceGuild", sourceScore.GuildName);
        Assert.Equal("RivalGuild", rivalScore.GuildName);
        Assert.Equal((long)sourceFirstHit.Query.Damage + sourceSecondHit.Query.Damage, (long)sourceScore.Score);
        Assert.Equal((long)rivalHit.Query.Damage, (long)rivalScore.Score);
        Assert.True(sourceScore.Score > rivalScore.Score);

        AssertEliteGuildWarRightCornerScoresInDescendingOrder(sourceSent, sourceScore, rivalScore);
        AssertEliteGuildWarRightCornerScoresInDescendingOrder(rivalSent, sourceScore, rivalScore);

        var output = log.ToString();
        Assert.Contains("WorldServer updated legacy EliteGuildWar pole score", output);
        Assert.Contains("source MsgEliteGuildWar.UpdateScore modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackChampionsOfWarrPoleNonLethalHitRecordsGuildScoreWithoutDeathResetOrCapture()
    {
        WorldPacketHandlers.ResetLegacyChampionsOfWarrScoreStateForTests();
        var sobNpc = CreateChampionsOfWarrPoleSobNpc(life: 250_000_000, maxLife: 250_000_000);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.ChampionsOfWarrMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.ChampionsOfWarrMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: CreateChampionsOfWarrPoleAttributes());
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange((ulong)directHit.Query.Damage, 1UL, checked((ulong)(sobNpc.Life - 1)));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("Champions", visibleSobNpc.Name);
        Assert.Equal(sobNpc.Life - checked((int)directHit.Query.Damage), visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);
        Assert.Empty(ParseSobNpcSpawns(sent));
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseMsgUpdates(sent), packet => packet.Uid == checked((uint)sobNpc.Id));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);

        var score = Assert.Single(
            ReadChampionsOfWarrScores(updatedSession, log.ToString()),
            entry => entry.GuildId == 77);
        Assert.Equal("SourceGuild", score.GuildName);
        Assert.Equal((long)directHit.Query.Damage, (long)score.Score);

        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == FirstRightCornerChatType &&
                packet.Content.Contains($"No 1. SourceGuild ({directHit.Query.Damage})"));

        var output = log.ToString();
        Assert.Contains("source MsgChampionsOfWarr.UpdateScore modeled", output);
        Assert.DoesNotContain("ChampionsOfWarr capture", output);
        Assert.DoesNotContain("ChampionsOfWarr reset", output);
        Assert.DoesNotContain("ChampionsOfWarr ended", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackChampionsOfWarrPoleLethalHitFinishesRoundAndResetsPoleToWinner()
    {
        WorldPacketHandlers.ResetLegacyChampionsOfWarrScoreStateForTests();
        const int remainingPoleHitPoints = 1_250;
        const int maxPoleHitPoints = 250_000_000;
        var sobNpc = CreateChampionsOfWarrPoleSobNpc(life: remainingPoleHitPoints, maxLife: maxPoleHitPoints);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 50_000, maxAttack: 50_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.ChampionsOfWarrMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var attributes = CreateChampionsOfWarrPoleAttributes();
        SetChampionsOfWarrPoleHitPointsAttributePairs(attributes, remainingPoleHitPoints);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.ChampionsOfWarrMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 50_000,
            extraCharacterAttributes: attributes);
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 50_000)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.Equal(remainingPoleHitPoints, directHit.Query.Damage);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);

        Assert.False(
            updatedSession.Character.Attributes.ContainsKey("ChampionsOfWarrScores"),
            "Expected MsgChampionsOfWarr.FinishRound Reset to clear current score table.");
        Assert.Equal("77", updatedSession.Character.Attributes["ChampionsOfWarrRoundOwnerGuildID"]);
        Assert.Equal("SourceGuild", updatedSession.Character.Attributes["ChampionsOfWarrRoundOwnerGuildName"]);
        Assert.Equal(remainingPoleHitPoints.ToString(CultureInfo.InvariantCulture), updatedSession.Character.Attributes["ChampionsOfWarrRoundOwnerScore"]);
        Assert.Equal("SourceGuild", updatedSession.Character.Attributes["ChampionsOfWarrPoleOwnerName"]);
        Assert.Equal("true", updatedSession.Character.Attributes["ChampionsOfWarrPoleReset"]);

        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == FirstRightCornerChatType &&
                packet.Content.Contains($"No 1. SourceGuild ({remainingPoleHitPoints})"));
        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == LegacyMsgMessagePacket.CenterChatType &&
                packet.Content.Contains("The Round Ownered by guild SourceGuild."));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("SourceGuild", visibleSobNpc.Name);
        Assert.Equal(maxPoleHitPoints, visibleSobNpc.Life);
        Assert.Equal(maxPoleHitPoints, visibleSobNpc.MaxLife);
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);

        var output = log.ToString();
        Assert.Contains("source MsgChampionsOfWarr.UpdateScore modeled", output);
        Assert.Contains("source MsgChampionsOfWarr.FinishRound modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackChampionsOfWarrPoleFollowUpHitAccumulatesGuildScore()
    {
        WorldPacketHandlers.ResetLegacyChampionsOfWarrScoreStateForTests();
        var sobNpc = CreateChampionsOfWarrPoleSobNpc(life: 250_000_000, maxLife: 250_000_000);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.ChampionsOfWarrMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.ChampionsOfWarrMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: CreateChampionsOfWarrPoleAttributes());
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));
        var firstHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange((ulong)firstHit.Query.Damage, 1UL, checked((ulong)(sobNpc.Life - 1)));
        Assert.True(sessions.TryGet(connection, out var afterFirstHit));
        Assert.NotNull(afterFirstHit);

        var firstScore = Assert.Single(
            ReadChampionsOfWarrScores(afterFirstHit, log.ToString()),
            entry => entry.GuildId == 77);
        Assert.Equal("SourceGuild", firstScore.GuildName);
        Assert.Equal((long)firstHit.Query.Damage, (long)firstScore.Score);

        sent.Clear();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                afterFirstHit,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));

        var secondHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange(
            (ulong)secondHit.Query.Damage,
            1UL,
            checked((ulong)(sobNpc.Life - checked((int)firstHit.Query.Damage) - 1)));
        Assert.True(sessions.TryGet(connection, out var afterSecondHit));
        Assert.NotNull(afterSecondHit);

        var score = Assert.Single(
            ReadChampionsOfWarrScores(afterSecondHit, log.ToString()),
            entry => entry.GuildId == 77);
        Assert.Equal("SourceGuild", score.GuildName);
        Assert.Equal((long)firstHit.Query.Damage + secondHit.Query.Damage, (long)score.Score);

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("Champions", visibleSobNpc.Name);
        Assert.Equal(
            sobNpc.Life - checked((int)firstHit.Query.Damage) - checked((int)secondHit.Query.Damage),
            visibleSobNpc.Life);
        Assert.Empty(ParseSobNpcSpawns(sent));
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseMsgUpdates(sent), packet => packet.Uid == checked((uint)sobNpc.Id));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);
        var output = log.ToString();
        Assert.Contains("source MsgChampionsOfWarr.UpdateScore modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackChampionsOfWarrPoleSecondGuildBroadcastsScoresInDescendingOrder()
    {
        WorldPacketHandlers.ResetLegacyChampionsOfWarrScoreStateForTests();
        var sobNpc = CreateChampionsOfWarrPoleSobNpc(life: 250_000_000, maxLife: 250_000_000);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.ChampionsOfWarrMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sourceSent = new List<byte[]>();
        var rivalSent = new List<byte[]>();
        using var log = new StringWriter();
        var sourceConnection = CreateSendingConnection(sourceSent);
        var sourceSession = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.ChampionsOfWarrMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: CreateChampionsOfWarrPoleAttributes());
        sessions.Set(sourceConnection, sourceSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                sourceConnection,
                log,
                sourceSession,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));
        var sourceFirstHit = Assert.Single(
            ParseAttacks(sourceSent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(sessions.TryGet(sourceConnection, out var afterSourceFirstHit));
        Assert.NotNull(afterSourceFirstHit);

        sourceSent.Clear();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                sourceConnection,
                log,
                afterSourceFirstHit,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));
        var sourceSecondHit = Assert.Single(
            ParseAttacks(sourceSent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(sessions.TryGet(sourceConnection, out var afterSourceSecondHit));
        Assert.NotNull(afterSourceSecondHit);

        const uint rivalPlayerUid = PlayerUid + 1;
        var rivalConnection = CreateSendingConnection(rivalSent);
        var rivalAttributes = CreateChampionsOfWarrPoleAttributes();
        rivalAttributes["GuildID"] = "88";
        rivalAttributes["GuildId"] = "88";
        rivalAttributes["GuildName"] = "RivalGuild";
        rivalAttributes["LegacyGuildName"] = "RivalGuild";
        var rivalSession = CreateSession(
            spellIds: [],
            playerUid: rivalPlayerUid,
            mapId: LegacySobNpcEventRules.ChampionsOfWarrMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: rivalAttributes);
        sessions.Set(rivalConnection, rivalSession);

        sourceSent.Clear();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                rivalConnection,
                log,
                rivalSession,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000,
                playerUid: rivalPlayerUid)).ToFrameBytes()));

        var rivalHit = Assert.Single(
            ParseAttacks(rivalSent),
            attack => attack.Query.Uid == rivalPlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(sessions.TryGet(rivalConnection, out var afterRivalHit));
        Assert.NotNull(afterRivalHit);

        var scores = ReadChampionsOfWarrScores(afterRivalHit, log.ToString());
        var sourceScore = Assert.Single(scores, entry => entry.GuildId == 77);
        var rivalScore = Assert.Single(scores, entry => entry.GuildId == 88);
        Assert.Equal("SourceGuild", sourceScore.GuildName);
        Assert.Equal("RivalGuild", rivalScore.GuildName);
        Assert.Equal((long)sourceFirstHit.Query.Damage + sourceSecondHit.Query.Damage, (long)sourceScore.Score);
        Assert.Equal((long)rivalHit.Query.Damage, (long)rivalScore.Score);
        Assert.True(sourceScore.Score > rivalScore.Score);

        AssertChampionsOfWarrRightCornerScoresInDescendingOrder(sourceSent, sourceScore, rivalScore);
        AssertChampionsOfWarrRightCornerScoresInDescendingOrder(rivalSent, sourceScore, rivalScore);

        var output = log.ToString();
        Assert.Contains("source MsgChampionsOfWarr.UpdateScore modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackUnionWarPoleNonLethalHitRecordsUnionScoreWithoutDeathResetOrCapture()
    {
        WorldPacketHandlers.ResetLegacyUnionWarScoreStateForTests();
        var sobNpc = CreateUnionWarPoleSobNpc();
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [UnionWarPoleMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [],
            mapId: UnionWarPoleMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: CreateUnionWarPoleAttributes());
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange((ulong)directHit.Query.Damage, 1UL, checked((ulong)(sobNpc.Life - 1)));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("UnionWar", visibleSobNpc.Name);
        Assert.Equal(sobNpc.Life - checked((int)directHit.Query.Damage), visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);
        Assert.Empty(ParseSobNpcSpawns(sent));
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseMsgUpdates(sent), packet => packet.Uid == checked((uint)sobNpc.Id));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);

        var score = Assert.Single(
            ReadUnionWarScores(updatedSession, log.ToString()),
            entry => entry.UnionId == 42);
        Assert.Equal("SourceUnion", score.UnionName);
        Assert.Equal((long)directHit.Query.Damage, (long)score.Score);

        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == FirstRightCornerChatType &&
                packet.Content.Contains($"No 1. SourceUnion ({directHit.Query.Damage})"));

        var output = log.ToString();
        Assert.Contains("source MsgUnionWar.UpdateScore modeled", output);
        Assert.DoesNotContain("UnionWar capture", output);
        Assert.DoesNotContain("UnionWar reset", output);
        Assert.DoesNotContain("UnionWar ended", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackUnionWarPoleLethalHitFinishesRoundAndResetsPoleToWinner()
    {
        WorldPacketHandlers.ResetLegacyUnionWarScoreStateForTests();
        const int remainingPoleHitPoints = 1_250;
        var sobNpc = CreateUnionWarPoleSobNpc(life: remainingPoleHitPoints, maxLife: UnionWarPoleLife);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 50_000, maxAttack: 50_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [UnionWarPoleMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var attributes = CreateUnionWarPoleAttributes();
        SetUnionWarPoleHitPointsAttributePairs(attributes, remainingPoleHitPoints);
        var session = CreateSession(
            spellIds: [],
            mapId: UnionWarPoleMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 50_000,
            extraCharacterAttributes: attributes);
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 50_000)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.Equal(remainingPoleHitPoints, directHit.Query.Damage);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);

        Assert.False(
            updatedSession.Character.Attributes.ContainsKey("UnionWarScores"),
            "Expected MsgUnionWar.FinishRound to clear current ScoreList after shuffling scores.");
        Assert.Equal("42", updatedSession.Character.Attributes["UnionWarWinnerUnionID"]);
        Assert.Equal("SourceUnion", updatedSession.Character.Attributes["UnionWarWinnerUnionName"]);
        Assert.Equal(remainingPoleHitPoints.ToString(CultureInfo.InvariantCulture), updatedSession.Character.Attributes["UnionWarWinnerScore"]);
        Assert.Equal("SourceUnion", updatedSession.Character.Attributes["UnionWarPoleOwnerName"]);
        Assert.Equal("true", updatedSession.Character.Attributes["UnionWarPoleReset"]);

        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == FirstRightCornerChatType &&
                packet.Content.Contains($"No 1. SourceUnion ({remainingPoleHitPoints})"));
        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.WhiteColor &&
                packet.ChatType == LegacyMsgMessagePacket.TalkChatType &&
                packet.Content.Contains($"Congratulations to SourceUnion,they've won the UnionWar round with a score of  {remainingPoleHitPoints}"));
        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.RedColor &&
                packet.ChatType == LegacyMsgMessagePacket.CenterChatType &&
                packet.Content.Contains($"Congratulations to SourceUnion, they've won the UnionWar round with a score of  {remainingPoleHitPoints}"));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("SourceUnion", visibleSobNpc.Name);
        Assert.Equal(UnionWarPoleLife, visibleSobNpc.Life);
        Assert.Equal(UnionWarPoleLife, visibleSobNpc.MaxLife);
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);

        var output = log.ToString();
        Assert.Contains("source MsgUnionWar.UpdateScore modeled", output);
        Assert.Contains("source MsgUnionWar.FinishRound modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackUnionWarPoleFollowUpSameUnionHitAccumulatesUnionScore()
    {
        WorldPacketHandlers.ResetLegacyUnionWarScoreStateForTests();
        var sobNpc = CreateUnionWarPoleSobNpc();
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [UnionWarPoleMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [],
            mapId: UnionWarPoleMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: CreateUnionWarPoleAttributes());
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));
        var firstHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange((ulong)firstHit.Query.Damage, 1UL, checked((ulong)(sobNpc.Life - 1)));
        Assert.True(sessions.TryGet(connection, out var afterFirstHit));
        Assert.NotNull(afterFirstHit);

        var firstScore = Assert.Single(
            ReadUnionWarScores(afterFirstHit, log.ToString()),
            entry => entry.UnionId == 42);
        Assert.Equal("SourceUnion", firstScore.UnionName);
        Assert.Equal((long)firstHit.Query.Damage, (long)firstScore.Score);

        sent.Clear();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                afterFirstHit,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));

        var secondHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange(
            (ulong)secondHit.Query.Damage,
            1UL,
            checked((ulong)(sobNpc.Life - checked((int)firstHit.Query.Damage) - 1)));
        Assert.True(sessions.TryGet(connection, out var afterSecondHit));
        Assert.NotNull(afterSecondHit);

        var score = Assert.Single(
            ReadUnionWarScores(afterSecondHit, log.ToString()),
            entry => entry.UnionId == 42);
        Assert.Equal("SourceUnion", score.UnionName);
        Assert.Equal((long)firstHit.Query.Damage + secondHit.Query.Damage, (long)score.Score);

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("UnionWar", visibleSobNpc.Name);
        Assert.Equal(
            sobNpc.Life - checked((int)firstHit.Query.Damage) - checked((int)secondHit.Query.Damage),
            visibleSobNpc.Life);
        Assert.Empty(ParseSobNpcSpawns(sent));
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseMsgUpdates(sent), packet => packet.Uid == checked((uint)sobNpc.Id));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);
        var output = log.ToString();
        Assert.Contains("source MsgUnionWar.UpdateScore modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackUnionWarPoleSecondUnionBroadcastsScoresInDescendingOrder()
    {
        WorldPacketHandlers.ResetLegacyUnionWarScoreStateForTests();
        var sobNpc = CreateUnionWarPoleSobNpc();
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [UnionWarPoleMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sourceSent = new List<byte[]>();
        var rivalSent = new List<byte[]>();
        using var log = new StringWriter();
        var sourceConnection = CreateSendingConnection(sourceSent);
        var sourceSession = CreateSession(
            spellIds: [],
            mapId: UnionWarPoleMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: CreateUnionWarPoleAttributes());
        sessions.Set(sourceConnection, sourceSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                sourceConnection,
                log,
                sourceSession,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));
        var sourceFirstHit = Assert.Single(
            ParseAttacks(sourceSent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(sessions.TryGet(sourceConnection, out var afterSourceFirstHit));
        Assert.NotNull(afterSourceFirstHit);

        sourceSent.Clear();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                sourceConnection,
                log,
                afterSourceFirstHit,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));
        var sourceSecondHit = Assert.Single(
            ParseAttacks(sourceSent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(sessions.TryGet(sourceConnection, out var afterSourceSecondHit));
        Assert.NotNull(afterSourceSecondHit);

        const uint rivalPlayerUid = PlayerUid + 1;
        var rivalConnection = CreateSendingConnection(rivalSent);
        var rivalSession = CreateSession(
            spellIds: [],
            playerUid: rivalPlayerUid,
            mapId: UnionWarPoleMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: CreateUnionWarPoleAttributes(
                unionId: 84,
                unionName: "RivalUnion",
                guildId: 88));
        sessions.Set(rivalConnection, rivalSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                rivalConnection,
                log,
                rivalSession,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000,
                playerUid: rivalPlayerUid)).ToFrameBytes()));

        var rivalHit = Assert.Single(
            ParseAttacks(rivalSent),
            attack => attack.Query.Uid == rivalPlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(sessions.TryGet(rivalConnection, out var afterRivalHit));
        Assert.NotNull(afterRivalHit);

        var scores = ReadUnionWarScores(afterRivalHit, log.ToString());
        var sourceScore = Assert.Single(scores, entry => entry.UnionId == 42);
        var rivalScore = Assert.Single(scores, entry => entry.UnionId == 84);
        Assert.Equal("SourceUnion", sourceScore.UnionName);
        Assert.Equal("RivalUnion", rivalScore.UnionName);
        Assert.Equal((long)sourceFirstHit.Query.Damage + sourceSecondHit.Query.Damage, (long)sourceScore.Score);
        Assert.Equal((long)rivalHit.Query.Damage, (long)rivalScore.Score);
        Assert.True(sourceScore.Score > rivalScore.Score);

        var rightCornerMessages = ParseLegacyMessages(rivalSent)
            .Where(packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                (packet.ChatType == FirstRightCornerChatType || packet.ChatType == ContinueRightCornerChatType) &&
                packet.Content.Any(content => content.StartsWith("No ", StringComparison.Ordinal)))
            .ToArray();
        Assert.Collection(
            rightCornerMessages,
            packet =>
            {
                Assert.Equal(FirstRightCornerChatType, packet.ChatType);
                Assert.Contains($"No 1. SourceUnion ({sourceScore.Score})", packet.Content);
            },
            packet =>
            {
                Assert.Equal(ContinueRightCornerChatType, packet.ChatType);
                Assert.Contains($"No 2. RivalUnion ({rivalScore.Score})", packet.Content);
            });

        var output = log.ToString();
        Assert.Contains("source MsgUnionWar.UpdateScore modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackGuild6PoleWarPoleNonLethalHitRecordsGuildScoreWithoutDeathResetOrCapture()
    {
        WorldPacketHandlers.ResetLegacyGuild6PoleWarScoreStateForTests();
        var sobNpc = CreateGuild6PoleWarPoleSobNpc(
            LegacySobNpcEventRules.Guild6PoleWarPoleUid,
            life: 50_000,
            maxLife: 50_000);
        Assert.True(LegacySobNpcEventRules.TryGetGuild6PoleWarPoleSchedule(
            sobNpc,
            out var scheduleName,
            out var displayName));
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.Guild6PoleWarMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.Guild6PoleWarMapId,
            x: PlayerX,
            y: PlayerY,
            strength: 1_000,
            extraCharacterAttributes: CreateGuild6PoleWarPoleAttributes(scheduleName));
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange((ulong)directHit.Query.Damage, 1UL, checked((ulong)(sobNpc.Life - 1)));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(displayName, visibleSobNpc.Name);
        Assert.Equal(sobNpc.Life - checked((int)directHit.Query.Damage), visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);
        Assert.Empty(ParseSobNpcSpawns(sent));
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);

        var score = Assert.Single(
            ReadGuild6PoleWarScores(updatedSession, log.ToString()),
            entry => entry.ScheduleName == scheduleName && entry.GuildId == 77);
        Assert.Equal("SourceGuild", score.GuildName);
        Assert.Equal((long)directHit.Query.Damage, (long)score.Score);

        var rightCornerMessage = Assert.Single(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == FirstRightCornerChatType &&
                packet.Content.Any(content => content.StartsWith("No ", StringComparison.Ordinal)));
        Assert.Contains($"No 1. SourceGuild ({score.Score})", rightCornerMessage.Content);

        var output = log.ToString();
        Assert.Contains("WorldServer updated legacy Guild6PoleWar pole score", output);
        Assert.Contains("source MsgGuild6PoleWar.UpdateScore modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackGuild6PoleWarPoleLethalHitFinishesRoundAndResetsPoleToWinner()
    {
        WorldPacketHandlers.ResetLegacyGuild6PoleWarScoreStateForTests();
        const int remainingPoleHitPoints = 1_250;
        const int maxPoleHitPoints = 50_000;
        var sobNpc = CreateGuild6PoleWarPoleSobNpc(
            LegacySobNpcEventRules.Guild6PoleWarPoleUid,
            life: remainingPoleHitPoints,
            maxLife: maxPoleHitPoints);
        Assert.True(LegacySobNpcEventRules.TryGetGuild6PoleWarPoleSchedule(
            sobNpc,
            out var scheduleName,
            out _));
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: maxPoleHitPoints, maxAttack: maxPoleHitPoints)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.Guild6PoleWarMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var attributes = CreateGuild6PoleWarPoleAttributes(scheduleName);
        SetGuild6PoleWarScheduleAttributePairs(attributes, scheduleName, "HitPoints", remainingPoleHitPoints);
        SetGuild6PoleWarScheduleAttributePairs(attributes, scheduleName, "Hitpoints", remainingPoleHitPoints);
        SetGuild6PoleWarScheduleAttributePairs(attributes, scheduleName, "PoleHitPoints", remainingPoleHitPoints);
        SetGuild6PoleWarScheduleAttributePairs(attributes, scheduleName, "PoleHitpoints", remainingPoleHitPoints);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.Guild6PoleWarMapId,
            x: PlayerX,
            y: PlayerY,
            strength: maxPoleHitPoints,
            extraCharacterAttributes: attributes);
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: maxPoleHitPoints)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.Equal(remainingPoleHitPoints, directHit.Query.Damage);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);

        Assert.False(
            updatedSession.Character.Attributes.ContainsKey("Guild6PoleWarScores"),
            "Expected Guild6PoleWar FinishRound Reset to clear current schedule scores.");
        Assert.Equal("77", updatedSession.Character.Attributes[scheduleName + "RoundOwnerGuildID"]);
        Assert.Equal("SourceGuild", updatedSession.Character.Attributes[scheduleName + "RoundOwnerGuildName"]);
        Assert.Equal(
            remainingPoleHitPoints.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes[scheduleName + "RoundOwnerScore"]);
        Assert.Equal("SourceGuild", updatedSession.Character.Attributes[scheduleName + "PoleOwnerName"]);
        Assert.Equal("true", updatedSession.Character.Attributes[scheduleName + "PoleReset"]);

        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == FirstRightCornerChatType &&
                packet.Content.Contains($"No 1. SourceGuild ({remainingPoleHitPoints})"));
        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.RedColor &&
                packet.ChatType == LegacyMsgMessagePacket.CenterChatType &&
                packet.Content.Contains("The Round Ownered by guild SourceGuild."));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("SourceGuild", visibleSobNpc.Name);
        Assert.Equal(maxPoleHitPoints, visibleSobNpc.Life);
        Assert.Equal(maxPoleHitPoints, visibleSobNpc.MaxLife);
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);

        var output = log.ToString();
        Assert.Contains("source MsgGuild6PoleWar.UpdateScore modeled", output);
        Assert.Contains("source MsgGuild6PoleWar.FinishRound modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackGuild6PoleWarPoleFollowUpHitAccumulatesGuildScore()
    {
        WorldPacketHandlers.ResetLegacyGuild6PoleWarScoreStateForTests();
        var sobNpc = CreateGuild6PoleWarPoleSobNpc(
            LegacySobNpcEventRules.Guild6PoleWar5PoleUid,
            life: 50_000,
            maxLife: 50_000);
        Assert.True(LegacySobNpcEventRules.TryGetGuild6PoleWarPoleSchedule(
            sobNpc,
            out var scheduleName,
            out var displayName));
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.Guild6PoleWarMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.Guild6PoleWarMapId,
            x: PlayerX,
            y: PlayerY,
            strength: 1_000,
            extraCharacterAttributes: CreateGuild6PoleWarPoleAttributes(scheduleName));
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));
        var firstHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange((ulong)firstHit.Query.Damage, 1UL, checked((ulong)(sobNpc.Life - 1)));
        Assert.True(sessions.TryGet(connection, out var afterFirstHit));
        Assert.NotNull(afterFirstHit);

        sent.Clear();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                afterFirstHit,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));

        var secondHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange(
            (ulong)secondHit.Query.Damage,
            1UL,
            checked((ulong)(sobNpc.Life - checked((int)firstHit.Query.Damage) - 1)));
        Assert.True(sessions.TryGet(connection, out var afterSecondHit));
        Assert.NotNull(afterSecondHit);

        var scores = ReadGuild6PoleWarScores(afterSecondHit, log.ToString());
        var score = Assert.Single(
            scores,
            entry => entry.ScheduleName == scheduleName && entry.GuildId == 77);
        Assert.DoesNotContain(scores, entry => entry.ScheduleName == "Guild6PoleWar");
        Assert.Equal("SourceGuild", score.GuildName);
        Assert.Equal((long)firstHit.Query.Damage + secondHit.Query.Damage, (long)score.Score);

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(displayName, visibleSobNpc.Name);
        Assert.Equal(
            sobNpc.Life - checked((int)firstHit.Query.Damage) - checked((int)secondHit.Query.Damage),
            visibleSobNpc.Life);
        Assert.Empty(ParseSobNpcSpawns(sent));
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);
        var output = log.ToString();
        Assert.Contains("WorldServer updated legacy Guild6PoleWar pole score", output);
        Assert.Contains("source MsgGuild6PoleWar.UpdateScore modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackGuild6PoleWarPoleTwoGuildsBroadcastsDescendingScorePanelForCurrentSchedule()
    {
        WorldPacketHandlers.ResetLegacyGuild6PoleWarScoreStateForTests();
        var currentSobNpc = CreateGuild6PoleWarPoleSobNpc(
            LegacySobNpcEventRules.Guild6PoleWarPoleUid,
            life: 50_000,
            maxLife: 50_000);
        var otherSobNpc = CreateGuild6PoleWarPoleSobNpc(
            LegacySobNpcEventRules.Guild6PoleWar2PoleUid,
            life: 50_000,
            maxLife: 50_000);
        Assert.True(LegacySobNpcEventRules.TryGetGuild6PoleWarPoleSchedule(
            currentSobNpc,
            out var currentScheduleName,
            out _));
        Assert.True(LegacySobNpcEventRules.TryGetGuild6PoleWarPoleSchedule(
            otherSobNpc,
            out var otherScheduleName,
            out _));
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [currentSobNpc, otherSobNpc],
            mapIds: [LegacySobNpcEventRules.Guild6PoleWarMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sourceSent = new List<byte[]>();
        var rivalSent = new List<byte[]>();
        using var log = new StringWriter();
        var sourceConnection = CreateSendingConnection(sourceSent);
        var sourceAttributes = CreateGuild6PoleWarPoleAttributes(currentScheduleName);
        foreach (var (key, value) in CreateGuild6PoleWarPoleAttributes(otherScheduleName))
        {
            sourceAttributes[key] = value;
        }

        var sourceSession = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.Guild6PoleWarMapId,
            x: PlayerX,
            y: PlayerY,
            strength: 1_000,
            extraCharacterAttributes: sourceAttributes);
        sessions.Set(sourceConnection, sourceSession);

        const uint rivalPlayerUid = PlayerUid + 1;
        var rivalConnection = CreateSendingConnection(rivalSent);
        var rivalAttributes = CreateGuild6PoleWarPoleAttributes(currentScheduleName);
        rivalAttributes["GuildID"] = "88";
        rivalAttributes["GuildName"] = "RivalGuild";
        var rivalSession = CreateSession(
            spellIds: [],
            playerUid: rivalPlayerUid,
            mapId: LegacySobNpcEventRules.Guild6PoleWarMapId,
            x: PlayerX,
            y: PlayerY,
            strength: 1_000,
            extraCharacterAttributes: rivalAttributes);
        sessions.Set(rivalConnection, rivalSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                sourceConnection,
                log,
                sourceSession,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                otherSobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));
        Assert.True(sessions.TryGet(sourceConnection, out var afterOtherScheduleHit));
        Assert.NotNull(afterOtherScheduleHit);
        sourceSent.Clear();
        rivalSent.Clear();

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                sourceConnection,
                log,
                afterOtherScheduleHit,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                currentSobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));
        var sourceFirstHit = Assert.Single(
            ParseAttacks(sourceSent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)currentSobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(sessions.TryGet(sourceConnection, out var afterSourceFirstHit));
        Assert.NotNull(afterSourceFirstHit);

        sourceSent.Clear();
        rivalSent.Clear();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                sourceConnection,
                log,
                afterSourceFirstHit,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                currentSobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));
        var sourceSecondHit = Assert.Single(
            ParseAttacks(sourceSent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)currentSobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(sessions.TryGet(sourceConnection, out var afterSourceSecondHit));
        Assert.NotNull(afterSourceSecondHit);

        sourceSent.Clear();
        rivalSent.Clear();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                rivalConnection,
                log,
                rivalSession,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                currentSobNpc,
                MsgAttackType.Physical,
                damage: 1_000,
                playerUid: rivalPlayerUid)).ToFrameBytes()));

        var rivalHit = Assert.Single(
            ParseAttacks(rivalSent),
            attack => attack.Query.Uid == rivalPlayerUid &&
                attack.Query.OpponentUid == checked((uint)currentSobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(sessions.TryGet(rivalConnection, out var afterRivalHit));
        Assert.NotNull(afterRivalHit);

        var scores = ReadGuild6PoleWarScores(afterRivalHit, log.ToString());
        var sourceScore = Assert.Single(
            scores,
            entry => entry.ScheduleName == currentScheduleName && entry.GuildId == 77);
        var rivalScore = Assert.Single(
            scores,
            entry => entry.ScheduleName == currentScheduleName && entry.GuildId == 88);
        var otherScheduleScore = Assert.Single(
            scores,
            entry => entry.ScheduleName == otherScheduleName && entry.GuildId == 77);
        Assert.Equal("SourceGuild", sourceScore.GuildName);
        Assert.Equal("RivalGuild", rivalScore.GuildName);
        Assert.Equal("SourceGuild", otherScheduleScore.GuildName);
        Assert.Equal((long)sourceFirstHit.Query.Damage + sourceSecondHit.Query.Damage, (long)sourceScore.Score);
        Assert.Equal((long)rivalHit.Query.Damage, (long)rivalScore.Score);
        Assert.True(sourceScore.Score > rivalScore.Score);

        AssertGuild6PoleWarRightCornerScoresInDescendingOrder(sourceSent, sourceScore, rivalScore);
        AssertGuild6PoleWarRightCornerScoresInDescendingOrder(rivalSent, sourceScore, rivalScore);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackGuildWarScorePoleNonLethalHitRecordsGuildScoreWithoutDeathResetOrCapture()
    {
        WorldPacketHandlers.ResetLegacyGuildWarScoreStateForTests();
        var sobNpc = CreateGuildWarScorePoleSobNpc(life: 50_000, maxLife: 50_000);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.GuildWarScoreMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.GuildWarScoreMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: CreateGuildWarScorePoleAttributes());
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange((ulong)directHit.Query.Damage, 1UL, checked((ulong)(sobNpc.Life - 1)));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("GuildWarSocre", visibleSobNpc.Name);
        Assert.Equal(sobNpc.Life - checked((int)directHit.Query.Damage), visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);
        Assert.Empty(ParseSobNpcSpawns(sent));
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);

        var score = Assert.Single(
            ReadGuildWarScoreScores(updatedSession, log.ToString()),
            entry => entry.GuildId == 77);
        Assert.Equal("SourceGuild", score.GuildName);
        Assert.Equal((long)directHit.Query.Damage, (long)score.Score);

        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == FirstRightCornerChatType &&
                packet.Content.Contains($"No 1. SourceGuild ({directHit.Query.Damage})"));

        var output = log.ToString();
        Assert.Contains("source MsgTopWarScore.UpdateScore modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackGuildWarScorePoleLethalHitFinishesRoundAndResetsPoleToWinner()
    {
        WorldPacketHandlers.ResetLegacyGuildWarScoreStateForTests();
        const int remainingPoleHitPoints = 1_250;
        const int maxPoleHitPoints = 50_000;
        var sobNpc = CreateGuildWarScorePoleSobNpc(life: remainingPoleHitPoints, maxLife: maxPoleHitPoints);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: maxPoleHitPoints, maxAttack: maxPoleHitPoints)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.GuildWarScoreMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var attributes = CreateGuildWarScorePoleAttributes();
        SetGuildWarScorePoleHitPointsAttributePairs(attributes, remainingPoleHitPoints);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.GuildWarScoreMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: maxPoleHitPoints,
            extraCharacterAttributes: attributes);
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: maxPoleHitPoints)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.Equal(remainingPoleHitPoints, directHit.Query.Damage);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);

        var score = Assert.Single(
            ReadGuildWarScoreScores(updatedSession, log.ToString()),
            entry => entry.GuildId == 77);
        Assert.Equal("SourceGuild", score.GuildName);
        Assert.Equal((long)remainingPoleHitPoints, (long)score.Score);

        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == FirstRightCornerChatType &&
                packet.Content.Contains($"No 1. SourceGuild ({remainingPoleHitPoints})"));
        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == LegacyMsgMessagePacket.CenterChatType &&
                packet.Content.Contains("The Round Ownered by guild SourceGuild."));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("SourceGuild", visibleSobNpc.Name);
        Assert.Equal(maxPoleHitPoints, visibleSobNpc.Life);
        Assert.Equal(maxPoleHitPoints, visibleSobNpc.MaxLife);
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);

        var output = log.ToString();
        Assert.Contains("source MsgTopWarScore.UpdateScore modeled", output);
        Assert.Contains("source MsgTopWarScore.FinishRound modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackGuildWarScorePoleFollowUpHitAccumulatesGuildScore()
    {
        WorldPacketHandlers.ResetLegacyGuildWarScoreStateForTests();
        var sobNpc = CreateGuildWarScorePoleSobNpc(life: 50_000, maxLife: 50_000);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.GuildWarScoreMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.GuildWarScoreMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: CreateGuildWarScorePoleAttributes());
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));
        var firstHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange((ulong)firstHit.Query.Damage, 1UL, checked((ulong)(sobNpc.Life - 1)));
        Assert.True(sessions.TryGet(connection, out var afterFirstHit));
        Assert.NotNull(afterFirstHit);

        var firstScore = Assert.Single(
            ReadGuildWarScoreScores(afterFirstHit, log.ToString()),
            entry => entry.GuildId == 77);
        Assert.Equal("SourceGuild", firstScore.GuildName);
        Assert.Equal((long)firstHit.Query.Damage, (long)firstScore.Score);

        sent.Clear();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                afterFirstHit,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));

        var secondHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange(
            (ulong)secondHit.Query.Damage,
            1UL,
            checked((ulong)(sobNpc.Life - checked((int)firstHit.Query.Damage) - 1)));
        Assert.True(sessions.TryGet(connection, out var afterSecondHit));
        Assert.NotNull(afterSecondHit);

        var score = Assert.Single(
            ReadGuildWarScoreScores(afterSecondHit, log.ToString()),
            entry => entry.GuildId == 77);
        Assert.Equal("SourceGuild", score.GuildName);
        Assert.Equal((long)firstHit.Query.Damage + secondHit.Query.Damage, (long)score.Score);

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("GuildWarSocre", visibleSobNpc.Name);
        Assert.Equal(
            sobNpc.Life - checked((int)firstHit.Query.Damage) - checked((int)secondHit.Query.Damage),
            visibleSobNpc.Life);
        Assert.Empty(ParseSobNpcSpawns(sent));
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);
        var output = log.ToString();
        Assert.Contains("source MsgTopWarScore.UpdateScore modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackGuildWarScorePoleSecondGuildBroadcastsScoresInDescendingOrder()
    {
        WorldPacketHandlers.ResetLegacyGuildWarScoreStateForTests();
        var sobNpc = CreateGuildWarScorePoleSobNpc(life: 50_000, maxLife: 50_000);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.GuildWarScoreMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sourceSent = new List<byte[]>();
        var rivalSent = new List<byte[]>();
        using var log = new StringWriter();
        var sourceConnection = CreateSendingConnection(sourceSent);
        var sourceSession = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.GuildWarScoreMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: CreateGuildWarScorePoleAttributes());
        sessions.Set(sourceConnection, sourceSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                sourceConnection,
                log,
                sourceSession,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));
        var sourceFirstHit = Assert.Single(
            ParseAttacks(sourceSent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(sessions.TryGet(sourceConnection, out var afterSourceFirstHit));
        Assert.NotNull(afterSourceFirstHit);

        sourceSent.Clear();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                sourceConnection,
                log,
                afterSourceFirstHit,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));
        var sourceSecondHit = Assert.Single(
            ParseAttacks(sourceSent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(sessions.TryGet(sourceConnection, out var afterSourceSecondHit));
        Assert.NotNull(afterSourceSecondHit);

        const uint rivalPlayerUid = PlayerUid + 1;
        var rivalConnection = CreateSendingConnection(rivalSent);
        var rivalAttributes = CreateGuildWarScorePoleAttributes();
        rivalAttributes["GuildID"] = "88";
        rivalAttributes["GuildId"] = "88";
        rivalAttributes["GuildName"] = "RivalGuild";
        rivalAttributes["LegacyGuildName"] = "RivalGuild";
        var rivalSession = CreateSession(
            spellIds: [],
            playerUid: rivalPlayerUid,
            mapId: LegacySobNpcEventRules.GuildWarScoreMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: rivalAttributes);
        sessions.Set(rivalConnection, rivalSession);

        sourceSent.Clear();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                rivalConnection,
                log,
                rivalSession,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000,
                playerUid: rivalPlayerUid)).ToFrameBytes()));

        var rivalHit = Assert.Single(
            ParseAttacks(rivalSent),
            attack => attack.Query.Uid == rivalPlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(sessions.TryGet(rivalConnection, out var afterRivalHit));
        Assert.NotNull(afterRivalHit);

        var scores = ReadGuildWarScoreScores(afterRivalHit, log.ToString());
        var sourceScore = Assert.Single(scores, entry => entry.GuildId == 77);
        var rivalScore = Assert.Single(scores, entry => entry.GuildId == 88);
        Assert.Equal("SourceGuild", sourceScore.GuildName);
        Assert.Equal("RivalGuild", rivalScore.GuildName);
        Assert.Equal((long)sourceFirstHit.Query.Damage + sourceSecondHit.Query.Damage, (long)sourceScore.Score);
        Assert.Equal((long)rivalHit.Query.Damage, (long)rivalScore.Score);
        Assert.True(sourceScore.Score > rivalScore.Score);

        AssertGuildWarScoreRightCornerScoresInDescendingOrder(sourceSent, sourceScore, rivalScore);
        AssertGuildWarScoreRightCornerScoresInDescendingOrder(rivalSent, sourceScore, rivalScore);

        var output = log.ToString();
        Assert.Contains("source MsgTopWarScore.UpdateScore modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackClanWarScorePoleNonLethalHitRecordsClanScoreWithoutDeathResetOrCapture()
    {
        WorldPacketHandlers.ResetLegacyClanWarScoreStateForTests();
        var sobNpc = CreateClanWarScorePoleSobNpc();
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.ClanWarScoreMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.ClanWarScoreMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: CreateClanWarScorePoleAttributes());
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange((ulong)directHit.Query.Damage, 1UL, checked((ulong)(sobNpc.Life - 1)));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("ClanWarSocre", visibleSobNpc.Name);
        Assert.Equal(sobNpc.Life - checked((int)directHit.Query.Damage), visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);
        Assert.Empty(ParseSobNpcSpawns(sent));
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);
        Assert.DoesNotContain(
            ParseMsgUpdates(sent),
            packet => packet.Uid == checked((uint)sobNpc.Id) &&
                packet.Updates.Any(update =>
                    (update.Type == LegacyMsgUpdateDataType.Hitpoints ||
                        update.Type == LegacyMsgUpdateDataType.MaxHitpoints) &&
                    update.Value == checked((uint)sobNpc.MaxLife)));
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);

        var score = Assert.Single(
            ReadClanWarScoreScores(updatedSession, log.ToString()),
            entry => entry.ClanId == 501);
        Assert.Equal("SourceClan", score.ClanName);
        Assert.Equal((long)directHit.Query.Damage, (long)score.Score);

        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == FirstRightCornerChatType &&
                packet.Content.Contains($"No 1. SourceClan ({directHit.Query.Damage})"));

        var output = log.ToString();
        Assert.Contains("source MsgClanScore.UpdateScore modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackClanWarScorePoleLethalHitFinishesRoundClearsScoresAndResetsWinnerPole()
    {
        WorldPacketHandlers.ResetLegacyClanWarScoreStateForTests();
        const int remainingPoleHitPoints = 1_250;
        var sobNpc = CreateClanWarScorePoleSobNpc(life: remainingPoleHitPoints, maxLife: ClanWarScorePoleLife);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 50_000, maxAttack: 50_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.ClanWarScoreMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var attributes = CreateClanWarScorePoleAttributes();
        SetClanWarScorePoleHitPointsAttributePairs(attributes, remainingPoleHitPoints);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.ClanWarScoreMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 50_000,
            extraCharacterAttributes: attributes);
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 50_000)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.Equal(remainingPoleHitPoints, directHit.Query.Damage);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);

        Assert.False(
            updatedSession.Character.Attributes.ContainsKey("ClanWarScoreScores"),
            "Expected MsgClanW.Reset to clear the current ScoreList after choosing RoundOwner.");
        Assert.Equal("501", updatedSession.Character.Attributes["ClanWarScoreRoundOwnerClanID"]);
        Assert.Equal("SourceClan", updatedSession.Character.Attributes["ClanWarScoreRoundOwnerName"]);
        Assert.Equal(
            remainingPoleHitPoints.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes["ClanWarScoreRoundOwnerScore"]);
        Assert.Equal("SourceClan", updatedSession.Character.Attributes["ClanWarScorePoleOwnerName"]);
        Assert.Equal("true", updatedSession.Character.Attributes["ClanWarScorePoleReset"]);

        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == FirstRightCornerChatType &&
                packet.Content.Contains($"No 1. SourceClan ({remainingPoleHitPoints})"));
        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.RedColor &&
                packet.ChatType == LegacyMsgMessagePacket.CenterChatType &&
                packet.Content.Contains("The Round Ownered by Clan SourceClan."));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("SourceClan", visibleSobNpc.Name);
        Assert.Equal(ClanWarScorePoleLife, visibleSobNpc.Life);
        Assert.Equal(ClanWarScorePoleLife, visibleSobNpc.MaxLife);
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);

        var output = log.ToString();
        Assert.Contains("source MsgClanScore.UpdateScore modeled", output);
        Assert.Contains("source MsgClanScore.FinishRound modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackClanWarScorePoleFollowUpSameClanHitKeepsFirstDamageScore()
    {
        WorldPacketHandlers.ResetLegacyClanWarScoreStateForTests();
        var sobNpc = CreateClanWarScorePoleSobNpc();
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.ClanWarScoreMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.ClanWarScoreMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: CreateClanWarScorePoleAttributes());
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));
        var firstHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange((ulong)firstHit.Query.Damage, 1UL, checked((ulong)(sobNpc.Life - 1)));
        Assert.True(sessions.TryGet(connection, out var afterFirstHit));
        Assert.NotNull(afterFirstHit);

        var firstScore = Assert.Single(
            ReadClanWarScoreScores(afterFirstHit, log.ToString()),
            entry => entry.ClanId == 501);
        Assert.Equal("SourceClan", firstScore.ClanName);
        Assert.Equal((long)firstHit.Query.Damage, (long)firstScore.Score);

        sent.Clear();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                afterFirstHit,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));

        var secondHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange(
            (ulong)secondHit.Query.Damage,
            1UL,
            checked((ulong)(sobNpc.Life - checked((int)firstHit.Query.Damage) - 1)));
        Assert.True(sessions.TryGet(connection, out var afterSecondHit));
        Assert.NotNull(afterSecondHit);

        var score = Assert.Single(
            ReadClanWarScoreScores(afterSecondHit, log.ToString()),
            entry => entry.ClanId == 501);
        Assert.Equal("SourceClan", score.ClanName);
        Assert.Equal((long)firstHit.Query.Damage, (long)score.Score);
        Assert.NotEqual((long)firstHit.Query.Damage + secondHit.Query.Damage, (long)score.Score);

        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == FirstRightCornerChatType &&
                packet.Content.Contains($"No 1. SourceClan ({firstHit.Query.Damage})"));
        Assert.DoesNotContain(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == FirstRightCornerChatType &&
                packet.Content.Contains($"No 1. SourceClan ({firstHit.Query.Damage + secondHit.Query.Damage})"));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("ClanWarSocre", visibleSobNpc.Name);
        Assert.Equal(
            sobNpc.Life - checked((int)firstHit.Query.Damage) - checked((int)secondHit.Query.Damage),
            visibleSobNpc.Life);
        Assert.Empty(ParseSobNpcSpawns(sent));
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);
        var output = log.ToString();
        Assert.Contains("source MsgClanScore.UpdateScore modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackClanWarScorePoleSecondClanBroadcastsScoresInDescendingOrder()
    {
        WorldPacketHandlers.ResetLegacyClanWarScoreStateForTests();
        var sobNpc = CreateClanWarScorePoleSobNpc();
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.ClanWarScoreMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sourceSent = new List<byte[]>();
        var rivalSent = new List<byte[]>();
        using var log = new StringWriter();
        var sourceConnection = CreateSendingConnection(sourceSent);
        var sourceSession = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.ClanWarScoreMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 2_000,
            extraCharacterAttributes: CreateClanWarScorePoleAttributes());
        sessions.Set(sourceConnection, sourceSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                sourceConnection,
                log,
                sourceSession,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));
        var sourceHit = Assert.Single(
            ParseAttacks(sourceSent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(sessions.TryGet(sourceConnection, out var afterSourceHit));
        Assert.NotNull(afterSourceHit);

        const uint rivalPlayerUid = PlayerUid + 1;
        var rivalConnection = CreateSendingConnection(rivalSent);
        var rivalAttributes = CreateClanWarScorePoleAttributes();
        rivalAttributes["ClanID"] = "502";
        rivalAttributes["ClanUID"] = "502";
        rivalAttributes["ClanName"] = "RivalClan";
        rivalAttributes["LegacyClanName"] = "RivalClan";
        var rivalSession = CreateSession(
            spellIds: [],
            playerUid: rivalPlayerUid,
            mapId: LegacySobNpcEventRules.ClanWarScoreMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: rivalAttributes);
        sessions.Set(rivalConnection, rivalSession);

        sourceSent.Clear();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                rivalConnection,
                log,
                rivalSession,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000,
                playerUid: rivalPlayerUid)).ToFrameBytes()));

        var rivalHit = Assert.Single(
            ParseAttacks(rivalSent),
            attack => attack.Query.Uid == rivalPlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(sessions.TryGet(rivalConnection, out var afterRivalHit));
        Assert.NotNull(afterRivalHit);

        var scores = ReadClanWarScoreScores(afterRivalHit, log.ToString());
        var sourceScore = Assert.Single(scores, entry => entry.ClanId == 501);
        var rivalScore = Assert.Single(scores, entry => entry.ClanId == 502);
        Assert.Equal("SourceClan", sourceScore.ClanName);
        Assert.Equal("RivalClan", rivalScore.ClanName);
        Assert.Equal((long)sourceHit.Query.Damage, (long)sourceScore.Score);
        Assert.Equal((long)rivalHit.Query.Damage, (long)rivalScore.Score);
        Assert.True(sourceScore.Score > rivalScore.Score);

        AssertClanWarScoreRightCornerScoresInDescendingOrder(sourceSent, sourceScore, rivalScore);
        AssertClanWarScoreRightCornerScoresInDescendingOrder(rivalSent, sourceScore, rivalScore);

        var output = log.ToString();
        Assert.Contains("source MsgClanScore.UpdateScore modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackClanWarCurrentPoleNonLethalHitRecordsCityClanScoreWithoutDeathResetOrCapture()
    {
        WorldPacketHandlers.ResetLegacyClanWarStateForTests();
        var sobNpc = CreateClanWarCurrentPoleSobNpc();
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [sobNpc.MapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [],
            mapId: sobNpc.MapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: CreateClanWarCurrentPoleAttributes());
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange((ulong)directHit.Query.Damage, 1UL, checked((ulong)(sobNpc.Life - 1)));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("ClanWarPole", visibleSobNpc.Name);
        Assert.Equal(sobNpc.Life - checked((int)directHit.Query.Damage), visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);
        Assert.Empty(ParseSobNpcSpawns(sent));
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);
        Assert.DoesNotContain(
            ParseMsgUpdates(sent),
            packet => packet.Uid == checked((uint)sobNpc.Id) &&
                packet.Updates.Any(update =>
                    (update.Type == LegacyMsgUpdateDataType.Hitpoints ||
                        update.Type == LegacyMsgUpdateDataType.MaxHitpoints) &&
                    update.Value == checked((uint)sobNpc.MaxLife)));
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);

        var score = Assert.Single(
            ReadClanWarScores(updatedSession, log.ToString()),
            entry => entry.City == "Twin" && entry.ClanId == 501);
        Assert.Equal("SourceClan", score.ClanName);
        Assert.Equal((long)directHit.Query.Damage, (long)score.Score);
        AssertNoClanWarCurrentRightCornerScorePanel(sent);

        var output = log.ToString();
        Assert.Contains("source MsgClanWar.CityWar.UpdateScore modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackClanWarCurrentPoleFollowUpSameClanHitAccumulatesUnlikeMsgClanWFixedScoreboard()
    {
        WorldPacketHandlers.ResetLegacyClanWarStateForTests();
        var sobNpc = CreateClanWarCurrentPoleSobNpc();
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [sobNpc.MapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [],
            mapId: sobNpc.MapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: CreateClanWarCurrentPoleAttributes());
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));
        var firstHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange((ulong)firstHit.Query.Damage, 1UL, checked((ulong)(sobNpc.Life - 1)));
        Assert.True(sessions.TryGet(connection, out var afterFirstHit));
        Assert.NotNull(afterFirstHit);

        var firstScore = Assert.Single(
            ReadClanWarScores(afterFirstHit, log.ToString()),
            entry => entry.City == "Twin" && entry.ClanId == 501);
        Assert.Equal("SourceClan", firstScore.ClanName);
        Assert.Equal((long)firstHit.Query.Damage, (long)firstScore.Score);
        AssertNoClanWarCurrentRightCornerScorePanel(sent);

        sent.Clear();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                afterFirstHit,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));

        var secondHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange(
            (ulong)secondHit.Query.Damage,
            1UL,
            checked((ulong)(sobNpc.Life - checked((int)firstHit.Query.Damage) - 1)));
        Assert.True(sessions.TryGet(connection, out var afterSecondHit));
        Assert.NotNull(afterSecondHit);

        var score = Assert.Single(
            ReadClanWarScores(afterSecondHit, log.ToString()),
            entry => entry.City == "Twin" && entry.ClanId == 501);
        Assert.Equal("SourceClan", score.ClanName);
        Assert.Equal((long)firstHit.Query.Damage + secondHit.Query.Damage, (long)score.Score);
        Assert.NotEqual((long)firstHit.Query.Damage, (long)score.Score);
        AssertNoClanWarCurrentRightCornerScorePanel(sent);

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("ClanWarPole", visibleSobNpc.Name);
        Assert.Equal(
            sobNpc.Life - checked((int)firstHit.Query.Damage) - checked((int)secondHit.Query.Damage),
            visibleSobNpc.Life);
        Assert.Empty(ParseSobNpcSpawns(sent));
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);
        var output = log.ToString();
        Assert.Contains("source MsgClanWar.CityWar.UpdateScore modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackClanWarCurrentPolePeriodicScorePanelBroadcastsDescendingScoresAfterDirectHits()
    {
        WorldPacketHandlers.ResetLegacyClanWarStateForTests();
        var sobNpc = CreateClanWarCurrentPoleSobNpc();
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [sobNpc.MapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sourceSent = new List<byte[]>();
        var rivalSent = new List<byte[]>();
        using var log = new StringWriter();
        var sourceConnection = CreateSendingConnection(sourceSent);
        var sourceSession = CreateSession(
            spellIds: [],
            mapId: sobNpc.MapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 2_000,
            extraCharacterAttributes: CreateClanWarCurrentPoleAttributes());
        sessions.Set(sourceConnection, sourceSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                sourceConnection,
                log,
                sourceSession,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));
        var sourceHit = Assert.Single(
            ParseAttacks(sourceSent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(sessions.TryGet(sourceConnection, out var afterSourceHit));
        Assert.NotNull(afterSourceHit);
        AssertNoClanWarCurrentRightCornerScorePanel(sourceSent);

        const uint rivalPlayerUid = PlayerUid + 1;
        var rivalConnection = CreateSendingConnection(rivalSent);
        var rivalAttributes = CreateClanWarCurrentPoleAttributes();
        rivalAttributes["ClanID"] = "502";
        rivalAttributes["ClanUID"] = "502";
        rivalAttributes["ClanName"] = "RivalClan";
        rivalAttributes["LegacyClanName"] = "RivalClan";
        var rivalSession = CreateSession(
            spellIds: [],
            playerUid: rivalPlayerUid,
            mapId: sobNpc.MapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: rivalAttributes);
        sessions.Set(rivalConnection, rivalSession);

        sourceSent.Clear();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                rivalConnection,
                log,
                rivalSession,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000,
                playerUid: rivalPlayerUid)).ToFrameBytes()));

        var rivalHit = Assert.Single(
            ParseAttacks(rivalSent),
            attack => attack.Query.Uid == rivalPlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(sessions.TryGet(rivalConnection, out var afterRivalHit));
        Assert.NotNull(afterRivalHit);

        var scores = ReadClanWarScores(afterRivalHit, log.ToString());
        var sourceScore = Assert.Single(scores, entry => entry.City == "Twin" && entry.ClanId == 501);
        var rivalScore = Assert.Single(scores, entry => entry.City == "Twin" && entry.ClanId == 502);
        Assert.Equal("SourceClan", sourceScore.ClanName);
        Assert.Equal("RivalClan", rivalScore.ClanName);
        Assert.Equal((long)sourceHit.Query.Damage, (long)sourceScore.Score);
        Assert.Equal((long)rivalHit.Query.Damage, (long)rivalScore.Score);
        Assert.True(sourceScore.Score > rivalScore.Score);
        AssertNoClanWarCurrentRightCornerScorePanel(sourceSent);
        AssertNoClanWarCurrentRightCornerScorePanel(rivalSent);

        sourceSent.Clear();
        rivalSent.Clear();
        var now = new DateTimeOffset(2026, 6, 30, 12, 0, 0, TimeSpan.Zero);
        await WorldPacketHandlers.SendDueLegacyClanWarCurrentScorePanelFramesAsync(
            new WorldPacketHandlerContext(
                sourceConnection,
                log,
                afterRivalHit,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            now,
            CancellationToken.None);

        AssertClanWarCurrentRightCornerScoresInDescendingOrder(sourceSent, sourceScore, rivalScore);
        AssertClanWarCurrentRightCornerScoresInDescendingOrder(rivalSent, sourceScore, rivalScore);

        sourceSent.Clear();
        rivalSent.Clear();
        await WorldPacketHandlers.SendDueLegacyClanWarCurrentScorePanelFramesAsync(
            new WorldPacketHandlerContext(
                sourceConnection,
                log,
                afterRivalHit,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            now.AddSeconds(7),
            CancellationToken.None);

        AssertNoClanWarCurrentRightCornerScorePanel(sourceSent);
        AssertNoClanWarCurrentRightCornerScorePanel(rivalSent);

        await WorldPacketHandlers.SendDueLegacyClanWarCurrentScorePanelFramesAsync(
            new WorldPacketHandlerContext(
                sourceConnection,
                log,
                afterRivalHit,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            now.AddSeconds(8),
            CancellationToken.None);

        AssertClanWarCurrentRightCornerScoresInDescendingOrder(sourceSent, sourceScore, rivalScore);
        AssertClanWarCurrentRightCornerScoresInDescendingOrder(rivalSent, sourceScore, rivalScore);

        var output = log.ToString();
        Assert.Contains("source MsgClanWar.CityWar.UpdateScore modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackClanWarCurrentPoleLethalHitFinishesRoundClearsScoresAndResetsWinnerPole()
    {
        WorldPacketHandlers.ResetLegacyClanWarStateForTests();
        var sobNpc = CreateClanWarCurrentPoleSobNpc();
        var worldData = CreateWorldData(
            items:
            [
                CreateClubItemType(
                    minAttack: checked((uint)ClanWarPoleLife),
                    maxAttack: checked((uint)ClanWarPoleLife)),
            ],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [sobNpc.MapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [],
            mapId: sobNpc.MapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: ClanWarPoleLife,
            extraCharacterAttributes: CreateClanWarCurrentPoleAttributes());
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: ClanWarPoleLife)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(directHit.Query.Damage >= sobNpc.Life, $"Direct hit did not kill the ClanWar current pole. Log={log}");

        var rightCornerMessages = ParseLegacyMessages(sent)
            .Where(packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                (packet.ChatType == FirstRightCornerChatType || packet.ChatType == ContinueRightCornerChatType) &&
                packet.Content.Any(content => content.StartsWith("No ", StringComparison.Ordinal)))
            .ToArray();
        Assert.Collection(
            rightCornerMessages,
            packet =>
            {
                Assert.Equal(FirstRightCornerChatType, packet.ChatType);
                Assert.Contains(
                    $"No 1. SourceClan ({ClanWarPoleLife})",
                    packet.Content);
            });

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.False(
            updatedSession.Character.Attributes.ContainsKey("ClanWarScores"),
            "Expected MsgClanWar.CityWar.FinishRound to clear current ScoreList after shuffling scores.");
        Assert.False(updatedSession.Character.Attributes.ContainsKey("LegacyClanWarScores"));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("SourceClan", visibleSobNpc.Name);
        Assert.Equal(ClanWarPoleLife, visibleSobNpc.Life);
        Assert.Equal(ClanWarPoleLife, visibleSobNpc.MaxLife);
        Assert.Contains(
            ParseSobNpcSpawns(sent),
            spawn => spawn.Uid == checked((uint)sobNpc.Id) &&
                spawn.Name == "SourceClan" &&
                spawn.Hitpoints == checked((uint)ClanWarPoleLife) &&
                spawn.MaxHitpoints == checked((uint)ClanWarPoleLife));

        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.Empty(ParseCaptureTheFlagScoreBasePackets(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);

        sent.Clear();
        await WorldPacketHandlers.SendDueLegacyClanWarCurrentScorePanelFramesAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                updatedSession,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            new DateTimeOffset(2026, 6, 30, 12, 0, 0, TimeSpan.Zero),
            CancellationToken.None);
        AssertNoClanWarCurrentRightCornerScorePanel(sent);

        var output = log.ToString();
        Assert.Contains("source MsgClanWar.CityWar.FinishRound modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackWarOfPlayersPoleNonLethalHitRecordsPlayerScoreWithoutDeathResetOrCapture()
    {
        WorldPacketHandlers.ResetLegacyWarOfPlayersScoreStateForTests();
        var sobNpc = CreateWarOfPlayersPoleSobNpc(life: 50_000, maxLife: 50_000);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.WarOfPlayersMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.WarOfPlayersMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: CreateWarOfPlayersPoleAttributes());
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange((ulong)directHit.Query.Damage, 1UL, checked((ulong)(sobNpc.Life - 1)));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("WarOfPlayers", visibleSobNpc.Name);
        Assert.Equal(sobNpc.Life - checked((int)directHit.Query.Damage), visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);
        Assert.Empty(ParseSobNpcSpawns(sent));
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);

        var score = Assert.Single(
            ReadWarOfPlayersScores(updatedSession, log.ToString()),
            entry => entry.PlayerUid == PlayerUid);
        Assert.Equal("Caster", score.PlayerName);
        Assert.Equal((long)directHit.Query.Damage, (long)score.Score);

        var rightCornerMessage = Assert.Single(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == FirstRightCornerChatType &&
                packet.Content.Any(content => content.StartsWith("No ", StringComparison.Ordinal)));
        Assert.Contains($"No 1. Caster ({score.Score})", rightCornerMessage.Content);

        var output = log.ToString();
        Assert.Contains("WorldServer updated legacy WarOfPlayers pole score", output);
        Assert.Contains("source MsgWarOfPlayers.UpdateScore modeled", output);
        Assert.DoesNotContain("WarOfPlayers capture", output);
        Assert.DoesNotContain("WarOfPlayers reset", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackWarOfPlayersPoleLethalHitFinishesRoundAndResetsPoleToWinner()
    {
        WorldPacketHandlers.ResetLegacyWarOfPlayersScoreStateForTests();
        const int remainingPoleHitPoints = 1_250;
        const int maxPoleHitPoints = 50_000;
        var sobNpc = CreateWarOfPlayersPoleSobNpc(life: remainingPoleHitPoints, maxLife: maxPoleHitPoints);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 50_000, maxAttack: 50_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.WarOfPlayersMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var attributes = CreateWarOfPlayersPoleAttributes();
        SetWarOfPlayersPoleHitPointsAttributePairs(attributes, remainingPoleHitPoints);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.WarOfPlayersMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 50_000,
            extraCharacterAttributes: attributes);
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 50_000)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.Equal(remainingPoleHitPoints, directHit.Query.Damage);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);

        Assert.False(
            updatedSession.Character.Attributes.ContainsKey("WarOfPlayersScores"),
            "Expected MsgWarOfPlayers.FinishRound Reset to clear current score table.");
        Assert.Equal(
            PlayerUid.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes["WarOfPlayersRoundOwnerPlayerUID"]);
        Assert.Equal("Caster", updatedSession.Character.Attributes["WarOfPlayersRoundOwnerName"]);
        Assert.Equal(
            remainingPoleHitPoints.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes["WarOfPlayersRoundOwnerScore"]);
        Assert.Equal("Caster", updatedSession.Character.Attributes["WarOfPlayersPoleOwnerName"]);
        Assert.Equal("true", updatedSession.Character.Attributes["WarOfPlayersPoleReset"]);

        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == FirstRightCornerChatType &&
                packet.Content.Contains($"No 1. Caster ({remainingPoleHitPoints})"));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("Caster", visibleSobNpc.Name);
        Assert.Equal(maxPoleHitPoints, visibleSobNpc.Life);
        Assert.Equal(maxPoleHitPoints, visibleSobNpc.MaxLife);
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);

        var output = log.ToString();
        Assert.Contains("source MsgWarOfPlayers.UpdateScore modeled", output);
        Assert.Contains("source MsgWarOfPlayers.FinishRound modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackWarOfPlayersPoleFollowUpHitAccumulatesPlayerScore()
    {
        WorldPacketHandlers.ResetLegacyWarOfPlayersScoreStateForTests();
        const uint followUpPlayerUid = PlayerUid + 1;
        var sobNpc = CreateWarOfPlayersPoleSobNpc(life: 50_000, maxLife: 50_000);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.WarOfPlayersMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [],
            playerUid: followUpPlayerUid,
            mapId: LegacySobNpcEventRules.WarOfPlayersMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: CreateWarOfPlayersPoleAttributes());
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000,
                playerUid: followUpPlayerUid)).ToFrameBytes()));
        var firstHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == followUpPlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange((ulong)firstHit.Query.Damage, 1UL, checked((ulong)(sobNpc.Life - 1)));
        Assert.True(sessions.TryGet(connection, out var afterFirstHit));
        Assert.NotNull(afterFirstHit);

        sent.Clear();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                afterFirstHit,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000,
                playerUid: followUpPlayerUid)).ToFrameBytes()));

        var secondHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == followUpPlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange(
            (ulong)secondHit.Query.Damage,
            1UL,
            checked((ulong)(sobNpc.Life - checked((int)firstHit.Query.Damage) - 1)));
        Assert.True(sessions.TryGet(connection, out var afterSecondHit));
        Assert.NotNull(afterSecondHit);

        var score = Assert.Single(
            ReadWarOfPlayersScores(afterSecondHit, log.ToString()),
            entry => entry.PlayerUid == followUpPlayerUid);
        Assert.Equal("Caster", score.PlayerName);
        Assert.Equal((long)firstHit.Query.Damage + secondHit.Query.Damage, (long)score.Score);

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("WarOfPlayers", visibleSobNpc.Name);
        Assert.Equal(
            sobNpc.Life - checked((int)firstHit.Query.Damage) - checked((int)secondHit.Query.Damage),
            visibleSobNpc.Life);
        Assert.Empty(ParseSobNpcSpawns(sent));
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);
        var output = log.ToString();
        Assert.Contains("WorldServer updated legacy WarOfPlayers pole score", output);
        Assert.Contains("source MsgWarOfPlayers.UpdateScore modeled", output);
        Assert.DoesNotContain("WarOfPlayers capture", output);
        Assert.DoesNotContain("WarOfPlayers reset", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackWarOfPlayersPoleSecondPlayerBroadcastsScoresInDescendingOrder()
    {
        WorldPacketHandlers.ResetLegacyWarOfPlayersScoreStateForTests();
        var sobNpc = CreateWarOfPlayersPoleSobNpc(life: 50_000, maxLife: 50_000);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.WarOfPlayersMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sourceSent = new List<byte[]>();
        var rivalSent = new List<byte[]>();
        using var log = new StringWriter();
        var sourceConnection = CreateSendingConnection(sourceSent);
        var sourceSession = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.WarOfPlayersMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: CreateWarOfPlayersPoleAttributes());
        sessions.Set(sourceConnection, sourceSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                sourceConnection,
                log,
                sourceSession,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));
        var sourceFirstHit = Assert.Single(
            ParseAttacks(sourceSent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(sessions.TryGet(sourceConnection, out var afterSourceFirstHit));
        Assert.NotNull(afterSourceFirstHit);

        sourceSent.Clear();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                sourceConnection,
                log,
                afterSourceFirstHit,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));
        var sourceSecondHit = Assert.Single(
            ParseAttacks(sourceSent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(sessions.TryGet(sourceConnection, out var afterSourceSecondHit));
        Assert.NotNull(afterSourceSecondHit);

        const uint rivalPlayerUid = PlayerUid + 1;
        var rivalConnection = CreateSendingConnection(rivalSent);
        var rivalSession = CreateSession(
            spellIds: [],
            playerUid: rivalPlayerUid,
            mapId: LegacySobNpcEventRules.WarOfPlayersMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: CreateWarOfPlayersPoleAttributes());
        sessions.Set(rivalConnection, rivalSession);

        sourceSent.Clear();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                rivalConnection,
                log,
                rivalSession,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000,
                playerUid: rivalPlayerUid)).ToFrameBytes()));

        var rivalHit = Assert.Single(
            ParseAttacks(rivalSent),
            attack => attack.Query.Uid == rivalPlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(sessions.TryGet(rivalConnection, out var afterRivalHit));
        Assert.NotNull(afterRivalHit);

        var scores = ReadWarOfPlayersScores(afterRivalHit, log.ToString());
        var sourceScore = Assert.Single(scores, entry => entry.PlayerUid == PlayerUid);
        var rivalScore = Assert.Single(scores, entry => entry.PlayerUid == rivalPlayerUid);
        Assert.Equal("Caster", sourceScore.PlayerName);
        Assert.Equal("Caster", rivalScore.PlayerName);
        Assert.Equal((long)sourceFirstHit.Query.Damage + sourceSecondHit.Query.Damage, (long)sourceScore.Score);
        Assert.Equal((long)rivalHit.Query.Damage, (long)rivalScore.Score);
        Assert.True(sourceScore.Score > rivalScore.Score);

        AssertWarOfPlayersRightCornerScoresInDescendingOrder(sourceSent, sourceScore, rivalScore);
        AssertWarOfPlayersRightCornerScoresInDescendingOrder(rivalSent, sourceScore, rivalScore);

        var output = log.ToString();
        Assert.Contains("WorldServer updated legacy WarOfPlayers pole score", output);
        Assert.Contains("source MsgWarOfPlayers.UpdateScore modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackEmperorWarPoleNonLethalHitRecordsPlayerScoreWithoutDeathResetOrCapture()
    {
        WorldPacketHandlers.ResetLegacyEmperorWarScoreStateForTests();
        var sobNpc = CreateEmperorWarPoleSobNpc();
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.EmperorWarMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.EmperorWarMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: CreateEmperorWarPoleAttributes());
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange((ulong)directHit.Query.Damage, 1UL, checked((ulong)(sobNpc.Life - 1)));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("Emperrors", visibleSobNpc.Name);
        Assert.Equal(sobNpc.Life - checked((int)directHit.Query.Damage), visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);
        Assert.Empty(ParseSobNpcSpawns(sent));
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);

        var score = Assert.Single(
            ReadEmperorWarScores(updatedSession, log.ToString()),
            entry => entry.PlayerUid == PlayerUid);
        Assert.Equal("Caster", score.PlayerName);
        Assert.Equal((long)directHit.Query.Damage, (long)score.Score);

        var rightCornerMessage = Assert.Single(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == FirstRightCornerChatType &&
                packet.Content.Any(content => content.StartsWith("No ", StringComparison.Ordinal)));
        Assert.Contains($"No 1. Caster ({directHit.Query.Damage})", rightCornerMessage.Content);

        var output = log.ToString();
        Assert.Contains("WorldServer updated legacy EmperorWar pole score", output);
        Assert.Contains("source MsgEmperorWar.UpdateScore modeled", output);
        Assert.DoesNotContain("EmperorWar capture", output);
        Assert.DoesNotContain("EmperorWar reset", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackEmperorWarPoleLethalHitFinishesRoundAndResetsPoleToWinner()
    {
        WorldPacketHandlers.ResetLegacyEmperorWarScoreStateForTests();
        const int remainingPoleHitPoints = 1_250;
        var sobNpc = CreateEmperorWarPoleSobNpc(life: remainingPoleHitPoints, maxLife: EmperorWarPoleLife);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 50_000, maxAttack: 50_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.EmperorWarMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var attributes = CreateEmperorWarPoleAttributes();
        SetEmperorWarPoleHitPointsAttributePairs(attributes, remainingPoleHitPoints);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.EmperorWarMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 50_000,
            extraCharacterAttributes: attributes);
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 50_000)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.Equal(remainingPoleHitPoints, directHit.Query.Damage);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);

        Assert.False(
            updatedSession.Character.Attributes.ContainsKey("EmperorWarScores"),
            "Expected MsgEmperorWar.FinishRound Reset to clear current score table.");
        Assert.Equal(
            PlayerUid.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes["EmperorWarRoundOwnerPlayerUID"]);
        Assert.Equal("Caster", updatedSession.Character.Attributes["EmperorWarRoundOwnerName"]);
        Assert.Equal(
            remainingPoleHitPoints.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes["EmperorWarRoundOwnerScore"]);
        Assert.Equal("Caster", updatedSession.Character.Attributes["EmperorWarPoleOwnerName"]);
        Assert.Equal("true", updatedSession.Character.Attributes["EmperorWarPoleReset"]);

        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == FirstRightCornerChatType &&
                packet.Content.Contains($"No 1. Caster ({remainingPoleHitPoints})"));
        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.RedColor &&
                packet.ChatType == LegacyMsgMessagePacket.CenterChatType &&
                packet.Content.Contains("The Round Ownered by Caster."));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("Caster", visibleSobNpc.Name);
        Assert.Equal(EmperorWarPoleLife, visibleSobNpc.Life);
        Assert.Equal(EmperorWarPoleLife, visibleSobNpc.MaxLife);
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);

        var output = log.ToString();
        Assert.Contains("source MsgEmperorWar.UpdateScore modeled", output);
        Assert.Contains("source MsgEmperorWar.FinishRound modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackEmperorWarPoleFollowUpHitAccumulatesPlayerScore()
    {
        WorldPacketHandlers.ResetLegacyEmperorWarScoreStateForTests();
        var sobNpc = CreateEmperorWarPoleSobNpc();
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.EmperorWarMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.EmperorWarMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: CreateEmperorWarPoleAttributes());
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));
        var firstHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange((ulong)firstHit.Query.Damage, 1UL, checked((ulong)(sobNpc.Life - 1)));
        Assert.True(sessions.TryGet(connection, out var afterFirstHit));
        Assert.NotNull(afterFirstHit);

        sent.Clear();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                afterFirstHit,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));

        var secondHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange(
            (ulong)secondHit.Query.Damage,
            1UL,
            checked((ulong)(sobNpc.Life - checked((int)firstHit.Query.Damage) - 1)));
        Assert.True(sessions.TryGet(connection, out var afterSecondHit));
        Assert.NotNull(afterSecondHit);

        var accumulatedScore = Assert.Single(
            ReadEmperorWarScores(afterSecondHit, log.ToString()),
            entry => entry.PlayerUid == PlayerUid);
        Assert.Equal("Caster", accumulatedScore.PlayerName);
        Assert.Equal((long)firstHit.Query.Damage + secondHit.Query.Damage, (long)accumulatedScore.Score);

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("Emperrors", visibleSobNpc.Name);
        Assert.Equal(
            sobNpc.Life - checked((int)firstHit.Query.Damage) - checked((int)secondHit.Query.Damage),
            visibleSobNpc.Life);
        Assert.Empty(ParseSobNpcSpawns(sent));
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);

        var output = log.ToString();
        Assert.Contains("WorldServer updated legacy EmperorWar pole score", output);
        Assert.Contains("source MsgEmperorWar.UpdateScore modeled", output);
        Assert.DoesNotContain("EmperorWar capture", output);
        Assert.DoesNotContain("EmperorWar reset", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackEmperorWarPoleSecondPlayerBroadcastsScoresInDescendingOrder()
    {
        WorldPacketHandlers.ResetLegacyEmperorWarScoreStateForTests();
        var sobNpc = CreateEmperorWarPoleSobNpc();
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.EmperorWarMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sourceSent = new List<byte[]>();
        var rivalSent = new List<byte[]>();
        using var log = new StringWriter();
        var sourceConnection = CreateSendingConnection(sourceSent);
        var sourceSession = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.EmperorWarMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: CreateEmperorWarPoleAttributes());
        sessions.Set(sourceConnection, sourceSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                sourceConnection,
                log,
                sourceSession,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));
        var sourceFirstHit = Assert.Single(
            ParseAttacks(sourceSent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(sessions.TryGet(sourceConnection, out var afterSourceFirstHit));
        Assert.NotNull(afterSourceFirstHit);

        sourceSent.Clear();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                sourceConnection,
                log,
                afterSourceFirstHit,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));
        var sourceSecondHit = Assert.Single(
            ParseAttacks(sourceSent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(sessions.TryGet(sourceConnection, out var afterSourceSecondHit));
        Assert.NotNull(afterSourceSecondHit);

        const uint rivalPlayerUid = PlayerUid + 1;
        var rivalConnection = CreateSendingConnection(rivalSent);
        var rivalSession = CreateSession(
            spellIds: [],
            playerUid: rivalPlayerUid,
            mapId: LegacySobNpcEventRules.EmperorWarMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: CreateEmperorWarPoleAttributes());
        rivalSession = rivalSession with
        {
            Character = rivalSession.Character with
            {
                Name = "RivalCaster",
            },
        };
        sessions.Set(rivalConnection, rivalSession);

        sourceSent.Clear();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                rivalConnection,
                log,
                rivalSession,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000,
                playerUid: rivalPlayerUid)).ToFrameBytes()));

        var rivalHit = Assert.Single(
            ParseAttacks(rivalSent),
            attack => attack.Query.Uid == rivalPlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.True(sessions.TryGet(rivalConnection, out var afterRivalHit));
        Assert.NotNull(afterRivalHit);

        var scores = ReadEmperorWarScores(afterRivalHit, log.ToString());
        var sourceScore = Assert.Single(scores, entry => entry.PlayerUid == PlayerUid);
        var rivalScore = Assert.Single(scores, entry => entry.PlayerUid == rivalPlayerUid);
        Assert.Equal("Caster", sourceScore.PlayerName);
        Assert.Equal("RivalCaster", rivalScore.PlayerName);
        Assert.Equal((long)sourceFirstHit.Query.Damage + sourceSecondHit.Query.Damage, (long)sourceScore.Score);
        Assert.Equal((long)rivalHit.Query.Damage, (long)rivalScore.Score);
        Assert.True(sourceScore.Score > rivalScore.Score);

        AssertEmperorWarRightCornerScoresInDescendingOrder(sourceSent, sourceScore, rivalScore);
        AssertEmperorWarRightCornerScoresInDescendingOrder(rivalSent, sourceScore, rivalScore);

        var output = log.ToString();
        Assert.Contains("WorldServer updated legacy EmperorWar pole score", output);
        Assert.Contains("source MsgEmperorWar.UpdateScore modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackTopPolePoleNonLethalHitRecordsPlayerScoreWithoutDeathResetOrCapture()
    {
        WorldPacketHandlers.ResetLegacyTopPoleScoreStateForTests();
        var sobNpc = CreateTopPolePoleSobNpc();
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.TopPoleMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.TopPoleMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: CreateTopPoleAttributes());
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange((ulong)directHit.Query.Damage, 1UL, checked((ulong)(sobNpc.Life - 1)));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("TestSrv", visibleSobNpc.Name);
        Assert.Equal(sobNpc.Life - checked((int)directHit.Query.Damage), visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);
        Assert.Empty(ParseSobNpcSpawns(sent));
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);

        var score = Assert.Single(
            ReadTopPoleScores(updatedSession, log.ToString()),
            entry => entry.PlayerUid == PlayerUid);
        Assert.Equal("Caster", score.PlayerName);
        Assert.Equal((long)directHit.Query.Damage, (long)score.Score);

        var rightCornerMessage = Assert.Single(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == FirstRightCornerChatType &&
                packet.Content.Any(content => content.StartsWith("No ", StringComparison.Ordinal)));
        Assert.Contains($"No 1. Caster ({score.Score})", rightCornerMessage.Content);

        var output = log.ToString();
        Assert.Contains("WorldServer updated legacy TopPole pole score", output);
        Assert.Contains("source MsgTopPole.UpdateScore modeled", output);
        Assert.DoesNotContain("TopPole capture", output);
        Assert.DoesNotContain("TopPole reset", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackTopPolePoleLethalHitFinishesRoundAndResetsPoleToWinner()
    {
        WorldPacketHandlers.ResetLegacyTopPoleScoreStateForTests();
        const int remainingPoleHitPoints = 1_250;
        var sobNpc = CreateTopPolePoleSobNpc(life: remainingPoleHitPoints, maxLife: TopPoleLife);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 50_000, maxAttack: 50_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.TopPoleMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var attributes = CreateTopPoleAttributes();
        SetTopPoleHitPointsAttributePairs(attributes, remainingPoleHitPoints);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.TopPoleMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 50_000,
            extraCharacterAttributes: attributes);
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 50_000)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.Equal(remainingPoleHitPoints, directHit.Query.Damage);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);

        Assert.False(
            updatedSession.Character.Attributes.ContainsKey("TopPoleScores"),
            "Expected MsgTopPole.FinishRound Reset to clear current score table.");
        Assert.Equal(
            PlayerUid.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes["TopPoleRoundOwnerPlayerUID"]);
        Assert.Equal("Caster", updatedSession.Character.Attributes["TopPoleRoundOwnerName"]);
        Assert.Equal(
            remainingPoleHitPoints.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes["TopPoleRoundOwnerScore"]);
        Assert.Equal("Caster", updatedSession.Character.Attributes["TopPolePoleOwnerName"]);
        Assert.Equal("true", updatedSession.Character.Attributes["TopPolePoleReset"]);

        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == FirstRightCornerChatType &&
                packet.Content.Contains($"No 1. Caster ({remainingPoleHitPoints})"));
        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.RedColor &&
                packet.ChatType == LegacyMsgMessagePacket.CenterChatType &&
                packet.Content.Contains("The Round Ownered by Caster."));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("Caster", visibleSobNpc.Name);
        Assert.Equal(TopPoleLife, visibleSobNpc.Life);
        Assert.Equal(TopPoleLife, visibleSobNpc.MaxLife);
        var spawn = Assert.Single(ParseSobNpcSpawns(sent));
        Assert.Equal(checked((uint)sobNpc.Id), spawn.Uid);
        Assert.Equal("Caster", spawn.Name);
        Assert.Equal((uint)TopPoleLife, spawn.Hitpoints);
        Assert.Equal((uint)TopPoleLife, spawn.MaxHitpoints);
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);

        var output = log.ToString();
        Assert.Contains("source MsgTopPole.UpdateScore modeled", output);
        Assert.Contains("source MsgTopPole.FinishRound modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackWarFightersPoleNonLethalHitRecordsPlayerScoreWithoutDeathResetOrCapture()
    {
        WorldPacketHandlers.ResetLegacyWarFightersScoreStateForTests();
        var sobNpc = CreateWarFightersPoleSobNpc();
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.WarFightersMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.WarFightersMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: CreateWarFightersAttributes());
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange((ulong)directHit.Query.Damage, 1UL, checked((ulong)(sobNpc.Life - 1)));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("WarFighters", visibleSobNpc.Name);
        Assert.Equal(sobNpc.Life - checked((int)directHit.Query.Damage), visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);
        Assert.Empty(ParseSobNpcSpawns(sent));
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);

        var score = Assert.Single(
            ReadWarFightersScores(updatedSession, log.ToString()),
            entry => entry.PlayerUid == PlayerUid);
        Assert.Equal("Caster", score.PlayerName);
        Assert.Equal((long)directHit.Query.Damage, (long)score.Score);

        var rightCornerMessage = Assert.Single(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == FirstRightCornerChatType &&
                packet.Content.Any(content => content.StartsWith("No ", StringComparison.Ordinal)));
        Assert.Contains($"No 1. Caster ({score.Score})", rightCornerMessage.Content);

        var output = log.ToString();
        Assert.Contains("WorldServer updated legacy WarFighters pole score", output);
        Assert.Contains("source MsgWarFighters.UpdateScore modeled", output);
        Assert.DoesNotContain("WarFighters capture", output);
        Assert.DoesNotContain("WarFighters reset", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackWarFightersPoleLethalHitFinishesRoundAndResetsPoleToWinner()
    {
        WorldPacketHandlers.ResetLegacyWarFightersScoreStateForTests();
        const int remainingPoleHitPoints = 1_250;
        var sobNpc = CreateWarFightersPoleSobNpc(life: remainingPoleHitPoints, maxLife: WarFightersPoleLife);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 50_000, maxAttack: 50_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.WarFightersMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var attributes = CreateWarFightersAttributes();
        SetWarFightersHitPointsAttributePairs(attributes, remainingPoleHitPoints);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.WarFightersMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 50_000,
            extraCharacterAttributes: attributes);
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 50_000)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.Equal(remainingPoleHitPoints, directHit.Query.Damage);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);

        Assert.False(
            updatedSession.Character.Attributes.ContainsKey("WarFightersScores"),
            "Expected MsgWarFighters.FinishRound Reset to clear current score table.");
        Assert.Equal(
            PlayerUid.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes["WarFightersRoundOwnerPlayerUID"]);
        Assert.Equal("Caster", updatedSession.Character.Attributes["WarFightersRoundOwnerName"]);
        Assert.Equal(
            remainingPoleHitPoints.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes["WarFightersRoundOwnerScore"]);
        Assert.Equal("Caster", updatedSession.Character.Attributes["WarFightersPoleOwnerName"]);
        Assert.Equal("true", updatedSession.Character.Attributes["WarFightersPoleReset"]);

        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == FirstRightCornerChatType &&
                packet.Content.Contains($"No 1. Caster ({remainingPoleHitPoints})"));
        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.RedColor &&
                packet.ChatType == LegacyMsgMessagePacket.CenterChatType &&
                packet.Content.Contains("The Round Ownered by Caster."));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("Caster", visibleSobNpc.Name);
        Assert.Equal(WarFightersPoleLife, visibleSobNpc.Life);
        Assert.Equal(WarFightersPoleLife, visibleSobNpc.MaxLife);
        var spawn = Assert.Single(ParseSobNpcSpawns(sent));
        Assert.Equal(checked((uint)sobNpc.Id), spawn.Uid);
        Assert.Equal("Caster", spawn.Name);
        Assert.Equal((uint)WarFightersPoleLife, spawn.Hitpoints);
        Assert.Equal((uint)WarFightersPoleLife, spawn.MaxHitpoints);
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);

        var output = log.ToString();
        Assert.Contains("source MsgWarFighters.UpdateScore modeled", output);
        Assert.Contains("source MsgWarFighters.FinishRound modeled", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackTrepilWarPoleNonLethalHitRecordsGuildScoreWithoutDeathResetOrCapture()
    {
        WorldPacketHandlers.ResetLegacyTrepilWarScoreStateForTests();
        var sobNpc = CreateTrepilWarPoleSobNpc();
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.TrepilWarMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.TrepilWarMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: CreateTrepilWarAttributes());
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange((ulong)directHit.Query.Damage, 1UL, checked((ulong)(sobNpc.Life - 1)));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("Trepile", visibleSobNpc.Name);
        Assert.Equal(sobNpc.Life - checked((int)directHit.Query.Damage), visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);
        Assert.Empty(ParseSobNpcSpawns(sent));
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);

        var score = Assert.Single(
            ReadTrepilWarScores(updatedSession, log.ToString()),
            entry => entry.GuildId == 77UL);
        Assert.Equal("SourceGuild", score.GuildName);
        Assert.Equal((long)directHit.Query.Damage, (long)score.Score);

        var rightCornerMessage = Assert.Single(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == FirstRightCornerChatType &&
                packet.Content.Any(content => content.StartsWith("No ", StringComparison.Ordinal)));
        Assert.Contains($"No 1. SourceGuild ({score.Score})", rightCornerMessage.Content);

        var output = log.ToString();
        Assert.Contains("WorldServer updated legacy TrepilWar pole score", output);
        Assert.Contains("source MsgTrepilWar.UpdateScore modeled", output);
        Assert.DoesNotContain("TrepilWar capture", output);
        Assert.DoesNotContain("TrepilWar reset", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackTrepilWarPoleLethalHitFinishesRoundAndResetsPoleToWinnerGuild()
    {
        WorldPacketHandlers.ResetLegacyTrepilWarScoreStateForTests();
        const int remainingPoleHitPoints = 1_250;
        var sobNpc = CreateTrepilWarPoleSobNpc(life: remainingPoleHitPoints, maxLife: TrepilWarPoleLife);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 50_000, maxAttack: 50_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.TrepilWarMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var attributes = CreateTrepilWarAttributes();
        SetTrepilWarHitPointsAttributePairs(attributes, remainingPoleHitPoints);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.TrepilWarMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 50_000,
            extraCharacterAttributes: attributes);
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 50_000)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.Equal(remainingPoleHitPoints, directHit.Query.Damage);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);

        Assert.False(
            updatedSession.Character.Attributes.ContainsKey("TrepilWarScores"),
            "Expected MsgTrepilWar.FinishRound Reset to clear current score table.");
        Assert.Equal("77", updatedSession.Character.Attributes["TrepilWarRoundOwnerGuildID"]);
        Assert.Equal("SourceGuild", updatedSession.Character.Attributes["TrepilWarRoundOwnerGuildName"]);
        Assert.Equal(
            remainingPoleHitPoints.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes["TrepilWarRoundOwnerScore"]);
        Assert.Equal("SourceGuild", updatedSession.Character.Attributes["TrepilWarPoleOwnerName"]);
        Assert.Equal("true", updatedSession.Character.Attributes["TrepilWarPoleReset"]);

        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == FirstRightCornerChatType &&
                packet.Content.Contains($"No 1. SourceGuild ({remainingPoleHitPoints})"));
        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.RedColor &&
                packet.ChatType == LegacyMsgMessagePacket.CenterChatType &&
                packet.Content.Contains("The Round Ownered by guild SourceGuild."));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("SourceGuild", visibleSobNpc.Name);
        Assert.Equal(TrepilWarPoleLife, visibleSobNpc.Life);
        Assert.Equal(TrepilWarPoleLife, visibleSobNpc.MaxLife);
        var spawn = Assert.Single(ParseSobNpcSpawns(sent));
        Assert.Equal(checked((uint)sobNpc.Id), spawn.Uid);
        Assert.Equal("SourceGuild", spawn.Name);
        Assert.Equal((uint)TrepilWarPoleLife, spawn.Hitpoints);
        Assert.Equal((uint)TrepilWarPoleLife, spawn.MaxHitpoints);
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);

        var output = log.ToString();
        Assert.Contains("source MsgTrepilWar.UpdateScore modeled", output);
        Assert.Contains("source MsgTrepilWar.FinishRound modeled", output);
    }

    [Theory]
    [InlineData(LegacySobNpcEventRules.NobilityKingPoleUid, "MsgNobilityPole", LegacyNobilityRanking.RankKing, "King")]
    [InlineData(LegacySobNpcEventRules.NobilityPrincePoleUid, "MsgNobilityPole1", LegacyNobilityRanking.RankPrince, "Prince")]
    [InlineData(LegacySobNpcEventRules.NobilityDukePoleUid, "MsgNobilityPole2", LegacyNobilityRanking.RankDuke, "Duke")]
    [InlineData(LegacySobNpcEventRules.NobilityEarlPoleUid, "MsgNobilityPole3", LegacyNobilityRanking.RankEarl, "Earl")]
    public async Task HandlerDirectPhysicalSobNpcAttackNobilityPoleNonLethalHitRecordsRankScoreWithoutDeathResetOrCapture(
        int poleUid,
        string scheduleName,
        int requiredRank,
        string displayRank)
    {
        WorldPacketHandlers.ResetLegacyNobilityPoleScoreStateForTests();
        var sobNpc = CreateNobilityPoleSobNpc(poleUid, "Enemy" + displayRank + "Pole");
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.NobilityPoleMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.NobilityPoleMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            extraCharacterAttributes: CreateNobilityPoleAttributes(scheduleName, requiredRank));
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange((ulong)directHit.Query.Damage, 1UL, checked((ulong)(sobNpc.Life - 1)));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("Enemy" + displayRank + "Pole", visibleSobNpc.Name);
        Assert.Equal(sobNpc.Life - checked((int)directHit.Query.Damage), visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);
        Assert.Empty(ParseSobNpcSpawns(sent));
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);

        var score = Assert.Single(
            ReadNobilityPoleScores(updatedSession, log.ToString()),
            entry => entry.ScheduleName == scheduleName && entry.PlayerUid == PlayerUid);
        Assert.Equal("Caster", score.PlayerName);
        Assert.Equal((long)directHit.Query.Damage, (long)score.Score);

        var rightCornerMessage = Assert.Single(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == FirstRightCornerChatType &&
                packet.Content.Any(content => content.StartsWith("No ", StringComparison.Ordinal)));
        Assert.Contains($"No 1. Caster ({score.Score})", rightCornerMessage.Content);

        var output = log.ToString();
        Assert.Contains("WorldServer updated legacy NobilityPole score", output);
        Assert.Contains("source MsgNobilityPole.UpdateScore modeled", output);
        Assert.DoesNotContain("NobilityPole capture", output);
        Assert.DoesNotContain("NobilityPole reset", output);
    }

    [Theory]
    [InlineData(LegacySobNpcEventRules.NobilityKingPoleUid, "MsgNobilityPole", LegacyNobilityRanking.RankKing, "King", "The [ King ]Round Ownered by Caster.")]
    [InlineData(LegacySobNpcEventRules.NobilityPrincePoleUid, "MsgNobilityPole1", LegacyNobilityRanking.RankPrince, "Prince", "The [ Prince ]Round Ownered by Caster.")]
    [InlineData(LegacySobNpcEventRules.NobilityDukePoleUid, "MsgNobilityPole2", LegacyNobilityRanking.RankDuke, "Duke", "The [ Duke ]Round Ownered by Caster.")]
    [InlineData(LegacySobNpcEventRules.NobilityEarlPoleUid, "MsgNobilityPole3", LegacyNobilityRanking.RankEarl, "Earl", "The [ Earl ] Round Ownered by Caster.")]
    public async Task HandlerDirectPhysicalSobNpcAttackNobilityPoleLethalHitFinishesRoundAndResetsPoleToWinner(
        int poleUid,
        string scheduleName,
        int requiredRank,
        string displayRank,
        string expectedFinishMessage)
    {
        WorldPacketHandlers.ResetLegacyNobilityPoleScoreStateForTests();
        const int remainingPoleHitPoints = 1_250;
        var sobNpc = CreateNobilityPoleSobNpc(
            poleUid,
            "Enemy" + displayRank + "Pole",
            life: remainingPoleHitPoints,
            maxLife: NobilityPoleLife);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 50_000, maxAttack: 50_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.NobilityPoleMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var attributes = CreateNobilityPoleAttributes(
            scheduleName,
            requiredRank,
            poleHitPoints: remainingPoleHitPoints);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.NobilityPoleMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 50_000,
            extraCharacterAttributes: attributes);
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 50_000)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.Equal(remainingPoleHitPoints, directHit.Query.Damage);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);

        Assert.False(
            updatedSession.Character.Attributes.ContainsKey("NobilityPoleScores"),
            "Expected NobilityPole FinishRound Reset to clear current score table.");
        Assert.Equal(scheduleName, updatedSession.Character.Attributes["NobilityPoleLastFinishedSchedule"]);
        Assert.Equal(displayRank, updatedSession.Character.Attributes["NobilityPoleLastFinishedRank"]);
        Assert.Equal(
            PlayerUid.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes[scheduleName + "RoundOwnerPlayerUID"]);
        Assert.Equal("Caster", updatedSession.Character.Attributes[scheduleName + "RoundOwnerName"]);
        Assert.Equal(
            remainingPoleHitPoints.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes[scheduleName + "RoundOwnerScore"]);
        Assert.Equal("Caster", updatedSession.Character.Attributes[scheduleName + "PoleOwnerName"]);
        Assert.Equal("true", updatedSession.Character.Attributes[scheduleName + "PoleReset"]);

        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == FirstRightCornerChatType &&
                packet.Content.Contains($"No 1. Caster ({remainingPoleHitPoints})"));
        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.RedColor &&
                packet.ChatType == LegacyMsgMessagePacket.CenterChatType &&
                packet.Content.Contains(expectedFinishMessage));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("Caster", visibleSobNpc.Name);
        Assert.Equal(NobilityPoleLife, visibleSobNpc.Life);
        Assert.Equal(NobilityPoleLife, visibleSobNpc.MaxLife);
        var spawn = Assert.Single(ParseSobNpcSpawns(sent));
        Assert.Equal(checked((uint)sobNpc.Id), spawn.Uid);
        Assert.Equal("Caster", spawn.Name);
        Assert.Equal((uint)NobilityPoleLife, spawn.Hitpoints);
        Assert.Equal((uint)NobilityPoleLife, spawn.MaxHitpoints);
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);

        var output = log.ToString();
        Assert.Contains("source MsgNobilityPole.UpdateScore modeled", output);
        Assert.Contains("source MsgNobilityPole.FinishRound modeled", output);
    }

    [Theory]
    [InlineData("MsgWarriorGod", LegacySobNpcEventRules.ClassPoleWarWarriorGodPoleUid, 2_000, "WarriorGod", "WarriorGodScores", "WarriorGod")]
    [InlineData("MsgArcherMaster", LegacySobNpcEventRules.ClassPoleWarArcherMasterPoleUid, 4_000, "ArcherMaster", "ArcherMasterScores", "ArcherMaster")]
    [InlineData("MsgNinjaMaster", LegacySobNpcEventRules.ClassPoleWarNinjaMasterPoleUid, 5_000, "NinjaMaster", "NinjaMasterScores", "NinjaMaster")]
    [InlineData("MsgTrojanMaster", LegacySobNpcEventRules.ClassPoleWarTrojanMasterPoleUid, 1_000, "TrojanMaster", "MsgTrojanMasterScores", "TrojanMaster")]
    [InlineData("MsgTaoistMaster", LegacySobNpcEventRules.ClassPoleWarTaoistMasterPoleUid, 13_002, "TaoistMaster", "TaoistMasterScores", "TaoistMaster")]
    [InlineData("MsgFireMaster", LegacySobNpcEventRules.ClassPoleWarFireMasterPoleUid, 14_002, "FireMaster", "FireMasterScores", "FireMaster")]
    [InlineData("MsgPirateMaster", LegacySobNpcEventRules.ClassPoleWarPirateMasterPoleUid, 7_000, "PirateMaster", "PirateMasterScores", "PirateMaster")]
    [InlineData("MsgKungfuKing", LegacySobNpcEventRules.ClassPoleWarKungfuKingPoleUid, 8_000, "KungfuKing", "KungfuKingScores", "KungfuKing")]
    [InlineData("MsgWindWalker", LegacySobNpcEventRules.ClassPoleWarWindWalkerPoleUid, 16_000, "WindWalker", "WindWalkerScores", "WindWalker")]
    [InlineData("MsgThnderStrike", LegacySobNpcEventRules.ClassPoleWarThnderStrikePoleUid, 9_000, "ThnderStrike", "ThnderStrikeScores", "ThnderStrike")]
    [InlineData("MsgClassPolaWar", LegacySobNpcEventRules.ClassPoleWarClassPolaWarPoleUid, 1_000, "ClassPolaWar", "ClassPolaWarScores", "ClassPolaWar")]
    [InlineData("MsgMonkMaster", LegacySobNpcEventRules.ClassPoleWarMonkMasterPoleUid, 6_000, "MonkMaster", "MonkMasterScores", "MonkMaster")]
    public async Task HandlerDirectPhysicalSobNpcAttackClassPoleWarPoleNonLethalHitRecordsPlayerScoreWithoutDeathResetOrCapture(
        string scheduleName,
        int poleUid,
        int classCode,
        string poleName,
        string scoresAttribute,
        string logName)
    {
        ResetLegacyClassPoleScoreState(scheduleName);
        var sobNpc = CreateClassPoleWarPoleSobNpc(poleUid, poleName);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 1_000, maxAttack: 1_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.ClassPoleWarMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.ClassPoleWarMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 1_000,
            classCode: classCode,
            extraCharacterAttributes: CreateClassPoleWarAttributes(scheduleName));
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 1_000)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.InRange((ulong)directHit.Query.Damage, 1UL, checked((ulong)(sobNpc.Life - 1)));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(poleName, visibleSobNpc.Name);
        Assert.Equal(sobNpc.Life - checked((int)directHit.Query.Damage), visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);
        Assert.Empty(ParseSobNpcSpawns(sent));
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);

        var score = Assert.Single(
            ReadClassPolePlayerScores(updatedSession, scoresAttribute, log.ToString()),
            entry => entry.PlayerUid == PlayerUid);
        Assert.Equal("Caster", score.PlayerName);
        Assert.Equal((long)directHit.Query.Damage, (long)score.Score);

        var rightCornerMessage = Assert.Single(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == FirstRightCornerChatType &&
                packet.Content.Any(content => content.StartsWith("No ", StringComparison.Ordinal)));
        Assert.Contains($"No 1. Caster ({score.Score})", rightCornerMessage.Content);

        var output = log.ToString();
        Assert.Contains("WorldServer updated legacy " + logName + " pole score", output);
        Assert.Contains("source " + scheduleName + ".UpdateScore modeled", output);
        Assert.DoesNotContain(logName + " capture", output);
        Assert.DoesNotContain(logName + " reset", output);
    }

    [Theory]
    [InlineData("MsgWarriorGod", LegacySobNpcEventRules.ClassPoleWarWarriorGodPoleUid, 2_000, "WarriorGod", "WarriorGodScores", "WarriorGod", LegacyMsgMessagePacket.RedColor)]
    [InlineData("MsgArcherMaster", LegacySobNpcEventRules.ClassPoleWarArcherMasterPoleUid, 4_000, "ArcherMaster", "ArcherMasterScores", "ArcherMaster", LegacyMsgMessagePacket.YellowColor)]
    [InlineData("MsgNinjaMaster", LegacySobNpcEventRules.ClassPoleWarNinjaMasterPoleUid, 5_000, "NinjaMaster", "NinjaMasterScores", "NinjaMaster", LegacyMsgMessagePacket.RedColor)]
    [InlineData("MsgTrojanMaster", LegacySobNpcEventRules.ClassPoleWarTrojanMasterPoleUid, 1_000, "TrojanMaster", "MsgTrojanMasterScores", "TrojanMaster", LegacyMsgMessagePacket.RedColor)]
    [InlineData("MsgTaoistMaster", LegacySobNpcEventRules.ClassPoleWarTaoistMasterPoleUid, 13_002, "TaoistMaster", "TaoistMasterScores", "TaoistMaster", LegacyMsgMessagePacket.RedColor)]
    [InlineData("MsgFireMaster", LegacySobNpcEventRules.ClassPoleWarFireMasterPoleUid, 14_002, "FireMaster", "FireMasterScores", "FireMaster", LegacyMsgMessagePacket.RedColor)]
    [InlineData("MsgPirateMaster", LegacySobNpcEventRules.ClassPoleWarPirateMasterPoleUid, 7_000, "PirateMaster", "PirateMasterScores", "PirateMaster", LegacyMsgMessagePacket.RedColor)]
    [InlineData("MsgKungfuKing", LegacySobNpcEventRules.ClassPoleWarKungfuKingPoleUid, 8_000, "KungfuKing", "KungfuKingScores", "KungfuKing", LegacyMsgMessagePacket.RedColor)]
    [InlineData("MsgWindWalker", LegacySobNpcEventRules.ClassPoleWarWindWalkerPoleUid, 16_000, "WindWalker", "WindWalkerScores", "WindWalker", LegacyMsgMessagePacket.RedColor)]
    [InlineData("MsgThnderStrike", LegacySobNpcEventRules.ClassPoleWarThnderStrikePoleUid, 9_000, "ThnderStrike", "ThnderStrikeScores", "ThnderStrike", LegacyMsgMessagePacket.RedColor)]
    [InlineData("MsgClassPolaWar", LegacySobNpcEventRules.ClassPoleWarClassPolaWarPoleUid, 1_000, "ClassPolaWar", "ClassPolaWarScores", "ClassPolaWar", LegacyMsgMessagePacket.RedColor)]
    [InlineData("MsgMonkMaster", LegacySobNpcEventRules.ClassPoleWarMonkMasterPoleUid, 6_000, "MonkMaster", "MonkMasterScores", "MonkMaster", LegacyMsgMessagePacket.RedColor)]
    public async Task HandlerDirectPhysicalSobNpcAttackClassPoleWarPoleLethalHitFinishesRoundAndResetsPoleToWinner(
        string scheduleName,
        int poleUid,
        int classCode,
        string poleName,
        string scoresAttribute,
        string logName,
        uint expectedCenterColor)
    {
        ResetLegacyClassPoleScoreState(scheduleName);
        const int remainingPoleHitPoints = 1_250;
        var sobNpc = CreateClassPoleWarPoleSobNpc(
            poleUid,
            poleName,
            life: remainingPoleHitPoints,
            maxLife: ClassPoleWarPoleLife);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 50_000, maxAttack: 50_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [LegacySobNpcEventRules.ClassPoleWarMapId]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var attributes = CreateClassPoleWarAttributes(
            scheduleName,
            poleHitPoints: remainingPoleHitPoints);
        var session = CreateSession(
            spellIds: [],
            mapId: LegacySobNpcEventRules.ClassPoleWarMapId,
            x: checked((ushort)(sobNpc.X - 1)),
            y: checked((ushort)sobNpc.Y),
            strength: 50_000,
            classCode: classCode,
            extraCharacterAttributes: attributes);
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 50_000)).ToFrameBytes()));

        var directHit = Assert.Single(
            ParseAttacks(sent),
            attack => attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == checked((uint)sobNpc.Id) &&
                attack.Query.AttackType == MsgAttackType.Physical);
        Assert.Equal(remainingPoleHitPoints, directHit.Query.Damage);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);

        Assert.False(
            updatedSession.Character.Attributes.ContainsKey(scoresAttribute),
            "Expected " + scheduleName + ".FinishRound Reset to clear current score table.");
        Assert.Equal(
            PlayerUid.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes[scheduleName + "RoundOwnerPlayerUID"]);
        Assert.Equal("Caster", updatedSession.Character.Attributes[scheduleName + "RoundOwnerName"]);
        Assert.Equal(
            remainingPoleHitPoints.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes[scheduleName + "RoundOwnerScore"]);
        Assert.Equal("Caster", updatedSession.Character.Attributes[scheduleName + "PoleOwnerName"]);
        Assert.Equal("true", updatedSession.Character.Attributes[scheduleName + "PoleReset"]);

        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                packet.ChatType == FirstRightCornerChatType &&
                packet.Content.Contains($"No 1. Caster ({remainingPoleHitPoints})"));
        Assert.Contains(
            ParseLegacyMessages(sent),
            packet => packet.Color == expectedCenterColor &&
                packet.ChatType == LegacyMsgMessagePacket.CenterChatType &&
                packet.Content.Contains("The Round Ownered by Caster."));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("Caster", visibleSobNpc.Name);
        Assert.Equal(ClassPoleWarPoleLife, visibleSobNpc.Life);
        Assert.Equal(ClassPoleWarPoleLife, visibleSobNpc.MaxLife);
        var spawn = Assert.Single(ParseSobNpcSpawns(sent));
        Assert.Equal(checked((uint)sobNpc.Id), spawn.Uid);
        Assert.Equal("Caster", spawn.Name);
        Assert.Equal((uint)ClassPoleWarPoleLife, spawn.Hitpoints);
        Assert.Equal((uint)ClassPoleWarPoleLife, spawn.MaxHitpoints);
        Assert.Empty(ParseNewSynWarPackets(sent));
        Assert.Empty(ParseCaptureTheFlagUpdates(sent));
        Assert.DoesNotContain(ParseAttacks(sent), attack => attack.Query.AttackType == MsgAttackType.Death);

        var output = log.ToString();
        Assert.Contains("source " + scheduleName + ".UpdateScore modeled", output);
        Assert.Contains("source " + scheduleName + ".FinishRound modeled", output);
        Assert.Contains("WorldServer finished legacy " + logName + " pole round", output);
    }

    [Fact]
    public async Task HandlerDirectPhysicalSobNpcAttackCaptureTheFlagBaseDeathCapturesBaseAndSendsOccupiedBase()
    {
        var sobNpc = CreateSobNpc(
            id: 9_001,
            name: "EnemyBase",
            kind: LegacySobNpcEventRules.PoleKind,
            mapId: 2_057,
            x: PlayerX + 1,
            y: PlayerY,
            life: 1,
            maxLife: 50_000);
        var worldData = CreateWorldData(
            items: [CreateClubItemType(minAttack: 25_000, maxAttack: 25_000)],
            magicRows: [],
            sobNpcs: [sobNpc],
            mapIds: [2_057]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var attributes = CreateGuildAttributes();
        attributes["CaptureTheFlagBases"] = "9001,206,230,88,0,1";
        var session = CreateSession(
            spellIds: [],
            mapId: 2_057,
            x: PlayerX,
            y: PlayerY,
            strength: 25_000,
            extraCharacterAttributes: attributes);
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(
                sobNpc,
                MsgAttackType.Physical,
                damage: 25_000)).ToFrameBytes()));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal("SourceGuild", visibleSobNpc.Name);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal("9001,206,230,77,0,1", updatedSession.Character.Attributes["CaptureTheFlagBases"]);

        var spawn = Assert.Single(ParseSobNpcSpawns(sent));
        Assert.Equal(checked((uint)sobNpc.Id), spawn.Uid);
        Assert.Equal("SourceGuild", spawn.Name);
        Assert.Equal((uint)sobNpc.MaxLife, spawn.Hitpoints);

        var occupiedBase = Assert.Single(
            ParseCaptureTheFlagUpdates(sent),
            packet => packet.Mode == LegacyCaptureTheFlagUpdateMode.OccupiedBase);
        Assert.Equal((uint)(sobNpc.Id % 100), occupiedBase.Parameter);
        Assert.Equal(1, occupiedBase.Count);

        var output = log.ToString();
        Assert.Contains("WorldServer sent legacy CaptureTheFlag base capture", output);
        Assert.Contains("source MsgCaptureTheFlag.UpdateFlagScore killed base branch", output);
    }

    private static MsgAttackQuery CreateSobNpcAttack(
        SobNpc sobNpc,
        MsgAttackType attackType,
        int damage,
        uint playerUid = PlayerUid)
    {
        return new MsgAttackQuery(
            Uid: playerUid,
            OpponentUid: checked((uint)sobNpc.Id),
            X: checked((ushort)sobNpc.X),
            Y: checked((ushort)sobNpc.Y),
            AttackType: attackType,
            Damage: damage,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0);
    }

    private static WorldSession CreateSession(
        IReadOnlyList<ushort> spellIds,
        uint playerUid = PlayerUid,
        bool includeKineticSparkStatus = false,
        IReadOnlyDictionary<ushort, uint>? archerPreProcRateRolls = null,
        int mapId = MapId,
        ushort x = PlayerX,
        ushort y = PlayerY,
        int strength = 1_000,
        int classCode = 142,
        IReadOnlyDictionary<string, string>? extraCharacterAttributes = null)
    {
        return new WorldSession(
            AccountId: 1,
            Username: "caster",
            CreateCharacter(
                playerUid,
                includeKineticSparkStatus,
                archerPreProcRateRolls,
                mapId,
                x,
                y,
                strength,
                classCode,
                extraCharacterAttributes),
            CreateEquippedClub(),
            new PlayerSpellSnapshot(
                spellIds.Select(spellId => new PlayerSpellSnapshotEntry(
                        spellId,
                        Level: 0,
                        Experience: 0,
                        PreviousLevel: 0,
                        SoulLevel: 0,
                        UseSpellSoul: 0))
                    .ToArray()),
            PlayerProficiencySnapshot.Empty,
            CombatGear: CombatGearSnapshot.Empty);
    }

    private static CharacterSnapshot CreateCharacter(
        uint playerUid,
        bool includeKineticSparkStatus,
        IReadOnlyDictionary<ushort, uint>? archerPreProcRateRolls,
        int mapId,
        ushort x,
        ushort y,
        int strength,
        int classCode,
        IReadOnlyDictionary<string, string>? extraCharacterAttributes)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "99",
        };
        if (includeKineticSparkStatus)
        {
            attributes["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag);
        }

        if (archerPreProcRateRolls is not null)
        {
            foreach (var (spellId, roll) in archerPreProcRateRolls)
            {
                attributes[
                    RuntimeLegacyArcherPreProcRateRollAttribute + "." + spellId.ToString(CultureInfo.InvariantCulture)] =
                    roll.ToString(CultureInfo.InvariantCulture);
            }
        }
        if (extraCharacterAttributes is not null)
        {
            foreach (var (key, value) in extraCharacterAttributes)
            {
                attributes[key] = value;
            }
        }

        return new CharacterSnapshot(
            Uid: playerUid,
            Name: "Caster",
            Body: 1_003,
            Face: 0,
            Hair: 0,
            Class: classCode,
            FirstClass: classCode,
            SecondClass: 0,
            Reborn: 0,
            Level: 120,
            Experience: 0,
            mapId,
            x,
            y,
            PreviousMapId: mapId,
            PreviousMapX: x,
            PreviousMapY: y,
            Strength: strength,
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
            SourceFile: "fixture://direct-sob-npc-physical-runtime",
            Attributes: attributes,
            PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private static SobNpc CreateSobNpc(
        int id,
        string name,
        int kind,
        int mapId = MapId,
        ushort x = PlayerX + 1,
        ushort y = PlayerY,
        int life = 50_000,
        int maxLife = 50_000,
        int lookface = 1_137)
    {
        var fields = new[]
        {
            id.ToString(CultureInfo.InvariantCulture),
            name,
            kind.ToString(CultureInfo.InvariantCulture),
            lookface.ToString(CultureInfo.InvariantCulture),
            mapId.ToString(CultureInfo.InvariantCulture),
            x.ToString(CultureInfo.InvariantCulture),
            y.ToString(CultureInfo.InvariantCulture),
            life.ToString(CultureInfo.InvariantCulture),
            maxLife.ToString(CultureInfo.InvariantCulture),
            "17",
            "1",
        };

        return new SobNpc(
            Id: id,
            Name: name,
            Kind: kind,
            Lookface: lookface,
            MapId: mapId,
            X: x,
            Y: y,
            Life: life,
            MaxLife: maxLife,
            Unknown9: 17,
            Unknown10: 1,
            LocalizedName: null,
            SourceLine: 1,
            RawLine: string.Join(',', fields),
            Fields: fields);
    }

    private static SobNpc CreateWarOfPlayersPoleSobNpc(int life, int maxLife)
    {
        return CreateSobNpc(
            id: LegacySobNpcEventRules.WarOfPlayersPoleUid,
            name: "WarOfPlayers",
            kind: LegacySobNpcEventRules.PoleKind,
            mapId: LegacySobNpcEventRules.WarOfPlayersMapId,
            x: 45,
            y: 45,
            life: life,
            maxLife: maxLife);
    }

    private static SobNpc CreateTopPolePoleSobNpc(
        int life = TopPoleLife,
        int maxLife = TopPoleLife)
    {
        return CreateSobNpc(
            id: LegacySobNpcEventRules.TopPoleUid,
            name: "TestSrv",
            kind: LegacySobNpcEventRules.PoleKind,
            mapId: LegacySobNpcEventRules.TopPoleMapId,
            x: 70,
            y: 67,
            life: life,
            maxLife: maxLife,
            lookface: 1_137);
    }

    private static SobNpc CreateWarFightersPoleSobNpc(
        int life = WarFightersPoleLife,
        int maxLife = WarFightersPoleLife)
    {
        return CreateSobNpc(
            id: LegacySobNpcEventRules.WarFightersPoleUid,
            name: "WarFighters",
            kind: LegacySobNpcEventRules.PoleKind,
            mapId: LegacySobNpcEventRules.WarFightersMapId,
            x: 166,
            y: 124,
            life: life,
            maxLife: maxLife,
            lookface: 1_137);
    }

    private static SobNpc CreateTrepilWarPoleSobNpc(
        int life = TrepilWarPoleLife,
        int maxLife = TrepilWarPoleLife)
    {
        return CreateSobNpc(
            id: LegacySobNpcEventRules.TrepilWarPoleUid,
            name: "Trepile",
            kind: LegacySobNpcEventRules.PoleKind,
            mapId: LegacySobNpcEventRules.TrepilWarMapId,
            x: 221,
            y: 221,
            life: life,
            maxLife: maxLife,
            lookface: 1_137);
    }

    private static SobNpc CreateNobilityPoleSobNpc(
        int poleUid,
        string poleName,
        int life = NobilityPoleLife,
        int maxLife = NobilityPoleLife)
    {
        return CreateSobNpc(
            id: poleUid,
            name: poleName,
            kind: LegacySobNpcEventRules.PoleKind,
            mapId: LegacySobNpcEventRules.NobilityPoleMapId,
            x: 85,
            y: 70,
            life: life,
            maxLife: maxLife,
            lookface: 1_137);
    }

    private static SobNpc CreateClassPoleWarPoleSobNpc(
        int poleUid,
        string poleName,
        int life = ClassPoleWarPoleLife,
        int maxLife = ClassPoleWarPoleLife)
    {
        return CreateSobNpc(
            id: poleUid,
            name: poleName,
            kind: LegacySobNpcEventRules.PoleKind,
            mapId: LegacySobNpcEventRules.ClassPoleWarMapId,
            x: 45,
            y: 45,
            life: life,
            maxLife: maxLife,
            lookface: 1_137);
    }

    private static SobNpc CreateEmperorWarPoleSobNpc(
        int life = EmperorWarPoleLife,
        int maxLife = EmperorWarPoleLife)
    {
        return CreateSobNpc(
            id: LegacySobNpcEventRules.EmperorWarPoleUid,
            name: "Emperrors",
            kind: LegacySobNpcEventRules.PoleKind,
            mapId: LegacySobNpcEventRules.EmperorWarMapId,
            x: 119,
            y: 120,
            life: life,
            maxLife: maxLife);
    }

    private static SobNpc CreateGuildWarScorePoleSobNpc(int life, int maxLife)
    {
        return CreateSobNpc(
            id: LegacySobNpcEventRules.GuildWarScorePoleUid,
            name: "GuildWarSocre",
            kind: LegacySobNpcEventRules.PoleKind,
            mapId: LegacySobNpcEventRules.GuildWarScoreMapId,
            x: 165,
            y: 219,
            life: life,
            maxLife: maxLife);
    }

    private static SobNpc CreateClanWarScorePoleSobNpc(
        int life = ClanWarScorePoleLife,
        int maxLife = ClanWarScorePoleLife)
    {
        return CreateSobNpc(
            id: LegacySobNpcEventRules.ClanWarScorePoleUid,
            name: "ClanWarSocre",
            kind: LegacySobNpcEventRules.PoleKind,
            mapId: LegacySobNpcEventRules.ClanWarScoreMapId,
            x: 221,
            y: 222,
            life: life,
            maxLife: maxLife);
    }

    private static SobNpc CreateClanWarCurrentPoleSobNpc()
    {
        return CreateSobNpc(
            id: LegacySobNpcEventRules.ClanWarPoleUid,
            name: "ClanWarPole",
            kind: LegacySobNpcEventRules.PoleKind,
            mapId: 1_505,
            x: 165,
            y: 219,
            life: ClanWarPoleLife,
            maxLife: ClanWarPoleLife);
    }

    private static SobNpc CreateEliteGuildWarPoleSobNpc(int life, int maxLife)
    {
        return CreateSobNpc(
            id: LegacySobNpcEventRules.EliteGuildWarPoleUid,
            name: "EliteGuildWar",
            kind: LegacySobNpcEventRules.PoleKind,
            mapId: LegacySobNpcEventRules.EliteGuildWarMapId,
            x: 125,
            y: 124,
            life: life,
            maxLife: maxLife,
            lookface: 1_137);
    }

    private static SobNpc CreateChampionsOfWarrPoleSobNpc(int life, int maxLife)
    {
        return CreateSobNpc(
            id: LegacySobNpcEventRules.ChampionsOfWarrPoleUid,
            name: "Champions",
            kind: LegacySobNpcEventRules.PoleKind,
            mapId: LegacySobNpcEventRules.ChampionsOfWarrMapId,
            x: 55,
            y: 54,
            life: life,
            maxLife: maxLife,
            lookface: 1_137);
    }

    private static SobNpc CreateUnionWarPoleSobNpc(
        int life = UnionWarPoleLife,
        int maxLife = UnionWarPoleLife)
    {
        string[] fields =
        [
            LegacySobNpcEventRules.UnionWarPoleUid.ToString(CultureInfo.InvariantCulture),
            "UnionWar",
            LegacySobNpcEventRules.StakeKind.ToString(CultureInfo.InvariantCulture),
            "8684",
            UnionWarPoleMapId.ToString(CultureInfo.InvariantCulture),
            "349",
            "406",
            life.ToString(CultureInfo.InvariantCulture),
            maxLife.ToString(CultureInfo.InvariantCulture),
            "21",
            "1",
        ];

        return new SobNpc(
            Id: LegacySobNpcEventRules.UnionWarPoleUid,
            Name: "UnionWar",
            Kind: LegacySobNpcEventRules.StakeKind,
            Lookface: 8_684,
            MapId: UnionWarPoleMapId,
            X: 349,
            Y: 406,
            Life: life,
            MaxLife: maxLife,
            Unknown9: 21,
            Unknown10: 1,
            LocalizedName: null,
            SourceLine: 1,
            RawLine: string.Join(',', fields),
            Fields: fields);
    }

    private static SobNpc CreateGuild6PoleWarPoleSobNpc(int poleUid, int life, int maxLife)
    {
        var probe = CreateSobNpc(
            id: poleUid,
            name: "Guild6PoleWarProbe",
            kind: LegacySobNpcEventRules.PoleKind,
            mapId: LegacySobNpcEventRules.Guild6PoleWarMapId,
            life: life,
            maxLife: maxLife);
        Assert.True(LegacySobNpcEventRules.TryGetGuild6PoleWarPoleSchedule(
            probe,
            out _,
            out var displayName));
        return CreateSobNpc(
            id: poleUid,
            name: displayName,
            kind: LegacySobNpcEventRules.PoleKind,
            mapId: LegacySobNpcEventRules.Guild6PoleWarMapId,
            x: PlayerX + 1,
            y: PlayerY,
            life: life,
            maxLife: maxLife);
    }

    private static Dictionary<string, string> CreateGuildAttributes()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["GuildID"] = "77",
            ["GuildName"] = "SourceGuild",
            ["GuildRank"] = "100",
        };
    }

    private static Dictionary<string, string> CreateSuperGuildWarPoleAttributes()
    {
        var attributes = CreateGuildAttributes();
        SetSuperGuildWarAttributeTriplets(attributes, poleHitPoints: 50_000, winnerGuildId: 777);
        SetSuperGuildWarAttributeTriplet(attributes, "Process", "Alive");
        SetSuperGuildWarAttributeTriplet(attributes, "Proces", "Alive");
        return attributes;
    }

    private static Dictionary<string, string> CreateEliteGuildWarPoleAttributes()
    {
        var attributes = CreateGuildAttributes();
        attributes["GuildId"] = attributes["GuildID"];
        attributes["LegacyGuildName"] = attributes["GuildName"];
        SetEliteGuildWarPoleHitPointsAttributePairs(attributes, 50_000);
        attributes["LegacyEliteGuildWarProcess"] = "Alive";
        attributes["EliteGuildWarProcess"] = "Alive";
        attributes["RuntimeEliteGuildWarProcess"] = "Alive";
        return attributes;
    }

    private static Dictionary<string, string> CreateChampionsOfWarrPoleAttributes()
    {
        var attributes = CreateGuildAttributes();
        attributes["GuildId"] = attributes["GuildID"];
        attributes["LegacyGuildName"] = attributes["GuildName"];
        SetChampionsOfWarrPoleHitPointsAttributePairs(attributes, 250_000_000);
        SetChampionsOfWarrProcessAttributeTriplet(attributes, "Process", "Alive");
        SetChampionsOfWarrProcessAttributeTriplet(attributes, "Proces", "Alive");
        return attributes;
    }

    private static Dictionary<string, string> CreateUnionWarPoleAttributes(
        ulong unionId = 42,
        string unionName = "SourceUnion",
        ulong guildId = 77)
    {
        var poleHitPoints = UnionWarPoleLife.ToString(CultureInfo.InvariantCulture);
        var unionIdText = unionId.ToString(CultureInfo.InvariantCulture);
        var guildIdText = guildId.ToString(CultureInfo.InvariantCulture);
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["UnionID"] = unionIdText,
            ["UnionUID"] = unionIdText,
            ["MyUnionID"] = unionIdText,
            ["MyUnionUID"] = unionIdText,
            ["UnionName"] = unionName,
            ["LegacyUnionName"] = unionName,
            ["GuildID"] = guildIdText,
            ["GuildId"] = guildIdText,
            ["GuildUnionID"] = unionIdText,
            ["GuildUnionId"] = unionIdText,
            ["LegacyUnionWarPoleHitPoints"] = poleHitPoints,
            ["LegacyUnionWarPoleHitpoints"] = poleHitPoints,
            ["UnionWarPoleHitPoints"] = poleHitPoints,
            ["UnionWarPoleHitpoints"] = poleHitPoints,
            ["LegacyUnionWarWinnerUnionID"] = "777",
            ["LegacyUnionWarWinnerUnionId"] = "777",
            ["UnionWarWinnerUnionID"] = "777",
            ["UnionWarWinnerUnionId"] = "777",
            ["LegacyUnionWarProcess"] = "Alive",
            ["UnionWarProcess"] = "Alive",
            ["RuntimeUnionWarProcess"] = "Alive",
            ["LegacyUnionWarProces"] = "Alive",
            ["UnionWarProces"] = "Alive",
        };
    }

    private static void SetUnionWarPoleHitPointsAttributePairs(
        Dictionary<string, string> attributes,
        int hitPoints)
    {
        var value = hitPoints.ToString(CultureInfo.InvariantCulture);
        attributes["LegacyUnionWarPoleHitPoints"] = value;
        attributes["LegacyUnionWarPoleHitpoints"] = value;
        attributes["UnionWarPoleHitPoints"] = value;
        attributes["UnionWarPoleHitpoints"] = value;
    }

    private static Dictionary<string, string> CreateGuild6PoleWarPoleAttributes(string scheduleName)
    {
        var attributes = CreateGuildAttributes();
        SetGuild6PoleWarScheduleAttributePairs(attributes, scheduleName, "HitPoints", 50_000);
        SetGuild6PoleWarScheduleAttributePairs(attributes, scheduleName, "Hitpoints", 50_000);
        SetGuild6PoleWarScheduleAttributePairs(attributes, scheduleName, "PoleHitPoints", 50_000);
        SetGuild6PoleWarScheduleAttributePairs(attributes, scheduleName, "PoleHitpoints", 50_000);
        SetGuild6PoleWarScheduleAttributeTriplet(attributes, scheduleName, "Process", "Alive");
        SetGuild6PoleWarScheduleAttributeTriplet(attributes, scheduleName, "Proces", "Alive");
        return attributes;
    }

    private static Dictionary<string, string> CreateGuildWarScorePoleAttributes()
    {
        var attributes = CreateGuildAttributes();
        attributes["GuildId"] = attributes["GuildID"];
        attributes["LegacyGuildName"] = attributes["GuildName"];
        SetGuildWarScorePoleHitPointsAttributePairs(attributes, 50_000);
        attributes["LegacyMsgGuildWarScoreProcess"] = "Alive";
        attributes["MsgGuildWarScoreProcess"] = "Alive";
        attributes["RuntimeMsgGuildWarScoreProcess"] = "Alive";
        attributes["LegacyGuildWarScoreProcess"] = "Alive";
        attributes["GuildWarScoreProcess"] = "Alive";
        attributes["RuntimeGuildWarScoreProcess"] = "Alive";
        return attributes;
    }

    private static Dictionary<string, string> CreateClanWarScorePoleAttributes()
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ClanID"] = "501",
            ["ClanUID"] = "501",
            ["ClanName"] = "SourceClan",
            ["LegacyClanName"] = "SourceClan",
        };
        SetClanWarScorePoleHitPointsAttributePairs(attributes, ClanWarScorePoleLife);
        attributes["LegacyMsgClanWarScoreProcess"] = "Alive";
        attributes["MsgClanWarScoreProcess"] = "Alive";
        attributes["RuntimeMsgClanWarScoreProcess"] = "Alive";
        attributes["LegacyClanWarScoreProcess"] = "Alive";
        attributes["ClanWarScoreProcess"] = "Alive";
        attributes["RuntimeClanWarScoreProcess"] = "Alive";
        return attributes;
    }

    private static Dictionary<string, string> CreateClanWarCurrentPoleAttributes()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ClanID"] = "501",
            ["ClanUID"] = "501",
            ["ClanName"] = "SourceClan",
            ["LegacyClanName"] = "SourceClan",
            ["LegacyClanWarProcess"] = "Alive",
            ["ClanWarProcess"] = "Alive",
            ["LegacyClanWarProces"] = "Alive",
            ["ClanWarProces"] = "Alive",
            ["LegacyClanWarCurrentProcess"] = "Alive",
            ["ClanWarCurrentProcess"] = "Alive",
            ["LegacyClanWarCurrentProces"] = "Alive",
            ["ClanWarCurrentProces"] = "Alive",
            ["LegacyClanWarCityProcess"] = "Alive",
            ["ClanWarCityProcess"] = "Alive",
            ["LegacyClanWarCityProces"] = "Alive",
            ["ClanWarCityProces"] = "Alive",
            ["LegacyClanWarActive"] = "true",
            ["ClanWarActive"] = "true",
            ["LegacyClanWarInWar"] = "true",
            ["ClanWarInWar"] = "true",
            ["LegacyClanWarWinnerClanID"] = "777",
            ["ClanWarWinnerClanID"] = "777",
            ["LegacyClanWarCity"] = "Twin",
            ["ClanWarCity"] = "Twin",
        };
    }

    private static Dictionary<string, string> CreateWarOfPlayersPoleAttributes()
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        SetWarOfPlayersPoleHitPointsAttributePairs(attributes, 50_000);
        attributes["LegacyMsgWarOfPlayersProcess"] = "Alive";
        attributes["MsgWarOfPlayersProcess"] = "Alive";
        attributes["RuntimeMsgWarOfPlayersProcess"] = "Alive";
        attributes["LegacyWarOfPlayersProcess"] = "Alive";
        attributes["WarOfPlayersProcess"] = "Alive";
        attributes["RuntimeWarOfPlayersProcess"] = "Alive";
        return attributes;
    }

    private static Dictionary<string, string> CreateTopPoleAttributes()
    {
        var attributes = CreateGuildAttributes();
        SetTopPoleHitPointsAttributePairs(attributes, TopPoleLife);
        attributes["LegacyMsgTopPoleProcess"] = "Alive";
        attributes["MsgTopPoleProcess"] = "Alive";
        attributes["RuntimeMsgTopPoleProcess"] = "Alive";
        attributes["LegacyTopPoleProcess"] = "Alive";
        attributes["TopPoleProcess"] = "Alive";
        attributes["RuntimeTopPoleProcess"] = "Alive";
        attributes["LegacyMsgTopPoleProces"] = "Alive";
        attributes["MsgTopPoleProces"] = "Alive";
        attributes["RuntimeMsgTopPoleProces"] = "Alive";
        attributes["LegacyTopPoleProces"] = "Alive";
        attributes["TopPoleProces"] = "Alive";
        attributes["RuntimeTopPoleProces"] = "Alive";
        return attributes;
    }

    private static Dictionary<string, string> CreateWarFightersAttributes()
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        SetWarFightersHitPointsAttributePairs(attributes, WarFightersPoleLife);
        attributes["LegacyMsgWarFightersProcess"] = "Alive";
        attributes["MsgWarFightersProcess"] = "Alive";
        attributes["RuntimeMsgWarFightersProcess"] = "Alive";
        attributes["LegacyWarFightersProcess"] = "Alive";
        attributes["WarFightersProcess"] = "Alive";
        attributes["RuntimeWarFightersProcess"] = "Alive";
        attributes["LegacyMsgWarFightersProces"] = "Alive";
        attributes["MsgWarFightersProces"] = "Alive";
        attributes["RuntimeMsgWarFightersProces"] = "Alive";
        attributes["LegacyWarFightersProces"] = "Alive";
        attributes["WarFightersProces"] = "Alive";
        attributes["RuntimeWarFightersProces"] = "Alive";
        return attributes;
    }

    private static Dictionary<string, string> CreateTrepilWarAttributes()
    {
        var attributes = CreateGuildAttributes();
        attributes["GuildId"] = attributes["GuildID"];
        attributes["LegacyGuildName"] = attributes["GuildName"];
        SetTrepilWarHitPointsAttributePairs(attributes, TrepilWarPoleLife);
        attributes["LegacyMsgTrepilWarProcess"] = "Alive";
        attributes["MsgTrepilWarProcess"] = "Alive";
        attributes["RuntimeMsgTrepilWarProcess"] = "Alive";
        attributes["LegacyTrepilWarProcess"] = "Alive";
        attributes["TrepilWarProcess"] = "Alive";
        attributes["RuntimeTrepilWarProcess"] = "Alive";
        attributes["LegacyMsgTrepilWarProces"] = "Alive";
        attributes["MsgTrepilWarProces"] = "Alive";
        attributes["RuntimeMsgTrepilWarProces"] = "Alive";
        attributes["LegacyTrepilWarProces"] = "Alive";
        attributes["TrepilWarProces"] = "Alive";
        attributes["RuntimeTrepilWarProces"] = "Alive";
        return attributes;
    }

    private static Dictionary<string, string> CreateNobilityPoleAttributes(
        string scheduleName,
        int nobilityRank,
        int poleHitPoints = NobilityPoleLife,
        string? process = "Alive")
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["NobilityRank"] = nobilityRank.ToString(CultureInfo.InvariantCulture),
        };
        SetNobilityPoleHitPointsAttributePairs(attributes, poleHitPoints);
        SetNobilityPoleScheduleAttributePair(attributes, scheduleName, "PoleHitPoints", poleHitPoints);
        SetNobilityPoleScheduleAttributePair(attributes, scheduleName, "PoleHitpoints", poleHitPoints);
        if (process is not null)
        {
            SetNobilityPoleScheduleAttributePair(attributes, scheduleName, "Process", process);
            SetNobilityPoleScheduleAttributePair(attributes, scheduleName, "Proces", process);
            attributes["LegacyNobilityPoleProcess"] = process;
            attributes["NobilityPoleProcess"] = process;
            attributes["RuntimeNobilityPoleProcess"] = process;
            attributes["LegacyNobilityPoleProces"] = process;
            attributes["NobilityPoleProces"] = process;
            attributes["RuntimeNobilityPoleProces"] = process;
        }

        return attributes;
    }

    private static void ResetLegacyClassPoleScoreState(string scheduleName)
    {
        if (scheduleName == "MsgWarriorGod")
        {
            WorldPacketHandlers.ResetLegacyWarriorGodScoreStateForTests();
            return;
        }

        if (scheduleName == "MsgArcherMaster")
        {
            WorldPacketHandlers.ResetLegacyArcherPoleScoreStateForTests();
            return;
        }

        if (scheduleName == "MsgNinjaMaster")
        {
            WorldPacketHandlers.ResetLegacyNinjaMasterScoreStateForTests();
            return;
        }

        if (scheduleName == "MsgTrojanMaster")
        {
            WorldPacketHandlers.ResetLegacyTrojanPoleScoreStateForTests();
            return;
        }

        if (scheduleName == "MsgTaoistMaster")
        {
            WorldPacketHandlers.ResetLegacyTaoistMasterScoreStateForTests();
            return;
        }

        if (scheduleName == "MsgFireMaster")
        {
            WorldPacketHandlers.ResetLegacyFireMasterScoreStateForTests();
            return;
        }

        if (scheduleName == "MsgPirateMaster")
        {
            WorldPacketHandlers.ResetLegacyPirateMasterScoreStateForTests();
            return;
        }

        if (scheduleName == "MsgKungfuKing")
        {
            WorldPacketHandlers.ResetLegacyKungfuKingScoreStateForTests();
            return;
        }

        if (scheduleName == "MsgWindWalker")
        {
            WorldPacketHandlers.ResetLegacyWindWalkerScoreStateForTests();
            return;
        }

        if (scheduleName == "MsgThnderStrike")
        {
            WorldPacketHandlers.ResetLegacyThnderStrikeScoreStateForTests();
            return;
        }

        if (scheduleName == "MsgClassPolaWar")
        {
            WorldPacketHandlers.ResetLegacyClassPolaWarScoreStateForTests();
            return;
        }

        if (scheduleName == "MsgMonkMaster")
        {
            WorldPacketHandlers.ResetLegacyMonkMasterScoreStateForTests();
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(scheduleName), scheduleName, null);
    }

    private static Dictionary<string, string> CreateClassPoleWarAttributes(
        string scheduleName,
        int poleHitPoints = ClassPoleWarPoleLife,
        string? process = "Alive")
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        SetClassPoleWarPoleHitPointsAttributePairs(attributes, scheduleName, poleHitPoints);
        if (process is not null)
        {
            SetClassPoleWarAttributeTriplet(attributes, scheduleName, "Process", process);
            SetClassPoleWarAttributeTriplet(attributes, scheduleName, "Proces", process);
        }

        return attributes;
    }

    private static Dictionary<string, string> CreateEmperorWarPoleAttributes()
    {
        var attributes = CreateGuildAttributes();
        attributes["GuildId"] = attributes["GuildID"];
        SetEmperorWarPoleHitPointsAttributePairs(attributes, EmperorWarPoleLife);
        attributes["LegacyMsgEmperorWarProcess"] = "Alive";
        attributes["MsgEmperorWarProcess"] = "Alive";
        attributes["RuntimeMsgEmperorWarProcess"] = "Alive";
        attributes["LegacyEmperorWarProcess"] = "Alive";
        attributes["EmperorWarProcess"] = "Alive";
        attributes["RuntimeEmperorWarProcess"] = "Alive";
        return attributes;
    }

    private static void SetEliteGuildWarPoleHitPointsAttributePairs(
        Dictionary<string, string> attributes,
        int poleHitPoints)
    {
        var value = poleHitPoints.ToString(CultureInfo.InvariantCulture);
        attributes["LegacyEliteGuildWarPoleHitPoints"] = value;
        attributes["LegacyEliteGuildWarPoleHitpoints"] = value;
        attributes["EliteGuildWarPoleHitPoints"] = value;
        attributes["EliteGuildWarPoleHitpoints"] = value;
        attributes["LegacyEliteGuildWarFurnituresPoleHitPoints"] = value;
        attributes["LegacyEliteGuildWarFurnituresPoleHitpoints"] = value;
        attributes["EliteGuildWarFurnituresPoleHitPoints"] = value;
        attributes["EliteGuildWarFurnituresPoleHitpoints"] = value;
    }

    private static void SetChampionsOfWarrPoleHitPointsAttributePairs(
        Dictionary<string, string> attributes,
        int poleHitPoints)
    {
        var value = poleHitPoints.ToString(CultureInfo.InvariantCulture);
        attributes["LegacyMsgChampionsOfWarrPoleHitPoints"] = value;
        attributes["LegacyMsgChampionsOfWarrPoleHitpoints"] = value;
        attributes["MsgChampionsOfWarrPoleHitPoints"] = value;
        attributes["MsgChampionsOfWarrPoleHitpoints"] = value;
        attributes["RuntimeMsgChampionsOfWarrPoleHitPoints"] = value;
        attributes["RuntimeMsgChampionsOfWarrPoleHitpoints"] = value;
        attributes["LegacyChampionsOfWarrPoleHitPoints"] = value;
        attributes["LegacyChampionsOfWarrPoleHitpoints"] = value;
        attributes["ChampionsOfWarrPoleHitPoints"] = value;
        attributes["ChampionsOfWarrPoleHitpoints"] = value;
        attributes["RuntimeChampionsOfWarrPoleHitPoints"] = value;
        attributes["RuntimeChampionsOfWarrPoleHitpoints"] = value;
    }

    private static void SetChampionsOfWarrProcessAttributeTriplet(
        Dictionary<string, string> attributes,
        string suffix,
        string value)
    {
        attributes["LegacyChampionsOfWarr" + suffix] = value;
        attributes["ChampionsOfWarr" + suffix] = value;
        attributes["RuntimeChampionsOfWarr" + suffix] = value;
    }

    private static void SetGuildWarScorePoleHitPointsAttributePairs(
        Dictionary<string, string> attributes,
        int poleHitPoints)
    {
        var value = poleHitPoints.ToString(CultureInfo.InvariantCulture);
        attributes["LegacyMsgGuildWarScorePoleHitPoints"] = value;
        attributes["LegacyMsgGuildWarScorePoleHitpoints"] = value;
        attributes["MsgGuildWarScorePoleHitPoints"] = value;
        attributes["MsgGuildWarScorePoleHitpoints"] = value;
        attributes["LegacyGuildWarScorePoleHitPoints"] = value;
        attributes["LegacyGuildWarScorePoleHitpoints"] = value;
        attributes["GuildWarScorePoleHitPoints"] = value;
        attributes["GuildWarScorePoleHitpoints"] = value;
    }

    private static void SetClanWarScorePoleHitPointsAttributePairs(
        Dictionary<string, string> attributes,
        int poleHitPoints)
    {
        var value = poleHitPoints.ToString(CultureInfo.InvariantCulture);
        attributes["LegacyMsgClanWarScorePoleHitPoints"] = value;
        attributes["LegacyMsgClanWarScorePoleHitpoints"] = value;
        attributes["MsgClanWarScorePoleHitPoints"] = value;
        attributes["MsgClanWarScorePoleHitpoints"] = value;
        attributes["RuntimeMsgClanWarScorePoleHitPoints"] = value;
        attributes["RuntimeMsgClanWarScorePoleHitpoints"] = value;
        attributes["LegacyClanWarScorePoleHitPoints"] = value;
        attributes["LegacyClanWarScorePoleHitpoints"] = value;
        attributes["ClanWarScorePoleHitPoints"] = value;
        attributes["ClanWarScorePoleHitpoints"] = value;
        attributes["RuntimeClanWarScorePoleHitPoints"] = value;
        attributes["RuntimeClanWarScorePoleHitpoints"] = value;
    }

    private static void SetWarOfPlayersPoleHitPointsAttributePairs(
        Dictionary<string, string> attributes,
        int poleHitPoints)
    {
        var value = poleHitPoints.ToString(CultureInfo.InvariantCulture);
        attributes["LegacyMsgWarOfPlayersHitPoints"] = value;
        attributes["LegacyMsgWarOfPlayersHitpoints"] = value;
        attributes["LegacyMsgWarOfPlayersPoleHitPoints"] = value;
        attributes["LegacyMsgWarOfPlayersPoleHitpoints"] = value;
        attributes["MsgWarOfPlayersHitPoints"] = value;
        attributes["MsgWarOfPlayersHitpoints"] = value;
        attributes["MsgWarOfPlayersPoleHitPoints"] = value;
        attributes["MsgWarOfPlayersPoleHitpoints"] = value;
        attributes["RuntimeMsgWarOfPlayersHitPoints"] = value;
        attributes["RuntimeMsgWarOfPlayersHitpoints"] = value;
        attributes["RuntimeMsgWarOfPlayersPoleHitPoints"] = value;
        attributes["RuntimeMsgWarOfPlayersPoleHitpoints"] = value;
        attributes["LegacyWarOfPlayersHitPoints"] = value;
        attributes["LegacyWarOfPlayersHitpoints"] = value;
        attributes["LegacyWarOfPlayersPoleHitPoints"] = value;
        attributes["LegacyWarOfPlayersPoleHitpoints"] = value;
        attributes["WarOfPlayersHitPoints"] = value;
        attributes["WarOfPlayersHitpoints"] = value;
        attributes["WarOfPlayersPoleHitPoints"] = value;
        attributes["WarOfPlayersPoleHitpoints"] = value;
        attributes["RuntimeWarOfPlayersHitPoints"] = value;
        attributes["RuntimeWarOfPlayersHitpoints"] = value;
        attributes["RuntimeWarOfPlayersPoleHitPoints"] = value;
        attributes["RuntimeWarOfPlayersPoleHitpoints"] = value;
    }

    private static void SetTopPoleHitPointsAttributePairs(
        Dictionary<string, string> attributes,
        int poleHitPoints)
    {
        var value = poleHitPoints.ToString(CultureInfo.InvariantCulture);
        attributes["LegacyMsgTopPoleHitPoints"] = value;
        attributes["LegacyMsgTopPoleHitpoints"] = value;
        attributes["MsgTopPoleHitPoints"] = value;
        attributes["MsgTopPoleHitpoints"] = value;
        attributes["LegacyTopPoleHitPoints"] = value;
        attributes["LegacyTopPoleHitpoints"] = value;
        attributes["TopPoleHitPoints"] = value;
        attributes["TopPoleHitpoints"] = value;
    }

    private static void SetWarFightersHitPointsAttributePairs(
        Dictionary<string, string> attributes,
        int poleHitPoints)
    {
        var value = poleHitPoints.ToString(CultureInfo.InvariantCulture);
        attributes["LegacyMsgWarFightersHitPoints"] = value;
        attributes["LegacyMsgWarFightersHitpoints"] = value;
        attributes["LegacyMsgWarFightersPoleHitPoints"] = value;
        attributes["LegacyMsgWarFightersPoleHitpoints"] = value;
        attributes["MsgWarFightersHitPoints"] = value;
        attributes["MsgWarFightersHitpoints"] = value;
        attributes["MsgWarFightersPoleHitPoints"] = value;
        attributes["MsgWarFightersPoleHitpoints"] = value;
        attributes["RuntimeMsgWarFightersHitPoints"] = value;
        attributes["RuntimeMsgWarFightersHitpoints"] = value;
        attributes["RuntimeMsgWarFightersPoleHitPoints"] = value;
        attributes["RuntimeMsgWarFightersPoleHitpoints"] = value;
        attributes["LegacyWarFightersHitPoints"] = value;
        attributes["LegacyWarFightersHitpoints"] = value;
        attributes["LegacyWarFightersPoleHitPoints"] = value;
        attributes["LegacyWarFightersPoleHitpoints"] = value;
        attributes["WarFightersHitPoints"] = value;
        attributes["WarFightersHitpoints"] = value;
        attributes["WarFightersPoleHitPoints"] = value;
        attributes["WarFightersPoleHitpoints"] = value;
        attributes["RuntimeWarFightersHitPoints"] = value;
        attributes["RuntimeWarFightersHitpoints"] = value;
        attributes["RuntimeWarFightersPoleHitPoints"] = value;
        attributes["RuntimeWarFightersPoleHitpoints"] = value;
    }

    private static void SetTrepilWarHitPointsAttributePairs(
        Dictionary<string, string> attributes,
        int poleHitPoints)
    {
        var value = poleHitPoints.ToString(CultureInfo.InvariantCulture);
        attributes["LegacyMsgTrepilWarHitPoints"] = value;
        attributes["LegacyMsgTrepilWarHitpoints"] = value;
        attributes["LegacyMsgTrepilWarPoleHitPoints"] = value;
        attributes["LegacyMsgTrepilWarPoleHitpoints"] = value;
        attributes["MsgTrepilWarHitPoints"] = value;
        attributes["MsgTrepilWarHitpoints"] = value;
        attributes["MsgTrepilWarPoleHitPoints"] = value;
        attributes["MsgTrepilWarPoleHitpoints"] = value;
        attributes["RuntimeMsgTrepilWarHitPoints"] = value;
        attributes["RuntimeMsgTrepilWarHitpoints"] = value;
        attributes["RuntimeMsgTrepilWarPoleHitPoints"] = value;
        attributes["RuntimeMsgTrepilWarPoleHitpoints"] = value;
        attributes["LegacyTrepilWarHitPoints"] = value;
        attributes["LegacyTrepilWarHitpoints"] = value;
        attributes["LegacyTrepilWarPoleHitPoints"] = value;
        attributes["LegacyTrepilWarPoleHitpoints"] = value;
        attributes["TrepilWarHitPoints"] = value;
        attributes["TrepilWarHitpoints"] = value;
        attributes["TrepilWarPoleHitPoints"] = value;
        attributes["TrepilWarPoleHitpoints"] = value;
        attributes["RuntimeTrepilWarHitPoints"] = value;
        attributes["RuntimeTrepilWarHitpoints"] = value;
        attributes["RuntimeTrepilWarPoleHitPoints"] = value;
        attributes["RuntimeTrepilWarPoleHitpoints"] = value;
    }

    private static void SetNobilityPoleHitPointsAttributePairs(
        Dictionary<string, string> attributes,
        int poleHitPoints)
    {
        var value = poleHitPoints.ToString(CultureInfo.InvariantCulture);
        attributes["LegacyNobilityPoleHitPoints"] = value;
        attributes["LegacyNobilityPoleHitpoints"] = value;
        attributes["NobilityPoleHitPoints"] = value;
        attributes["NobilityPoleHitpoints"] = value;
        attributes["RuntimeNobilityPoleHitPoints"] = value;
        attributes["RuntimeNobilityPoleHitpoints"] = value;
        attributes["LegacyNobilityPolePoleHitPoints"] = value;
        attributes["LegacyNobilityPolePoleHitpoints"] = value;
        attributes["NobilityPolePoleHitPoints"] = value;
        attributes["NobilityPolePoleHitpoints"] = value;
        attributes["RuntimeNobilityPolePoleHitPoints"] = value;
        attributes["RuntimeNobilityPolePoleHitpoints"] = value;
    }

    private static void SetNobilityPoleScheduleAttributePair(
        Dictionary<string, string> attributes,
        string scheduleName,
        string suffix,
        int value)
    {
        SetNobilityPoleScheduleAttributePair(
            attributes,
            scheduleName,
            suffix,
            value.ToString(CultureInfo.InvariantCulture));
    }

    private static void SetNobilityPoleScheduleAttributePair(
        Dictionary<string, string> attributes,
        string scheduleName,
        string suffix,
        string value)
    {
        attributes["Legacy" + scheduleName + suffix] = value;
        attributes[scheduleName + suffix] = value;
        attributes["Runtime" + scheduleName + suffix] = value;
    }

    private static void SetClassPoleWarPoleHitPointsAttributePairs(
        Dictionary<string, string> attributes,
        string scheduleName,
        int poleHitPoints)
    {
        var value = poleHitPoints.ToString(CultureInfo.InvariantCulture);
        SetClassPoleWarAttributeTriplet(attributes, scheduleName, "HitPoints", value);
        SetClassPoleWarAttributeTriplet(attributes, scheduleName, "Hitpoints", value);
        SetClassPoleWarAttributeTriplet(attributes, scheduleName, "PoleHitPoints", value);
        SetClassPoleWarAttributeTriplet(attributes, scheduleName, "PoleHitpoints", value);
    }

    private static void SetClassPoleWarAttributeTriplet(
        Dictionary<string, string> attributes,
        string scheduleName,
        string suffix,
        string value)
    {
        attributes["Legacy" + scheduleName + suffix] = value;
        attributes[scheduleName + suffix] = value;
        attributes["Runtime" + scheduleName + suffix] = value;
    }

    private static void SetEmperorWarPoleHitPointsAttributePairs(
        Dictionary<string, string> attributes,
        int poleHitPoints)
    {
        var value = poleHitPoints.ToString(CultureInfo.InvariantCulture);
        attributes["LegacyMsgEmperorWarHitPoints"] = value;
        attributes["LegacyMsgEmperorWarHitpoints"] = value;
        attributes["LegacyMsgEmperorWarPoleHitPoints"] = value;
        attributes["LegacyMsgEmperorWarPoleHitpoints"] = value;
        attributes["MsgEmperorWarHitPoints"] = value;
        attributes["MsgEmperorWarHitpoints"] = value;
        attributes["MsgEmperorWarPoleHitPoints"] = value;
        attributes["MsgEmperorWarPoleHitpoints"] = value;
        attributes["RuntimeMsgEmperorWarHitPoints"] = value;
        attributes["RuntimeMsgEmperorWarHitpoints"] = value;
        attributes["RuntimeMsgEmperorWarPoleHitPoints"] = value;
        attributes["RuntimeMsgEmperorWarPoleHitpoints"] = value;
        attributes["LegacyEmperorWarHitPoints"] = value;
        attributes["LegacyEmperorWarHitpoints"] = value;
        attributes["LegacyEmperorWarPoleHitPoints"] = value;
        attributes["LegacyEmperorWarPoleHitpoints"] = value;
        attributes["EmperorWarHitPoints"] = value;
        attributes["EmperorWarHitpoints"] = value;
        attributes["EmperorWarPoleHitPoints"] = value;
        attributes["EmperorWarPoleHitpoints"] = value;
        attributes["RuntimeEmperorWarHitPoints"] = value;
        attributes["RuntimeEmperorWarHitpoints"] = value;
        attributes["RuntimeEmperorWarPoleHitPoints"] = value;
        attributes["RuntimeEmperorWarPoleHitpoints"] = value;
    }

    private static void SetSuperGuildWarAttributeTriplets(
        Dictionary<string, string> attributes,
        int poleHitPoints,
        int winnerGuildId)
    {
        var poleHitPointsText = poleHitPoints.ToString(CultureInfo.InvariantCulture);
        SetSuperGuildWarAttributeTriplet(attributes, "HitPoints", poleHitPointsText);
        SetSuperGuildWarAttributeTriplet(attributes, "Hitpoints", poleHitPointsText);
        SetSuperGuildWarAttributeTriplet(attributes, "PoleHitPoints", poleHitPointsText);
        SetSuperGuildWarAttributeTriplet(attributes, "PoleHitpoints", poleHitPointsText);

        var winnerGuildIdText = winnerGuildId.ToString(CultureInfo.InvariantCulture);
        SetSuperGuildWarAttributeTriplet(attributes, "WinnerGuildID", winnerGuildIdText);
        SetSuperGuildWarAttributeTriplet(attributes, "WinnerGuildId", winnerGuildIdText);
    }

    private static void SetSuperGuildWarAttributeTriplet(
        Dictionary<string, string> attributes,
        string suffix,
        string value)
    {
        string[] aliases = ["MsgSuperGuildWar", "SuperGuildWar", "MsgSchedulesSuperGuildWar"];
        foreach (var alias in aliases)
        {
            attributes["Legacy" + alias + suffix] = value;
            attributes[alias + suffix] = value;
            attributes["Runtime" + alias + suffix] = value;
        }
    }

    private static void SetGuild6PoleWarScheduleAttributePairs(
        Dictionary<string, string> attributes,
        string scheduleName,
        string suffix,
        int value)
    {
        SetGuild6PoleWarScheduleAttributeTriplet(
            attributes,
            scheduleName,
            suffix,
            value.ToString(CultureInfo.InvariantCulture));
    }

    private static void SetGuild6PoleWarScheduleAttributeTriplet(
        Dictionary<string, string> attributes,
        string scheduleName,
        string suffix,
        string value)
    {
        attributes["Legacy" + scheduleName + suffix] = value;
        attributes[scheduleName + suffix] = value;
        attributes["Runtime" + scheduleName + suffix] = value;
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

    private static ItemType CreateClubItemType(uint minAttack, uint maxAttack)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = ClubItemType.ToString(CultureInfo.InvariantCulture);
        fields[1] = "DirectSobClub";
        fields[13] = minAttack.ToString(CultureInfo.InvariantCulture);
        fields[14] = maxAttack.ToString(CultureInfo.InvariantCulture);
        return new ItemType(ClubItemType, "DirectSobClub", fields);
    }

    private static MagicType CreateKineticSparkMagicRow()
    {
        return CreateMagicRow(KineticSparkSpellId, KineticSparkSort, "KineticSpark", range: 8);
    }

    private static MagicType CreateArcherPreProcMagicRow(ushort spellId)
    {
        return spellId == ArcherPreProcNebulousHuntSpellId
            ? CreateMagicRow(spellId, SectorBackSort, "NebulousHunt", range: 18)
            : CreateMagicRow(spellId, BombSort, "CrescentShadow", range: 9);
    }

    private static MagicType CreateMagicRow(ushort spellId, byte sort, string name, ushort range)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = sort.ToString(CultureInfo.InvariantCulture);
        fields[3] = name;
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = "100";
        fields[12] = "30";
        fields[13] = "0";
        fields[14] = "1";
        fields[15] = range.ToString(CultureInfo.InvariantCulture);
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
        return new MagicType(spellId, spellId, name, fields);
    }

    private static WorldDataCatalog CreateWorldData(
        IReadOnlyList<ItemType> items,
        IReadOnlyList<MagicType> magicRows,
        IReadOnlyList<SobNpc> sobNpcs,
        IReadOnlyList<int> mapIds)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-29T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://direct-sob-npc-physical-runtime",
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
            magic: magicRows,
            magicEffects: [],
            magicTypeOperations: [],
            titleTypes: [],
            combatGear: [],
            traps: [],
            npcs: [],
            sobNpcs: sobNpcs,
            portals: [],
            maps: mapIds.Select(CreateMapMetadata).ToArray(),
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

    private static MapMetadata CreateMapMetadata(int mapId)
    {
        return new MapMetadata(
            mapId,
            mapId.ToString(CultureInfo.InvariantCulture) + ".ini",
            Name: null,
            MapDoc: mapId,
            TypeRaw: "0",
            Type: 0,
            Portal0X: null,
            Portal0Y: null,
            RebornMap: mapId,
            RebornPortal: 0,
            ResLev: 0,
            LinkMap: 0,
            LinkX: 0,
            LinkY: 0,
            ColorRaw: "0",
            Color: 0,
            Attributes: new Dictionary<string, string>());
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

    private static IReadOnlyList<MsgAttackPacket> ParseAttacks(IEnumerable<byte[]> frames)
    {
        var packets = new List<MsgAttackPacket>();
        foreach (var bytes in frames)
        {
            if (MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
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

    private static IReadOnlyList<LegacyMsgUpdatePacket> ParseMsgUpdates(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyMsgUpdatePacket>();
        foreach (var bytes in frames)
        {
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyMsgMessagePacket> ParseLegacyMessages(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyMsgMessagePacket>();
        foreach (var bytes in frames)
        {
            if (LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyNewSynWarPacket> ParseNewSynWarPackets(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyNewSynWarPacket>();
        foreach (var bytes in frames)
        {
            if (LegacyNewSynWarPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacySobNpcSpawnPacket> ParseSobNpcSpawns(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacySobNpcSpawnPacket>();
        foreach (var bytes in frames)
        {
            if (LegacySobNpcSpawnPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyCaptureTheFlagUpdatePacket> ParseCaptureTheFlagUpdates(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyCaptureTheFlagUpdatePacket>();
        foreach (var bytes in frames)
        {
            if (LegacyCaptureTheFlagUpdatePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyCaptureTheFlagScoreBasePacket> ParseCaptureTheFlagScoreBasePackets(
        IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyCaptureTheFlagScoreBasePacket>();
        foreach (var bytes in frames)
        {
            var frame = ReadFrame(bytes);
            if (TryParseCaptureTheFlagScoreBasePacket(frame, out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static bool TryParseCaptureTheFlagScoreBasePacket(
        PacketFrame frame,
        out LegacyCaptureTheFlagScoreBasePacket packet)
    {
        packet = default;
        const int modeOffset = 0;
        const int parameterOffset = modeOffset + sizeof(uint);
        const int reservedOffset = parameterOffset + sizeof(uint);
        const int parameter2Offset = reservedOffset + 16;
        const int countOffset = parameter2Offset + sizeof(ushort);
        const int entryOffset = LegacyCaptureTheFlagUpdatePacket.PayloadLength;
        const int entryLength = (sizeof(uint) * 3) + 32;

        if (frame.Header.Opcode != LegacyCaptureTheFlagUpdatePacket.Opcode ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader) ||
            reader.Payload.Length < entryOffset)
        {
            return false;
        }

        var mode = (LegacyCaptureTheFlagUpdateMode)reader.ReadUInt32(modeOffset);
        if (mode != LegacyCaptureTheFlagUpdateMode.ScoreBase)
        {
            return false;
        }

        var entryCount = reader.ReadUInt16(parameter2Offset);
        if (reader.Payload.Length != entryOffset + (entryCount * entryLength))
        {
            return false;
        }

        var entries = new List<LegacyCaptureTheFlagScoreBaseEntry>(entryCount);
        for (var index = 0; index < entryCount; index++)
        {
            var offset = entryOffset + (index * entryLength);
            entries.Add(new LegacyCaptureTheFlagScoreBaseEntry(
                reader.ReadUInt32(offset),
                reader.ReadUInt32(offset + sizeof(uint)),
                reader.ReadCString(offset + (sizeof(uint) * 3), 32, Encoding.ASCII)));
        }

        packet = new LegacyCaptureTheFlagScoreBasePacket(
            mode,
            reader.ReadUInt32(parameterOffset),
            reader.ReadUInt16(parameter2Offset),
            reader.ReadUInt16(countOffset),
            entries);
        return true;
    }

    private static IReadOnlyList<CaptureTheFlagBaseScore> ReadCaptureTheFlagBaseScores(
        WorldSession? session,
        string log)
    {
        Assert.NotNull(session);
        Assert.True(
            session.Character.Attributes.TryGetValue("CaptureTheFlagBaseScores", out var raw),
            "Expected direct CTF base physical damage to record session attribute CaptureTheFlagBaseScores " +
            "as baseUid,guildId,score,guildName entries. Log=" + log);

        var entries = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var scores = new List<CaptureTheFlagBaseScore>(entries.Length);
        foreach (var entry in entries)
        {
            var fields = entry.Split(',', 4, StringSplitOptions.TrimEntries);
            Assert.True(
                fields.Length >= 4,
                "Expected CaptureTheFlagBaseScores entry shape baseUid,guildId,score,guildName. Entry=" + entry);
            Assert.True(uint.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var baseUid));
            Assert.True(ulong.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var guildId));
            Assert.True(uint.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var score));
            scores.Add(new CaptureTheFlagBaseScore(baseUid, guildId, Uri.UnescapeDataString(fields[3]), score));
        }

        return scores;
    }

    private readonly record struct LegacyCaptureTheFlagScoreBasePacket(
        LegacyCaptureTheFlagUpdateMode Mode,
        uint Parameter,
        ushort Parameter2,
        ushort Count,
        IReadOnlyList<LegacyCaptureTheFlagScoreBaseEntry> Entries);

    private readonly record struct LegacyCaptureTheFlagScoreBaseEntry(
        uint Rank,
        uint Score,
        string GuildName);

    private readonly record struct CaptureTheFlagBaseScore(
        uint BaseUid,
        ulong GuildId,
        string GuildName,
        uint Score);

    private static IReadOnlyList<SuperGuildWarScore> ReadSuperGuildWarScores(
        WorldSession? session,
        string log)
    {
        Assert.NotNull(session);
        Assert.True(
            session.Character.Attributes.TryGetValue("SuperGuildWarScores", out var raw),
            "Expected direct SuperGuildWar pole physical damage to record session attribute SuperGuildWarScores " +
            "as guildId,score,guildName entries. Log=" + log);

        var entries = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var scores = new List<SuperGuildWarScore>(entries.Length);
        foreach (var entry in entries)
        {
            var fields = entry.Split(',', 3, StringSplitOptions.TrimEntries);
            Assert.True(
                fields.Length >= 3,
                "Expected SuperGuildWarScores entry shape guildId,score,guildName. Entry=" + entry);
            Assert.True(ulong.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var guildId));
            Assert.True(uint.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var score));
            scores.Add(new SuperGuildWarScore(guildId, Uri.UnescapeDataString(fields[2]), score));
        }

        return scores;
    }

    private static void AssertSuperGuildWarRightCornerScoresInDescendingOrder(
        IEnumerable<byte[]> sent,
        SuperGuildWarScore firstScore,
        SuperGuildWarScore secondScore)
    {
        var rightCornerMessages = ParseLegacyMessages(sent)
            .Where(packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                (packet.ChatType == FirstRightCornerChatType || packet.ChatType == ContinueRightCornerChatType) &&
                packet.Content.Any(content => content.StartsWith("No ", StringComparison.Ordinal)))
            .ToArray();
        Assert.Collection(
            rightCornerMessages,
            packet =>
            {
                Assert.Equal(FirstRightCornerChatType, packet.ChatType);
                Assert.Contains(
                    $"No 1. {firstScore.GuildName} ({firstScore.Score})",
                    packet.Content);
            },
            packet =>
            {
                Assert.Equal(ContinueRightCornerChatType, packet.ChatType);
                Assert.Contains(
                    $"No 2. {secondScore.GuildName} ({secondScore.Score})",
                    packet.Content);
            });
    }

    private readonly record struct SuperGuildWarScore(
        ulong GuildId,
        string GuildName,
        uint Score);

    private static IReadOnlyList<UnionWarScore> ReadUnionWarScores(
        WorldSession? session,
        string log)
    {
        Assert.NotNull(session);
        Assert.True(
            session.Character.Attributes.TryGetValue("UnionWarScores", out var raw),
            "Expected direct UnionWar pole physical damage to record session attribute UnionWarScores " +
            "as unionId,score,unionName entries. Log=" + log);

        var entries = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var scores = new List<UnionWarScore>(entries.Length);
        foreach (var entry in entries)
        {
            var fields = entry.Split(',', 3, StringSplitOptions.TrimEntries);
            Assert.True(
                fields.Length >= 3,
                "Expected UnionWarScores entry shape unionId,score,unionName. Entry=" + entry);
            Assert.True(ulong.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var unionId));
            Assert.True(uint.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var score));
            scores.Add(new UnionWarScore(unionId, Uri.UnescapeDataString(fields[2]), score));
        }

        return scores;
    }

    private readonly record struct UnionWarScore(
        ulong UnionId,
        string UnionName,
        uint Score);

    private static IReadOnlyList<EliteGuildWarScore> ReadEliteGuildWarScores(
        WorldSession? session,
        string log)
    {
        Assert.NotNull(session);
        Assert.True(
            session.Character.Attributes.TryGetValue("EliteGuildWarScores", out var raw),
            "Expected direct EliteGuildWar pole physical damage to record session attribute EliteGuildWarScores " +
            "as guildId,score,guildName entries. Log=" + log);

        var entries = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var scores = new List<EliteGuildWarScore>(entries.Length);
        foreach (var entry in entries)
        {
            var fields = entry.Split(',', 3, StringSplitOptions.TrimEntries);
            Assert.True(
                fields.Length >= 3,
                "Expected EliteGuildWarScores entry shape guildId,score,guildName. Entry=" + entry);
            Assert.True(ulong.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var guildId));
            Assert.True(uint.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var score));
            scores.Add(new EliteGuildWarScore(guildId, Uri.UnescapeDataString(fields[2]), score));
        }

        return scores;
    }

    private static void AssertEliteGuildWarRightCornerScoresInDescendingOrder(
        IEnumerable<byte[]> sent,
        EliteGuildWarScore firstScore,
        EliteGuildWarScore secondScore)
    {
        var rightCornerMessages = ParseLegacyMessages(sent)
            .Where(packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                (packet.ChatType == FirstRightCornerChatType || packet.ChatType == ContinueRightCornerChatType) &&
                packet.Content.Any(content => content.StartsWith("No ", StringComparison.Ordinal)))
            .ToArray();
        Assert.Collection(
            rightCornerMessages,
            packet =>
            {
                Assert.Equal(FirstRightCornerChatType, packet.ChatType);
                Assert.Contains(
                    $"No 1. {firstScore.GuildName} ({firstScore.Score})",
                    packet.Content);
            },
            packet =>
            {
                Assert.Equal(ContinueRightCornerChatType, packet.ChatType);
                Assert.Contains(
                    $"No 2. {secondScore.GuildName} ({secondScore.Score})",
                    packet.Content);
            });
    }

    private readonly record struct EliteGuildWarScore(
        ulong GuildId,
        string GuildName,
        uint Score);

    private static IReadOnlyList<ChampionsOfWarrScore> ReadChampionsOfWarrScores(
        WorldSession? session,
        string log)
    {
        Assert.NotNull(session);
        Assert.True(
            session.Character.Attributes.TryGetValue("ChampionsOfWarrScores", out var raw),
            "Expected direct ChampionsOfWarr pole physical damage to record session attribute ChampionsOfWarrScores " +
            "as guildId,score,guildName entries. Log=" + log);

        var entries = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var scores = new List<ChampionsOfWarrScore>(entries.Length);
        foreach (var entry in entries)
        {
            var fields = entry.Split(',', 3, StringSplitOptions.TrimEntries);
            Assert.True(
                fields.Length >= 3,
                "Expected ChampionsOfWarrScores entry shape guildId,score,guildName. Entry=" + entry);
            Assert.True(ulong.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var guildId));
            Assert.True(uint.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var score));
            scores.Add(new ChampionsOfWarrScore(guildId, Uri.UnescapeDataString(fields[2]), score));
        }

        return scores;
    }

    private static void AssertChampionsOfWarrRightCornerScoresInDescendingOrder(
        IEnumerable<byte[]> sent,
        ChampionsOfWarrScore firstScore,
        ChampionsOfWarrScore secondScore)
    {
        var rightCornerMessages = ParseLegacyMessages(sent)
            .Where(packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                (packet.ChatType == FirstRightCornerChatType || packet.ChatType == ContinueRightCornerChatType) &&
                packet.Content.Any(content => content.StartsWith("No ", StringComparison.Ordinal)))
            .ToArray();
        Assert.Collection(
            rightCornerMessages,
            packet =>
            {
                Assert.Equal(FirstRightCornerChatType, packet.ChatType);
                Assert.Contains(
                    $"No 1. {firstScore.GuildName} ({firstScore.Score})",
                    packet.Content);
            },
            packet =>
            {
                Assert.Equal(ContinueRightCornerChatType, packet.ChatType);
                Assert.Contains(
                    $"No 2. {secondScore.GuildName} ({secondScore.Score})",
                    packet.Content);
            });
    }

    private readonly record struct ChampionsOfWarrScore(
        ulong GuildId,
        string GuildName,
        uint Score);

    private static IReadOnlyList<GuildWarScoreEntry> ReadGuildWarScoreScores(
        WorldSession? session,
        string log)
    {
        Assert.NotNull(session);
        Assert.True(
            session.Character.Attributes.TryGetValue("GuildWarScoreScores", out var raw),
            "Expected direct GuildWarScore pole physical damage to record session attribute GuildWarScoreScores " +
            "as guildId,score,guildName entries. Log=" + log);

        var entries = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var scores = new List<GuildWarScoreEntry>(entries.Length);
        foreach (var entry in entries)
        {
            var fields = entry.Split(',', 3, StringSplitOptions.TrimEntries);
            Assert.True(
                fields.Length >= 3,
                "Expected GuildWarScoreScores entry shape guildId,score,guildName. Entry=" + entry);
            Assert.True(ulong.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var guildId));
            Assert.True(uint.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var score));
            scores.Add(new GuildWarScoreEntry(guildId, Uri.UnescapeDataString(fields[2]), score));
        }

        return scores;
    }

    private static void AssertGuildWarScoreRightCornerScoresInDescendingOrder(
        IEnumerable<byte[]> sent,
        GuildWarScoreEntry firstScore,
        GuildWarScoreEntry secondScore)
    {
        var rightCornerMessages = ParseLegacyMessages(sent)
            .Where(packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                (packet.ChatType == FirstRightCornerChatType || packet.ChatType == ContinueRightCornerChatType) &&
                packet.Content.Any(content => content.StartsWith("No ", StringComparison.Ordinal)))
            .ToArray();
        Assert.Collection(
            rightCornerMessages,
            packet =>
            {
                Assert.Equal(FirstRightCornerChatType, packet.ChatType);
                Assert.Contains(
                    $"No 1. {firstScore.GuildName} ({firstScore.Score})",
                    packet.Content);
            },
            packet =>
            {
                Assert.Equal(ContinueRightCornerChatType, packet.ChatType);
                Assert.Contains(
                    $"No 2. {secondScore.GuildName} ({secondScore.Score})",
                    packet.Content);
            });
    }

    private readonly record struct GuildWarScoreEntry(
        ulong GuildId,
        string GuildName,
        uint Score);

    private static IReadOnlyList<ClanWarScoreEntry> ReadClanWarScoreScores(
        WorldSession? session,
        string log)
    {
        Assert.NotNull(session);
        Assert.True(
            session.Character.Attributes.TryGetValue("ClanWarScoreScores", out var raw),
            "Expected direct ClanWarScore pole physical damage to record session attribute ClanWarScoreScores " +
            "as clanId,score,clanName entries. Log=" + log);

        var entries = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var scores = new List<ClanWarScoreEntry>(entries.Length);
        foreach (var entry in entries)
        {
            var fields = entry.Split(',', 3, StringSplitOptions.TrimEntries);
            Assert.True(
                fields.Length >= 3,
                "Expected ClanWarScoreScores entry shape clanId,score,clanName. Entry=" + entry);
            Assert.True(ulong.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var clanId));
            Assert.True(uint.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var score));
            scores.Add(new ClanWarScoreEntry(clanId, Uri.UnescapeDataString(fields[2]), score));
        }

        return scores;
    }

    private static void AssertClanWarScoreRightCornerScoresInDescendingOrder(
        IEnumerable<byte[]> sent,
        ClanWarScoreEntry firstScore,
        ClanWarScoreEntry secondScore)
    {
        var rightCornerMessages = ParseLegacyMessages(sent)
            .Where(packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                (packet.ChatType == FirstRightCornerChatType || packet.ChatType == ContinueRightCornerChatType) &&
                packet.Content.Any(content => content.StartsWith("No ", StringComparison.Ordinal)))
            .ToArray();
        Assert.Collection(
            rightCornerMessages,
            packet =>
            {
                Assert.Equal(FirstRightCornerChatType, packet.ChatType);
                Assert.Contains(
                    $"No 1. {firstScore.ClanName} ({firstScore.Score})",
                    packet.Content);
            },
            packet =>
            {
                Assert.Equal(ContinueRightCornerChatType, packet.ChatType);
                Assert.Contains(
                    $"No 2. {secondScore.ClanName} ({secondScore.Score})",
                    packet.Content);
            });
    }

    private readonly record struct ClanWarScoreEntry(
        ulong ClanId,
        string ClanName,
        uint Score);

    private static IReadOnlyList<ClanWarCurrentScoreEntry> ReadClanWarScores(
        WorldSession? session,
        string log)
    {
        Assert.NotNull(session);
        Assert.True(
            session.Character.Attributes.TryGetValue("ClanWarScores", out var raw),
            "Expected direct ClanWar current-war pole physical damage to record session attribute ClanWarScores " +
            "as city,clanId,score,clanName entries. Log=" + log);

        var entries = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var scores = new List<ClanWarCurrentScoreEntry>(entries.Length);
        foreach (var entry in entries)
        {
            var fields = entry.Split(',', 4, StringSplitOptions.TrimEntries);
            Assert.True(
                fields.Length >= 4,
                "Expected ClanWarScores entry shape city,clanId,score,clanName. Entry=" + entry);
            Assert.True(ulong.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var clanId));
            Assert.True(uint.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var score));
            scores.Add(new ClanWarCurrentScoreEntry(
                Uri.UnescapeDataString(fields[0]),
                clanId,
                Uri.UnescapeDataString(fields[3]),
                score));
        }

        return scores;
    }

    private readonly record struct ClanWarCurrentScoreEntry(
        string City,
        ulong ClanId,
        string ClanName,
        uint Score);

    private static void AssertNoClanWarCurrentRightCornerScorePanel(IEnumerable<byte[]> sent)
    {
        Assert.DoesNotContain(
            ParseLegacyMessages(sent),
            packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                (packet.ChatType == FirstRightCornerChatType || packet.ChatType == ContinueRightCornerChatType) &&
                packet.Content.Any(content => content.StartsWith("No ", StringComparison.Ordinal)));
    }

    private static void AssertClanWarCurrentRightCornerScoresInDescendingOrder(
        IEnumerable<byte[]> sent,
        ClanWarCurrentScoreEntry firstScore,
        ClanWarCurrentScoreEntry secondScore)
    {
        var rightCornerMessages = ParseLegacyMessages(sent)
            .Where(packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                (packet.ChatType == FirstRightCornerChatType || packet.ChatType == ContinueRightCornerChatType) &&
                packet.Content.Any(content => content.StartsWith("No ", StringComparison.Ordinal)))
            .ToArray();
        Assert.Collection(
            rightCornerMessages,
            packet =>
            {
                Assert.Equal(FirstRightCornerChatType, packet.ChatType);
                Assert.Contains(
                    $"No 1. {firstScore.ClanName} ({firstScore.Score})",
                    packet.Content);
            },
            packet =>
            {
                Assert.Equal(ContinueRightCornerChatType, packet.ChatType);
                Assert.Contains(
                    $"No 2. {secondScore.ClanName} ({secondScore.Score})",
                    packet.Content);
            });
    }

    private static IReadOnlyList<Guild6PoleWarScore> ReadGuild6PoleWarScores(
        WorldSession? session,
        string log)
    {
        Assert.NotNull(session);
        Assert.True(
            session.Character.Attributes.TryGetValue("Guild6PoleWarScores", out var raw),
            "Expected direct Guild6PoleWar pole physical damage to record session attribute Guild6PoleWarScores " +
            "as scheduleName,guildId,score,guildName entries. Log=" + log);

        var entries = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var scores = new List<Guild6PoleWarScore>(entries.Length);
        foreach (var entry in entries)
        {
            var fields = entry.Split(',', 5, StringSplitOptions.TrimEntries);
            Assert.True(
                fields.Length >= 4,
                "Expected Guild6PoleWarScores entry shape scheduleName,guildId,score,guildName. Entry=" + entry);

            var scheduleName = fields[0];
            var guildIdOffset = 1;
            if (!ulong.TryParse(fields[guildIdOffset], NumberStyles.Integer, CultureInfo.InvariantCulture, out var guildId))
            {
                Assert.True(
                    fields.Length >= 5,
                    "Expected Guild6PoleWarScores entry shape scheduleName,guildId,score,guildName. Entry=" + entry);
                scheduleName = fields[1];
                guildIdOffset = 2;
                Assert.True(
                    ulong.TryParse(fields[guildIdOffset], NumberStyles.Integer, CultureInfo.InvariantCulture, out guildId),
                    "Expected Guild6PoleWarScores guild id after schedule name. Entry=" + entry);
            }

            Assert.True(uint.TryParse(
                fields[guildIdOffset + 1],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var score));
            scores.Add(new Guild6PoleWarScore(
                Uri.UnescapeDataString(scheduleName),
                guildId,
                Uri.UnescapeDataString(fields[guildIdOffset + 2]),
                score));
        }

        return scores;
    }

    private static void AssertGuild6PoleWarRightCornerScoresInDescendingOrder(
        IEnumerable<byte[]> sent,
        Guild6PoleWarScore firstScore,
        Guild6PoleWarScore secondScore)
    {
        var rightCornerMessages = ParseLegacyMessages(sent)
            .Where(packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                (packet.ChatType == FirstRightCornerChatType || packet.ChatType == ContinueRightCornerChatType) &&
                packet.Content.Any(content => content.StartsWith("No ", StringComparison.Ordinal)))
            .ToArray();
        Assert.Collection(
            rightCornerMessages,
            packet =>
            {
                Assert.Equal(FirstRightCornerChatType, packet.ChatType);
                Assert.Contains(
                    $"No 1. {firstScore.GuildName} ({firstScore.Score})",
                    packet.Content);
            },
            packet =>
            {
                Assert.Equal(ContinueRightCornerChatType, packet.ChatType);
                Assert.Contains(
                    $"No 2. {secondScore.GuildName} ({secondScore.Score})",
                    packet.Content);
            });
    }

    private readonly record struct Guild6PoleWarScore(
        string ScheduleName,
        ulong GuildId,
        string GuildName,
        uint Score);

    private static IReadOnlyList<TopPoleScore> ReadTopPoleScores(
        WorldSession? session,
        string log)
    {
        Assert.NotNull(session);
        Assert.True(
            session.Character.Attributes.TryGetValue("TopPoleScores", out var raw),
            "Expected direct TopPole pole physical damage to record session attribute TopPoleScores " +
            "as playerUid,score,playerName entries. Log=" + log);

        var entries = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var scores = new List<TopPoleScore>(entries.Length);
        foreach (var entry in entries)
        {
            var fields = entry.Split(',', 3, StringSplitOptions.TrimEntries);
            Assert.True(
                fields.Length >= 3,
                "Expected TopPoleScores entry shape playerUid,score,playerName. Entry=" + entry);
            Assert.True(uint.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var playerUid));
            Assert.True(uint.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var score));
            scores.Add(new TopPoleScore(playerUid, Uri.UnescapeDataString(fields[2]), score));
        }

        return scores;
    }

    private readonly record struct TopPoleScore(
        uint PlayerUid,
        string PlayerName,
        uint Score);

    private static IReadOnlyList<WarFightersScore> ReadWarFightersScores(
        WorldSession? session,
        string log)
    {
        Assert.NotNull(session);
        Assert.True(
            session.Character.Attributes.TryGetValue("WarFightersScores", out var raw),
            "Expected direct WarFighters pole physical damage to record session attribute WarFightersScores " +
            "as playerUid,score,playerName entries. Log=" + log);

        var entries = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var scores = new List<WarFightersScore>(entries.Length);
        foreach (var entry in entries)
        {
            var fields = entry.Split(',', 3, StringSplitOptions.TrimEntries);
            Assert.True(
                fields.Length >= 3,
                "Expected WarFightersScores entry shape playerUid,score,playerName. Entry=" + entry);
            Assert.True(uint.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var playerUid));
            Assert.True(uint.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var score));
            scores.Add(new WarFightersScore(playerUid, Uri.UnescapeDataString(fields[2]), score));
        }

        return scores;
    }

    private readonly record struct WarFightersScore(
        uint PlayerUid,
        string PlayerName,
        uint Score);

    private static IReadOnlyList<TrepilWarScore> ReadTrepilWarScores(
        WorldSession? session,
        string log)
    {
        Assert.NotNull(session);
        Assert.True(
            session.Character.Attributes.TryGetValue("TrepilWarScores", out var raw),
            "Expected direct TrepilWar pole physical damage to record session attribute TrepilWarScores " +
            "as guildId,score,guildName entries. Log=" + log);

        var entries = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var scores = new List<TrepilWarScore>(entries.Length);
        foreach (var entry in entries)
        {
            var fields = entry.Split(',', 3, StringSplitOptions.TrimEntries);
            Assert.True(
                fields.Length >= 3,
                "Expected TrepilWarScores entry shape guildId,score,guildName. Entry=" + entry);
            Assert.True(ulong.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var guildId));
            Assert.True(uint.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var score));
            scores.Add(new TrepilWarScore(guildId, Uri.UnescapeDataString(fields[2]), score));
        }

        return scores;
    }

    private readonly record struct TrepilWarScore(
        ulong GuildId,
        string GuildName,
        uint Score);

    private static IReadOnlyList<NobilityPoleScore> ReadNobilityPoleScores(
        WorldSession? session,
        string log)
    {
        Assert.NotNull(session);
        Assert.True(
            session.Character.Attributes.TryGetValue("NobilityPoleScores", out var raw),
            "Expected direct NobilityPole physical damage to record session attribute NobilityPoleScores " +
            "as scheduleName,playerUid,score,playerName entries. Log=" + log);

        var entries = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var scores = new List<NobilityPoleScore>(entries.Length);
        foreach (var entry in entries)
        {
            var fields = entry.Split(',', 4, StringSplitOptions.TrimEntries);
            Assert.True(
                fields.Length >= 4,
                "Expected NobilityPoleScores entry shape scheduleName,playerUid,score,playerName. Entry=" + entry);
            Assert.True(uint.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var playerUid));
            Assert.True(uint.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var score));
            scores.Add(new NobilityPoleScore(fields[0], playerUid, Uri.UnescapeDataString(fields[3]), score));
        }

        return scores;
    }

    private readonly record struct NobilityPoleScore(
        string ScheduleName,
        uint PlayerUid,
        string PlayerName,
        uint Score);

    private static IReadOnlyList<ClassPolePlayerScore> ReadClassPolePlayerScores(
        WorldSession? session,
        string scoresAttribute,
        string log)
    {
        Assert.NotNull(session);
        Assert.True(
            session.Character.Attributes.TryGetValue(scoresAttribute, out var raw),
            "Expected direct class-pole physical damage to record session attribute " + scoresAttribute +
            " as playerUid,score,playerName entries. Log=" + log);

        var entries = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var scores = new List<ClassPolePlayerScore>(entries.Length);
        foreach (var entry in entries)
        {
            var fields = entry.Split(',', 3, StringSplitOptions.TrimEntries);
            Assert.True(
                fields.Length >= 3,
                "Expected class-pole score entry shape playerUid,score,playerName. Entry=" + entry);
            Assert.True(uint.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var playerUid));
            Assert.True(uint.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var score));
            scores.Add(new ClassPolePlayerScore(playerUid, Uri.UnescapeDataString(fields[2]), score));
        }

        return scores;
    }

    private readonly record struct ClassPolePlayerScore(
        uint PlayerUid,
        string PlayerName,
        uint Score);

    private static IReadOnlyList<EmperorWarScore> ReadEmperorWarScores(
        WorldSession? session,
        string log)
    {
        Assert.NotNull(session);
        Assert.True(
            session.Character.Attributes.TryGetValue("EmperorWarScores", out var raw),
            "Expected direct EmperorWar pole physical damage to record session attribute EmperorWarScores " +
            "as playerUid,score,playerName entries. Log=" + log);

        var entries = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var scores = new List<EmperorWarScore>(entries.Length);
        foreach (var entry in entries)
        {
            var fields = entry.Split(',', 3, StringSplitOptions.TrimEntries);
            Assert.True(
                fields.Length >= 3,
                "Expected EmperorWarScores entry shape playerUid,score,playerName. Entry=" + entry);
            Assert.True(uint.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var playerUid));
            Assert.True(uint.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var score));
            scores.Add(new EmperorWarScore(playerUid, Uri.UnescapeDataString(fields[2]), score));
        }

        return scores;
    }

    private static void AssertEmperorWarRightCornerScoresInDescendingOrder(
        IEnumerable<byte[]> sent,
        EmperorWarScore firstScore,
        EmperorWarScore secondScore)
    {
        var rightCornerMessages = ParseLegacyMessages(sent)
            .Where(packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                (packet.ChatType == FirstRightCornerChatType || packet.ChatType == ContinueRightCornerChatType) &&
                packet.Content.Any(content => content.StartsWith("No ", StringComparison.Ordinal)))
            .ToArray();
        Assert.Collection(
            rightCornerMessages,
            packet =>
            {
                Assert.Equal(FirstRightCornerChatType, packet.ChatType);
                Assert.Contains(
                    $"No 1. {firstScore.PlayerName} ({firstScore.Score})",
                    packet.Content);
            },
            packet =>
            {
                Assert.Equal(ContinueRightCornerChatType, packet.ChatType);
                Assert.Contains(
                    $"No 2. {secondScore.PlayerName} ({secondScore.Score})",
                    packet.Content);
            });
    }

    private readonly record struct EmperorWarScore(
        uint PlayerUid,
        string PlayerName,
        uint Score);

    private static IReadOnlyList<WarOfPlayersScore> ReadWarOfPlayersScores(
        WorldSession? session,
        string log)
    {
        Assert.NotNull(session);
        Assert.True(
            session.Character.Attributes.TryGetValue("WarOfPlayersScores", out var raw),
            "Expected direct WarOfPlayers pole physical damage to record session attribute WarOfPlayersScores " +
            "as playerUid,score,playerName entries. Log=" + log);

        var entries = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var scores = new List<WarOfPlayersScore>(entries.Length);
        foreach (var entry in entries)
        {
            var fields = entry.Split(',', 3, StringSplitOptions.TrimEntries);
            Assert.True(
                fields.Length >= 3,
                "Expected WarOfPlayersScores entry shape playerUid,score,playerName. Entry=" + entry);
            Assert.True(uint.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var playerUid));
            Assert.True(uint.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var score));
            scores.Add(new WarOfPlayersScore(playerUid, Uri.UnescapeDataString(fields[2]), score));
        }

        return scores;
    }

    private static void AssertWarOfPlayersRightCornerScoresInDescendingOrder(
        IEnumerable<byte[]> sent,
        WarOfPlayersScore firstScore,
        WarOfPlayersScore secondScore)
    {
        var rightCornerMessages = ParseLegacyMessages(sent)
            .Where(packet => packet.Color == LegacyMsgMessagePacket.YellowColor &&
                (packet.ChatType == FirstRightCornerChatType || packet.ChatType == ContinueRightCornerChatType) &&
                packet.Content.Any(content => content.StartsWith("No ", StringComparison.Ordinal)))
            .ToArray();
        Assert.Collection(
            rightCornerMessages,
            packet =>
            {
                Assert.Equal(FirstRightCornerChatType, packet.ChatType);
                Assert.Contains(
                    $"No 1. {firstScore.PlayerName} ({firstScore.Score})",
                    packet.Content);
            },
            packet =>
            {
                Assert.Equal(ContinueRightCornerChatType, packet.ChatType);
                Assert.Contains(
                    $"No 2. {secondScore.PlayerName} ({secondScore.Score})",
                    packet.Content);
            });
    }

    private readonly record struct WarOfPlayersScore(
        uint PlayerUid,
        string PlayerName,
        uint Score);

    private static LegacyExperienceGainInputs CreateExpectedLegacyExperienceInputs(
        WorldSession session,
        ulong baseAmount)
    {
        return new LegacyExperienceGainInputs(
            BaseAmount: baseAmount,
            PlayerLevel: checked((int)session.Character.Level),
            CursedTimer: 0,
            SoulChantEquipped: false,
            SoulChantItemLevel: 0,
            ServerExperienceRate: LegacyExperienceGainPolicy.ReadServerExperienceRate(session.Character.Attributes, _ => null),
            RainbowGemPercent: 0,
            DoubleExperienceTime: 0,
            DoubleExperienceRate: 1.0,
            MapId: checked((int)session.Character.MapId),
            HasOblivion: false,
            HasHeavenBlessing: false);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
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

    private static bool HasStatusFlag(IReadOnlyDictionary<string, string> attributes, int flag)
    {
        if (!attributes.TryGetValue("StatusFlags", out var statusFlags) &&
            !attributes.TryGetValue("BitVector", out statusFlags))
        {
            return false;
        }

        var parts = statusFlags.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var wordIndex = flag / 32;
        if (wordIndex >= parts.Length ||
            !ulong.TryParse(parts[wordIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var word))
        {
            return false;
        }

        return (word & (1UL << (flag % 32))) != 0;
    }
}
