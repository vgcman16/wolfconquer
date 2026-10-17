using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static readonly Regex LegacyNameInvalidTrailingCharacterPattern =
        new("[^A-Za-z0-9ء-ي*~.&.$一-龥]$", RegexOptions.CultureInvariant);

    private static readonly string[] LegacyNameForbiddenTokens =
    [
        "GM",
        "PM",
        "MrRayzo",
        "TestSrv",
        "SYSTEM",
        "None",
        "Clicker",
        "SpeedHack",
        "Aimbot",
        "CheatEngine",
        "Hack",
        "Engine",
        "Cheat",
        "clicker",
        "speed",
        "GMPM",
        "Moksha",
        "TQ",
        "TQGMPM",
        "TQPM",
        "TQGM",
        "guard1",
        "Guard",
        "Guard1",
        "Kosamk",
        "kosamk",
        "{",
        "}",
        "[",
        "]",
    ];



    private static readonly ushort[] LegacyBansheeTyrantBossSpellTable = [30_013, 30_014, 10_372, 30_013];
    private static readonly ushort[] LegacySpookGluttonBossSpellTable = [8_003, 11_311, 10_364];
    private static readonly ushort[] LegacyMonster3971BossSpellTable = LegacyBoss3971SpellBranch.SpellIds.ToArray();
    private static readonly ushort[] LegacyMonster213883BossSpellTable = [7_013, 7_017, 10_362, 10_531, 11_322];
    private static readonly ushort[] LegacyMonster999996641BossSpellTable = [13_190];
    private static readonly ushort[] LegacyMonster6643BossSpellTable = [10_500, 10_502, 10_503, 10_504, 10_506];
    private static readonly ushort[] LegacyMonster7981BossSpellTable = LegacyBossSpellBranch.SpellIds.ToArray();

    private const ushort LegacyEagleEyeSpellId = 11_030;
    private const ushort LegacyMoveLineSpellId = 11_110;
    private const ushort LegacyAddBlackSpotSpellId = 11_100;
    private const ushort LegacyBlackSpotSpellId = 11_120;
    private const ushort LegacyWindstormSpellId = 11_140;
    private const ushort LegacyBloomOfDeathSpellId = 14_720;
    private const ushort LegacyCounterPunchSpellId = 14_470;
    private const ushort LegacyBackfireSpellId = 12_680;
    private const ushort LegacyCounterKillSpellId = 6_003;
    private const ushort LegacyEagleEyeCountdownSpellId = 11_130;
    private const ushort LegacyRevengeTailSpellId = 13_030;
    private const ushort LegacyHellVortexSpellId = 16_370;
    private const ushort LegacyAngerOfStomperSpellId = 12_980;
    private const ushort LegacySacredBlessingSpellId = 15_890;
    private const ushort LegacyDeathSighSpellId = 15_900;
    private const ushort LegacyDeadlyStrikeSpellId = 15_910;
    private const ushort LegacyHerculesSpellId = 1_115;
    private const ushort LegacyBloodyScytheSpellId = 11_170;
    private const ushort LegacyMegaboltSpellId = 15_790;
    private const ushort LegacyWildDashAttackSpellId = 17_660;
    private const ushort LegacyThunderStrikeSpellId = 15_860;
    private const ushort LegacyRevengeAttackSpellId = 17_410;
    private const ushort LegacyArrowBladesSpellId = 18_220;
    private const ushort LegacyStarburstArrowsSpellId = 18_240;
    private const ushort LegacyDragonTransformationPassiveSpellId = 22_590;
    private const ushort LegacyDragonRisingSpellId = 22_630;
    private const ushort LegacySuanniRoarSpellId = 22_810;
    private const ushort LegacyRoamerSpellId = 7_040;
    private const ushort LegacyPirateDrukyleSpellId = 21_530;
    private const ushort LegacyPirateSpitfireSpellId = 21_600;
    private const ushort LegacyPirateLavaSeaSpellId = 21_610;
    private const ushort LegacyPirateStarVolcanoSpellId = 21_620;
    private const ushort LegacyPirateCaptiveArrowSpellId = 21_640;
    private const ushort LegacyPirateSandMistSpellId = 21_650;
    private const ushort LegacyPirateSpitfirePassiveSpellId = 21_690;
    private const ushort LegacyPirateStarVolcanoPassiveSpellId = 22_070;
    private const ushort LegacyPirateLavaSeaPassiveSpellId = 22_080;
    private const ushort LegacyPirateDrukylePassiveSpellId = 22_110;
    private const ushort LegacyPirateHolySanctionSpellId = 21_510;
    private const ushort LegacyPirateIceAgeSpellId = 21_560;
    private const ushort LegacyPirateTwospiendSpearSpellId = 21_570;
    private const ushort LegacyPiratePheasantBeakSpellId = 21_580;
    private const ushort LegacyPirateGiantGunSpellId = 21_660;
    private const ushort LegacyPirateHolySanctionPassiveSpellId = 21_670;
    private const ushort LegacyPirateTwospiendSpearPassiveSpellId = 21_680;
    private const ushort LegacyPirateGiantGunPassiveSpellId = 21_700;
    private const ushort LegacyPirateThunderlordAttackSpellId = 21_920;
    private const ushort LegacyPirateIceAgePassiveSpellId = 22_090;
    private const ushort LegacyPiratePheasantBeakPassiveSpellId = 22_100;
    private const ushort LegacySeaBurialSpellId = 14_680;
    private const ushort LegacySuperTwofoldBladeSpellId = 12_080;
    private const ushort LegacySelectorCannonBarrageSpellId = 11_050;
    private const ushort LegacyWarSuit402NormalAttack1SpellId = 11_950;
    private const ushort LegacyWarSuit402NormalAttack2SpellId = 11_951;
    private const ushort LegacyWarSuit402NormalAttack3SpellId = 11_952;
    private const ushort LegacyWildwindSpellId = 14_530;
    private const ushort LegacyHeavensWrathSpellId = 15_710;
    private const ushort LegacySelectorCrackingShockSpellId = 15_720;
    private const ushort LegacySelectorThunderRampageSpellId = 15_780;
    private const ushort LegacyWindstormBattleaxeSpellId = 15_800;
    private const ushort LegacyNormalAttack1SpellId = 17_390;
    private const ushort LegacyNormalAttack2SpellId = 17_391;
    private const ushort LegacyNormalAttack3SpellId = 17_392;
    private const ushort LegacyWarSuit401NormalAttack1SpellId = 17_400;
    private const ushort LegacyWarSuit401NormalAttack2SpellId = 17_401;
    private const ushort LegacyTripleBlastsSpellId = 12_850;
    private const ushort LegacySwirlingStormSpellId = 12_890;
    private const ushort LegacyChargingVortexSpellId = 11_190;
    private const ushort LegacyBlisteringWaveSpellId = 11_650;
    private const ushort LegacyMortalWoundSpellId = 11_660;
    private const ushort LegacyBonePulseSpellId = 16_390;
    private const ushort LegacyWaterPrisonSpellId = 16_400;
    private const ushort LegacyWaterPrisonPassiveSpellId = 16_410;
    private const ushort LegacyWhirlShurikenSpellId = 16_420;
    private const ushort LegacyWhirlShurikenPassiveSpellId = 16_430;
    private const ushort LegacyLightningSlashSpellId = 16_480;
    private const ushort LegacyLightningSlashPassiveSpellId = 16_490;
    private const ushort LegacyWildFireballSpellId = 16_520;
    private const ushort LegacyWildFireballPassiveSpellId = 16_530;
    private const ushort LegacyIronboneSpellId = 17_640;
    private const ushort LegacyIronbonePassiveSpellId = 17_650;
    private const ushort LegacyHealingSnowSpellId = 12_950;
    private const ushort LegacyChillingSnowSpellId = 12_960;
    private const ushort LegacyFreezingPelterSpellId = 13_020;
    private const ulong LegacyConquerActionNone = 0;
    private const ulong LegacyConquerActionSit = 250;
    private const int LegacyMagicRateField = 12;
    private const int LegacyMagicDamageField = 10;
    private const int LegacyHellVortexRadius = 6;
    private const string MonsterRuntimeStateResource = nameof(MonsterRuntimeStateStore);
    private const string SobNpcRuntimeStateResource = nameof(SobNpcRuntimeStateStore);
    private const string FloorItemRuntimeStateResource = nameof(FloorItemRuntimeStateStore);
    private const string WorldDataResource = nameof(WorldDataCatalog);
    private const string RuntimeBlackSpotAttribute = "BlackSpot";
    private const string RuntimeBlackSpotExpiresAtUtcAttribute = "BlackSpotExpiresAtUtc";
    private const int LegacyBlackSpotDirectRange = 2;
    private const int LegacyPhysicalSpellsHitCount = 3;
    private const int LegacyPhysicalSpellsMonsterRange = 3;
    private const int LegacyPhysicalSpellsPlayerRange = 4;
    private const int LegacyBloomOfDeathRange = 6;
    private const int LegacyPirateBombRadius = 6;
    private const long LegacyViewThreshold = 18;
    private const long LegacyStaticNpcViewThreshold = 19;
    private const long LegacyFloorItemPickupRange = 5;










    private const uint LegacyMaxBuyCount = 100;




































    private const long LegacyMaxPlayerLevel = 140;
    private const uint LegacyArrestTokenItemId = 711624;
    private const byte LegacyNpcClosedOption = byte.MaxValue;



    private static readonly Encoding LegacyCharacterSnapshotEncoding = CreateLegacyCharacterSnapshotEncoding();
    private static readonly ushort[] LegacyGeneratedNecklaceTypes = [120, 121];
    private static readonly ushort[] LegacyGeneratedRingTypes = [150, 151];
    private static readonly ushort[] LegacyGeneratedArmetTypes = [111, 112, 113, 114, 117, 118];
    private static readonly ushort[] LegacyGeneratedArmorTypes = [130, 131, 132, 133, 134];
    private static readonly ushort[] LegacyGeneratedOneHanderTypes = [410, 420, 421, 430, 440, 450, 460, 480, 481, 490, 500, 601];
    private static readonly ushort[] LegacyGeneratedTwoHanderTypes = [510, 530, 560, 561, 580, 900];
    private static readonly uint[] LegacyMap1011Or1020Family3130ItemDrops = [721_080, 722_057, 1_088_000, 3_005_126, 3_005_125];
    private static readonly uint[] LegacyDeityLandRandomDropItems =
    [
        3_007_269,
        3_006_531,
        3_006_532,
        3_006_533,
        3_006_534,
        3_006_535,
        3_006_536,
        3_006_537,
        3_006_538,
        3_006_539,
        3_008_185,
        3_008_186,
        3_008_187,
        3_008_188,
        3_008_189,
        3_330_770,
        3_007_269,
    ];
    private static readonly uint[] LegacyDeityLandWarDevilLuckyRewardItems =
    [
        3_307_085,
        3_307_086,
        3_307_087,
        3_303_484,
        3_008_193,
        3_008_198,
        3_008_197,
        3_008_196,
        3_008_195,
        3_008_194,
        3_303_974,
        3_307_084,
        3_307_085,
        3_307_086,
        3_307_087,
        3_307_092,
        3_307_093,
        3_307_094,
        3_307_244,
        3_307_098,
        1_088_000,
        3_316_793,
        3_330_770,
    ];
    private static readonly (uint ItemId, byte SourceCount)[] LegacyDeityLandWarDevilBaseRewardItems =
    [
        (3_303_520, 5),
        (3_008_735, 4),
        (3_008_965, 2),
        (3_301_252, 1),
        (3_303_085, 5),
    ];
    private static readonly uint[] LegacyDeityLandStoneSpriteRewardItems =
    [
        730_001,
        730_002,
        730_003,
        730_004,
        730_005,
    ];



    private const int LegacyFlashingNameStatusFlag = 0;
    private const int LegacyRedNameStatusFlag = 0x0E;
    private const int LegacyBlackNameStatusFlag = 0x0F;
    private const int LegacyFlyFlag = 0x1B;
    private const int LegacyDeadFlag = 0x05;
    private const int LegacyGhostFlag = 0x0A;
    private const int LegacyStarOfAccuracyFlag = 0x07;
    private const int LegacyMagicShieldFlag = 0x08;
    private const int LegacyStigmaFlag = 0x09;
    private const int LegacyFocusedFlag = 0x15;
    private const int LegacyInvisibilityFlag = 0x16;
    private const int LegacySupermanFlag = 0x12;
    private const int LegacyFatalStrikeFlag = 0x2F;
    private const int LegacyCastPrayFlag = 0x1E;
    private const int LegacyPrayingFlag = 0x1F;
    private const int LegacyHeavenBlessingFlag = 0x21;
    private const int LegacyRideFlag = 0x32;
    private const int LegacyMagicDefenderFlag = 0x80;
    private const int LegacyShurikenVortexFlag = 0x2E;
    private const int LegacyBackfireFlag = 0xB3;
    private const int LegacyRevengeTailFlag = 0xCA;
    private const int LegacyImmortalForceFlag = 0xDC;
    private const int LegacyRiseOfTaoismFlag = 0xE3;
    private const int LegacyIronGuardFlag = 0xE4;
    private const int LegacyTopWaterTaoistFlag = 0x29;
    private const int LegacyConfusedFlag = 0x3C;
    private const int LegacyCursedFlag = 0x20;
    private const int LegacyTop8WeeklyFlag = 0x3F;
    private const int LegacyTop4WeeklyFlag = 0x40;
    private const int LegacyTop2WeeklyFlag = 0x41;
    private const int LegacyFireAuraFlag = 0x6C;
    private const uint LegacyPkModePk = 0;
    private const uint LegacyPkModePeace = 1;
    private const uint LegacyPkModeTeam = 2;
    private const uint LegacyPkModeCapture = 3;
    private const uint LegacyPkModeRevenge = 4;
    private const uint LegacyPkModeGuild = 5;
    private const uint LegacyPkModeJiang = 6;
    private const uint LegacyPkModeCs = 7;
    private const uint LegacyPkModeUnion = 11;
    private const ulong LegacyJiangAttackFlagNone = 0;
    private const ulong LegacyJiangAttackFlagNotHitFriends = 1;
    private const ulong LegacyJiangAttackFlagNotHitClanMembers = 2;
    private const ulong LegacyJiangAttackFlagNotHitGuildMembers = 4;
    private const ulong LegacyJiangAttackFlagNotHitAlliedGuild = 8;
    private const ulong LegacyJiangAttackFlagNoHitAlliesClan = 16;
    private const long LegacySameGarmentPkMapId = 1_082;
    private const ushort LegacyGarmentEquipmentPosition = 9;
    private const ushort LegacyAlternateGarmentEquipmentPosition = 59;
    private const ushort LegacyDevouringStrikeSpellId = 15_700;
    private const uint LegacyActionPickDwParam = 220;
    private static readonly HashSet<long> LegacyFreePkMapIds =
    [
        10_762, 10_763, 10_593, 10_300, 10_759, 10_079, 3_872, 10_299, 10_760, 10_298,
        10_761, 10_397, 10_398, 1_039_910_402, 10_401, 10_403, 10_404, 10_437, 10_438,
        10_439, 10_440, 10_441, 10_442, 6_011, 3_998, 6_784, 5_342, 8_009, 2_353,
        5_339, 6_964, 3_071, 6_546, 3_935, 8_437, 9_988, 5_599, 2_071, 7_357,
        6_521, 3_825, 6_891, 8_521, 6_525, 6_729, 6_000, 6_001, 1_505,
        1_005, 1_038, 700, 1_508, 3_868, 2_057, 1_082,
    ];
    private static readonly HashSet<long> LegacyBlockAttackMapIds =
    [
        10_364, 11_724, 10_343, 10_322, 3_825, 6_072, 3_830, 5_040, 3_820, 1_004,
        3_831, 3_832, 3_834, 3_826, 3_827, 3_828, 3_829, 3_833, 9_995, 1_068,
        4_020, 4_000, 4_003, 4_006, 4_008, 4_009, 1_860, 1_858, 1_801, 1_780,
        1_779, 9_972, 1_806, 1_002, 3_954, 10_342, 10_341, 10_324, 10_323, 3_081,
        1_036, 1_008, 601, 1_006, 1_511, 1_039, 700, 2_068, 2_063, 2_064,
        2_061, 2_062, 1_950,
    ];


    private const int LegacyPotionCooldownMilliseconds = 150;
    private const uint LegacyServerConfigPhysicalDamagePercent = 100;
    private const uint LegacyServerConfigProficiencyExperienceRate = 1_000;
    private const int LegacyPathOfShadowFlag = 0x91;
    private const int LegacyFineRain1Flag = 0xD4;
    private const int LegacyFineRain2Flag = 0xD5;
    private const int LegacyFreezeFlag = 0x3B;
    private const int LegacyAzureShieldFlag = 0x5D;
    private const int LegacyShieldBlockFlag = 0x71;
    private const int LegacyPoisonStarFlag = 0x77;
    private const int LegacyScurvyBombFlag = 0x60;
    private const int LegacyCannonBarrageFlag = 0x78;
    private const int LegacyBlackbeardsRageFlag = 0x79;
    private const int LegacyOblivionFlag = 0x70;
    private const int LegacyManiacDanceFlag = 0xB5;
    private const int LegacySacrificeFlag = 0xD3;
    private const int LegacyInfinityFlag = 0xD7;
    private const int LegacyAbsolutionFlag = 0xD8;
    private const int LegacyIronShieldFlag = 0xD9;
    private const int LegacySlayerFlag = 0xDA;
    private const int LegacyDeathSighFlag = 0xFD;
    private const int LegacyHealingSnowFlag = 0xC4;
    private const int LegacyChillingSnowFlag = 0xC5;
    private const int LegacyXChillingSnowFlag = 0xC6;
    private const int LegacyFreezingPelterFlag = 0xC8;
    private const int LegacyNeptuneCurseFlag = 0xDE;
    private const int LegacyCrackStarFlag = 0xE6;
    private const int LegacyThunderRampageFlag = 0xEB;
    private const int LegacyAttackUpFlag = 0xE9;
    private const int LegacyPaperDanceFlag = 0x120;
    private const int LegacyHookMoonFlag = 0x103;
    private const int LegacyHookMoonCasterFlag = 0x104;
    private const int LegacyCelestialDanceFlag = 0xFA;
    private const int LegacyAxeShadowFlag = 0xFB;
    private const int LegacyVenomFlag = 0x163;
    private const int LegacySolidFlag = 0x164;
    private const ushort LegacyTideTrapSpellId = 14_250;
    private const uint LegacyTideTrapFloorItemId = 1_080;
    private const int LegacyTideTrapDetonationMilliseconds = 4_000;
    private const double LegacyTideTrapHitRadius = 2.0;
    private const int LegacyHawksAmbitionFlag = 0x100;
    private const int LegacyScurvyBombFatigueFlag = 0x14;
    private const ulong LegacyScurvyBombFatigueValue = 6;
    private const uint LegacyBoostedPotionHitpointPercent = 200;
    private const uint LegacySteedRaceFrozenTrapSeconds = 4;
    private const uint LegacyShadowCloneCounterStart = 700_100;
    private const uint LegacyLivePetCounterStart = 700_200;
    private const ushort LegacyThundercloudRuntimeSpellId = 13_190;
    private const uint LegacyThundercloudFloorItemId = 3_843;
    private const int LegacyThundercloudPulseMilliseconds = 1_000;
    private const int LegacyThundercloudLifetimeMilliseconds = 25_000;
    private const long LegacyNewGuildWarMapId = 1_038;
    private const string RuntimeNewGuildWarItemsAttribute = "NewGuildWarItems";
    private const string RuntimePendingMessageBoxAttribute = LegacyRuntimeMessageBoxPrompt.PendingAttribute;
    private const string RuntimeAlienWorldLeaveMessageBox = "AlienWorldLeave";
    private const string RuntimeTopInHoursInvitationMessageBox = LegacyRuntimeMessageBoxPrompt.TopInHoursInvitationKey;
    private const string RuntimePendingMessageBoxMapIdAttribute = LegacyRuntimeMessageBoxPrompt.DestinationMapIdAttribute;
    private const string RuntimePendingMessageBoxXAttribute = LegacyRuntimeMessageBoxPrompt.DestinationXAttribute;
    private const string RuntimePendingMessageBoxYAttribute = LegacyRuntimeMessageBoxPrompt.DestinationYAttribute;
    private const string RuntimeLegacyMonsterDropRollAttribute = "LegacyMonsterDropRoll";
    private const string RuntimeLegacyMonsterDeathConquerPointRollAttribute = "LegacyMonsterDeathConquerPointRoll";
    private const string RuntimeLegacyMonsterDeathConquerPointRateRollAttribute = "LegacyMonsterDeathConquerPointRateRoll";
    private const string RuntimeLegacyMonsterDeathConquerPointRatePercentAttribute = "LegacyMonsterDeathConquerPointRatePercent";
    private const string RuntimeLegacyFamily3319RewardBranchAttribute = "LegacyFamily3319RewardBranch";
    private const string RuntimeLegacyFamily8418RewardBranchAttribute = "LegacyFamily8418RewardBranch";
    private const string RuntimeLegacyHouseDemonRewardBranchAttribute = "LegacyHouseDemonRewardBranch";
    private const string RuntimeLegacyMap3071Family2700RewardBranchAttribute = "LegacyMap3071Family2700RewardBranch";
    private const string RuntimeLegacyMap3071Family7022RewardBranchAttribute = "LegacyMap3071Family7022RewardBranch";
    private const string RuntimeLegacyMap3935BeastSpawnRateAttribute = "LegacyMap3935BeastSpawnRate";
    private const string RuntimeLegacyMap1780KidHunterSpawnRateAttribute = "LegacyMap1780KidHunterSpawnRate";
    private const string RuntimeLegacyMap1015Family85ScarletTokenRateAttribute = "LegacyMap1015Family85ScarletTokenRate";
    private const string RuntimeLegacyMap1015Family2226PitchTokenRateAttribute = "LegacyMap1015Family2226PitchTokenRate";
    private const string RuntimeLegacyMap1015Family2225BlackTokenRateAttribute = "LegacyMap1015Family2225BlackTokenRate";
    private const string RuntimeLegacyMap1015Family79IndigoTokenRateAttribute = "LegacyMap1015Family79IndigoTokenRate";
    private const string RuntimeLegacyMap1015Family79ShotSaberRateAttribute = "LegacyMap1015Family79ShotSaberRate";
    private const string RuntimeLegacyMap1015Family84YarnBallRateAttribute = "LegacyMap1015Family84YarnBallRate";
    private const string RuntimeLegacyMap1015Family84FlowerWineRateAttribute = "LegacyMap1015Family84FlowerWineRate";
    private const string RuntimeLegacyMap1015Family84RoyalSwordRateAttribute = "LegacyMap1015Family84RoyalSwordRate";
    private const string RuntimeLegacyMap1015Family55FoodSupplyRateAttribute = "LegacyMap1015Family55FoodSupplyRate";
    private const string RuntimeLegacyMap1015Family55BaconRateAttribute = "LegacyMap1015Family55BaconRate";
    private const string RuntimeLegacyMap1015Family19HawkClawRateAttribute = "LegacyMap1015Family19HawkClawRate";
    private const string RuntimeLegacyMap1015Family19GallbladerrRateAttribute = "LegacyMap1015Family19GallbladerrRate";
    private const string RuntimeLegacyMap1015Family19FeatherRateAttribute = "LegacyMap1015Family19FeatherRate";
    private const string RuntimeLegacyMap1015Family19OldBookRateAttribute = "LegacyMap1015Family19OldBookRate";
    private const string RuntimeLegacyMap1015Family78HawkClawRateAttribute = "LegacyMap1015Family78HawkClawRate";
    private const string RuntimeLegacyMap1015Family78GallbladerrRateAttribute = "LegacyMap1015Family78GallbladerrRate";
    private const string RuntimeLegacyMap1015Family78Quest1628RateAttribute = "LegacyMap1015Family78Quest1628Rate";
    private const string RuntimeLegacyMap1015Family78HawkBloodRateAttribute = "LegacyMap1015Family78HawkBloodRate";
    private const string RuntimeLegacyMap1015Family78HawkClawFangRateAttribute = "LegacyMap1015Family78HawkClawFangRate";
    private const string RuntimeLegacyMap1015Family77BirdBloodRateAttribute = "LegacyMap1015Family77BirdBloodRate";
    private const string RuntimeLegacyMap1015Family77BirdBeakRateAttribute = "LegacyMap1015Family77BirdBeakRate";
    private const string RuntimeLegacyMap1015Family77SharpClawRateAttribute = "LegacyMap1015Family77SharpClawRate";
    private const string RuntimeLegacyMap1015Family77BirdmanFeatherRateAttribute = "LegacyMap1015Family77BirdmanFeatherRate";
    private const string RuntimeLegacyMap1015Family18Ground729094RateAttribute = "LegacyMap1015Family18Ground729094Rate";
    private const string RuntimeLegacyMap1015Family18BirdBloodRateAttribute = "LegacyMap1015Family18BirdBloodRate";
    private const string RuntimeLegacyMap1015Family18GoldenBeakRateAttribute = "LegacyMap1015Family18GoldenBeakRate";
    private const string RuntimeLegacyMap1015Family18BirdmanFeathersRateAttribute = "LegacyMap1015Family18BirdmanFeathersRate";
    private const string RuntimeLegacyMap1015Family18BirdmanArmRateAttribute = "LegacyMap1015Family18BirdmanArmRate";
    private const string RuntimeLegacyMap1015Family18ToolBagRateAttribute = "LegacyMap1015Family18ToolBagRate";
    private const string RuntimeLegacyMap1000Family2179CapriceLeaderRateAttribute = "LegacyMap1000Family2179CapriceLeaderRate";
    private const string RuntimeLegacyMap1000Family76BladeGhostFangRateAttribute = "LegacyMap1000Family76BladeGhostFangRate";
    private const string RuntimeLegacyMap1000Family76StoneSpellRateAttribute = "LegacyMap1000Family76StoneSpellRate";
    private const string RuntimeLegacyMap1000Family76AntidoteRateAttribute = "LegacyMap1000Family76AntidoteRate";
    private const string RuntimeLegacyMap1000Family76BladeGhostEarRateAttribute = "LegacyMap1000Family76BladeGhostEarRate";
    private const string RuntimeLegacyMap1000Family17BladeGhostEarRateAttribute = "LegacyMap1000Family17BladeGhostEarRate";
    private const string RuntimeLegacyMap1000Family17StraightHookRateAttribute = "LegacyMap1000Family17StraightHookRate";
    private const string RuntimeLegacyMap1000Family17SharpBladeRateAttribute = "LegacyMap1000Family17SharpBladeRate";
    private const string RuntimeLegacyMap1000Family17SharpAxeRateAttribute = "LegacyMap1000Family17SharpAxeRate";
    private const string RuntimeLegacyMap1000Family75StoneRateAttribute = "LegacyMap1000Family75StoneRate";
    private const string RuntimeLegacyMap1000Family75StoneChoiceAttribute = "LegacyMap1000Family75StoneChoice";
    private const string RuntimeLegacyMap1000Family75MarbleRateAttribute = "LegacyMap1000Family75MarbleRate";
    private const string RuntimeLegacyMap1000Family75MonsterTailRateAttribute = "LegacyMap1000Family75MonsterTailRate";
    private const string RuntimeLegacyMap1000Family75ThunderStoneRateAttribute = "LegacyMap1000Family75ThunderStoneRate";
    private const string RuntimeLegacyMap1000Family51RockMonsterTailRateAttribute = "LegacyMap1000Family51RockMonsterTailRate";
    private const string RuntimeLegacyMap1000Family51ColorfulStoneRateAttribute = "LegacyMap1000Family51ColorfulStoneRate";
    private const string RuntimeLegacyMap1000Family51GrindstoneRateAttribute = "LegacyMap1000Family51GrindstoneRate";
    private const string RuntimeLegacyMap1000Family51SlabstoneRateAttribute = "LegacyMap1000Family51SlabstoneRate";
    private const string RuntimeLegacyMap1000Family74SecretLetterRateAttribute = "LegacyMap1000Family74SecretLetterRate";
    private const string RuntimeLegacyMap1000Family74ArmorRateAttribute = "LegacyMap1000Family74ArmorRate";
    private const string RuntimeLegacyMap1000Family14SandbagRateAttribute = "LegacyMap1000Family14SandbagRate";
    private const string RuntimeLegacyMap1000Family14SandPillRateAttribute = "LegacyMap1000Family14SandPillRate";
    private const string RuntimeLegacyMap1000Family14Quest1453RateAttribute = "LegacyMap1000Family14Quest1453Rate";
    private const string RuntimeLegacyMap1000Family15HeartRateAttribute = "LegacyMap1000Family15HeartRate";
    private const string RuntimeLegacyMap1000Family15HammerRateAttribute = "LegacyMap1000Family15HammerRate";
    private const string RuntimeLegacyMap1000Family15HeavyAxeRateAttribute = "LegacyMap1000Family15HeavyAxeRate";
    private const string RuntimeLegacyMap1000Family73SandEssenceRateAttribute = "LegacyMap1000Family73SandEssenceRate";
    private const string RuntimeLegacyMap1020Family72Quest1341RateAttribute = "LegacyMap1020Family72Quest1341Rate";
    private const string RuntimeLegacyMap1020Family13Quest1341RateAttribute = "LegacyMap1020Family13Quest1341Rate";
    private const string RuntimeLegacyMap1020Family13Quest1333RateAttribute = "LegacyMap1020Family13Quest1333Rate";
    private const string RuntimeLegacyMap1020Family13Quest1783RateAttribute = "LegacyMap1020Family13Quest1783Rate";
    private const string RuntimeLegacyMap1020Family71Quest1328RateAttribute = "LegacyMap1020Family71Quest1328Rate";
    private const string RuntimeLegacyMap1020Family12Quest1327RateAttribute = "LegacyMap1020Family12Quest1327Rate";
    private const string RuntimeLegacyMap1020Family12Quest1329RateAttribute = "LegacyMap1020Family12Quest1329Rate";
    private const string RuntimeLegacyMap1020Family11Quest1363RateAttribute = "LegacyMap1020Family11Quest1363Rate";
    private const string RuntimeLegacyMap1020Family14334ApeLeaderRateAttribute = "LegacyMap1020Family14334ApeLeaderRate";
    private const string RuntimeLegacyMap1020Family10Quest1311RateAttribute = "LegacyMap1020Family10Quest1311Rate";
    private const string RuntimeLegacyMap1020Family10Quest1746RateAttribute = "LegacyMap1020Family10Quest1746Rate";
    private const string RuntimeLegacyMap1020Family11223VultureFeatherRateAttribute = "LegacyMap1020Family11223VultureFeatherRate";
    private const string RuntimeLegacyMap1011Family11007BanditDropRateAttribute = "LegacyMap1011Family11007BanditDropRate";
    private const string RuntimeLegacyMap3935Quest35007StrikePointsAttribute = "LegacyMap3935Quest35007StrikePoints";
    private const string RuntimeLegacyMap3998Family40900RuneFragmentRateAttribute = "LegacyMap3998Family40900RuneFragmentRate";
    private const string RuntimeLegacyMap3998Family40905WheelOfNatureRateAttribute = "LegacyMap3998Family40905WheelOfNatureRate";
    private const string RuntimeLegacyMap3998Family40906EarthForceRateAttribute = "LegacyMap3998Family40906EarthForceRate";
    private const string RuntimeLegacyMonsterDropMoneyAmountAttribute = "LegacyMonsterDropMoneyAmount";
    private const string RuntimeLegacyMapSpecialDropRateAttribute = "LegacyMapSpecialDropRate";
    private const string RuntimeLegacyMapSpecialDropItemIndexAttribute = "LegacyMapSpecialDropItemIndex";
    private const string RuntimeLegacyDeityLandRandomDropRateAttribute = "LegacyDeityLandRandomDropRate";
    private const string RuntimeLegacyDeityLandRandomDropRollAttribute = "LegacyDeityLandRandomDropRoll";
    private const string RuntimeLegacyDeityLandReward4050001RateAttribute = "LegacyDeityLandReward4050001Rate";
    private const string RuntimeLegacyDeityLandReward4060001RateAttribute = "LegacyDeityLandReward4060001Rate";
    private const string RuntimeLegacyDeityLandRedRuneFragmentRateAttribute = "LegacyDeityLandRedRuneFragmentRate";
    private const string RuntimeLegacyDeityLandYellowRuneFragmentRateAttribute = "LegacyDeityLandYellowRuneFragmentRate";
    private const string RuntimeLegacyDeityLandInventoryRelicFirstRateAttribute = "LegacyDeityLandInventoryRelicFirstRate";
    private const string RuntimeLegacyDeityLandInventoryRelicSecondRateAttribute = "LegacyDeityLandInventoryRelicSecondRate";
    private const string RuntimeLegacyDeityLandRareChestKeyFirstRateAttribute = "LegacyDeityLandRareChestKeyFirstRate";
    private const string RuntimeLegacyDeityLandRareChestKeySecondRateAttribute = "LegacyDeityLandRareChestKeySecondRate";
    private const string RuntimeLegacyDeityLandEpicChestKeyFirstRateAttribute = "LegacyDeityLandEpicChestKeyFirstRate";
    private const string RuntimeLegacyDeityLandEpicChestKeySecondRateAttribute = "LegacyDeityLandEpicChestKeySecondRate";
    private const string RuntimeLegacyDeityLandLuckyPointsAttribute = "DeityLandLuckyPoints";
    private const string RuntimeLegacyDeityLandWarDevilLuckyRewardRollAttribute = "LegacyDeityLandWarDevilLuckyRewardRoll";
    private const string RuntimeLegacyDeityLandWarDevilBaseRewardRollAttribute = "LegacyDeityLandWarDevilBaseRewardRoll";
    private const string RuntimeLegacyDeityLandStoneSpriteRewardItemIdAttribute = "LegacyDeityLandStoneSpriteRewardItemId";
    private const string RuntimeLegacyMap1001BullGhostCaptureRateAttribute = "LegacyMap1001BullGhostCaptureRate";
    private const string RuntimeLegacyMap1001BatEssenceRateAttribute = "LegacyMap1001BatEssenceRate";
    private const string RuntimeLegacyMap1001TombBatKeyRateAttribute = "LegacyMap1001TombBatKeyRate";
    private const string RuntimeLegacyOnFerentPillAttribute = "OnFerentPill";
    private const string RuntimeLegacyOnFerventPillAttribute = "OnFerventPill";
    private const string RuntimeLegacyBattleFieldTournamentAliveAttribute = "LegacyBattleFieldTournamentAlive";
    private const string RuntimeLegacySaturnAliveAttribute = "LegacySaturnAlive";
    private const uint LegacyDeityLandQuestId = 3_944;
    private const string RuntimeLegacyBoss4212RareDropAttribute = "LegacyBoss4212RareDrop";
    private const string RuntimeLegacyBoss4171RareDropAttribute = "LegacyBoss4171RareDrop";
    private const string RuntimeLegacyBoss4220RareDropAttribute = "LegacyBoss4220RareDrop";
    private const string RuntimeLegacyBoss20055Add711679Attribute = "LegacyBoss20055Add711679";
    private const string RuntimeLegacyBoss20055Add711188Attribute = "LegacyBoss20055Add711188";
    private const string RuntimeLegacyBoss10250PrimaryDropIndexAttribute = "LegacyBoss10250PrimaryDropIndex";
    private const string RuntimeLegacyBoss10250SecondaryDropIndexAttribute = "LegacyBoss10250SecondaryDropIndex";
    private const string RuntimeLegacyBoss10250RelicItemIndexAttribute = "LegacyBoss10250RelicItemIndex";
    private const string RuntimeLegacyBoss10250RelicDurabilityRollAttribute = "LegacyBoss10250RelicDurabilityRoll";
    private const string RuntimeLegacyBoss10250RelicAttributeCountAttribute = "LegacyBoss10250RelicAttributeCount";
    private const string RuntimeLegacyBoss10250RelicAttributeIndexesAttribute = "LegacyBoss10250RelicAttributeIndexes";
    private const string RuntimeLegacyBoss10250RelicAttributeValuesAttribute = "LegacyBoss10250RelicAttributeValues";
    private const string RuntimeLegacyBoss3970DragonBallCountAttribute = "LegacyBoss3970DragonBallCount";
    private const string RuntimeLegacyBoss3970TalismanIndexesAttribute = "LegacyBoss3970TalismanIndexes";
    private const string RuntimeLegacyBossSoulsPurificationRollModuloAttribute = "LegacyBossSoulsPurificationRollModulo";
    private const string RuntimeLegacyBossSoulsPurificationIndexesAttribute = "LegacyBossSoulsPurificationIndexes";
    private const string RuntimeLegacyBossSoulsAccessoryRollModuloAttribute = "LegacyBossSoulsAccessoryRollModulo";
    private const string RuntimeLegacyBossSoulsAccessoryIndexesAttribute = "LegacyBossSoulsAccessoryIndexes";
    private const string RuntimeLegacyMonsterPhysicalAttackRollAttribute = "LegacyMonsterPhysicalAttackRoll";
    private const string RuntimeLegacyMonsterDodgeRollAttribute = "LegacyMonsterDodgeRoll";
    private const string RuntimeLegacyMonsterDodgeSecondaryRollAttribute = "LegacyMonsterDodgeSecondaryRoll";
    private const string RuntimeLegacyMonsterResponseReflectRollAttribute = "LegacyMonsterResponseReflectRoll";
    private const string RuntimeLegacyMonsterCounterPunchRollAttribute = "LegacyMonsterCounterPunchRoll";
    private const string RuntimeLegacyPhysicalLuckyStrikeRollAttribute = "LegacyPhysicalLuckyStrikeRoll";
    private const string RuntimeLegacyPhysicalRefineryRollAttribute = "LegacyPhysicalRefineryRoll";
    private const string RuntimeLegacyPhysicalDashRollAttribute = "LegacyPhysicalDashRoll";
    private const string RuntimeLegacyMagicLuckyStrikeRollAttribute = "LegacyMagicLuckyStrikeRoll";
    private const string RuntimeLegacyMagicRefineryRollAttribute = "LegacyMagicRefineryRoll";
    private const string RuntimeLegacyOverwhelmRollAttribute = "LegacyOverwhelmRoll";
    private const string RuntimeLegacySolidnessRollAttribute = "LegacySolidnessRoll";
    private const string RuntimeLegacyCounterKillActiveAttribute = "LegacyCounterKillActive";
    internal const string RuntimeLegacyEagleEyeCountDownAttribute = "LegacyEagleEyeCountDown";
    private const string RuntimeLegacyEagleEyeResponseRollAttribute = "LegacyEagleEyeResponseRoll";
    private const string RuntimeLegacyRevengeTailChangeAttribute = "LegacyRevengeTailChange";
    private const string RuntimeLegacyThundercloudSightAttribute = "ThundercloudSight";
    private const string RuntimeLegacyAttackHitAttribute = "AttackHit";
    private const int LegacyThundercloudRandCoordRadius = 5;
    private static readonly (int X, int Y)[] LegacyThundercloudRandCoordOffsets =
        CreateLegacyThundercloudRandCoordOffsets();
    private const string RuntimeLegacyNinjaHellVortexEquippedAttribute = "LegacyNinjaHellVortexEquipped";
    private const string RuntimeLegacyHellVortexRollAttribute = "LegacyHellVortexRoll";

    private const string RuntimeLegacyMonsterDropItemSortAttribute = "LegacyMonsterDropItemSort";
    private const string RuntimeLegacyMonsterDropItemLevelAttribute = "LegacyMonsterDropItemLevel";
    private const string RuntimeLegacyMonsterDropItemQualityAttribute = "LegacyMonsterDropItemQuality";
    private const string RuntimeLegacyBossSpellIndexAttribute = "LegacyBossSpellIndex";
    private const string RuntimeLegacyBossStatusRollAttribute = "LegacyBossStatusRoll";
    private const string RuntimeLegacyBossStatusSecondaryRollAttribute = "LegacyBossStatusSecondaryRoll";
    private const string RuntimeLegacyBossAcalanathaRollAttribute = "LegacyBossAcalanathaRoll";
    private const string RuntimeLegacyGuildWarTerrainMaskAttribute = "TerainMask";
    private const string RuntimeLegacyGuildWarTerrainMaskAliasAttribute = "TerrainMask";
    private const string RuntimeLegacyGuildWarCurrentTerrainMaskAttribute = "CurrentTerrainMask";
    private const string RuntimeLegacyGuildWarLeftGateOpenAttribute = "LegacyGuildWarLeftGateOpen";
    private const string RuntimeLegacyGuildWarRightGateOpenAttribute = "LegacyGuildWarRightGateOpen";
    private const string LegacyGuildWarIllegalJumpMessage = "Illegal jumping over the gates detected.";
    private const string RuntimePkModeAttribute = "PkMode";
    private const string RuntimePendingMessageBoxSecondsAttribute = LegacyRuntimeMessageBoxPrompt.TimeoutSecondsAttribute;
    private const long LegacyAlienWorldMapId = 10_428;
    private const int LegacyMsgNinjaHeaderFieldCount = 20;
    private const int LegacyMsgNinjaItemRecordFieldCount = 5;
    private const int LegacyGouyuTypeDamageValueIndex = 6;

    private static readonly HashSet<uint> LegacyKingdomMissionIds = [35_024, 35_007, 35_025, 35_028, 35_034];
    private const long LegacyAlienWorldLeaveX = 61;
    private const long LegacyAlienWorldLeaveY = 66;
    private const long LegacyTwinCityMapId = 1_002;
    private const long LegacyTwinCityReturnX = 351;
    private const long LegacyTwinCityReturnY = 422;
    private const string LegacyAlienWorldLeavePrompt =
        "Are you sure you want to leave the alien world and return to Twin City?";
    private static readonly int[] LegacyMovementClearedFlags =
    [
        LegacyFocusedFlag,
        LegacyCastPrayFlag,
        LegacyPrayingFlag,
        LegacyMagicDefenderFlag,
    ];

    private static readonly (int X, int Y)[] LegacyMountDeltas =
    [
        (0, 2), (-2, 2), (-2, 0), (-2, -2), (0, -2), (2, -2), (2, 0), (2, 2),
        (-1, 2), (-2, 1), (-2, -1), (-1, -2), (1, -2), (2, -1), (2, 1), (1, 2),
        (-1, 2), (-2, 1), (-2, -1), (-1, -2), (1, -2), (2, -1), (2, 1), (1, 2),
    ];

    private static int s_legacyFloorItemUid = 900_027;
    private static long s_legacyMonsterDeathDynamicId = 44_400_000;
}
