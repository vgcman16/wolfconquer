using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyKineticSparkPassivePlayerRuntimeTests
{
    private const uint PlayerUid = 1_293_600;
    private const uint PrimaryPlayerUid = 1_293_601;
    private const uint NearbyPlayerUid = 1_293_602;
    private const uint OutsidePlayerUid = 1_293_603;
    private const uint OutOfRangePlayerUid = 1_293_604;
    private const int BowItemType = 500_000;
    private const ushort ArcherPreKineticCloseProcSpellId = 18_750;
    private const ushort ArcherPreKineticFallbackProcSpellId = 10_990;
    private const ushort CombatGearColdMoonSpellId = 11_950;
    private const ushort CombatGearHunterSpellId = 10_890;
    private const ushort CombatGearRedcursePassiveSpellId = 17_580;
    private const ushort CombatGearDragonhowlSpellId = 17_620;
    private const ushort CombatGearBloodlustImmersionSpellId = 17_600;
    private const ushort CombatGearBloodlustInsoucianceSpellId = 17_610;
    private const ushort CombatGearArchShadowSpellId = 18_980;
    private const ushort CombatGearStoneCrackerStarFlowSpellId = 10_950;
    private const ushort CombatGearFlowKnackSpellId = 20_210;
    private const ushort CombatGearWaterAegisSpellId = 20_230;
    private const ushort CombatGearFantasyKnackSpellId = 20_240;
    private const ushort CombatGearDivineEmptinessSpellId = 20_260;
    private const ushort CombatGearFlameShieldSpellId = 20_290;
    private const ushort CombatGearDeadwoodCurseSpellId = 20_330;
    private const ushort CombatGearCrackMantraSpellId = 20_380;
    private const ushort CombatGearMagneticLightSpellId = 20_220;
    private const ushort CombatGearAblazeBladeSpellId = 20_360;
    private const ushort CombatGearWildPhoenixSpellId = 20_370;
    private const ushort CombatGearPirateArmedSpellId = 21_710;
    private const ushort CombatGearPirateSenseSpellId = 21_720;
    private const ushort CombatGearPirateFusingSpellId = 21_590;
    private const ushort CombatGearPirateSailingSpellId = 21_810;
    private const ushort CombatGearPirateDarkSpellId = 21_930;
    private const ushort KineticSparkSpellId = 11_590;
    private const ushort MudWallSpellId = 16_440;
    private const ushort LegacyInfiniteMistSpellId = 16_380;
    private const ushort VenomSpellId = 20_000;
    private const uint CombatGearDragonhowlItemId = 0xC9;
    private const uint CombatGearBloodlustItemId = 0xCA;
    private const uint CombatGearRedcurseItemId = 0xCB;
    private const uint CombatGearStoneCrackerItemId = 0x191;
    private const uint CombatGearColdMoonItemId = 0x192;
    private const uint CombatGearTaoist1ItemId = 0x515;
    private const uint CombatGearTaoist2ItemId = 0x516;
    private const uint CombatGearTaoist3ItemId = 0x517;
    private const uint CombatGearTaoist4ItemId = 0x518;
    private const uint CombatGearTaoist5ItemId = 0x519;
    private const uint CombatGearPirate1ItemId = 701;
    private const uint CombatGearPirate2ItemId = 702;
    private const uint CombatGearPirate3ItemId = 703;
    private const int LegacyMagicShieldFlag = 0x08;
    private const int LegacyStigmaFlag = 0x09;
    private const int LegacyHawksAmbitionFlag = 0x100;
    private const int CombatGearArchShadowFlag = 0x05 | 0x40 | 0x100;
    private const int CombatGearStoneCrackerWaterTop4WeeklyFlag = 0x10D | 0x40;
    private const int CombatGearStoneCrackerLightningTop4WeeklyFlag = 0x110 | 0x40;
    private const int CombatGearStoneCrackerSageSpeedModeTop4WeeklyFlag = 0x111 | 0x40;
    private const int CombatGearLightningMagicShieldTop4WeeklyFlag = 0x158;
    private const int CombatGearVenomMagicShieldFlag = 0x16B;
    private const int CombatGearFlowKnackFlag = 0x29 | 0x40 | 0x100;
    private const int CombatGearWaterAegisFlag = 0x5D;
    private const int CombatGearFantasyKnackFlag = 0x79 | 0x100;
    private const int CombatGearDivineEmptinessFlag = 0x5D | 0x120;
    private const int CombatGearFlameShieldFlag = 0x3C | 0x40 | 0x100;
    private const int CombatGearImmersionFlag = 0x12F;
    private const int CombatGearInsoucianceFlag = 0x130;
    private const int CombatGearDragonPierceCritFlag = 0x133;
    private const int CombatGearDragonPierceTortoiseFlag = 0x134;
    private const int CombatGearDragonPierceBreakFlag = 0x135;
    private const int CombatGearDragonPierceStaminaFlag = 0x136;
    private const int CombatGearCrackMantraPhysicalFlag = 0x17A;
    private const int CombatGearCrackMantraMagicFlag = 0x17B;
    private const int CombatGearDeadwoodCurseFlag = 0x17E;
    private const int CombatGearWildPhoenixFlag = 0x170;
    private const int CombatGearPirateSenseFlag = 0x19C;
    private const int CombatGearPirateArmedFlag = 0x19D;
    private const int CombatGearPirateFusingFlag = 0x199;
    private const int CombatGearPirateSailingFlag = 0x1A3;
    private const int LegacyVenomFlag = 0x163;
    private const int LegacySolidFlag = 0x164;
    private const int LegacyInfiniteMistFlag = 0x11A;
    private const int NinjaSageSpeedModeFlag = 0x111;
    private const LegacyMsgUpdateDataType CombatGearStoneCrackerStarFlowUpdateType =
        (LegacyMsgUpdateDataType)136;
    private const byte BombSort = 5;
    private const byte SectorBackSort = 69;
    private const byte AttackSort = 1;
    private const byte AttachStatusSort = 6;
    private const byte KineticSparkSort = 67;
    private const int KineticSparkStatusFlag = 0x93;
    private const int LegacyMudWallFlag = 0x112;
    private const uint LegacyMudSigilImpeccableType = 301;
    private const string RuntimeLegacyArcherPreKineticProcRateRollAttribute = "LegacyArcherPreProcRateRoll";
    private const string RuntimeLegacyKineticSparkPassiveRateRollAttribute = "LegacyKineticSparkPassiveRateRoll";
    private const string RuntimeLegacyMudWallRollAttribute = "LegacyMudWallRoll";
    private const string RuntimeLegacyCombatGearPassiveRollAttribute = "LegacyCombatGearPassiveRoll";
    private const string RuntimeLegacyCombatGearAttackRateRollAttribute = "LegacyCombatGearAttackRateRoll";
    private const string RuntimeLegacyCombatGearAttackSpellIndexAttribute = "LegacyCombatGearAttackSpellIndex";
    private const string RuntimeLegacySpeffectRollAttribute = "LegacySpeffectRoll";
    private const string RuntimeLegacySpeffectLevelAttribute = "LegacySpeffectLevel";
    private const string RuntimeLegacyInfiniteMistRollAttribute = "LegacyInfiniteMistRoll";
    private const string RuntimeLegacyNinjaSageModeRollAttribute = "LegacyNinjaSageModeRoll";
    private const string RuntimeLegacyNinjaLevelsAttribute = "LegacyNinjaLevels";
    private const string RuntimeLegacyThundercloudSightAttribute = "ThundercloudSight";
    private const string RuntimeLegacyStoneCrackerArrowBladesAttribute = "ArrowBlades";
    private const string RuntimeLegacyStoneCrackerStarFlowAttribute = "StarFlow";
    private const string RuntimeLegacyCombatGearDragonhowlTortoiseRollAttribute =
        "LegacyCombatGearDragonhowlRoll.17620.Tortoise";
    private const string RuntimeLegacyCombatGearDragonhowlBreakRollAttribute =
        "LegacyCombatGearDragonhowlRoll.17620.Break";
    private const string RuntimeLegacyCombatGearTaoistCrackMantraRollAttribute =
        "LegacyCombatGearTaoistRoll.20380.CrackMantra";
    private const string RuntimeLegacyDragonPierceTortoiseAttribute = "DragonPierceTortoise";
    private const string RuntimeLegacyDragonPierceBreakAttribute = "DragonPierceBreak";
    private const string RuntimeLegacyDragonPierceStaminaAttribute = "DragonPierceStamina";
    private const string RuntimeLegacyImmersionPowerAttribute = "ImmersionPower";
    private const string RuntimeLegacyInsouciancePowerAttribute = "InsouciancePower";
    private const string RuntimeLegacyCrackMantra1Attribute = "CrackMantra1";
    private const string RuntimeLegacyCrackMantra2Attribute = "CrackMantra2";
    private const string RuntimeLegacyDeadwoodCurseDamageOnHumanAttribute = "DeadwoodCurseDamageOnHuman";
    private const string RuntimeLegacyDeadwoodCurseDamage2Attribute = "DeadwoodCurseDamage2";
    private const string RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute =
        "LegacyCombatGearWildPhoenixPackedDamage";
    private const string RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute =
        "LegacyCombatGearWildPhoenixChargeCount";
    private const string RuntimeLegacyVenomDamageAttribute = "Venom";
    private const string RuntimeLegacyVenomExpiresAtUtcAttribute = "LegacyVenomExpiresAtUtc";
    private const int MapId = 1_015;
    private const ushort PlayerX = 830;
    private const ushort PlayerY = 584;
    private const ushort PrimaryPlayerX = 832;
    private const ushort PrimaryPlayerY = 584;
    private const ushort NearbyPlayerX = 835;
    private const ushort NearbyPlayerY = 584;
    private const ushort OutsidePlayerX = 836;
    private const ushort OutsidePlayerY = 584;
    private const ushort OutOfRangePlayerX = 849;
    private const ushort OutOfRangePlayerY = 584;

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    [Theory]
    [InlineData(MsgAttackType.Archer)]
    public async Task HandlerPlayerAttackWithActiveKineticSparkChainsToNearbyVisiblePlayers(MsgAttackType attackType)
    {
        var spellsRoot = Path.Combine(
            Path.GetTempPath(),
            "WolfConquer-KineticSpark-Player-Spells-" + Guid.NewGuid().ToString("N"));
        var profsRoot = Path.Combine(
            Path.GetTempPath(),
            "WolfConquer-KineticSpark-Player-Profs-" + Guid.NewGuid().ToString("N"));
        try
        {
            var learnedKineticSpark = CreatePlayerSpell(KineticSparkSpellId, experience: 12_345);
            var startingProficiency = new PlayerProficiencySnapshotEntry(
                500,
                Level: 5,
                PreviousLevel: 0,
                Experience: 456,
                NeededExperience: LegacyProficiencyPacket.GetNeededExperience(5));
            var playerSpells = new PlayerSpellSnapshot([learnedKineticSpark]);
            var playerProficiencies = new PlayerProficiencySnapshot([startingProficiency]);
            new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(PlayerUid, playerSpells);
            new PlayerProficiencySnapshotLoader(profsRoot).SaveByCharacterUid(PlayerUid, playerProficiencies);

            var worldData = CreateWorldData([CreateBowItemType()], [CreateKineticSparkMagicRow()]);
            var sessions = new WorldSessionStore();
            var casterSent = new List<byte[]>();
            var primarySent = new List<byte[]>();
            var nearbySent = new List<byte[]>();
            var outsideSent = new List<byte[]>();
            using var log = new StringWriter();
            var casterConnection = CreateSendingConnection(casterSent);
            var primaryConnection = CreateSendingConnection(primarySent);
            var nearbyConnection = CreateSendingConnection(nearbySent);
            var outsideConnection = CreateSendingConnection(outsideSent);
            var casterSession = CreateSession(
                PlayerUid,
                "Caster",
                PlayerX,
                PlayerY,
                playerItems: CreateEquippedBow(),
                playerSpells: playerSpells,
                playerProficiencies: playerProficiencies,
                attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
                    [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
                });
            var primarySession = CreateSession(PrimaryPlayerUid, "Primary", PrimaryPlayerX, PrimaryPlayerY);
            var nearbySession = CreateSession(NearbyPlayerUid, "Nearby", NearbyPlayerX, NearbyPlayerY);
            var outsideSession = CreateSession(OutsidePlayerUid, "Outside", OutsidePlayerX, OutsidePlayerY);
            sessions.Set(casterConnection, casterSession);
            sessions.Set(primaryConnection, primarySession);
            sessions.Set(nearbyConnection, nearbySession);
            sessions.Set(outsideConnection, outsideSession);

            await PacketHandlers.HandleAsync(
                new WorldPacketHandlerContext(
                    casterConnection,
                    log,
                    casterSession,
                    WorldData: worldData,
                    Sessions: sessions,
                    PlayerSpellsRoot: spellsRoot,
                    PlayerProficienciesRoot: profsRoot),
                ReadFrame(new MsgAttackPacket(CreatePlayerAttack(primarySession, attackType)).ToFrameBytes()));

            var attackResponses = ParseAttacks(casterSent);
            var spark = Assert.Single(
                ParseSpellAnimations(casterSent),
                animation => animation.SpellId == KineticSparkSpellId);
            Assert.Equal(PlayerUid, spark.AttackerUid);
            Assert.Equal(0U, spark.AttackedUid);
            Assert.Equal(PrimaryPlayerX, spark.X);
            Assert.Equal(PrimaryPlayerY, spark.Y);
            Assert.Contains(spark.Targets, target => target.Uid == PrimaryPlayerUid);
            Assert.Contains(spark.Targets, target => target.Uid == NearbyPlayerUid);
            Assert.DoesNotContain(spark.Targets, target => target.Uid == OutsidePlayerUid);
            Assert.All(spark.Targets, target => Assert.True(target.Hit));
            Assert.DoesNotContain(
                attackResponses,
                attack => attack.Query.AttackType == attackType &&
                    attack.Query.OpponentUid == PrimaryPlayerUid);

            Assert.True(sessions.TryGet(primaryConnection, out var updatedPrimarySession));
            Assert.NotNull(updatedPrimarySession);
            Assert.True(updatedPrimarySession.Character.MinHitPoints < primarySession.Character.MinHitPoints);
            Assert.Equal(
                PlayerUid.ToString(CultureInfo.InvariantCulture),
                updatedPrimarySession.Character.Attributes[RuntimeLegacyThundercloudSightAttribute]);
            Assert.True(sessions.TryGet(nearbyConnection, out var updatedNearbySession));
            Assert.NotNull(updatedNearbySession);
            Assert.True(updatedNearbySession.Character.MinHitPoints < nearbySession.Character.MinHitPoints);
            Assert.Equal(
                PlayerUid.ToString(CultureInfo.InvariantCulture),
                updatedNearbySession.Character.Attributes[RuntimeLegacyThundercloudSightAttribute]);
            Assert.True(sessions.TryGet(outsideConnection, out var updatedOutsideSession));
            Assert.NotNull(updatedOutsideSession);
            Assert.Equal(outsideSession.Character.MinHitPoints, updatedOutsideSession.Character.MinHitPoints);
            Assert.False(
                updatedOutsideSession.Character.Attributes.ContainsKey(RuntimeLegacyThundercloudSightAttribute));

            Assert.Empty(ParseProficiencyPackets(casterSent));
            Assert.Equal(
                playerProficiencies.Proficiencies,
                new PlayerProficiencySnapshotLoader(profsRoot).LoadByCharacterUid(PlayerUid).Proficiencies);
            Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
            Assert.NotNull(updatedCasterSession);
            Assert.Equal(
                spark.Targets[spark.Targets.Count - 1].Uid.ToString(CultureInfo.InvariantCulture),
                updatedCasterSession.Character.Attributes[RuntimeLegacyThundercloudSightAttribute]);
            Assert.Equal(playerSpells.Spells, updatedCasterSession.PlayerSpells.Spells);
            Assert.Equal(
                playerSpells.Spells,
                new PlayerSpellSnapshotLoader(spellsRoot).LoadByCharacterUid(PlayerUid).Spells);
        }
        finally
        {
            if (Directory.Exists(spellsRoot))
            {
                Directory.Delete(spellsRoot, recursive: true);
            }

            if (Directory.Exists(profsRoot))
            {
                Directory.Delete(profsRoot, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(true, 2_850U)]
    [InlineData(false, 3_000U)]
    public async Task HandlerPlayerAttackWithActiveKineticSparkAppliesOnlyDirectMudSigilImpeccableReduction(
        bool mudWallFlag,
        uint expectedDamage)
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        const byte mudWallLevel = 2;
        const uint startingDamage = 10_000;
        const uint mudWallRate = 1_000;
        const uint mudWallReduction = 2_000;
        const int sigilLevel = 7;
        const int sigilPower = 500;
        var playerSpells = new PlayerSpellSnapshot([CreatePlayerSpell(KineticSparkSpellId)]);
        var defenderSpells = new PlayerSpellSnapshot([CreatePlayerSpell(MudWallSpellId, mudWallLevel)]);
        var worldData = CreateWorldData(
            [CreateBowItemType(attack: startingDamage - 1_000)],
            [
                CreateKineticSparkMagicRow(),
                CreateMudWallMagicRow(mudWallLevel, mudWallRate, mudWallReduction),
            ],
            gouyuTypes:
            [
                CreateGouyuType(
                    checked((int)LegacyMudSigilImpeccableType),
                    checked((int)LegacyMudSigilImpeccableType),
                    sigilLevel,
                    sigilPower),
            ]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var defenderSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var defenderConnection = CreateSendingConnection(defenderSent);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            playerItems: CreateEquippedBow(),
            playerSpells: playerSpells,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
            });
        var defenderAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MsgNinja"] = MsgNinjaWithEquippedItems(
                (LegacyMudSigilImpeccableType, checked((uint)sigilLevel), 1U)),
            [RuntimeLegacyMudWallRollAttribute] = "0",
        };
        if (mudWallFlag)
        {
            defenderAttributes["StatusFlags"] = BuildStatusFlags(LegacyMudWallFlag);
        }

        var defenderSession = CreateSession(
            PrimaryPlayerUid,
            "Defender",
            PrimaryPlayerX,
            PrimaryPlayerY,
            classCode: 5_005,
            playerSpells: defenderSpells,
            attributes: defenderAttributes);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(defenderConnection, defenderSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(defenderSession, attackType)).ToFrameBytes()));

        var spark = Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.SpellId == KineticSparkSpellId);
        var target = Assert.Single(spark.Targets, target => target.Uid == PrimaryPlayerUid);
        Assert.True(target.Hit);

        Assert.True(sessions.TryGet(defenderConnection, out var updatedDefenderSession));
        Assert.NotNull(updatedDefenderSession);
        Assert.Equal(
            defenderSession.Character.MinHitPoints - expectedDamage,
            updatedDefenderSession.Character.MinHitPoints);
    }

    [Fact]
    public async Task HandlerPlayerAttackWithActiveKineticSparkConsumesWildPhoenixOnChainedPlayer()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        const uint wildPhoenixDuration = 32;
        const uint wildPhoenixPackedDamage = 18_007;
        var playerSpells = new PlayerSpellSnapshot([CreatePlayerSpell(KineticSparkSpellId)]);
        var nearbyPlayerSpells = new PlayerSpellSnapshot([CreatePlayerSpell(CombatGearWildPhoenixSpellId)]);
        var worldData = CreateWorldData(
            [CreateBowItemType()],
            [
                CreateKineticSparkMagicRow(),
                CreateCombatGearWildPhoenixMagicRow(),
            ]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var primarySent = new List<byte[]>();
        var nearbySent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var primaryConnection = CreateSendingConnection(primarySent);
        var nearbyConnection = CreateSendingConnection(nearbySent);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            playerItems: CreateEquippedBow(),
            playerSpells: playerSpells,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
            });
        var primarySession = CreateSession(PrimaryPlayerUid, "Primary", PrimaryPlayerX, PrimaryPlayerY);
        var nearbySession = CreateSession(
            NearbyPlayerUid,
            "Nearby",
            NearbyPlayerX,
            NearbyPlayerY,
            playerSpells: nearbyPlayerSpells,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(CombatGearWildPhoenixFlag),
                [RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] =
                    wildPhoenixPackedDamage.ToString(CultureInfo.InvariantCulture),
                [RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = "2",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(primaryConnection, primarySession);
        sessions.Set(nearbyConnection, nearbySession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(primarySession, attackType)).ToFrameBytes()));

        var spark = Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.SpellId == KineticSparkSpellId);
        var primaryTarget = Assert.Single(spark.Targets, target => target.Uid == PrimaryPlayerUid);
        var nearbyTarget = Assert.Single(spark.Targets, target => target.Uid == NearbyPlayerUid);
        Assert.True(primaryTarget.Hit);
        Assert.True(nearbyTarget.Hit);
        Assert.True(primaryTarget.Damage > 1U);
        Assert.Equal(0U, nearbyTarget.Damage);

        Assert.True(sessions.TryGet(primaryConnection, out var updatedPrimarySession));
        Assert.NotNull(updatedPrimarySession);
        Assert.True(updatedPrimarySession.Character.MinHitPoints < primarySession.Character.MinHitPoints);

        Assert.True(sessions.TryGet(nearbyConnection, out var updatedNearbySession));
        Assert.NotNull(updatedNearbySession);
        Assert.Equal(nearbySession.Character.MinHitPoints, updatedNearbySession.Character.MinHitPoints);
        Assert.Equal(
            "1",
            updatedNearbySession.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.True(HasLegacyStatusFlag(updatedNearbySession.Character.Attributes, CombatGearWildPhoenixFlag));

        AssertCombatGearArchiveSkillUpdate(
            nearbySent,
            NearbyPlayerUid,
            CombatGearWildPhoenixFlag,
            wildPhoenixDuration,
            wildPhoenixPackedDamage,
            spellLevel: 1);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy WildPhoenix defender charge", output);
        Assert.Contains("targetUid=" + NearbyPlayerUid.ToString(CultureInfo.InvariantCulture), output);
        Assert.Contains("remainingCharges=1", output);
        Assert.Contains("WorldServer handled legacy KineticSpark passive player proc", output);
    }

    [Fact]
    public async Task HandlerPlayerAttackWithActiveKineticSparkChainedPlayerInfiniteMistArchiveSkillPrecedesSparkAnimationAndKeepsDamage()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var playerSpells = new PlayerSpellSnapshot([CreatePlayerSpell(KineticSparkSpellId)]);
        var nearbyPlayerSpells = new PlayerSpellSnapshot([CreatePlayerSpell(LegacyInfiniteMistSpellId)]);
        var worldData = CreateWorldData(
            [CreateBowItemType()],
            [
                CreateKineticSparkMagicRow(),
                CreateInfiniteMistMagicRow(),
            ]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var primarySent = new List<byte[]>();
        var nearbySent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var primaryConnection = CreateSendingConnection(primarySent);
        var nearbyConnection = CreateSendingConnection(nearbySent);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            playerItems: CreateEquippedBow(),
            playerSpells: playerSpells,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
            });
        var primarySession = CreateSession(PrimaryPlayerUid, "Primary", PrimaryPlayerX, PrimaryPlayerY);
        var nearbySession = CreateSession(
            NearbyPlayerUid,
            "Nearby",
            NearbyPlayerX,
            NearbyPlayerY,
            classCode: 5_051,
            playerSpells: nearbyPlayerSpells,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [RuntimeLegacyInfiniteMistRollAttribute] = "0",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(primaryConnection, primarySession);
        sessions.Set(nearbyConnection, nearbySession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(primarySession, attackType)).ToFrameBytes()));

        var spark = Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.SpellId == KineticSparkSpellId);
        var nearbyTarget = Assert.Single(spark.Targets, target => target.Uid == NearbyPlayerUid);
        Assert.True(nearbyTarget.Hit);
        Assert.True(nearbyTarget.Damage > 0U);

        Assert.True(sessions.TryGet(nearbyConnection, out var updatedNearbySession));
        Assert.NotNull(updatedNearbySession);
        Assert.True(updatedNearbySession.Character.MinHitPoints < nearbySession.Character.MinHitPoints);

        AssertInfiniteMistArchiveSkillBeforeKineticSpark(
            casterSent,
            PlayerUid,
            expectedDuration: 2,
            expectedDamage: 1);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(
            HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, LegacyInfiniteMistFlag),
            "Expected KineticSpark player receive path to apply defender InfiniteMist to the attacker.");
    }

    [Fact]
    public async Task HandlerPlayerAttackWithActiveKineticSparkTriggersReceiveAttackPlayerNinjaSageModeUsingChance()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        const uint duration = 10;
        var playerSpells = new PlayerSpellSnapshot([CreatePlayerSpell(KineticSparkSpellId)]);
        var worldData = CreateWorldData(
            [CreateBowItemType()],
            [CreateKineticSparkMagicRow()],
            gouyuImmortals: [CreateGouyuImmortal(level: 17, chance: 5_000, rate: 0, seconds: checked((int)duration))]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var primarySent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var primaryConnection = CreateSendingConnection(primarySent);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            classCode: 5_005,
            playerItems: CreateEquippedBow(),
            playerSpells: playerSpells,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
                [RuntimeLegacyNinjaLevelsAttribute] = "17",
                [RuntimeLegacyNinjaSageModeRollAttribute] = "0",
            });
        var primarySession = CreateSession(PrimaryPlayerUid, "Primary", PrimaryPlayerX, PrimaryPlayerY);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(primaryConnection, primarySession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(primarySession, attackType)).ToFrameBytes()));

        var spark = Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.SpellId == KineticSparkSpellId);
        var primaryTarget = Assert.Single(spark.Targets, target => target.Uid == PrimaryPlayerUid);
        Assert.True(primaryTarget.Hit);
        Assert.True(primaryTarget.Damage > 0U);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(
            HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, NinjaSageSpeedModeFlag),
            "Expected ReceiveAttack.Player Ninja SageMode to trigger from GouyuImmortal Chance while Rate is zero.");
        AssertArchiveSkillBeforeKineticSpark(
            casterSent,
            PlayerUid,
            NinjaSageSpeedModeFlag,
            expectedDuration: duration,
            expectedDamage: 0);

        var output = log.ToString();
        Assert.Contains("ReceiveAttack.Player", output);
        Assert.Contains("Ninja SageMode", output);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandlerPlayerAttackWithActiveKineticSparkTriggersDefenderCombatGearUpdateTargetPassive(
        bool redcurseOnPrimary)
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var playerSpells = new PlayerSpellSnapshot([CreatePlayerSpell(KineticSparkSpellId)]);
        var worldData = CreateWorldData(
            [CreateBowItemType()],
            [
                CreateKineticSparkMagicRow(),
                CreateCombatGearRedcursePassiveMagicRow(),
            ],
            [CreateRedcurseCombatGearDefinition()]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var primarySent = new List<byte[]>();
        var nearbySent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var primaryConnection = CreateSendingConnection(primarySent);
        var nearbyConnection = CreateSendingConnection(nearbySent);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            playerItems: CreateEquippedBow(),
            playerSpells: playerSpells,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
            });
        var primarySession = CreateSession(
            PrimaryPlayerUid,
            "Primary",
            PrimaryPlayerX,
            PrimaryPlayerY,
            classCode: redcurseOnPrimary ? 2_005 : 142,
            combatGear: redcurseOnPrimary ? CreateEquippedCombatGear(CombatGearRedcurseItemId) : null,
            attributes: redcurseOnPrimary ? CreateForcedCombatGearPassiveAttributes() : null);
        var nearbySession = CreateSession(
            NearbyPlayerUid,
            "Nearby",
            NearbyPlayerX,
            NearbyPlayerY,
            classCode: redcurseOnPrimary ? 142 : 2_005,
            combatGear: redcurseOnPrimary ? null : CreateEquippedCombatGear(CombatGearRedcurseItemId),
            attributes: redcurseOnPrimary ? null : CreateForcedCombatGearPassiveAttributes());
        sessions.Set(casterConnection, casterSession);
        sessions.Set(primaryConnection, primarySession);
        sessions.Set(nearbyConnection, nearbySession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(primarySession, attackType)).ToFrameBytes()));

        var spark = Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.SpellId == KineticSparkSpellId);
        var defenderConnection = redcurseOnPrimary ? primaryConnection : nearbyConnection;
        var defenderSent = redcurseOnPrimary ? primarySent : nearbySent;
        var defenderUid = redcurseOnPrimary ? PrimaryPlayerUid : NearbyPlayerUid;
        Assert.Contains(spark.Targets, target => target.Uid == defenderUid);
        Assert.True(sessions.TryGet(defenderConnection, out var updatedDefenderSession));
        Assert.NotNull(updatedDefenderSession);
        Assert.True(HasLegacyStatusFlag(
            updatedDefenderSession.Character.Attributes,
            CombatGearLightningMagicShieldTop4WeeklyFlag));
        AssertCombatGearPassiveStatusUpdates(defenderSent, defenderUid, expectedCount: 2);
        AssertCombatGearPassiveArchiveSkills(defenderSent, defenderUid, expectedCount: 2);

        var combatGearAnimations = ParseSpellAnimations(defenderSent)
            .Where(animation => animation.SpellId == CombatGearRedcursePassiveSpellId)
            .ToArray();
        Assert.Equal(2, combatGearAnimations.Length);
        Assert.All(combatGearAnimations, combatGearAnimation =>
        {
            Assert.Equal(defenderUid, combatGearAnimation.AttackerUid);
            Assert.Contains(combatGearAnimation.Targets, target => target.Uid == defenderUid);
        });

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy KineticSpark passive player proc", output);
        Assert.Contains("WorldServer sent legacy CombatGear UpdateTarget passive status", output);
        Assert.Contains("attempts=2 applied=2", output);
        Assert.Contains("spellId=" + CombatGearRedcursePassiveSpellId.ToString(CultureInfo.InvariantCulture), output);
    }

    [Theory]
    [InlineData(CombatGearDivineEmptinessSpellId, CombatGearTaoist1ItemId, 13_005, CombatGearDivineEmptinessFlag, true, 50_061U)]
    [InlineData(CombatGearFlowKnackSpellId, CombatGearTaoist2ItemId, 13_005, CombatGearFlowKnackFlag, true, 50_062U)]
    [InlineData(CombatGearFantasyKnackSpellId, CombatGearTaoist4ItemId, 13_005, CombatGearFantasyKnackFlag, true, 50_063U)]
    [InlineData(CombatGearWaterAegisSpellId, CombatGearTaoist2ItemId, 13_005, CombatGearWaterAegisFlag, false, 50_064U)]
    [InlineData(CombatGearFlameShieldSpellId, CombatGearTaoist5ItemId, 14_005, CombatGearFlameShieldFlag, true, 50_065U)]
    [InlineData(CombatGearHunterSpellId, CombatGearColdMoonItemId, 4_001, CombatGearLightningMagicShieldTop4WeeklyFlag, false, 50_066U)]
    public async Task HandlerPlayerAttackWithActiveKineticSparkSendsDefenderCombatGearUpdateTargetEquipmentRefreshTail(
        ushort spellId,
        uint combatGearItemId,
        long defenderClassCode,
        int expectedStatusFlag,
        bool expectsArchiveSkill,
        uint defenderBowItemUid)
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var playerSpells = new PlayerSpellSnapshot([CreatePlayerSpell(KineticSparkSpellId)]);
        var defenderEquipment = CreateEquippedBow(defenderBowItemUid);
        var worldData = CreateWorldData(
            [CreateBowItemType()],
            [
                CreateKineticSparkMagicRow(),
                CreateCombatGearUpdateTargetPassiveMagicRow(spellId),
            ],
            [CreateCombatGearDefinition(combatGearItemId, spellId)]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var defenderSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var defenderConnection = CreateSendingConnection(defenderSent);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            playerItems: CreateEquippedBow(),
            playerSpells: playerSpells,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
            });
        var defenderSession = CreateSession(
            PrimaryPlayerUid,
            "Defender",
            PrimaryPlayerX,
            PrimaryPlayerY,
            classCode: defenderClassCode,
            playerItems: defenderEquipment,
            combatGear: CreateEquippedCombatGear(combatGearItemId),
            attributes: CreateForcedCombatGearPassiveAttributes(spellId));
        sessions.Set(casterConnection, casterSession);
        sessions.Set(defenderConnection, defenderSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(defenderSession, attackType)).ToFrameBytes()));

        var spark = Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.SpellId == KineticSparkSpellId);
        Assert.Contains(spark.Targets, target => target.Uid == PrimaryPlayerUid);

        Assert.True(sessions.TryGet(defenderConnection, out var updatedDefenderSession));
        Assert.NotNull(updatedDefenderSession);
        Assert.True(HasLegacyStatusFlag(updatedDefenderSession.Character.Attributes, expectedStatusFlag));
        AssertDefenderCombatGearEquipmentRefreshTail(
            defenderSent,
            PrimaryPlayerUid,
            spellId,
            expectedStatusFlag,
            defenderEquipment,
            expectsArchiveSkill);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy CombatGear UpdateTarget player defender passives", output);
        Assert.Contains("attempts=2 applied=2", output);
        Assert.Contains("spellId=" + spellId.ToString(CultureInfo.InvariantCulture), output);
        Assert.Contains("equipmentRefreshPackets=1", output);
    }

    [Fact]
    public async Task HandlerPlayerAttackWithActiveKineticSparkTriggersAttackerStoneCrackerArchShadowReceiveUpdate()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var playerSpells = new PlayerSpellSnapshot(
            [
                CreatePlayerSpell(KineticSparkSpellId),
                CreatePlayerSpell(CombatGearArchShadowSpellId),
            ]);
        var worldData = CreateWorldData(
            [CreateBowItemType()],
            [
                CreateKineticSparkMagicRow(),
                CreateCombatGearArchShadowMagicRow(),
            ],
            [CreateCombatGearDefinition(CombatGearStoneCrackerItemId, CombatGearArchShadowSpellId)]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var defenderSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var defenderConnection = CreateSendingConnection(defenderSent);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            classCode: 4_001,
            playerItems: CreateEquippedBow(),
            playerSpells: playerSpells,
            combatGear: CreateEquippedCombatGear(CombatGearStoneCrackerItemId),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
                [RuntimeLegacyCombatGearAttackRateRollAttribute + "." +
                    CombatGearArchShadowSpellId.ToString(CultureInfo.InvariantCulture)] = "0",
            });
        var defenderSession = CreateSession(PrimaryPlayerUid, "Defender", PrimaryPlayerX, PrimaryPlayerY);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(defenderConnection, defenderSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(defenderSession, attackType)).ToFrameBytes()));

        var spark = Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.SpellId == KineticSparkSpellId);
        Assert.Contains(spark.Targets, target => target.Uid == PrimaryPlayerUid);

        Assert.True(sessions.TryGet(defenderConnection, out var updatedDefenderSession));
        Assert.NotNull(updatedDefenderSession);
        Assert.True(HasLegacyStatusFlag(updatedDefenderSession.Character.Attributes, CombatGearArchShadowFlag));
        Assert.Contains(
            ParseMsgUpdates(defenderSent),
            packet => packet.Uid == PrimaryPlayerUid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, CombatGearArchShadowFlag)));

        var output = log.ToString();
        Assert.Contains("ArchShadow", output);
        Assert.Contains("spellId=" + CombatGearArchShadowSpellId.ToString(CultureInfo.InvariantCulture), output);
    }

    [Fact]
    public async Task HandlerPlayerAttackWithActiveKineticSparkSkipsAttackerStoneCrackerArchShadowOnRateMiss()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var playerSpells = new PlayerSpellSnapshot(
            [
                CreatePlayerSpell(KineticSparkSpellId),
                CreatePlayerSpell(CombatGearArchShadowSpellId),
            ]);
        var worldData = CreateWorldData(
            [CreateBowItemType()],
            [
                CreateKineticSparkMagicRow(),
                CreateCombatGearArchShadowMagicRow(),
            ],
            [CreateCombatGearDefinition(CombatGearStoneCrackerItemId, CombatGearArchShadowSpellId)]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var defenderSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var defenderConnection = CreateSendingConnection(defenderSent);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            classCode: 4_001,
            playerItems: CreateEquippedBow(),
            playerSpells: playerSpells,
            combatGear: CreateEquippedCombatGear(CombatGearStoneCrackerItemId),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
                [RuntimeLegacyCombatGearAttackRateRollAttribute + "." +
                    CombatGearArchShadowSpellId.ToString(CultureInfo.InvariantCulture)] = "10",
            });
        var defenderSession = CreateSession(PrimaryPlayerUid, "Defender", PrimaryPlayerX, PrimaryPlayerY);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(defenderConnection, defenderSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(defenderSession, attackType)).ToFrameBytes()));

        var spark = Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.SpellId == KineticSparkSpellId);
        Assert.Contains(spark.Targets, target => target.Uid == PrimaryPlayerUid);

        Assert.True(sessions.TryGet(defenderConnection, out var updatedDefenderSession));
        Assert.NotNull(updatedDefenderSession);
        Assert.False(HasLegacyStatusFlag(updatedDefenderSession.Character.Attributes, CombatGearArchShadowFlag));
        Assert.DoesNotContain(
            ParseMsgUpdates(defenderSent),
            packet => packet.Uid == PrimaryPlayerUid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, CombatGearArchShadowFlag)));

    }

    [Fact]
    public async Task HandlerPlayerAttackWithActiveKineticSparkChargesAttackerStoneCrackerArrowBladesAndStarFlow()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var playerSpells = new PlayerSpellSnapshot(
            [
                CreatePlayerSpell(KineticSparkSpellId),
                CreatePlayerSpell(CombatGearStoneCrackerStarFlowSpellId),
                CreatePlayerSpell(CombatGearArchShadowSpellId),
            ]);
        var worldData = CreateWorldData(
            [CreateBowItemType()],
            [
                CreateKineticSparkMagicRow(),
                CreateStoneCrackerStarFlowMagicRow(),
                CreateCombatGearArchShadowMagicRow(),
            ],
            [CreateCombatGearDefinition(CombatGearStoneCrackerItemId, CombatGearStoneCrackerStarFlowSpellId)]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var defenderSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var defenderConnection = CreateSendingConnection(defenderSent);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            classCode: 4_001,
            playerItems: CreateEquippedBow(),
            playerSpells: playerSpells,
            combatGear: CreateEquippedCombatGear(CombatGearStoneCrackerItemId),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
                [RuntimeLegacyCombatGearAttackRateRollAttribute + "." +
                    CombatGearArchShadowSpellId.ToString(CultureInfo.InvariantCulture)] = "10",
                [RuntimeLegacyStoneCrackerArrowBladesAttribute] = "2",
                [RuntimeLegacyStoneCrackerStarFlowAttribute] = "0",
            });
        var defenderSession = CreateSession(PrimaryPlayerUid, "Defender", PrimaryPlayerX, PrimaryPlayerY);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(defenderConnection, defenderSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(defenderSession, attackType)).ToFrameBytes()));

        var spark = Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.SpellId == KineticSparkSpellId);
        Assert.Contains(spark.Targets, target => target.Uid == PrimaryPlayerUid);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal("3", updatedCasterSession.Character.Attributes[RuntimeLegacyStoneCrackerArrowBladesAttribute]);
        Assert.Equal("1", updatedCasterSession.Character.Attributes[RuntimeLegacyStoneCrackerStarFlowAttribute]);
        Assert.True(HasLegacyStatusFlag(
            updatedCasterSession.Character.Attributes,
            CombatGearStoneCrackerWaterTop4WeeklyFlag));
        Assert.True(HasLegacyStatusFlag(
            updatedCasterSession.Character.Attributes,
            CombatGearStoneCrackerSageSpeedModeTop4WeeklyFlag));

        AssertStoneCrackerWaterAppendIconUpdate(casterSent, PlayerUid);
        Assert.DoesNotContain(
            ParseMsgUpdates(defenderSent),
            packet => packet.Uid == PlayerUid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.AppendIcon &&
                    entry.Flag == CombatGearStoneCrackerWaterTop4WeeklyFlag));
        AssertCombatGearStatusFlagUpdate(defenderSent, PlayerUid, CombatGearStoneCrackerWaterTop4WeeklyFlag);
        AssertStoneCrackerStarFlowChargeUpdate(casterSent, PlayerUid, expectedStarFlow: 1);
        Assert.DoesNotContain(
            ParseMsgUpdates(defenderSent),
            packet => packet.Uid == PlayerUid &&
                packet.Updates.Any(entry =>
                    entry.Type == CombatGearStoneCrackerStarFlowUpdateType &&
                    entry.Flag == CombatGearStoneCrackerSageSpeedModeTop4WeeklyFlag));
        AssertCombatGearStatusFlagUpdate(defenderSent, PlayerUid, CombatGearStoneCrackerSageSpeedModeTop4WeeklyFlag);
    }

    [Fact]
    public async Task HandlerPlayerAttackWithActiveKineticSparkThirdAttackerStoneCrackerStarFlowFiresSpellAndResets()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var playerSpells = new PlayerSpellSnapshot(
            [
                CreatePlayerSpell(KineticSparkSpellId),
                CreatePlayerSpell(CombatGearStoneCrackerStarFlowSpellId),
            ]);
        var worldData = CreateWorldData(
            [CreateBowItemType()],
            [
                CreateKineticSparkMagicRow(),
                CreateStoneCrackerStarFlowMagicRow(),
            ],
            [CreateCombatGearDefinition(CombatGearStoneCrackerItemId, CombatGearStoneCrackerStarFlowSpellId)]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var defenderSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var defenderConnection = CreateSendingConnection(defenderSent);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            classCode: 4_001,
            playerItems: CreateEquippedBow(),
            playerSpells: playerSpells,
            combatGear: CreateEquippedCombatGear(CombatGearStoneCrackerItemId),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(
                    KineticSparkStatusFlag,
                    CombatGearStoneCrackerSageSpeedModeTop4WeeklyFlag),
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
                [RuntimeLegacyStoneCrackerArrowBladesAttribute] = "0",
                [RuntimeLegacyStoneCrackerStarFlowAttribute] = "2",
            });
        var defenderSession = CreateSession(PrimaryPlayerUid, "Defender", PrimaryPlayerX, PrimaryPlayerY);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(defenderConnection, defenderSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(defenderSession, attackType)).ToFrameBytes()));

        var casterAnimations = ParseSpellAnimations(casterSent);
        var spark = Assert.Single(casterAnimations, animation => animation.SpellId == KineticSparkSpellId);
        var sparkTarget = Assert.Single(spark.Targets, target => target.Uid == PrimaryPlayerUid);
        var starFlow = Assert.Single(
            casterAnimations,
            animation => animation.SpellId == CombatGearStoneCrackerStarFlowSpellId);
        Assert.Equal(PlayerUid, starFlow.AttackerUid);
        Assert.Equal(PlayerX, starFlow.X);
        Assert.Equal(PlayerY, starFlow.Y);
        var starFlowTarget = Assert.Single(starFlow.Targets, target => target.Uid == PrimaryPlayerUid);
        Assert.True(starFlowTarget.Hit);
        Assert.True(starFlowTarget.Damage > 0);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal("0", updatedCasterSession.Character.Attributes[RuntimeLegacyStoneCrackerStarFlowAttribute]);
        Assert.False(HasLegacyStatusFlag(
            updatedCasterSession.Character.Attributes,
            CombatGearStoneCrackerSageSpeedModeTop4WeeklyFlag));
        Assert.True(HasLegacyStatusFlag(
            updatedCasterSession.Character.Attributes,
            CombatGearStoneCrackerLightningTop4WeeklyFlag));

        Assert.True(sessions.TryGet(defenderConnection, out var updatedDefenderSession));
        Assert.NotNull(updatedDefenderSession);
        Assert.True(
            updatedDefenderSession.Character.MinHitPoints <
            defenderSession.Character.MinHitPoints - (long)sparkTarget.Damage);
    }

    [Fact]
    public async Task HandlerPlayerAttackWithActiveKineticSparkStarFlowOnlyPlayerHitRecursivelyAdvancesAttackerStoneCrackerCounters()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        const ushort primaryX = PlayerX + 8;
        const ushort primaryY = PlayerY;
        const ushort starFlowVictimX = PlayerX + 1;
        const ushort starFlowVictimY = PlayerY;
        var playerSpells = new PlayerSpellSnapshot(
            [
                CreatePlayerSpell(KineticSparkSpellId),
                CreatePlayerSpell(CombatGearStoneCrackerStarFlowSpellId),
            ]);
        var worldData = CreateWorldData(
            [CreateBowItemType()],
            [
                CreateKineticSparkMagicRow(),
                CreateStoneCrackerStarFlowMagicRow(),
            ],
            [CreateCombatGearDefinition(CombatGearStoneCrackerItemId, CombatGearStoneCrackerStarFlowSpellId)]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var primarySent = new List<byte[]>();
        var starFlowVictimSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var primaryConnection = CreateSendingConnection(primarySent);
        var starFlowVictimConnection = CreateSendingConnection(starFlowVictimSent);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            classCode: 4_001,
            playerItems: CreateEquippedBow(),
            playerSpells: playerSpells,
            combatGear: CreateEquippedCombatGear(CombatGearStoneCrackerItemId),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(
                    KineticSparkStatusFlag,
                    CombatGearStoneCrackerSageSpeedModeTop4WeeklyFlag),
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
                [RuntimeLegacyStoneCrackerArrowBladesAttribute] = "0",
                [RuntimeLegacyStoneCrackerStarFlowAttribute] = "2",
            });
        var primarySession = CreateSession(PrimaryPlayerUid, "Primary", primaryX, primaryY);
        var starFlowVictimSession = CreateSession(
            NearbyPlayerUid,
            "StarFlowVictim",
            starFlowVictimX,
            starFlowVictimY);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(primaryConnection, primarySession);
        sessions.Set(starFlowVictimConnection, starFlowVictimSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(primarySession, attackType)).ToFrameBytes()));

        var casterAnimations = ParseSpellAnimations(casterSent);
        var spark = Assert.Single(casterAnimations, animation => animation.SpellId == KineticSparkSpellId);
        Assert.Contains(spark.Targets, target => target.Uid == PrimaryPlayerUid);
        Assert.DoesNotContain(spark.Targets, target => target.Uid == NearbyPlayerUid);
        var starFlow = Assert.Single(
            casterAnimations,
            animation => animation.SpellId == CombatGearStoneCrackerStarFlowSpellId);
        Assert.Equal(PlayerUid, starFlow.AttackerUid);
        Assert.Equal(PlayerX, starFlow.X);
        Assert.Equal(PlayerY, starFlow.Y);
        var starFlowTarget = Assert.Single(starFlow.Targets, target => target.Uid == NearbyPlayerUid);
        Assert.True(starFlowTarget.Hit);
        Assert.True(starFlowTarget.Damage > 0);
        Assert.DoesNotContain(starFlow.Targets, target => target.Uid == PrimaryPlayerUid);

        Assert.True(sessions.TryGet(starFlowVictimConnection, out var updatedVictimSession));
        Assert.NotNull(updatedVictimSession);
        Assert.True(updatedVictimSession.Character.MinHitPoints < starFlowVictimSession.Character.MinHitPoints);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal("2", updatedCasterSession.Character.Attributes[RuntimeLegacyStoneCrackerArrowBladesAttribute]);
        Assert.Equal("0", updatedCasterSession.Character.Attributes[RuntimeLegacyStoneCrackerStarFlowAttribute]);
        Assert.True(HasLegacyStatusFlag(
            updatedCasterSession.Character.Attributes,
            CombatGearStoneCrackerLightningTop4WeeklyFlag));
        Assert.False(HasLegacyStatusFlag(
            updatedCasterSession.Character.Attributes,
            CombatGearStoneCrackerSageSpeedModeTop4WeeklyFlag));

        var output = log.ToString();
        Assert.Equal(2, output.Split(
            "WorldServer applied legacy CombatGear Update player StoneCracker counters",
            StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public async Task HandlerPlayerAttackWithActiveKineticSparkStarFlowOnlyPlayerHitTriggersDefenderUpdateTargetExactlyTwiceBeforeHitpoints()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        const ushort primaryX = PlayerX + 8;
        const ushort primaryY = PlayerY;
        const ushort starFlowVictimX = PlayerX + 1;
        const ushort starFlowVictimY = PlayerY;
        var playerSpells = new PlayerSpellSnapshot(
            [
                CreatePlayerSpell(KineticSparkSpellId),
                CreatePlayerSpell(CombatGearStoneCrackerStarFlowSpellId),
            ]);
        var worldData = CreateWorldData(
            [CreateBowItemType()],
            [
                CreateKineticSparkMagicRow(),
                CreateStoneCrackerStarFlowMagicRow(),
                CreateCombatGearRedcursePassiveMagicRow(),
            ],
            [
                CreateCombatGearDefinition(CombatGearStoneCrackerItemId, CombatGearStoneCrackerStarFlowSpellId),
                CreateRedcurseCombatGearDefinition(),
            ]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var primarySent = new List<byte[]>();
        var starFlowVictimSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var primaryConnection = CreateSendingConnection(primarySent);
        var starFlowVictimConnection = CreateSendingConnection(starFlowVictimSent);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            classCode: 4_001,
            playerItems: CreateEquippedBow(),
            playerSpells: playerSpells,
            combatGear: CreateEquippedCombatGear(CombatGearStoneCrackerItemId),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(
                    KineticSparkStatusFlag,
                    CombatGearStoneCrackerSageSpeedModeTop4WeeklyFlag),
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
                [RuntimeLegacyStoneCrackerArrowBladesAttribute] = "0",
                [RuntimeLegacyStoneCrackerStarFlowAttribute] = "2",
            });
        var primarySession = CreateSession(PrimaryPlayerUid, "Primary", primaryX, primaryY);
        var starFlowVictimSession = CreateSession(
            NearbyPlayerUid,
            "StarFlowVictim",
            starFlowVictimX,
            starFlowVictimY,
            classCode: 2_005,
            playerSpells: new PlayerSpellSnapshot([CreatePlayerSpell(CombatGearRedcursePassiveSpellId)]),
            combatGear: CreateEquippedCombatGear(CombatGearRedcurseItemId),
            attributes: CreateForcedCombatGearPassiveAttributes());
        sessions.Set(casterConnection, casterSession);
        sessions.Set(primaryConnection, primarySession);
        sessions.Set(starFlowVictimConnection, starFlowVictimSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(primarySession, attackType)).ToFrameBytes()));

        var casterAnimations = ParseSpellAnimations(casterSent);
        var spark = Assert.Single(casterAnimations, animation => animation.SpellId == KineticSparkSpellId);
        Assert.Contains(spark.Targets, target => target.Uid == PrimaryPlayerUid);
        Assert.DoesNotContain(spark.Targets, target => target.Uid == NearbyPlayerUid);
        var starFlow = Assert.Single(
            casterAnimations,
            animation => animation.SpellId == CombatGearStoneCrackerStarFlowSpellId);
        Assert.Contains(starFlow.Targets, target => target.Uid == NearbyPlayerUid);
        Assert.DoesNotContain(starFlow.Targets, target => target.Uid == PrimaryPlayerUid);

        Assert.True(sessions.TryGet(starFlowVictimConnection, out var updatedVictimSession));
        Assert.NotNull(updatedVictimSession);
        Assert.True(HasLegacyStatusFlag(
            updatedVictimSession.Character.Attributes,
            CombatGearLightningMagicShieldTop4WeeklyFlag));
        AssertCombatGearPassiveStatusUpdates(starFlowVictimSent, NearbyPlayerUid, expectedCount: 2);
        AssertCombatGearPassiveArchiveSkills(starFlowVictimSent, NearbyPlayerUid, expectedCount: 2);

        var combatGearAnimations = ParseSpellAnimations(starFlowVictimSent)
            .Where(animation => animation.SpellId == CombatGearRedcursePassiveSpellId).ToArray();
        Assert.Equal(2, combatGearAnimations.Length);
        Assert.All(combatGearAnimations, animation =>
        {
            Assert.Equal(NearbyPlayerUid, animation.AttackerUid);
            Assert.Contains(animation.Targets, target => target.Uid == NearbyPlayerUid);
        });

        var openingKinds = new List<string>();
        var openingIndices = new List<int>();
        for (var index = 0; index < starFlowVictimSent.Count; index++)
        {
            var frame = ReadFrame(starFlowVictimSent[index]);
            string? kind = null;
            if (LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Uid == NearbyPlayerUid)
            {
                if (update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is { } values && IsLegacyStatusFlagSet(values, CombatGearLightningMagicShieldTop4WeeklyFlag)))
                    kind = "StatusFlag";
                else if (update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == CombatGearLightningMagicShieldTop4WeeklyFlag && entry.Time == 2))
                    kind = "ArchiveSkill";
            }
            else if (LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == CombatGearRedcursePassiveSpellId && animation.AttackerUid == NearbyPlayerUid)
                kind = "Animation";
            if (kind is not null)
            {
                openingKinds.Add(kind);
                openingIndices.Add(index);
            }
        }
        Assert.Equal(new[] { "StatusFlag", "ArchiveSkill", "Animation", "StatusFlag", "ArchiveSkill", "Animation" },
            openingKinds);
        var firstHitpointIndex = starFlowVictimSent.FindIndex(bytes =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) && update.Uid == NearbyPlayerUid &&
            update.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints));
        Assert.True(firstHitpointIndex > openingIndices[^1]);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy CombatGear UpdateTarget player defender passives", output);
        Assert.Contains("attempts=2 applied=3; source=ReceiveAttack.Player.Execute opening before receive modifiers/HP.", output);
    }

    [Fact]
    public async Task HandlerPlayerAttackWithActiveKineticSparkStarFlowUsesReceiveAdjustedPlayerDamage()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        const ushort primaryX = PlayerX + 8;
        const ushort primaryY = PlayerY;
        const ushort starFlowVictimX = PlayerX + 1;
        const ushort starFlowVictimY = PlayerY;
        var playerSpells = new PlayerSpellSnapshot(
            [
                CreatePlayerSpell(KineticSparkSpellId),
                CreatePlayerSpell(CombatGearStoneCrackerStarFlowSpellId),
            ]);
        var worldData = CreateWorldData(
            [CreateBowItemType()],
            [
                CreateKineticSparkMagicRow(),
                CreateStoneCrackerStarFlowMagicRow(),
            ],
            [CreateCombatGearDefinition(CombatGearStoneCrackerItemId, CombatGearStoneCrackerStarFlowSpellId)]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var primarySent = new List<byte[]>();
        var starFlowVictimSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var primaryConnection = CreateSendingConnection(primarySent);
        var starFlowVictimConnection = CreateSendingConnection(starFlowVictimSent);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            classCode: 4_001,
            playerItems: CreateEquippedBow(),
            playerSpells: playerSpells,
            combatGear: CreateEquippedCombatGear(CombatGearStoneCrackerItemId),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(
                    KineticSparkStatusFlag,
                    CombatGearStoneCrackerSageSpeedModeTop4WeeklyFlag),
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
                [RuntimeLegacyStoneCrackerArrowBladesAttribute] = "0",
                [RuntimeLegacyStoneCrackerStarFlowAttribute] = "2",
                ["HawksAmbitionPower"] = "50",
            });
        var primarySession = CreateSession(PrimaryPlayerUid, "Primary", primaryX, primaryY);
        var starFlowVictimSession = CreateSession(
            NearbyPlayerUid,
            "StarFlowVictim",
            starFlowVictimX,
            starFlowVictimY,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyHawksAmbitionFlag),
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(primaryConnection, primarySession);
        sessions.Set(starFlowVictimConnection, starFlowVictimSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(primarySession, attackType)).ToFrameBytes()));

        var casterAnimations = ParseSpellAnimations(casterSent);
        var spark = Assert.Single(casterAnimations, animation => animation.SpellId == KineticSparkSpellId);
        Assert.Contains(spark.Targets, target => target.Uid == PrimaryPlayerUid);
        Assert.DoesNotContain(spark.Targets, target => target.Uid == NearbyPlayerUid);
        var starFlow = Assert.Single(
            casterAnimations,
            animation => animation.SpellId == CombatGearStoneCrackerStarFlowSpellId);
        Assert.Equal(PlayerUid, starFlow.AttackerUid);
        Assert.Equal(PlayerX, starFlow.X);
        Assert.Equal(PlayerY, starFlow.Y);
        var starFlowTarget = Assert.Single(starFlow.Targets, target => target.Uid == NearbyPlayerUid);
        Assert.True(starFlowTarget.Hit);
        Assert.True(starFlowTarget.Damage > 0);
        Assert.DoesNotContain(starFlow.Targets, target => target.Uid == PrimaryPlayerUid);

        Assert.True(sessions.TryGet(starFlowVictimConnection, out var updatedVictimSession));
        Assert.NotNull(updatedVictimSession);
        var victimDamageApplied = starFlowVictimSession.Character.MinHitPoints -
            updatedVictimSession.Character.MinHitPoints;
        Assert.Equal(CheckedUInt32(victimDamageApplied), starFlowTarget.Damage);
        Assert.False(HasLegacyStatusFlag(updatedVictimSession.Character.Attributes, LegacyHawksAmbitionFlag));

        var output = log.ToString();
        Assert.Contains("HawksAmbition player-hit reduction", output);
        Assert.Contains("StarFlow", output);
    }

    [Fact]
    public async Task HandlerPlayerAttackWithActiveKineticSparkTriggersAttackerDragonhowlReceiveUpdate()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        const uint duration = 7;
        const uint critPower = 850;
        const uint tortoisePower = 45;
        const uint breakPower = 60;
        const uint staminaPower = 15;
        var playerSpells = new PlayerSpellSnapshot(
            [
                CreatePlayerSpell(KineticSparkSpellId),
                CreatePlayerSpell(CombatGearDragonhowlSpellId),
            ]);
        var worldData = CreateWorldData(
            [CreateBowItemType()],
            [
                CreateKineticSparkMagicRow(),
                CreateCombatGearDragonhowlMagicRow(
                    duration,
                    critPower,
                    tortoisePower,
                    breakPower,
                    staminaPower),
            ],
            [CreateCombatGearDefinition(CombatGearDragonhowlItemId, CombatGearDragonhowlSpellId)]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var defenderSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var defenderConnection = CreateSendingConnection(defenderSent);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            classCode: 2_005,
            playerItems: CreateEquippedBow(),
            playerSpells: playerSpells,
            combatGear: CreateEquippedCombatGear(CombatGearDragonhowlItemId),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
                [RuntimeLegacyCombatGearAttackRateRollAttribute + "." +
                    CombatGearDragonhowlSpellId.ToString(CultureInfo.InvariantCulture)] = "0",
                [RuntimeLegacyCombatGearDragonhowlTortoiseRollAttribute] = "0",
                [RuntimeLegacyCombatGearDragonhowlBreakRollAttribute] = "0",
            });
        var defenderSession = CreateSession(PrimaryPlayerUid, "Defender", PrimaryPlayerX, PrimaryPlayerY);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(defenderConnection, defenderSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(defenderSession, attackType)).ToFrameBytes()));

        var spark = Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.SpellId == KineticSparkSpellId);
        Assert.Contains(spark.Targets, target => target.Uid == PrimaryPlayerUid);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, CombatGearDragonPierceCritFlag));
        Assert.True(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, CombatGearDragonPierceTortoiseFlag));
        Assert.True(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, CombatGearDragonPierceBreakFlag));
        Assert.Equal(
            tortoisePower.ToString(CultureInfo.InvariantCulture),
            updatedCasterSession.Character.Attributes[RuntimeLegacyDragonPierceTortoiseAttribute]);
        Assert.Equal(
            breakPower.ToString(CultureInfo.InvariantCulture),
            updatedCasterSession.Character.Attributes[RuntimeLegacyDragonPierceBreakAttribute]);
        Assert.Equal(
            staminaPower.ToString(CultureInfo.InvariantCulture),
            updatedCasterSession.Character.Attributes[RuntimeLegacyDragonPierceStaminaAttribute]);

        AssertCombatGearStatusFlagUpdate(casterSent, PlayerUid, CombatGearDragonPierceCritFlag);
        AssertCombatGearStatusFlagUpdate(casterSent, PlayerUid, CombatGearDragonPierceTortoiseFlag);
        AssertCombatGearStatusFlagUpdate(casterSent, PlayerUid, CombatGearDragonPierceBreakFlag);
        AssertCombatGearArchiveSkillUpdate(
            casterSent,
            PlayerUid,
            CombatGearDragonPierceCritFlag,
            duration: 10);
        AssertCombatGearArchiveSkillUpdate(
            casterSent,
            PlayerUid,
            CombatGearDragonPierceTortoiseFlag,
            duration: 10);
        AssertCombatGearArchiveSkillUpdate(
            casterSent,
            PlayerUid,
            CombatGearDragonPierceBreakFlag,
            duration: 10);
        AssertCombatGearSelfAnimation(casterSent, PlayerUid, CombatGearDragonhowlSpellId);

        var output = log.ToString();
        Assert.Contains("Dragonhowl", output);
        Assert.Contains("spellId=" + CombatGearDragonhowlSpellId.ToString(CultureInfo.InvariantCulture), output);
    }

    [Fact]
    public async Task HandlerPlayerAttackWithActiveKineticSparkTriggersAttackerBloodlustReceiveUpdate()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        const uint immersionDuration = 6;
        const uint immersionPower = 320;
        const uint insouciancePower = 410;
        var playerSpells = new PlayerSpellSnapshot(
            [
                CreatePlayerSpell(KineticSparkSpellId),
                CreatePlayerSpell(CombatGearBloodlustImmersionSpellId),
                CreatePlayerSpell(CombatGearBloodlustInsoucianceSpellId),
            ]);
        var worldData = CreateWorldData(
            [CreateBowItemType()],
            [
                CreateKineticSparkMagicRow(),
                CreateCombatGearBloodlustImmersionMagicRow(immersionDuration, immersionPower),
                CreateCombatGearBloodlustInsoucianceMagicRow(4, insouciancePower),
            ],
            [
                CreateCombatGearDefinition(
                    CombatGearBloodlustItemId,
                    CombatGearBloodlustImmersionSpellId,
                    CombatGearBloodlustInsoucianceSpellId),
            ]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var defenderSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var defenderConnection = CreateSendingConnection(defenderSent);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            classCode: 2_005,
            playerItems: CreateEquippedBow(),
            playerSpells: playerSpells,
            combatGear: CreateEquippedCombatGear(CombatGearBloodlustItemId),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
                [RuntimeLegacyCombatGearAttackRateRollAttribute + "." +
                    CombatGearBloodlustImmersionSpellId.ToString(CultureInfo.InvariantCulture)] = "0",
                [RuntimeLegacyCombatGearAttackRateRollAttribute + "." +
                    CombatGearBloodlustInsoucianceSpellId.ToString(CultureInfo.InvariantCulture)] = "0",
            });
        var defenderSession = CreateSession(PrimaryPlayerUid, "Defender", PrimaryPlayerX, PrimaryPlayerY);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(defenderConnection, defenderSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(defenderSession, attackType)).ToFrameBytes()));

        var spark = Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.SpellId == KineticSparkSpellId);
        Assert.Contains(spark.Targets, target => target.Uid == PrimaryPlayerUid);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, CombatGearImmersionFlag));
        Assert.True(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, CombatGearInsoucianceFlag));
        Assert.Equal(
            immersionPower.ToString(CultureInfo.InvariantCulture),
            updatedCasterSession.Character.Attributes[RuntimeLegacyImmersionPowerAttribute]);
        Assert.Equal(
            insouciancePower.ToString(CultureInfo.InvariantCulture),
            updatedCasterSession.Character.Attributes[RuntimeLegacyInsouciancePowerAttribute]);

        AssertCombatGearStatusFlagUpdate(casterSent, PlayerUid, CombatGearImmersionFlag);
        AssertCombatGearStatusFlagUpdate(casterSent, PlayerUid, CombatGearInsoucianceFlag);
        AssertCombatGearArchiveSkillUpdate(
            casterSent,
            PlayerUid,
            CombatGearImmersionFlag,
            immersionDuration);
        AssertCombatGearArchiveSkillUpdate(
            casterSent,
            PlayerUid,
            CombatGearInsoucianceFlag,
            duration: 10);
        AssertCombatGearSelfAnimation(casterSent, PlayerUid, CombatGearBloodlustImmersionSpellId);
        AssertCombatGearSelfAnimation(casterSent, PlayerUid, CombatGearBloodlustInsoucianceSpellId);

        var output = log.ToString();
        Assert.Contains("Bloodlust", output);
        Assert.Contains("spellId=" + CombatGearBloodlustImmersionSpellId.ToString(CultureInfo.InvariantCulture), output);
        Assert.Contains("spellId=" + CombatGearBloodlustInsoucianceSpellId.ToString(CultureInfo.InvariantCulture), output);
    }

    [Fact]
    public async Task HandlerPlayerAttackWithActiveKineticSparkTriggersAttackerPirateArmedAndSenseReceiveUpdate()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        const uint duration = 10;
        const uint bowItemUid = 50_007;
        var equippedBow = CreateEquippedBow(bowItemUid);
        var playerSpells = new PlayerSpellSnapshot(
            [
                CreatePlayerSpell(KineticSparkSpellId),
                CreatePlayerSpell(CombatGearPirateArmedSpellId),
                CreatePlayerSpell(CombatGearPirateSenseSpellId),
            ]);
        var worldData = CreateWorldData(
            [CreateBowItemType()],
            [
                CreateKineticSparkMagicRow(),
                CreateCombatGearPirateArmedMagicRow(),
                CreateCombatGearPirateSenseMagicRow(),
            ],
            [
                CreateCombatGearDefinition(CombatGearPirate1ItemId, CombatGearPirateArmedSpellId),
                CreateCombatGearDefinition(CombatGearPirate2ItemId, CombatGearPirateSenseSpellId),
            ]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var defenderSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var defenderConnection = CreateSendingConnection(defenderSent);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            classCode: 7_005,
            playerItems: equippedBow,
            playerSpells: playerSpells,
            combatGear: CreateEquippedCombatGear(CombatGearPirate1ItemId, CombatGearPirate2ItemId),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
                [RuntimeLegacyCombatGearAttackRateRollAttribute + "." +
                    CombatGearPirateArmedSpellId.ToString(CultureInfo.InvariantCulture)] = "0",
                [RuntimeLegacyCombatGearAttackRateRollAttribute + "." +
                    CombatGearPirateSenseSpellId.ToString(CultureInfo.InvariantCulture)] = "0",
            });
        var defenderSession = CreateSession(PrimaryPlayerUid, "Defender", PrimaryPlayerX, PrimaryPlayerY);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(defenderConnection, defenderSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(defenderSession, attackType)).ToFrameBytes()));

        var spark = Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.SpellId == KineticSparkSpellId);
        Assert.Contains(spark.Targets, target => target.Uid == PrimaryPlayerUid);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, CombatGearPirateArmedFlag));
        Assert.True(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, CombatGearPirateSenseFlag));

        AssertCombatGearStatusFlagUpdate(casterSent, PlayerUid, CombatGearPirateArmedFlag);
        AssertCombatGearStatusFlagUpdate(casterSent, PlayerUid, CombatGearPirateSenseFlag);
        AssertCombatGearArchiveSkillUpdate(
            casterSent,
            PlayerUid,
            CombatGearPirateArmedFlag,
            duration);
        AssertCombatGearArchiveSkillUpdate(
            casterSent,
            PlayerUid,
            CombatGearPirateSenseFlag,
            duration);
        AssertCombatGearSelfAnimation(casterSent, PlayerUid, CombatGearPirateArmedSpellId);
        AssertCombatGearSelfAnimation(casterSent, PlayerUid, CombatGearPirateSenseSpellId);

        var equipmentRefreshes = ParseShowEquipmentRefreshes(casterSent);
        Assert.Equal(2, equipmentRefreshes.Count);
        Assert.All(
            equipmentRefreshes,
            packet =>
            {
                Assert.Equal(
                    LegacyShowEquipmentPacket.CreateEquipmentRefresh(equippedBow, alternateEquipmentActive: false),
                    packet);
                Assert.Equal(bowItemUid, packet.EquipmentSlots.RightWeapon);
            });

        var equipmentRefreshIndexes = FrameIndexes(
            casterSent,
            frame => LegacyShowEquipmentPacket.TryParse(frame, out _));
        var armedAnimationIndex = SingleFrameIndex(
            casterSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == CombatGearPirateArmedSpellId);
        var senseAnimationIndex = SingleFrameIndex(
            casterSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == CombatGearPirateSenseSpellId);
        Assert.Equal(2, equipmentRefreshIndexes.Count);
        Assert.True(armedAnimationIndex < equipmentRefreshIndexes[0]);
        Assert.True(equipmentRefreshIndexes[0] < senseAnimationIndex);
        Assert.True(senseAnimationIndex < equipmentRefreshIndexes[1]);
    }

    [Fact]
    public async Task HandlerPlayerAttackWithActiveKineticSparkTriggersAttackerPirateBlackJadeSailingReceiveUpdate()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        const uint blackJadeId = 21_810_907;
        const uint bowItemUid = 50_004;
        const uint duration = 10;
        const uint blockDamage = 700;
        var equippedBow = CreateEquippedBow(bowItemUid);
        var playerSpells = new PlayerSpellSnapshot([CreatePlayerSpell(KineticSparkSpellId)]);
        var worldData = CreateWorldData(
            [CreateBowItemType()],
            [
                CreateKineticSparkMagicRow(),
                CreateCombatGearPirateSailingMagicRow(blockDamage),
            ],
            daoqiDictTypes:
            [
                CreateDaoqiBlackJadeSpellEntry(
                    checked((int)blackJadeId),
                    checked(CombatGearPirateSailingSpellId * 1000)),
            ]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var defenderSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var defenderConnection = CreateSendingConnection(defenderSent);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            classCode: 7_005,
            playerItems: equippedBow,
            playerSpells: playerSpells,
            combatGear: CreateEquippedCombatGearWithBlackJades(CombatGearPirate1ItemId, blackJadeId),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
                [RuntimeLegacyCombatGearAttackSpellIndexAttribute] = "0",
                [RuntimeLegacyCombatGearAttackRateRollAttribute + "." +
                    CombatGearPirateSailingSpellId.ToString(CultureInfo.InvariantCulture)] = "0",
            });
        var defenderSession = CreateSession(PrimaryPlayerUid, "Defender", PrimaryPlayerX, PrimaryPlayerY);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(defenderConnection, defenderSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(defenderSession, attackType)).ToFrameBytes()));

        var spark = Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.SpellId == KineticSparkSpellId);
        Assert.Contains(spark.Targets, target => target.Uid == PrimaryPlayerUid);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(
            HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, CombatGearPirateSailingFlag),
            log.ToString());

        AssertCombatGearStatusFlagUpdate(casterSent, PlayerUid, CombatGearPirateSailingFlag);
        AssertCombatGearArchiveSkillUpdate(
            casterSent,
            PlayerUid,
            CombatGearPirateSailingFlag,
            duration);
        AssertCombatGearSelfAnimation(casterSent, PlayerUid, CombatGearPirateSailingSpellId);

        var equipmentRefresh = Assert.Single(ParseShowEquipmentRefreshes(casterSent));
        Assert.Equal(
            LegacyShowEquipmentPacket.CreateEquipmentRefresh(equippedBow, alternateEquipmentActive: false),
            equipmentRefresh);
        Assert.Equal(LegacyShowEquipmentPacket.ShowAction, equipmentRefresh.Action);
        Assert.Equal(0U, equipmentRefresh.WParam);
        Assert.Equal(0U, equipmentRefresh.Alternante);
        Assert.Equal(bowItemUid, equipmentRefresh.EquipmentSlots.RightWeapon);
        Assert.Equal(0U, equipmentRefresh.EquipmentSlots.LeftWeapon);

        var sailingStatusIndex = SingleFrameIndex(
            casterSent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var packet) &&
                packet.Uid == PlayerUid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, CombatGearPirateSailingFlag)));
        var sailingArchiveIndex = SingleFrameIndex(
            casterSent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var packet) &&
                packet.Uid == PlayerUid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == CombatGearPirateSailingFlag &&
                    entry.Time == duration));
        var sailingAnimationIndex = SingleFrameIndex(
            casterSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == CombatGearPirateSailingSpellId);
        var equipmentRefreshIndex = SingleFrameIndex(
            casterSent,
            frame => LegacyShowEquipmentPacket.TryParse(frame, out _));
        Assert.True(
            sailingStatusIndex < sailingArchiveIndex,
            $"Sailing status should be sent before archive skill. status={sailingStatusIndex} " +
            $"archive={sailingArchiveIndex}");
        Assert.True(
            sailingArchiveIndex < sailingAnimationIndex,
            $"Sailing archive skill should be sent before animation. archive={sailingArchiveIndex} " +
            $"animation={sailingAnimationIndex}");
        Assert.True(
            sailingAnimationIndex < equipmentRefreshIndex,
            $"Sailing equipment refresh should be sent after the status/archive/animation path. " +
            $"status={sailingStatusIndex} archive={sailingArchiveIndex} animation={sailingAnimationIndex} " +
            $"equipment={equipmentRefreshIndex}");

        var output = log.ToString();
        Assert.Contains("spellId=" + CombatGearPirateSailingSpellId.ToString(CultureInfo.InvariantCulture), output);
        Assert.DoesNotContain("black-jade random UseRune branch", output);
    }

    [Fact]
    public async Task HandlerPlayerAttackWithActiveKineticSparkSobNpcHitTriggersAttackerPirateBlackJadeSailingReceiveUpdate()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        const uint blackJadeId = 21_810_908;
        const uint bowItemUid = 50_009;
        const uint duration = 10;
        const uint blockDamage = 700;
        var primary = CreateSobNpc(id: 65_606, name: "BlackJadeSailingPole", x: PlayerX + 2, y: PlayerY);
        var equippedBow = CreateEquippedBow(bowItemUid);
        var playerSpells = new PlayerSpellSnapshot([CreatePlayerSpell(KineticSparkSpellId)]);
        var worldData = CreateWorldData(
            [CreateBowItemType()],
            [
                CreateKineticSparkMagicRow(),
                CreateCombatGearPirateSailingMagicRow(blockDamage),
            ],
            daoqiDictTypes:
            [
                CreateDaoqiBlackJadeSpellEntry(
                    checked((int)blackJadeId),
                    checked(CombatGearPirateSailingSpellId * 1000)),
            ],
            sobNpcs: [primary]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var casterSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            classCode: 7_005,
            playerItems: equippedBow,
            playerSpells: playerSpells,
            combatGear: CreateEquippedCombatGearWithBlackJades(CombatGearPirate1ItemId, blackJadeId),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
                [RuntimeLegacyCombatGearAttackSpellIndexAttribute] = "0",
                [RuntimeLegacyCombatGearAttackRateRollAttribute + "." +
                    CombatGearPirateSailingSpellId.ToString(CultureInfo.InvariantCulture)] = "0",
            });
        sessions.Set(casterConnection, casterSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(CreateSobNpcAttack(primary, attackType)).ToFrameBytes()));

        var spark = Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.SpellId == KineticSparkSpellId);
        var sparkTarget = Assert.Single(spark.Targets, target => target.Uid == checked((uint)primary.Id));
        Assert.True(sparkTarget.Hit);
        Assert.True(sobNpcStates.GetVisible(primary).Life < primary.Life);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(
            HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, CombatGearPirateSailingFlag),
            log.ToString());

        AssertCombatGearStatusFlagUpdate(casterSent, PlayerUid, CombatGearPirateSailingFlag);
        AssertCombatGearArchiveSkillUpdate(
            casterSent,
            PlayerUid,
            CombatGearPirateSailingFlag,
            duration);
        AssertCombatGearSelfAnimation(casterSent, PlayerUid, CombatGearPirateSailingSpellId);

        var equipmentRefresh = Assert.Single(ParseShowEquipmentRefreshes(casterSent));
        Assert.Equal(
            LegacyShowEquipmentPacket.CreateEquipmentRefresh(equippedBow, alternateEquipmentActive: false),
            equipmentRefresh);
        Assert.Equal(bowItemUid, equipmentRefresh.EquipmentSlots.RightWeapon);

        var output = log.ToString();
        Assert.Contains("black-jade UseRune", output);
        Assert.Contains("spellId=" + CombatGearPirateSailingSpellId.ToString(CultureInfo.InvariantCulture), output);
    }

    [Theory]
    [InlineData(21_730, 0x1B0, 11, 310, 21_730_901)]
    [InlineData(21_740, 0x1B1, 12, 320, 21_740_901)]
    [InlineData(21_750, 0x1B2, 13, 330, 21_750_901)]
    [InlineData(21_760, 0x1B3, 14, 340, 21_760_901)]
    [InlineData(21_770, 0x1B4, 15, 350, 21_770_901)]
    [InlineData(21_780, 0x1B5, 16, 360, 21_780_901)]
    [InlineData(21_790, 0x1B6, 17, 370, 21_790_901)]
    [InlineData(21_800, 0x1B7, 18, 380, 21_800_901)]
    [InlineData(21_820, 0x1B8, 19, 390, 21_820_901)]
    public async Task HandlerPlayerAttackWithActiveKineticSparkTriggersAttackerPirateBlackJadeAllowedNonzeroUseRuneStatus(
        int spellId,
        int statusFlag,
        int duration,
        int gDamage,
        int blackJadeId)
    {
        const uint bowItemUid = 50_012;
        var checkedSpellId = checked((ushort)spellId);
        var result = await RunPirateBlackJadeUseRuneAsync(
            checkedSpellId,
            checked((uint)blackJadeId),
            CreateCombatGearPirateBlackJadeUseRuneMagicRow(
                checkedSpellId,
                statusFlag,
                checked((uint)duration),
                checked((uint)gDamage)),
            bowItemUid);

        Assert.True(
            HasLegacyStatusFlag(result.UpdatedCasterSession.Character.Attributes, statusFlag),
            result.LogOutput);
        AssertCombatGearStatusFlagUpdate(result.CasterSent, PlayerUid, statusFlag);
        AssertCombatGearArchiveSkillUpdate(
            result.CasterSent,
            PlayerUid,
            statusFlag,
            checked((uint)duration),
            checked((uint)gDamage));
        AssertCombatGearSelfAnimation(
            result.CasterSent,
            PlayerUid,
            checkedSpellId,
            expectedHit: true);

        var equipmentRefresh = Assert.Single(ParseShowEquipmentRefreshes(result.CasterSent));
        Assert.Equal(
            LegacyShowEquipmentPacket.CreateEquipmentRefresh(result.EquippedBow, alternateEquipmentActive: false),
            equipmentRefresh);
        Assert.Equal(bowItemUid, equipmentRefresh.EquipmentSlots.RightWeapon);

        Assert.Contains("black-jade UseRune", result.LogOutput);
        Assert.Contains("spellId=" + spellId.ToString(CultureInfo.InvariantCulture), result.LogOutput);
    }

    [Fact]
    public async Task HandlerPlayerAttackWithActiveKineticSparkTriggersAttackerPirateBlackJadeFusingZeroDurationApply()
    {
        const uint blackJadeId = 21_590_907;
        const uint bowItemUid = 50_005;
        const uint fusingDamage = 500;
        var result = await RunPirateBlackJadeUseRuneAsync(
            CombatGearPirateFusingSpellId,
            blackJadeId,
            CreateCombatGearPirateFusingMagicRow(fusingDamage),
            bowItemUid);

        Assert.True(
            HasLegacyStatusFlag(result.UpdatedCasterSession.Character.Attributes, CombatGearPirateFusingFlag),
            result.LogOutput);
        AssertCombatGearStatusFlagUpdate(result.CasterSent, PlayerUid, CombatGearPirateFusingFlag);
        AssertCombatGearArchiveSkillUpdate(
            result.CasterSent,
            PlayerUid,
            CombatGearPirateFusingFlag,
            duration: 0,
            damage: fusingDamage);
        AssertCombatGearSelfAnimation(
            result.CasterSent,
            PlayerUid,
            CombatGearPirateFusingSpellId,
            expectedHit: false);

        var equipmentRefresh = Assert.Single(ParseShowEquipmentRefreshes(result.CasterSent));
        Assert.Equal(
            LegacyShowEquipmentPacket.CreateEquipmentRefresh(result.EquippedBow, alternateEquipmentActive: false),
            equipmentRefresh);
        Assert.Equal(bowItemUid, equipmentRefresh.EquipmentSlots.RightWeapon);

        var statusIndex = SingleFrameIndex(
            result.CasterSent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var packet) &&
                packet.Uid == PlayerUid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, CombatGearPirateFusingFlag)));
        var archiveIndex = SingleFrameIndex(
            result.CasterSent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var packet) &&
                packet.Uid == PlayerUid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == CombatGearPirateFusingFlag &&
                    entry.Time == 0 &&
                    entry.Damage == fusingDamage));
        var animationIndex = SingleFrameIndex(
            result.CasterSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == CombatGearPirateFusingSpellId);
        var equipmentRefreshIndex = SingleFrameIndex(
            result.CasterSent,
            frame => LegacyShowEquipmentPacket.TryParse(frame, out _));
        Assert.True(statusIndex < archiveIndex);
        Assert.True(archiveIndex < animationIndex);
        Assert.True(animationIndex < equipmentRefreshIndex);
    }

    [Fact]
    public async Task HandlerPlayerAttackWithActiveKineticSparkTriggersAttackerPirateBlackJadeFusingZeroDurationToggleOff()
    {
        const uint blackJadeId = 21_590_917;
        const uint bowItemUid = 50_006;
        var result = await RunPirateBlackJadeUseRuneAsync(
            CombatGearPirateFusingSpellId,
            blackJadeId,
            CreateCombatGearPirateFusingMagicRow(),
            bowItemUid,
            CombatGearPirateFusingFlag);

        Assert.False(
            HasLegacyStatusFlag(result.UpdatedCasterSession.Character.Attributes, CombatGearPirateFusingFlag),
            result.LogOutput);
        Assert.DoesNotContain(
            ParseMsgUpdates(result.CasterSent),
            packet => packet.Uid == PlayerUid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == CombatGearPirateFusingFlag));
        AssertCombatGearSelfAnimation(
            result.CasterSent,
            PlayerUid,
            CombatGearPirateFusingSpellId,
            expectedHit: false);

        var equipmentRefresh = Assert.Single(ParseShowEquipmentRefreshes(result.CasterSent));
        Assert.Equal(
            LegacyShowEquipmentPacket.CreateEquipmentRefresh(result.EquippedBow, alternateEquipmentActive: false),
            equipmentRefresh);
        Assert.Equal(bowItemUid, equipmentRefresh.EquipmentSlots.RightWeapon);

        var statusIndex = SingleFrameIndex(
            result.CasterSent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var packet) &&
                packet.Uid == PlayerUid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, KineticSparkStatusFlag) &&
                    !IsLegacyStatusFlagSet(entry.Values, CombatGearPirateFusingFlag)));
        var animationIndex = SingleFrameIndex(
            result.CasterSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == CombatGearPirateFusingSpellId);
        var equipmentRefreshIndex = SingleFrameIndex(
            result.CasterSent,
            frame => LegacyShowEquipmentPacket.TryParse(frame, out _));
        Assert.True(statusIndex < animationIndex);
        Assert.True(animationIndex < equipmentRefreshIndex);
    }

    [Fact]
    public async Task HandlerPlayerAttackWithActiveKineticSparkTriggersAttackerPirateDarkRemovesDefenderBuffs()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var playerSpells = new PlayerSpellSnapshot(
            [
                CreatePlayerSpell(KineticSparkSpellId),
                CreatePlayerSpell(CombatGearPirateDarkSpellId),
            ]);
        var worldData = CreateWorldData(
            [CreateBowItemType()],
            [
                CreateKineticSparkMagicRow(),
                CreateCombatGearPirateDarkMagicRow(),
            ],
            [CreateCombatGearDefinition(CombatGearPirate3ItemId, CombatGearPirateDarkSpellId)]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var defenderSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var defenderConnection = CreateSendingConnection(defenderSent);
        var defenderEquippedBow = CreateEquippedBow(50_008);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            classCode: 7_005,
            playerItems: CreateEquippedBow(),
            playerSpells: playerSpells,
            combatGear: CreateEquippedCombatGear(CombatGearPirate3ItemId),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
            });
        var defenderSession = CreateSession(
            PrimaryPlayerUid,
            "Defender",
            PrimaryPlayerX,
            PrimaryPlayerY,
            playerItems: defenderEquippedBow,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyStigmaFlag, LegacyMagicShieldFlag),
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(defenderConnection, defenderSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(defenderSession, attackType)).ToFrameBytes()));

        var spark = Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.SpellId == KineticSparkSpellId);
        Assert.Contains(spark.Targets, target => target.Uid == PrimaryPlayerUid);

        Assert.True(sessions.TryGet(defenderConnection, out var updatedDefenderSession));
        Assert.NotNull(updatedDefenderSession);
        Assert.False(HasLegacyStatusFlag(updatedDefenderSession.Character.Attributes, LegacyStigmaFlag));
        Assert.False(HasLegacyStatusFlag(updatedDefenderSession.Character.Attributes, LegacyMagicShieldFlag));

        var darkAnimation = Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.SpellId == CombatGearPirateDarkSpellId);
        Assert.Equal(PlayerUid, darkAnimation.AttackerUid);
        Assert.Equal(0U, darkAnimation.AttackedUid);
        Assert.Equal(PrimaryPlayerX, darkAnimation.X);
        Assert.Equal(PrimaryPlayerY, darkAnimation.Y);

        var darkTarget = Assert.Single(darkAnimation.Targets);
        Assert.Equal(PrimaryPlayerUid, darkTarget.Uid);
        Assert.Equal(0U, darkTarget.Damage);

        var defenderEquipmentRefresh = Assert.Single(ParseShowEquipmentRefreshes(defenderSent));
        Assert.Equal(
            LegacyShowEquipmentPacket.CreateEquipmentRefresh(defenderEquippedBow, alternateEquipmentActive: false),
            defenderEquipmentRefresh);
        var defenderStatusIndex = SingleFrameIndex(
            defenderSent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var packet) &&
                packet.Uid == PrimaryPlayerUid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    !IsLegacyStatusFlagSet(entry.Values, LegacyStigmaFlag) &&
                    !IsLegacyStatusFlagSet(entry.Values, LegacyMagicShieldFlag)));
        var defenderEquipmentIndex = SingleFrameIndex(
            defenderSent,
            frame => LegacyShowEquipmentPacket.TryParse(frame, out _));
        Assert.True(defenderStatusIndex < defenderEquipmentIndex);
    }

    [Theory]
    [InlineData("0", CombatGearCrackMantraPhysicalFlag, RuntimeLegacyCrackMantra1Attribute, RuntimeLegacyCrackMantra2Attribute, 1_100)]
    [InlineData("50", CombatGearCrackMantraMagicFlag, RuntimeLegacyCrackMantra2Attribute, RuntimeLegacyCrackMantra1Attribute, 1_000)]
    public async Task HandlerPlayerAttackWithActiveKineticSparkTriggersAttackerTaoist3CrackMantraReceiveUpdate(
        string forcedCrackMantraRoll,
        int expectedFlag,
        string expectedAttribute,
        string absentAttribute,
        int expectedDamage)
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        const uint crackMantraDamageOnHuman = 1_100;
        const uint crackMantraDamage2 = 1_000;
        var playerSpells = new PlayerSpellSnapshot(
            [
                CreatePlayerSpell(KineticSparkSpellId),
                CreatePlayerSpell(CombatGearCrackMantraSpellId),
            ]);
        var worldData = CreateWorldData(
            [CreateBowItemType()],
            [
                CreateKineticSparkMagicRow(),
                CreateCombatGearCrackMantraMagicRow(crackMantraDamageOnHuman, crackMantraDamage2),
            ],
            [CreateCombatGearDefinition(CombatGearTaoist3ItemId, CombatGearCrackMantraSpellId)]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var defenderSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var defenderConnection = CreateSendingConnection(defenderSent);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            classCode: 13_005,
            playerItems: CreateEquippedBow(),
            playerSpells: playerSpells,
            combatGear: CreateEquippedCombatGear(CombatGearTaoist3ItemId),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
                [RuntimeLegacyCombatGearTaoistCrackMantraRollAttribute] = forcedCrackMantraRoll,
            });
        var defenderEquippedBow = CreateEquippedBow(50_038);
        var defenderSession = CreateSession(
            PrimaryPlayerUid,
            "Defender",
            PrimaryPlayerX,
            PrimaryPlayerY,
            playerItems: defenderEquippedBow);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(defenderConnection, defenderSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(defenderSession, attackType)).ToFrameBytes()));

        var spark = Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.SpellId == KineticSparkSpellId);
        Assert.Contains(spark.Targets, target => target.Uid == PrimaryPlayerUid);

        Assert.True(sessions.TryGet(defenderConnection, out var updatedDefenderSession));
        Assert.NotNull(updatedDefenderSession);
        Assert.True(HasLegacyStatusFlag(updatedDefenderSession.Character.Attributes, expectedFlag));
        Assert.Equal(
            expectedDamage.ToString(CultureInfo.InvariantCulture),
            updatedDefenderSession.Character.Attributes[expectedAttribute]);
        Assert.False(updatedDefenderSession.Character.Attributes.ContainsKey(absentAttribute));

        AssertCombatGearStatusFlagUpdate(defenderSent, PrimaryPlayerUid, expectedFlag);
        AssertCombatGearArchiveSkillUpdate(
            defenderSent,
            PrimaryPlayerUid,
            expectedFlag,
            duration: 5,
            damage: checked((uint)expectedDamage));
        AssertCombatGearTargetAnimation(casterSent, PlayerUid, PrimaryPlayerUid, CombatGearCrackMantraSpellId);
        var defenderEquipmentRefreshes = ParseShowEquipmentRefreshes(defenderSent);
        Assert.Equal(2, defenderEquipmentRefreshes.Count);
        var expectedDefenderEquipmentRefresh =
            LegacyShowEquipmentPacket.CreateEquipmentRefresh(defenderEquippedBow, alternateEquipmentActive: false);
        Assert.All(
            defenderEquipmentRefreshes,
            refresh => Assert.Equal(expectedDefenderEquipmentRefresh, refresh));
        var defenderStatusIndex = SingleFrameIndex(
            defenderSent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var packet) &&
                packet.Uid == PrimaryPlayerUid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, expectedFlag)));
        var defenderArchiveIndex = SingleFrameIndex(
            defenderSent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var packet) &&
                packet.Uid == PrimaryPlayerUid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == (uint)expectedFlag));
        var defenderEquipmentIndexes = FrameIndexes(
            defenderSent,
            frame => LegacyShowEquipmentPacket.TryParse(frame, out _));
        Assert.Equal(2, defenderEquipmentIndexes.Count);
        Assert.True(defenderStatusIndex < defenderEquipmentIndexes[0]);
        Assert.True(defenderArchiveIndex < defenderEquipmentIndexes[0]);

        var output = log.ToString();
        Assert.Contains("CrackMantra", output);
        Assert.Contains("spellId=" + CombatGearCrackMantraSpellId.ToString(CultureInfo.InvariantCulture), output);
    }

    [Fact]
    public async Task HandlerPlayerAttackWithActiveKineticSparkTriggersAttackerTaoist1DeadwoodCurseReceiveUpdate()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        const uint duration = 8;
        const uint deadwoodDamageOnHuman = 1_000;
        const uint deadwoodDamage2 = 5;
        var playerSpells = new PlayerSpellSnapshot(
            [
                CreatePlayerSpell(KineticSparkSpellId),
                CreatePlayerSpell(CombatGearDeadwoodCurseSpellId),
            ]);
        var worldData = CreateWorldData(
            [CreateBowItemType()],
            [
                CreateKineticSparkMagicRow(),
                CreateCombatGearDeadwoodCurseMagicRow(duration, deadwoodDamageOnHuman, deadwoodDamage2),
            ],
            [CreateCombatGearDefinition(CombatGearTaoist1ItemId, CombatGearDeadwoodCurseSpellId)]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var defenderSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var defenderConnection = CreateSendingConnection(defenderSent);
        var equippedBow = CreateEquippedBow(50_330);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            classCode: 13_005,
            playerItems: equippedBow,
            playerSpells: playerSpells,
            combatGear: CreateEquippedCombatGear(CombatGearTaoist1ItemId),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
            });
        var defenderSession = CreateSession(PrimaryPlayerUid, "Defender", PrimaryPlayerX, PrimaryPlayerY);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(defenderConnection, defenderSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(defenderSession, attackType)).ToFrameBytes()));

        var spark = Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.SpellId == KineticSparkSpellId);
        Assert.Contains(spark.Targets, target => target.Uid == PrimaryPlayerUid);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, CombatGearDeadwoodCurseFlag));
        Assert.Equal(
            deadwoodDamageOnHuman.ToString(CultureInfo.InvariantCulture),
            updatedCasterSession.Character.Attributes[RuntimeLegacyDeadwoodCurseDamageOnHumanAttribute]);
        Assert.Equal(
            deadwoodDamage2.ToString(CultureInfo.InvariantCulture),
            updatedCasterSession.Character.Attributes[RuntimeLegacyDeadwoodCurseDamage2Attribute]);

        AssertCombatGearStatusFlagUpdate(casterSent, PlayerUid, CombatGearDeadwoodCurseFlag);
        AssertCombatGearArchiveSkillUpdate(
            casterSent,
            PlayerUid,
            CombatGearDeadwoodCurseFlag,
            duration,
            deadwoodDamageOnHuman,
            spellLevel: deadwoodDamage2);
        AssertCombatGearSelfAnimation(casterSent, PlayerUid, CombatGearDeadwoodCurseSpellId);
        var equipmentRefresh = Assert.Single(ParseShowEquipmentRefreshes(casterSent));
        Assert.Equal(
            LegacyShowEquipmentPacket.CreateEquipmentRefresh(equippedBow, alternateEquipmentActive: false),
            equipmentRefresh);
        var deadwoodAnimationIndex = SingleFrameIndex(
            casterSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == CombatGearDeadwoodCurseSpellId);
        var equipmentRefreshIndex = SingleFrameIndex(
            casterSent,
            frame => LegacyShowEquipmentPacket.TryParse(frame, out _));
        Assert.True(deadwoodAnimationIndex < equipmentRefreshIndex);

        var output = log.ToString();
        Assert.Contains("DeadwoodCurse", output);
        Assert.Contains("spellId=" + CombatGearDeadwoodCurseSpellId.ToString(CultureInfo.InvariantCulture), output);
    }

    [Fact]
    public async Task HandlerPlayerAttackWithActiveKineticSparkTriggersDefenderAblazeBladePlayerTarget()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var playerSpells = new PlayerSpellSnapshot([CreatePlayerSpell(KineticSparkSpellId)]);
        var worldData = CreateWorldData(
            [CreateBowItemType()],
            [
                CreateKineticSparkMagicRow(),
                CreateCombatGearAblazeBladeMagicRow(),
            ],
            [CreateCombatGearDefinition(CombatGearTaoist5ItemId, CombatGearAblazeBladeSpellId)]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var defenderSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var defenderConnection = CreateSendingConnection(defenderSent);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            playerItems: CreateEquippedBow(),
            playerSpells: playerSpells,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
            });
        var defenderSession = CreateSession(
            PrimaryPlayerUid,
            "Defender",
            PrimaryPlayerX,
            PrimaryPlayerY,
            classCode: 14_005,
            playerSpells: new PlayerSpellSnapshot([CreatePlayerSpell(CombatGearAblazeBladeSpellId)]),
            combatGear: CreateEquippedCombatGear(CombatGearTaoist5ItemId),
            attributes: CreateForcedCombatGearPassiveAttributes(CombatGearAblazeBladeSpellId));
        sessions.Set(casterConnection, casterSession);
        sessions.Set(defenderConnection, defenderSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(defenderSession, attackType)).ToFrameBytes()));

        var spark = Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.SpellId == KineticSparkSpellId);
        Assert.Contains(spark.Targets, target => target.Uid == PrimaryPlayerUid);

        var ablazeAnimations = ParseSpellAnimations(defenderSent)
            .Where(animation => animation.SpellId == CombatGearAblazeBladeSpellId)
            .ToArray();
        Assert.NotEmpty(ablazeAnimations);
        Assert.All(ablazeAnimations, animation => Assert.Equal(PrimaryPlayerUid, animation.AttackerUid));
        Assert.Contains(
            ablazeAnimations.SelectMany(animation => animation.Targets),
            target => target.Uid == PlayerUid && target.Damage > 0 && target.Hit);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(updatedCasterSession.Character.MinHitPoints < casterSession.Character.MinHitPoints);
    }

    [Fact]
    public async Task HandlerPlayerAttackWithActiveKineticSparkTriggersDefenderMagneticLightPlayerTarget()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var playerSpells = new PlayerSpellSnapshot([CreatePlayerSpell(KineticSparkSpellId)]);
        var worldData = CreateWorldData(
            [CreateBowItemType()],
            [
                CreateKineticSparkMagicRow(),
                CreateCombatGearMagneticLightMagicRow(),
            ],
            [CreateCombatGearDefinition(CombatGearTaoist4ItemId, CombatGearMagneticLightSpellId)]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var defenderSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var defenderConnection = CreateSendingConnection(defenderSent);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            playerItems: CreateEquippedBow(),
            playerSpells: playerSpells,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
            });
        var defenderAttributes = new Dictionary<string, string>(
            CreateForcedCombatGearPassiveAttributes(CombatGearMagneticLightSpellId),
            StringComparer.OrdinalIgnoreCase)
        {
            ["Angle"] = "6",
        };
        var defenderSession = CreateSession(
            PrimaryPlayerUid,
            "Defender",
            PrimaryPlayerX,
            PrimaryPlayerY,
            classCode: 13_005,
            playerSpells: new PlayerSpellSnapshot([CreatePlayerSpell(CombatGearMagneticLightSpellId)]),
            combatGear: CreateEquippedCombatGear(CombatGearTaoist4ItemId),
            attributes: defenderAttributes);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(defenderConnection, defenderSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(defenderSession, attackType)).ToFrameBytes()));

        var spark = Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.SpellId == KineticSparkSpellId);
        Assert.Contains(spark.Targets, target => target.Uid == PrimaryPlayerUid);

        Assert.True(sessions.TryGet(defenderConnection, out var updatedDefenderSession));
        Assert.NotNull(updatedDefenderSession);
        Assert.True(HasLegacyStatusFlag(
            updatedDefenderSession.Character.Attributes,
            CombatGearVenomMagicShieldFlag));

        var magneticAnimations = ParseSpellAnimations(defenderSent)
            .Where(animation => animation.SpellId == CombatGearMagneticLightSpellId)
            .ToArray();
        Assert.NotEmpty(magneticAnimations);
        Assert.All(magneticAnimations, animation =>
        {
            Assert.Equal(PrimaryPlayerUid, animation.AttackerUid);
            Assert.Equal(1, animation.BombFlag);
        });
        Assert.Contains(
            magneticAnimations.SelectMany(animation => animation.Targets),
            target => target.Uid == PlayerUid && target.Damage == 15_000U && target.Hit);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        var attackerTookDamage = updatedCasterSession.Character.MinHitPoints < casterSession.Character.MinHitPoints;
        var attackerMoved = updatedCasterSession.Character.X != casterSession.Character.X ||
            updatedCasterSession.Character.Y != casterSession.Character.Y;
        var animationMoved = magneticAnimations
            .SelectMany(animation => animation.Targets)
            .Any(target => target.Uid == PlayerUid && (target.NewX != 0 || target.NewY != 0));
        Assert.True(
            attackerTookDamage || attackerMoved || animationMoved,
            "Expected MagneticLight to damage the attacker or report player pushback.");
    }

    [Fact]
    public async Task HandlerPlayerAttackWithActiveKineticSparkTriggersReceiveAttackPlayerSpeffectAttributeSelfEffects()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var playerSpells = new PlayerSpellSnapshot(
            [
                CreatePlayerSpell(KineticSparkSpellId),
                CreatePlayerSpell(VenomSpellId),
            ]);
        var worldData = CreateWorldData(
            [CreateBowItemType()],
            [
                CreateKineticSparkMagicRow(),
                CreateVenomMagicRow(damageOnHuman: 4_321),
            ]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var defenderSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var defenderConnection = CreateSendingConnection(defenderSent);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            playerItems: CreateEquippedBow(),
            playerSpells: playerSpells,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
                [RuntimeLegacySpeffectLevelAttribute + ".Venom"] = "1",
                [RuntimeLegacySpeffectRollAttribute + ".Venom"] = "0",
            });
        var defenderSession = CreateSession(
            PrimaryPlayerUid,
            "Defender",
            PrimaryPlayerX,
            PrimaryPlayerY,
            minHitPoints: 1_000,
            minMana: 100,
            maxMana: 1_000,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [RuntimeLegacySpeffectLevelAttribute + ".Solid"] = "4",
                [RuntimeLegacySpeffectLevelAttribute + ".Bloodthirst"] = "1",
                [RuntimeLegacySpeffectLevelAttribute + ".Elan"] = "1",
                [RuntimeLegacySpeffectRollAttribute + ".Solid"] = "0",
                [RuntimeLegacySpeffectRollAttribute + ".Bloodthirst"] = "0",
                [RuntimeLegacySpeffectRollAttribute + ".Elan"] = "0",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(defenderConnection, defenderSession);

        var before = DateTimeOffset.UtcNow;
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(defenderSession, attackType)).ToFrameBytes()));
        var after = DateTimeOffset.UtcNow;

        Assert.True(sessions.TryGet(defenderConnection, out var updatedDefenderSession));
        Assert.NotNull(updatedDefenderSession);
        Assert.True(HasLegacyStatusFlag(updatedDefenderSession.Character.Attributes, LegacyVenomFlag));
        Assert.Equal("4321", updatedDefenderSession.Character.Attributes[RuntimeLegacyVenomDamageAttribute]);
        Assert.True(DateTimeOffset.TryParse(
            updatedDefenderSession.Character.Attributes[RuntimeLegacyVenomExpiresAtUtcAttribute],
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var expiresAt));
        Assert.InRange(expiresAt, before.AddSeconds(5), after.AddSeconds(7));
        Assert.True(HasLegacyStatusFlag(updatedDefenderSession.Character.Attributes, LegacySolidFlag));
        Assert.Equal("2400", updatedDefenderSession.Character.Attributes["Solid"]);
        Assert.True(updatedDefenderSession.Character.MinHitPoints > defenderSession.Character.MinHitPoints);
        Assert.Equal(550, updatedDefenderSession.Character.MinMana);

        AssertSpeffectVenomStatusUpdate(defenderSent, PrimaryPlayerUid);
        AssertSpeffectVenomSelfAnimation(casterSent, PlayerUid);
        AssertSpeffectVenomSelfAnimation(defenderSent, PlayerUid);
        AssertSpeffectSolidStatusUpdates(defenderSent, PrimaryPlayerUid);
        AssertSpeffectSolidArchiveSkill(defenderSent, PrimaryPlayerUid);
        AssertSpeffectBloodthirstElanUpdate(defenderSent, PrimaryPlayerUid, value: 4);
        AssertSpeffectBloodthirstElanUpdate(defenderSent, PrimaryPlayerUid, value: 17);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy ReceiveAttack.Player speffect Venom", output);
        Assert.DoesNotContain("WorldServer skipped legacy ReceiveAttack.Player speffect Venom attacker branch", output);
        Assert.Contains("WorldServer applied legacy ReceiveAttack.Player speffect Solid", output);
        Assert.Contains("WorldServer applied legacy ReceiveAttack.Player speffect Bloodthirst", output);
        Assert.Contains("WorldServer applied legacy ReceiveAttack.Player speffect Elan", output);
    }

    [Fact]
    public async Task HandlerPlayerAttackRejectsPrimaryKineticSparkTargetBeforeForcedRateMiss()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var playerSpells = new PlayerSpellSnapshot([CreatePlayerSpell(KineticSparkSpellId)]);
        var worldData = CreateWorldData([CreateBowItemType()], [CreateKineticSparkMagicRow()]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var primarySent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var primaryConnection = CreateSendingConnection(primarySent);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            playerItems: CreateEquippedBow(),
            playerSpells: playerSpells,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "99",
            });
        var primarySession = CreateSession(
            PrimaryPlayerUid,
            "Primary",
            PrimaryPlayerX,
            PrimaryPlayerY,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Invisible"] = "true",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(primaryConnection, primarySession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(primarySession, attackType)).ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains("WorldServer rejected legacy KineticSpark passive player proc target", output);
        Assert.Contains("source CanAttackPlayer.Verified rejected attacked.Invisible", output);
        AssertNoKineticSparkAnimation(casterSent.Concat(primarySent));
    }

    [Fact]
    public async Task HandlerArcherPlayerPreKineticPrimaryCanAttackRejectsBeforePreProcAndKineticRate()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var playerSpells = new PlayerSpellSnapshot(
            [
                CreatePlayerSpell(ArcherPreKineticCloseProcSpellId),
                CreatePlayerSpell(ArcherPreKineticFallbackProcSpellId),
                CreatePlayerSpell(KineticSparkSpellId),
            ]);
        var worldData = CreateWorldData(
            [CreateBowItemType()],
            [
                CreateArcherPreKineticProcMagicRow(ArcherPreKineticCloseProcSpellId),
                CreateArcherPreKineticProcMagicRow(ArcherPreKineticFallbackProcSpellId),
                CreateKineticSparkMagicRow(),
            ]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var primarySent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var primaryConnection = CreateSendingConnection(primarySent);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            playerItems: CreateEquippedBow(),
            playerSpells: playerSpells,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
                [RuntimeLegacyArcherPreKineticProcRateRollAttribute] = "0",
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
            });
        var primarySession = CreateSession(
            PrimaryPlayerUid,
            "Primary",
            PrimaryPlayerX,
            PrimaryPlayerY,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Invisible"] = "true",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(primaryConnection, primarySession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(primarySession, attackType)).ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains("WorldServer rejected legacy Archer pre-Kinetic player proc target", output);
        Assert.Contains("source CanAttackPlayer.Verified rejected attacked.Invisible", output);
        Assert.DoesNotContain("WorldServer handled legacy Archer pre-Kinetic player proc", output);
        Assert.DoesNotContain("WorldServer handled legacy KineticSpark passive player proc", output);
        Assert.DoesNotContain(
            ParseSpellAnimations(casterSent.Concat(primarySent)),
            animation => animation.SpellId is ArcherPreKineticCloseProcSpellId or
                ArcherPreKineticFallbackProcSpellId or
                KineticSparkSpellId);
    }

    [Fact]
    public async Task HandlerArcherPlayerOutOfRangeRejectsBeforePreProcAndKinetic()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var playerSpells = new PlayerSpellSnapshot(
            [
                CreatePlayerSpell(ArcherPreKineticCloseProcSpellId),
                CreatePlayerSpell(ArcherPreKineticFallbackProcSpellId),
                CreatePlayerSpell(KineticSparkSpellId),
            ]);
        var worldData = CreateWorldData(
            [CreateBowItemType()],
            [
                CreateArcherPreKineticProcMagicRow(ArcherPreKineticCloseProcSpellId),
                CreateArcherPreKineticProcMagicRow(ArcherPreKineticFallbackProcSpellId),
                CreateKineticSparkMagicRow(),
            ]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            playerItems: CreateEquippedBow(),
            playerSpells: playerSpells,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
                [RuntimeLegacyArcherPreKineticProcRateRollAttribute] = "0",
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
            });
        var targetSession = CreateSession(
            OutOfRangePlayerUid,
            "OutOfRange",
            OutOfRangePlayerX,
            OutOfRangePlayerY);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(targetSession, attackType)).ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains("WorldServer rejected legacy Archer player target", output);
        Assert.Contains("targetUid=" + OutOfRangePlayerUid.ToString(CultureInfo.InvariantCulture), output);
        Assert.Contains("distance=19 range=18 autoAttack=false", output);
        Assert.Contains("source=MsgAttackPacket.cs AttackID.Archer player branch Base.GetDistance <= 18 gate", output);
        Assert.DoesNotContain("WorldServer handled legacy Archer pre-Kinetic player proc", output);
        Assert.DoesNotContain("WorldServer handled legacy KineticSpark passive player proc", output);
        Assert.DoesNotContain(
            ParseSpellAnimations(casterSent.Concat(targetSent)),
            animation => animation.SpellId is ArcherPreKineticCloseProcSpellId or
                ArcherPreKineticFallbackProcSpellId or
                KineticSparkSpellId);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(targetSession.Character.MinHitPoints, updatedTargetSession.Character.MinHitPoints);
    }

    [Fact]
    public async Task HandlerArcherPlayerOutOfRangeRejectsKineticSparkBeforeRateRoll()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var playerSpells = new PlayerSpellSnapshot([CreatePlayerSpell(KineticSparkSpellId)]);
        var worldData = CreateWorldData([CreateBowItemType()], [CreateKineticSparkMagicRow()]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            playerItems: CreateEquippedBow(),
            playerSpells: playerSpells,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
            });
        var targetSession = CreateSession(
            OutOfRangePlayerUid,
            "OutOfRange",
            OutOfRangePlayerX,
            OutOfRangePlayerY);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(targetSession, attackType)).ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains("WorldServer rejected legacy Archer player target", output);
        Assert.Contains("distance=19 range=18 autoAttack=false", output);
        Assert.DoesNotContain("WorldServer handled legacy KineticSpark passive player proc", output);
        AssertNoKineticSparkAnimation(casterSent.Concat(targetSent));
    }

    [Fact]
    public async Task HandlerArcherPlayer18750PreProcRunsBeforeKinetic()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var playerSpells = new PlayerSpellSnapshot(
            [
                CreatePlayerSpell(ArcherPreKineticCloseProcSpellId),
                CreatePlayerSpell(KineticSparkSpellId),
            ]);
        var worldData = CreateWorldData(
            [CreateBowItemType()],
            [
                CreateArcherPreKineticProcMagicRow(ArcherPreKineticCloseProcSpellId),
                CreateKineticSparkMagicRow(),
            ]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var primarySent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var primaryConnection = CreateSendingConnection(primarySent);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            playerItems: CreateEquippedBow(),
            playerSpells: playerSpells,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
                [RuntimeLegacyArcherPreKineticProcRateRollAttribute] = "0",
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
            });
        var primarySession = CreateSession(PrimaryPlayerUid, "Primary", PrimaryPlayerX, PrimaryPlayerY);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(primaryConnection, primarySession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(primarySession, attackType)).ToFrameBytes()));

        var preProc = Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.SpellId == ArcherPreKineticCloseProcSpellId);
        Assert.Equal(PlayerUid, preProc.AttackerUid);
        Assert.Equal(PrimaryPlayerX, preProc.X);
        Assert.Equal(PrimaryPlayerY, preProc.Y);
        Assert.All(preProc.Targets, target => Assert.Equal(PrimaryPlayerUid, target.Uid));
        Assert.DoesNotContain(ParseSpellAnimations(casterSent), animation => animation.SpellId == KineticSparkSpellId);
        Assert.Contains("WorldServer handled legacy Archer pre-Kinetic player proc", log.ToString());
        Assert.DoesNotContain("WorldServer handled legacy KineticSpark passive player proc", log.ToString());
    }

    [Fact]
    public async Task HandlerArcherPlayerCombatGearSuppressesPreProcsAndKinetic()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var playerSpells = new PlayerSpellSnapshot(
            [
                CreatePlayerSpell(ArcherPreKineticCloseProcSpellId),
                CreatePlayerSpell(ArcherPreKineticFallbackProcSpellId),
                CreatePlayerSpell(KineticSparkSpellId),
            ]);
        var worldData = CreateWorldData(
            [CreateBowItemType()],
            [
                CreateCombatGearActiveMagicRow(CombatGearColdMoonSpellId),
                CreateArcherPreKineticProcMagicRow(ArcherPreKineticCloseProcSpellId),
                CreateArcherPreKineticProcMagicRow(ArcherPreKineticFallbackProcSpellId),
                CreateKineticSparkMagicRow(),
            ]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var primarySent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var primaryConnection = CreateSendingConnection(primarySent);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            classCode: 4_001,
            playerItems: CreateEquippedBow(),
            playerSpells: playerSpells,
            combatGear: CreateEquippedCombatGear(CombatGearColdMoonItemId),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
                [RuntimeLegacyArcherPreKineticProcRateRollAttribute] = "0",
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
            });
        var primarySession = CreateSession(PrimaryPlayerUid, "Primary", PrimaryPlayerX, PrimaryPlayerY);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(primaryConnection, primarySession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(primarySession, attackType)).ToFrameBytes()));

        var combatGear = Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.SpellId == CombatGearColdMoonSpellId);
        Assert.Equal(PlayerUid, combatGear.AttackerUid);
        Assert.Equal(PrimaryPlayerX, combatGear.X);
        Assert.Equal(PrimaryPlayerY, combatGear.Y);
        Assert.Contains(combatGear.Targets, target => target.Uid == PrimaryPlayerUid);
        Assert.DoesNotContain(
            ParseSpellAnimations(casterSent.Concat(primarySent)),
            animation => animation.SpellId is ArcherPreKineticCloseProcSpellId or
                ArcherPreKineticFallbackProcSpellId or
                KineticSparkSpellId);
        Assert.DoesNotContain(
            ParseAttacks(casterSent.Concat(primarySent)),
            attack => attack.Query.AttackType == attackType &&
                attack.Query.OpponentUid == PrimaryPlayerUid);

        Assert.True(sessions.TryGet(primaryConnection, out var updatedPrimarySession));
        Assert.NotNull(updatedPrimarySession);
        Assert.True(updatedPrimarySession.Character.MinHitPoints < primarySession.Character.MinHitPoints);

        var output = log.ToString();
        Assert.Contains("WorldServer replaced legacy Archer player physical attack with CombatGear spell", output);
        Assert.Contains("spellId=" + CombatGearColdMoonSpellId.ToString(CultureInfo.InvariantCulture), output);
        Assert.DoesNotContain("WorldServer handled legacy Archer pre-Kinetic player proc", output);
        Assert.DoesNotContain("WorldServer handled legacy KineticSpark passive player proc", output);
    }

    [Fact]
    public async Task HandlerArcherPlayerPreKineticRateMissFallsThroughToKinetic()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var playerSpells = new PlayerSpellSnapshot(
            [
                CreatePlayerSpell(ArcherPreKineticCloseProcSpellId),
                CreatePlayerSpell(ArcherPreKineticFallbackProcSpellId),
                CreatePlayerSpell(KineticSparkSpellId),
            ]);
        var worldData = CreateWorldData(
            [CreateBowItemType()],
            [
                CreateArcherPreKineticProcMagicRow(ArcherPreKineticCloseProcSpellId),
                CreateArcherPreKineticProcMagicRow(ArcherPreKineticFallbackProcSpellId),
                CreateKineticSparkMagicRow(),
            ]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var primarySent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var primaryConnection = CreateSendingConnection(primarySent);
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            playerItems: CreateEquippedBow(),
            playerSpells: playerSpells,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
                [RuntimeLegacyArcherPreKineticProcRateRollAttribute] = "99",
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
            });
        var primarySession = CreateSession(PrimaryPlayerUid, "Primary", PrimaryPlayerX, PrimaryPlayerY);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(primaryConnection, primarySession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(primarySession, attackType)).ToFrameBytes()));

        var kinetic = Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.SpellId == KineticSparkSpellId);
        Assert.Contains(kinetic.Targets, target => target.Uid == PrimaryPlayerUid);
        Assert.DoesNotContain(
            ParseSpellAnimations(casterSent),
            animation => animation.SpellId is ArcherPreKineticCloseProcSpellId or
                ArcherPreKineticFallbackProcSpellId);
        Assert.DoesNotContain("WorldServer handled legacy Archer pre-Kinetic player proc", log.ToString());
        Assert.Contains("WorldServer handled legacy KineticSpark passive player proc", log.ToString());
    }

    private static MsgAttackQuery CreatePlayerAttack(WorldSession target, MsgAttackType attackType)
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: CheckedUInt32(target.Character.Uid),
            X: checked((ushort)target.Character.X),
            Y: checked((ushort)target.Character.Y),
            AttackType: attackType,
            Damage: 500,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0);
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

    private static WorldSession CreateSession(
        uint uid,
        string name,
        ushort x,
        ushort y,
        long classCode = 142,
        PlayerItemSnapshot? playerItems = null,
        PlayerSpellSnapshot? playerSpells = null,
        PlayerProficiencySnapshot? playerProficiencies = null,
        CombatGearSnapshot? combatGear = null,
        IReadOnlyDictionary<string, string>? attributes = null,
        long minHitPoints = 20_000,
        long minMana = 500,
        long maxMana = 500)
    {
        return new WorldSession(
            AccountId: checked((int)(uid - 1_293_500)),
            Username: name.ToLowerInvariant(),
            new CharacterSnapshot(
                Uid: uid,
                Name: name,
                Body: 1_003,
                Face: 0,
                Hair: 0,
                Class: classCode,
                FirstClass: classCode,
                SecondClass: 0,
                Reborn: 0,
                Level: 120,
                Experience: 0,
                MapId,
                X: x,
                Y: y,
                PreviousMapId: MapId,
                PreviousMapX: x,
                PreviousMapY: y,
                Strength: 1_000,
                Agility: 0,
                Vitality: 0,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: minHitPoints,
                MinMana: minMana,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 1,
                SourceFile: "fixture://kinetic-spark-passive-player-runtime",
                Attributes: attributes is null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(attributes, StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
            {
                MaxMana = maxMana,
            },
            playerItems ?? PlayerItemSnapshot.Empty,
            playerSpells ?? PlayerSpellSnapshot.Empty,
            playerProficiencies ?? PlayerProficiencySnapshot.Empty,
            CombatGear: combatGear ?? CombatGearSnapshot.Empty);
    }

    private static PlayerSpellSnapshotEntry CreatePlayerSpell(
        ushort spellId,
        ushort level = 0,
        int experience = 0)
    {
        return new PlayerSpellSnapshotEntry(
            spellId,
            level,
            experience,
            PreviousLevel: 0,
            SoulLevel: 0,
            UseSpellSoul: 0);
    }

    private static PlayerItemSnapshot CreateEquippedBow(uint itemUid = 0)
    {
        return new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                new PlayerItemSnapshotItem
                {
                    Uid = itemUid,
                    ItemId = BowItemType,
                    Durability = 100,
                    MaximumDurability = 100,
                    Position = 4,
                },
            ],
            RuneItems: []);
    }

    private static ItemType CreateBowItemType(uint attack = 1_000)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = BowItemType.ToString(CultureInfo.InvariantCulture);
        fields[1] = "SourceClueBow";
        fields[13] = attack.ToString(CultureInfo.InvariantCulture);
        fields[14] = attack.ToString(CultureInfo.InvariantCulture);
        return new ItemType(BowItemType, "SourceClueBow", fields);
    }

    private static SobNpc CreateSobNpc(
        int id,
        string name,
        ushort x,
        ushort y,
        int kind = 10,
        int life = 50_000,
        int maxLife = 50_000)
    {
        var fields = new[]
        {
            id.ToString(CultureInfo.InvariantCulture),
            name,
            kind.ToString(CultureInfo.InvariantCulture),
            "1137",
            MapId.ToString(CultureInfo.InvariantCulture),
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
            Lookface: 1_137,
            MapId,
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
        fields[14] = "5";
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

    private static MagicType CreateMudWallMagicRow(byte level, uint damageOnHuman, uint damage2)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = MudWallSpellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = MudWallSpellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = AttachStatusSort.ToString(CultureInfo.InvariantCulture);
        fields[3] = "MudWall";
        fields[8] = level.ToString(CultureInfo.InvariantCulture);
        fields[9] = "0";
        fields[10] = "0";
        fields[12] = "0";
        fields[13] = "0";
        fields[14] = "1";
        fields[15] = "18";
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
        return new MagicType(MudWallSpellId, MudWallSpellId, "MudWall", fields);
    }

    private static MagicType CreateInfiniteMistMagicRow()
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = LegacyInfiniteMistSpellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = LegacyInfiniteMistSpellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = AttachStatusSort.ToString(CultureInfo.InvariantCulture);
        fields[3] = "InfiniteMist";
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = "1";
        fields[12] = "10";
        fields[13] = "2";
        fields[14] = "0";
        fields[15] = "15";
        fields[16] = "282";
        fields[18] = "0";
        fields[20] = "0";
        fields[22] = "0";
        fields[29] = "0";
        fields[33] = "0";
        fields[35] = "0";
        fields[36] = "0";
        fields[37] = "0";
        fields[44] = "0";
        fields[47] = "0";
        return new MagicType(LegacyInfiniteMistSpellId, LegacyInfiniteMistSpellId, "InfiniteMist", fields);
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

    private static GouyuType CreateGouyuType(int id, int type, int level, int power)
    {
        var values = Enumerable.Repeat(0, 10).ToArray();
        values[5] = power;
        return new GouyuType(
            id,
            type,
            level,
            values,
            "fixture://gouyu_type",
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

    private static MagicType CreateVenomMagicRow(uint damageOnHuman, byte level = 0)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
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

    private static MagicType CreateArcherPreKineticProcMagicRow(ushort spellId)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = (spellId == ArcherPreKineticFallbackProcSpellId ? SectorBackSort : BombSort)
            .ToString(CultureInfo.InvariantCulture);
        fields[3] = spellId == ArcherPreKineticFallbackProcSpellId ? "NebulousHunt" : "CrescentShadow";
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = "100";
        fields[12] = "30";
        fields[13] = "0";
        fields[14] = "3";
        fields[15] = "4";
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
            spellId == ArcherPreKineticFallbackProcSpellId ? "NebulousHunt" : "CrescentShadow",
            fields);
    }

    private static MagicType CreateCombatGearActiveMagicRow(ushort spellId)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = AttackSort.ToString(CultureInfo.InvariantCulture);
        fields[3] = "ColdMoon";
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = "120";
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
        return new MagicType(spellId, spellId, "ColdMoon", fields);
    }

    private static MagicType CreateCombatGearRedcursePassiveMagicRow()
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = CombatGearRedcursePassiveSpellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = CombatGearRedcursePassiveSpellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = AttachStatusSort.ToString(CultureInfo.InvariantCulture);
        fields[3] = "ArmorOfImmunity";
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = "0";
        fields[12] = "10";
        fields[13] = "2";
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
            CombatGearRedcursePassiveSpellId,
            CombatGearRedcursePassiveSpellId,
            "ArmorOfImmunity",
            fields);
    }

    private static MagicType CreateCombatGearWildPhoenixMagicRow()
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = CombatGearWildPhoenixSpellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = CombatGearWildPhoenixSpellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = AttachStatusSort.ToString(CultureInfo.InvariantCulture);
        fields[3] = "WildPhoenix";
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = "4";
        fields[12] = "10";
        fields[13] = "32";
        fields[14] = "1";
        fields[15] = "18";
        fields[18] = "0";
        fields[20] = "1";
        fields[22] = "0";
        fields[29] = "0";
        fields[33] = "0";
        fields[35] = "18";
        fields[36] = "7";
        fields[37] = "0";
        fields[44] = "0";
        fields[47] = "0";
        return new MagicType(
            CombatGearWildPhoenixSpellId,
            CombatGearWildPhoenixSpellId,
            "WildPhoenix",
            fields);
    }

    private static MagicType CreateCombatGearUpdateTargetPassiveMagicRow(ushort spellId)
    {
        var name = spellId switch
        {
            CombatGearHunterSpellId => "Hunter",
            CombatGearFlowKnackSpellId => "FlowKnack",
            CombatGearWaterAegisSpellId => "WaterAegis",
            CombatGearFantasyKnackSpellId => "FantasyKnack",
            CombatGearDivineEmptinessSpellId => "DivineEmptiness",
            CombatGearFlameShieldSpellId => "FlameShield",
            _ => "CombatGearPassive",
        };
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = AttachStatusSort.ToString(CultureInfo.InvariantCulture);
        fields[3] = name;
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = "700";
        fields[12] = "1";
        fields[13] = "10";
        fields[14] = "1";
        fields[15] = "18";
        fields[18] = "0";
        fields[20] = "1";
        fields[22] = "0";
        fields[29] = "0";
        fields[33] = "0";
        fields[35] = "450";
        fields[36] = "25";
        fields[37] = "0";
        fields[44] = "6";
        fields[47] = "0";
        return new MagicType(spellId, spellId, name, fields);
    }

    private static MagicType CreateCombatGearDragonhowlMagicRow(
        uint duration,
        uint damage,
        uint damageOnHuman,
        uint damage2,
        uint damage3)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = CombatGearDragonhowlSpellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = CombatGearDragonhowlSpellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = AttachStatusSort.ToString(CultureInfo.InvariantCulture);
        fields[3] = "Dragonhowl";
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = damage.ToString(CultureInfo.InvariantCulture);
        fields[12] = "50";
        fields[13] = duration.ToString(CultureInfo.InvariantCulture);
        fields[14] = "1";
        fields[15] = "18";
        fields[18] = "0";
        fields[20] = "1";
        fields[22] = "0";
        fields[29] = "0";
        fields[33] = "0";
        fields[35] = damageOnHuman.ToString(CultureInfo.InvariantCulture);
        fields[36] = damage2.ToString(CultureInfo.InvariantCulture);
        fields[37] = damage3.ToString(CultureInfo.InvariantCulture);
        fields[44] = "0";
        fields[47] = "0";
        return new MagicType(
            CombatGearDragonhowlSpellId,
            CombatGearDragonhowlSpellId,
            "Dragonhowl",
            fields);
    }

    private static MagicType CreateCombatGearBloodlustImmersionMagicRow(uint duration, uint damage)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = CombatGearBloodlustImmersionSpellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = CombatGearBloodlustImmersionSpellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = AttachStatusSort.ToString(CultureInfo.InvariantCulture);
        fields[3] = "Immersion";
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = damage.ToString(CultureInfo.InvariantCulture);
        fields[12] = "100";
        fields[13] = duration.ToString(CultureInfo.InvariantCulture);
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
            CombatGearBloodlustImmersionSpellId,
            CombatGearBloodlustImmersionSpellId,
            "Immersion",
            fields);
    }

    private static MagicType CreateCombatGearBloodlustInsoucianceMagicRow(uint duration, uint damage)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = CombatGearBloodlustInsoucianceSpellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = CombatGearBloodlustInsoucianceSpellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = AttachStatusSort.ToString(CultureInfo.InvariantCulture);
        fields[3] = "Insouciance";
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = damage.ToString(CultureInfo.InvariantCulture);
        fields[12] = "100";
        fields[13] = duration.ToString(CultureInfo.InvariantCulture);
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
            CombatGearBloodlustInsoucianceSpellId,
            CombatGearBloodlustInsoucianceSpellId,
            "Insouciance",
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

    private static MagicType CreateCombatGearPirateFusingMagicRow(uint gDamage = 500)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = CombatGearPirateFusingSpellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = CombatGearPirateFusingSpellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = AttachStatusSort.ToString(CultureInfo.InvariantCulture);
        fields[3] = "Fusing";
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = gDamage.ToString(CultureInfo.InvariantCulture);
        fields[12] = "0";
        fields[13] = "0";
        fields[14] = "1";
        fields[15] = "18";
        fields[16] = CombatGearPirateFusingFlag.ToString(CultureInfo.InvariantCulture);
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
            CombatGearPirateFusingSpellId,
            CombatGearPirateFusingSpellId,
            "Fusing",
            fields);
    }

    private static MagicType CreateCombatGearPirateSailingMagicRow(uint blockDamage)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = CombatGearPirateSailingSpellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = CombatGearPirateSailingSpellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = AttachStatusSort.ToString(CultureInfo.InvariantCulture);
        fields[3] = "Sailing";
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = blockDamage.ToString(CultureInfo.InvariantCulture);
        fields[12] = "100";
        fields[13] = "10";
        fields[14] = "1";
        fields[15] = "18";
        fields[16] = CombatGearPirateSailingFlag.ToString(CultureInfo.InvariantCulture);
        fields[18] = "0";
        fields[20] = "1";
        fields[22] = "0";
        fields[29] = "0";
        fields[33] = "0";
        fields[35] = blockDamage.ToString(CultureInfo.InvariantCulture);
        fields[36] = "0";
        fields[37] = "0";
        fields[44] = "0";
        fields[47] = "0";
        return new MagicType(
            CombatGearPirateSailingSpellId,
            CombatGearPirateSailingSpellId,
            "Sailing",
            fields);
    }

    private static MagicType CreateCombatGearPirateBlackJadeUseRuneMagicRow(
        ushort spellId,
        int statusFlag,
        uint duration,
        uint gDamage)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = AttachStatusSort.ToString(CultureInfo.InvariantCulture);
        fields[3] = "BlackJadeUseRune" + spellId.ToString(CultureInfo.InvariantCulture);
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = gDamage.ToString(CultureInfo.InvariantCulture);
        fields[12] = "100";
        fields[13] = duration.ToString(CultureInfo.InvariantCulture);
        fields[14] = "1";
        fields[15] = "18";
        fields[16] = statusFlag.ToString(CultureInfo.InvariantCulture);
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
        var name = fields[3];
        return new MagicType(spellId, spellId, name, fields);
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

    private static MagicType CreateCombatGearCrackMantraMagicRow(uint damageOnHuman, uint damage2)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = CombatGearCrackMantraSpellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = CombatGearCrackMantraSpellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = AttachStatusSort.ToString(CultureInfo.InvariantCulture);
        fields[3] = "CrackMantra";
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = "0";
        fields[12] = "1500";
        fields[13] = "1";
        fields[14] = "15";
        fields[15] = "15";
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
            CombatGearCrackMantraSpellId,
            CombatGearCrackMantraSpellId,
            "CrackMantra",
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

    private static MagicType CreateCombatGearArchShadowMagicRow()
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = CombatGearArchShadowSpellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = CombatGearArchShadowSpellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = AttackSort.ToString(CultureInfo.InvariantCulture);
        fields[3] = "ArchShadow";
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = "160";
        fields[12] = "10";
        fields[13] = "5";
        fields[14] = "1";
        fields[15] = "20";
        fields[18] = "0";
        fields[20] = "1";
        fields[22] = "0";
        fields[29] = "0";
        fields[33] = "0";
        fields[35] = "5";
        fields[36] = "0";
        fields[37] = "0";
        fields[44] = "0";
        fields[47] = "0";
        return new MagicType(
            CombatGearArchShadowSpellId,
            CombatGearArchShadowSpellId,
            "ArchShadow",
            fields);
    }

    private static MagicType CreateStoneCrackerStarFlowMagicRow(uint damageOnHuman = 450)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = CombatGearStoneCrackerStarFlowSpellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = CombatGearStoneCrackerStarFlowSpellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = "5"; // Matching10950 is Bomb; its proc query has OpponentUID0.
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

    private static MagicType CreateCombatGearMagneticLightMagicRow()
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = CombatGearMagneticLightSpellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = CombatGearMagneticLightSpellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = "105";
        fields[3] = "MagneticLight";
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = "15000";
        fields[12] = "1";
        fields[13] = "10";
        fields[14] = "1";
        fields[15] = "18";
        fields[18] = "0";
        fields[20] = "1";
        fields[22] = "0";
        fields[29] = "0";
        fields[33] = "0";
        fields[35] = "500";
        fields[36] = "0";
        fields[37] = "0";
        fields[44] = "0";
        fields[47] = "0";
        return new MagicType(
            CombatGearMagneticLightSpellId,
            CombatGearMagneticLightSpellId,
            "MagneticLight",
            fields);
    }

    private static MagicType CreateCombatGearAblazeBladeMagicRow()
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = CombatGearAblazeBladeSpellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = CombatGearAblazeBladeSpellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = AttackSort.ToString(CultureInfo.InvariantCulture);
        fields[3] = "AblazeBlade";
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = "600";
        fields[12] = "1";
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
            CombatGearAblazeBladeSpellId,
            CombatGearAblazeBladeSpellId,
            "AblazeBlade",
            fields);
    }

    private static CombatGearSnapshot CreateEquippedCombatGear(uint itemId)
    {
        return new CombatGearSnapshot(
            0,
            0,
            0,
            0,
            1,
            CombatGearAirPowersSnapshot.Empty,
            [
                new CombatGearSnapshotItem(itemId, 1, 0, 1, 0, 0, [[], []], []),
            ],
            CombatGearBlackJadeBagSnapshot.Empty);
    }

    private static CombatGearSnapshot CreateEquippedCombatGear(params uint[] itemIds)
    {
        return new CombatGearSnapshot(
            0,
            0,
            0,
            0,
            itemIds.Length,
            CombatGearAirPowersSnapshot.Empty,
            itemIds
                .Select(itemId => new CombatGearSnapshotItem(itemId, 1, 0, 1, 0, 0, [[], []], []))
                .ToArray(),
            CombatGearBlackJadeBagSnapshot.Empty);
    }

    private static CombatGearSnapshot CreateEquippedCombatGearWithBlackJades(
        uint itemId,
        params uint[] blackJadeIds)
    {
        return new CombatGearSnapshot(
            0,
            0,
            0,
            0,
            1,
            CombatGearAirPowersSnapshot.Empty,
            [
                new CombatGearSnapshotItem(itemId, 1, 0, 1, 0, 0, [[], []], blackJadeIds),
            ],
            CombatGearBlackJadeBagSnapshot.Empty);
    }

    private static CombatGear CreateRedcurseCombatGearDefinition()
    {
        return CreateCombatGearDefinition(CombatGearRedcurseItemId, CombatGearRedcursePassiveSpellId);
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

    private static DaoqiDictType CreateDaoqiBlackJadeSpellEntry(int id, int spellAttributeValue)
    {
        var fields = new[]
        {
            id.ToString(CultureInfo.InvariantCulture),
            "2",
            "24",
            spellAttributeValue.ToString(CultureInfo.InvariantCulture),
            "0",
            "0",
            "600",
            "0",
            "0",
            "0",
            "10",
            "0",
            "0",
            "10",
            "701",
            "",
        };

        return new DaoqiDictType(
            id,
            Type: 2,
            Level: 24,
            Values: fields.Skip(3).Take(12).Select(value =>
                int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : 0).ToArray(),
            SourceFile: "fixture://daoqi_dict_type",
            LineNumber: 1,
            RawLine: string.Join("@@", fields),
            Fields: fields);
    }

    private static IReadOnlyDictionary<string, string> CreateForcedCombatGearPassiveAttributes(
        ushort? spellId = null)
    {
        var attributeName = spellId is null
            ? RuntimeLegacyCombatGearPassiveRollAttribute
            : RuntimeLegacyCombatGearPassiveRollAttribute + "." +
                spellId.Value.ToString(CultureInfo.InvariantCulture);
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [attributeName] = "0",
        };
    }

    private static WorldDataCatalog CreateWorldData(
        IReadOnlyList<ItemType> items,
        IReadOnlyList<MagicType> magicRows,
        IReadOnlyList<CombatGear>? combatGear = null,
        IReadOnlyList<DaoqiDictType>? daoqiDictTypes = null,
        IReadOnlyList<SobNpc>? sobNpcs = null,
        IReadOnlyList<GouyuType>? gouyuTypes = null,
        IReadOnlyList<GouyuImmortal>? gouyuImmortals = null)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-23T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://kinetic-spark-passive-player-runtime",
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
            gouyuTypes: gouyuTypes ?? [],
            gouyuImmortals: gouyuImmortals ?? [],
            specialEffectAttributes: [],
            daoqiDictTypes: daoqiDictTypes ?? [],
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
            combatGear: combatGear ?? [],
            traps: [],
            npcs: [],
            sobNpcs: sobNpcs ?? [],
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
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
    }

    private static async Task<PirateBlackJadeUseRuneResult> RunPirateBlackJadeUseRuneAsync(
        ushort spellId,
        uint blackJadeId,
        MagicType magicRow,
        uint bowItemUid,
        params int[] extraStatusFlags)
    {
        var equippedBow = CreateEquippedBow(bowItemUid);
        var worldData = CreateWorldData(
            [CreateBowItemType()],
            [
                CreateKineticSparkMagicRow(),
                magicRow,
            ],
            daoqiDictTypes:
            [
                CreateDaoqiBlackJadeSpellEntry(
                    checked((int)blackJadeId),
                    checked(spellId * 1000)),
            ]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var defenderSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var defenderConnection = CreateSendingConnection(defenderSent);
        var statusFlags = new[] { KineticSparkStatusFlag }
            .Concat(extraStatusFlags)
            .ToArray();
        var casterSession = CreateSession(
            PlayerUid,
            "Caster",
            PlayerX,
            PlayerY,
            classCode: 7_005,
            playerItems: equippedBow,
            playerSpells: new PlayerSpellSnapshot([CreatePlayerSpell(KineticSparkSpellId)]),
            combatGear: CreateEquippedCombatGearWithBlackJades(CombatGearPirate1ItemId, blackJadeId),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(statusFlags),
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
                [RuntimeLegacyCombatGearAttackSpellIndexAttribute] = "0",
                [RuntimeLegacyCombatGearAttackRateRollAttribute + "." +
                    spellId.ToString(CultureInfo.InvariantCulture)] = "0",
            });
        var defenderSession = CreateSession(PrimaryPlayerUid, "Defender", PrimaryPlayerX, PrimaryPlayerY);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(defenderConnection, defenderSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(CreatePlayerAttack(defenderSession, MsgAttackType.Archer)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.SpellId == KineticSparkSpellId);
        return new PirateBlackJadeUseRuneResult(
            casterSent,
            updatedCasterSession,
            equippedBow,
            log.ToString());
    }

    private sealed record PirateBlackJadeUseRuneResult(
        List<byte[]> CasterSent,
        WorldSession UpdatedCasterSession,
        PlayerItemSnapshot EquippedBow,
        string LogOutput);

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

    private static void AssertNoKineticSparkAnimation(IEnumerable<byte[]> frames)
    {
        Assert.DoesNotContain(
            ParseSpellAnimations(frames),
            animation => animation.SpellId == KineticSparkSpellId);
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

    private static IReadOnlyList<LegacyProficiencyPacket> ParseProficiencyPackets(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyProficiencyPacket>();
        foreach (var bytes in frames)
        {
            if (LegacyProficiencyPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyShowEquipmentPacket> ParseShowEquipmentRefreshes(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyShowEquipmentPacket>();
        foreach (var bytes in frames)
        {
            if (LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static int SingleFrameIndex(IReadOnlyList<byte[]> frames, Func<PacketFrame, bool> predicate)
    {
        var indexes = new List<int>();
        for (var index = 0; index < frames.Count; index++)
        {
            if (predicate(ReadFrame(frames[index])))
            {
                indexes.Add(index);
            }
        }

        return Assert.Single(indexes);
    }

    private static IReadOnlyList<int> FrameIndexes(
        IReadOnlyList<byte[]> frames,
        Func<PacketFrame, bool> predicate)
    {
        var indexes = new List<int>();
        for (var index = 0; index < frames.Count; index++)
        {
            if (predicate(ReadFrame(frames[index])))
            {
                indexes.Add(index);
            }
        }

        return indexes;
    }

    private static void AssertInfiniteMistArchiveSkillBeforeKineticSpark(
        IReadOnlyList<byte[]> frames,
        uint attackerUid,
        uint expectedDuration,
        uint expectedDamage)
    {
        AssertArchiveSkillBeforeKineticSpark(
            frames,
            attackerUid,
            LegacyInfiniteMistFlag,
            expectedDuration,
            expectedDamage);
    }

    private static void AssertArchiveSkillBeforeKineticSpark(
        IReadOnlyList<byte[]> frames,
        uint uid,
        int flag,
        uint expectedDuration,
        uint expectedDamage)
    {
        var archiveIndexes = FrameIndexes(
            frames,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var packet) &&
                packet.Uid == uid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == checked((uint)flag) &&
                    entry.Time == expectedDuration &&
                    entry.Damage == expectedDamage));
        Assert.True(
            archiveIndexes.Count > 0,
            $"Expected KineticSpark player receive path to emit ArchiveSkill flag {flag} before the KineticSpark animation.");
        var sparkIndex = SingleFrameIndex(
            frames,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == KineticSparkSpellId);
        Assert.True(
            archiveIndexes[0] < sparkIndex,
            $"Expected ArchiveSkill flag {flag} to precede the KineticSpark animation.");
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

    private static void AssertDefenderCombatGearEquipmentRefreshTail(
        IReadOnlyList<byte[]> defenderSent,
        uint defenderUid,
        ushort spellId,
        int expectedStatusFlag,
        PlayerItemSnapshot expectedEquipment,
        bool expectsArchiveSkill)
    {
        var expectedEquipmentRefresh =
            LegacyShowEquipmentPacket.CreateEquipmentRefresh(expectedEquipment, alternateEquipmentActive: false);
        var equipmentRefreshes = ParseShowEquipmentRefreshes(defenderSent);
        Assert.Equal(2, equipmentRefreshes.Count);
        Assert.All(
            equipmentRefreshes,
            refresh =>
            {
                Assert.Equal(expectedEquipmentRefresh, refresh);
                Assert.Equal(LegacyShowEquipmentPacket.ShowAction, refresh.Action);
                Assert.Equal(0U, refresh.WParam);
                Assert.Equal(0U, refresh.Alternante);
            });

        var statusIndexes = FrameIndexes(
            defenderSent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var packet) &&
                packet.Uid == defenderUid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, expectedStatusFlag)));
        var archiveIndexes = FrameIndexes(
            defenderSent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var packet) &&
                packet.Uid == defenderUid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == checked((uint)expectedStatusFlag)));
        var animationIndexes = FrameIndexes(
            defenderSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == spellId);
        var equipmentIndexes = FrameIndexes(
            defenderSent,
            frame => LegacyShowEquipmentPacket.TryParse(frame, out _));

        Assert.Equal(2, statusIndexes.Count);
        Assert.Equal(expectsArchiveSkill ? 2 : 0, archiveIndexes.Count);
        Assert.Equal(2, animationIndexes.Count);
        Assert.Equal(2, equipmentIndexes.Count);

        for (var index = 0; index < equipmentIndexes.Count; index++)
        {
            Assert.True(statusIndexes[index] < animationIndexes[index]);
            if (expectsArchiveSkill)
            {
                Assert.True(statusIndexes[index] < archiveIndexes[index]);
                Assert.True(archiveIndexes[index] < animationIndexes[index]);
            }

            Assert.True(animationIndexes[index] < equipmentIndexes[index]);
        }
    }

    private static void AssertCombatGearPassiveStatusUpdates(
        IEnumerable<byte[]> frames,
        uint uid,
        int expectedCount)
    {
        Assert.Equal(
            expectedCount,
            ParseMsgUpdates(frames).Count(
                packet => packet.Uid == uid &&
                    packet.Updates.Any(entry =>
                        entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                        entry.Values is not null &&
                        IsLegacyStatusFlagSet(entry.Values, CombatGearLightningMagicShieldTop4WeeklyFlag))));
    }

    private static void AssertCombatGearPassiveArchiveSkills(
        IEnumerable<byte[]> frames,
        uint uid,
        int expectedCount)
    {
        Assert.Equal(
            expectedCount,
            ParseMsgUpdates(frames).Count(
                packet => packet.Uid == uid &&
                    packet.Updates.Any(entry =>
                        entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                        entry.Flag == CombatGearLightningMagicShieldTop4WeeklyFlag &&
                        entry.Time == 2)));
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
        uint duration,
        uint? damage = null,
        uint? spellLevel = null)
    {
        Assert.Contains(
            ParseMsgUpdates(frames),
            packet => packet.Uid == uid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == checked((uint)flag) &&
                    entry.Time == duration &&
                    (damage is null || entry.Damage == damage.Value) &&
                    (spellLevel is null || entry.SpellLevel == spellLevel.Value)));
    }

    private static LegacySpellAnimationPacket AssertCombatGearSelfAnimation(
        IEnumerable<byte[]> frames,
        uint uid,
        ushort spellId,
        bool? expectedHit = null)
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
        if (expectedHit is not null)
        {
            Assert.Equal(expectedHit.Value, target.Hit);
        }

        return animation;
    }

    private static void AssertCombatGearTargetAnimation(
        IEnumerable<byte[]> frames,
        uint attackerUid,
        uint targetUid,
        ushort spellId)
    {
        var animation = Assert.Single(
            ParseSpellAnimations(frames),
            packet => packet.SpellId == spellId);
        Assert.Equal(attackerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(0, animation.X);
        Assert.Equal(0, animation.Y);

        var target = Assert.Single(animation.Targets);
        Assert.Equal(targetUid, target.Uid);
        Assert.Equal(0U, target.Damage);
    }

    private static void AssertSpeffectVenomStatusUpdate(IEnumerable<byte[]> frames, uint uid)
    {
        Assert.Contains(
            ParseMsgUpdates(frames),
            packet => packet.Uid == uid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, LegacyVenomFlag)));
    }

    private static void AssertSpeffectVenomSelfAnimation(IEnumerable<byte[]> frames, uint uid)
    {
        var animation = Assert.Single(
            ParseSpellAnimations(frames),
            packet => packet.SpellId == VenomSpellId);
        Assert.Equal(uid, animation.AttackerUid);
        Assert.Equal(uid, animation.AttackedUid);
        Assert.Equal(0, animation.X);
        Assert.Equal(0, animation.Y);

        var target = Assert.Single(animation.Targets);
        Assert.Equal(uid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.False(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
    }

    private static void AssertSpeffectSolidStatusUpdates(IEnumerable<byte[]> frames, uint uid)
    {
        Assert.Contains(
            ParseMsgUpdates(frames),
            packet => packet.Uid == uid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, LegacySolidFlag)));
    }

    private static void AssertSpeffectSolidArchiveSkill(IEnumerable<byte[]> frames, uint uid)
    {
        Assert.Contains(
            ParseMsgUpdates(frames),
            packet => packet.Uid == uid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == LegacySolidFlag &&
                    entry.Time == 5 &&
                    entry.Damage == 2_400));
    }

    private static void AssertSpeffectBloodthirstElanUpdate(
        IEnumerable<byte[]> frames,
        uint uid,
        ulong value)
    {
        Assert.Contains(
            ParseMsgUpdates(frames),
            packet => packet.Uid == uid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.BloodthirstElan &&
                    entry.Value == value));
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

    private static string BuildStatusFlags(params int[] flags)
    {
        var words = new ulong[18];
        foreach (var flag in flags)
        {
            words[flag / 32] |= 1UL << (flag % 32);
        }

        return string.Join('/', words);
    }

    private static uint CheckedUInt32(long value)
    {
        return checked((uint)value);
    }
}
