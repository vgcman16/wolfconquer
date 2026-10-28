using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyPromotionTaoistNpcId = 10_000;
    private const string LegacyPromotionTaoistSource = "NpcHandler.PromotionTaoist";
    private const ushort LegacyPromotionTaoistAvatarId = 6;

    private const string LegacyPromotionTaoistAzureShieldSourceRange = "NpcHandler.cs:41657-41696";
    private const string LegacyPromotionTaoistHeavenBladeSourceRange = "NpcHandler.cs:41729-41768";
    private const string LegacyPromotionTaoistSpeedLightningSourceRange = "NpcHandler.cs:41843-41881";
    private const string LegacyPromotionTaoistReviveSourceRange = "NpcHandler.cs:41882-41929";
    private const string LegacyPromotionTaoistVulcanoSourceRange = "NpcHandler.cs:41930-41968";
    private const string LegacyPromotionTaoistLightningSourceRange = "NpcHandler.cs:41969-42007";
    private const string LegacyPromotionTaoistChainBoltSourceRange = "NpcHandler.cs:42008-42035";
    private const string LegacyPromotionTaoistFireOfHellSourceRange = "NpcHandler.cs:42036-42074";
    private const string LegacyPromotionTaoistChainBoltAliasSourceRange = "NpcHandler.cs:42127-42140";
    private const string LegacyPromotionTaoistLightningAliasSourceRange = "NpcHandler.cs:42141-42168";
    private const string LegacyPromotionTaoistBombSourceRange = "NpcHandler.cs:42169-42207";
    private const string LegacyPromotionTaoistFireCircleSourceRange = "NpcHandler.cs:42208-42246";
    private const string LegacyPromotionTaoistFireMeteorSourceRange = "NpcHandler.cs:42247-42285";
    private const string LegacyPromotionTaoistFireRingSourceRange = "NpcHandler.cs:42286-42324";
    private const string LegacyPromotionTaoistTornadoSourceRange = "NpcHandler.cs:42325-42395";
    private const string LegacyPromotionTaoistFireSourceRange = "NpcHandler.cs:42396-42466";
    private const string LegacyPromotionTaoistNectarSourceRange = "NpcHandler.cs:42467-42505";
    private const string LegacyPromotionTaoistEffectMpSourceRange = "NpcHandler.cs:42506-42544";
    private const string LegacyPromotionTaoistPraySourceRange = "NpcHandler.cs:42545-42583";
    private const string LegacyPromotionTaoistInvisibilitySourceRange = "NpcHandler.cs:42584-42622";
    private const string LegacyPromotionTaoistStigmaSourceRange = "NpcHandler.cs:42623-42661";
    private const string LegacyPromotionTaoistMagicShieldSourceRange = "NpcHandler.cs:42662-42700";
    private const string LegacyPromotionTaoistStarOfAccuracySourceRange = "NpcHandler.cs:42701-42739";
    private const string LegacyPromotionTaoistHealingRainSourceRange = "NpcHandler.cs:42740-42778";
    private const string LegacyPromotionTaoistMeditationSourceRange = "NpcHandler.cs:42779-42817";
    private const string LegacyPromotionTaoistCureSourceRange = "NpcHandler.cs:42818-42845";
    private const string LegacyPromotionTaoistThunderSourceRange = "NpcHandler.cs:42846-42873";
    private const string LegacyPromotionTaoistEpicSkillSourceRange = "NpcHandler.cs:41288-41365";
    private const string LegacyPromotionTaoistQuestFinishSourceRange = "NpcHandler.cs:41392-41442";
    private const string LegacyPromotionTaoistEpicQuestTransportSourceRange = "NpcHandler.cs:41461-41475";
    private const string LegacyPromotionTaoistAdvancedSourceRange = "NpcHandler.cs:41477-41615";

    private const byte LegacyPromotionTaoistAdvancedPromotionOption = 53;
    private const byte LegacyPromotionTaoistThunderOption = 20;
    private const byte LegacyPromotionTaoistAzureShieldOption = 51;
    private const byte LegacyPromotionTaoistHeavenBladeOption = 52;
    private const byte LegacyPromotionTaoistCureOption = 21;
    private const byte LegacyPromotionTaoistMeditationOption = 22;
    private const byte LegacyPromotionTaoistFireOption = 23;
    private const byte LegacyPromotionTaoistTornadoOption = 24;
    private const byte LegacyPromotionTaoistFireRingOption = 25;
    private const byte LegacyPromotionTaoistFireMeteorOption = 26;
    private const byte LegacyPromotionTaoistFireCircleOption = 27;
    private const byte LegacyPromotionTaoistBombOption = 28;
    private const byte LegacyPromotionTaoistHealingRainOption = 29;
    private const byte LegacyPromotionTaoistStarOfAccuracyOption = 30;
    private const byte LegacyPromotionTaoistMagicShieldOption = 31;
    private const byte LegacyPromotionTaoistStigmaOption = 32;
    private const byte LegacyPromotionTaoistInvisibilityOption = 33;
    private const byte LegacyPromotionTaoistPrayOption = 34;
    private const byte LegacyPromotionTaoistEffectMpOption = 35;
    private const byte LegacyPromotionTaoistNectarOption = 36;
    private const byte LegacyPromotionTaoistChainBoltOption = 40;
    private const byte LegacyPromotionTaoistLightningOption = 41;
    private const byte LegacyPromotionTaoistVulcanoOption = 42;
    private const byte LegacyPromotionTaoistReviveOption = 43;
    private const byte LegacyPromotionTaoistSpeedLightningOption = 44;
    private const byte LegacyPromotionTaoistFireOfHellOption = 58;
    private const byte LegacyPromotionTaoistChainBoltAliasOption = 70;
    private const byte LegacyPromotionTaoistLightningAliasOption = 71;
    private const byte LegacyPromotionTaoistAuroraLotusOption = 91;
    private const byte LegacyPromotionTaoistBlessingTouchOption = 92;
    private const byte LegacyPromotionTaoistFlameLotusOption = 93;
    private const byte LegacyPromotionTaoistSearingTouchOption = 94;
    private const byte LegacyPromotionTaoistQuestFinishOption = 111;
    private const byte LegacyPromotionTaoistEpicQuestTransportOption = 191;

    private const ushort LegacyPromotionTaoistThunderSpellId = 1_000;
    private const ushort LegacyPromotionTaoistAzureShieldSpellId = 10_312;
    private const ushort LegacyPromotionTaoistHeavenBladeSpellId = 10_310;
    private const ushort LegacyPromotionTaoistCureSpellId = 1_005;
    private const ushort LegacyPromotionTaoistMeditationSpellId = 1_195;
    private const ushort LegacyPromotionTaoistFireSpellId = 1_001;
    private const ushort LegacyPromotionTaoistTornadoSpellId = 1_002;
    private const ushort LegacyPromotionTaoistFireRingSpellId = 1_150;
    private const ushort LegacyPromotionTaoistFireMeteorSpellId = 1_180;
    private const ushort LegacyPromotionTaoistFireCircleSpellId = 1_120;
    private const ushort LegacyPromotionTaoistBombSpellId = 1_160;
    private const ushort LegacyPromotionTaoistHealingRainSpellId = 1_055;
    private const ushort LegacyPromotionTaoistStarOfAccuracySpellId = 1_085;
    private const ushort LegacyPromotionTaoistMagicShieldSpellId = 1_090;
    private const ushort LegacyPromotionTaoistStigmaSpellId = 1_095;
    private const ushort LegacyPromotionTaoistInvisibilitySpellId = 1_075;
    private const ushort LegacyPromotionTaoistPraySpellId = 1_100;
    private const ushort LegacyPromotionTaoistEffectMpSpellId = 1_175;
    private const ushort LegacyPromotionTaoistNectarSpellId = 1_170;
    private const ushort LegacyPromotionTaoistChainBoltSpellId = 10_309;
    private const ushort LegacyPromotionTaoistLightningSpellId = 1_010;
    private const ushort LegacyPromotionTaoistVulcanoSpellId = 1_125;
    private const ushort LegacyPromotionTaoistReviveSpellId = 1_050;
    private const ushort LegacyPromotionTaoistSpeedLightningSpellId = 5_001;
    private const ushort LegacyPromotionTaoistFireOfHellSpellId = 1_165;
    private const ushort LegacyPromotionTaoistAuroraLotusSpellId = 12_370;
    private const ushort LegacyPromotionTaoistBlessingTouchSpellId = 12_390;
    private const ushort LegacyPromotionTaoistFlameLotusSpellId = 12_380;
    private const ushort LegacyPromotionTaoistSearingTouchSpellId = 12_400;

    private const long LegacyPromotionTaoistNoSourceLevelGate = 0;
    private const long LegacyPromotionTaoistEpicSkillLevelGate = 15;
    private const long LegacyPromotionTaoistEpicSkillCost = 2_150;
    private const long LegacyPromotionTaoistLightningLevelGate = 3;
    private const long LegacyPromotionTaoistLevel40Gate = 40;
    private const long LegacyPromotionTaoistMeditationLevelGate = 44;
    private const long LegacyPromotionTaoistStarOfAccuracyLevelGate = 45;
    private const long LegacyPromotionTaoistMagicShieldLevelGate = 50;
    private const long LegacyPromotionTaoistFireMeteorLevelGate = 52;
    private const long LegacyPromotionTaoistFireRingLevelGate = 55;
    private const long LegacyPromotionTaoistStigmaLevelGate = 55;
    private const long LegacyPromotionTaoistInvisibilityLevelGate = 60;
    private const long LegacyPromotionTaoistFireCircleLevelGate = 65;
    private const long LegacyPromotionTaoistPrayLevelGate = 70;
    private const long LegacyPromotionTaoistEffectMpLevelGate = 81;
    private const long LegacyPromotionTaoistBombLevelGate = 82;
    private const long LegacyPromotionTaoistTornadoLevelGate = 92;
    private const long LegacyPromotionTaoistFireOfHellLevelGate = 93;
    private const long LegacyPromotionTaoistNectarLevelGate = 94;
    private const long LegacyPromotionTaoistEpicQuestLevelGate = 70;
    private const long LegacyPromotionTaoistEpicQuestRebornGate = 2;
    private const long LegacyPromotionTaoistEpicQuestMapId = 3_933;
    private const long LegacyPromotionTaoistEpicQuestX = 84;
    private const long LegacyPromotionTaoistEpicQuestY = 84;
    private const long LegacyPromotionWaterTaoistRankTwoClass = 13_002;
    private const long LegacyPromotionWaterTaoistRankThreeClass = 13_003;
    private const long LegacyPromotionWaterTaoistRankFourClass = 13_004;
    private const long LegacyPromotionFireTaoistRankTwoClass = 14_002;
    private const long LegacyPromotionFireTaoistRankThreeClass = 14_003;
    private const long LegacyPromotionFireTaoistRankFourClass = 14_004;
    private const long LegacyPromotionTaoistRankTwoLevelGate = 70;
    private const long LegacyPromotionTaoistRankThreeLevelGate = 100;
    private const long LegacyPromotionTaoistRankFourLevelGate = 110;

    private static ValueTask<bool> TryHandleLegacyTaoistPromotionSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return optionId switch
        {
            LegacyPromotionTaoistAdvancedPromotionOption => TryHandleLegacyTaoistAdvancedPromotionAsync(
                context,
                session,
                optionId,
                cancellationToken),
            LegacyPromotionTaoistAuroraLotusOption => TryLearnLegacyTaoistEpicPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistAuroraLotusSpellId,
                "AuroraLotus",
                cancellationToken),
            LegacyPromotionTaoistBlessingTouchOption => TryLearnLegacyTaoistEpicPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistBlessingTouchSpellId,
                "BlessingTouch",
                cancellationToken),
            LegacyPromotionTaoistFlameLotusOption => TryLearnLegacyTaoistEpicPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistFlameLotusSpellId,
                "FlameLotus",
                cancellationToken),
            LegacyPromotionTaoistSearingTouchOption => TryLearnLegacyTaoistEpicPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistSearingTouchSpellId,
                "SearingTouch",
                cancellationToken),
            LegacyPromotionTaoistQuestFinishOption => TryHandleLegacyTaoistQuestFinishOptionAsync(
                context,
                session,
                optionId,
                cancellationToken),
            LegacyPromotionTaoistEpicQuestTransportOption => TryHandleLegacyTaoistEpicQuestTransportAsync(
                context,
                session,
                optionId,
                cancellationToken),
            LegacyPromotionTaoistAzureShieldOption => TryHandleLegacyTaoistPureSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistAzureShieldSpellId,
                "AzureShield",
                LegacyPromotionTaoistAzureShieldSourceRange,
                IsLegacyWaterTaoistClass,
                requiredBranchName: "Water Taoist",
                rebornGateText: "Only pure Fire Taoists, who have trained as Water Taoists for all 3 of their lives, can learn the Pure Skill, AzureShield.",
                comboGateText: "Only pure water Taoists, who have trained as Water Taoists for all 3 of their lives, can learn the Pure Skill, AzureShield.",
                comboGateReason: "source pure Water Taoist combo gate rejected.",
                cancellationToken),
            LegacyPromotionTaoistHeavenBladeOption => TryHandleLegacyTaoistPureSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistHeavenBladeSpellId,
                "HeavenBlade",
                LegacyPromotionTaoistHeavenBladeSourceRange,
                IsLegacyFireTaoistClass,
                requiredBranchName: "Fire Taoist",
                rebornGateText: "Only pure Fire Taoists, who have trained as Fire Taoists for all 3 of their lives, can learn the Pure Skill, Heaven Blade.",
                comboGateText: "Only pure Fire Taoists, who have trained as Fire Taoists for all 3 of their lives, can learn the Pure Skill, Heaven Blade.",
                comboGateReason: "source pure Fire Taoist combo gate rejected.",
                cancellationToken),
            LegacyPromotionTaoistThunderOption => TryLearnLegacyTaoistPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistThunderSpellId,
                "Thunder",
                LegacyPromotionTaoistNoSourceLevelGate,
                LegacyPromotionTaoistThunderSourceRange,
                cancellationToken),
            LegacyPromotionTaoistCureOption => TryLearnLegacyTaoistPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistCureSpellId,
                "Cure",
                LegacyPromotionTaoistNoSourceLevelGate,
                LegacyPromotionTaoistCureSourceRange,
                cancellationToken),
            LegacyPromotionTaoistMeditationOption => TryLearnLegacyTaoistPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistMeditationSpellId,
                "Meditation",
                LegacyPromotionTaoistMeditationLevelGate,
                LegacyPromotionTaoistMeditationSourceRange,
                cancellationToken),
            LegacyPromotionTaoistFireOption => TryLearnLegacyTaoistPrerequisiteSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistFireSpellId,
                "Fire",
                LegacyPromotionTaoistLevel40Gate,
                LegacyPromotionTaoistFireSourceRange,
                LegacyPromotionTaoistThunderSpellId,
                requiredPrerequisiteLevel: 4,
                cancellationToken),
            LegacyPromotionTaoistTornadoOption => TryLearnLegacyTaoistPrerequisiteSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistTornadoSpellId,
                "Tornado",
                LegacyPromotionTaoistTornadoLevelGate,
                LegacyPromotionTaoistTornadoSourceRange,
                LegacyPromotionTaoistFireSpellId,
                requiredPrerequisiteLevel: 3,
                cancellationToken),
            LegacyPromotionTaoistFireRingOption => TryLearnLegacyTaoistSourceLevelPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistFireRingSpellId,
                "FireRing",
                LegacyPromotionTaoistFireRingLevelGate,
                LegacyPromotionTaoistFireRingSourceRange,
                sourceRequiredLevelText: "55",
                cancellationToken),
            LegacyPromotionTaoistFireMeteorOption => TryLearnLegacyTaoistSourceLevelPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistFireMeteorSpellId,
                "FireMeteor",
                LegacyPromotionTaoistFireMeteorLevelGate,
                LegacyPromotionTaoistFireMeteorSourceRange,
                sourceRequiredLevelText: "52",
                cancellationToken),
            LegacyPromotionTaoistFireCircleOption => TryLearnLegacyTaoistSourceLevelPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistFireCircleSpellId,
                "FireCircle",
                LegacyPromotionTaoistFireCircleLevelGate,
                LegacyPromotionTaoistFireCircleSourceRange,
                sourceRequiredLevelText: "65",
                cancellationToken),
            LegacyPromotionTaoistBombOption => TryLearnLegacyTaoistSourceLevelPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistBombSpellId,
                "Bomb",
                LegacyPromotionTaoistBombLevelGate,
                LegacyPromotionTaoistBombSourceRange,
                sourceRequiredLevelText: "82",
                cancellationToken),
            LegacyPromotionTaoistHealingRainOption => TryLearnLegacyTaoistPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistHealingRainSpellId,
                "HealingRain",
                LegacyPromotionTaoistLevel40Gate,
                LegacyPromotionTaoistHealingRainSourceRange,
                cancellationToken),
            LegacyPromotionTaoistStarOfAccuracyOption => TryLearnLegacyTaoistPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistStarOfAccuracySpellId,
                "StarofAccuracy",
                LegacyPromotionTaoistStarOfAccuracyLevelGate,
                LegacyPromotionTaoistStarOfAccuracySourceRange,
                cancellationToken),
            LegacyPromotionTaoistMagicShieldOption => TryLearnLegacyTaoistPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistMagicShieldSpellId,
                "MagicShield",
                LegacyPromotionTaoistMagicShieldLevelGate,
                LegacyPromotionTaoistMagicShieldSourceRange,
                cancellationToken),
            LegacyPromotionTaoistStigmaOption => TryLearnLegacyTaoistPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistStigmaSpellId,
                "Stigma",
                LegacyPromotionTaoistStigmaLevelGate,
                LegacyPromotionTaoistStigmaSourceRange,
                cancellationToken),
            LegacyPromotionTaoistInvisibilityOption => TryLearnLegacyTaoistPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistInvisibilitySpellId,
                "Invisibility",
                LegacyPromotionTaoistInvisibilityLevelGate,
                LegacyPromotionTaoistInvisibilitySourceRange,
                cancellationToken),
            LegacyPromotionTaoistPrayOption => TryLearnLegacyTaoistPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistPraySpellId,
                "Pray",
                LegacyPromotionTaoistPrayLevelGate,
                LegacyPromotionTaoistPraySourceRange,
                cancellationToken),
            LegacyPromotionTaoistEffectMpOption => TryLearnLegacyTaoistPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistEffectMpSpellId,
                "EffectMP",
                LegacyPromotionTaoistEffectMpLevelGate,
                LegacyPromotionTaoistEffectMpSourceRange,
                cancellationToken),
            LegacyPromotionTaoistNectarOption => TryLearnLegacyTaoistPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistNectarSpellId,
                "Nectar",
                LegacyPromotionTaoistNectarLevelGate,
                LegacyPromotionTaoistNectarSourceRange,
                cancellationToken),
            LegacyPromotionTaoistChainBoltOption => TryLearnLegacyTaoistPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistChainBoltSpellId,
                "ChainBolt",
                LegacyPromotionTaoistNoSourceLevelGate,
                LegacyPromotionTaoistChainBoltSourceRange,
                cancellationToken),
            LegacyPromotionTaoistLightningOption => TryLearnLegacyTaoistPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistLightningSpellId,
                "Lightning",
                LegacyPromotionTaoistLightningLevelGate,
                LegacyPromotionTaoistLightningSourceRange,
                cancellationToken),
            LegacyPromotionTaoistVulcanoOption => TryLearnLegacyTaoistPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistVulcanoSpellId,
                "Vulcano",
                LegacyPromotionTaoistLevel40Gate,
                LegacyPromotionTaoistVulcanoSourceRange,
                cancellationToken),
            LegacyPromotionTaoistReviveOption => TryLearnLegacyTaoistWaterPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistReviveSpellId,
                "Revive",
                LegacyPromotionTaoistLevel40Gate,
                LegacyPromotionTaoistReviveSourceRange,
                cancellationToken),
            LegacyPromotionTaoistSpeedLightningOption => TryLearnLegacyTaoistPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistSpeedLightningSpellId,
                "SpeedLightning",
                LegacyPromotionTaoistLevel40Gate,
                LegacyPromotionTaoistSpeedLightningSourceRange,
                cancellationToken),
            LegacyPromotionTaoistFireOfHellOption => TryLearnLegacyTaoistSourceLevelPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistFireOfHellSpellId,
                "FireofHell",
                LegacyPromotionTaoistFireOfHellLevelGate,
                LegacyPromotionTaoistFireOfHellSourceRange,
                sourceRequiredLevelText: "40",
                cancellationToken),
            LegacyPromotionTaoistChainBoltAliasOption => TryLearnLegacyTaoistPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistChainBoltSpellId,
                "ChainBolt",
                LegacyPromotionTaoistNoSourceLevelGate,
                LegacyPromotionTaoistChainBoltAliasSourceRange,
                cancellationToken),
            LegacyPromotionTaoistLightningAliasOption => TryLearnLegacyTaoistPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionTaoistLightningSpellId,
                "Lightning",
                LegacyPromotionTaoistNoSourceLevelGate,
                LegacyPromotionTaoistLightningAliasSourceRange,
                cancellationToken),
            _ => ValueTask.FromResult(false),
        };
    }

    private static async ValueTask<bool> TryHandleLegacyTaoistQuestFinishOptionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var nextOption = session.Character.Class switch
        {
            10_000 => (byte)1,
            10_001 => (byte)3,
            _ => LegacyPromotionTaoistAdvancedPromotionOption,
        };
        var optionText = session.Character.Class == 10_000
            ? "I~want~to~get~promoted"
            : "I~want~to~get~promoted.";

        var updatedSession = await TryApplyLegacyPromotionQuestFinishRewardAsync(
                context,
                session,
                optionId,
                new LegacyPromotionQuestFinishRewardSpec(
                    LegacyPromotionTaoistSource,
                    LegacyPromotionTaoistQuestFinishSourceRange,
                    "NpcHandler.cs:41215",
                    "GetFinishQuest(2001, class, 1004)",
                    "PromotionTaoist",
                    2_001,
                    LegacyPromotionQuestFinishDefaultRewardItemId),
                cancellationToken)
            .ConfigureAwait(false);

        await SendLegacyNpcDialogAsync(
                context with { Session = updatedSession },
                LegacyPromotionTaoistSource,
                [
                    "Wow, look at you, you`ll be another hero in the world of Conquer! Here, you can learn some skills and get promoted! Do you want to learn more?",
                    "~get promoted here to start your life. Would you like to learn something about your class promotion and quests?.",
                ],
                [new LegacyNpcDialogOption(optionText, nextOption)],
                avatarId: 259,
                cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyTaoistAdvancedPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        switch (session.Character.Class)
        {
            case LegacyPromotionWaterTaoistRankTwoClass:
            case LegacyPromotionFireTaoistRankTwoClass:
                return await TryHandleLegacyTaoistAdvancedItemPromotionAsync(
                        context,
                        session,
                        optionId,
                        LegacyPromotionTaoistRankTwoLevelGate,
                        LegacyPromotionEmeraldItemId,
                        "Emerald",
                        "I'm sorry but you need to have 1 Emerald at your inventory , to get one go and kill some HillMonsters ( Desert City ) to get one .",
                        "Excellent! You`ve learned ways of the Taoist. Do you feel a different energy in your body? That`s stronger power and battle prowess!",
                        cancellationToken)
                    .ConfigureAwait(false);

            case LegacyPromotionWaterTaoistRankThreeClass:
            case LegacyPromotionFireTaoistRankThreeClass:
                return await TryHandleLegacyTaoistAdvancedItemPromotionAsync(
                        context,
                        session,
                        optionId,
                        LegacyPromotionTaoistRankThreeLevelGate,
                        LegacyPromotionMeteorItemId,
                        "Meteor",
                        "I'm sorry but you need to have 1 Meteor at your inventory.",
                        "Excellent! Do you feel a different energy in your body? That`s stronger power and battle prowess!",
                        cancellationToken)
                    .ConfigureAwait(false);

            case LegacyPromotionWaterTaoistRankFourClass:
            case LegacyPromotionFireTaoistRankFourClass:
                return await TryHandleLegacyTaoistAdvancedMoonBoxPromotionAsync(
                        context,
                        session,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);

            default:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyPromotionTaoistSource,
                        ["You cannot be promoted anymore. You have mastered your class"],
                        [new LegacyNpcDialogOption("Thank~you,~Master.", LegacyNpcClosedOption)],
                        LegacyPromotionTaoistAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                await LogLegacyTaoistAdvancedPromotionSkippedAsync(
                        context,
                        optionId,
                        $"class={session.Character.Class}; source class gate rejected.")
                    .ConfigureAwait(false);
                return true;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyTaoistAdvancedItemPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        long requiredLevel,
        uint requiredItemId,
        string requiredItemName,
        string missingItemText,
        string successText,
        CancellationToken cancellationToken)
    {
        if (session.Character.Level < requiredLevel)
        {
            await SendLegacyTaoistAdvancedLevelGateAsync(
                    context,
                    session,
                    optionId,
                    requiredLevel,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!HasLegacyInventoryStackCount(session.PlayerItems, requiredItemId, 1))
        {
            await SendLegacyTaoistAdvancedMissingItemAsync(
                    context,
                    session,
                    optionId,
                    requiredItemId,
                    requiredItemName,
                    missingItemText,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var mutationFrames = new List<byte[]>();
        var updatedItems = ConsumeLegacyInventoryStack(
            session.PlayerItems,
            requiredItemId,
            1,
            mutationFrames);
        var updatedSession = await ApplyLegacyPromotionMasterItemsAsync(
                context,
                session,
                updatedItems,
                optionId,
                LegacyPromotionTaoistSource,
                LegacyPromotionTaoistAdvancedSourceRange,
                $"consumedItemId={requiredItemId} consumedCount=1")
            .ConfigureAwait(false);
        var updatedContext = context with { Session = updatedSession };
        await SendLegacyPromotionMasterItemFramesAsync(updatedContext, mutationFrames, cancellationToken)
            .ConfigureAwait(false);

        updatedSession = await PromoteLegacyTaoistAdvancedClassAsync(
                updatedContext,
                updatedSession,
                optionId)
            .ConfigureAwait(false);
        updatedContext = context with { Session = updatedSession };
        await SendLegacyTaoistAdvancedSuccessAsync(updatedContext, successText, cancellationToken)
            .ConfigureAwait(false);
        await LogLegacyTaoistAdvancedPromotionHandledAsync(
                updatedContext,
                optionId,
                $"consumedItemId={requiredItemId} consumedCount=1 grantedItemId=0")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyTaoistAdvancedMoonBoxPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (session.Character.Level < LegacyPromotionTaoistRankFourLevelGate)
        {
            await SendLegacyTaoistAdvancedLevelGateAsync(
                    context,
                    session,
                    optionId,
                    LegacyPromotionTaoistRankFourLevelGate,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!HasLegacyInventoryStackCount(session.PlayerItems, LegacyPromotionMoonBoxItemId, 1))
        {
            await SendLegacyTaoistAdvancedMissingItemAsync(
                    context,
                    session,
                    optionId,
                    LegacyPromotionMoonBoxItemId,
                    "MoonBox",
                    "I'm sorry but you need to have 1 MoonBox at your inventory.",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!TryReadItemType(context.WorldData, LegacyPromotionDragonBallItemId, out var dragonBallType))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc PromotionTaoist advanced promotion missing DragonBall item type " +
                    $"{FormatSession(context)} source={LegacyPromotionTaoistSource} " +
                    $"sourceRange={LegacyPromotionTaoistAdvancedSourceRange} option={optionId} " +
                    $"rewardItemId={LegacyPromotionDragonBallItemId}; promotion skipped to avoid consuming MoonBox without reward.")
                .ConfigureAwait(false);
            return true;
        }

        var mutationFrames = new List<byte[]>();
        var updatedItems = ConsumeLegacyInventoryStack(
            session.PlayerItems,
            LegacyPromotionMoonBoxItemId,
            1,
            mutationFrames);
        var dragonBall = CreatePurchasedItems(updatedItems, dragonBallType, 1)[0];
        updatedItems = updatedItems with
        {
            InventoryItems = updatedItems.InventoryItems.Concat([dragonBall]).OrderBy(item => item.Uid).ToArray(),
        };

        var updatedSession = await ApplyLegacyPromotionMasterItemsAsync(
                context,
                session,
                updatedItems,
                optionId,
                LegacyPromotionTaoistSource,
                LegacyPromotionTaoistAdvancedSourceRange,
                $"consumedItemId={LegacyPromotionMoonBoxItemId} consumedCount=1 grantedItemId={LegacyPromotionDragonBallItemId} grantedCount=1")
            .ConfigureAwait(false);
        var updatedContext = context with { Session = updatedSession };
        await SendLegacyPromotionMasterItemFramesAsync(updatedContext, mutationFrames, cancellationToken)
            .ConfigureAwait(false);
        await updatedContext.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(dragonBall).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await updatedContext.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk("You received DragonBall.").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        updatedSession = await PromoteLegacyTaoistAdvancedClassAsync(
                updatedContext,
                updatedSession,
                optionId)
            .ConfigureAwait(false);
        updatedContext = context with { Session = updatedSession };
        await SendLegacyTaoistAdvancedSuccessAsync(
                updatedContext,
                "Excellent!Do you feel a different energy in your body? That`s stronger power and battle prowess!",
                cancellationToken)
            .ConfigureAwait(false);
        await LogLegacyTaoistAdvancedPromotionHandledAsync(
                updatedContext,
                optionId,
                $"consumedItemId={LegacyPromotionMoonBoxItemId} consumedCount=1 grantedItemId={LegacyPromotionDragonBallItemId} grantedCount=1")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<WorldSession> PromoteLegacyTaoistAdvancedClassAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId)
    {
        var updatedCharacter = session.Character with
        {
            Class = checked(session.Character.Class + 1),
        };
        return await ApplyLegacyPromotionCharacterAsync(
                context,
                session,
                updatedCharacter,
                optionId,
                LegacyPromotionTaoistSource,
                LegacyPromotionTaoistAdvancedSourceRange)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyTaoistAdvancedLevelGateAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        long requiredLevel,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionTaoistSource,
                [$"There is much that you must learn before you can accept my training. Slaughter a legion of monsters first! [Level {requiredLevel}+]"],
                [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
                LegacyPromotionTaoistAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await LogLegacyTaoistAdvancedPromotionSkippedAsync(
                context,
                optionId,
                $"level={session.Character.Level} requiredLevel={requiredLevel}; source level gate rejected.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyTaoistAdvancedMissingItemAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        uint requiredItemId,
        string requiredItemName,
        string missingItemText,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionTaoistSource,
                [missingItemText],
                [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
                LegacyPromotionTaoistAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await LogLegacyTaoistAdvancedPromotionSkippedAsync(
                context,
                optionId,
                $"class={session.Character.Class} itemId={requiredItemId} itemName={requiredItemName}; source item gate rejected.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyTaoistAdvancedSuccessAsync(
        WorldPacketHandlerContext context,
        string successText,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionTaoistSource,
                [successText],
                [new LegacyNpcDialogOption("Thank~you,~Master.", LegacyNpcClosedOption)],
                LegacyPromotionTaoistAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask LogLegacyTaoistAdvancedPromotionHandledAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        string mutationSummary)
    {
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionTaoist advanced promotion " +
                $"{FormatSession(context)} source={LegacyPromotionTaoistSource} " +
                $"sourceRange={LegacyPromotionTaoistAdvancedSourceRange} option={optionId} {mutationSummary}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask LogLegacyTaoistAdvancedPromotionSkippedAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc PromotionTaoist advanced promotion " +
                $"{FormatSession(context)} source={LegacyPromotionTaoistSource} " +
                $"sourceRange={LegacyPromotionTaoistAdvancedSourceRange} option={optionId} {reason}")
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryHandleLegacyTaoistPureSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        ushort spellId,
        string spellName,
        string sourceRange,
        Func<long, bool> isRequiredBranchClass,
        string requiredBranchName,
        string rebornGateText,
        string comboGateText,
        string comboGateReason,
        CancellationToken cancellationToken)
    {
        if (!isRequiredBranchClass(session.Character.Class))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionTaoist pure skill " +
                    $"{FormatSession(context)} source={LegacyPromotionTaoistSource} " +
                    $"sourceRange={sourceRange} option={optionId} spellId={spellId} " +
                    $"class={session.Character.Class} requiredBranch=\"{requiredBranchName}\"; " +
                    "source current class gate broke without dialog.")
                .ConfigureAwait(false);
            return true;
        }

        if (session.Character.Reborn < 2)
        {
            await SendLegacyTaoistPureSkillGateAsync(
                    context,
                    session,
                    optionId,
                    sourceRange,
                    rebornGateText,
                    "source reborn gate rejected.",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!isRequiredBranchClass(session.Character.FirstClass) ||
            !isRequiredBranchClass(session.Character.SecondClass))
        {
            await SendLegacyTaoistPureSkillGateAsync(
                    context,
                    session,
                    optionId,
                    sourceRange,
                    comboGateText,
                    comboGateReason,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (session.Character.Level < LegacyPromotionTaoistLevel40Gate)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionTaoistSource,
                    ["Sorry. I can`t teach you this skill until you reach level 40 and get promoted."],
                    [new LegacyNpcDialogOption("Okay.~I~see..", LegacyNpcClosedOption)],
                    LegacyPromotionTaoistAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionTaoist pure skill " +
                    $"{FormatSession(context)} source={LegacyPromotionTaoistSource} " +
                    $"sourceRange={sourceRange} option={optionId} spellId={spellId} " +
                    $"level={session.Character.Level} requiredLevel={LegacyPromotionTaoistLevel40Gate}; " +
                    "source level gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (session.PlayerSpells.ContainsSpell(spellId))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionTaoistSource,
                    [$"You`ve learned {spellName}!"],
                    [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                    LegacyPromotionTaoistAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionTaoist pure skill " +
                    $"{FormatSession(context)} source={LegacyPromotionTaoistSource} " +
                    $"sourceRange={sourceRange} option={optionId} spellId={spellId}; already learned.")
                .ConfigureAwait(false);
            return true;
        }

        var learned = CreateLegacyPromotionSpellEntry(spellId);
        var updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(session.PlayerSpells, learned);
        var updatedSession = await ApplyLegacyPromotionMasterSpellsAsync(
                context,
                session,
                updatedSpells,
                optionId,
                LegacyPromotionTaoistSource,
                sourceRange,
                spellId.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ConfigureAwait(false);

        var updatedContext = context with { Session = updatedSession };
        await SendLegacyPromotionMasterLearnedSpellAsync(
                updatedContext,
                LegacyPromotionTaoistSource,
                LegacyPromotionTaoistAvatarId,
                learned,
                spellName,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionTaoist pure skill " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionTaoistSource} " +
                $"sourceRange={sourceRange} option={optionId} spellId={spellId}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryLearnLegacyTaoistEpicPromotionSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        ushort spellId,
        string spellName,
        CancellationToken cancellationToken)
    {
        if (session.PlayerSpells.ContainsSpell(spellId))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionTaoistSource,
                    ["Taoist Master has already acquired this skill. Go practice and feel its power."],
                    [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionTaoist epic skill " +
                    $"{FormatSession(context)} source={LegacyPromotionTaoistSource} " +
                    $"sourceRange={LegacyPromotionTaoistEpicSkillSourceRange} option={optionId} " +
                    $"spellId={spellId}; already learned.")
                .ConfigureAwait(false);
            return true;
        }

        if (session.Character.Level < LegacyPromotionTaoistEpicSkillLevelGate)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionTaoistSource,
                    [$"\tThe study of [{spellName}] requires you to reach Level 15."],
                    [new LegacyNpcDialogOption("I`ll~find~you~at~Lv.15!", LegacyNpcClosedOption)],
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionTaoist epic skill " +
                    $"{FormatSession(context)} source={LegacyPromotionTaoistSource} " +
                    $"sourceRange={LegacyPromotionTaoistEpicSkillSourceRange} option={optionId} " +
                    $"spellId={spellId} level={session.Character.Level} " +
                    $"requiredLevel={LegacyPromotionTaoistEpicSkillLevelGate}; source level gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (session.Character.ConquerPoints < LegacyPromotionTaoistEpicSkillCost)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionTaoistSource,
                    ["Come~on,~you~don`t~have~2,150~CPs~with~you."],
                    [new LegacyNpcDialogOption("Sorry...", LegacyNpcClosedOption)],
                    avatarId: 5,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionTaoist epic skill " +
                    $"{FormatSession(context)} source={LegacyPromotionTaoistSource} " +
                    $"sourceRange={LegacyPromotionTaoistEpicSkillSourceRange} option={optionId} " +
                    $"spellId={spellId} conquerPoints={session.Character.ConquerPoints} " +
                    $"requiredConquerPoints={LegacyPromotionTaoistEpicSkillCost}; insufficient CP.")
                .ConfigureAwait(false);
            return true;
        }

        var learned = CreateLegacyPromotionSpellEntry(spellId);
        if (!TryCommitLegacyPaidSpellLearning(context, session, learned, LegacySpellLearningCurrency.ConquerPoints,
                LegacyPromotionTaoistEpicSkillCost, LegacyPromotionTaoistEpicSkillLevelGate, requiresTrojanEpicWeapon: false, cancellationToken,
                out var paid, out var reason))
        {
            await LogLegacyPaidSpellRefusalAsync(context, reason).ConfigureAwait(false);
            return true;
        }
        var updatedSession = paid!.Updated;
        var updatedContext = context with { Session = updatedSession };
        if (!await paid.PublishGrantAsync(cancellationToken).ConfigureAwait(false)) return true;
        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyPromotionTaoistSource,
                [$"\tYou`ve acquired [{spellName}]. Go practice and feel its power."],
                [new LegacyNpcDialogOption("Thanks!", LegacyNpcClosedOption)],
                avatarId: null,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionTaoist epic skill " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionTaoistSource} " +
                $"sourceRange={LegacyPromotionTaoistEpicSkillSourceRange} option={optionId} " +
                $"spellId={spellId} costConquerPoints={LegacyPromotionTaoistEpicSkillCost}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyTaoistEpicQuestTransportAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (session.Character.Level >= LegacyPromotionTaoistEpicQuestLevelGate &&
            session.Character.Reborn >= LegacyPromotionTaoistEpicQuestRebornGate)
        {
            await SendLegacyNpcTeleportAsync(
                    context,
                    session,
                    LegacyPromotionTaoistNpcId,
                    optionId,
                    new LegacyNpcTeleportDestination(
                        LegacyPromotionTaoistEpicQuestMapId,
                        LegacyPromotionTaoistEpicQuestX,
                        LegacyPromotionTaoistEpicQuestY,
                        LegacyPromotionTaoistSource),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyNpc PromotionTaoist epic quest transport " +
                    $"{FormatSession(context)} source={LegacyPromotionTaoistSource} " +
                    $"sourceRange={LegacyPromotionTaoistEpicQuestTransportSourceRange} option={optionId} " +
                    $"level={session.Character.Level} reborn={session.Character.Reborn} " +
                    $"map={LegacyPromotionTaoistEpicQuestMapId} x={LegacyPromotionTaoistEpicQuestX} y={LegacyPromotionTaoistEpicQuestY}.")
                .ConfigureAwait(false);
            return true;
        }

        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionTaoistSource,
                ["Please come back when you will be 2nd Reborn and with a higher level than 70."],
                [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                LegacyPromotionTaoistAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc PromotionTaoist epic quest transport " +
                $"{FormatSession(context)} source={LegacyPromotionTaoistSource} " +
                $"sourceRange={LegacyPromotionTaoistEpicQuestTransportSourceRange} option={optionId} " +
                $"level={session.Character.Level} requiredLevel={LegacyPromotionTaoistEpicQuestLevelGate} " +
                $"reborn={session.Character.Reborn} requiredReborn={LegacyPromotionTaoistEpicQuestRebornGate}; source gate rejected.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask SendLegacyTaoistPureSkillGateAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string sourceRange,
        string text,
        string reason,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionTaoistSource,
                [text],
                [new LegacyNpcDialogOption("Ok~I~see.", LegacyNpcClosedOption)],
                LegacyPromotionTaoistAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc PromotionTaoist pure skill " +
                $"{FormatSession(context)} source={LegacyPromotionTaoistSource} " +
                $"sourceRange={sourceRange} option={optionId} reborn={session.Character.Reborn} " +
                $"firstClass={session.Character.FirstClass} secondClass={session.Character.SecondClass}; {reason}")
            .ConfigureAwait(false);
    }

    private static ValueTask<bool> TryLearnLegacyTaoistPromotionSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        ushort spellId,
        string spellName,
        long requiredLevel,
        string sourceRange,
        CancellationToken cancellationToken)
    {
        return TryLearnLegacyPromotionMasterSkillAsync(
            context,
            session,
            optionId,
            spellId,
            spellName,
            requiredLevel,
            LegacyPromotionTaoistSource,
            sourceRange,
            LegacyPromotionTaoistAvatarId,
            cancellationToken);
    }

    private static async ValueTask<bool> TryLearnLegacyTaoistSourceLevelPromotionSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        ushort spellId,
        string spellName,
        long requiredLevel,
        string sourceRange,
        string sourceRequiredLevelText,
        CancellationToken cancellationToken)
    {
        if (session.Character.Level < requiredLevel)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionTaoistSource,
                    [$"Sorry. I can`t teach you this skill until you reach level {sourceRequiredLevelText} and get promoted."],
                    [new LegacyNpcDialogOption("Okay.~I~see..", LegacyNpcClosedOption)],
                    LegacyPromotionTaoistAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionTaoist skill " +
                    $"{FormatSession(context)} source={LegacyPromotionTaoistSource} " +
                    $"sourceRange={sourceRange} option={optionId} spellId={spellId} " +
                    $"level={session.Character.Level} requiredLevel={requiredLevel}; source level gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        return await TryLearnLegacyTaoistPromotionSkillAsync(
                context,
                session,
                optionId,
                spellId,
                spellName,
                LegacyPromotionTaoistNoSourceLevelGate,
                sourceRange,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryLearnLegacyTaoistPrerequisiteSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        ushort spellId,
        string spellName,
        long requiredLevel,
        string sourceRange,
        ushort prerequisiteSpellId,
        ushort requiredPrerequisiteLevel,
        CancellationToken cancellationToken)
    {
        if (session.Character.Level >= requiredLevel &&
            !HasLegacyTaoistPromotionSpellAtLevel(session, prerequisiteSpellId, requiredPrerequisiteLevel))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionTaoistSource,
                    ["Sorry. but you need thunder level 4."],
                    [new LegacyNpcDialogOption("Okay.~I~see..", LegacyNpcClosedOption)],
                    LegacyPromotionTaoistAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionTaoist skill " +
                    $"{FormatSession(context)} source={LegacyPromotionTaoistSource} " +
                    $"sourceRange={sourceRange} option={optionId} spellId={spellId} " +
                    $"prerequisiteSpellId={prerequisiteSpellId} requiredPrerequisiteLevel={requiredPrerequisiteLevel}; " +
                    "source prerequisite gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        return await TryLearnLegacyTaoistPromotionSkillAsync(
                context,
                session,
                optionId,
                spellId,
                spellName,
                requiredLevel,
                sourceRange,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryLearnLegacyTaoistWaterPromotionSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        ushort spellId,
        string spellName,
        long requiredLevel,
        string sourceRange,
        CancellationToken cancellationToken)
    {
        if (!IsLegacyWaterTaoistClass(session.Character.Class))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionTaoist skill " +
                    $"{FormatSession(context)} source={LegacyPromotionTaoistSource} " +
                    $"sourceRange={sourceRange} option={optionId} spellId={spellId} " +
                    $"class={session.Character.Class}; source water class gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        return await TryLearnLegacyTaoistPromotionSkillAsync(
                context,
                session,
                optionId,
                spellId,
                spellName,
                requiredLevel,
                sourceRange,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool HasLegacyTaoistPromotionSpellAtLevel(
        WorldSession session,
        ushort spellId,
        ushort requiredLevel)
    {
        foreach (var spell in session.PlayerSpells.Spells)
        {
            if (spell.Id == spellId && spell.Level == requiredLevel)
            {
                return true;
            }
        }

        return false;
    }
}
