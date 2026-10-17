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
    [PacketHandler(WorldPacketOpcodes.MsgQuestData)]
    private static async ValueTask HandleMsgQuestDataAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (MsgQuestDataPacket.TryParseTaskDetailFrame(frame, out var taskDetailFrame))
        {
            var runtimeQuestsFound = 0;
            var responses = 0;

            if (context.Session is { } taskDetailSession)
            {
                foreach (var record in taskDetailFrame.Records)
                {
                    if (!taskDetailSession.Character.PlayerQuests.TryGetQuest(record.Uid, out var taskDetailQuest))
                    {
                        continue;
                    }

                    runtimeQuestsFound++;
                    var response = MsgQuestDataPacket.CreateObjectiveFrame(taskDetailQuest);
                    await context.Connection.SendAsync(response, cancellationToken)
                        .ConfigureAwait(false);
                    responses++;
                }
            }

            var taskUids = string.Join(',', taskDetailFrame.Records.Select(record => record.Uid));
            var incomingIntentions = string.Join(
                ';',
                taskDetailFrame.Records.Select(
                    record => $"{record.Uid}:[{string.Join(',', record.Intentions)}]"));
            var noResponseSuffix = responses == 0 ? "; no objective response sent." : ".";

            await context.Log.WriteLineAsync(
                    "WorldServer handled MsgQuestData " +
                    $"{FormatSession(context)} layout=latest-client-task-detail " +
                    $"control={taskDetailFrame.Control} recordCount={taskDetailFrame.Records.Count} " +
                    $"taskUids={taskUids} incomingIntentions={incomingIntentions} " +
                    $"payloadLength={frame.Payload.Length} runtimeQuestsFound={runtimeQuestsFound} " +
                    $"responses={responses}{noResponseSuffix}")
                .ConfigureAwait(false);
            return;
        }

        if (!MsgQuestDataPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed MsgQuestData packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is { } session &&
            session.Character.PlayerQuests.TryGetQuest(packet.Uid, out var quest))
        {
            var response = MsgQuestDataPacket.CreateObjectiveFrame(quest);
            await context.Connection.SendAsync(response, cancellationToken)
                .ConfigureAwait(false);

            await context.Log.WriteLineAsync(
                    "WorldServer handled MsgQuestData " +
                    $"{FormatSession(context)} unknown={packet.Unknown} uid={packet.Uid} intent={packet.Intent} " +
                    $"layout=legacy payloadLength={frame.Payload.Length} runtimeQuest=found " +
                    $"objectiveCount={quest.Intentions.Count} responseBytes={response.Length}.")
                .ConfigureAwait(false);
            return;
        }

        var runtimeQuestState = context.Session is null ? "session-unavailable" : "not-found";

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgQuestData " +
                $"{FormatSession(context)} unknown={packet.Unknown} uid={packet.Uid} intent={packet.Intent} " +
                $"layout=legacy payloadLength={frame.Payload.Length} " +
                $"runtimeQuest={runtimeQuestState}; no objective response sent.")
            .ConfigureAwait(false);
    }

}
