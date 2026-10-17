using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyPromotionWindWalkerNpcId = 19_634;
    private const string LegacyPromotionWindWalkerSource = "NpcHandler.PromotionWindWalker";
    private const ushort LegacyPromotionWindWalkerAvatarId = 333;
    private const ushort LegacyPromotionWindWalkerItemAvatarId = 6;
    private const string LegacyPromotionWindWalkerPromotionSourceRange = "NpcHandler.cs:46279-46379";
    private const string LegacyPromotionWindWalkerQuestRouteSourceRange = "NpcHandler.cs:46276-46278";
    private const string LegacyPromotionWindWalkerBranchSourceRange = "NpcHandler.cs:47355-47596; Player.cs:3010-3025";
    private const byte LegacyPromotionWindWalkerPromotionOption = 50;
    private const byte LegacyPromotionWindWalkerQuestRouteOption = 56;
    private const byte LegacyPromotionWindWalkerBranchDialogOption = 1;
    private const byte LegacyPromotionWindWalkerFreeMeleeOption = 2;
    private const byte LegacyPromotionWindWalkerFreeRangedOption = 3;
    private const byte LegacyPromotionWindWalkerItemMeleeOption = 4;
    private const byte LegacyPromotionWindWalkerConquerMeleeOption = 5;
    private const byte LegacyPromotionWindWalkerItemRangedOption = 6;
    private const byte LegacyPromotionWindWalkerConquerRangedOption = 7;
    private const long LegacyPromotionWindWalkerDiscipleClass = 16_000;
    private const long LegacyPromotionWindWalkerPlainFanGuardClass = 16_001;
    private const long LegacyPromotionWindWalkerInkFanInstructorClass = 16_002;
    private const long LegacyPromotionWindWalkerBrocadeFanProtectorClass = 16_003;
    private const long LegacyPromotionWindWalkerJadeFanElderClass = 16_004;
    private const long LegacyPromotionWindWalkerDiscipleLevelGate = 15;
    private const long LegacyPromotionWindWalkerPlainFanGuardLevelGate = 40;
    private const long LegacyPromotionWindWalkerInkFanInstructorLevelGate = 70;
    private const long LegacyPromotionWindWalkerBrocadeFanProtectorLevelGate = 100;
    private const long LegacyPromotionWindWalkerJadeFanElderLevelGate = 110;
    private const uint LegacyPromotionWindWalkerPrideFanItemId = 626_079;
    private const uint LegacyPromotionWindWalkerWindCoreItemId = 3_301_705;
    private const long LegacyPromotionWindWalkerBranchConquerPointCost = 27;
    private const ulong LegacyPromotionWindWalkerOnMeleeAttackFlag = 8;
    private const string LegacyPromotionWindWalkerMainFlagAttribute = "MainFlag";
    private const string LegacyPromotionWindWalkerFreeCooldownAttribute = "CanChangeWindWalkerFree";
    private const int LegacyPromotionWindWalkerPrideFanRewardCount = 2;
    private const byte LegacyPromotionWindWalkerUnboundFlag = 0;
    private const byte LegacyPromotionWindWalkerBoundFlag = 1;

    private static async ValueTask<bool> TryHandleLegacyWindWalkerPromotionSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == LegacyPromotionWindWalkerPromotionOption)
        {
            return await TryHandleLegacyWindWalkerPromotionAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false);
        }

        if (await TryHandleLegacyWindWalkerQuestRouteAsync(context, optionId)
                .ConfigureAwait(false))
        {
            return true;
        }

        if (await TryHandleLegacyWindWalkerStomperPromotionSkillAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false))
        {
            return true;
        }

        return await TryHandleLegacyWindWalkerChaserPromotionSkillAsync(context, session, optionId, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryHandleLegacyWindWalkerBranchDialogAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return optionId switch
        {
            LegacyPromotionWindWalkerBranchDialogOption => await SendLegacyWindWalkerBranchDialogAsync(
                    context,
                    session,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyPromotionWindWalkerFreeMeleeOption or LegacyPromotionWindWalkerFreeRangedOption => await TryHandleLegacyWindWalkerFreeBranchSwitchAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyPromotionWindWalkerItemMeleeOption or LegacyPromotionWindWalkerItemRangedOption => await TryHandleLegacyWindWalkerItemBranchSwitchAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyPromotionWindWalkerConquerMeleeOption or LegacyPromotionWindWalkerConquerRangedOption => await TryHandleLegacyWindWalkerConquerBranchSwitchAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => false,
        };
    }

    private static async ValueTask<bool> SendLegacyWindWalkerBranchDialogAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var onMelee = IsLegacyWindWalkerMeleeBranch(session.Character);
        var freeAvailable = IsLegacyWindWalkerFreeBranchSwitchAvailable(session.Character, DateTime.UtcNow);
        LegacyNpcDialogOption[] branchOptions = freeAvailable
            ? onMelee
                ? [new("Switch~to~[RangedStomper]~branch.", LegacyPromotionWindWalkerFreeRangedOption)]
                : [new("Switch~to~[MeleeStomper]~branch.", LegacyPromotionWindWalkerFreeMeleeOption)]
            : onMelee
                ?
                [
                    new("Switch~to~[RangedChaser]~branch.~(1~Wind~Core)", LegacyPromotionWindWalkerItemRangedOption),
                    new("Switch~to~[RangedChaser]~branch.~(27~CPs)", LegacyPromotionWindWalkerConquerRangedOption),
                ]
                :
                [
                    new("Switch~to~[MeleeStomper]~branch.~(1~Wind~Core)", LegacyPromotionWindWalkerItemMeleeOption),
                    new("Switch~to~[MeleeStomper]~branch.~(27~CPs)", LegacyPromotionWindWalkerConquerMeleeOption),
                ];

        var text = freeAvailable
            ? "Every WindWalker can switch branches for free once every 30 days."
            : $"You are currently on the [{GetLegacyWindWalkerBranchName(session.Character)}] branch. You can still switch with a Wind Core or 27 CPs.";
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionWindWalkerSource,
                [
                    "WindWalkers can switch between the melee Stomper branch and the ranged Chaser branch.",
                    text,
                ],
                branchOptions.Concat([new LegacyNpcDialogOption("I'll~think~about~it.", LegacyNpcClosedOption)]).ToArray(),
                LegacyPromotionWindWalkerAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyWindWalkerFreeBranchSwitchAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        if (!IsLegacyWindWalkerFreeBranchSwitchAvailable(session.Character, now))
        {
            await LogLegacyWindWalkerBranchSwitchIgnoredAsync(
                    context,
                    session,
                    optionId,
                    "source free 30-day cooldown gate rejected silently.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedCharacter = WithCharacterAttribute(
            session.Character,
            LegacyPromotionWindWalkerFreeCooldownAttribute,
            FormatLegacyWindWalkerBranchCooldown(now.AddDays(30)));
        await ApplyLegacyWindWalkerBranchSwitchAsync(
                context,
                session,
                optionId,
                updatedCharacter,
                GetLegacyWindWalkerBranchSuccessText(optionId),
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyWindWalkerItemBranchSwitchAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (!HasLegacyWindWalkerInventoryStackCount(
                session.PlayerItems,
                LegacyPromotionWindWalkerWindCoreItemId,
                1,
                [LegacyPromotionWindWalkerUnboundFlag]))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionWindWalkerSource,
                    ["I can't help you switch your WindWalker branch without a Wind Core. You can exchange one in the Horse Racing Shop."],
                    [new LegacyNpcDialogOption("I~understand.", LegacyNpcClosedOption)],
                    LegacyPromotionWindWalkerAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await LogLegacyWindWalkerBranchSwitchSkippedAsync(
                    context,
                    session,
                    optionId,
                    $"itemId={LegacyPromotionWindWalkerWindCoreItemId} acceptedBoundFlags={LegacyPromotionWindWalkerUnboundFlag}; source Wind Core gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        var mutationFrames = new List<byte[]>();
        var updatedItems = ConsumeLegacyWindWalkerInventoryStack(
            session.PlayerItems,
            LegacyPromotionWindWalkerWindCoreItemId,
            1,
            [LegacyPromotionWindWalkerUnboundFlag],
            mutationFrames);
        var itemSession = await ApplyLegacyPromotionMasterItemsAsync(
                context,
                session,
                updatedItems,
                optionId,
                LegacyPromotionWindWalkerSource,
                LegacyPromotionWindWalkerBranchSourceRange,
                $"consumedItemId={LegacyPromotionWindWalkerWindCoreItemId} consumedCount=1")
            .ConfigureAwait(false);
        var switchedSession = await ApplyLegacyWindWalkerBranchSwitchAsync(
                context,
                itemSession,
                optionId,
                itemSession.Character,
                GetLegacyWindWalkerBranchSuccessText(optionId),
                cancellationToken)
            .ConfigureAwait(false);
        var updatedContext = context with { Session = switchedSession };
        await SendLegacyPromotionMasterItemFramesAsync(updatedContext, mutationFrames, cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyWindWalkerConquerBranchSwitchAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (session.Character.ConquerPoints < LegacyPromotionWindWalkerBranchConquerPointCost)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionWindWalkerSource,
                    ["I'm afraid I can't help you switch branches without enough CPs."],
                    [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                    LegacyPromotionWindWalkerAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await LogLegacyWindWalkerBranchSwitchSkippedAsync(
                    context,
                    session,
                    optionId,
                    $"conquerPoints={session.Character.ConquerPoints} requiredConquerPoints={LegacyPromotionWindWalkerBranchConquerPointCost}; source CP gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (ReadAttributeBool(session.Character.Attributes, "InTrade"))
        {
            await LogLegacyWindWalkerBranchSwitchIgnoredAsync(
                    context,
                    session,
                    optionId,
                    "source InTrade gate rejected silently.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedCharacter = session.Character with
        {
            ConquerPoints = checked(session.Character.ConquerPoints - LegacyPromotionWindWalkerBranchConquerPointCost),
        };
        var updatedSession = await ApplyLegacyWindWalkerBranchSwitchAsync(
                context,
                session,
                optionId,
                updatedCharacter,
                GetLegacyWindWalkerBranchSuccessText(optionId),
                cancellationToken)
            .ConfigureAwait(false);
        var updatedContext = context with { Session = updatedSession };
        await SendScalarUpdateAsync(
                updatedContext,
                updatedSession.Character,
                LegacyMsgUpdateDataType.ConquerPoints,
                checked((ulong)updatedSession.Character.ConquerPoints),
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<WorldSession> ApplyLegacyWindWalkerBranchSwitchAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CharacterSnapshot baseCharacter,
        string successText,
        CancellationToken cancellationToken)
    {
        var mainFlagBefore = ReadAttributeUInt64(
            session.Character.Attributes,
            LegacyPromotionWindWalkerMainFlagAttribute);
        var mainFlagAfter = (mainFlagBefore & LegacyPromotionWindWalkerOnMeleeAttackFlag) == LegacyPromotionWindWalkerOnMeleeAttackFlag
            ? mainFlagBefore & ~LegacyPromotionWindWalkerOnMeleeAttackFlag
            : mainFlagBefore | LegacyPromotionWindWalkerOnMeleeAttackFlag;
        var updatedCharacter = WithCharacterAttribute(
            baseCharacter,
            LegacyPromotionWindWalkerMainFlagAttribute,
            mainFlagAfter);
        var updatedSession = await ApplyLegacyPromotionCharacterAsync(
                context,
                session,
                updatedCharacter,
                optionId,
                LegacyPromotionWindWalkerSource,
                LegacyPromotionWindWalkerBranchSourceRange)
            .ConfigureAwait(false);
        var updatedContext = context with { Session = updatedSession };
        await SendScalarUpdateAsync(
                updatedContext,
                updatedCharacter,
                LegacyMsgUpdateDataType.MainFlag,
                mainFlagAfter,
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyPromotionWindWalkerSource,
                [successText],
                [new LegacyNpcDialogOption("Thank you!", LegacyNpcClosedOption)],
                LegacyPromotionWindWalkerAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionWindWalker branch switch " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionWindWalkerSource} " +
                $"sourceRange={LegacyPromotionWindWalkerBranchSourceRange} option={optionId} " +
                $"mainFlagBefore={mainFlagBefore} mainFlagAfter={mainFlagAfter}.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static bool IsLegacyWindWalkerMeleeBranch(CharacterSnapshot character)
    {
        return (ReadAttributeUInt64(character.Attributes, LegacyPromotionWindWalkerMainFlagAttribute) &
            LegacyPromotionWindWalkerOnMeleeAttackFlag) == LegacyPromotionWindWalkerOnMeleeAttackFlag;
    }

    private static bool IsLegacyWindWalkerFreeBranchSwitchAvailable(CharacterSnapshot character, DateTime nowUtc)
    {
        if (!TryReadLegacyWindWalkerFreeCooldown(character, out var cooldownUtc))
        {
            return true;
        }

        return nowUtc > cooldownUtc;
    }

    private static bool TryReadLegacyWindWalkerFreeCooldown(CharacterSnapshot character, out DateTime cooldownUtc)
    {
        cooldownUtc = default;
        if (!character.Attributes.TryGetValue(LegacyPromotionWindWalkerFreeCooldownAttribute, out var raw))
        {
            return false;
        }

        return DateTime.TryParse(
            raw,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out cooldownUtc);
    }

    private static string GetLegacyWindWalkerBranchName(CharacterSnapshot character)
    {
        return IsLegacyWindWalkerMeleeBranch(character) ? "MeleeStomper" : "RangedChaser";
    }

    private static string GetLegacyWindWalkerBranchSuccessText(byte optionId)
    {
        return optionId switch
        {
            LegacyPromotionWindWalkerFreeRangedOption => "Okay, I have switched your branch to [RangedStomper].",
            LegacyPromotionWindWalkerItemRangedOption or LegacyPromotionWindWalkerConquerRangedOption => "Okay, I have switched your branch to [RangedChaser].",
            _ => "Okay, I have switched your branch to [MeleeStomper].",
        };
    }

    private static string FormatLegacyWindWalkerBranchCooldown(DateTime value)
    {
        return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }

    private static async ValueTask LogLegacyWindWalkerBranchSwitchSkippedAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc PromotionWindWalker branch switch " +
                $"{FormatSession(context)} source={LegacyPromotionWindWalkerSource} " +
                $"sourceRange={LegacyPromotionWindWalkerBranchSourceRange} option={optionId} " +
                $"class={session.Character.Class} {reason}")
            .ConfigureAwait(false);
    }

    private static async ValueTask LogLegacyWindWalkerBranchSwitchIgnoredAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "WorldServer ignored LegacyNpc PromotionWindWalker branch switch " +
                $"{FormatSession(context)} source={LegacyPromotionWindWalkerSource} " +
                $"sourceRange={LegacyPromotionWindWalkerBranchSourceRange} option={optionId} " +
                $"class={session.Character.Class}; {reason}")
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryHandleLegacyWindWalkerQuestRouteAsync(
        WorldPacketHandlerContext context,
        byte optionId)
    {
        if (optionId != LegacyPromotionWindWalkerQuestRouteOption)
        {
            return false;
        }

        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc PromotionWindWalker quest route " +
                $"{FormatSession(context)} source={LegacyPromotionWindWalkerSource} " +
                $"sourceRange={LegacyPromotionWindWalkerQuestRouteSourceRange} option={optionId} " +
                "map=1002 x=384 y=251 npcId=7991; " +
                "source calls QuestGUI.SendAutoPatcher(1002, 384, 251, 7991), but no safe local packet model exists.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyWindWalkerPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return session.Character.Class switch
        {
            LegacyPromotionWindWalkerDiscipleClass => await TryHandleLegacyWindWalkerClassOnlyPromotionAsync(
                    context,
                    session,
                    optionId,
                    LegacyPromotionWindWalkerDiscipleLevelGate,
                    "Brother, you are not yet qualified to become [Iron Fan]. You should reach Level 15 to be promoted.",
                    "I will work hard to reach Level 15.",
                    "Excellent! You have learned the ways of the Iron Fan school. Do you feel a different energy in your body? That is stronger power and battle prowess!",
                    LegacyPromotionWindWalkerAvatarId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyPromotionWindWalkerPlainFanGuardClass => await TryHandleLegacyWindWalkerRankFortyPromotionAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyPromotionWindWalkerInkFanInstructorClass => await TryHandleLegacyWindWalkerItemPromotionAsync(
                    context,
                    session,
                    optionId,
                    LegacyPromotionWindWalkerInkFanInstructorLevelGate,
                    LegacyPromotionEmeraldItemId,
                    "Emerald",
                    [LegacyPromotionWindWalkerUnboundFlag, LegacyPromotionWindWalkerBoundFlag],
                    "I'm sorry but you need to have 1 Emerald in your inventory. Go kill some Hill Monsters (Desert City) to get one.",
                    "Excellent! You have learned the ways of the Taoist. Do you feel a different energy in your body? That is stronger power and battle prowess!",
                    rewardItemId: null,
                    rewardSystemTalk: null,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyPromotionWindWalkerBrocadeFanProtectorClass => await TryHandleLegacyWindWalkerItemPromotionAsync(
                    context,
                    session,
                    optionId,
                    LegacyPromotionWindWalkerBrocadeFanProtectorLevelGate,
                    LegacyPromotionMeteorItemId,
                    "Meteor",
                    [LegacyPromotionWindWalkerUnboundFlag],
                    "I'm sorry but you need to have 1 Meteor in your inventory.",
                    "Excellent! Do you feel a different energy in your body? That is stronger power and battle prowess!",
                    rewardItemId: null,
                    rewardSystemTalk: null,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyPromotionWindWalkerJadeFanElderClass => await TryHandleLegacyWindWalkerItemPromotionAsync(
                    context,
                    session,
                    optionId,
                    LegacyPromotionWindWalkerJadeFanElderLevelGate,
                    LegacyPromotionMoonBoxItemId,
                    "MoonBox",
                    [LegacyPromotionWindWalkerUnboundFlag],
                    "I'm sorry but you need to have 1 MoonBox in your inventory.",
                    "Excellent! Do you feel a different energy in your body? That is stronger power and battle prowess!",
                    LegacyPromotionDragonBallItemId,
                    "You received DragonBall.",
                    cancellationToken)
                .ConfigureAwait(false),
            _ => await SendLegacyWindWalkerNoMorePromotionsAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
        };
    }

    private static async ValueTask<bool> TryHandleLegacyWindWalkerClassOnlyPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        long requiredLevel,
        string levelGateText,
        string levelGateOptionText,
        string successText,
        ushort avatarId,
        CancellationToken cancellationToken)
    {
        if (session.Character.Level < requiredLevel)
        {
            await SendLegacyWindWalkerPromotionLevelGateAsync(
                    context,
                    session,
                    optionId,
                    requiredLevel,
                    levelGateText,
                    levelGateOptionText,
                    avatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var updatedSession = await PromoteLegacyWindWalkerAsync(context, session, optionId)
            .ConfigureAwait(false);
        var updatedContext = context with { Session = updatedSession };
        await SendLegacyWindWalkerPromotionSuccessAsync(updatedContext, successText, avatarId, cancellationToken)
            .ConfigureAwait(false);
        await LogLegacyWindWalkerPromotionHandledAsync(
                updatedContext,
                optionId,
                "consumedItemId=0 consumedCount=0 grantedItemId=0 grantedCount=0")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyWindWalkerRankFortyPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (session.Character.Level < LegacyPromotionWindWalkerPlainFanGuardLevelGate)
        {
            await SendLegacyWindWalkerPromotionLevelGateAsync(
                    context,
                    session,
                    optionId,
                    LegacyPromotionWindWalkerPlainFanGuardLevelGate,
                    "Brother, you are not yet qualified to become [Iron Fan]. You should reach Level 40 to be promoted.",
                    "I will work hard to reach Level 40.",
                    LegacyPromotionWindWalkerAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var updatedSession = session;
        if (session.Character.Reborn == 0)
        {
            if (!HasLegacyInventorySpace(session.PlayerItems, LegacyPromotionWindWalkerPrideFanRewardCount))
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyPromotionWindWalkerSource,
                        ["Please make two more spaces in your inventory."],
                        [new LegacyNpcDialogOption("Let me check.", LegacyNpcClosedOption)],
                        LegacyPromotionWindWalkerAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                await LogLegacyWindWalkerPromotionSkippedAsync(
                        context,
                        optionId,
                        $"rewardItemId={LegacyPromotionWindWalkerPrideFanItemId} rewardCount={LegacyPromotionWindWalkerPrideFanRewardCount} loadedInventoryItems={session.PlayerItems.InventoryItems.Count}; source inventory space gate rejected.")
                    .ConfigureAwait(false);
                return true;
            }

            if (!TryReadItemType(context.WorldData, LegacyPromotionWindWalkerPrideFanItemId, out var prideFanType))
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyNpc PromotionWindWalker promotion missing PrideFan item type " +
                        $"{FormatSession(context)} source={LegacyPromotionWindWalkerSource} " +
                        $"sourceRange={LegacyPromotionWindWalkerPromotionSourceRange} option={optionId} " +
                        $"rewardItemId={LegacyPromotionWindWalkerPrideFanItemId} rewardCount={LegacyPromotionWindWalkerPrideFanRewardCount}; " +
                        "promotion skipped to avoid advancing class without source rewards.")
                    .ConfigureAwait(false);
                return true;
            }

            var grantedItems = CreatePurchasedItems(
                session.PlayerItems,
                prideFanType,
                LegacyPromotionWindWalkerPrideFanRewardCount,
                isBound: true);
            var updatedItems = session.PlayerItems with
            {
                InventoryItems = session.PlayerItems.InventoryItems.Concat(grantedItems).OrderBy(item => item.Uid).ToArray(),
            };
            updatedSession = await ApplyLegacyPromotionMasterItemsAsync(
                    context,
                    session,
                    updatedItems,
                    optionId,
                    LegacyPromotionWindWalkerSource,
                    LegacyPromotionWindWalkerPromotionSourceRange,
                    $"grantedItemId={LegacyPromotionWindWalkerPrideFanItemId} grantedCount={LegacyPromotionWindWalkerPrideFanRewardCount} bound=1")
                .ConfigureAwait(false);
            var updatedContext = context with { Session = updatedSession };
            foreach (var item in grantedItems)
            {
                await context.Connection.SendAsync(
                        LegacyGameItemPacket.FromSnapshotItem(item).ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("You received (Super)PrideFan*2.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            context = updatedContext;
        }

        updatedSession = await PromoteLegacyWindWalkerAsync(context, updatedSession, optionId)
            .ConfigureAwait(false);
        var promotedContext = context with { Session = updatedSession };
        await SendLegacyWindWalkerPromotionSuccessAsync(
                promotedContext,
                "Excellent! You have learned the ways of the WindWalker. Do you feel a different energy in your body? That is stronger power and battle prowess!",
                LegacyPromotionWindWalkerAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await LogLegacyWindWalkerPromotionHandledAsync(
                promotedContext,
                optionId,
                session.Character.Reborn == 0
                    ? $"consumedItemId=0 consumedCount=0 grantedItemId={LegacyPromotionWindWalkerPrideFanItemId} grantedCount={LegacyPromotionWindWalkerPrideFanRewardCount} bound=1"
                    : "consumedItemId=0 consumedCount=0 grantedItemId=0 grantedCount=0 rebornSkipReward=1")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyWindWalkerItemPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        long requiredLevel,
        uint requiredItemId,
        string requiredItemName,
        byte[] acceptedBoundFlags,
        string missingItemText,
        string successText,
        uint? rewardItemId,
        string? rewardSystemTalk,
        CancellationToken cancellationToken)
    {
        if (session.Character.Level < requiredLevel)
        {
            await SendLegacyWindWalkerPromotionLevelGateAsync(
                    context,
                    session,
                    optionId,
                    requiredLevel,
                    $"There is much that you must learn before you can accept my training. Slaughter a legion of monsters first! [Level {requiredLevel}+]",
                    "I~see.",
                    LegacyPromotionWindWalkerItemAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!HasLegacyWindWalkerInventoryStackCount(session.PlayerItems, requiredItemId, 1, acceptedBoundFlags))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionWindWalkerSource,
                    [missingItemText],
                    [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
                    LegacyPromotionWindWalkerItemAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await LogLegacyWindWalkerPromotionSkippedAsync(
                    context,
                    optionId,
                    $"itemId={requiredItemId} itemName={requiredItemName} acceptedBoundFlags={string.Join(",", acceptedBoundFlags)}; source item gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        ItemType? rewardItemType = null;
        if (rewardItemId is { } itemId &&
            !TryReadItemType(context.WorldData, itemId, out rewardItemType))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc PromotionWindWalker promotion missing reward item type " +
                    $"{FormatSession(context)} source={LegacyPromotionWindWalkerSource} " +
                    $"sourceRange={LegacyPromotionWindWalkerPromotionSourceRange} option={optionId} " +
                    $"requiredItemId={requiredItemId} rewardItemId={itemId}; promotion skipped to avoid consuming source item without reward.")
                .ConfigureAwait(false);
            return true;
        }

        var mutationFrames = new List<byte[]>();
        var updatedItems = ConsumeLegacyWindWalkerInventoryStack(
            session.PlayerItems,
            requiredItemId,
            1,
            acceptedBoundFlags,
            mutationFrames);
        PlayerItemSnapshotItem? rewardItem = null;
        if (rewardItemType is not null)
        {
            rewardItem = CreatePurchasedItems(updatedItems, rewardItemType, 1)[0];
            updatedItems = updatedItems with
            {
                InventoryItems = updatedItems.InventoryItems.Concat([rewardItem]).OrderBy(item => item.Uid).ToArray(),
            };
        }

        var updatedSession = await ApplyLegacyPromotionMasterItemsAsync(
                context,
                session,
                updatedItems,
                optionId,
                LegacyPromotionWindWalkerSource,
                LegacyPromotionWindWalkerPromotionSourceRange,
                rewardItemId is { } grantedItemId
                    ? $"consumedItemId={requiredItemId} consumedCount=1 grantedItemId={grantedItemId} grantedCount={(rewardItem is null ? 0 : 1)}"
                    : $"consumedItemId={requiredItemId} consumedCount=1 grantedItemId=0 grantedCount=0")
            .ConfigureAwait(false);
        var updatedContext = context with { Session = updatedSession };
        await SendLegacyPromotionMasterItemFramesAsync(updatedContext, mutationFrames, cancellationToken)
            .ConfigureAwait(false);

        if (rewardItem is not null)
        {
            await context.Connection.SendAsync(
                    LegacyGameItemPacket.FromSnapshotItem(rewardItem).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            if (rewardSystemTalk is not null)
            {
                await context.Connection.SendAsync(
                        LegacyMsgMessagePacket.SystemTalk(rewardSystemTalk).ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        updatedSession = await PromoteLegacyWindWalkerAsync(updatedContext, updatedSession, optionId)
            .ConfigureAwait(false);
        updatedContext = context with { Session = updatedSession };
        await SendLegacyWindWalkerPromotionSuccessAsync(
                updatedContext,
                successText,
                LegacyPromotionWindWalkerItemAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await LogLegacyWindWalkerPromotionHandledAsync(
                updatedContext,
                optionId,
                rewardItemId is { } loggedRewardItemId
                    ? $"consumedItemId={requiredItemId} consumedCount=1 grantedItemId={loggedRewardItemId} grantedCount={(rewardItem is null ? 0 : 1)}"
                    : $"consumedItemId={requiredItemId} consumedCount=1 grantedItemId=0 grantedCount=0")
            .ConfigureAwait(false);
        return true;
    }

    private static bool HasLegacyWindWalkerInventoryStackCount(
        PlayerItemSnapshot items,
        uint itemTypeId,
        uint requiredCount,
        IReadOnlyCollection<byte> acceptedBoundFlags)
    {
        var available = 0UL;
        foreach (var item in items.InventoryItems.Where(item =>
                     item.ItemId == itemTypeId && acceptedBoundFlags.Contains(item.Bound)))
        {
            available += item.StackSize == 0 ? 1UL : item.StackSize;
            if (available >= requiredCount)
            {
                return true;
            }
        }

        return false;
    }

    private static PlayerItemSnapshot ConsumeLegacyWindWalkerInventoryStack(
        PlayerItemSnapshot items,
        uint itemTypeId,
        uint count,
        IReadOnlyCollection<byte> acceptedBoundFlags,
        List<byte[]> mutationFrames)
    {
        var remaining = count;
        var inventoryItems = items.InventoryItems.ToList();
        for (var index = 0; index < inventoryItems.Count && remaining > 0; index++)
        {
            var item = inventoryItems[index];
            if (item.ItemId != itemTypeId || !acceptedBoundFlags.Contains(item.Bound))
            {
                continue;
            }

            var available = item.StackSize == 0 ? 1U : item.StackSize;
            var consumed = Math.Min(available, remaining);
            if (consumed >= available)
            {
                mutationFrames.Add(
                    new MsgItemUsagePacket(
                        item.Uid,
                        DwParam: 0,
                        MsgItemUsageAction.RemoveInventory,
                        Timestamp: 0,
                        DwParam2: 0,
                        DwParam3: 0,
                        DwParam4: 0).ToFrameBytes());
                inventoryItems.RemoveAt(index);
                index--;
            }
            else
            {
                var updatedItem = item.WithStackSize(checked((ushort)(available - consumed)));
                inventoryItems[index] = updatedItem;
                mutationFrames.Add(
                    LegacyGameItemPacket.FromSnapshotItem(
                        updatedItem,
                        mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes());
            }

            remaining -= consumed;
        }

        return new PlayerItemSnapshot(
            inventoryItems,
            items.RelicFuseItems,
            items.MythsoulBagItems,
            items.EquipmentItems,
            items.RuneItems,
            items.WardrobeItems)
        {
            WarehouseItems = items.WarehouseItems,
        };
    }

    private static async ValueTask<WorldSession> PromoteLegacyWindWalkerAsync(
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
                LegacyPromotionWindWalkerSource,
                LegacyPromotionWindWalkerPromotionSourceRange)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyWindWalkerPromotionLevelGateAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        long requiredLevel,
        string text,
        string optionText,
        ushort avatarId,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionWindWalkerSource,
                [text],
                [new LegacyNpcDialogOption(optionText, LegacyNpcClosedOption)],
                avatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await LogLegacyWindWalkerPromotionSkippedAsync(
                context,
                optionId,
                $"level={session.Character.Level} requiredLevel={requiredLevel}; source level gate rejected.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyWindWalkerPromotionSuccessAsync(
        WorldPacketHandlerContext context,
        string successText,
        ushort avatarId,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionWindWalkerSource,
                [successText],
                [new LegacyNpcDialogOption("Thank you, Master.", LegacyNpcClosedOption)],
                avatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> SendLegacyWindWalkerNoMorePromotionsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionWindWalkerSource,
                ["You cannot be promoted anymore. You have mastered your class"],
                [new LegacyNpcDialogOption("Thank you, Master.", LegacyNpcClosedOption)],
                LegacyPromotionWindWalkerItemAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await LogLegacyWindWalkerPromotionSkippedAsync(
                context,
                optionId,
                $"class={session.Character.Class}; no source promotion rank matched.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask LogLegacyWindWalkerPromotionHandledAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        string itemSummary)
    {
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionWindWalker promotion " +
                $"{FormatSession(context)} source={LegacyPromotionWindWalkerSource} " +
                $"sourceRange={LegacyPromotionWindWalkerPromotionSourceRange} option={optionId} {itemSummary}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask LogLegacyWindWalkerPromotionSkippedAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc PromotionWindWalker promotion " +
                $"{FormatSession(context)} source={LegacyPromotionWindWalkerSource} " +
                $"sourceRange={LegacyPromotionWindWalkerPromotionSourceRange} option={optionId} {reason}")
            .ConfigureAwait(false);
    }
}
