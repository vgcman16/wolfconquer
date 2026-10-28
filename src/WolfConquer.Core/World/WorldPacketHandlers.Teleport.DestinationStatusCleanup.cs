namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal static async ValueTask<WorldSession?> ApplyLegacyTeleportDestinationStatusCleanupAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (session.Character.MapId is not (1_038 or 3_868) ||
            !HasLegacyStatusFlag(session.Character, LegacyFatalStrikeFlag))
        {
            return session;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (context.Sessions is not { } sessions)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy GameClient.Teleport destination FatalStrike cleanup " +
                    $"{FormatSession(context with { Session = session })} destinationMap={session.Character.MapId}; " +
                    "WorldSessionStore is unavailable, so the cleanup cannot be compare-and-set.")
                .ConfigureAwait(false);
            return null;
        }

        var updatedCharacter = WithoutCharacterAttribute(
            WithoutLegacyStatusFlag(session.Character, LegacyFatalStrikeFlag),
            RuntimeLegacyFatalStrikeExpiresAtUtcAttribute);
        var updatedSession = session with { Character = updatedCharacter };
        if (!TryCommitOwnedLegacyTeleport(context, session, updatedSession, out var committedSession))
        {
            sessions.TryGet(context.Connection, out var currentSession);
            var currentUid = currentSession?.Character.Uid.ToString() ?? "missing";
            var currentMap = currentSession?.Character.MapId.ToString() ?? "missing";
            await context.Log.WriteLineAsync(
                    "WorldServer skipped stale legacy GameClient.Teleport destination FatalStrike cleanup " +
                    $"expectedUid={session.Character.Uid} expectedDestinationMap={session.Character.MapId} " +
                    $"currentUid={currentUid} currentMap={currentMap}; " +
                    "WorldSessionStore.TryCompareAndSet rejected the transition session, so no status packet was sent.")
                .ConfigureAwait(false);
            return null;
        }

        updatedSession = committedSession!;

        await SendPlayerStatusFlagVectorToVisibleAsync(
                context with { Session = updatedSession },
                updatedSession,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy GameClient.Teleport destination FatalStrike cleanup " +
                $"{FormatSession(context with { Session = updatedSession })} destinationMap={updatedSession.Character.MapId}; " +
                "source=GameClient.Teleport lines 2010-2013.")
            .ConfigureAwait(false);
        return updatedSession;
    }
}
