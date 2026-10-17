using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyKineticSparkPassiveSobNpcRuntimeTests
{
    private const uint PlayerUid = 1_293_600;
    private const int ClubItemType = 480_000;
    private const ushort KineticSparkSpellId = 11_590;
    private const ushort ArcherPreProcCrescentShadowSpellId = 18_750;
    private const ushort ArcherPreProcNebulousHuntSpellId = 10_990;
    private const ushort CombatGearRedcurseSpellId = 17_540;
    private const ushort CombatGearDeadwoodCurseSpellId = 20_330;
    private const ushort CombatGearCrackMantraSpellId = 20_380;
    private const ushort CombatGearPirateArmedSpellId = 21_710;
    private const ushort CombatGearPirateSenseSpellId = 21_720;
    private const ushort CombatGearPirateDarkSpellId = 21_930;
    private const ushort CombatGearStoneCrackerStarFlowSpellId = 10_950;
    private const uint CombatGearRedcurseItemId = 0xCB;
    private const uint CombatGearStoneCrackerItemId = 0x191;
    private const uint CombatGearTaoist1ItemId = 0x515;
    private const uint CombatGearTaoist3ItemId = 0x517;
    private const uint CombatGearPirate1ItemId = 701;
    private const uint CombatGearPirate3ItemId = 703;
    private const int CombatGearStoneCrackerWaterTop4WeeklyFlag = 0x10D | 0x40;
    private const int CombatGearStoneCrackerLightningTop4WeeklyFlag = 0x110 | 0x40;
    private const int CombatGearStoneCrackerSageSpeedModeTop4WeeklyFlag = 0x111 | 0x40;
    private const byte BombSort = 5;
    private const byte SectorBackSort = 69;
    private const byte KineticSparkSort = 67;
    private const byte AttackSort = 1;
    private const byte AttachStatusSort = 6;
    private const LegacyMsgUpdateDataType CombatGearStoneCrackerStarFlowUpdateType =
        (LegacyMsgUpdateDataType)136;
    private const int KineticSparkStatusFlag = 0x93;
    private const int CelestialDanceStatusFlag = 0xFA;
    private const int AxeShadowStatusFlag = 0xFB;
    private const int CleanSweepStatusFlag = 0xFE;
    private const int HookMoonCasterStatusFlag = 0x104;
    private const int CombatGearDeadwoodCurseFlag = 0x17E;
    private const int CombatGearCrackMantraPhysicalFlag = 0x7A | 0x100;
    private const int CombatGearCrackMantraMagicFlag = 0x3B | 0x40 | 0x100;
    private const int CombatGearPirateSenseFlag = 0x19C;
    private const int CombatGearPirateArmedFlag = 0x19D;
    private const int NinjaSageSpeedModeFlag = 0x111;
    private const string RuntimeLegacyKineticSparkPassiveRateRollAttribute = "LegacyKineticSparkPassiveRateRoll";
    private const string RuntimeLegacyArcherPreProcRateRollAttribute = "LegacyArcherPreProcRateRoll";
    private const string RuntimeLegacyCombatGearAttackSpellIndexAttribute = "LegacyCombatGearAttackSpellIndex";
    private const string RuntimeLegacyDeadwoodCurseDamageOnHumanAttribute = "DeadwoodCurseDamageOnHuman";
    private const string RuntimeLegacyDeadwoodCurseDamage2Attribute = "DeadwoodCurseDamage2";
    private const string RuntimeLegacyThundercloudSightAttribute = "ThundercloudSight";
    private const string RuntimeLegacyAttackHitAttribute = "AttackHit";
    private const string RuntimeLegacyStoneCrackerArrowBladesAttribute = "ArrowBlades";
    private const string RuntimeLegacyStoneCrackerStarFlowAttribute = "StarFlow";
    private const string RuntimeLegacyNinjaSageModeRollAttribute = "LegacyNinjaSageModeRoll";
    private const int MapId = 1_015;
    private const ushort PlayerX = 205;
    private const ushort PlayerY = 230;
    private const ushort PrimarySobNpcX = 210;
    private const ushort PrimarySobNpcY = 230;
    private const ushort NearbySobNpcX = 213;
    private const ushort NearbySobNpcY = 230;
    private const ushort OutsideSobNpcX = 215;
    private const ushort OutsideSobNpcY = 230;
    private const ushort BeyondDirectRangeSobNpcX = 224;
    private const ushort BeyondDirectRangeSobNpcY = 230;

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    private readonly record struct LegacySobNpcDamageProbe(uint RequestedDamage, int VisibleLife);

    [Fact]
    public async Task HandlerArcherSobNpcAttackWithCrescentShadowPreProcRunsBeforeKineticSpark()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var primary = CreateSobNpc(id: 65_586, name: "PreProcPole", x: PlayerX + 1, y: PlayerY);
        var worldData = CreateWorldData(
            items: [CreateClubItemType()],
            magicRows: [CreateKineticSparkMagicRow(), CreateArcherPreProcMagicRow(ArcherPreProcCrescentShadowSpellId)],
            sobNpcs: [primary]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [KineticSparkSpellId, ArcherPreProcCrescentShadowSpellId],
            archerPreProcRateRolls: new Dictionary<ushort, uint>
            {
                [ArcherPreProcCrescentShadowSpellId] = 0,
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
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(primary, attackType)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(ArcherPreProcCrescentShadowSpellId, animation.SpellId);
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(primary.X, animation.X);
        Assert.Equal(primary.Y, animation.Y);
        Assert.Contains(animation.Targets, target => target.Uid == checked((uint)primary.Id));
        Assert.True(sobNpcStates.GetVisible(primary).Life < primary.Life);
        AssertNoKineticSparkAnimation(sent);
        Assert.Contains("WorldServer handled legacy Archer pre-Kinetic SOB NPC proc", log.ToString());
    }

    [Fact]
    public async Task HandlerArcherSobNpcAttackWithCombatGearSuppressesPreProcsAndKineticSpark()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var primary = CreateSobNpc(id: 65_587, name: "CombatGearPole", x: PlayerX + 1, y: PlayerY);
        var worldData = CreateWorldData(
            items: [CreateClubItemType()],
            magicRows:
            [
                CreateKineticSparkMagicRow(),
                CreateArcherPreProcMagicRow(ArcherPreProcCrescentShadowSpellId),
                CreateArcherPreProcMagicRow(ArcherPreProcNebulousHuntSpellId),
                CreateCombatGearMagicRow(CombatGearRedcurseSpellId),
            ],
            sobNpcs: [primary]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            forcedRateRoll: 0,
            spellIds:
            [
                KineticSparkSpellId,
                ArcherPreProcCrescentShadowSpellId,
                ArcherPreProcNebulousHuntSpellId,
            ],
            archerPreProcRateRolls: new Dictionary<ushort, uint>
            {
                [ArcherPreProcCrescentShadowSpellId] = 0,
                [ArcherPreProcNebulousHuntSpellId] = 0,
            },
            classCode: 2_005,
            combatGear: CreateCombatGear(CombatGearRedcurseItemId),
            extraCharacterAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [RuntimeLegacyCombatGearAttackSpellIndexAttribute] = "0",
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
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(primary, attackType)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(CombatGearRedcurseSpellId, animation.SpellId);
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(primary.X, animation.X);
        Assert.Equal(primary.Y, animation.Y);
        Assert.Contains(animation.Targets, target => target.Uid == checked((uint)primary.Id));
        Assert.True(sobNpcStates.GetVisible(primary).Life < primary.Life);

        var output = log.ToString();
        Assert.Contains("WorldServer replaced legacy Archer SOB NPC attack with CombatGear spell", output);
        Assert.Contains("spellId=17540", output);
        Assert.DoesNotContain("WorldServer handled legacy Archer pre-Kinetic SOB NPC proc", output);
        Assert.DoesNotContain("WorldServer handled legacy KineticSpark passive SOB NPC proc", output);
        AssertNoSpellAnimation(sent, ArcherPreProcCrescentShadowSpellId);
        AssertNoSpellAnimation(sent, ArcherPreProcNebulousHuntSpellId);
        AssertNoKineticSparkAnimation(sent);
        Assert.DoesNotContain(
            ParseAttacks(sent),
            attack => attack.Query.AttackType == attackType &&
                attack.Query.OpponentUid == checked((uint)primary.Id));
    }

    [Fact]
    public async Task HandlerArcherSobNpcAttackWithActiveKineticSparkChainsToNearbySobNpcs()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var primary = CreateSobNpc(id: 65_590, name: "PrimarySparkPole", x: PrimarySobNpcX, y: PrimarySobNpcY);
        var nearby = CreateSobNpc(id: 65_591, name: "NearbySparkPole", x: NearbySobNpcX, y: NearbySobNpcY);
        var outside = CreateSobNpc(id: 65_592, name: "OutsideSparkPole", x: OutsideSobNpcX, y: OutsideSobNpcY);
        var worldData = CreateWorldData(
            items: [CreateClubItemType()],
            magicRows: [CreateKineticSparkMagicRow()],
            sobNpcs: [primary, nearby, outside]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(primary, attackType)).ToFrameBytes()));

        var attackResponses = ParseAttacks(sent);

        var spark = Assert.Single(
            ParseSpellAnimations(sent),
            animation => animation.SpellId == KineticSparkSpellId);
        Assert.Equal(PlayerUid, spark.AttackerUid);
        Assert.Equal(0U, spark.AttackedUid);
        Assert.Equal(PrimarySobNpcX, spark.X);
        Assert.Equal(PrimarySobNpcY, spark.Y);
        Assert.Contains(spark.Targets, target => target.Uid == checked((uint)primary.Id));
        Assert.Contains(spark.Targets, target => target.Uid == checked((uint)nearby.Id));
        Assert.DoesNotContain(spark.Targets, target => target.Uid == checked((uint)outside.Id));
        Assert.All(spark.Targets, target => Assert.True(target.Hit));

        Assert.True(sobNpcStates.GetVisible(primary).Life < primary.Life);
        Assert.True(sobNpcStates.GetVisible(nearby).Life < nearby.Life);
        Assert.Equal(outside.Life, sobNpcStates.GetVisible(outside).Life);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.Equal(
            nearby.Id.ToString(CultureInfo.InvariantCulture),
            updatedSession!.Character.Attributes[RuntimeLegacyThundercloudSightAttribute]);
        AssertLegacyAttackHit(updatedSession!);
        Assert.DoesNotContain(
            attackResponses,
            attack => attack.Query.AttackType == attackType &&
                attack.Query.OpponentUid == checked((uint)primary.Id));
        Assert.True(
            sent.Count > attackResponses.Count,
            $"Expected a KineticSpark passive effect beyond ordinary MsgAttack responses. SentFrames={sent.Count}. Log={log}");
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
    public async Task HandlerArcherSobNpcAttackWithActiveKineticSparkGuildWarGateDeathSendsNewSynWarAndDemolitionSobNpcUpdate(
        int sobNpcId,
        string sobNpcName,
        int lookface,
        int expectedDemolitionMesh,
        uint expectedDestroyedDwParam)
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var primary = CreateSobNpc(
            id: sobNpcId,
            name: sobNpcName,
            x: PlayerX + 1,
            y: PlayerY,
            kind: LegacySobNpcEventRules.GateKind,
            life: 1,
            lookface: lookface,
            mapId: LegacySobNpcEventRules.GuildWarMapId);
        var worldData = CreateWorldData(
            items: [CreateClubItemType()],
            magicRows: [CreateKineticSparkMagicRow()],
            sobNpcs: [primary]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var guildWarStates = new LegacyGuildWarRuntimeStateStore();
        const uint gateOwnerGuildId = 88;
        sobNpcStates.SetOwnerGuildId(primary, gateOwnerGuildId);
        guildWarStates.Start();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: LegacySobNpcEventRules.GuildWarMapId,
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
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(primary, attackType)).ToFrameBytes()));

        var visibleSobNpc = sobNpcStates.GetVisible(primary);
        Assert.Equal(primary.MaxLife, visibleSobNpc.Life);
        Assert.Equal(primary.MaxLife, visibleSobNpc.MaxLife);
        Assert.Equal(expectedDemolitionMesh, visibleSobNpc.Lookface);
        Assert.Equal(gateOwnerGuildId, sobNpcStates.GetOwnerGuildId(primary));

        var spawn = Assert.Single(ParseSobNpcSpawns(sent));
        Assert.Equal(checked((uint)primary.Id), spawn.Uid);
        Assert.Equal((uint)primary.MaxLife, spawn.Hitpoints);
        Assert.Equal((uint)primary.MaxLife, spawn.MaxHitpoints);
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
        Assert.Contains("WorldServer handled legacy KineticSpark passive SOB NPC proc", output);
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
    public async Task HandlerArcherSobNpcAttackWithActiveKineticSparkGuildWarCapturePillarDeathSendsNewSynWarAndCaptureSobNpcUpdate(
        int sobNpcId,
        string sobNpcName,
        uint expectedOwnershipDwParam)
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var primary = CreateSobNpc(
            id: sobNpcId,
            name: sobNpcName,
            x: PlayerX + 1,
            y: PlayerY,
            kind: LegacySobNpcEventRules.GuildWarCapturePillarKind,
            life: 1,
            mapId: LegacySobNpcEventRules.GuildWarMapId);
        var worldData = CreateWorldData(
            items: [CreateClubItemType()],
            magicRows: [CreateKineticSparkMagicRow()],
            sobNpcs: [primary]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var guildWarStates = new LegacyGuildWarRuntimeStateStore();
        guildWarStates.Start();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: LegacySobNpcEventRules.GuildWarMapId,
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
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(primary, attackType)).ToFrameBytes()));

        var visibleSobNpc = sobNpcStates.GetVisible(primary);
        var ownerGuildId = sobNpcStates.GetOwnerGuildId(primary);
        Assert.Equal(77U, ownerGuildId);
        Assert.Equal("SourceGuild", visibleSobNpc.Name);
        Assert.Equal(primary.MaxLife, visibleSobNpc.Life);
        Assert.Equal(primary.MaxLife, visibleSobNpc.MaxLife);
        Assert.Equal(primary.Lookface, visibleSobNpc.Lookface);

        var spawn = Assert.Single(ParseSobNpcSpawns(sent));
        Assert.Equal(checked((uint)primary.Id), spawn.Uid);
        Assert.Equal((uint)primary.MaxLife, spawn.Hitpoints);
        Assert.Equal((uint)primary.MaxLife, spawn.MaxHitpoints);
        Assert.Equal(primary.Lookface, spawn.Mesh);
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
        Assert.Contains("WorldServer handled legacy KineticSpark passive SOB NPC proc", output);
    }

    [Fact]
    public async Task HandlerArcherSobNpcAttackWithActiveKineticSparkAppliesAxeShadowSobReceiveDamageAfterExistingModifiers()
    {
        const uint hookMoonPower = 150;
        const uint cleanSweepPower = 40;
        const uint axeShadowPower = 25;
        var existingModifiers = await RunKineticSparkSobNpcDamageProbeAsync(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(
                    KineticSparkStatusFlag,
                    HookMoonCasterStatusFlag,
                    CleanSweepStatusFlag),
                ["HookMoonPower"] = hookMoonPower.ToString(CultureInfo.InvariantCulture),
                ["CleanSweepPower"] = cleanSweepPower.ToString(CultureInfo.InvariantCulture),
            });

        var withAxeShadow = await RunKineticSparkSobNpcDamageProbeAsync(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(
                    KineticSparkStatusFlag,
                    HookMoonCasterStatusFlag,
                    CleanSweepStatusFlag,
                    AxeShadowStatusFlag),
                ["HookMoonPower"] = hookMoonPower.ToString(CultureInfo.InvariantCulture),
                ["CleanSweepPower"] = cleanSweepPower.ToString(CultureInfo.InvariantCulture),
                ["AxeShadowPower"] = axeShadowPower.ToString(CultureInfo.InvariantCulture),
            });

        var expectedDamage = AddLegacyPercent(existingModifiers.RequestedDamage, axeShadowPower);
        Assert.Equal(expectedDamage, withAxeShadow.RequestedDamage);
        Assert.Equal(50_000 - checked((int)expectedDamage), withAxeShadow.VisibleLife);
        Assert.True(withAxeShadow.RequestedDamage > existingModifiers.RequestedDamage);
    }

    [Fact]
    public async Task HandlerArcherSobNpcAttackWithActiveKineticSparkAppliesCelestialDanceAfterHookMoonCleanSweepAndAxeShadow()
    {
        const uint hookMoonPower = 150;
        const uint cleanSweepPower = 40;
        const uint axeShadowPower = 25;
        const uint celestialDancePower = 60;
        var baseline = await RunKineticSparkSobNpcDamageProbeAsync();

        var withAllModifiers = await RunKineticSparkSobNpcDamageProbeAsync(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(
                    KineticSparkStatusFlag,
                    HookMoonCasterStatusFlag,
                    CleanSweepStatusFlag,
                    AxeShadowStatusFlag,
                    CelestialDanceStatusFlag),
                ["HookMoonPower"] = hookMoonPower.ToString(CultureInfo.InvariantCulture),
                ["CleanSweepPower"] = cleanSweepPower.ToString(CultureInfo.InvariantCulture),
                ["AxeShadowPower"] = axeShadowPower.ToString(CultureInfo.InvariantCulture),
                ["CelestialDancePower"] = celestialDancePower.ToString(CultureInfo.InvariantCulture),
            });

        var afterHookMoon = MulLegacyPercent(baseline.RequestedDamage, hookMoonPower);
        var afterCleanSweep = AddLegacyPercent(afterHookMoon, cleanSweepPower);
        var afterAxeShadow = AddLegacyPercent(afterCleanSweep, axeShadowPower);
        var expectedDamage = MulLegacyPercent(afterAxeShadow, celestialDancePower);

        Assert.Equal(expectedDamage, withAllModifiers.RequestedDamage);
        Assert.Equal(50_000 - checked((int)expectedDamage), withAllModifiers.VisibleLife);
    }

    [Fact]
    public async Task HandlerArcherSobNpcAttackWithActiveKineticSparkChargesAttackerStoneCrackerArrowBladesAndStarFlow()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var primary = CreateSobNpc(id: 65_598, name: "StoneCrackerChargePole", x: PrimarySobNpcX, y: PrimarySobNpcY);
        var worldData = CreateWorldData(
            items: [CreateClubItemType()],
            magicRows:
            [
                CreateKineticSparkMagicRow(),
                CreateStoneCrackerStarFlowMagicRow(),
            ],
            sobNpcs: [primary],
            combatGearDefinitions:
            [
                CreateCombatGearDefinition(CombatGearStoneCrackerItemId, CombatGearStoneCrackerStarFlowSpellId),
            ]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            forcedRateRoll: 0,
            spellIds:
            [
                KineticSparkSpellId,
                CombatGearStoneCrackerStarFlowSpellId,
            ],
            classCode: 4_001,
            combatGear: CreateCombatGear(CombatGearStoneCrackerItemId),
            extraCharacterAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [RuntimeLegacyStoneCrackerArrowBladesAttribute] = "2",
                [RuntimeLegacyStoneCrackerStarFlowAttribute] = "0",
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
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(primary, attackType)).ToFrameBytes()));

        var kinetic = Assert.Single(ParseSpellAnimations(sent), animation => animation.SpellId == KineticSparkSpellId);
        Assert.Contains(kinetic.Targets, target => target.Uid == checked((uint)primary.Id));

        Assert.Contains("StoneCracker", log.ToString());

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal("3", updatedSession.Character.Attributes[RuntimeLegacyStoneCrackerArrowBladesAttribute]);
        Assert.Equal("1", updatedSession.Character.Attributes[RuntimeLegacyStoneCrackerStarFlowAttribute]);
        Assert.True(HasLegacyStatusFlag(
            updatedSession.Character.Attributes,
            CombatGearStoneCrackerWaterTop4WeeklyFlag));
        Assert.True(HasLegacyStatusFlag(
            updatedSession.Character.Attributes,
            CombatGearStoneCrackerSageSpeedModeTop4WeeklyFlag));

        AssertCombatGearStatusFlagUpdate(sent, PlayerUid, CombatGearStoneCrackerWaterTop4WeeklyFlag);
        AssertCombatGearStatusFlagUpdate(sent, PlayerUid, CombatGearStoneCrackerSageSpeedModeTop4WeeklyFlag);
        AssertStoneCrackerWaterAppendIconUpdate(sent, PlayerUid);
        AssertStoneCrackerStarFlowChargeUpdate(sent, PlayerUid, expectedStarFlow: 1);
    }

    [Fact]
    public async Task HandlerArcherSobNpcAttackWithActiveKineticSparkThirdStoneCrackerStarFlowFiresSpellAtSobTargetAndResets()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var primary = CreateSobNpc(id: 65_599, name: "StoneCrackerStarFlowPole", x: PrimarySobNpcX, y: PrimarySobNpcY);
        var worldData = CreateWorldData(
            items: [CreateClubItemType()],
            magicRows:
            [
                CreateKineticSparkMagicRow(),
                CreateStoneCrackerStarFlowMagicRow(),
            ],
            sobNpcs: [primary],
            combatGearDefinitions:
            [
                CreateCombatGearDefinition(CombatGearStoneCrackerItemId, CombatGearStoneCrackerStarFlowSpellId),
            ]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            forcedRateRoll: 0,
            spellIds:
            [
                KineticSparkSpellId,
                CombatGearStoneCrackerStarFlowSpellId,
            ],
            classCode: 4_001,
            combatGear: CreateCombatGear(CombatGearStoneCrackerItemId),
            x: 208, // Current owner is2 from primary210; source10950 is owner-centered.
            extraCharacterAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(
                    KineticSparkStatusFlag,
                    CombatGearStoneCrackerSageSpeedModeTop4WeeklyFlag),
                [RuntimeLegacyStoneCrackerArrowBladesAttribute] = "0",
                [RuntimeLegacyStoneCrackerStarFlowAttribute] = "2",
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
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(primary, attackType)).ToFrameBytes()));

        var animations = ParseSpellAnimations(sent);
        var kinetic = Assert.Single(animations, animation => animation.SpellId == KineticSparkSpellId);
        Assert.Contains(kinetic.Targets, target => target.Uid == checked((uint)primary.Id));

        Assert.Contains("StoneCracker", log.ToString());

        var starFlow = Assert.Single(
            animations,
            animation => animation.SpellId == CombatGearStoneCrackerStarFlowSpellId);
        Assert.Equal(PlayerUid, starFlow.AttackerUid);
        Assert.Equal((ushort)208, starFlow.X);
        Assert.Equal(PlayerY, starFlow.Y);
        var starFlowTarget = Assert.Single(starFlow.Targets, target => target.Uid == checked((uint)primary.Id));
        Assert.True(starFlowTarget.Hit);
        Assert.True(starFlowTarget.Damage > 0);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal("0", updatedSession.Character.Attributes[RuntimeLegacyStoneCrackerStarFlowAttribute]);
        Assert.False(HasLegacyStatusFlag(
            updatedSession.Character.Attributes,
            CombatGearStoneCrackerSageSpeedModeTop4WeeklyFlag));
        Assert.True(HasLegacyStatusFlag(
            updatedSession.Character.Attributes,
            CombatGearStoneCrackerLightningTop4WeeklyFlag));
        AssertCombatGearStatusFlagUpdate(sent, PlayerUid, CombatGearStoneCrackerLightningTop4WeeklyFlag);
    }

    [Fact]
    public async Task HandlerArcherSobNpcThirdStarFlowExcludesOutsidePrimaryFromOwnerOriginWithoutSkippingKineticHit()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var primary = CreateSobNpc(id: 65_598, name: "OutsideStarFlowPole", x: PrimarySobNpcX, y: PrimarySobNpcY);
        var worldData = CreateWorldData(
            items: [CreateClubItemType()],
            magicRows:
            [
                CreateKineticSparkMagicRow(),
                CreateStoneCrackerStarFlowMagicRow(),
            ],
            sobNpcs: [primary],
            combatGearDefinitions:
            [
                CreateCombatGearDefinition(CombatGearStoneCrackerItemId, CombatGearStoneCrackerStarFlowSpellId),
            ]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        // Same controlled pre-third state as the inherited positive fixture, not acquisition proof.
        var session = CreateSession(
            forcedRateRoll: 0,
            spellIds: [KineticSparkSpellId, CombatGearStoneCrackerStarFlowSpellId],
            classCode: 4_001,
            combatGear: CreateCombatGear(CombatGearStoneCrackerItemId),
            extraCharacterAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(
                    KineticSparkStatusFlag, CombatGearStoneCrackerSageSpeedModeTop4WeeklyFlag),
                [RuntimeLegacyStoneCrackerArrowBladesAttribute] = "0",
                [RuntimeLegacyStoneCrackerStarFlowAttribute] = "2",
            });
        Assert.Equal((ushort)205, checked((ushort)session.Character.X));
        Assert.Equal(5, Math.Max(Math.Abs(session.Character.X - primary.X), Math.Abs(session.Character.Y - primary.Y)));
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(connection, log, session, WorldData: worldData,
                Sessions: sessions, SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(primary, attackType)).ToFrameBytes()));

        var animations = ParseSpellAnimations(sent);
        var kinetic = Assert.Single(animations, animation => animation.SpellId == KineticSparkSpellId);
        var kineticTarget = Assert.Single(kinetic.Targets, target => target.Uid == checked((uint)primary.Id));
        Assert.True(kineticTarget.Hit);
        Assert.True(kineticTarget.Damage > 0);
        Assert.True(sobNpcStates.GetVisible(primary).Life < primary.Life);
        var starFlow = Assert.Single(animations, animation => animation.SpellId == CombatGearStoneCrackerStarFlowSpellId);
        Assert.Equal(PlayerUid, starFlow.AttackerUid);
        Assert.Equal(PlayerX, starFlow.X);
        Assert.Equal(PlayerY, starFlow.Y);
        Assert.Empty(starFlow.Targets); // Owner205/230 is5 away; source10950 radius is2.5, not target-centered.
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal("0", updatedSession.Character.Attributes[RuntimeLegacyStoneCrackerStarFlowAttribute]);
        Assert.Equal("1", updatedSession.Character.Attributes[RuntimeLegacyStoneCrackerArrowBladesAttribute]);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, CombatGearStoneCrackerSageSpeedModeTop4WeeklyFlag));
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, CombatGearStoneCrackerLightningTop4WeeklyFlag));
        AssertCombatGearStatusFlagUpdate(sent, PlayerUid, CombatGearStoneCrackerLightningTop4WeeklyFlag);
        Assert.Equal(session.Character.MinMana, updatedSession.Character.MinMana);
        Assert.Contains("StoneCracker", log.ToString());
    }

    [Fact]
    public async Task HandlerArcherSobNpcAttackWithActiveKineticSparkTriggersReceiveAttackNpcCombatGearTaoistDeadwoodCurseUpdate()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        const uint duration = 8;
        const uint damageOnHuman = 1_000;
        const uint damage2 = 5;
        var primary = CreateSobNpc(id: 65_597, name: "DeadwoodReceivePole", x: PrimarySobNpcX, y: PrimarySobNpcY);
        var worldData = CreateWorldData(
            items: [CreateClubItemType()],
            magicRows:
            [
                CreateKineticSparkMagicRow(),
                CreateCombatGearDeadwoodCurseMagicRow(duration, damageOnHuman, damage2),
            ],
            sobNpcs: [primary]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            forcedRateRoll: 0,
            spellIds:
            [
                KineticSparkSpellId,
                CombatGearDeadwoodCurseSpellId,
            ],
            classCode: 13_005,
            combatGear: CreateCombatGear(CombatGearTaoist1ItemId));
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(primary, attackType)).ToFrameBytes()));

        var kinetic = Assert.Single(ParseSpellAnimations(sent), animation => animation.SpellId == KineticSparkSpellId);
        Assert.Contains(kinetic.Targets, target => target.Uid == checked((uint)primary.Id));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, CombatGearDeadwoodCurseFlag));
        Assert.Equal(
            damageOnHuman.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes[RuntimeLegacyDeadwoodCurseDamageOnHumanAttribute]);
        Assert.Equal(
            damage2.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes[RuntimeLegacyDeadwoodCurseDamage2Attribute]);

        AssertCombatGearStatusFlagUpdate(sent, PlayerUid, CombatGearDeadwoodCurseFlag);
        AssertCombatGearArchiveSkillUpdate(sent, PlayerUid, CombatGearDeadwoodCurseFlag, duration);
        AssertCombatGearSelfAnimation(sent, PlayerUid, CombatGearDeadwoodCurseSpellId);

        var output = log.ToString();
        Assert.Contains("DeadwoodCurse", output);
        Assert.Contains("targetUid=" + primary.Id.ToString(CultureInfo.InvariantCulture), output);
    }

    [Fact]
    public async Task HandlerArcherSobNpcAttackWithActiveKineticSparkSkipsPlayerTargetOnlyCombatGearTaoistCrackMantra()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var primary = CreateSobNpc(id: 65_602, name: "CrackMantraReceivePole", x: PrimarySobNpcX, y: PrimarySobNpcY);
        var worldData = CreateWorldData(
            items: [CreateClubItemType()],
            magicRows:
            [
                CreateKineticSparkMagicRow(),
                CreateCombatGearCrackMantraMagicRow(),
            ],
            sobNpcs: [primary]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            forcedRateRoll: 0,
            spellIds:
            [
                KineticSparkSpellId,
                CombatGearCrackMantraSpellId,
            ],
            classCode: 13_005,
            combatGear: CreateCombatGear(CombatGearTaoist3ItemId));
        var beforeStatusFlags = session.Character.Attributes["StatusFlags"];
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(primary, attackType)).ToFrameBytes()));

        var kinetic = Assert.Single(ParseSpellAnimations(sent), animation => animation.SpellId == KineticSparkSpellId);
        var kineticTarget = Assert.Single(kinetic.Targets, target => target.Uid == checked((uint)primary.Id));
        Assert.True(kineticTarget.Hit);
        Assert.True(kineticTarget.Damage > 0);
        Assert.True(sobNpcStates.GetVisible(primary).Life < primary.Life);

        Assert.DoesNotContain(
            ParseAttacks(sent),
            packet => packet.Query.SpellId == CombatGearCrackMantraSpellId);
        AssertNoSpellAnimation(sent, CombatGearCrackMantraSpellId);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(beforeStatusFlags, updatedSession.Character.Attributes["StatusFlags"]);
        Assert.DoesNotContain(
            ParseMsgUpdates(sent),
            packet => packet.Uid == PlayerUid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    (IsLegacyStatusFlagSet(entry.Values, CombatGearCrackMantraPhysicalFlag) ||
                        IsLegacyStatusFlagSet(entry.Values, CombatGearCrackMantraMagicFlag))));
        Assert.DoesNotContain(
            ParseMsgUpdates(sent),
            packet => packet.Uid == PlayerUid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    (entry.Flag == checked((uint)CombatGearCrackMantraPhysicalFlag) ||
                        entry.Flag == checked((uint)CombatGearCrackMantraMagicFlag))));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy KineticSpark passive SOB NPC proc", output);
        Assert.DoesNotContain("CrackMantra", output);
    }

    [Fact]
    public async Task HandlerArcherSobNpcAttackWithActiveKineticSparkTriggersReceiveAttackNpcCombatGearPirateArmedAndSenseUpdate()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        const uint duration = 10;
        var primary = CreateSobNpc(id: 65_596, name: "PirateReceivePole", x: PrimarySobNpcX, y: PrimarySobNpcY);
        var worldData = CreateWorldData(
            items: [CreateClubItemType()],
            magicRows:
            [
                CreateKineticSparkMagicRow(),
                CreateCombatGearPirateArmedMagicRow(),
                CreateCombatGearPirateSenseMagicRow(),
            ],
            sobNpcs: [primary]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            forcedRateRoll: 0,
            spellIds:
            [
                KineticSparkSpellId,
                CombatGearPirateArmedSpellId,
                CombatGearPirateSenseSpellId,
            ],
            classCode: 7_005,
            combatGear: CreateCombatGear(CombatGearPirate1ItemId));
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(primary, attackType)).ToFrameBytes()));

        var kinetic = Assert.Single(ParseSpellAnimations(sent), animation => animation.SpellId == KineticSparkSpellId);
        Assert.Contains(kinetic.Targets, target => target.Uid == checked((uint)primary.Id));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, CombatGearPirateArmedFlag));
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, CombatGearPirateSenseFlag));

        AssertCombatGearStatusFlagUpdate(sent, PlayerUid, CombatGearPirateArmedFlag);
        AssertCombatGearStatusFlagUpdate(sent, PlayerUid, CombatGearPirateSenseFlag);
        AssertCombatGearArchiveSkillUpdate(sent, PlayerUid, CombatGearPirateArmedFlag, duration);
        AssertCombatGearArchiveSkillUpdate(sent, PlayerUid, CombatGearPirateSenseFlag, duration);
        AssertCombatGearSelfAnimation(sent, PlayerUid, CombatGearPirateArmedSpellId);
        AssertCombatGearSelfAnimation(sent, PlayerUid, CombatGearPirateSenseSpellId);
    }

    [Fact]
    public async Task HandlerArcherSobNpcAttackWithActiveKineticSparkTriggersReceiveAttackNpcCombatGearPirateDarkNoStatusMutation()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var primary = CreateSobNpc(id: 65_601, name: "PirateDarkReceivePole", x: PrimarySobNpcX, y: PrimarySobNpcY);
        var worldData = CreateWorldData(
            items: [CreateClubItemType()],
            magicRows:
            [
                CreateKineticSparkMagicRow(),
                CreateCombatGearPirateDarkMagicRow(),
            ],
            sobNpcs: [primary]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            forcedRateRoll: 0,
            spellIds:
            [
                KineticSparkSpellId,
                CombatGearPirateDarkSpellId,
            ],
            classCode: 7_005,
            combatGear: CreateCombatGear(CombatGearPirate3ItemId));
        var beforeStatusFlags = session.Character.Attributes["StatusFlags"];
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(primary, attackType)).ToFrameBytes()));

        var kinetic = Assert.Single(ParseSpellAnimations(sent), animation => animation.SpellId == KineticSparkSpellId);
        Assert.Contains(kinetic.Targets, target => target.Uid == checked((uint)primary.Id));

        var darkInteraction = Assert.Single(
            ParseAttacks(sent),
            packet => packet.Query.SpellId == CombatGearPirateDarkSpellId);
        Assert.Equal(PlayerUid, darkInteraction.Query.Uid);
        Assert.Equal(checked((uint)primary.Id), darkInteraction.Query.OpponentUid);
        Assert.Equal(primary.X, darkInteraction.Query.X);
        Assert.Equal(primary.Y, darkInteraction.Query.Y);
        Assert.Equal(MsgAttackType.None, darkInteraction.Query.AttackType);
        Assert.Equal(0, darkInteraction.Query.Damage);

        var darkAnimation = Assert.Single(
            ParseSpellAnimations(sent),
            animation => animation.SpellId == CombatGearPirateDarkSpellId);
        Assert.Equal(PlayerUid, darkAnimation.AttackerUid);
        Assert.Equal(0U, darkAnimation.AttackedUid);
        Assert.Equal(primary.X, darkAnimation.X);
        Assert.Equal(primary.Y, darkAnimation.Y);
        Assert.Empty(darkAnimation.Targets);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(beforeStatusFlags, updatedSession.Character.Attributes["StatusFlags"]);
        Assert.DoesNotContain(
            ParseMsgUpdates(sent),
            packet => packet.Uid == PlayerUid &&
                packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        Assert.Contains("SOB Pirate Dark no-op", log.ToString());
    }

    [Fact]
    public async Task HandlerArcherSobNpcAttackWithActiveKineticSparkTriggersReceiveAttackNpcNinjaSageModeUsingChance()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        const uint duration = 10;
        var primary = CreateSobNpc(id: 65_600, name: "NinjaSageReceivePole", x: PrimarySobNpcX, y: PrimarySobNpcY);
        var worldData = CreateWorldData(
            items: [CreateClubItemType()],
            magicRows: [CreateKineticSparkMagicRow()],
            sobNpcs: [primary],
            gouyuImmortals: [CreateGouyuImmortal(level: 17, chance: 5_000, rate: 0, seconds: checked((int)duration))]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            forcedRateRoll: 0,
            classCode: 5_005,
            extraCharacterAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [RuntimeLegacyNinjaSageModeRollAttribute] = "0",
                ["MsgNinja"] = MsgNinjaWithEquippedItems((itemType: 104U, level: 8U, position: 1U), (105U, 9U, 2U)),
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
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(primary, attackType)).ToFrameBytes()));

        var kinetic = Assert.Single(ParseSpellAnimations(sent), animation => animation.SpellId == KineticSparkSpellId);
        Assert.Contains(kinetic.Targets, target => target.Uid == checked((uint)primary.Id));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(
            HasLegacyStatusFlag(updatedSession.Character.Attributes, NinjaSageSpeedModeFlag),
            "Expected ReceiveAttack.Npc Ninja SageMode to trigger from GouyuImmortal Chance while Rate is zero.");

        AssertCombatGearStatusFlagUpdate(sent, PlayerUid, NinjaSageSpeedModeFlag);
        AssertCombatGearArchiveSkillUpdate(sent, PlayerUid, NinjaSageSpeedModeFlag, duration);
    }

    [Fact]
    public async Task HandlerArcherSobNpcAttackRejectsSobNpcBeforePreProcAndKineticRateRolls()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var primary = CreateSobNpc(
            id: 65_593,
            name: "RejectedSparkSob",
            x: PrimarySobNpcX,
            y: PrimarySobNpcY,
            kind: 2,
            life: 0,
            maxLife: 0);
        var worldData = CreateWorldData(
            items: [CreateClubItemType()],
            magicRows:
            [
                CreateKineticSparkMagicRow(),
                CreateArcherPreProcMagicRow(ArcherPreProcCrescentShadowSpellId),
            ],
            sobNpcs: [primary]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            forcedRateRoll: 0,
            spellIds: [KineticSparkSpellId, ArcherPreProcCrescentShadowSpellId],
            archerPreProcRateRolls: new Dictionary<ushort, uint>
            {
                [ArcherPreProcCrescentShadowSpellId] = 0,
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
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(primary, attackType)).ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains("WorldServer rejected legacy Archer pre-Kinetic SOB NPC target", output);
        Assert.Contains("source CanAttackNpc.Verified rejected dead SOB NPC", output);
        Assert.DoesNotContain("WorldServer handled legacy Archer pre-Kinetic SOB NPC proc", output);
        AssertNoKineticSparkAnimation(sent);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        AssertNoLegacyAttackHit(updatedSession!);
    }

    [Fact]
    public async Task HandlerArcherSobNpcAttackBeyondDirectRangeSuppressesPreProcsKineticSparkAndFallback()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var primary = CreateSobNpc(
            id: 65_589,
            name: "BeyondDirectRangePole",
            x: BeyondDirectRangeSobNpcX,
            y: BeyondDirectRangeSobNpcY);
        var worldData = CreateWorldData(
            items: [CreateClubItemType()],
            magicRows:
            [
                CreateKineticSparkMagicRow(),
                CreateArcherPreProcMagicRow(ArcherPreProcCrescentShadowSpellId),
                CreateArcherPreProcMagicRow(ArcherPreProcNebulousHuntSpellId),
            ],
            sobNpcs: [primary]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            forcedRateRoll: 0,
            spellIds:
            [
                KineticSparkSpellId,
                ArcherPreProcCrescentShadowSpellId,
                ArcherPreProcNebulousHuntSpellId,
            ],
            archerPreProcRateRolls: new Dictionary<ushort, uint>
            {
                [ArcherPreProcCrescentShadowSpellId] = 0,
                [ArcherPreProcNebulousHuntSpellId] = 0,
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
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(primary, attackType)).ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains("WorldServer rejected legacy Archer SOB target", output);
        Assert.Contains("sobNpcUid=" + primary.Id.ToString(CultureInfo.InvariantCulture), output);
        Assert.Contains("distance=19 range=18 autoAttack=false", output);
        Assert.Contains("source=MsgAttackPacket.cs AttackID.Archer SOB branch Base.GetDistance <= 18 gate", output);
        Assert.DoesNotContain("WorldServer handled legacy Archer pre-Kinetic SOB NPC proc", output);
        Assert.DoesNotContain("WorldServer handled legacy KineticSpark passive SOB NPC proc", output);
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Empty(ParseAttacks(sent));
        Assert.Equal(primary.Life, sobNpcStates.GetVisible(primary).Life);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        AssertNoLegacyAttackHit(updatedSession!);
    }

    [Fact]
    public async Task HandlerArcherSobNpcAttackBeyondTwoTilesSkipsCrescentShadowAndAllowsKineticSpark()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var primary = CreateSobNpc(id: 65_594, name: "FarPreProcPole", x: PrimarySobNpcX, y: PrimarySobNpcY);
        var worldData = CreateWorldData(
            items: [CreateClubItemType()],
            magicRows: [CreateKineticSparkMagicRow(), CreateArcherPreProcMagicRow(ArcherPreProcCrescentShadowSpellId)],
            sobNpcs: [primary]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            forcedRateRoll: 0,
            spellIds: [KineticSparkSpellId, ArcherPreProcCrescentShadowSpellId],
            archerPreProcRateRolls: new Dictionary<ushort, uint>
            {
                [ArcherPreProcCrescentShadowSpellId] = 0,
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
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(primary, attackType)).ToFrameBytes()));

        Assert.DoesNotContain(
            ParseSpellAnimations(sent),
            animation => animation.SpellId == ArcherPreProcCrescentShadowSpellId);
        var kinetic = Assert.Single(ParseSpellAnimations(sent), animation => animation.SpellId == KineticSparkSpellId);
        Assert.Contains(kinetic.Targets, target => target.Uid == checked((uint)primary.Id));
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.Equal(
            primary.Id.ToString(CultureInfo.InvariantCulture),
            updatedSession!.Character.Attributes[RuntimeLegacyThundercloudSightAttribute]);
        AssertLegacyAttackHit(updatedSession);
        var output = log.ToString();
        Assert.DoesNotContain("WorldServer handled legacy Archer pre-Kinetic SOB NPC proc", output);
        Assert.Contains("WorldServer handled legacy KineticSpark passive SOB NPC proc", output);
    }

    [Fact]
    public async Task HandlerArcherSobNpcAttackCanUseSectorBackPreProcBeyondTwoTiles()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var primary = CreateSobNpc(id: 65_595, name: "SectorBackPole", x: PrimarySobNpcX, y: PrimarySobNpcY);
        var worldData = CreateWorldData(
            items: [CreateClubItemType()],
            magicRows: [CreateKineticSparkMagicRow(), CreateArcherPreProcMagicRow(ArcherPreProcNebulousHuntSpellId)],
            sobNpcs: [primary]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [KineticSparkSpellId, ArcherPreProcNebulousHuntSpellId],
            archerPreProcRateRolls: new Dictionary<ushort, uint>
            {
                [ArcherPreProcNebulousHuntSpellId] = 0,
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
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(primary, attackType)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(ArcherPreProcNebulousHuntSpellId, animation.SpellId);
        AssertNoKineticSparkAnimation(sent);
        Assert.Contains("WorldServer handled legacy Archer pre-Kinetic SOB NPC proc", log.ToString());
    }

    private static MsgAttackQuery CreateSobNpcAttack(SobNpc sobNpc, MsgAttackType attackType)
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: checked((uint)sobNpc.Id),
            X: checked((ushort)sobNpc.X),
            Y: checked((ushort)sobNpc.Y),
            AttackType: attackType,
            Damage: 500,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0);
    }

    private static async Task<LegacySobNpcDamageProbe> RunKineticSparkSobNpcDamageProbeAsync(
        IReadOnlyDictionary<string, string>? extraCharacterAttributes = null)
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var primary = CreateSobNpc(id: 65_595, name: "ModifierSparkPole", x: PrimarySobNpcX, y: PrimarySobNpcY);
        var worldData = CreateWorldData(
            items: [CreateClubItemType()],
            magicRows: [CreateKineticSparkMagicRow()],
            sobNpcs: [primary]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(forcedRateRoll: 0, extraCharacterAttributes: extraCharacterAttributes);
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(primary, attackType)).ToFrameBytes()));

        var kinetic = Assert.Single(ParseSpellAnimations(sent), animation => animation.SpellId == KineticSparkSpellId);
        var target = Assert.Single(kinetic.Targets, target => target.Uid == checked((uint)primary.Id));
        Assert.True(target.Hit);

        var visible = sobNpcStates.GetVisible(primary);
        Assert.Equal(primary.Life - checked((int)target.Damage), visible.Life);
        Assert.Contains("WorldServer handled legacy KineticSpark passive SOB NPC proc", log.ToString());
        return new LegacySobNpcDamageProbe(target.Damage, visible.Life);
    }

    private static WorldSession CreateSession(
        int forcedRateRoll = 0,
        IReadOnlyList<ushort>? spellIds = null,
        IReadOnlyDictionary<ushort, uint>? archerPreProcRateRolls = null,
        int classCode = 142,
        CombatGearSnapshot? combatGear = null,
        IReadOnlyDictionary<string, string>? extraCharacterAttributes = null,
        int mapId = MapId,
        ushort x = PlayerX,
        ushort y = PlayerY)
    {
        spellIds ??= [KineticSparkSpellId];
        return new WorldSession(
            AccountId: 1,
            Username: "caster",
            CreateCharacter(forcedRateRoll, archerPreProcRateRolls, classCode, extraCharacterAttributes, mapId, x, y),
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
            CombatGear: combatGear ?? CombatGearSnapshot.Empty);
    }

    private static CharacterSnapshot CreateCharacter(
        int forcedRateRoll,
        IReadOnlyDictionary<ushort, uint>? archerPreProcRateRolls,
        int classCode,
        IReadOnlyDictionary<string, string>? extraCharacterAttributes,
        int mapId,
        ushort x,
        ushort y)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
            [RuntimeLegacyKineticSparkPassiveRateRollAttribute] =
                forcedRateRoll.ToString(CultureInfo.InvariantCulture),
        };
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
            Uid: PlayerUid,
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
            Strength: 1_000,
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
            SourceFile: "fixture://kinetic-spark-passive-sob-npc-runtime",
            Attributes: attributes,
            PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private static SobNpc CreateSobNpc(
        int id,
        string name,
        ushort x,
        ushort y,
        int kind = 10,
        int life = 50_000,
        int maxLife = 50_000,
        int lookface = 1_137,
        int mapId = MapId)
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

    private static Dictionary<string, string> CreateGuildAttributes()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["GuildID"] = "77",
            ["GuildName"] = "SourceGuild",
            ["GuildRank"] = "100",
        };
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

    private static CombatGearSnapshot CreateCombatGear(uint itemId)
    {
        return new CombatGearSnapshot(
            MasteryPoints: 0,
            TaoistReiki: 0,
            PirateExp: 0,
            Lottery: 0,
            Count: 1,
            CombatGearAirPowersSnapshot.Empty,
            Items:
            [
                new CombatGearSnapshotItem(
                    itemId,
                    Level: 10,
                    Exp: 0,
                    Equip: 1,
                    Transform: 0,
                    MasteryScore: 0,
                    Animas:
                    [
                        [0U, 0U, 0U, 0U, 0U, 0U, 0U],
                        [0U, 0U, 0U, 0U, 0U, 0U, 0U],
                    ],
                    BlackJades: [0U, 0U, 0U, 0U, 0U, 0U]),
            ],
            CombatGearBlackJadeBagSnapshot.Empty);
    }

    private static CombatGear CreateCombatGearDefinition(uint itemId, params ushort[] spellIds)
    {
        var encodedSkills = spellIds.Select(spellId => checked(spellId * 1000)).ToArray();
        return new CombatGear(
            Id: checked((int)itemId),
            GearTypeId: checked((int)itemId),
            Level: 1,
            UpExperience: 0,
            Life: 0,
            Attack: 0,
            Defense: 0,
            UnknownStat0: 0,
            MagicDefense: 0,
            MagicTypeIds: encodedSkills,
            Score: 0,
            Unknown0: 0,
            Unknown1: 0,
            Unknown2: 0,
            SourceFile: "fixture://combat_gear",
            LineNumber: 1,
            RawLine: "",
            Fields: []);
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
        fields[15] = "8";
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

    private static MagicType CreateCombatGearMagicRow(ushort spellId)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = AttackSort.ToString(CultureInfo.InvariantCulture);
        fields[3] = "RedcurseCombatGear";
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = "120";
        fields[13] = "0";
        fields[14] = "1";
        fields[15] = "9";
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
        return new MagicType(spellId, spellId, "RedcurseCombatGear", fields);
    }

    private static MagicType CreateStoneCrackerStarFlowMagicRow(uint damageOnHuman = 450)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = CombatGearStoneCrackerStarFlowSpellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = CombatGearStoneCrackerStarFlowSpellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = BombSort.ToString(CultureInfo.InvariantCulture); // Matching10950/queryOpponent0 is Bomb5.
        fields[3] = "StarFlow";
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = damageOnHuman.ToString(CultureInfo.InvariantCulture);
        fields[12] = "10";
        fields[13] = "10";
        fields[14] = "1";
        fields[15] = "3";
        fields[18] = "0";
        fields[20] = "1";
        fields[22] = "0";
        fields[29] = "0";
        fields[33] = "0";
        fields[35] = damageOnHuman.ToString(CultureInfo.InvariantCulture);
        fields[36] = "0";
        fields[37] = "0";
        fields[44] = "0";
        fields[47] = "0";
        return new MagicType(
            CombatGearStoneCrackerStarFlowSpellId,
            CombatGearStoneCrackerStarFlowSpellId,
            "StarFlow",
            fields);
    }

    private static MagicType CreateCombatGearDeadwoodCurseMagicRow(
        uint duration,
        uint damageOnHuman,
        uint damage2)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = CombatGearDeadwoodCurseSpellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = CombatGearDeadwoodCurseSpellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = AttachStatusSort.ToString(CultureInfo.InvariantCulture);
        fields[3] = "DeadwoodCurse";
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = "0";
        fields[12] = "1000";
        fields[13] = duration.ToString(CultureInfo.InvariantCulture);
        fields[14] = "0";
        fields[15] = "16";
        fields[18] = "0";
        fields[20] = "1";
        fields[22] = "0";
        fields[29] = "0";
        fields[33] = "0";
        fields[35] = damageOnHuman.ToString(CultureInfo.InvariantCulture);
        fields[36] = damage2.ToString(CultureInfo.InvariantCulture);
        fields[37] = "0";
        fields[44] = "0";
        fields[47] = "0";
        return new MagicType(
            CombatGearDeadwoodCurseSpellId,
            CombatGearDeadwoodCurseSpellId,
            "DeadwoodCurse",
            fields);
    }

    private static MagicType CreateCombatGearCrackMantraMagicRow()
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = CombatGearCrackMantraSpellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = CombatGearCrackMantraSpellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = AttachStatusSort.ToString(CultureInfo.InvariantCulture);
        fields[3] = "CrackMantra";
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = "0";
        fields[12] = "100";
        fields[13] = "5";
        fields[14] = "0";
        fields[15] = "18";
        fields[18] = "0";
        fields[20] = "1";
        fields[22] = "0";
        fields[29] = "0";
        fields[33] = "0";
        fields[35] = "1";
        fields[36] = "1";
        fields[37] = "0";
        fields[44] = "0";
        fields[47] = "0";
        return new MagicType(
            CombatGearCrackMantraSpellId,
            CombatGearCrackMantraSpellId,
            "CrackMantra",
            fields);
    }

    private static MagicType CreateCombatGearPirateArmedMagicRow()
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = CombatGearPirateArmedSpellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = CombatGearPirateArmedSpellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = AttachStatusSort.ToString(CultureInfo.InvariantCulture);
        fields[3] = "Armed";
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = "3000";
        fields[12] = "100";
        fields[13] = "10";
        fields[14] = "1";
        fields[15] = "18";
        fields[16] = CombatGearPirateArmedFlag.ToString(CultureInfo.InvariantCulture);
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
        return new MagicType(
            CombatGearPirateArmedSpellId,
            CombatGearPirateArmedSpellId,
            "Armed",
            fields);
    }

    private static MagicType CreateCombatGearPirateSenseMagicRow()
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = CombatGearPirateSenseSpellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = CombatGearPirateSenseSpellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = AttachStatusSort.ToString(CultureInfo.InvariantCulture);
        fields[3] = "Sense";
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = "0";
        fields[12] = "100";
        fields[13] = "10";
        fields[14] = "1";
        fields[15] = "18";
        fields[16] = CombatGearPirateSenseFlag.ToString(CultureInfo.InvariantCulture);
        fields[18] = "0";
        fields[20] = "1";
        fields[22] = "0";
        fields[29] = "0";
        fields[33] = "0";
        fields[35] = "3000";
        fields[36] = "1000";
        fields[37] = "0";
        fields[44] = "0";
        fields[47] = "0";
        return new MagicType(
            CombatGearPirateSenseSpellId,
            CombatGearPirateSenseSpellId,
            "Sense",
            fields);
    }

    private static MagicType CreateCombatGearPirateDarkMagicRow()
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = CombatGearPirateDarkSpellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = CombatGearPirateDarkSpellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = AttachStatusSort.ToString(CultureInfo.InvariantCulture);
        fields[3] = "Dark";
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = "0";
        fields[12] = "100";
        fields[13] = "0";
        fields[14] = "1";
        fields[15] = "18";
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
        return new MagicType(
            CombatGearPirateDarkSpellId,
            CombatGearPirateDarkSpellId,
            "Dark",
            fields);
    }

    private static MagicType CreateArcherPreProcMagicRow(ushort spellId)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = (spellId == ArcherPreProcNebulousHuntSpellId ? SectorBackSort : BombSort)
            .ToString(CultureInfo.InvariantCulture);
        fields[3] = spellId == ArcherPreProcNebulousHuntSpellId ? "NebulousHunt" : "CrescentShadow";
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = "120";
        fields[12] = "30";
        fields[13] = "0";
        fields[14] = "1";
        fields[15] = "9";
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
        return new MagicType(
            spellId,
            spellId,
            spellId == ArcherPreProcNebulousHuntSpellId ? "NebulousHunt" : "CrescentShadow",
            fields);
    }

    private static WorldDataCatalog CreateWorldData(
        IReadOnlyList<ItemType> items,
        IReadOnlyList<MagicType> magicRows,
        IReadOnlyList<SobNpc> sobNpcs,
        IReadOnlyList<CombatGear>? combatGearDefinitions = null,
        IReadOnlyList<GouyuImmortal>? gouyuImmortals = null)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-23T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://kinetic-spark-passive-sob-npc-runtime",
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
            gouyuImmortals: gouyuImmortals ?? [],
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
            combatGear: combatGearDefinitions ?? [],
            traps: [],
            npcs: [],
            sobNpcs: sobNpcs,
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

    private static GouyuImmortal CreateGouyuImmortal(int level, int chance, int rate, int seconds)
    {
        var values = Enumerable.Repeat(0, 15).ToArray();
        values[0] = level;
        values[5] = chance;
        values[6] = seconds;
        values[7] = rate;
        return new GouyuImmortal(
            Id: level,
            Values: values,
            SourceFile: "fixture://gouyu_immortal",
            LineNumber: 1,
            RawLine: "",
            Fields: []);
    }

    private static string MsgNinjaWithEquippedItems(params (uint itemType, uint level, uint position)[] items)
    {
        var fields = Enumerable.Repeat("0", 20)
            .Concat(items.SelectMany(item =>
                new[]
                {
                    item.itemType.ToString(CultureInfo.InvariantCulture),
                    item.level.ToString(CultureInfo.InvariantCulture),
                    item.position.ToString(CultureInfo.InvariantCulture),
                    "0",
                    "0",
                }));
        return string.Join('/', fields) + "/";
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

    private static void AssertStoneCrackerWaterAppendIconUpdate(IEnumerable<byte[]> frames, uint uid)
    {
        Assert.Contains(
            ParseMsgUpdates(frames),
            packet => packet.Uid == uid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.AppendIcon &&
                    entry.Flag == (uint)CombatGearStoneCrackerWaterTop4WeeklyFlag &&
                    entry.Damage == 2));
    }

    private static void AssertStoneCrackerStarFlowChargeUpdate(
        IEnumerable<byte[]> frames,
        uint uid,
        uint expectedStarFlow)
    {
        Assert.Contains(
            ParseMsgUpdates(frames),
            packet => packet.Uid == uid &&
                packet.Updates.Any(entry =>
                    entry.Type == CombatGearStoneCrackerStarFlowUpdateType &&
                    entry.Flag == (uint)CombatGearStoneCrackerSageSpeedModeTop4WeeklyFlag &&
                    (entry.Damage == expectedStarFlow || entry.Value == expectedStarFlow)));
    }

    private static void AssertCombatGearStatusFlagUpdate(IEnumerable<byte[]> frames, uint uid, int flag)
    {
        Assert.Contains(
            ParseMsgUpdates(frames),
            packet => packet.Uid == uid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, flag)));
    }

    private static void AssertCombatGearArchiveSkillUpdate(
        IEnumerable<byte[]> frames,
        uint uid,
        int flag,
        uint duration)
    {
        Assert.Contains(
            ParseMsgUpdates(frames),
            packet => packet.Uid == uid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == checked((uint)flag) &&
                    entry.Time == duration));
    }

    private static LegacySpellAnimationPacket AssertCombatGearSelfAnimation(
        IEnumerable<byte[]> frames,
        uint uid,
        ushort spellId)
    {
        var animation = Assert.Single(
            ParseSpellAnimations(frames),
            packet => packet.SpellId == spellId);
        Assert.Equal(uid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(0, animation.X);
        Assert.Equal(0, animation.Y);

        var target = Assert.Single(animation.Targets);
        Assert.Equal(uid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        return animation;
    }

    private static void AssertNoSpellAnimation(IEnumerable<byte[]> frames, ushort spellId)
    {
        Assert.DoesNotContain(
            ParseSpellAnimations(frames),
            animation => animation.SpellId == spellId);
    }

    private static void AssertNoKineticSparkAnimation(IEnumerable<byte[]> frames)
    {
        AssertNoSpellAnimation(frames, KineticSparkSpellId);
    }

    private static void AssertLegacyAttackHit(WorldSession session)
    {
        Assert.True(
            session.Character.Attributes.TryGetValue(RuntimeLegacyAttackHitAttribute, out var rawAttackHit),
            "Expected legacy KineticSpark passive SOB NPC hit to set AttackHit.");
        Assert.True(
            bool.TryParse(rawAttackHit, out var attackHit),
            "Expected legacy KineticSpark passive SOB NPC AttackHit value to be a boolean.");
        Assert.True(attackHit);
    }

    private static void AssertNoLegacyAttackHit(WorldSession session)
    {
        Assert.False(
            session.Character.Attributes.ContainsKey(RuntimeLegacyAttackHitAttribute),
            "Expected rejected legacy KineticSpark passive SOB NPC path to leave AttackHit unset.");
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

    private static string BuildStatusFlags(params int[] flags)
    {
        var words = new ulong[18];
        foreach (var flag in flags)
        {
            words[flag / 32] |= 1UL << (flag % 32);
        }

        return string.Join('/', words);
    }

    private static uint AddLegacyPercent(uint damage, uint percent)
    {
        return checked(damage + MulLegacyPercent(damage, percent));
    }

    private static uint MulLegacyPercent(uint damage, uint percent)
    {
        return checked(damage * percent / 100);
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
}
