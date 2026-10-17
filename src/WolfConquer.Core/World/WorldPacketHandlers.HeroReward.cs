using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private readonly record struct LegacyHeroRewardGoal(uint GoalId, byte Claim);
    private readonly record struct LegacyHeroRewardClaim(
        uint GoalId,
        uint Stage,
        IReadOnlyList<StageGoalReward> Rewards);

    private const string RuntimeHeroRewardsAttribute = "HeroRewards";

    [PacketHandler(WorldPacketOpcodes.LegacyHeroReward)]
    private static async ValueTask HandleLegacyHeroRewardAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyHeroRewardPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyHeroReward packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyHeroReward " +
                    $"{FormatSession(context)} action={packet.Action} prize={packet.Prize}; " +
                    "no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == LegacyHeroRewardAction.Show)
        {
            await HandleLegacyHeroRewardShowAsync(context, session, packet, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (packet.Action is LegacyHeroRewardAction.ClaimPrize or LegacyHeroRewardAction.ClaimSubPrize)
        {
            await HandleLegacyHeroRewardClaimAsync(context, session, packet, cancellationToken).ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyHeroReward " +
                $"{FormatSession(context)} action={packet.Action} prize={packet.Prize}; action is not modeled.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyHeroRewardShowAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyHeroRewardPacket packet,
        CancellationToken cancellationToken)
    {
        var stage = Math.Min(packet.Prize, 11u);
        var goals = ParseLegacyHeroRewards(session.Character.Attributes)
            .Where(goal => goal.GoalId / 100 == stage || goal.GoalId == stage)
            .ToArray();
        var response = new LegacyHeroRewardListPacket(
            stage,
            GetLegacyHeroRewardAllDone(stage, goals),
            goals.Select(goal => new LegacyHeroRewardListItem(goal.GoalId, Unknown: 0, goal.Claim)).ToArray());

        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyHeroReward " +
                $"{FormatSession(context)} action={packet.Action} prize={packet.Prize} " +
                $"stage={stage} goals={response.Items.Count} allDone={response.AllDone}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyHeroRewardClaimAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyHeroRewardPacket packet,
        CancellationToken cancellationToken)
    {
        if (!TryCreateLegacyHeroRewardClaim(context.WorldData, packet.Prize, out var claim, out var reason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyHeroReward claim " +
                    $"{FormatSession(context)} action={packet.Action} prize={packet.Prize}; {reason}.")
                .ConfigureAwait(false);
            return;
        }

        var goals = ParseLegacyHeroRewards(session.Character.Attributes);
        var goalIndex = goals.FindIndex(goal => goal.GoalId == claim.GoalId);
        if (goalIndex < 0 || goals[goalIndex].Claim != 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyHeroReward claim " +
                    $"{FormatSession(context)} action={packet.Action} prize={packet.Prize} goal={claim.GoalId}; " +
                    "goal is missing or already claimed.")
                .ConfigureAwait(false);
            return;
        }

        var reward = claim.Rewards[0];
        if (reward.Amount < 1)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyHeroReward claim " +
                    $"{FormatSession(context)} action={packet.Action} prize={packet.Prize}; reward amount={reward.Amount}.")
                .ConfigureAwait(false);
            return;
        }

        if (reward.ItemTypeId < 1 ||
            !TryReadItemType(context.WorldData, checked((uint)reward.ItemTypeId), out var itemType))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyHeroReward claim " +
                    $"{FormatSession(context)} action={packet.Action} prize={packet.Prize}; reward itemTypeId={reward.ItemTypeId} was not loaded.")
                .ConfigureAwait(false);
            return;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, reward.Amount))
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox($"Please make {reward.Amount} spaces in your inventory.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyHeroReward claim " +
                    $"{FormatSession(context)} action={packet.Action} prize={packet.Prize}; inventory is full.")
                .ConfigureAwait(false);
            return;
        }

        var plus = reward.ItemTypeId is >= 730_001 and <= 730_008
            ? checked((byte)(reward.ItemTypeId % 10))
            : (byte)0;
        var items = CreatePurchasedItems(session.PlayerItems, itemType, reward.Amount, isBound: true, plus: plus);
        goals[goalIndex] = goals[goalIndex] with { Claim = 1 };
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems.Concat(items).ToArray(),
        };
        var updatedSession = session with
        {
            Character = WithCharacterAttribute(
                session.Character,
                RuntimeHeroRewardsAttribute,
                SerializeLegacyHeroRewards(goals)),
            PlayerItems = updatedItems,
        };
        context.Sessions?.Set(context.Connection, updatedSession);

        foreach (var item in items)
        {
            await context.Connection.SendAsync(
                    LegacyGameItemPacket.FromSnapshotItem(item).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyHeroReward claim " +
                $"{FormatSession(context)} action={packet.Action} prize={packet.Prize} stage={claim.Stage} " +
                $"itemTypeId={reward.ItemTypeId} count={reward.Amount} bound=1.")
            .ConfigureAwait(false);
    }

    private static byte GetLegacyHeroRewardAllDone(uint stage, IReadOnlyList<LegacyHeroRewardGoal> goals)
    {
        var hasClaimedStagePrize = false;
        var allKnownGoalsClaimed = true;
        var knownStageGoalCount = 0;

        foreach (var goal in goals)
        {
            if (goal.GoalId == stage && goal.Claim > 0)
            {
                hasClaimedStagePrize = true;
            }
            else if (goal.GoalId / 100 == stage)
            {
                knownStageGoalCount++;
                if (goal.Claim == 0)
                {
                    allKnownGoalsClaimed = false;
                }
            }
        }

        return hasClaimedStagePrize && knownStageGoalCount > 0 && allKnownGoalsClaimed ? (byte)1 : (byte)0;
    }

    private static bool TryCreateLegacyHeroRewardClaim(
        WorldDataCatalog? worldData,
        uint prize,
        out LegacyHeroRewardClaim claim,
        out string reason)
    {
        claim = default;
        if (worldData is null)
        {
            reason = "world data was not loaded";
            return false;
        }

        if (prize >= 100)
        {
            var stage = prize / 100;
            var goalId = prize % 100;
            if (stage == 0 ||
                goalId == 0 ||
                stage > int.MaxValue ||
                goalId > int.MaxValue ||
                !worldData.StageGoalTasksByStageAndGoalId.TryGetValue(((int)stage, (int)goalId), out var task) ||
                task.Rewards.Count == 0)
            {
                reason = "stage-goal task was not loaded";
                return false;
            }

            claim = new LegacyHeroRewardClaim(prize, stage, task.Rewards);
            reason = "";
            return true;
        }

        if (prize == 0 ||
            prize > int.MaxValue ||
            !worldData.StageGoalStagesById.TryGetValue((int)prize, out var stageGoal) ||
            stageGoal.Rewards.Count == 0)
        {
            reason = "stage reward was not loaded";
            return false;
        }

        claim = new LegacyHeroRewardClaim(prize, prize, stageGoal.Rewards);
        reason = "";
        return true;
    }

    private static WorldSession AddLegacyHeroRewardGoal(WorldSession session, uint goalId)
    {
        var goals = ParseLegacyHeroRewards(session.Character.Attributes);
        if (!goals.Any(goal => goal.GoalId == goalId))
        {
            goals.Add(new LegacyHeroRewardGoal(goalId, Claim: 0));
        }

        return session with
        {
            Character = WithCharacterAttribute(
                session.Character,
                RuntimeHeroRewardsAttribute,
                SerializeLegacyHeroRewards(goals)),
        };
    }

    private static List<LegacyHeroRewardGoal> ParseLegacyHeroRewards(
        IReadOnlyDictionary<string, string> attributes)
    {
        if (!attributes.TryGetValue(RuntimeHeroRewardsAttribute, out var raw) ||
            string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var fields = raw.Split('/');
        var offset = 0;
        var count = ReadLegacyDelimitedInt(fields, ref offset);
        var goals = new List<LegacyHeroRewardGoal>(Math.Max(0, count));
        var seen = new HashSet<uint>();
        for (var index = 0; index < count; index++)
        {
            var goalId = ReadLegacyDelimitedUInt32(fields, ref offset);
            var claim = ReadLegacyDelimitedByte(fields, ref offset);
            if (goalId != 0 && seen.Add(goalId))
            {
                goals.Add(new LegacyHeroRewardGoal(goalId, claim));
            }
        }

        return goals;
    }

    private static string SerializeLegacyHeroRewards(IReadOnlyList<LegacyHeroRewardGoal> goals)
    {
        var fields = new List<string>(1 + (goals.Count * 2))
        {
            goals.Count.ToString(CultureInfo.InvariantCulture),
        };

        foreach (var goal in goals)
        {
            fields.Add(goal.GoalId.ToString(CultureInfo.InvariantCulture));
            fields.Add(goal.Claim.ToString(CultureInfo.InvariantCulture));
        }

        return string.Join('/', fields) + "/";
    }

}
