using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyPromotionPirateNpcId = 9_391;
    private const string LegacyPromotionPirateSource = "NpcHandler.PromotionPirate";
    private const ushort LegacyPromotionPirateAvatarId = 234;
    private const ushort LegacyPromotionPirateSkillAvatarId = 8;
    private const ushort LegacyPromotionPiratePureSkillAvatarId = 5;
    private const ushort LegacyPromotionPirateRebornGateAvatarId = 5;
    private const ushort LegacyPromotionPirateFirstRankPromotionAvatarId = 5;
    private const ushort LegacyPromotionPirateItemPromotionAvatarId = 6;
    private const long LegacyPromotionPirateSkillLevelGate = 100;
    private const long LegacyPromotionPirateStarterLevelGate = 15;
    private const long LegacyPromotionPirateInternClass = 7_000;
    private const long LegacyPromotionPirateClass = 7_001;
    private const long LegacyPromotionPirateGunnerClass = 7_002;
    private const long LegacyPromotionPirateQuartermasterClass = 7_003;
    private const long LegacyPromotionPirateCaptainClass = 7_004;

    private const string LegacyPromotionPirateAdvancedPromotionSourceRange = "NpcHandler.cs:51356-51586";
    private const string LegacyPromotionPirateScurvyBombSourceRange = "NpcHandler.cs:51546-51564";
    private const string LegacyPromotionPirateSeaBurialSourceRange = "NpcHandler.cs:51693-51731";
    private const string LegacyPromotionPirateBloomofDeathSourceRange = "NpcHandler.cs:51732-51770";
    private const string LegacyPromotionPirateImmortalForceSourceRange = "NpcHandler.cs:51771-51809";
    private const string LegacyPromotionPirateStarterSourceRange = "NpcHandler.cs:51587-51643";
    private const string LegacyPromotionPirateBlackspotSourceRange = "NpcHandler.cs:51824-51862";
    private const string LegacyPromotionPirateCannonBarrageSourceRange = "NpcHandler.cs:51883-51921";
    private const string LegacyPromotionPirateGolemSourceRange = "NpcHandler.cs:51922-51960";
    private const string LegacyPromotionPirateWindstormSourceRange = "NpcHandler.cs:51961-51999";
    private const string LegacyPromotionPirateBladeTempestSourceRange = "NpcHandler.cs:52000-52038";
    private const string LegacyPromotionPirateGaleBombSourceRange = "NpcHandler.cs:52039-52077";
    private const string LegacyPromotionPirateAdrenalineRushSourceRange = "NpcHandler.cs:52078-52116";
    private const string LegacyPromotionPirateEagleEyeSourceRange = "NpcHandler.cs:52117-52155";
    private const string LegacyPromotionPirateBlackbeardsRageSourceRange = "NpcHandler.cs:52156-52194";
    private const string LegacyPromotionPirateKrakensRevengeSourceRange = "NpcHandler.cs:52195-52244";
    private const string LegacyPromotionPirateQuestFinishSourceRange = "NpcHandler.cs:51318-51355";
    private const string LegacyPromotionPirateQuestFinishLookupSourceRange = "NpcHandler.cs:51269";

    private const byte LegacyPromotionPirateStarterOption = 3;
    private const byte LegacyPromotionPirateAdvancedPromotionOption = 4;
    private const byte LegacyPromotionPirateBladeTempestOption = 6;
    private const byte LegacyPromotionPirateGaleBombOption = 7;
    private const byte LegacyPromotionPirateAdrenalineRushOption = 8;
    private const byte LegacyPromotionPirateEagleEyeOption = 9;
    private const byte LegacyPromotionPirateBlackbeardsRageOption = 10;
    private const byte LegacyPromotionPirateKrakensRevengeOption = 11;
    private const byte LegacyPromotionPirateBlackspotOption = 12;
    private const byte LegacyPromotionPirateWindstormOption = 13;
    private const byte LegacyPromotionPirateGolemOption = 14;
    private const byte LegacyPromotionPirateScurvyBombOption = 21;
    private const byte LegacyPromotionPirateCannonBarrageOption = 60;
    private const byte LegacyPromotionPirateQuestFinishOption = 111;
    private const byte LegacyPromotionPirateImmortalForceOption = 247;
    private const byte LegacyPromotionPirateBloomofDeathOption = 248;
    private const byte LegacyPromotionPirateSeaBurialOption = 249;

    private const ushort LegacyPromotionPirateBladeTempestSpellId = 11_110;
    private const ushort LegacyPromotionPirateGaleBombSpellId = 11_070;
    private const ushort LegacyPromotionPirateAdrenalineRushSpellId = 11_130;
    private const ushort LegacyPromotionPirateEagleEyeSpellId = 11_030;
    private const ushort LegacyPromotionPirateBlackbeardsRageSpellId = 11_060;
    private const ushort LegacyPromotionPirateKrakensRevengeSpellId = 11_100;
    private const ushort LegacyPromotionPirateBlackspotSpellId = 11_120;
    private const ushort LegacyPromotionPirateWindstormSpellId = 11_140;
    private const ushort LegacyPromotionPirateGolemSpellId = 1_270;
    private const ushort LegacyPromotionPirateScurvyBombSpellId = 11_040;
    private const ushort LegacyPromotionPirateCannonBarrageSpellId = 11_050;
    private const ushort LegacyPromotionPirateSeaBurialSpellId = 14_680;
    private const ushort LegacyPromotionPirateImmortalForceSpellId = 14_710;
    private const ushort LegacyPromotionPirateBloomofDeathSpellId = 14_720;
    private const uint LegacyPromotionPirateQuestFinishMissionId = 2_001;
    private const uint LegacyPromotionPirateQuestFinishRewardItemId = 3_005_160;

    private const string LegacyPromotionPirateScurvyBombSpellIdsLog = "11040";
    private const string LegacyPromotionPirateStarterSpellIdsLog = "11050,11110";

    private static readonly ushort[] LegacyPromotionPirateStarterSpellIds =
    [
        LegacyPromotionPirateCannonBarrageSpellId,
        LegacyPromotionPirateBladeTempestSpellId,
    ];

    private static ValueTask<bool> TryHandleLegacyPiratePromotionSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == LegacyPromotionPirateStarterOption)
        {
            return TryHandleLegacyPirateStarterPromotionAsync(
                context,
                session,
                optionId,
                cancellationToken);
        }

        if (optionId == LegacyPromotionPirateAdvancedPromotionOption)
        {
            return TryHandleLegacyPirateAdvancedPromotionAsync(
                context,
                session,
                optionId,
                cancellationToken);
        }

        if (optionId == LegacyPromotionPirateScurvyBombOption)
        {
            return TryHandleLegacyPiratePureSkillAsync(
                context,
                session,
                optionId,
                cancellationToken);
        }

        return optionId switch
        {
            LegacyPromotionPirateQuestFinishOption => TryHandleLegacyPirateQuestFinishOptionAsync(
                context,
                session,
                optionId,
                cancellationToken),
            LegacyPromotionPirateBladeTempestOption => TryLearnLegacyPiratePromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionPirateBladeTempestSpellId,
                "BladeTempest",
                LegacyPromotionPirateBladeTempestSourceRange,
                cancellationToken),
            LegacyPromotionPirateGaleBombOption => TryLearnLegacyPiratePromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionPirateGaleBombSpellId,
                "GaleBomb",
                LegacyPromotionPirateGaleBombSourceRange,
                cancellationToken),
            LegacyPromotionPirateAdrenalineRushOption => TryLearnLegacyPiratePromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionPirateAdrenalineRushSpellId,
                "AdrenalineRush",
                LegacyPromotionPirateAdrenalineRushSourceRange,
                cancellationToken),
            LegacyPromotionPirateEagleEyeOption => TryLearnLegacyPiratePromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionPirateEagleEyeSpellId,
                "EagleEye",
                LegacyPromotionPirateEagleEyeSourceRange,
                cancellationToken),
            LegacyPromotionPirateBlackbeardsRageOption => TryLearnLegacyPiratePromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionPirateBlackbeardsRageSpellId,
                "BlackbeardsRage",
                LegacyPromotionPirateBlackbeardsRageSourceRange,
                cancellationToken),
            LegacyPromotionPirateKrakensRevengeOption => TryLearnLegacyPirateKrakensRevengeSkillAsync(
                context,
                session,
                optionId,
                cancellationToken),
            LegacyPromotionPirateBlackspotOption => TryLearnLegacyPiratePromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionPirateBlackspotSpellId,
                "Blackspot",
                LegacyPromotionPirateBlackspotSourceRange,
                cancellationToken),
            LegacyPromotionPirateWindstormOption => TryLearnLegacyPiratePromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionPirateWindstormSpellId,
                "Windstorm",
                LegacyPromotionPirateWindstormSourceRange,
                cancellationToken),
            LegacyPromotionPirateGolemOption => TryLearnLegacyPiratePromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionPirateGolemSpellId,
                "Golem",
                LegacyPromotionPirateGolemSourceRange,
                cancellationToken),
            LegacyPromotionPirateCannonBarrageOption => TryLearnLegacyPiratePromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionPirateCannonBarrageSpellId,
                "CannonBarrage",
                LegacyPromotionPirateCannonBarrageSourceRange,
                cancellationToken),
            LegacyPromotionPirateImmortalForceOption => TryLearnLegacyPiratePromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionPirateImmortalForceSpellId,
                "ImmortalForce",
                LegacyPromotionPirateImmortalForceSourceRange,
                cancellationToken),
            LegacyPromotionPirateBloomofDeathOption => TryLearnLegacyPiratePromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionPirateBloomofDeathSpellId,
                "BloomofDeath",
                LegacyPromotionPirateBloomofDeathSourceRange,
                cancellationToken),
            LegacyPromotionPirateSeaBurialOption => TryLearnLegacyPiratePromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionPirateSeaBurialSpellId,
                "SeaBurial",
                LegacyPromotionPirateSeaBurialSourceRange,
                cancellationToken),
            _ => ValueTask.FromResult(false),
        };
    }

    private static async ValueTask<bool> TryHandleLegacyPirateQuestFinishOptionAsync(
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
                    LegacyPromotionPirateSource,
                    LegacyPromotionPirateQuestFinishSourceRange,
                    LegacyPromotionPirateQuestFinishLookupSourceRange,
                    "GetFinishQuest(2001, class, 1004)",
                    "PromotionPirate",
                    LegacyPromotionPirateQuestFinishMissionId,
                    LegacyPromotionPirateQuestFinishRewardItemId),
                cancellationToken)
            .ConfigureAwait(false);

        var nextOption = session.Character.Class == LegacyPromotionPirateInternClass
            ? (byte)1
            : LegacyPromotionPirateAdvancedPromotionOption;

        await SendLegacyNpcDialogAsync(
                context with { Session = activeSession },
                LegacyPromotionPirateSource,
                [
                    "Wow, look at you, you`ll be another hero in the world of Conquer! Here, you can learn some skills and get promoted! Do you want to learn more?",
                    "~get promoted here to start your life. Would you like to learn something about your class promotion and quests?.",
                ],
                [new LegacyNpcDialogOption("I~want~to~get~promoted", nextOption)],
                LegacyPromotionPirateAvatarId,
                cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyPirateAdvancedPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return session.Character.Class switch
        {
            LegacyPromotionPirateClass => await TryHandleLegacyPirateFirstRankPromotionAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyPromotionPirateGunnerClass => await TryHandleLegacyPirateItemPromotionAsync(
                    context,
                    session,
                    optionId,
                    requiredLevel: 70,
                    requiredItemId: LegacyPromotionEmeraldItemId,
                    itemName: "Emerald",
                    missingItemText: "I'm sorry but you need to have 1 Emerald at your inventory , to get one go and kill some HillMonsters ( Desert City ) to get one .",
                    successText: "Excellent! You`ve learned ways of the Taoist. Do you feel a different energy in your body? That`s stronger power and battle prowess!",
                    rewardItemId: null,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyPromotionPirateQuartermasterClass => await TryHandleLegacyPirateItemPromotionAsync(
                    context,
                    session,
                    optionId,
                    requiredLevel: 100,
                    requiredItemId: LegacyPromotionMeteorItemId,
                    itemName: "Meteor",
                    missingItemText: "I'm sorry but you need to have 1 Meteor at your inventory.",
                    successText: "Excellent! Do you feel a different energy in your body? That`s stronger power and battle prowess!",
                    rewardItemId: null,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyPromotionPirateCaptainClass => await TryHandleLegacyPirateItemPromotionAsync(
                    context,
                    session,
                    optionId,
                    requiredLevel: 110,
                    requiredItemId: LegacyPromotionMoonBoxItemId,
                    itemName: "MoonBox",
                    missingItemText: "I'm sorry but you need to have 1 MoonBox at your inventory.",
                    successText: "Excellent!Do you feel a different energy in your body? That`s stronger power and battle prowess!",
                    rewardItemId: LegacyPromotionDragonBallItemId,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => await SendLegacyPirateAdvancedPromotionCompleteAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
        };
    }

    private static async ValueTask<bool> TryHandleLegacyPirateFirstRankPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (session.Character.Level < 40)
        {
            if (session.Character.Level >= 15)
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyPromotionPirateSource,
                        [
                            "As a Pirate, you`ll need to reach Level 40 to get promoted to a Pirate Gunner. You will learn Adrenaline Rush, Eagle Eye, Blackbeard`s Rage and Black Spot after promotion.",
                            "~If you never change your class as a Pirate after the 2nd Rebirth, you can learn Scurvy Bomb.",
                        ],
                        [new LegacyNpcDialogOption("I`ll~think~about~it.", LegacyNpcClosedOption)],
                        LegacyPromotionPirateAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await SendLegacyPirateAdvancedLevelGateAsync(
                    context,
                    requiredLevel: 40,
                    LegacyPromotionPirateFirstRankPromotionAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionPirate advanced promotion " +
                    $"{FormatSession(context)} source={LegacyPromotionPirateSource} " +
                    $"sourceRange={LegacyPromotionPirateAdvancedPromotionSourceRange} option={optionId} " +
                    $"level={session.Character.Level} class={session.Character.Class} requiredLevel=40; " +
                    "source level gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedSession = await ApplyLegacyPirateAdvancedPromotionClassAsync(
                context,
                session,
                optionId)
            .ConfigureAwait(false);
        var updatedContext = context with { Session = updatedSession };
        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyPromotionPirateSource,
                ["Excellent! You`ve learned ways of the Pirate. Do you feel a different energy in your body? That`s stronger power and battle prowess!"],
                [new LegacyNpcDialogOption("Thank~you,~Master.", LegacyNpcClosedOption)],
                LegacyPromotionPirateFirstRankPromotionAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionPirate advanced promotion " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionPirateSource} " +
                $"sourceRange={LegacyPromotionPirateAdvancedPromotionSourceRange} option={optionId} " +
                "rank=first itemId=0 rewardItemId=0.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyPirateItemPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        long requiredLevel,
        uint requiredItemId,
        string itemName,
        string missingItemText,
        string successText,
        uint? rewardItemId,
        CancellationToken cancellationToken)
    {
        if (session.Character.Level < requiredLevel)
        {
            await SendLegacyPirateAdvancedLevelGateAsync(
                    context,
                    requiredLevel,
                    LegacyPromotionPirateItemPromotionAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionPirate advanced promotion " +
                    $"{FormatSession(context)} source={LegacyPromotionPirateSource} " +
                    $"sourceRange={LegacyPromotionPirateAdvancedPromotionSourceRange} option={optionId} " +
                    $"level={session.Character.Level} class={session.Character.Class} requiredLevel={requiredLevel}; " +
                    "source level gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (!HasLegacyInventoryStackCount(session.PlayerItems, requiredItemId, 1))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionPirateSource,
                    [missingItemText],
                    [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
                    LegacyPromotionPirateItemPromotionAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionPirate advanced promotion " +
                    $"{FormatSession(context)} source={LegacyPromotionPirateSource} " +
                    $"sourceRange={LegacyPromotionPirateAdvancedPromotionSourceRange} option={optionId} " +
                    $"level={session.Character.Level} class={session.Character.Class} itemId={requiredItemId} " +
                    $"itemName={itemName}; source item gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        var mutationFrames = new List<byte[]>();
        var updatedItems = ConsumeLegacyInventoryStack(
            session.PlayerItems,
            requiredItemId,
            1,
            mutationFrames);
        PlayerItemSnapshotItem? rewardItem = null;
        if (rewardItemId is { } itemId)
        {
            if (!TryReadItemType(context.WorldData, itemId, out var rewardItemType))
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyNpc PromotionPirate advanced promotion reward missing item type " +
                        $"{FormatSession(context)} source={LegacyPromotionPirateSource} " +
                        $"sourceRange={LegacyPromotionPirateAdvancedPromotionSourceRange} option={optionId} " +
                        $"requiredItemId={requiredItemId} rewardItemId={itemId}; promotion was left unchanged.")
                    .ConfigureAwait(false);
                return true;
            }

            var grantedRewardItem = CreatePurchasedItems(updatedItems, rewardItemType, 1)[0];
            rewardItem = grantedRewardItem;
            updatedItems = updatedItems with
            {
                InventoryItems = updatedItems.InventoryItems.Concat([grantedRewardItem]).ToArray(),
            };
            mutationFrames.Add(LegacyGameItemPacket.FromSnapshotItem(grantedRewardItem).ToFrameBytes());
        }

        var updatedSession = await ApplyLegacyPromotionMasterItemsAsync(
                context,
                session,
                updatedItems,
                optionId,
                LegacyPromotionPirateSource,
                LegacyPromotionPirateAdvancedPromotionSourceRange,
                $"consumedItemId={requiredItemId} consumedItemCount=1 rewardItemId={rewardItemId ?? 0}")
            .ConfigureAwait(false);
        var itemContext = context with { Session = updatedSession };
        await SendLegacyPromotionMasterItemFramesAsync(itemContext, mutationFrames, cancellationToken)
            .ConfigureAwait(false);

        if (rewardItem is not null)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("You received DragonBall.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        updatedSession = await ApplyLegacyPirateAdvancedPromotionClassAsync(
                context,
                updatedSession,
                optionId)
            .ConfigureAwait(false);
        var updatedContext = context with { Session = updatedSession };
        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyPromotionPirateSource,
                [successText],
                [new LegacyNpcDialogOption("Thank~you,~Master.", LegacyNpcClosedOption)],
                LegacyPromotionPirateItemPromotionAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionPirate advanced promotion " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionPirateSource} " +
                $"sourceRange={LegacyPromotionPirateAdvancedPromotionSourceRange} option={optionId} " +
                $"requiredItemId={requiredItemId} rewardItemId={rewardItemId ?? 0} " +
                $"rewardItemUid={rewardItem?.Uid ?? 0}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<WorldSession> ApplyLegacyPirateAdvancedPromotionClassAsync(
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
                LegacyPromotionPirateSource,
                LegacyPromotionPirateAdvancedPromotionSourceRange)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyPirateAdvancedLevelGateAsync(
        WorldPacketHandlerContext context,
        long requiredLevel,
        ushort avatarId,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionPirateSource,
                [$"There is much that you must learn before you can accept my training. Slaughter a legion of monsters first! [Level {requiredLevel}+]"],
                [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
                avatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> SendLegacyPirateAdvancedPromotionCompleteAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPromotionPirateSource,
                ["You cannot be promoted anymore. You have mastered your class"],
                [new LegacyNpcDialogOption("Thank~you,~Master.", LegacyNpcClosedOption)],
                LegacyPromotionPirateItemPromotionAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc PromotionPirate advanced promotion " +
                $"{FormatSession(context)} source={LegacyPromotionPirateSource} " +
                $"sourceRange={LegacyPromotionPirateAdvancedPromotionSourceRange} option={optionId} " +
                $"level={session.Character.Level} class={session.Character.Class}; source max promotion branch reached.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyPirateStarterPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (!IsLegacyPirateStarterPromotionEligible(session))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionPirateSource,
                    ["Sorry, you need level 15."],
                    [new LegacyNpcDialogOption("Ah ok.", LegacyNpcClosedOption)],
                    LegacyPromotionPirateAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionPirate starter promotion " +
                    $"{FormatSession(context)} source={LegacyPromotionPirateSource} " +
                    $"sourceRange={LegacyPromotionPirateStarterSourceRange} option={optionId} " +
                    $"level={session.Character.Level} class={session.Character.Class}; source promotion gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        var learnedSpells = new List<PlayerSpellSnapshotEntry>(capacity: LegacyPromotionPirateStarterSpellIds.Length);
        var updatedSpells = session.PlayerSpells;
        foreach (var spellId in LegacyPromotionPirateStarterSpellIds)
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
                LegacyPromotionPirateSource,
                LegacyPromotionPirateStarterSourceRange,
                LegacyPromotionPirateStarterSpellIdsLog)
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
                LegacyPromotionPirateSource,
                LegacyPromotionPirateStarterSourceRange)
            .ConfigureAwait(false);

        var updatedContext = context with { Session = updatedSession };
        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyPromotionPirateSource,
                ["You`ve been promoted!"],
                [new LegacyNpcDialogOption("Great,~thanks!", LegacyNpcClosedOption)],
                LegacyPromotionPirateAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionPirate starter promotion " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionPirateSource} " +
                $"sourceRange={LegacyPromotionPirateStarterSourceRange} option={optionId} " +
                $"spellIds={LegacyPromotionPirateStarterSpellIdsLog}.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool IsLegacyPirateStarterPromotionEligible(WorldSession session)
    {
        return session.Character.Level >= LegacyPromotionPirateStarterLevelGate &&
            session.Character.Class == LegacyPromotionPirateInternClass;
    }

    private static async ValueTask<bool> TryHandleLegacyPiratePureSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (session.Character.Reborn < 2 ||
            !IsLegacyPiratePromotionClass(session.Character.FirstClass) ||
            !IsLegacyPiratePromotionClass(session.Character.SecondClass))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionPirateSource,
                    ["If you want to learn the Scurvy Bomb, you`ll need to be a pure Pirate, who`ve been a Pirate for 3 straight lives."],
                    [new LegacyNpcDialogOption("OK", LegacyNpcClosedOption)],
                    LegacyPromotionPirateAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionPirate pure skill " +
                    $"{FormatSession(context)} source={LegacyPromotionPirateSource} " +
                    $"sourceRange={LegacyPromotionPirateScurvyBombSourceRange} option={optionId} " +
                    $"reborn={session.Character.Reborn} firstClass={session.Character.FirstClass} " +
                    $"secondClass={session.Character.SecondClass}; source pure Pirate combo gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (session.PlayerSpells.ContainsSpell(LegacyPromotionPirateScurvyBombSpellId))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionPirateSource,
                    ["You`ve learned ScurvyBomb!"],
                    [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                    LegacyPromotionPiratePureSkillAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionPirate pure skill " +
                    $"{FormatSession(context)} source={LegacyPromotionPirateSource} " +
                    $"sourceRange={LegacyPromotionPirateScurvyBombSourceRange} option={optionId} " +
                    $"spellId={LegacyPromotionPirateScurvyBombSpellId}; already learned.")
                .ConfigureAwait(false);
            return true;
        }

        var learned = CreateLegacyPromotionSpellEntry(LegacyPromotionPirateScurvyBombSpellId);
        var updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(session.PlayerSpells, learned);
        var updatedSession = await ApplyLegacyPromotionMasterSpellsAsync(
                context,
                session,
                updatedSpells,
                optionId,
                LegacyPromotionPirateSource,
                LegacyPromotionPirateScurvyBombSourceRange,
                LegacyPromotionPirateScurvyBombSpellIdsLog)
            .ConfigureAwait(false);

        var updatedContext = context with { Session = updatedSession };
        await context.Connection.SendAsync(LegacySpellPacket.FromSnapshot(learned).ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyPromotionPirateSource,
                ["Congratulation! You just have learned the skill ScurvyBomb"],
                [new LegacyNpcDialogOption("Ok , thank you.", LegacyNpcClosedOption)],
                LegacyPromotionPiratePureSkillAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionPirate pure skill " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionPirateSource} " +
                $"sourceRange={LegacyPromotionPirateScurvyBombSourceRange} option={optionId} " +
                $"spellId={LegacyPromotionPirateScurvyBombSpellId}.")
            .ConfigureAwait(false);
        return true;
    }

    private static ValueTask<bool> TryLearnLegacyPiratePromotionSkillAsync(
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
            LegacyPromotionPirateSkillLevelGate,
            LegacyPromotionPirateSource,
            sourceRange,
            LegacyPromotionPirateSkillAvatarId,
            cancellationToken);
    }

    private static async ValueTask<bool> TryLearnLegacyPirateKrakensRevengeSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (session.Character.Reborn <= 1 || !IsLegacyPiratePromotionClass(session.Character.FirstClass))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionPirateSource,
                    ["Sorry you are not reborn, or not is pirat-pirat "],
                    [new LegacyNpcDialogOption("Ok , thank you.", LegacyNpcClosedOption)],
                    LegacyPromotionPirateRebornGateAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionMaster skill " +
                    $"{FormatSession(context)} source={LegacyPromotionPirateSource} " +
                    $"sourceRange={LegacyPromotionPirateKrakensRevengeSourceRange} option={optionId} " +
                    $"reborn={session.Character.Reborn} firstClass={session.Character.FirstClass}; " +
                    "source reborn/first-class gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        return await TryLearnLegacyPiratePromotionSkillAsync(
                context,
                session,
                optionId,
                LegacyPromotionPirateKrakensRevengeSpellId,
                "KrakensRevenge",
                LegacyPromotionPirateKrakensRevengeSourceRange,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
