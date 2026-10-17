using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyPromotionMonkNpcId = 8_314;
    private const string LegacyPromotionMonkSource = "NpcHandler.PromotionMonk";
    private const ushort LegacyPromotionMonkAvatarId = 226;
    private const long LegacyPromotionMonkStarterSkillLevelGate = 1;
    private const long LegacyPromotionMonkLowSkillLevelGate = 20;
    private const long LegacyPromotionMonkMidSkillLevelGate = 40;
    private const long LegacyPromotionMonkAdvancedSkillLevelGate = 70;
    private const long LegacyPromotionMonkHighSkillLevelGate = 100;

    private const string LegacyPromotionMonkWhirlwindKickSourceRange = "NpcHandler.cs:43580-43601";
    private const string LegacyPromotionMonkTripleAttackSourceRange = "NpcHandler.cs:43602-43621";
    private const string LegacyPromotionMonkOblivionSourceRange = "NpcHandler.cs:43622-43641";
    private const string LegacyPromotionMonkEarthAuraSourceRange = "NpcHandler.cs:43642-43670";
    private const string LegacyPromotionMonkFireAuraSourceRange = "NpcHandler.cs:43671-43699";
    private const string LegacyPromotionMonkWatherAuraSourceRange = "NpcHandler.cs:43700-43728";
    private const string LegacyPromotionMonkWoodAuraSourceRange = "NpcHandler.cs:43729-43757";
    private const string LegacyPromotionMonkMetalAuraSourceRange = "NpcHandler.cs:43758-43786";
    private const string LegacyPromotionMonkCompassionSourceRange = "NpcHandler.cs:43787-43815";
    private const string LegacyPromotionMonkTranquilitySourceRange = "NpcHandler.cs:43816-43844";
    private const string LegacyPromotionMonkRadiantPalmSourceRange = "NpcHandler.cs:43845-43873";
    private const string LegacyPromotionMonkSerenitySourceRange = "NpcHandler.cs:43874-43902";
    private const string LegacyPromotionMonkTyrantAuraSourceRange = "NpcHandler.cs:43903-43931";
    private const string LegacyPromotionMonkFendAuraSourceRange = "NpcHandler.cs:43932-43960";
    private const string LegacyPromotionMonkSoulShackleSourceRange = "NpcHandler.cs:43457-43489";
    private const string LegacyPromotionMonkGraceOfHeavenSourceRange = "NpcHandler.cs:43068-43112";
    private const string LegacyPromotionMonkWrathOfTheEmperorSourceRange = "NpcHandler.cs:43113-43157";
    private const string LegacyPromotionMonkInfernalEchoSourceRange = "NpcHandler.cs:43158-43202";
    private const string LegacyPromotionMonkQuestFinishSourceRange = "NpcHandler.cs:43215-43247";
    private const string LegacyPromotionMonkStarterSourceRange = "NpcHandler.cs:43961-43999";
    private const string LegacyPromotionMonkAdvancedPromotionSourceRange = "NpcHandler.cs:43280-43440";
    private const string LegacyPromotionMonkGoldenCicadaTeleportSourceRange = "NpcHandler.cs:43264-43278";

    private const byte LegacyPromotionMonkQuestFinishOption = 111;
    private const byte LegacyPromotionMonkStarterAcceptOption = 2;
    private const byte LegacyPromotionMonkSoulShackleOption = 21;
    private const byte LegacyPromotionMonkAdvancedPromotionOption = 22;
    private const byte LegacyPromotionMonkFendAuraOption = 4;
    private const byte LegacyPromotionMonkTyrantAuraOption = 5;
    private const byte LegacyPromotionMonkSerenityOption = 6;
    private const byte LegacyPromotionMonkRadiantPalmOption = 7;
    private const byte LegacyPromotionMonkTranquilityOption = 8;
    private const byte LegacyPromotionMonkCompassionOption = 10;
    private const byte LegacyPromotionMonkMetalAuraOption = 11;
    private const byte LegacyPromotionMonkWoodAuraOption = 12;
    private const byte LegacyPromotionMonkWatherAuraOption = 13;
    private const byte LegacyPromotionMonkFireAuraOption = 14;
    private const byte LegacyPromotionMonkEarthAuraOption = 15;
    private const byte LegacyPromotionMonkWhirlwindKickOption = 71;
    private const byte LegacyPromotionMonkTripleAttackOption = 72;
    private const byte LegacyPromotionMonkOblivionOption = 73;
    private const byte LegacyPromotionMonkInfernalEchoOption = 83;
    private const byte LegacyPromotionMonkWrathOfTheEmperorOption = 84;
    private const byte LegacyPromotionMonkGraceOfHeavenOption = 85;
    private const byte LegacyPromotionMonkGoldenCicadaTeleportOption = 91;

    private const uint LegacyPromotionMonkWhirlwindKickItemId = 622_439;
    private const int LegacyPromotionMonkWhirlwindKickItemCount = 2;
    private const uint LegacyPromotionMonkQuestFinishQuestId = 2_001;
    private const uint LegacyPromotionMonkQuestFinishRewardItemId = 3_005_160;
    private const long LegacyPromotionMonkEpicSkillCost = 2_150;
    private const ushort LegacyPromotionMonkEpicAvatarId = 203;
    private const long LegacyPromotionMonkGoldenCicadaTeleportLevelGate = 70;
    private const long LegacyPromotionMonkGoldenCicadaTeleportRebornGate = 2;
    private const long LegacyPromotionMonkGoldenCicadaTeleportMapId = 3_983;
    private const long LegacyPromotionMonkGoldenCicadaTeleportX = 89;
    private const long LegacyPromotionMonkGoldenCicadaTeleportY = 71;
    private const string LegacyPromotionMonkGoldenCicadaTeleportGateText =
        "Please come back when you will be 2nd Reborn and with a higher level than 70.";

    private const long LegacyPromotionMonkStarterPromotionLevelGate = 15;
    private const long LegacyPromotionMonkInternClass = 6_000;
    private const long LegacyPromotionMonkFirstClass = 6_001;
    private const long LegacyPromotionMonkSecondClass = 6_002;
    private const long LegacyPromotionMonkThirdClass = 6_003;
    private const long LegacyPromotionMonkFourthClass = 6_004;
    private const long LegacyPromotionMonkFirstAdvancedPromotionLevelGate = 40;
    private const long LegacyPromotionMonkSecondAdvancedPromotionLevelGate = 70;
    private const long LegacyPromotionMonkThirdAdvancedPromotionLevelGate = 100;
    private const long LegacyPromotionMonkFourthAdvancedPromotionLevelGate = 110;

    private const string LegacyPromotionMonkAdvancedLevelGateText =
        "There is much that you must learn before you can accept my training. Slaughter a legion of monsters first! [Level {0}+]";
    private const string LegacyPromotionMonkFirstAdvancedSuccessText =
        "Excellent! You`ve learned ways of the Monk. Do you feel a different energy in your body? That`s stronger power and battle prowess!";
    private const string LegacyPromotionMonkSecondAdvancedSuccessText =
        "Excellent! You`ve learned ways of the Taoist. Do you feel a different energy in your body? That`s stronger power and battle prowess!";
    private const string LegacyPromotionMonkThirdAdvancedSuccessText =
        "Excellent! Do you feel a different energy in your body? That`s stronger power and battle prowess!";
    private const string LegacyPromotionMonkFourthAdvancedSuccessText =
        "Excellent!Do you feel a different energy in your body? That`s stronger power and battle prowess!";
    private const string LegacyPromotionMonkAdvancedMasteredText =
        "You cannot be promoted anymore. You have mastered your class";
    private const string LegacyPromotionMonkMissingEmeraldText =
        "I'm sorry but you need to have 1 Emerald at your inventory , to get one go and kill some HillMonsters ( Desert City ) to get one .";
    private const string LegacyPromotionMonkMissingMeteorText =
        "I'm sorry but you need to have 1 Meteor at your inventory.";
    private const string LegacyPromotionMonkMissingMoonBoxText =
        "I'm sorry but you need to have 1 MoonBox at your inventory.";
    private const string LegacyPromotionMonkDragonBallRewardText = "You received DragonBall.";

    private const ushort LegacyPromotionMonkRadiantPalmSpellId = 10_381;
    private const ushort LegacyPromotionMonkOblivionSpellId = 10_390;
    private const ushort LegacyPromotionMonkTyrantAuraSpellId = 10_395;
    private const ushort LegacyPromotionMonkSerenitySpellId = 10_400;
    private const ushort LegacyPromotionMonkFendAuraSpellId = 10_410;
    private const ushort LegacyPromotionMonkWhirlwindKickSpellId = 10_415;
    private const ushort LegacyPromotionMonkMetalAuraSpellId = 10_420;
    private const ushort LegacyPromotionMonkWoodAuraSpellId = 10_421;
    private const ushort LegacyPromotionMonkWatherAuraSpellId = 10_422;
    private const ushort LegacyPromotionMonkFireAuraSpellId = 10_423;
    private const ushort LegacyPromotionMonkEarthAuraSpellId = 10_424;
    private const ushort LegacyPromotionMonkTranquilitySpellId = 10_425;
    private const ushort LegacyPromotionMonkCompassionSpellId = 10_430;
    private const ushort LegacyPromotionMonkTripleAttackSpellId = 10_490;
    private const ushort LegacyPromotionMonkSoulShackleSpellId = 10_405;
    private const ushort LegacyPromotionMonkInfernalEchoSpellId = 12_550;
    private const ushort LegacyPromotionMonkGraceOfHeavenSpellId = 12_560;
    private const ushort LegacyPromotionMonkWrathOfTheEmperorSpellId = 12_570;

    private static ValueTask<bool> TryHandleLegacyMonkPromotionSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return optionId switch
        {
            LegacyPromotionMonkStarterAcceptOption => TryHandleLegacyMonkStarterPromotionAsync(
                context,
                session,
                optionId,
                cancellationToken),
            LegacyPromotionMonkQuestFinishOption => TryHandleLegacyMonkQuestFinishOptionAsync(
                context,
                session,
                optionId,
                cancellationToken),
            LegacyPromotionMonkSoulShackleOption => TryHandleLegacyMonkPureSkillAsync(
                context,
                session,
                optionId,
                cancellationToken),
            LegacyPromotionMonkAdvancedPromotionOption => TryHandleLegacyMonkAdvancedPromotionAsync(
                context,
                session,
                optionId,
                cancellationToken),
            LegacyPromotionMonkFendAuraOption => TryLearnLegacyMonkPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionMonkFendAuraSpellId,
                "FendAura",
                LegacyPromotionMonkLowSkillLevelGate,
                LegacyPromotionMonkFendAuraSourceRange,
                cancellationToken),
            LegacyPromotionMonkTyrantAuraOption => TryLearnLegacyMonkPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionMonkTyrantAuraSpellId,
                "TyrantAura",
                LegacyPromotionMonkLowSkillLevelGate,
                LegacyPromotionMonkTyrantAuraSourceRange,
                cancellationToken),
            LegacyPromotionMonkSerenityOption => TryLearnLegacyMonkPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionMonkSerenitySpellId,
                "Serenity",
                LegacyPromotionMonkMidSkillLevelGate,
                LegacyPromotionMonkSerenitySourceRange,
                cancellationToken),
            LegacyPromotionMonkRadiantPalmOption => TryLearnLegacyMonkPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionMonkRadiantPalmSpellId,
                "RadiantPalm",
                LegacyPromotionMonkMidSkillLevelGate,
                LegacyPromotionMonkRadiantPalmSourceRange,
                cancellationToken),
            LegacyPromotionMonkTranquilityOption => TryLearnLegacyMonkPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionMonkTranquilitySpellId,
                "Tranquility",
                LegacyPromotionMonkAdvancedSkillLevelGate,
                LegacyPromotionMonkTranquilitySourceRange,
                cancellationToken),
            LegacyPromotionMonkCompassionOption => TryLearnLegacyMonkPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionMonkCompassionSpellId,
                "Compassion",
                LegacyPromotionMonkHighSkillLevelGate,
                LegacyPromotionMonkCompassionSourceRange,
                cancellationToken),
            LegacyPromotionMonkMetalAuraOption => TryLearnLegacyMonkPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionMonkMetalAuraSpellId,
                "MetalAura",
                LegacyPromotionMonkHighSkillLevelGate,
                LegacyPromotionMonkMetalAuraSourceRange,
                cancellationToken),
            LegacyPromotionMonkWoodAuraOption => TryLearnLegacyMonkPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionMonkWoodAuraSpellId,
                "WoodAura",
                LegacyPromotionMonkHighSkillLevelGate,
                LegacyPromotionMonkWoodAuraSourceRange,
                cancellationToken),
            LegacyPromotionMonkWatherAuraOption => TryLearnLegacyMonkPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionMonkWatherAuraSpellId,
                "WatherAura",
                LegacyPromotionMonkHighSkillLevelGate,
                LegacyPromotionMonkWatherAuraSourceRange,
                cancellationToken),
            LegacyPromotionMonkFireAuraOption => TryLearnLegacyMonkPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionMonkFireAuraSpellId,
                "FireAura",
                LegacyPromotionMonkHighSkillLevelGate,
                LegacyPromotionMonkFireAuraSourceRange,
                cancellationToken),
            LegacyPromotionMonkEarthAuraOption => TryLearnLegacyMonkPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionMonkEarthAuraSpellId,
                "EarthAura",
                LegacyPromotionMonkHighSkillLevelGate,
                LegacyPromotionMonkEarthAuraSourceRange,
                cancellationToken),
            LegacyPromotionMonkWhirlwindKickOption => TryLearnLegacyMonkWhirlwindKickAsync(
                context,
                session,
                cancellationToken),
            LegacyPromotionMonkTripleAttackOption => TryLearnLegacyMonkPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionMonkTripleAttackSpellId,
                "TripleAttack",
                LegacyPromotionMonkStarterSkillLevelGate,
                LegacyPromotionMonkTripleAttackSourceRange,
                cancellationToken),
            LegacyPromotionMonkOblivionOption => TryLearnLegacyMonkPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionMonkOblivionSpellId,
                "Oblivion",
                LegacyPromotionMonkStarterSkillLevelGate,
                LegacyPromotionMonkOblivionSourceRange,
                cancellationToken),
            LegacyPromotionMonkInfernalEchoOption => TryLearnLegacyMonkEpicSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionMonkInfernalEchoSpellId,
                "InfernalEcho",
                LegacyPromotionMonkInfernalEchoSourceRange,
                cancellationToken),
            LegacyPromotionMonkWrathOfTheEmperorOption => TryLearnLegacyMonkEpicSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionMonkWrathOfTheEmperorSpellId,
                "WrathoftheEmperor",
                LegacyPromotionMonkWrathOfTheEmperorSourceRange,
                cancellationToken),
            LegacyPromotionMonkGraceOfHeavenOption => TryLearnLegacyMonkEpicSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionMonkGraceOfHeavenSpellId,
                "GraceofHeaven",
                LegacyPromotionMonkGraceOfHeavenSourceRange,
                cancellationToken),
            LegacyPromotionMonkGoldenCicadaTeleportOption => TryHandleLegacyMonkGoldenCicadaTeleportAsync(
                context,
                session,
                optionId,
                cancellationToken),
            _ => ValueTask.FromResult(false),
        };
    }

    private static async ValueTask<bool> TryHandleLegacyMonkQuestFinishOptionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var nextPromotionOption = session.Character.Class == LegacyPromotionMonkInternClass
            ? (byte)1
            : LegacyPromotionMonkAdvancedPromotionOption;

        session = await TryApplyLegacyPromotionQuestFinishRewardAsync(
                context,
                session,
                optionId,
                new LegacyPromotionQuestFinishRewardSpec(
                    LegacyPromotionMonkSource,
                    LegacyPromotionMonkQuestFinishSourceRange,
                    "NpcHandler.cs:43013",
                    "GetFinishQuest(2001, class, 1004)",
                    "PromotionMonk",
                    LegacyPromotionMonkQuestFinishQuestId,
                    LegacyPromotionMonkQuestFinishRewardItemId),
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = session };

        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionMonkSource,
                [
                    "Wow, look at you, you`ll be another hero in the world of Conquer! Here, you can learn some skills and get promoted! Do you want to learn more?",
                    "~get promoted here to start your life. Would you like to learn something about your class promotion and quests?.",
                ],
                [new LegacyNpcDialogOption("I~want~to~get~promoted", nextPromotionOption)],
                avatarId: 259,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyMonkGoldenCicadaTeleportAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (session.Character.Level >= LegacyPromotionMonkGoldenCicadaTeleportLevelGate &&
            session.Character.Reborn >= LegacyPromotionMonkGoldenCicadaTeleportRebornGate)
        {
            await SendLegacyNpcTeleportAsync(
                    context,
                    session,
                    LegacyPromotionMonkNpcId,
                    optionId,
                    new LegacyNpcTeleportDestination(
                        LegacyPromotionMonkGoldenCicadaTeleportMapId,
                        LegacyPromotionMonkGoldenCicadaTeleportX,
                        LegacyPromotionMonkGoldenCicadaTeleportY,
                        LegacyPromotionMonkSource),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyNpc PromotionMonk Golden Cicada teleport " +
                    $"{FormatSession(context)} source={LegacyPromotionMonkSource} " +
                    $"sourceRange={LegacyPromotionMonkGoldenCicadaTeleportSourceRange} option={optionId} " +
                    $"map={LegacyPromotionMonkGoldenCicadaTeleportMapId} " +
                    $"x={LegacyPromotionMonkGoldenCicadaTeleportX} y={LegacyPromotionMonkGoldenCicadaTeleportY}.")
                .ConfigureAwait(false);
            return true;
        }

        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionMonkSource,
                [LegacyPromotionMonkGoldenCicadaTeleportGateText],
                [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                LegacyPromotionMonkAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc PromotionMonk Golden Cicada teleport " +
                $"{FormatSession(context)} source={LegacyPromotionMonkSource} " +
                $"sourceRange={LegacyPromotionMonkGoldenCicadaTeleportSourceRange} option={optionId} " +
                $"level={session.Character.Level} requiredLevel={LegacyPromotionMonkGoldenCicadaTeleportLevelGate} " +
                $"reborn={session.Character.Reborn} requiredReborn={LegacyPromotionMonkGoldenCicadaTeleportRebornGate}; " +
                "source gate rejected.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyMonkPureSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (session.Character.Reborn < 2)
        {
            await SendLegacyMonkSoulShacklePureGateAsync(
                    context,
                    session,
                    optionId,
                    "source reborn gate rejected.",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!IsLegacyMonkClass(session.Character.FirstClass) ||
            !IsLegacyMonkClass(session.Character.SecondClass))
        {
            await SendLegacyMonkSoulShacklePureGateAsync(
                    context,
                    session,
                    optionId,
                    "source pure Monk combo gate rejected.",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (session.Character.Level < LegacyPromotionMonkMidSkillLevelGate ||
            session.Character.Class < LegacyPromotionMonkSecondClass)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionMonkSource,
                    ["Sorry. I can`t teach you this skill until you reach level 40 and get promoted."],
                    [new LegacyNpcDialogOption("Okay.~I~see..", LegacyNpcClosedOption)],
                    LegacyPromotionMonkAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionMonk pure skill " +
                    $"{FormatSession(context)} source={LegacyPromotionMonkSource} " +
                    $"sourceRange={LegacyPromotionMonkSoulShackleSourceRange} option={optionId} " +
                    $"level={session.Character.Level} requiredLevel={LegacyPromotionMonkMidSkillLevelGate} " +
                    $"class={session.Character.Class} requiredClass={LegacyPromotionMonkSecondClass}; " +
                    "source promotion gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (session.PlayerSpells.ContainsSpell(LegacyPromotionMonkSoulShackleSpellId))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionMonkSource,
                    ["You`ve learned SoulShackle!"],
                    [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                    LegacyPromotionMonkAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionMonk pure skill " +
                    $"{FormatSession(context)} source={LegacyPromotionMonkSource} " +
                    $"sourceRange={LegacyPromotionMonkSoulShackleSourceRange} option={optionId} " +
                    $"spellId={LegacyPromotionMonkSoulShackleSpellId}; already learned.")
                .ConfigureAwait(false);
            return true;
        }

        var learned = CreateLegacyPromotionSpellEntry(LegacyPromotionMonkSoulShackleSpellId);
        var updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(session.PlayerSpells, learned);
        var updatedSession = await ApplyLegacyPromotionMasterSpellsAsync(
                context,
                session,
                updatedSpells,
                optionId,
                LegacyPromotionMonkSource,
                LegacyPromotionMonkSoulShackleSourceRange,
                LegacyPromotionMonkSoulShackleSpellId.ToString(CultureInfo.InvariantCulture))
            .ConfigureAwait(false);

        var updatedContext = context with { Session = updatedSession };
        await SendLegacyPromotionMasterLearnedSpellAsync(
                updatedContext,
                LegacyPromotionMonkSource,
                LegacyPromotionMonkAvatarId,
                learned,
                "SoulShackle",
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionMonk pure skill " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionMonkSource} " +
                $"sourceRange={LegacyPromotionMonkSoulShackleSourceRange} option={optionId} " +
                $"spellId={LegacyPromotionMonkSoulShackleSpellId}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask SendLegacyMonkSoulShacklePureGateAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string reason,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionMonkSource,
                ["You can only learn the Pure Skill `Soul Shackle` when you`ve been reborn twice, and have become a Pure Monk or Saint."],
                [new LegacyNpcDialogOption("Ok~I~see.", LegacyNpcClosedOption)],
                LegacyPromotionMonkAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc PromotionMonk pure skill " +
                $"{FormatSession(context)} source={LegacyPromotionMonkSource} " +
                $"sourceRange={LegacyPromotionMonkSoulShackleSourceRange} option={optionId} " +
                $"reborn={session.Character.Reborn} firstClass={session.Character.FirstClass} " +
                $"secondClass={session.Character.SecondClass}; {reason}")
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryHandleLegacyMonkStarterPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (!IsLegacyMonkStarterPromotionEligible(session))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionMonk starter promotion " +
                    $"{FormatSession(context)} source={LegacyPromotionMonkSource} " +
                    $"sourceRange={LegacyPromotionMonkStarterSourceRange} option={optionId} " +
                    $"level={session.Character.Level} class={session.Character.Class}; source promotion gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        var learnedSpells = new List<PlayerSpellSnapshotEntry>(capacity: 3);
        var updatedSpells = session.PlayerSpells;
        if (!updatedSpells.ContainsSpell(LegacyPromotionMonkWhirlwindKickSpellId))
        {
            var learnedWhirlwindKick = CreateLegacyPromotionSpellEntry(LegacyPromotionMonkWhirlwindKickSpellId);
            learnedSpells.Add(learnedWhirlwindKick);
            updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(updatedSpells, learnedWhirlwindKick);
        }

        if (!updatedSpells.ContainsSpell(LegacyPromotionMonkTripleAttackSpellId))
        {
            var learnedTripleAttack = CreateLegacyPromotionSpellEntry(LegacyPromotionMonkTripleAttackSpellId);
            learnedSpells.Add(learnedTripleAttack);
            updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(updatedSpells, learnedTripleAttack);
        }

        if (!updatedSpells.ContainsSpell(LegacyPromotionMonkOblivionSpellId))
        {
            var learnedOblivion = CreateLegacyPromotionSpellEntry(LegacyPromotionMonkOblivionSpellId);
            learnedSpells.Add(learnedOblivion);
            updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(updatedSpells, learnedOblivion);
        }

        var updatedSession = await ApplyLegacyPromotionMasterSpellsAsync(
                context,
                session,
                updatedSpells,
                optionId,
                LegacyPromotionMonkSource,
                LegacyPromotionMonkStarterSourceRange,
                $"{LegacyPromotionMonkWhirlwindKickSpellId},{LegacyPromotionMonkTripleAttackSpellId},{LegacyPromotionMonkOblivionSpellId}")
            .ConfigureAwait(false);

        foreach (var learnedSpell in learnedSpells)
        {
            await context.Connection.SendAsync(
                    LegacySpellPacket.FromSnapshot(learnedSpell).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var updatedCharacter = updatedSession.Character with
        {
            Class = checked(updatedSession.Character.Class + 1),
        };
        var updatedContext = context with { Session = updatedSession };
        updatedSession = await ApplyLegacyPromotionCharacterAsync(
                updatedContext,
                updatedSession,
                updatedCharacter,
                optionId,
                LegacyPromotionMonkSource,
                LegacyPromotionMonkStarterSourceRange)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionMonk starter promotion " +
                $"{FormatSession(context with { Session = updatedSession })} source={LegacyPromotionMonkSource} " +
                $"sourceRange={LegacyPromotionMonkStarterSourceRange} option={optionId} " +
                $"spellIds={LegacyPromotionMonkWhirlwindKickSpellId},{LegacyPromotionMonkTripleAttackSpellId},{LegacyPromotionMonkOblivionSpellId}.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool IsLegacyMonkStarterPromotionEligible(WorldSession session)
    {
        return session.Character.Level >= LegacyPromotionMonkStarterPromotionLevelGate &&
            session.Character.Class == LegacyPromotionMonkInternClass;
    }

    private static async ValueTask<bool> TryHandleLegacyMonkAdvancedPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return session.Character.Class switch
        {
            LegacyPromotionMonkFirstClass => await TryHandleLegacyMonkFirstAdvancedPromotionAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyPromotionMonkSecondClass => await TryHandleLegacyMonkItemAdvancedPromotionAsync(
                    context,
                    session,
                    optionId,
                    LegacyPromotionMonkSecondAdvancedPromotionLevelGate,
                    LegacyPromotionEmeraldItemId,
                    LegacyPromotionMonkMissingEmeraldText,
                    LegacyPromotionMonkSecondAdvancedSuccessText,
                    grantsDragonBall: false,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyPromotionMonkThirdClass => await TryHandleLegacyMonkItemAdvancedPromotionAsync(
                    context,
                    session,
                    optionId,
                    LegacyPromotionMonkThirdAdvancedPromotionLevelGate,
                    LegacyPromotionMeteorItemId,
                    LegacyPromotionMonkMissingMeteorText,
                    LegacyPromotionMonkThirdAdvancedSuccessText,
                    grantsDragonBall: false,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyPromotionMonkFourthClass => await TryHandleLegacyMonkItemAdvancedPromotionAsync(
                    context,
                    session,
                    optionId,
                    LegacyPromotionMonkFourthAdvancedPromotionLevelGate,
                    LegacyPromotionMoonBoxItemId,
                    LegacyPromotionMonkMissingMoonBoxText,
                    LegacyPromotionMonkFourthAdvancedSuccessText,
                    grantsDragonBall: true,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => await SendLegacyMonkAdvancedPromotionMasteredAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
        };
    }

    private static async ValueTask<bool> TryHandleLegacyMonkFirstAdvancedPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (session.Character.Level < LegacyPromotionMonkFirstAdvancedPromotionLevelGate)
        {
            await SendLegacyMonkAdvancedPromotionRejectedAsync(
                    context,
                    session,
                    optionId,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        LegacyPromotionMonkAdvancedLevelGateText,
                        LegacyPromotionMonkFirstAdvancedPromotionLevelGate),
                    LegacyPromotionMonkAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var updatedSession = await PromoteLegacyMonkAdvancedClassAsync(context, session, optionId)
            .ConfigureAwait(false);
        var updatedContext = context with { Session = updatedSession };
        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyPromotionMonkSource,
                [LegacyPromotionMonkFirstAdvancedSuccessText],
                [new LegacyNpcDialogOption("Thank~you,~Master.", LegacyNpcClosedOption)],
                LegacyPromotionMonkAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionMonk advanced promotion " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionMonkSource} " +
                $"sourceRange={LegacyPromotionMonkAdvancedPromotionSourceRange} option={optionId} " +
                $"previousClass={session.Character.Class} class={updatedSession.Character.Class}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyMonkItemAdvancedPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        long requiredLevel,
        uint requiredItemId,
        string missingItemText,
        string successText,
        bool grantsDragonBall,
        CancellationToken cancellationToken)
    {
        if (session.Character.Level < requiredLevel)
        {
            await SendLegacyMonkAdvancedPromotionRejectedAsync(
                    context,
                    session,
                    optionId,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        LegacyPromotionMonkAdvancedLevelGateText,
                        requiredLevel),
                    avatarId: 6,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!HasLegacyInventoryStackCount(session.PlayerItems, requiredItemId, 1))
        {
            await SendLegacyMonkAdvancedPromotionRejectedAsync(
                    context,
                    session,
                    optionId,
                    missingItemText,
                    avatarId: 6,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        ItemType? dragonBallType = null;
        if (grantsDragonBall &&
            !TryReadItemType(context.WorldData, LegacyPromotionDragonBallItemId, out dragonBallType))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc PromotionMonk advanced promotion missing DragonBall item type " +
                    $"{FormatSession(context)} source={LegacyPromotionMonkSource} " +
                    $"sourceRange={LegacyPromotionMonkAdvancedPromotionSourceRange} option={optionId} " +
                    $"class={session.Character.Class} rewardItemId={LegacyPromotionDragonBallItemId}; " +
                    "MoonBox was not consumed and class was not changed.")
                .ConfigureAwait(false);
            return true;
        }

        var mutationFrames = new List<byte[]>();
        var updatedItems = ConsumeLegacyInventoryStack(session.PlayerItems, requiredItemId, 1, mutationFrames);
        PlayerItemSnapshotItem? dragonBall = null;
        if (grantsDragonBall)
        {
            dragonBall = CreatePurchasedItems(updatedItems, dragonBallType!, 1)[0];
            updatedItems = updatedItems with
            {
                InventoryItems = updatedItems.InventoryItems.Concat([dragonBall]).ToArray(),
            };
            mutationFrames.Add(LegacyGameItemPacket.FromSnapshotItem(dragonBall).ToFrameBytes());
        }

        var mutationSummary = grantsDragonBall
            ? $"consumedItemId={requiredItemId} consumedCount=1 rewardItemId={LegacyPromotionDragonBallItemId} rewardCount=1"
            : $"consumedItemId={requiredItemId} consumedCount=1";
        var updatedSession = await ApplyLegacyPromotionMasterItemsAsync(
                context,
                session,
                updatedItems,
                optionId,
                LegacyPromotionMonkSource,
                LegacyPromotionMonkAdvancedPromotionSourceRange,
                mutationSummary)
            .ConfigureAwait(false);
        var updatedContext = context with { Session = updatedSession };

        await SendLegacyPromotionMasterItemFramesAsync(updatedContext, mutationFrames, cancellationToken)
            .ConfigureAwait(false);
        if (dragonBall is not null)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk(LegacyPromotionMonkDragonBallRewardText).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        updatedSession = await PromoteLegacyMonkAdvancedClassAsync(updatedContext, updatedSession, optionId)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };
        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyPromotionMonkSource,
                [successText],
                [new LegacyNpcDialogOption("Thank~you,~Master.", LegacyNpcClosedOption)],
                avatarId: 6,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionMonk advanced promotion " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionMonkSource} " +
                $"sourceRange={LegacyPromotionMonkAdvancedPromotionSourceRange} option={optionId} " +
                $"previousClass={session.Character.Class} class={updatedSession.Character.Class} {mutationSummary}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> SendLegacyMonkAdvancedPromotionRejectedAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string text,
        ushort avatarId,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionMonkSource,
                [text],
                [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
                avatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc PromotionMonk advanced promotion " +
                $"{FormatSession(context)} source={LegacyPromotionMonkSource} " +
                $"sourceRange={LegacyPromotionMonkAdvancedPromotionSourceRange} option={optionId} " +
                $"level={session.Character.Level} class={session.Character.Class}; source gate rejected.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> SendLegacyMonkAdvancedPromotionMasteredAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionMonkSource,
                [LegacyPromotionMonkAdvancedMasteredText],
                [new LegacyNpcDialogOption("Thank~you,~Master.", LegacyNpcClosedOption)],
                avatarId: 6,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc PromotionMonk advanced promotion " +
                $"{FormatSession(context)} source={LegacyPromotionMonkSource} " +
                $"sourceRange={LegacyPromotionMonkAdvancedPromotionSourceRange} option={optionId} " +
                $"level={session.Character.Level} class={session.Character.Class}; source class gate rejected.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<WorldSession> PromoteLegacyMonkAdvancedClassAsync(
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
                LegacyPromotionMonkSource,
                LegacyPromotionMonkAdvancedPromotionSourceRange)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryLearnLegacyMonkWhirlwindKickAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (session.Character.Level >= LegacyPromotionMonkStarterSkillLevelGate &&
            !session.PlayerSpells.ContainsSpell(LegacyPromotionMonkWhirlwindKickSpellId))
        {
            session = await TryGrantLegacyMonkWhirlwindKickItemsAsync(context, session, cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = session };
        }

        return await TryLearnLegacyMonkPromotionSkillAsync(
                context,
                session,
                LegacyPromotionMonkWhirlwindKickOption,
                LegacyPromotionMonkWhirlwindKickSpellId,
                "WhirlwindKick",
                LegacyPromotionMonkStarterSkillLevelGate,
                LegacyPromotionMonkWhirlwindKickSourceRange,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<WorldSession> TryGrantLegacyMonkWhirlwindKickItemsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (!TryReadItemType(context.WorldData, LegacyPromotionMonkWhirlwindKickItemId, out var itemType))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionMonk WhirlwindKick item grant " +
                    $"{FormatSession(context)} source={LegacyPromotionMonkSource} " +
                    $"sourceRange={LegacyPromotionMonkWhirlwindKickSourceRange} " +
                    $"option={LegacyPromotionMonkWhirlwindKickOption} itemId={LegacyPromotionMonkWhirlwindKickItemId} " +
                    $"itemCount={LegacyPromotionMonkWhirlwindKickItemCount}; missing item type, so side grant was not modeled.")
                .ConfigureAwait(false);
            return session;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, LegacyPromotionMonkWhirlwindKickItemCount))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionMonk WhirlwindKick item grant " +
                    $"{FormatSession(context)} source={LegacyPromotionMonkSource} " +
                    $"sourceRange={LegacyPromotionMonkWhirlwindKickSourceRange} " +
                    $"option={LegacyPromotionMonkWhirlwindKickOption} itemId={LegacyPromotionMonkWhirlwindKickItemId} " +
                    $"itemCount={LegacyPromotionMonkWhirlwindKickItemCount} loadedInventoryItems={session.PlayerItems.InventoryItems.Count}; inventory is full.")
                .ConfigureAwait(false);
            return session;
        }

        var grantedItems = CreatePurchasedItems(
            session.PlayerItems,
            itemType,
            LegacyPromotionMonkWhirlwindKickItemCount);
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems.Concat(grantedItems).ToArray(),
        };
        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await PersistLegacyMonkWhirlwindKickItemsAsync(updatedContext, updatedSession).ConfigureAwait(false);

        foreach (var item in grantedItems)
        {
            await context.Connection.SendAsync(
                    LegacyGameItemPacket.FromSnapshotItem(item).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc PromotionMonk WhirlwindKick item grant " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionMonkSource} " +
                $"sourceRange={LegacyPromotionMonkWhirlwindKickSourceRange} " +
                $"option={LegacyPromotionMonkWhirlwindKickOption} itemId={LegacyPromotionMonkWhirlwindKickItemId} " +
                $"itemCount={LegacyPromotionMonkWhirlwindKickItemCount} " +
                $"itemUids={string.Join(",", grantedItems.Select(item => item.Uid.ToString(CultureInfo.InvariantCulture)))}.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask PersistLegacyMonkWhirlwindKickItemsAsync(
        WorldPacketHandlerContext context,
        WorldSession updatedSession)
    {
        if (!string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            try
            {
                new PlayerItemSnapshotLoader(context.PlayerItemsRoot).SaveByCharacterUid(
                    updatedSession.Character.Uid,
                    updatedSession.PlayerItems);
                await context.Log.WriteLineAsync(
                        "WorldServer persisted LegacyNpc PromotionMonk WhirlwindKick item grant " +
                        $"{FormatSession(context)} source={LegacyPromotionMonkSource} " +
                        $"sourceRange={LegacyPromotionMonkWhirlwindKickSourceRange} " +
                        $"path={updatedSession.Character.Uid.ToString(CultureInfo.InvariantCulture)}.bin " +
                        $"itemId={LegacyPromotionMonkWhirlwindKickItemId} itemCount={LegacyPromotionMonkWhirlwindKickItemCount}.")
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is ArgumentException ||
                ex is NotSupportedException)
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyNpc PromotionMonk WhirlwindKick item persistence " +
                        $"{FormatSession(context)} source={LegacyPromotionMonkSource} " +
                        $"sourceRange={LegacyPromotionMonkWhirlwindKickSourceRange} " +
                        $"itemId={LegacyPromotionMonkWhirlwindKickItemId} itemCount={LegacyPromotionMonkWhirlwindKickItemCount} " +
                        $"reason={ex.Message}.")
                    .ConfigureAwait(false);
            }

            return;
        }

        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc PromotionMonk WhirlwindKick item grant missing PlayerItemsRoot " +
                $"{FormatSession(context)} source={LegacyPromotionMonkSource} " +
                $"sourceRange={LegacyPromotionMonkWhirlwindKickSourceRange} " +
                $"itemId={LegacyPromotionMonkWhirlwindKickItemId} itemCount={LegacyPromotionMonkWhirlwindKickItemCount}; " +
                "granted items are session-only.")
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryLearnLegacyMonkEpicSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        ushort spellId,
        string spellName,
        string sourceRange,
        CancellationToken cancellationToken)
    {
        if (session.Character.ConquerPoints < LegacyPromotionMonkEpicSkillCost)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionMonkSource,
                    ["Come~on,~you~don`t~have~2,150~CPs~with~you."],
                    [new LegacyNpcDialogOption("Sorry...", LegacyNpcClosedOption)],
                    avatarId: 5,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionMonk epic skill " +
                    $"{FormatSession(context)} source={LegacyPromotionMonkSource} " +
                    $"sourceRange={sourceRange} option={optionId} spellId={spellId} " +
                    $"conquerPoints={session.Character.ConquerPoints} " +
                    $"requiredConquerPoints={LegacyPromotionMonkEpicSkillCost}; insufficient CP.")
                .ConfigureAwait(false);
            return true;
        }

        var debitedSession = await DebitLegacyMonkEpicConquerPointsAsync(
                context,
                session,
                optionId,
                sourceRange,
                cancellationToken)
            .ConfigureAwait(false);
        var debitedContext = context with { Session = debitedSession };

        if (debitedSession.PlayerSpells.ContainsSpell(spellId))
        {
            await SendLegacyNpcDialogAsync(
                    debitedContext,
                    LegacyPromotionMonkSource,
                    [$"You`ve learned {spellName}!"],
                    [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                    LegacyPromotionMonkEpicAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyNpc PromotionMonk epic skill " +
                    $"{FormatSession(debitedContext)} source={LegacyPromotionMonkSource} " +
                    $"sourceRange={sourceRange} option={optionId} spellId={spellId}; " +
                    $"already learned costConquerPoints={LegacyPromotionMonkEpicSkillCost}.")
                .ConfigureAwait(false);
            return true;
        }

        var learned = CreateLegacyPromotionSpellEntry(spellId);
        var updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(debitedSession.PlayerSpells, learned);
        var updatedSession = await ApplyLegacyPromotionMasterSpellsAsync(
                debitedContext,
                debitedSession,
                updatedSpells,
                optionId,
                LegacyPromotionMonkSource,
                sourceRange,
                spellId.ToString(CultureInfo.InvariantCulture))
            .ConfigureAwait(false);

        var updatedContext = context with { Session = updatedSession };
        await SendLegacyPromotionMasterLearnedSpellAsync(
                updatedContext,
                LegacyPromotionMonkSource,
                LegacyPromotionMonkEpicAvatarId,
                learned,
                spellName,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionMonk epic skill " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionMonkSource} " +
                $"sourceRange={sourceRange} option={optionId} spellId={spellId} " +
                $"costConquerPoints={LegacyPromotionMonkEpicSkillCost}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<WorldSession> DebitLegacyMonkEpicConquerPointsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string sourceRange,
        CancellationToken cancellationToken)
    {
        var updatedCharacter = session.Character with
        {
            ConquerPoints = checked(session.Character.ConquerPoints - LegacyPromotionMonkEpicSkillCost),
        };
        var updatedSession = await ApplyLegacyPromotionCharacterAsync(
                context,
                session,
                updatedCharacter,
                optionId,
                LegacyPromotionMonkSource,
                sourceRange)
            .ConfigureAwait(false);
        var updatedContext = context with { Session = updatedSession };
        await SendScalarUpdateAsync(
                updatedContext,
                updatedCharacter,
                LegacyMsgUpdateDataType.ConquerPoints,
                checked((ulong)updatedCharacter.ConquerPoints),
                cancellationToken)
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static ValueTask<bool> TryLearnLegacyMonkPromotionSkillAsync(
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
            LegacyPromotionMonkSource,
            sourceRange,
            LegacyPromotionMonkAvatarId,
            cancellationToken);
    }
}
