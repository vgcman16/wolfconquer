using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask TryOpenLegacyAdminDragonWarAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        CancellationToken cancellationToken)
    {
        if (context.DragonWarStates is not { } dragonWarStates)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer ignored LegacyNpc DragonWar admin open; DragonWar runtime state store is unavailable " +
                    $"{FormatSession(context)} source={sourceMethod} option={LegacyAdminOpenDragonWarOption}.")
                .ConfigureAwait(false);
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    ["DragonWar runtime is not available on this server."],
                    [new("I understand.", LegacyNpcClosedOption)],
                    LegacyAdminUtilityAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var update = dragonWarStates.Open(DateTimeOffset.Now);
        if (update is null)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer ignored LegacyNpc DragonWar admin open; DragonWar is already open or running " +
                    $"{FormatSession(context)} source={sourceMethod} option={LegacyAdminOpenDragonWarOption} " +
                    $"process={dragonWarStates.Process}.")
                .ConfigureAwait(false);
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    ["DragonWar is already open or running."],
                    [new("I understand.", LegacyNpcClosedOption)],
                    LegacyAdminUtilityAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var sentCount = await SendLegacyAdminDragonWarOpenUpdateAsync(
                context,
                update,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer opened legacy DragonWar from admin utility " +
                $"{FormatSession(context)} source={sourceMethod} option={LegacyAdminOpenDragonWarOption} " +
                $"promptedSessions={sentCount}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask<int> SendLegacyAdminDragonWarOpenUpdateAsync(
        WorldPacketHandlerContext context,
        LegacyRuntimeUpdate update,
        CancellationToken cancellationToken)
    {
        var activeSessions = context.Sessions?.Snapshot();
        if (context.Sessions is { } sessions && activeSessions is { Count: > 0 })
        {
            var sentCount = 0;
            foreach (var active in activeSessions)
            {
                await SendLegacyAdminDragonWarOpenUpdateToConnectionAsync(
                        context,
                        active.Connection,
                        active.Session,
                        sessions,
                        update,
                        cancellationToken)
                    .ConfigureAwait(false);
                sentCount++;
            }

            return sentCount;
        }

        var fallbackSession = context.Session;
        if (fallbackSession is null &&
            context.Sessions?.TryGet(context.Connection, out var storedSession) == true)
        {
            fallbackSession = storedSession;
        }

        if (fallbackSession is null)
        {
            await context.Connection.SendAsync(update.FrameBytes, cancellationToken).ConfigureAwait(false);
            return 1;
        }

        await SendLegacyAdminDragonWarOpenUpdateToConnectionAsync(
                context,
                context.Connection,
                fallbackSession,
                context.Sessions,
                update,
                cancellationToken)
            .ConfigureAwait(false);
        return 1;
    }

    private static async ValueTask SendLegacyAdminDragonWarOpenUpdateToConnectionAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        WorldSession session,
        WorldSessionStore? sessions,
        LegacyRuntimeUpdate update,
        CancellationToken cancellationToken)
    {
        await connection.SendAsync(update.FrameBytes, cancellationToken).ConfigureAwait(false);

        if (update.MessageBoxPrompt is not { } prompt)
        {
            return;
        }

        var promptedSession = session with
        {
            Character = WithRuntimeMessageBoxPrompt(session.Character, prompt),
        };
        sessions?.Set(connection, promptedSession);

        await connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox(prompt.Text).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
