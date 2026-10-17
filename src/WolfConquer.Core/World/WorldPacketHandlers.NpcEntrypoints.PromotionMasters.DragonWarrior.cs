using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyPromotionDragonWarriorNpcId = 17_126;
    private const string LegacyPromotionDragonWarriorSource = "NpcHandler.PromotionLeeLong";
    private const ushort LegacyPromotionDragonWarriorAvatarId = 259;
    private const ushort LegacyPromotionDragonWarriorSkillAvatarId = 8;
    private const long LegacyPromotionDragonWarriorSkillLevelGate = 100;

    private const string LegacyPromotionDragonWarriorQuestFinishSourceRange = "NpcHandler.cs:49693-49728";
    private const string LegacyPromotionDragonWarriorAdvancedSourceRange = "NpcHandler.cs:49729-50040";
    private const string LegacyPromotionDragonWarriorAirStrikeSourceRange = "NpcHandler.cs:50041-50079";
    private const string LegacyPromotionDragonWarriorEarthSweepSourceRange = "NpcHandler.cs:50080-50118";
    private const string LegacyPromotionDragonWarriorKickSourceRange = "NpcHandler.cs:50119-50157";
    private const string LegacyPromotionDragonWarriorStormKickSourceRange = "NpcHandler.cs:50158-50196";
    private const string LegacyPromotionDragonWarriorViolentKickSourceRange = "NpcHandler.cs:50197-50235";
    private const string LegacyPromotionDragonWarriorSpeedKickSourceRange = "NpcHandler.cs:50236-50274";
    private const string LegacyPromotionDragonWarriorDragonSwingSourceRange = "NpcHandler.cs:50275-50313";
    private const string LegacyPromotionDragonWarriorDragonRoarSourceRange = "NpcHandler.cs:50314-50352";
    private const string LegacyPromotionDragonWarriorDragonFurySourceRange = "NpcHandler.cs:49920-49939";
    private const string LegacyPromotionDragonWarriorDragonSlashSourceRange = "NpcHandler.cs:50353-50391";
    private const string LegacyPromotionDragonWarriorSplittingSwipeSourceRange = "NpcHandler.cs:50392-50430";
    private const string LegacyPromotionDragonWarriorCrackingSwipeSourceRange = "NpcHandler.cs:50431-50469";
    private const string LegacyPromotionDragonWarriorDragonFlowSourceRange = "NpcHandler.cs:50470-50508";
    private const string LegacyPromotionDragonWarriorAirSweepSourceRange = "NpcHandler.cs:50509-50547";
    private const string LegacyPromotionDragonWarriorAirRaidSourceRange = "NpcHandler.cs:50548-50586";
    private const string LegacyPromotionDragonWarriorAirKickSourceRange = "NpcHandler.cs:50587-50625";
    private const string LegacyPromotionDragonWarriorDragonCycloneSourceRange = "NpcHandler.cs:50626-50664";
    private const string LegacyPromotionDragonWarriorDragonPunchSourceRange = "NpcHandler.cs:50665-50703";
    private const string LegacyPromotionDragonWarriorStarterSourceRange = "NpcHandler.cs:50704-50751";

    private const byte LegacyPromotionDragonWarriorStarterOption = 3;
    private const byte LegacyPromotionDragonWarriorAdvancedOption = 4;
    private const byte LegacyPromotionDragonWarriorQuestFinishOption = 111;
    private const byte LegacyPromotionDragonWarriorDragonPunchOption = 6;
    private const byte LegacyPromotionDragonWarriorDragonCycloneOption = 7;
    private const byte LegacyPromotionDragonWarriorAirKickOption = 8;
    private const byte LegacyPromotionDragonWarriorAirRaidOption = 9;
    private const byte LegacyPromotionDragonWarriorAirSweepOption = 10;
    private const byte LegacyPromotionDragonWarriorDragonFlowOption = 11;
    private const byte LegacyPromotionDragonWarriorCrackingSwipeOption = 12;
    private const byte LegacyPromotionDragonWarriorSplittingSwipeOption = 13;
    private const byte LegacyPromotionDragonWarriorDragonSlashOption = 14;
    private const byte LegacyPromotionDragonWarriorDragonRoarOption = 16;
    private const byte LegacyPromotionDragonWarriorDragonSwingOption = 17;
    private const byte LegacyPromotionDragonWarriorSpeedKickOption = 18;
    private const byte LegacyPromotionDragonWarriorViolentKickOption = 19;
    private const byte LegacyPromotionDragonWarriorStormKickOption = 20;
    private const byte LegacyPromotionDragonWarriorAirStrikeOption = 121;
    private const byte LegacyPromotionDragonWarriorEarthSweepOption = 122;
    private const byte LegacyPromotionDragonWarriorKickOption = 123;
    private const byte LegacyPromotionDragonWarriorDragonFuryOption = 31;

    private const long LegacyPromotionDragonWarriorStarterLevelGate = 15;
    private const long LegacyPromotionDragonWarriorInternClass = 8_000;
    private const long LegacyPromotionDragonWarriorFirstPromotionClass = 8_001;
    private const long LegacyPromotionDragonWarriorSecondPromotionClass = 8_002;
    private const long LegacyPromotionDragonWarriorThirdPromotionClass = 8_003;
    private const long LegacyPromotionDragonWarriorFourthPromotionClass = 8_004;
    private const long LegacyPromotionDragonWarriorExpertLevelGate = 40;
    private const long LegacyPromotionDragonWarriorLevel70Gate = 70;
    private const long LegacyPromotionDragonWarriorLevel100Gate = 100;
    private const long LegacyPromotionDragonWarriorLevel110Gate = 110;
    private const ushort LegacyPromotionDragonWarriorSpeedKickSpellId = 12_120;
    private const ushort LegacyPromotionDragonWarriorViolentKickSpellId = 12_130;
    private const ushort LegacyPromotionDragonWarriorStormKickSpellId = 12_140;
    private const ushort LegacyPromotionDragonWarriorCrackingSwipeSpellId = 12_160;
    private const ushort LegacyPromotionDragonWarriorSplittingSwipeSpellId = 12_170;
    private const ushort LegacyPromotionDragonWarriorDragonSwingSpellId = 12_200;
    private const ushort LegacyPromotionDragonWarriorAirStrikeSpellId = 12_210;
    private const ushort LegacyPromotionDragonWarriorEarthSweepSpellId = 12_220;
    private const ushort LegacyPromotionDragonWarriorKickSpellId = 12_230;
    private const ushort LegacyPromotionDragonWarriorDragonPunchSpellId = 12_240;
    private const ushort LegacyPromotionDragonWarriorDragonFlowSpellId = 12_270;
    private const ushort LegacyPromotionDragonWarriorDragonRoarSpellId = 12_280;
    private const ushort LegacyPromotionDragonWarriorDragonCycloneSpellId = 12_290;
    private const ushort LegacyPromotionDragonWarriorAirKickSpellId = 12_320;
    private const ushort LegacyPromotionDragonWarriorAirSweepSpellId = 12_330;
    private const ushort LegacyPromotionDragonWarriorAirRaidSpellId = 12_340;
    private const ushort LegacyPromotionDragonWarriorDragonSlashSpellId = 12_350;
    private const ushort LegacyPromotionDragonWarriorDragonFurySpellId = 12_300;
    private const ushort LegacyPromotionDragonWarriorDragonFuryAlreadyLearnedAvatarId = 5;
    private const uint LegacyPromotionDragonWarriorQuestFinishMissionId = 5;
    private const uint LegacyPromotionDragonWarriorQuestFinishRewardItemId = 3_005_160;

    private static readonly ushort[] LegacyPromotionDragonWarriorStarterSpellIds =
    [
        LegacyPromotionDragonWarriorDragonPunchSpellId,
        LegacyPromotionDragonWarriorDragonCycloneSpellId,
        LegacyPromotionDragonWarriorDragonFlowSpellId,
        LegacyPromotionDragonWarriorAirRaidSpellId,
        LegacyPromotionDragonWarriorAirSweepSpellId,
        LegacyPromotionDragonWarriorAirKickSpellId,
    ];

    private const string LegacyPromotionDragonWarriorStarterSpellIdsLog = "12240,12290,12270,12340,12330,12320";

    private static ValueTask<bool> TryHandleLegacyDragonWarriorPromotionSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return optionId switch
        {
            LegacyPromotionDragonWarriorStarterOption => TryHandleLegacyDragonWarriorStarterPromotionAsync(
                context,
                session,
                optionId,
                cancellationToken),
            LegacyPromotionDragonWarriorAdvancedOption => TryHandleLegacyDragonWarriorAdvancedPromotionAsync(
                context,
                session,
                optionId,
                cancellationToken),
            LegacyPromotionDragonWarriorQuestFinishOption => TryHandleLegacyDragonWarriorQuestFinishOptionAsync(
                context,
                session,
                optionId,
                cancellationToken),
            LegacyPromotionDragonWarriorDragonFuryOption => TryHandleLegacyDragonWarriorPureSkillAsync(
                context,
                session,
                optionId,
                cancellationToken),
            LegacyPromotionDragonWarriorDragonPunchOption => TryLearnLegacyDragonWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionDragonWarriorDragonPunchSpellId,
                "DragonPunch",
                LegacyPromotionDragonWarriorDragonPunchSourceRange,
                cancellationToken),
            LegacyPromotionDragonWarriorDragonCycloneOption => TryLearnLegacyDragonWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionDragonWarriorDragonCycloneSpellId,
                "DragonCyclone",
                LegacyPromotionDragonWarriorDragonCycloneSourceRange,
                cancellationToken),
            LegacyPromotionDragonWarriorAirKickOption => TryLearnLegacyDragonWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionDragonWarriorAirKickSpellId,
                "AirKick",
                LegacyPromotionDragonWarriorAirKickSourceRange,
                cancellationToken),
            LegacyPromotionDragonWarriorAirRaidOption => TryLearnLegacyDragonWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionDragonWarriorAirRaidSpellId,
                "AirRaid",
                LegacyPromotionDragonWarriorAirRaidSourceRange,
                cancellationToken),
            LegacyPromotionDragonWarriorAirSweepOption => TryLearnLegacyDragonWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionDragonWarriorAirSweepSpellId,
                "AirSweep",
                LegacyPromotionDragonWarriorAirSweepSourceRange,
                cancellationToken),
            LegacyPromotionDragonWarriorDragonFlowOption => TryLearnLegacyDragonWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionDragonWarriorDragonFlowSpellId,
                "DragonFlow",
                LegacyPromotionDragonWarriorDragonFlowSourceRange,
                cancellationToken),
            LegacyPromotionDragonWarriorCrackingSwipeOption => TryLearnLegacyDragonWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionDragonWarriorCrackingSwipeSpellId,
                "CrackingSwipe",
                LegacyPromotionDragonWarriorCrackingSwipeSourceRange,
                cancellationToken),
            LegacyPromotionDragonWarriorSplittingSwipeOption => TryLearnLegacyDragonWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionDragonWarriorSplittingSwipeSpellId,
                "SplittingSwipe",
                LegacyPromotionDragonWarriorSplittingSwipeSourceRange,
                cancellationToken),
            LegacyPromotionDragonWarriorDragonSlashOption => TryLearnLegacyDragonWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionDragonWarriorDragonSlashSpellId,
                "DragonSlash",
                LegacyPromotionDragonWarriorDragonSlashSourceRange,
                cancellationToken),
            LegacyPromotionDragonWarriorDragonRoarOption => TryLearnLegacyDragonWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionDragonWarriorDragonRoarSpellId,
                "DragonRoar",
                LegacyPromotionDragonWarriorDragonRoarSourceRange,
                cancellationToken),
            LegacyPromotionDragonWarriorDragonSwingOption => TryLearnLegacyDragonWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionDragonWarriorDragonSwingSpellId,
                "DragonSwing",
                LegacyPromotionDragonWarriorDragonSwingSourceRange,
                cancellationToken),
            LegacyPromotionDragonWarriorSpeedKickOption => TryLearnLegacyDragonWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionDragonWarriorSpeedKickSpellId,
                "SpeedKick",
                LegacyPromotionDragonWarriorSpeedKickSourceRange,
                cancellationToken),
            LegacyPromotionDragonWarriorViolentKickOption => TryLearnLegacyDragonWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionDragonWarriorViolentKickSpellId,
                "ViolentKick",
                LegacyPromotionDragonWarriorViolentKickSourceRange,
                cancellationToken),
            LegacyPromotionDragonWarriorStormKickOption => TryLearnLegacyDragonWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionDragonWarriorStormKickSpellId,
                "StormKick",
                LegacyPromotionDragonWarriorStormKickSourceRange,
                cancellationToken),
            LegacyPromotionDragonWarriorAirStrikeOption => TryLearnLegacyDragonWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionDragonWarriorAirStrikeSpellId,
                "AirStrike",
                LegacyPromotionDragonWarriorAirStrikeSourceRange,
                cancellationToken),
            LegacyPromotionDragonWarriorEarthSweepOption => TryLearnLegacyDragonWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionDragonWarriorEarthSweepSpellId,
                "EarthSweep",
                LegacyPromotionDragonWarriorEarthSweepSourceRange,
                cancellationToken),
            LegacyPromotionDragonWarriorKickOption => TryLearnLegacyDragonWarriorPromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionDragonWarriorKickSpellId,
                "Kick",
                LegacyPromotionDragonWarriorKickSourceRange,
                cancellationToken),
            _ => ValueTask.FromResult(false),
        };
    }

    private static async ValueTask<bool> TryHandleLegacyDragonWarriorQuestFinishOptionAsync(
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
                    LegacyPromotionDragonWarriorSource,
                    LegacyPromotionDragonWarriorQuestFinishSourceRange,
                    "NpcHandler.cs:49655",
                    "GetFinishQuest(5, class, 6426)",
                    "PromotionLeeLong",
                    LegacyPromotionDragonWarriorQuestFinishMissionId,
                    LegacyPromotionDragonWarriorQuestFinishRewardItemId),
                cancellationToken)
            .ConfigureAwait(false);

        await SendLegacyNpcDialogAsync(
                context with { Session = activeSession },
                LegacyPromotionDragonWarriorSource,
                [
                    "Wow, look at you, you`ll be another hero in the world of Conquer! Here, you can learn some skills and get promoted! Do you want to learn more?",
                    "~get promoted here to start your life. Would you like to learn something about your class promotion and quests?.",
                ],
                [new LegacyNpcDialogOption("Job~Promotion~2.", LegacyPromotionDragonWarriorStarterOption)],
                avatarId: LegacyPromotionDragonWarriorAvatarId,
                cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyDragonWarriorPureSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (session.Character.Reborn < 2)
        {
            await SendLegacyDragonWarriorPureSkillFailureAsync(context, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionLeeLong pure skill " +
                    $"{FormatSession(context)} source={LegacyPromotionDragonWarriorSource} " +
                    $"sourceRange={LegacyPromotionDragonWarriorDragonFurySourceRange} option={optionId} " +
                    $"reborn={session.Character.Reborn} requiredReborn=2; source reborn gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (!IsLegacyDragonWarriorClass(session.Character.FirstClass) ||
            !IsLegacyDragonWarriorClass(session.Character.SecondClass))
        {
            await SendLegacyDragonWarriorPureSkillFailureAsync(context, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionLeeLong pure skill " +
                    $"{FormatSession(context)} source={LegacyPromotionDragonWarriorSource} " +
                    $"sourceRange={LegacyPromotionDragonWarriorDragonFurySourceRange} option={optionId} " +
                    $"firstClass={session.Character.FirstClass} secondClass={session.Character.SecondClass}; " +
                    "source pure Dragon-Warrior combo gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (session.PlayerSpells.ContainsSpell(LegacyPromotionDragonWarriorDragonFurySpellId))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionDragonWarriorSource,
                    ["You`ve learned DragonFury!"],
                    [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                    LegacyPromotionDragonWarriorDragonFuryAlreadyLearnedAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionLeeLong pure skill " +
                    $"{FormatSession(context)} source={LegacyPromotionDragonWarriorSource} " +
                    $"sourceRange={LegacyPromotionDragonWarriorDragonFurySourceRange} option={optionId} " +
                    $"spellId={LegacyPromotionDragonWarriorDragonFurySpellId}; already learned.")
                .ConfigureAwait(false);
            return true;
        }

        var learned = CreateLegacyPromotionSpellEntry(LegacyPromotionDragonWarriorDragonFurySpellId);
        var updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(session.PlayerSpells, learned);
        var updatedSession = await ApplyLegacyPromotionMasterSpellsAsync(
                context,
                session,
                updatedSpells,
                optionId,
                LegacyPromotionDragonWarriorSource,
                LegacyPromotionDragonWarriorDragonFurySourceRange,
                LegacyPromotionDragonWarriorDragonFurySpellId.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ConfigureAwait(false);

        var updatedContext = context with { Session = updatedSession };
        await context.Connection.SendAsync(LegacySpellPacket.FromSnapshot(learned).ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyPromotionDragonWarriorSource,
                ["Congratulation! You just have learned the skill DragonFury"],
                [new LegacyNpcDialogOption("Ok , thank you.", LegacyNpcClosedOption)],
                LegacyPromotionDragonWarriorAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionLeeLong pure skill " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionDragonWarriorSource} " +
                $"sourceRange={LegacyPromotionDragonWarriorDragonFurySourceRange} option={optionId} " +
                $"spellId={LegacyPromotionDragonWarriorDragonFurySpellId}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask SendLegacyDragonWarriorPureSkillFailureAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionDragonWarriorSource,
                ["To learn [Dragon Fury], you should be Dragon-Warrior for three lives in a row and reach at least Level 70."],
                [new LegacyNpcDialogOption("OK", LegacyNpcClosedOption)],
                LegacyPromotionDragonWarriorAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryHandleLegacyDragonWarriorAdvancedPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        switch (session.Character.Class)
        {
            case LegacyPromotionDragonWarriorFirstPromotionClass:
                if (session.Character.Level >= LegacyPromotionDragonWarriorStarterLevelGate &&
                    session.Character.Level < LegacyPromotionDragonWarriorExpertLevelGate)
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            LegacyPromotionDragonWarriorSource,
                            [
                                "`Dragon-Warrior` can get promoted to be `Expert Dragon-Warrior` at Level 40.",
                                "You should have reached Level 40 to get promoted. Keep practicing!.",
                            ],
                            [new LegacyNpcDialogOption("I`ll~think~about~it.", LegacyNpcClosedOption)],
                            LegacyPromotionDragonWarriorAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                    await context.Log.WriteLineAsync(
                            "WorldServer skipped LegacyNpc PromotionLeeLong advanced promotion " +
                            $"{FormatSession(context)} source={LegacyPromotionDragonWarriorSource} " +
                            $"sourceRange={LegacyPromotionDragonWarriorAdvancedSourceRange} option={optionId} " +
                            $"level={session.Character.Level} requiredLevel={LegacyPromotionDragonWarriorExpertLevelGate} " +
                            $"class={session.Character.Class}; source promotion gate rejected.")
                        .ConfigureAwait(false);
                    return true;
                }

                if (session.Character.Level < LegacyPromotionDragonWarriorExpertLevelGate)
                {
                    return await SendLegacyDragonWarriorAdvancedLevelGateAsync(
                            context,
                            session,
                            optionId,
                            LegacyPromotionDragonWarriorExpertLevelGate,
                            LegacyPromotionDragonWarriorAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                return await ApplyLegacyDragonWarriorAdvancedPromotionAsync(
                        context,
                        session,
                        optionId,
                        "Excellent! You`ve learned ways of the Dragon-Warrior. Do you feel a different energy in your body? That`s stronger power and battle prowess!",
                        LegacyPromotionDragonWarriorAvatarId,
                        "noItem",
                        cancellationToken)
                    .ConfigureAwait(false);

            case LegacyPromotionDragonWarriorSecondPromotionClass:
                if (session.Character.Level < LegacyPromotionDragonWarriorLevel70Gate)
                {
                    return await SendLegacyDragonWarriorAdvancedLevelGateAsync(
                            context,
                            session,
                            optionId,
                            LegacyPromotionDragonWarriorLevel70Gate,
                            avatarId: 6,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                return await TryApplyLegacyDragonWarriorItemPromotionAsync(
                        context,
                        session,
                        optionId,
                        LegacyPromotionEmeraldItemId,
                        "I'm sorry but you need to have 1 Emerald at your inventory , to get one go and kill some HillMonsters ( Desert City ) to get one .",
                        "Excellent! You`ve learned ways of the Taoist. Do you feel a different energy in your body? That`s stronger power and battle prowess!",
                        cancellationToken)
                    .ConfigureAwait(false);

            case LegacyPromotionDragonWarriorThirdPromotionClass:
                if (session.Character.Level < LegacyPromotionDragonWarriorLevel100Gate)
                {
                    return await SendLegacyDragonWarriorAdvancedLevelGateAsync(
                            context,
                            session,
                            optionId,
                            LegacyPromotionDragonWarriorLevel100Gate,
                            avatarId: 6,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                return await TryApplyLegacyDragonWarriorItemPromotionAsync(
                        context,
                        session,
                        optionId,
                        LegacyPromotionMeteorItemId,
                        "I'm sorry but you need to have 1 Meteor at your inventory.",
                        "Excellent! Do you feel a different energy in your body? That`s stronger power and battle prowess!",
                        cancellationToken)
                    .ConfigureAwait(false);

            case LegacyPromotionDragonWarriorFourthPromotionClass:
                if (session.Character.Level < LegacyPromotionDragonWarriorLevel110Gate)
                {
                    return await SendLegacyDragonWarriorAdvancedLevelGateAsync(
                            context,
                            session,
                            optionId,
                            LegacyPromotionDragonWarriorLevel110Gate,
                            avatarId: 6,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                return await TryApplyLegacyDragonWarriorMoonBoxPromotionAsync(
                        context,
                        session,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);

            default:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyPromotionDragonWarriorSource,
                        ["You cannot be promoted anymore. You have mastered your class"],
                        [new LegacyNpcDialogOption("Thank~you,~Master.", LegacyNpcClosedOption)],
                        avatarId: 6,
                        cancellationToken)
                    .ConfigureAwait(false);
                await context.Log.WriteLineAsync(
                        "WorldServer skipped LegacyNpc PromotionLeeLong advanced promotion " +
                        $"{FormatSession(context)} source={LegacyPromotionDragonWarriorSource} " +
                        $"sourceRange={LegacyPromotionDragonWarriorAdvancedSourceRange} option={optionId} " +
                        $"class={session.Character.Class}; source class has no advanced promotion branch.")
                    .ConfigureAwait(false);
                return true;
        }
    }

    private static async ValueTask<bool> SendLegacyDragonWarriorAdvancedLevelGateAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        long requiredLevel,
        ushort avatarId,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionDragonWarriorSource,
                [$"There is much that you must learn before you can accept my training. Slaughter a legion of monsters first! [Level {requiredLevel}+]"],
                [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
                avatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc PromotionLeeLong advanced promotion " +
                $"{FormatSession(context)} source={LegacyPromotionDragonWarriorSource} " +
                $"sourceRange={LegacyPromotionDragonWarriorAdvancedSourceRange} option={optionId} " +
                $"level={session.Character.Level} requiredLevel={requiredLevel} class={session.Character.Class}; " +
                "source level gate rejected.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryApplyLegacyDragonWarriorItemPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        uint itemId,
        string missingItemText,
        string successText,
        CancellationToken cancellationToken)
    {
        if (!HasLegacyInventoryStackCount(session.PlayerItems, itemId, 1))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionDragonWarriorSource,
                    [missingItemText],
                    [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
                    avatarId: 6,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionLeeLong advanced promotion " +
                    $"{FormatSession(context)} source={LegacyPromotionDragonWarriorSource} " +
                    $"sourceRange={LegacyPromotionDragonWarriorAdvancedSourceRange} option={optionId} " +
                    $"class={session.Character.Class} itemId={itemId}; source item missing.")
                .ConfigureAwait(false);
            return true;
        }

        var mutationFrames = new List<byte[]>();
        var updatedItems = ConsumeLegacyInventoryStack(session.PlayerItems, itemId, 1, mutationFrames);
        var updatedSession = await ApplyLegacyPromotionMasterItemsAsync(
                context,
                session,
                updatedItems,
                optionId,
                LegacyPromotionDragonWarriorSource,
                LegacyPromotionDragonWarriorAdvancedSourceRange,
                $"consumeItemId={itemId} consumeCount=1")
            .ConfigureAwait(false);
        await SendLegacyPromotionMasterItemFramesAsync(context, mutationFrames, cancellationToken)
            .ConfigureAwait(false);

        return await ApplyLegacyDragonWarriorAdvancedPromotionAsync(
                context with { Session = updatedSession },
                updatedSession,
                optionId,
                successText,
                avatarId: 6,
                $"consumeItemId={itemId} consumeCount=1",
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryApplyLegacyDragonWarriorMoonBoxPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (!HasLegacyInventoryStackCount(session.PlayerItems, LegacyPromotionMoonBoxItemId, 1))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionDragonWarriorSource,
                    ["I'm sorry but you need to have 1 MoonBox at your inventory."],
                    [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
                    avatarId: 6,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionLeeLong advanced promotion " +
                    $"{FormatSession(context)} source={LegacyPromotionDragonWarriorSource} " +
                    $"sourceRange={LegacyPromotionDragonWarriorAdvancedSourceRange} option={optionId} " +
                    $"class={session.Character.Class} itemId={LegacyPromotionMoonBoxItemId}; source item missing.")
                .ConfigureAwait(false);
            return true;
        }

        if (!TryReadItemType(context.WorldData, LegacyPromotionDragonBallItemId, out var dragonBallType))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc PromotionLeeLong advanced promotion missing DragonBall item type " +
                    $"{FormatSession(context)} source={LegacyPromotionDragonWarriorSource} " +
                    $"sourceRange={LegacyPromotionDragonWarriorAdvancedSourceRange} option={optionId} " +
                    $"rewardItemId={LegacyPromotionDragonBallItemId}; promotion not modeled.")
                .ConfigureAwait(false);
            return true;
        }

        var mutationFrames = new List<byte[]>();
        var updatedItems = ConsumeLegacyInventoryStack(
            session.PlayerItems,
            LegacyPromotionMoonBoxItemId,
            1,
            mutationFrames);
        var dragonBall = CreatePurchasedItems(updatedItems, dragonBallType, count: 1)[0];
        updatedItems = updatedItems with
        {
            InventoryItems = updatedItems.InventoryItems.Concat([dragonBall]).OrderBy(item => item.Uid).ToArray(),
        };

        var updatedSession = await ApplyLegacyPromotionMasterItemsAsync(
                context,
                session,
                updatedItems,
                optionId,
                LegacyPromotionDragonWarriorSource,
                LegacyPromotionDragonWarriorAdvancedSourceRange,
                $"consumeItemId={LegacyPromotionMoonBoxItemId} consumeCount=1 rewardItemId={LegacyPromotionDragonBallItemId} rewardCount=1")
            .ConfigureAwait(false);
        await SendLegacyPromotionMasterItemFramesAsync(context, mutationFrames, cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(dragonBall).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk("You received DragonBall.").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        return await ApplyLegacyDragonWarriorAdvancedPromotionAsync(
                context with { Session = updatedSession },
                updatedSession,
                optionId,
                "Excellent!Do you feel a different energy in your body? That`s stronger power and battle prowess!",
                avatarId: 6,
                $"consumeItemId={LegacyPromotionMoonBoxItemId} consumeCount=1 rewardItemId={LegacyPromotionDragonBallItemId} rewardCount=1",
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> ApplyLegacyDragonWarriorAdvancedPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string successText,
        ushort avatarId,
        string mutationSummary,
        CancellationToken cancellationToken)
    {
        var previousClass = session.Character.Class;
        var updatedCharacter = session.Character with
        {
            Class = checked(session.Character.Class + 1),
        };
        var updatedSession = await ApplyLegacyPromotionCharacterAsync(
                context,
                session,
                updatedCharacter,
                optionId,
                LegacyPromotionDragonWarriorSource,
                LegacyPromotionDragonWarriorAdvancedSourceRange)
            .ConfigureAwait(false);
        var updatedContext = context with { Session = updatedSession };
        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyPromotionDragonWarriorSource,
                [successText],
                [new LegacyNpcDialogOption("Thank~you,~Master.", LegacyNpcClosedOption)],
                avatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionLeeLong advanced promotion " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionDragonWarriorSource} " +
                $"sourceRange={LegacyPromotionDragonWarriorAdvancedSourceRange} option={optionId} " +
                $"previousClass={previousClass} newClass={updatedCharacter.Class} {mutationSummary}.")
            .ConfigureAwait(false);
        return true;
    }

    private static ValueTask<bool> TryLearnLegacyDragonWarriorPromotionSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        ushort spellId,
        string spellName,
        string sourceRange,
        CancellationToken cancellationToken)
    {
        return TryLearnLegacyPromotionMasterSkillAsync(
            context,
            session,
            optionId,
            spellId,
            spellName,
            LegacyPromotionDragonWarriorSkillLevelGate,
            LegacyPromotionDragonWarriorSource,
            sourceRange,
            LegacyPromotionDragonWarriorSkillAvatarId,
            cancellationToken);
    }

    private static async ValueTask<bool> TryHandleLegacyDragonWarriorStarterPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (!IsLegacyDragonWarriorStarterPromotionEligible(session))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionDragonWarriorSource,
                    ["Sorry, you need level 15."],
                    [new LegacyNpcDialogOption("Ah ok.", LegacyNpcClosedOption)],
                    LegacyPromotionDragonWarriorAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionLeeLong starter promotion " +
                    $"{FormatSession(context)} source={LegacyPromotionDragonWarriorSource} " +
                    $"sourceRange={LegacyPromotionDragonWarriorStarterSourceRange} option={optionId} " +
                    $"level={session.Character.Level} class={session.Character.Class}; source promotion gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        var learnedSpells = new List<PlayerSpellSnapshotEntry>(capacity: LegacyPromotionDragonWarriorStarterSpellIds.Length);
        var updatedSpells = session.PlayerSpells;
        foreach (var spellId in LegacyPromotionDragonWarriorStarterSpellIds)
        {
            if (updatedSpells.ContainsSpell(spellId))
            {
                continue;
            }

            var learned = CreateLegacyPromotionSpellEntry(spellId);
            learnedSpells.Add(learned);
            updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(updatedSpells, learned);
        }

        var updatedSession = await ApplyLegacyPromotionMasterSpellsAsync(
                context,
                session,
                updatedSpells,
                optionId,
                LegacyPromotionDragonWarriorSource,
                LegacyPromotionDragonWarriorStarterSourceRange,
                LegacyPromotionDragonWarriorStarterSpellIdsLog)
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
                LegacyPromotionDragonWarriorSource,
                LegacyPromotionDragonWarriorStarterSourceRange)
            .ConfigureAwait(false);

        var updatedContext = context with { Session = updatedSession };
        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyPromotionDragonWarriorSource,
                ["You`ve been promoted!"],
                [new LegacyNpcDialogOption("Great,~thanks!", LegacyNpcClosedOption)],
                LegacyPromotionDragonWarriorAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionLeeLong starter promotion " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionDragonWarriorSource} " +
                $"sourceRange={LegacyPromotionDragonWarriorStarterSourceRange} option={optionId} " +
                $"spellIds={LegacyPromotionDragonWarriorStarterSpellIdsLog}.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool IsLegacyDragonWarriorStarterPromotionEligible(WorldSession session)
    {
        return session.Character.Level >= LegacyPromotionDragonWarriorStarterLevelGate &&
            session.Character.Class == LegacyPromotionDragonWarriorInternClass;
    }
}
