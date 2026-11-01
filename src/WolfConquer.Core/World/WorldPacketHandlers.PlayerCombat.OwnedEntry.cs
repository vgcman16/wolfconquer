namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Called before the generic Intensify/Focused/gear prefixes. A known owned
    // player can only use the sealed joint settlement, never fall through to a
    // legacy path that publishes Interaction before its HP writer.
    private static async ValueTask<bool> TryHandleOwnedBasicPhysicalEntryAsync(WorldPacketHandlerContext context,
        MsgAttackPacket packet, CancellationToken cancellationToken)
    {
        if (packet.Query.AttackType != MsgAttackType.Physical ||
            context.Session?.NativeGameClient?.Player?.HasOwnedActionAttachment != true) return false;
        if (context.Sessions is not { } store || context.Session is not { } actor ||
            !store.TryPrepareBasicPhysicalEntityContext(context, actor, out var actual) ||
            packet.Query.Uid != actor.Character.Uid ||
            packet.Query.Damage > 0 && !store.IsActualBasicPhysicalServerRequest(context, packet)) return true;
        context = actual;
        var matches = store.Snapshot().Where(row => row.Session.Character.Uid == packet.Query.OpponentUid).ToArray();
        var nonPlayers = ReadOwnedPhysicalNonPlayerTargets(context, actor, packet.Query.OpponentUid);
        if (matches.Length + nonPlayers.NamespaceCount != 1) return true;
        if (matches.Length == 0) return !CanEnterOwnedPhysicalNonPlayer(context, actor, nonPlayers);
        var target = matches[0];
        if (ReferenceEquals(target.Connection, context.Connection) ||
            !CanOwnedBasicPhysicalDamageTarget(context, actor, target.Session,
                (context.TimeProvider ?? throw new InvalidOperationException("basicPhysicalClockOwnerMissing")).GetUtcNow(), out _)) return true;
        if (!store.TryBeginBasicPhysical(context, packet, target, out var attempt) || attempt is null) return true;
        context = context with { BasicPhysicalAttempt = attempt };
        try
        {
            if (!store.TryCommitOwnedBasicPhysical(context, packet, target, out var publication, out var reason) || publication is null)
            {
                await context.Log.WriteLineAsync($"WorldServer refused owned basic physical {FormatSession(context)} reason={reason}.")
                    .ConfigureAwait(false);
                return true;
            }
            bool complete = await publication.PublishAsync(cancellationToken).ConfigureAwait(false);
            await context.Log.WriteLineAsync($"WorldServer committed owned basic physical {FormatSession(context)} " +
                $"targetUid={packet.Query.OpponentUid} outputComplete={complete} phase={publication.OutputPhase}.").ConfigureAwait(false);
            return true;
        }
        finally { store.EndBasicPhysicalAttempt(attempt); }
    }
}
