using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyPromotionNinjaNpcId = 4_972;
    private const string LegacyPromotionNinjaSource = "NpcHandler.PromotionNinja";
    private const ushort LegacyPromotionNinjaAvatarId = 203;

    private const string LegacyPromotionNinjaStarterSourceRange = "NpcHandler.cs:44411-44459";
    private const string LegacyPromotionNinjaQuestFinishLookupSourceRange = "NpcHandler.cs:44019";
    private const string LegacyPromotionNinjaQuestFinishSourceRange = "NpcHandler.cs:44063-44102";
    private const string LegacyPromotionNinjaAdvancedPromotionSourceRange = "NpcHandler.cs:44252-44410";
    private const string LegacyPromotionNinjaFatalSpinSourceRange = "NpcHandler.cs:44139-44172";
    private const string LegacyPromotionNinjaTwilightDanceSourceRange = "NpcHandler.cs:44173-44206";
    private const string LegacyPromotionNinjaShadowCloneSourceRange = "NpcHandler.cs:44207-44240";
    private const string LegacyPromotionNinjaCounterKillSourceRange = "NpcHandler.cs:44459-44491";
    private const string LegacyPromotionNinjaFatalStrikeSourceRange = "NpcHandler.cs:44536-44574";
    private const string LegacyPromotionNinjaShurikenVortexSourceRange = "NpcHandler.cs:44575-44613";
    private const string LegacyPromotionNinjaGapingWoundsSourceRange = "NpcHandler.cs:44632-44670";
    private const string LegacyPromotionNinjaBloodyScytheSourceRange = "NpcHandler.cs:44671-44709";
    private const string LegacyPromotionNinjaMortalDragSourceRange = "NpcHandler.cs:44710-44748";
    private const string LegacyPromotionNinjaTwofoldBladesSourceRange = "NpcHandler.cs:44765-44803";
    private const string LegacyPromotionNinjaSuperTwofoldBladeSourceRange = "NpcHandler.cs:44804-44842";
    private const string LegacyPromotionNinjaToxicFogSourceRange = "NpcHandler.cs:44859-44897";
    private const string LegacyPromotionNinjaArcherBaneSourceRange = "NpcHandler.cs:44898-44936";
    private const string LegacyPromotionNinjaPoisonStarSourceRange = "NpcHandler.cs:44937-44999";

    private const byte LegacyPromotionNinjaPoisonStarOption = 4;
    private const byte LegacyPromotionNinjaArcherBaneOption = 5;
    private const byte LegacyPromotionNinjaToxicFogOption = 14;
    private const byte LegacyPromotionNinjaTwofoldBladesOption = 7;
    private const byte LegacyPromotionNinjaSuperTwofoldBladeOption = 249;
    private const byte LegacyPromotionNinjaMortalDragOption = 9;
    private const byte LegacyPromotionNinjaBloodyScytheOption = 10;
    private const byte LegacyPromotionNinjaGapingWoundsOption = 87;
    private const byte LegacyPromotionNinjaShurikenVortexOption = 12;
    private const byte LegacyPromotionNinjaFatalStrikeOption = 13;
    private const byte LegacyPromotionNinjaCounterKillOption = 16;
    private const byte LegacyPromotionNinjaStarterAcceptOption = 2;
    private const byte LegacyPromotionNinjaAdvancedPromotionOption = 20;
    private const byte LegacyPromotionNinjaQuestFinishOption = 111;
    private const byte LegacyPromotionNinjaShadowCloneOption = 83;
    private const byte LegacyPromotionNinjaTwilightDanceOption = 84;
    private const byte LegacyPromotionNinjaFatalSpinOption = 85;

    private const ushort LegacyPromotionNinjaArcherBaneSpellId = 6_004;
    private const ushort LegacyPromotionNinjaToxicFogSpellId = 6_001;
    private const ushort LegacyPromotionNinjaTwofoldBladesSpellId = 6_000;
    private const ushort LegacyPromotionNinjaSuperTwofoldBladeSpellId = 12_080;
    private const ushort LegacyPromotionNinjaMortalDragSpellId = 11_180;
    private const ushort LegacyPromotionNinjaBloodyScytheSpellId = 11_170;
    private const ushort LegacyPromotionNinjaGapingWoundsSpellId = 11_230;
    private const ushort LegacyPromotionNinjaShurikenVortexSpellId = 6_010;
    private const ushort LegacyPromotionNinjaFatalStrikeSpellId = 6_011;
    private const ushort LegacyPromotionNinjaShadowCloneSpellId = 12_090;
    private const ushort LegacyPromotionNinjaTwilightDanceSpellId = 12_070;
    private const ushort LegacyPromotionNinjaFatalSpinSpellId = 12_110;
    private const ushort LegacyPromotionNinjaPoisonStarSpellId = 6_002;
    private const long LegacyPromotionNinjaEpicSkillCost = 2_150;
    private const long LegacyPromotionNinjaStarterLevelGate = 15;
    private const long LegacyPromotionNinjaInternClass = 5_000;
    private const long LegacyPromotionNinjaRankOneClass = 5_001;
    private const long LegacyPromotionNinjaRankTwoClass = 5_002;
    private const long LegacyPromotionNinjaRankThreeClass = 5_003;
    private const long LegacyPromotionNinjaRankFourClass = 5_004;
    private const uint LegacyPromotionNinjaQuestFinishMissionId = 2_001;
    private const uint LegacyPromotionNinjaQuestFinishRewardItemId = 3_005_160;

    private const string LegacyPromotionNinjaMissingEmeraldText =
        "I'm sorry but you need to have 1 Emerald at your inventory , to get one go and kill some HillMonsters ( Desert City ) to get one .";
    private const string LegacyPromotionNinjaMissingMeteorText =
        "I'm sorry but you need to have 1 Meteor at your inventory.";
    private const string LegacyPromotionNinjaMissingMoonBoxText =
        "I'm sorry but you need to have 1 MoonBox at your inventory.";
    private const string LegacyPromotionNinjaDragonBallReceivedText = "You received DragonBall.";

    private static ValueTask<bool> TryHandleLegacyNinjaPromotionSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == LegacyPromotionNinjaQuestFinishOption)
        {
            return TryHandleLegacyNinjaQuestFinishAsync(
                context,
                session,
                optionId,
                cancellationToken);
        }

        if (optionId == LegacyPromotionNinjaStarterAcceptOption)
        {
            return TryHandleLegacyNinjaStarterPromotionAsync(
                context,
                session,
                optionId,
                cancellationToken);
        }

        if (optionId == LegacyPromotionNinjaAdvancedPromotionOption)
        {
            return TryHandleLegacyNinjaAdvancedPromotionAsync(
                context,
                session,
                optionId,
                cancellationToken);
        }

        if (optionId == LegacyPromotionNinjaPoisonStarOption)
        {
            return TryHandleLegacyNinjaPoisonStarAsync(
                context,
                session,
                optionId,
                cancellationToken);
        }

        if (optionId is LegacyPromotionNinjaShadowCloneOption or
            LegacyPromotionNinjaTwilightDanceOption or
            LegacyPromotionNinjaFatalSpinOption)
        {
            return TryHandleLegacyNinjaEpicSkillAsync(
                context,
                session,
                optionId,
                cancellationToken);
        }

        if (optionId == LegacyPromotionNinjaCounterKillOption)
        {
            return TryHandleLegacyNinjaPureSkillAsync(
                context,
                session,
                optionId,
                cancellationToken);
        }

        return optionId switch
        {
            LegacyPromotionNinjaArcherBaneOption => TryLearnLegacyNinjaPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionNinjaArcherBaneSpellId,
                "ArcherBane",
                requiredLevel: 110,
                requiredClass: 5_005,
                "Sorry. I can`t teach you this skill until you reach level 110 and get promoted.",
                LegacyPromotionNinjaArcherBaneSourceRange,
                cancellationToken),
            LegacyPromotionNinjaToxicFogOption => TryLearnLegacyNinjaPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionNinjaToxicFogSpellId,
                "ToxicFog",
                requiredLevel: 15,
                requiredClass: 5_001,
                "Sorry. I can`t teach you this skill until you reach level 70 and get promoted.",
                LegacyPromotionNinjaToxicFogSourceRange,
                cancellationToken),
            LegacyPromotionNinjaTwofoldBladesOption => TryLearnLegacyNinjaPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionNinjaTwofoldBladesSpellId,
                "TwofoldBlades",
                requiredLevel: 40,
                requiredClass: 5_002,
                "Sorry. I can`t teach you this skill until you reach level 40 and get promoted.",
                LegacyPromotionNinjaTwofoldBladesSourceRange,
                cancellationToken),
            LegacyPromotionNinjaSuperTwofoldBladeOption => TryLearnLegacyNinjaPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionNinjaSuperTwofoldBladeSpellId,
                "SuperTwofoldBlade",
                requiredLevel: 70,
                requiredClass: 5_003,
                "Sorry. I can`t teach you this skill until you reach level 70 and get promoted.",
                LegacyPromotionNinjaSuperTwofoldBladeSourceRange,
                cancellationToken),
            LegacyPromotionNinjaMortalDragOption => TryLearnLegacyNinjaPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionNinjaMortalDragSpellId,
                "MortalDrag",
                requiredLevel: 20,
                requiredClass: 5_001,
                "Sorry. I can`t teach you this skill until you reach level 20 and get promoted.",
                LegacyPromotionNinjaMortalDragSourceRange,
                cancellationToken),
            LegacyPromotionNinjaBloodyScytheOption => TryLearnLegacyNinjaPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionNinjaBloodyScytheSpellId,
                "BloodyScythe",
                requiredLevel: 40,
                requiredClass: 5_002,
                "Sorry. I can`t teach you this skill until you reach level 40 and get promoted.",
                LegacyPromotionNinjaBloodyScytheSourceRange,
                cancellationToken),
            LegacyPromotionNinjaGapingWoundsOption => TryLearnLegacyNinjaPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionNinjaGapingWoundsSpellId,
                "GapingWounds",
                requiredLevel: 100,
                requiredClass: 5_002,
                "Sorry. I can`t teach you this skill until you reach level 40 and get promoted.",
                LegacyPromotionNinjaGapingWoundsSourceRange,
                cancellationToken),
            LegacyPromotionNinjaShurikenVortexOption => TryLearnLegacyNinjaPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionNinjaShurikenVortexSpellId,
                "ShurikenVortex",
                requiredLevel: 70,
                requiredClass: 5_003,
                "Sorry. I can`t teach you this skill until you reach level 70 and get promoted.",
                LegacyPromotionNinjaShurikenVortexSourceRange,
                cancellationToken),
            LegacyPromotionNinjaFatalStrikeOption => TryLearnLegacyNinjaPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionNinjaFatalStrikeSpellId,
                "FatalStrike",
                requiredLevel: 3,
                requiredClass: 5_000,
                "Sorry. I can`t teach you this skill until you reach level 3 and get promoted.",
                LegacyPromotionNinjaFatalStrikeSourceRange,
                cancellationToken),
            _ => ValueTask.FromResult(false),
        };
    }

    private static ValueTask<bool> TryHandleLegacyNinjaEpicSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return optionId switch
        {
            LegacyPromotionNinjaShadowCloneOption => TryLearnLegacyNinjaEpicSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionNinjaShadowCloneSpellId,
                "ShadowClone",
                LegacyPromotionNinjaShadowCloneSourceRange,
                cancellationToken),
            LegacyPromotionNinjaTwilightDanceOption => TryLearnLegacyNinjaEpicSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionNinjaTwilightDanceSpellId,
                "TwilightDance",
                LegacyPromotionNinjaTwilightDanceSourceRange,
                cancellationToken),
            LegacyPromotionNinjaFatalSpinOption => TryLearnLegacyNinjaEpicSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionNinjaFatalSpinSpellId,
                "FatalSpin",
                LegacyPromotionNinjaFatalSpinSourceRange,
                cancellationToken),
            _ => ValueTask.FromResult(false),
        };
    }

    private static async ValueTask<bool> TryHandleLegacyNinjaQuestFinishAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var activeSession = await TryApplyLegacyPromotionQuestFinishRewardAsync(
                context,
                session,
                optionId,
                new LegacyPromotionQuestFinishRewardSpec(
                    LegacyPromotionNinjaSource,
                    LegacyPromotionNinjaQuestFinishSourceRange,
                    LegacyPromotionNinjaQuestFinishLookupSourceRange,
                    "GetFinishQuest(2001, class, 1004)",
                    "PromotionNinja",
                    LegacyPromotionNinjaQuestFinishMissionId,
                    LegacyPromotionNinjaQuestFinishRewardItemId),
                cancellationToken)
            .ConfigureAwait(false);

        var nextPromotionOption = activeSession.Character.Class == LegacyPromotionNinjaInternClass
            ? (byte)1
            : LegacyPromotionNinjaAdvancedPromotionOption;

        await SendLegacyNpcDialogAsync(
                context with { Session = activeSession },
                LegacyPromotionNinjaSource,
                [
                    "Wow, look at you, you`ll be another hero in the world of Conquer! Here, you can learn some skills and get promoted! Do you want to learn more?",
                    "~get promoted here to start your life. Would you like to learn something about your class promotion and quests?.",
                ],
                [new LegacyNpcDialogOption("I~want~to~get~promoted", nextPromotionOption)],
                259,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static ValueTask<bool> TryLearnLegacyNinjaEpicSkillAsync(
        WorldPacketHandlerContext context, WorldSession session, byte optionId, ushort spellId,
        string spellName, string sourceRange, CancellationToken cancellationToken) =>
        TryLearnLegacyAtomicEpicSkillAsync(context, session, optionId, spellId, spellName, sourceRange,
            LegacyPromotionNinjaSource, "Ninja", LegacyPromotionNinjaAvatarId, LegacyPromotionNinjaEpicSkillCost, cancellationToken);

    private static async ValueTask<bool> TryHandleLegacyNinjaPureSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (session.Character.Reborn < 2)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionNinjaSource,
                    ["Only pure Ninja, who have spent all 3 lives as a Ninja, can learn the Pure Skill, Counter Kill."],
                    [new LegacyNpcDialogOption("Ok~I~see.", LegacyNpcClosedOption)],
                    LegacyPromotionNinjaAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionNinja pure skill " +
                    $"{FormatSession(context)} source={LegacyPromotionNinjaSource} " +
                    $"sourceRange={LegacyPromotionNinjaCounterKillSourceRange} option={optionId} " +
                    $"reborn={session.Character.Reborn} requiredReborn=2; source reborn gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (!IsLegacyNinjaClass(session.Character.FirstClass) ||
            !IsLegacyNinjaClass(session.Character.SecondClass))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionNinjaSource,
                    ["Only pure Ninja, who have spent all 3 lives as a Ninja, can learn the Pure Skill, Counter Kill."],
                    [new LegacyNpcDialogOption("Ok~I~see.", LegacyNpcClosedOption)],
                    LegacyPromotionNinjaAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionNinja pure skill " +
                    $"{FormatSession(context)} source={LegacyPromotionNinjaSource} " +
                    $"sourceRange={LegacyPromotionNinjaCounterKillSourceRange} option={optionId} " +
                    $"firstClass={session.Character.FirstClass} secondClass={session.Character.SecondClass}; " +
                    "source pure Ninja combo gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (session.Character.Level < 40 || session.Character.Class < LegacyPromotionNinjaRankTwoClass)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionNinjaSource,
                    ["Sorry. I can`t teach you this skill until you reach level 40 and get promoted."],
                    [new LegacyNpcDialogOption("Okay.~I~see..", LegacyNpcClosedOption)],
                    LegacyPromotionNinjaAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionNinja pure skill " +
                    $"{FormatSession(context)} source={LegacyPromotionNinjaSource} " +
                    $"sourceRange={LegacyPromotionNinjaCounterKillSourceRange} option={optionId} " +
                    $"level={session.Character.Level} requiredLevel=40 " +
                    $"class={session.Character.Class} requiredClass={LegacyPromotionNinjaRankTwoClass}; " +
                    "source promotion gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (session.PlayerSpells.ContainsSpell(LegacyCounterKillSpellId))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionNinjaSource,
                    ["You`ve learned CounterKill!"],
                    [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                    LegacyPromotionNinjaAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionNinja pure skill " +
                    $"{FormatSession(context)} source={LegacyPromotionNinjaSource} " +
                    $"sourceRange={LegacyPromotionNinjaCounterKillSourceRange} option={optionId} " +
                    $"spellId={LegacyCounterKillSpellId}; already learned.")
                .ConfigureAwait(false);
            return true;
        }

        var learned = CreateLegacyPromotionSpellEntry(LegacyCounterKillSpellId);
        var updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(session.PlayerSpells, learned);
        var updatedSession = await ApplyLegacyPromotionMasterSpellsAsync(
                context,
                session,
                updatedSpells,
                optionId,
                LegacyPromotionNinjaSource,
                LegacyPromotionNinjaCounterKillSourceRange,
                LegacyCounterKillSpellId.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ConfigureAwait(false);

        var updatedContext = context with { Session = updatedSession };
        await context.Connection.SendAsync(LegacySpellPacket.FromSnapshot(learned).ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyPromotionNinjaSource,
                ["Congratulation! You just have learned the skill CounterKill"],
                [new LegacyNpcDialogOption("Ok , thank you.", LegacyNpcClosedOption)],
                LegacyPromotionNinjaAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionNinja pure skill " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionNinjaSource} " +
                $"sourceRange={LegacyPromotionNinjaCounterKillSourceRange} option={optionId} " +
                $"spellId={LegacyCounterKillSpellId}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyNinjaPoisonStarAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string lineageGateText =
            "Only reborn Ninja-ninja, who have spent all 2 lives as Ninja, can learn the that Skill, PoisonStar.";

        if (session.Character.Reborn == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionNinjaSource,
                    [lineageGateText],
                    [new LegacyNpcDialogOption("Ok~I~see.", LegacyNpcClosedOption)],
                    LegacyPromotionNinjaAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionNinja PoisonStar skill " +
                    $"{FormatSession(context)} source={LegacyPromotionNinjaSource} " +
                    $"sourceRange={LegacyPromotionNinjaPoisonStarSourceRange} option={optionId} " +
                    $"reborn={session.Character.Reborn}; source reborn gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (!IsLegacyNinjaPoisonStarLineageEligible(session.Character))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionNinjaSource,
                    [lineageGateText],
                    [new LegacyNpcDialogOption("Ok~I~see.", LegacyNpcClosedOption)],
                    LegacyPromotionNinjaAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionNinja PoisonStar skill " +
                    $"{FormatSession(context)} source={LegacyPromotionNinjaSource} " +
                    $"sourceRange={LegacyPromotionNinjaPoisonStarSourceRange} option={optionId} " +
                    $"reborn={session.Character.Reborn} firstClass={session.Character.FirstClass} " +
                    $"secondClass={session.Character.SecondClass}; source PoisonStar lineage gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (session.Character.Level < 70 || session.Character.Class < LegacyPromotionNinjaRankThreeClass)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionNinjaSource,
                    ["Sorry. I can`t teach you this skill until you reach level 70 and get promoted."],
                    [new LegacyNpcDialogOption("Okay.~I~see..", LegacyNpcClosedOption)],
                    LegacyPromotionNinjaAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionNinja PoisonStar skill " +
                    $"{FormatSession(context)} source={LegacyPromotionNinjaSource} " +
                    $"sourceRange={LegacyPromotionNinjaPoisonStarSourceRange} option={optionId} " +
                    $"level={session.Character.Level} requiredLevel=70 " +
                    $"class={session.Character.Class} requiredClass={LegacyPromotionNinjaRankThreeClass}; " +
                    "source promotion gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (session.PlayerSpells.ContainsSpell(LegacyPromotionNinjaPoisonStarSpellId))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionNinjaSource,
                    ["You`ve learned PoisonStar!"],
                    [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                    LegacyPromotionNinjaAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionNinja PoisonStar skill " +
                    $"{FormatSession(context)} source={LegacyPromotionNinjaSource} " +
                    $"sourceRange={LegacyPromotionNinjaPoisonStarSourceRange} option={optionId} " +
                    $"spellId={LegacyPromotionNinjaPoisonStarSpellId}; already learned.")
                .ConfigureAwait(false);
            return true;
        }

        var learned = CreateLegacyPromotionSpellEntry(LegacyPromotionNinjaPoisonStarSpellId);
        var updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(session.PlayerSpells, learned);
        var updatedSession = await ApplyLegacyPromotionMasterSpellsAsync(
                context,
                session,
                updatedSpells,
                optionId,
                LegacyPromotionNinjaSource,
                LegacyPromotionNinjaPoisonStarSourceRange,
                LegacyPromotionNinjaPoisonStarSpellId.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ConfigureAwait(false);

        var updatedContext = context with { Session = updatedSession };
        await updatedContext.Connection.SendAsync(
                LegacySpellPacket.FromSnapshot(learned).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyPromotionNinjaSource,
                ["Congratulation! You just have learned the skill PoisonStar"],
                [new LegacyNpcDialogOption("Ok , thank you.", LegacyNpcClosedOption)],
                LegacyPromotionNinjaAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionNinja PoisonStar skill " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionNinjaSource} " +
                $"sourceRange={LegacyPromotionNinjaPoisonStarSourceRange} option={optionId} " +
                $"spellId={LegacyPromotionNinjaPoisonStarSpellId}.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool IsLegacyNinjaPoisonStarLineageEligible(CharacterSnapshot character)
    {
        return (character.Reborn == 1 && IsLegacyNinjaClass(character.FirstClass)) ||
            (character.Reborn == 2 && IsLegacyNinjaClass(character.SecondClass));
    }

    private static async ValueTask<bool> TryHandleLegacyNinjaAdvancedPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var branch = GetLegacyNinjaAdvancedPromotionBranch(session.Character.Class);
        if (branch.RequiredClass == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionNinjaSource,
                    ["You cannot be promoted anymore. You have mastered your class"],
                    [new LegacyNpcDialogOption("Thank~you,~Master.", LegacyNpcClosedOption)],
                    6,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionNinja advanced promotion " +
                    $"{FormatSession(context)} source={LegacyPromotionNinjaSource} " +
                    $"sourceRange={LegacyPromotionNinjaAdvancedPromotionSourceRange} option={optionId} " +
                    $"class={session.Character.Class}; source class gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (session.Character.Level < branch.RequiredLevel)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionNinjaSource,
                    [$"There is much that you must learn before you can accept my training. Slaughter a legion of monsters first! [Level {branch.RequiredLevel}+]"],
                    [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
                    branch.AvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionNinja advanced promotion " +
                    $"{FormatSession(context)} source={LegacyPromotionNinjaSource} " +
                    $"sourceRange={LegacyPromotionNinjaAdvancedPromotionSourceRange} option={optionId} " +
                    $"class={session.Character.Class} level={session.Character.Level} " +
                    $"requiredClass={branch.RequiredClass} requiredLevel={branch.RequiredLevel}; source level gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (branch.RequiredItemId != 0 &&
            !HasLegacyInventoryStackCount(session.PlayerItems, branch.RequiredItemId, requiredCount: 1))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionNinjaSource,
                    [branch.MissingItemText],
                    [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
                    6,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionNinja advanced promotion " +
                    $"{FormatSession(context)} source={LegacyPromotionNinjaSource} " +
                    $"sourceRange={LegacyPromotionNinjaAdvancedPromotionSourceRange} option={optionId} " +
                    $"class={session.Character.Class} level={session.Character.Level} " +
                    $"requiredItemId={branch.RequiredItemId}; source item gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        ItemType? rewardItemType = null;
        if (branch.RewardItemId != 0 &&
            !TryReadItemType(context.WorldData, branch.RewardItemId, out rewardItemType))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc PromotionNinja advanced promotion missing reward item type " +
                    $"{FormatSession(context)} source={LegacyPromotionNinjaSource} " +
                    $"sourceRange={LegacyPromotionNinjaAdvancedPromotionSourceRange} option={optionId} " +
                    $"rewardItemId={branch.RewardItemId}; character and inventory update skipped.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedSession = session;
        var mutationFrames = new List<byte[]>();
        if (branch.RequiredItemId != 0)
        {
            var updatedItems = ConsumeLegacyInventoryStack(
                session.PlayerItems,
                branch.RequiredItemId,
                count: 1,
                mutationFrames);

            if (rewardItemType is not null)
            {
                var rewardItem = CreatePurchasedItems(updatedItems, rewardItemType, count: 1)[0];
                updatedItems = updatedItems with
                {
                    InventoryItems = updatedItems.InventoryItems.Concat([rewardItem]).ToArray(),
                };
                mutationFrames.Add(LegacyGameItemPacket.FromSnapshotItem(rewardItem).ToFrameBytes());
            }

            updatedSession = await ApplyLegacyPromotionMasterItemsAsync(
                    context,
                    session,
                    updatedItems,
                    optionId,
                    LegacyPromotionNinjaSource,
                    LegacyPromotionNinjaAdvancedPromotionSourceRange,
                    branch.RewardItemId == 0
                        ? $"consumedItemId={branch.RequiredItemId} consumedCount=1"
                        : $"consumedItemId={branch.RequiredItemId} consumedCount=1 rewardItemId={branch.RewardItemId} rewardCount=1")
                .ConfigureAwait(false);
            await SendLegacyPromotionMasterItemFramesAsync(context, mutationFrames, cancellationToken)
                .ConfigureAwait(false);

            if (branch.RewardItemId == LegacyPromotionDragonBallItemId)
            {
                await context.Connection.SendAsync(
                        LegacyMsgMessagePacket.SystemTalk(LegacyPromotionNinjaDragonBallReceivedText).ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var updatedCharacter = updatedSession.Character with
        {
            Class = checked(updatedSession.Character.Class + 1),
        };
        updatedSession = await ApplyLegacyPromotionCharacterAsync(
                context,
                updatedSession,
                updatedCharacter,
                optionId,
                LegacyPromotionNinjaSource,
                LegacyPromotionNinjaAdvancedPromotionSourceRange)
            .ConfigureAwait(false);

        var updatedContext = context with { Session = updatedSession };
        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyPromotionNinjaSource,
                [branch.SuccessText],
                [new LegacyNpcDialogOption("Thank~you,~Master.", LegacyNpcClosedOption)],
                branch.AvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionNinja advanced promotion " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionNinjaSource} " +
                $"sourceRange={LegacyPromotionNinjaAdvancedPromotionSourceRange} option={optionId} " +
                $"class={session.Character.Class}->{updatedCharacter.Class} level={session.Character.Level} " +
                $"requiredItemId={branch.RequiredItemId} rewardItemId={branch.RewardItemId}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyNinjaStarterPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (!IsLegacyNinjaStarterPromotionEligible(session))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionNinja starter promotion " +
                    $"{FormatSession(context)} source={LegacyPromotionNinjaSource} " +
                    $"sourceRange={LegacyPromotionNinjaStarterSourceRange} option={optionId} " +
                    $"level={session.Character.Level} class={session.Character.Class}; source promotion gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        var learnedSpells = new List<PlayerSpellSnapshotEntry>(capacity: 2);
        var updatedSpells = session.PlayerSpells;
        if (!updatedSpells.ContainsSpell(LegacyPromotionNinjaFatalStrikeSpellId))
        {
            var learnedFatalStrike = CreateLegacyPromotionSpellEntry(LegacyPromotionNinjaFatalStrikeSpellId);
            learnedSpells.Add(learnedFatalStrike);
            updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(
                updatedSpells,
                learnedFatalStrike);
        }

        if (!updatedSpells.ContainsSpell(LegacyPromotionNinjaToxicFogSpellId))
        {
            var learnedToxicFog = CreateLegacyPromotionSpellEntry(LegacyPromotionNinjaToxicFogSpellId);
            learnedSpells.Add(learnedToxicFog);
            updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(
                updatedSpells,
                learnedToxicFog);
        }

        var updatedSession = await ApplyLegacyPromotionMasterSpellsAsync(
                context,
                session,
                updatedSpells,
                optionId,
                LegacyPromotionNinjaSource,
                LegacyPromotionNinjaStarterSourceRange,
                $"{LegacyPromotionNinjaFatalStrikeSpellId},{LegacyPromotionNinjaToxicFogSpellId}")
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
        updatedSession = await ApplyLegacyPromotionCharacterAsync(
                context,
                updatedSession,
                updatedCharacter,
                optionId,
                LegacyPromotionNinjaSource,
                LegacyPromotionNinjaStarterSourceRange)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionNinja starter promotion " +
                $"{FormatSession(context with { Session = updatedSession })} source={LegacyPromotionNinjaSource} " +
                $"sourceRange={LegacyPromotionNinjaStarterSourceRange} option={optionId} " +
                $"spellIds={LegacyPromotionNinjaFatalStrikeSpellId},{LegacyPromotionNinjaToxicFogSpellId}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryLearnLegacyNinjaPromotionSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        ushort spellId,
        string spellName,
        long requiredLevel,
        long requiredClass,
        string promotionGateText,
        string sourceRange,
        CancellationToken cancellationToken)
    {
        if (session.Character.Class < requiredClass)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionNinjaSource,
                    [promotionGateText],
                    [new LegacyNpcDialogOption("Okay.~I~see..", LegacyNpcClosedOption)],
                    LegacyPromotionNinjaAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionMaster skill " +
                    $"{FormatSession(context)} source={LegacyPromotionNinjaSource} sourceRange={sourceRange} " +
                    $"option={optionId} class={session.Character.Class} requiredClass={requiredClass}; " +
                    "source promotion gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        return await TryLearnLegacyPromotionMasterSkillAsync(
                context,
                session,
                optionId,
                spellId,
                spellName,
                requiredLevel,
                LegacyPromotionNinjaSource,
                sourceRange,
                LegacyPromotionNinjaAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsLegacyNinjaStarterPromotionEligible(WorldSession session)
    {
        return session.Character.Level >= LegacyPromotionNinjaStarterLevelGate &&
            session.Character.Class == LegacyPromotionNinjaInternClass;
    }

    private static LegacyNinjaAdvancedPromotionBranch GetLegacyNinjaAdvancedPromotionBranch(long classCode)
    {
        return classCode switch
        {
            LegacyPromotionNinjaRankOneClass => new LegacyNinjaAdvancedPromotionBranch(
                LegacyPromotionNinjaRankOneClass,
                RequiredLevel: 40,
                RequiredItemId: 0,
                RewardItemId: 0,
                MissingItemText: "",
                SuccessText: "Excellent! You`ve learned ways of the Ninja. Do you feel a different energy in your body? That`s stronger power and battle prowess!",
                AvatarId: LegacyPromotionNinjaAvatarId),
            LegacyPromotionNinjaRankTwoClass => new LegacyNinjaAdvancedPromotionBranch(
                LegacyPromotionNinjaRankTwoClass,
                RequiredLevel: 70,
                RequiredItemId: LegacyPromotionEmeraldItemId,
                RewardItemId: 0,
                MissingItemText: LegacyPromotionNinjaMissingEmeraldText,
                SuccessText: "Excellent! You`ve learned ways of the Taoist. Do you feel a different energy in your body? That`s stronger power and battle prowess!",
                AvatarId: 6),
            LegacyPromotionNinjaRankThreeClass => new LegacyNinjaAdvancedPromotionBranch(
                LegacyPromotionNinjaRankThreeClass,
                RequiredLevel: 100,
                RequiredItemId: LegacyPromotionMeteorItemId,
                RewardItemId: 0,
                MissingItemText: LegacyPromotionNinjaMissingMeteorText,
                SuccessText: "Excellent! Do you feel a different energy in your body? That`s stronger power and battle prowess!",
                AvatarId: 6),
            LegacyPromotionNinjaRankFourClass => new LegacyNinjaAdvancedPromotionBranch(
                LegacyPromotionNinjaRankFourClass,
                RequiredLevel: 110,
                RequiredItemId: LegacyPromotionMoonBoxItemId,
                RewardItemId: LegacyPromotionDragonBallItemId,
                MissingItemText: LegacyPromotionNinjaMissingMoonBoxText,
                SuccessText: "Excellent!Do you feel a different energy in your body? That`s stronger power and battle prowess!",
                AvatarId: 6),
            _ => default,
        };
    }

    private readonly record struct LegacyNinjaAdvancedPromotionBranch(
        long RequiredClass,
        long RequiredLevel,
        uint RequiredItemId,
        uint RewardItemId,
        string MissingItemText,
        string SuccessText,
        ushort AvatarId);
}
