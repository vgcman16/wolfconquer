namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<LegacyEquipmentQueryPublicationResult> SendLegacyFineRainAcquisitionFrameAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyEquipmentQueryPublicationScope scope,
        byte[] capturedFrame,
        CancellationToken cancellationToken)
    {
        var current = RefreshLegacyEquipmentQueryPublication(context, session, scope);
        if (!current.Completed) return current;
        await context.Connection.SendAsync(capturedFrame, cancellationToken).ConfigureAwait(false);
        current = RefreshLegacyEquipmentQueryPublication(context, session, scope);
        if (!current.Completed) return current;
        if (context.Sessions is not { } sessions) return current;

        // Source SendUpdate captures bytes ONCE, owner first then current view.
        // SameUID power/flag/recast changes do not cancel these entered peer sends.
        foreach (var candidate in sessions.Snapshot())
        {
            current = RefreshLegacyEquipmentQueryPublication(context, current.Session!, scope);
            if (!current.Completed) return current;
            if (ReferenceEquals(candidate.Connection, context.Connection) ||
                !sessions.TryGet(candidate.Connection, out var peer) || peer is null ||
                peer.Character.Uid != candidate.Session.Character.Uid || peer.Character.Uid == scope.OwnerUid ||
                !CanSeePlayer(current.Session!.Character, peer.Character))
                continue;
            await candidate.Connection.SendAsync(capturedFrame, cancellationToken).ConfigureAwait(false);
            current = RefreshLegacyEquipmentQueryPublication(context, current.Session!, scope);
            if (!current.Completed) return current;
        }
        return current;
    }
}
