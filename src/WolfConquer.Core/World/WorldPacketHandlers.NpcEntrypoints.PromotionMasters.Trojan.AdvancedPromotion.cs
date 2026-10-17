using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyPromotionTrojanAdvancedPromotionSourceRange = "NpcHandler.cs:47785-47946";
    private const string LegacyPromotionTrojanEpicQuestRouteSourceRange = "NpcHandler.cs:47782-47784";
    private const byte LegacyPromotionTrojanAdvancedPromotionOption = 3;
    private const byte LegacyPromotionTrojanEpicQuestRouteOption = 101;
    private const long LegacyPromotionTrojanRankOneClass = 1_001;
    private const long LegacyPromotionTrojanRankTwoClass = 1_002;
    private const long LegacyPromotionTrojanRankThreeClass = 1_003;
    private const long LegacyPromotionTrojanRankFourClass = 1_004;

    private const string LegacyPromotionTrojanMissingEmeraldText =
        "I'm sorry but you need to have 1 Emerald at your inventory , to get one go and kill some HillMonsters ( Desert City ) to get one .";
    private const string LegacyPromotionTrojanMissingMeteorText =
        "I'm sorry but you need to have 1 Meteor at your inventory.";
    private const string LegacyPromotionTrojanMissingMoonBoxText =
        "I'm sorry but you need to have 1 MoonBox at your inventory.";
    private const string LegacyPromotionTrojanDragonBallReceivedText = "You received DragonBall.";

    private static async ValueTask<bool> TryHandleLegacyTrojanAdvancedPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId != LegacyPromotionTrojanAdvancedPromotionOption)
        {
            return false;
        }

        var branch = GetLegacyTrojanAdvancedPromotionBranch(session.Character.Class);
        if (branch.RequiredClass == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionTrojanSource,
                    ["You cannot be promoted anymore. You have mastered your class"],
                    [new LegacyNpcDialogOption("Thank~you,~Master.", LegacyNpcClosedOption)],
                    avatarId: 6,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionTrojan advanced promotion " +
                    $"{FormatSession(context)} source={LegacyPromotionTrojanSource} " +
                    $"sourceRange={LegacyPromotionTrojanAdvancedPromotionSourceRange} option={optionId} " +
                    $"class={session.Character.Class}; source class gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        if (session.Character.Level < branch.RequiredLevel)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPromotionTrojanSource,
                    [$"There is much that you must learn before you can accept my training. Slaughter a legion of monsters first! [Level {branch.RequiredLevel}+]"],
                    [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
                    branch.AvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionTrojan advanced promotion " +
                    $"{FormatSession(context)} source={LegacyPromotionTrojanSource} " +
                    $"sourceRange={LegacyPromotionTrojanAdvancedPromotionSourceRange} option={optionId} " +
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
                    LegacyPromotionTrojanSource,
                    [branch.MissingItemText],
                    [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
                    branch.AvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionTrojan advanced promotion " +
                    $"{FormatSession(context)} source={LegacyPromotionTrojanSource} " +
                    $"sourceRange={LegacyPromotionTrojanAdvancedPromotionSourceRange} option={optionId} " +
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
                    "PARITY-GAP LegacyNpc PromotionTrojan advanced promotion missing reward item type " +
                    $"{FormatSession(context)} source={LegacyPromotionTrojanSource} " +
                    $"sourceRange={LegacyPromotionTrojanAdvancedPromotionSourceRange} option={optionId} " +
                    $"rewardItemId={branch.RewardItemId}; character and inventory update skipped.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedSession = session;
        if (branch.RequiredItemId != 0)
        {
            var mutationFrames = new List<byte[]>();
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
                    LegacyPromotionTrojanSource,
                    LegacyPromotionTrojanAdvancedPromotionSourceRange,
                    branch.RewardItemId == 0
                        ? $"consumedItemId={branch.RequiredItemId} consumedCount=1"
                        : $"consumedItemId={branch.RequiredItemId} consumedCount=1 rewardItemId={branch.RewardItemId} rewardCount=1")
                .ConfigureAwait(false);
            await SendLegacyPromotionMasterItemFramesAsync(
                    context with { Session = updatedSession },
                    mutationFrames,
                    cancellationToken)
                .ConfigureAwait(false);

            if (branch.RewardItemId == LegacyPromotionDragonBallItemId)
            {
                await context.Connection.SendAsync(
                        LegacyMsgMessagePacket.SystemTalk(LegacyPromotionTrojanDragonBallReceivedText).ToFrameBytes(),
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
                LegacyPromotionTrojanSource,
                LegacyPromotionTrojanAdvancedPromotionSourceRange)
            .ConfigureAwait(false);

        var updatedContext = context with { Session = updatedSession };
        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyPromotionTrojanSource,
                [branch.SuccessText],
                [new LegacyNpcDialogOption("Thank~you,~Master.", LegacyNpcClosedOption)],
                branch.AvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionTrojan advanced promotion " +
                $"{FormatSession(updatedContext)} source={LegacyPromotionTrojanSource} " +
                $"sourceRange={LegacyPromotionTrojanAdvancedPromotionSourceRange} option={optionId} " +
                $"class={session.Character.Class}->{updatedCharacter.Class} level={session.Character.Level} " +
                $"requiredItemId={branch.RequiredItemId} rewardItemId={branch.RewardItemId}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyTrojanEpicQuestRouteAsync(
        WorldPacketHandlerContext context,
        byte optionId)
    {
        if (optionId != LegacyPromotionTrojanEpicQuestRouteOption)
        {
            return false;
        }

        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc PromotionTrojan epic quest route " +
                $"{FormatSession(context)} source={LegacyPromotionTrojanSource} " +
                $"sourceRange={LegacyPromotionTrojanEpicQuestRouteSourceRange} option={optionId} " +
                "map=1002 x=267 y=194 npcId=10579; " +
                "source calls QuestGUI.SendAutoPatcher(1002, 267, 194, 10579), but no safe local packet model exists.")
            .ConfigureAwait(false);
        return true;
    }

    private static LegacyTrojanAdvancedPromotionBranch GetLegacyTrojanAdvancedPromotionBranch(long classCode)
    {
        return classCode switch
        {
            LegacyPromotionTrojanRankOneClass => new LegacyTrojanAdvancedPromotionBranch(
                LegacyPromotionTrojanRankOneClass,
                RequiredLevel: 40,
                RequiredItemId: 0,
                RewardItemId: 0,
                MissingItemText: "",
                SuccessText: "Excellent! You`ve learned ways of the Trojan. Do you feel a different energy in your body? That`s stronger power and battle prowess!",
                AvatarId: LegacyPromotionTrojanAvatarId),
            LegacyPromotionTrojanRankTwoClass => new LegacyTrojanAdvancedPromotionBranch(
                LegacyPromotionTrojanRankTwoClass,
                RequiredLevel: 70,
                RequiredItemId: LegacyPromotionEmeraldItemId,
                RewardItemId: 0,
                MissingItemText: LegacyPromotionTrojanMissingEmeraldText,
                SuccessText: "Excellent! You`ve learned ways of the Taoist. Do you feel a different energy in your body? That`s stronger power and battle prowess!",
                AvatarId: 6),
            LegacyPromotionTrojanRankThreeClass => new LegacyTrojanAdvancedPromotionBranch(
                LegacyPromotionTrojanRankThreeClass,
                RequiredLevel: 100,
                RequiredItemId: LegacyPromotionMeteorItemId,
                RewardItemId: 0,
                MissingItemText: LegacyPromotionTrojanMissingMeteorText,
                SuccessText: "Excellent! Do you feel a different energy in your body? That`s stronger power and battle prowess!",
                AvatarId: 6),
            LegacyPromotionTrojanRankFourClass => new LegacyTrojanAdvancedPromotionBranch(
                LegacyPromotionTrojanRankFourClass,
                RequiredLevel: 110,
                RequiredItemId: LegacyPromotionMoonBoxItemId,
                RewardItemId: LegacyPromotionDragonBallItemId,
                MissingItemText: LegacyPromotionTrojanMissingMoonBoxText,
                SuccessText: "Excellent!Do you feel a different energy in your body? That`s stronger power and battle prowess!",
                AvatarId: 6),
            _ => default,
        };
    }

    private readonly record struct LegacyTrojanAdvancedPromotionBranch(
        long RequiredClass,
        long RequiredLevel,
        uint RequiredItemId,
        uint RewardItemId,
        string MissingItemText,
        string SuccessText,
        ushort AvatarId);
}
