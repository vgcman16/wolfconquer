using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    [PacketHandler(WorldPacketOpcodes.LegacySecondaryPassword)]
    private static async ValueTask HandleLegacySecondaryPasswordAsync(WorldPacketHandlerContext context,
        PacketFrame frame, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!LegacySecondaryPasswordPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync("WorldServer rejected malformed LegacySecondaryPassword.");
            return;
        }
        LegacySecondaryPasswordTransition? response = null;
        var reason = "ownerUnavailable";
        // Like Trade, mutate/persist exclusively but release the lease before
        // awaiting sockets. No PIN values or raw packets enter diagnostic logs.
        using (var operation = context.TradeOperations is { } operations
            ? await operations.EnterExclusiveAsync(cancellationToken).ConfigureAwait(false) : null)
        lock (LegacySecondaryPasswordRuntime.SyncRoot)
        {
            if (context.TradeRecovery?.IsBlocked == true) reason = "recoveryRequired";
            else if (context.Sessions is { } sessions && sessions.TryGet(context.Connection, out var current) &&
                current is not null && LegacySecondaryPasswordRuntime.TryGet(context.Connection, current,
                    out var state, out var wasChecked))
            {
                // Native DateTime.Now carries Local kind. Preserve that field
                // identity while allowing the provider to supply the local wall clock.
                var now = DateTime.SpecifyKind(
                    (context.TimeProvider ?? TimeProvider.System).GetLocalNow().DateTime, DateTimeKind.Local);
                var transition = state.Apply(packet, wasChecked, now);
                if (transition is null) reason = "ignoredAction";
                else
                {
                    var changed = transition.State != state;
                    var updated = changed ? LegacySecondaryPasswordPersistence.WithState(current, transition.State) : current;
                    var failure = string.Empty;
                    if (sessions.TryCommitSession(context.Connection, current, updated, () =>
                    {
                        if (!LegacySecondaryPasswordRuntime.TryGet(context.Connection, current, out var latest, out var latestChecked) ||
                            latest != state || latestChecked != wasChecked)
                        {
                            failure = "secondaryPasswordOwnerChanged";
                            return false;
                        }
                        return changed ? LegacySecondaryPasswordPersistence.TryPersist(current, transition.State, out failure)
                            : LegacySecondaryPasswordPersistence.TryValidateBaseline(current, out failure);
                    }))
                    {
                        if (LegacySecondaryPasswordRuntime.TryPublish(context.Connection, current, updated,
                            transition.State, transition.IsChecked))
                        {
                            response = transition;
                            reason = "handled";
                        }
                        else
                        {
                            context.TradeRecovery?.Block("secondaryPasswordRecoveryRequired:ownerChangedAfterCommit");
                            reason = "ownerChangedAfterCommit";
                        }
                    }
                    else
                    {
                        if (failure.StartsWith("secondaryPasswordRecoveryRequired", StringComparison.Ordinal))
                            context.TradeRecovery?.Block(failure);
                        reason = "commitRejected:" + failure;
                    }
                }
            }
        }
        if (response is not null)
        {
            await context.Connection.SendAsync(response.Reply.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            if (response.Message.Length != 0)
                await context.Connection.SendAsync(LegacyMsgMessagePacket.SystemTalk(response.Message).ToFrameBytes(), cancellationToken)
                    .ConfigureAwait(false);
        }
        await context.Log.WriteLineAsync($"WorldServer handled LegacySecondaryPassword action={packet.Action}; result={reason}.")
            .ConfigureAwait(false);
    }
}
