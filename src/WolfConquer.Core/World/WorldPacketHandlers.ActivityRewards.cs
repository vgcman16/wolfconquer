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
    private static async ValueTask<WorldSession> GrantLegacyQualifierRewardItemAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        WorldSession session,
        uint rewardItemId,
        string qualifierName,
        string reason,
        CancellationToken cancellationToken)
    {
        if (!TryReadItemType(context.WorldData, rewardItemId, out var itemType))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped {qualifierName} reward item " +
                    $"uid={session.Character.Uid} itemId={rewardItemId} reason='{reason}'; item type was not loaded.")
                .ConfigureAwait(false);
            return session;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            var returnedReward = CreatePurchasedItems(session.PlayerItems, itemType, 1)[0]
                .WithWarehouseId(LegacyReturnedItemsWarehouseId, position: 0);
            var returnedItems = session.PlayerItems.WarehouseItems
                .Concat([returnedReward])
                .OrderBy(item => item.WarehouseId)
                .ThenBy(item => item.Uid)
                .ToArray();
            var returnedSnapshot = session.PlayerItems with
            {
                WarehouseItems = returnedItems,
            };
            var returnedSession = session with { PlayerItems = returnedSnapshot };
            context.Sessions?.Set(connection, returnedSession);

            await connection.SendAsync(
                    LegacyGameItemPacket.FromSnapshotItem(
                        returnedReward,
                        mode: LegacyGameItemPacket.AddItemReturnedMode).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    $"WorldServer granted {qualifierName} returned reward item " +
                    $"uid={session.Character.Uid} itemId={rewardItemId} reason='{reason}' " +
                    $"warehouseId={LegacyReturnedItemsWarehouseId}.")
                .ConfigureAwait(false);
            return returnedSession;
        }

        var reward = CreatePurchasedItems(session.PlayerItems, itemType, 1)[0];
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems.Concat([reward]).ToArray(),
        };
        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(connection, updatedSession);

        await connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(reward).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer granted {qualifierName} reward item " +
                $"uid={session.Character.Uid} itemId={rewardItemId} reason='{reason}'.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> IncreaseLegacyActivityTaskAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        WorldSession session,
        uint taskId,
        CancellationToken cancellationToken)
    {
        if (context.WorldData is not { } worldData ||
            taskId > int.MaxValue ||
            !worldData.ActivityTasksBySectionId.TryGetValue((int)taskId, out var taskDefinition))
        {
            return session;
        }

        var state = LegacyActivenessState.FromSnapshot(session.Character);
        var tasks = state.Tasks.ToList();
        var taskIndex = tasks.FindIndex(task => task.Id == taskId);
        LegacyActivityTaskState task;
        if (taskIndex >= 0)
        {
            task = tasks[taskIndex];
        }
        else
        {
            if (!IsLegacyActivityTaskOpen(taskDefinition, session.Character))
            {
                return session;
            }

            task = new LegacyActivityTaskState(taskId, Progress: 0, Completed: 0);
            taskIndex = tasks.Count;
            tasks.Add(task);
        }

        if (task.Completed != 0)
        {
            return session;
        }

        var requiredProgress = CheckedByte(taskDefinition.Progress, nameof(taskDefinition.Progress));
        var progress = task.Progress;
        if (progress < requiredProgress)
        {
            progress++;
        }

        var completed = task.Completed;
        var activityPoints = state.ActivityPoints;
        if (progress == requiredProgress)
        {
            completed = 1;
            activityPoints = checked(activityPoints + CheckedByte(taskDefinition.ActiveValue, nameof(taskDefinition.ActiveValue)));
            await connection.SendAsync(
                    new LegacyActivityRewardsInfoPacket(
                        LegacyActivityRewardsInfoPacket.InitializeListAction,
                        activityPoints).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var updatedTask = new LegacyActivityTaskState(taskId, progress, completed);
        tasks[taskIndex] = updatedTask;
        var updatedState = new LegacyActivenessState(state.ClaimRewards, activityPoints, tasks.ToArray());
        var updatedSession = session with
        {
            Character = WithCharacterAttribute(
                session.Character,
                "Activeness",
                SerializeLegacyActiveness(updatedState)),
        };
        context.Sessions?.Set(connection, updatedSession);

        await connection.SendAsync(
                new LegacyActivityTasksPacket(
                    LegacyActivityTasksPacket.InitializeListAction,
                    [updatedTask]).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static bool IsLegacyActivityTaskOpen(ActivityTaskDefinition taskDefinition, CharacterSnapshot character)
    {
        var openLevel = checked((uint)(taskDefinition.OpenLevel ?? 0));
        var requiredLevel = openLevel % 1000;
        var requiredReborn = (openLevel - requiredLevel) / 1000;
        return character.Level >= requiredLevel && character.Reborn >= requiredReborn;
    }

    private static string SerializeLegacyActiveness(LegacyActivenessState state)
    {
        var fields = new List<string>(3 + (state.Tasks.Count * 3))
        {
            state.ClaimRewards.ToString(CultureInfo.InvariantCulture),
            state.ActivityPoints.ToString(CultureInfo.InvariantCulture),
            state.Tasks.Count.ToString(CultureInfo.InvariantCulture),
        };

        foreach (var task in state.Tasks)
        {
            fields.Add(task.Id.ToString(CultureInfo.InvariantCulture));
            fields.Add(task.Progress.ToString(CultureInfo.InvariantCulture));
            fields.Add(task.Completed.ToString(CultureInfo.InvariantCulture));
        }

        return string.Join('/', fields) + "/";
    }

    private static int ReadLegacyDelimitedInt(IReadOnlyList<string> fields, ref int offset)
    {
        if (offset < fields.Count &&
            int.TryParse(fields[offset], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            offset++;
            return value;
        }

        offset++;
        return 0;
    }

    private static uint ReadLegacyDelimitedUInt32(IReadOnlyList<string> fields, ref int offset)
    {
        if (offset < fields.Count &&
            uint.TryParse(fields[offset], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            offset++;
            return value;
        }

        offset++;
        return 0;
    }

    private static byte ReadLegacyDelimitedByte(IReadOnlyList<string> fields, ref int offset)
    {
        if (offset < fields.Count &&
            byte.TryParse(fields[offset], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            offset++;
            return value;
        }

        offset++;
        return 0;
    }

    private static byte CheckedByte(int value, string name)
    {
        if (value is < byte.MinValue or > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy activity task value exceeds byte range.");
        }

        return (byte)value;
    }
}
