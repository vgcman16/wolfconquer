using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyPromotionArcherNpcId = 400;
    private const string LegacyPromotionArcherSource = "NpcHandler.PromotionArcher";
    private const ushort LegacyPromotionArcherAvatarId = 10;
    private const ushort LegacyPromotionArcherSkillAvatarId = 8;
    private const long LegacyPromotionArcherSkillLevelGate = 100;

    private const string LegacyPromotionArcherAdvancedSourceRange = "NpcHandler.cs:48780-48982";
    private const string LegacyPromotionArcherQuestFinishSourceRange = "NpcHandler.cs:48729-48779";
    private const string LegacyPromotionArcherStarterSourceRange = "NpcHandler.cs:48983-49021";
    private const string LegacyPromotionArcherStarArrowSourceRange = "NpcHandler.cs:49029-49048";
    private const string LegacyPromotionArcherDaggerStormSourceRange = "NpcHandler.cs:49113-49151";
    private const string LegacyPromotionArcherSpiritFocusSourceRange = "NpcHandler.cs:49152-49190";
    private const string LegacyPromotionArcherBlisteringWaveSourceRange = "NpcHandler.cs:49191-49229";
    private const string LegacyPromotionArcherKineticSparkSourceRange = "NpcHandler.cs:49230-49268";
    private const string LegacyPromotionArcherMortalWoundSourceRange = "NpcHandler.cs:49269-49307";
    private const string LegacyPromotionArcherBladeFlurrySourceRange = "NpcHandler.cs:49308-49346";
    private const string LegacyPromotionArcherPathOfShadowSourceRange = "NpcHandler.cs:49347-49385";
    private const string LegacyPromotionArcherXpFlySourceRange = "NpcHandler.cs:49408-49446";
    private const string LegacyPromotionArcherScatterFireSourceRange = "NpcHandler.cs:49447-49485";
    private const string LegacyPromotionArcherIntensifySourceRange = "NpcHandler.cs:49486-49524";
    private const string LegacyPromotionArcherArrowRainSourceRange = "NpcHandler.cs:49525-49563";
    private const string LegacyPromotionArcherFlySourceRange = "NpcHandler.cs:49564-49602";
    private const string LegacyPromotionArcherRapidFireSourceRange = "NpcHandler.cs:49603-49641";

    private const byte LegacyPromotionArcherStarterAcceptOption = 2;
    private const byte LegacyPromotionArcherQuestFinishOption = 111;
    private const byte LegacyPromotionArcherStarArrowOption = 31;
    private const byte LegacyPromotionArcherAdvancedPromotionOption = 33;
    private const byte LegacyPromotionArcherRapidFireOption = 13;
    private const byte LegacyPromotionArcherFlyOption = 14;
    private const byte LegacyPromotionArcherArrowRainOption = 15;
    private const byte LegacyPromotionArcherIntensifyOption = 16;
    private const byte LegacyPromotionArcherScatterFireOption = 17;
    private const byte LegacyPromotionArcherPathOfShadowOption = 20;
    private const byte LegacyPromotionArcherBladeFlurryOption = 21;
    private const byte LegacyPromotionArcherMortalWoundOption = 22;
    private const byte LegacyPromotionArcherKineticSparkOption = 23;
    private const byte LegacyPromotionArcherBlisteringWaveOption = 24;
    private const byte LegacyPromotionArcherSpiritFocusOption = 25;
    private const byte LegacyPromotionArcherDaggerStormOption = 26;
    private const byte LegacyPromotionArcherXpFlyOption = 70;

    private const long LegacyPromotionArcherStarterLevelGate = 15;
    private const long LegacyPromotionArcherInternClass = 4_000;
    private const long LegacyPromotionArcherClass = 4_001;
    private const long LegacyPromotionEagleArcherClass = 4_002;
    private const long LegacyPromotionTigerArcherClass = 4_003;
    private const long LegacyPromotionDragonArcherClass = 4_004;
    private const long LegacyPromotionArcherLevelGate = 40;
    private const long LegacyPromotionEagleArcherLevelGate = 70;
    private const long LegacyPromotionTigerArcherLevelGate = 100;
    private const long LegacyPromotionDragonArcherLevelGate = 110;
    private const uint LegacyPromotionArcherEuxeniteOreItemId = 1_072_031;
    private const uint LegacyPromotionArcherScarletBowItemId = 500_069;
    private const ushort LegacyPromotionArcherRapidFireSpellId = 8_000;
    private const ushort LegacyPromotionArcherScatterFireSpellId = 8_001;
    private const ushort LegacyPromotionArcherXpFlySpellId = 8_002;
    private const ushort LegacyPromotionArcherFlySpellId = 8_003;
    private const ushort LegacyPromotionArcherArrowRainSpellId = 8_030;
    private const ushort LegacyPromotionArcherIntensifySpellId = 9_000;
    private const ushort LegacyPromotionArcherKineticSparkSpellId = 11_590;
    private const ushort LegacyPromotionArcherDaggerStormSpellId = 11_600;
    private const ushort LegacyPromotionArcherBladeFlurrySpellId = 11_610;
    private const ushort LegacyPromotionArcherPathOfShadowSpellId = 11_620;
    private const ushort LegacyPromotionArcherBlisteringWaveSpellId = 11_650;
    private const ushort LegacyPromotionArcherMortalWoundSpellId = 11_660;
    private const ushort LegacyPromotionArcherSpiritFocusSpellId = 11_670;
    private const ushort LegacyPromotionArcherStarArrowSpellId = 10_313;
    private const uint LegacyPromotionArcherQuestFinishMissionId = 2_001;
    private const uint LegacyPromotionArcherQuestFinishRewardItemId = 3_005_160;

    private static ValueTask<bool> TryHandleLegacyArcherPromotionSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return optionId switch
        {
            LegacyPromotionArcherAdvancedPromotionOption => TryHandleLegacyArcherAdvancedPromotionAsync(
                context,
                session,
                optionId,
                cancellationToken),
            LegacyPromotionArcherStarterAcceptOption => TryHandleLegacyArcherStarterPromotionAsync(
                context,
                session,
                optionId,
                cancellationToken),
            LegacyPromotionArcherQuestFinishOption => TryHandleLegacyArcherQuestFinishOptionAsync(
                context,
                session,
                optionId,
                cancellationToken),
            LegacyPromotionArcherStarArrowOption => TryHandleLegacyArcherPureSkillAsync(
                context,
                session,
                optionId,
                cancellationToken),
            LegacyPromotionArcherXpFlyOption => TryLearnLegacyArcherSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionArcherXpFlySpellId,
                "XpFly",
                LegacyPromotionArcherXpFlySourceRange,
                cancellationToken),
            LegacyPromotionArcherRapidFireOption => TryLearnLegacyArcherSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionArcherRapidFireSpellId,
                "RapidFire",
                LegacyPromotionArcherRapidFireSourceRange,
                cancellationToken),
            LegacyPromotionArcherFlyOption => TryLearnLegacyArcherSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionArcherFlySpellId,
                "Fly",
                LegacyPromotionArcherFlySourceRange,
                cancellationToken),
            LegacyPromotionArcherArrowRainOption => TryLearnLegacyArcherSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionArcherArrowRainSpellId,
                "ArrowRain",
                LegacyPromotionArcherArrowRainSourceRange,
                cancellationToken),
            LegacyPromotionArcherIntensifyOption => TryLearnLegacyArcherSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionArcherIntensifySpellId,
                "Intensify",
                LegacyPromotionArcherIntensifySourceRange,
                cancellationToken),
            LegacyPromotionArcherScatterFireOption => TryLearnLegacyArcherSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionArcherScatterFireSpellId,
                "ScatterFire",
                LegacyPromotionArcherScatterFireSourceRange,
                cancellationToken),
            LegacyPromotionArcherPathOfShadowOption => TryLearnLegacyArcherSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionArcherPathOfShadowSpellId,
                "PathOfShadow",
                LegacyPromotionArcherPathOfShadowSourceRange,
                cancellationToken),
            LegacyPromotionArcherBladeFlurryOption => TryLearnLegacyArcherSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionArcherBladeFlurrySpellId,
                "BladeFlurry",
                LegacyPromotionArcherBladeFlurrySourceRange,
                cancellationToken),
            LegacyPromotionArcherMortalWoundOption => TryLearnLegacyArcherSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionArcherMortalWoundSpellId,
                "MortalWound",
                LegacyPromotionArcherMortalWoundSourceRange,
                cancellationToken),
            LegacyPromotionArcherKineticSparkOption => TryLearnLegacyArcherSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionArcherKineticSparkSpellId,
                "KineticSpark",
                LegacyPromotionArcherKineticSparkSourceRange,
                cancellationToken),
            LegacyPromotionArcherBlisteringWaveOption => TryLearnLegacyArcherSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionArcherBlisteringWaveSpellId,
                "BlisteringWave",
                LegacyPromotionArcherBlisteringWaveSourceRange,
                cancellationToken),
            LegacyPromotionArcherSpiritFocusOption => TryLearnLegacyArcherSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionArcherSpiritFocusSpellId,
                "SpiritFocus",
                LegacyPromotionArcherSpiritFocusSourceRange,
                cancellationToken),
            LegacyPromotionArcherDaggerStormOption => TryLearnLegacyArcherSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionArcherDaggerStormSpellId,
                "DaggerStorm",
                LegacyPromotionArcherDaggerStormSourceRange,
                cancellationToken),
            _ => ValueTask.FromResult(false),
        };
    }

    private static async ValueTask<bool> TryHandleLegacyArcherQuestFinishOptionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var nextPromotionOption = session.Character.Class == LegacyPromotionArcherInternClass
            ? (byte)1
            : LegacyPromotionArcherAdvancedPromotionOption;

        var updatedSession = await TryApplyLegacyPromotionQuestFinishRewardAsync(
                context,
                session,
                optionId,
                new LegacyPromotionQuestFinishRewardSpec(
                    LegacyPromotionArcherSource,
                    LegacyPromotionArcherQuestFinishSourceRange,
                    "NpcHandler.cs:48693",
                    "GetFinishQuest(2001, class, map 1004)",
                    "PromotionArcher",
                    LegacyPromotionArcherQuestFinishMissionId,
                    LegacyPromotionArcherQuestFinishRewardItemId),
                cancellationToken)
            .ConfigureAwait(false);

        await SendLegacyNpcDialogAsync(
                context with { Session = updatedSession },
                LegacyPromotionArcherSource,
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

    private static async ValueTask<bool> TryHandleLegacyArcherAdvancedPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (session.Character.Class == LegacyPromotionArcherClass &&
            session.Character.Level >= LegacyPromotionArcherStarterLevelGate &&
            session.Character.Level < LegacyPromotionArcherLevelGate)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionArcherSource,
                    ["Sorry,~you~cannot~get~promoted~before~you~reach~level~40.~Please~train~harder."],
                    [new LegacyNpcDialogOption("I`ll~think~about~it.", LegacyNpcClosedOption)],
                    avatarId: 234,
                    cancellationToken)
                .ConfigureAwait(false);
            await LogLegacyArcherAdvancedPromotionSkippedAsync(
                    context,
                    optionId,
                    $"level={session.Character.Level} requiredLevel={LegacyPromotionArcherLevelGate}; source low-level gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        switch (session.Character.Class)
        {
            case LegacyPromotionArcherClass:
                return await TryHandleLegacyArcherAdvancedPromotionStageAsync(
                        context,
                        session,
                        optionId,
                        LegacyPromotionArcherLevelGate,
                        LegacyPromotionArcherEuxeniteOreItemId,
                        requiredItemCount: 5,
                        missingItemText: "I'm sorry but you need to have 5 EuxeniteOre in your inventory.",
                        successText: "Excellent! You`ve learned ways of the Archer. Do you feel a different energy in your body? That`s stronger power and battle prowess!",
                        avatarId: LegacyPromotionArcherAvatarId,
                        rewardItemId: session.Character.Reborn == 0 ? LegacyPromotionArcherScarletBowItemId : null,
                        rewardSystemTalk: session.Character.Reborn == 0 ? "You received 1 (Super)ScarletBow." : null,
                        requireRewardSpace: session.Character.Reborn == 0,
                        cancellationToken)
                    .ConfigureAwait(false);
            case LegacyPromotionEagleArcherClass:
                return await TryHandleLegacyArcherAdvancedPromotionStageAsync(
                        context,
                        session,
                        optionId,
                        LegacyPromotionEagleArcherLevelGate,
                        LegacyPromotionEmeraldItemId,
                        requiredItemCount: 1,
                        missingItemText: "I'm sorry but you need to have 1 Emerald at your inventory , to get one go and kill some HillMonsters ( Desert City ) to get one .",
                        successText: "Excellent! You`ve learned ways of the Taoist. Do you feel a different energy in your body? That`s stronger power and battle prowess!",
                        avatarId: 6,
                        rewardItemId: null,
                        rewardSystemTalk: null,
                        requireRewardSpace: false,
                        cancellationToken)
                    .ConfigureAwait(false);
            case LegacyPromotionTigerArcherClass:
                return await TryHandleLegacyArcherAdvancedPromotionStageAsync(
                        context,
                        session,
                        optionId,
                        LegacyPromotionTigerArcherLevelGate,
                        LegacyPromotionMeteorItemId,
                        requiredItemCount: 1,
                        missingItemText: "I'm sorry but you need to have 1 Meteor at your inventory.",
                        successText: "Excellent! Do you feel a different energy in your body? That`s stronger power and battle prowess!",
                        avatarId: 6,
                        rewardItemId: null,
                        rewardSystemTalk: null,
                        requireRewardSpace: false,
                        cancellationToken)
                    .ConfigureAwait(false);
            case LegacyPromotionDragonArcherClass:
                return await TryHandleLegacyArcherAdvancedPromotionStageAsync(
                        context,
                        session,
                        optionId,
                        LegacyPromotionDragonArcherLevelGate,
                        LegacyPromotionMoonBoxItemId,
                        requiredItemCount: 1,
                        missingItemText: "I'm sorry but you need to have 1 MoonBox at your inventory.",
                        successText: "Excellent!Do you feel a different energy in your body? That`s stronger power and battle prowess!",
                        avatarId: 6,
                        rewardItemId: LegacyPromotionDragonBallItemId,
                        rewardSystemTalk: "You received DragonBall.",
                        requireRewardSpace: false,
                        cancellationToken)
                    .ConfigureAwait(false);
            default:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyPromotionArcherSource,
                        ["You cannot be promoted anymore. You have mastered your class"],
                        [new LegacyNpcDialogOption("Thank~you,~Master.", LegacyNpcClosedOption)],
                        avatarId: 6,
                        cancellationToken)
                    .ConfigureAwait(false);
                await LogLegacyArcherAdvancedPromotionSkippedAsync(
                        context,
                        optionId,
                        $"class={session.Character.Class}; source class gate rejected.")
                    .ConfigureAwait(false);
                return true;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyArcherAdvancedPromotionStageAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        long requiredLevel,
        uint requiredItemId,
        uint requiredItemCount,
        string missingItemText,
        string successText,
        ushort avatarId,
        uint? rewardItemId,
        string? rewardSystemTalk,
        bool requireRewardSpace,
        CancellationToken cancellationToken)
    {
        if (session.Character.Level < requiredLevel)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionArcherSource,
                    [$"There is much that you must learn before you can accept my training. Slaughter a legion of monsters first! [Level {requiredLevel}+]"],
                    [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
                    avatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await LogLegacyArcherAdvancedPromotionSkippedAsync(
                    context,
                    optionId,
                    $"level={session.Character.Level} requiredLevel={requiredLevel}; source level gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (!HasLegacyInventoryStackCount(session.PlayerItems, requiredItemId, requiredItemCount))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionArcherSource,
                    [missingItemText],
                    [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
                    avatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await LogLegacyArcherAdvancedPromotionSkippedAsync(
                    context,
                    optionId,
                    $"itemId={requiredItemId} itemCount={requiredItemCount}; source item gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (requireRewardSpace && !HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox("Please make 1 more space in your inventory.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await LogLegacyArcherAdvancedPromotionSkippedAsync(
                    context,
                    optionId,
                    $"rewardItemId={rewardItemId} loadedInventoryItems={session.PlayerItems.InventoryItems.Count}; source inventory space gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        var mutationFrames = new List<byte[]>();
        var updatedItems = ConsumeLegacyInventoryStack(
            session.PlayerItems,
            requiredItemId,
            requiredItemCount,
            mutationFrames);
        var grantedItems = Array.Empty<PlayerItemSnapshotItem>();

        if (rewardItemId is { } itemId)
        {
            if (TryReadItemType(context.WorldData, itemId, out var itemType))
            {
                grantedItems = CreatePurchasedItems(updatedItems, itemType, count: 1).ToArray();
                updatedItems = updatedItems with
                {
                    InventoryItems = updatedItems.InventoryItems.Concat(grantedItems).OrderBy(item => item.Uid).ToArray(),
                };
            }
            else
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyNpc PromotionArcher advanced promotion reward item type missing " +
                        $"{FormatSession(context)} source={LegacyPromotionArcherSource} " +
                        $"sourceRange={LegacyPromotionArcherAdvancedSourceRange} option={optionId} " +
                        $"rewardItemId={itemId}; source would continue after item consumption and class promotion.")
                    .ConfigureAwait(false);
            }
        }

        var mutationSummary = rewardItemId is { } grantedItemId
            ? $"consumed itemId={requiredItemId} itemCount={requiredItemCount} rewardItemId={grantedItemId}"
            : $"consumed itemId={requiredItemId} itemCount={requiredItemCount}";
        var updatedSession = await ApplyLegacyPromotionMasterItemsAsync(
                context,
                session,
                updatedItems,
                optionId,
                LegacyPromotionArcherSource,
                LegacyPromotionArcherAdvancedSourceRange,
                mutationSummary)
            .ConfigureAwait(false);

        await SendLegacyPromotionMasterItemFramesAsync(context, mutationFrames, cancellationToken)
            .ConfigureAwait(false);

        foreach (var item in grantedItems)
        {
            await context.Connection.SendAsync(
                    LegacyGameItemPacket.FromSnapshotItem(item).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (grantedItems.Length > 0 && rewardSystemTalk is not null)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk(rewardSystemTalk).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var updatedContext = context with { Session = updatedSession };
        var updatedCharacter = updatedSession.Character with
        {
            Class = checked(updatedSession.Character.Class + 1),
        };
        updatedSession = await ApplyLegacyPromotionCharacterAsync(
                updatedContext,
                updatedSession,
                updatedCharacter,
                optionId,
                LegacyPromotionArcherSource,
                LegacyPromotionArcherAdvancedSourceRange)
            .ConfigureAwait(false);

        updatedContext = context with { Session = updatedSession };
        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyPromotionArcherSource,
                [successText],
                [new LegacyNpcDialogOption("Thank~you,~Master.", LegacyNpcClosedOption)],
                avatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionArcher advanced promotion " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionArcherSource} " +
                $"sourceRange={LegacyPromotionArcherAdvancedSourceRange} option={optionId} " +
                $"requiredItemId={requiredItemId} requiredItemCount={requiredItemCount} " +
                $"rewardItemId={rewardItemId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none"}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask LogLegacyArcherAdvancedPromotionSkippedAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc PromotionArcher advanced promotion " +
                $"{FormatSession(context)} source={LegacyPromotionArcherSource} " +
                $"sourceRange={LegacyPromotionArcherAdvancedSourceRange} option={optionId} {reason}")
            .ConfigureAwait(false);
    }

    private static ValueTask<bool> TryLearnLegacyArcherSkillAsync(
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
            LegacyPromotionArcherSkillLevelGate,
            LegacyPromotionArcherSource,
            sourceRange,
            LegacyPromotionArcherSkillAvatarId,
            cancellationToken);
    }

    private static async ValueTask<bool> TryHandleLegacyArcherPureSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (session.Character.Reborn != 2)
        {
            await SendLegacyArcherPureSkillRejectedAsync(context, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionArcher pure skill " +
                    $"{FormatSession(context)} source={LegacyPromotionArcherSource} " +
                    $"sourceRange={LegacyPromotionArcherStarArrowSourceRange} option={optionId} " +
                    $"reborn={session.Character.Reborn} requiredReborn=2; source reborn gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (!IsLegacyArcherClass(session.Character.FirstClass) ||
            !IsLegacyArcherClass(session.Character.SecondClass))
        {
            await SendLegacyArcherPureSkillRejectedAsync(context, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionArcher pure skill " +
                    $"{FormatSession(context)} source={LegacyPromotionArcherSource} " +
                    $"sourceRange={LegacyPromotionArcherStarArrowSourceRange} option={optionId} " +
                    $"firstClass={session.Character.FirstClass} secondClass={session.Character.SecondClass}; " +
                    "source pure Archer combo gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (session.PlayerSpells.ContainsSpell(LegacyPromotionArcherStarArrowSpellId))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionArcherSource,
                    ["You`ve learned StarArrow!"],
                    [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                    LegacyPromotionArcherAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionArcher pure skill " +
                    $"{FormatSession(context)} source={LegacyPromotionArcherSource} " +
                    $"sourceRange={LegacyPromotionArcherStarArrowSourceRange} option={optionId} " +
                    $"spellId={LegacyPromotionArcherStarArrowSpellId}; already learned.")
                .ConfigureAwait(false);
            return true;
        }

        var learned = CreateLegacyPromotionSpellEntry(LegacyPromotionArcherStarArrowSpellId);
        var updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(session.PlayerSpells, learned);
        var updatedSession = await ApplyLegacyPromotionMasterSpellsAsync(
                context,
                session,
                updatedSpells,
                optionId,
                LegacyPromotionArcherSource,
                LegacyPromotionArcherStarArrowSourceRange,
                LegacyPromotionArcherStarArrowSpellId.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ConfigureAwait(false);

        var updatedContext = context with { Session = updatedSession };
        await context.Connection.SendAsync(LegacySpellPacket.FromSnapshot(learned).ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyPromotionArcherSource,
                ["Congratulation! You just have learned the skill StarArrow"],
                [new LegacyNpcDialogOption("Ok , thank you.", LegacyNpcClosedOption)],
                LegacyPromotionArcherAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionArcher pure skill " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionArcherSource} " +
                $"sourceRange={LegacyPromotionArcherStarArrowSourceRange} option={optionId} " +
                $"spellId={LegacyPromotionArcherStarArrowSpellId}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask SendLegacyArcherPureSkillRejectedAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionArcherSource,
                ["Only pure Archers, whose classes for their past two lives and their current life are all Archers, can learn the pure skill Star Arrow."],
                [new LegacyNpcDialogOption("Okay.~I~see.", LegacyNpcClosedOption)],
                LegacyPromotionArcherAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryHandleLegacyArcherStarterPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (!IsLegacyArcherStarterPromotionEligible(session))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionArcher starter promotion " +
                    $"{FormatSession(context)} source={LegacyPromotionArcherSource} " +
                    $"sourceRange={LegacyPromotionArcherStarterSourceRange} option={optionId} " +
                    $"level={session.Character.Level} class={session.Character.Class}; source promotion gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        PlayerSpellSnapshotEntry? learned = null;
        var updatedSpells = session.PlayerSpells;
        if (!updatedSpells.ContainsSpell(LegacyPromotionArcherXpFlySpellId))
        {
            learned = CreateLegacyPromotionSpellEntry(LegacyPromotionArcherXpFlySpellId);
            updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(updatedSpells, learned);
        }

        var updatedSession = await ApplyLegacyPromotionMasterSpellsAsync(
                context,
                session,
                updatedSpells,
                optionId,
                LegacyPromotionArcherSource,
                LegacyPromotionArcherStarterSourceRange,
                LegacyPromotionArcherXpFlySpellId.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ConfigureAwait(false);

        if (learned is not null)
        {
            await context.Connection.SendAsync(
                    LegacySpellPacket.FromSnapshot(learned).ToFrameBytes(),
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
                LegacyPromotionArcherSource,
                LegacyPromotionArcherStarterSourceRange)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionArcher starter promotion " +
                $"{FormatSession(context with { Session = updatedSession })} source={LegacyPromotionArcherSource} " +
                $"sourceRange={LegacyPromotionArcherStarterSourceRange} option={optionId} " +
                $"spellId={LegacyPromotionArcherXpFlySpellId}.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool IsLegacyArcherStarterPromotionEligible(WorldSession session)
    {
        return session.Character.Level >= LegacyPromotionArcherStarterLevelGate &&
            session.Character.Class == LegacyPromotionArcherInternClass;
    }
}
