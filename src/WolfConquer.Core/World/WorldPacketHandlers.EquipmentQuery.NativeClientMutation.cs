namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal static LegacyEquipmentQueryPublicationResult CommitLegacyEquipmentQueryMutation(
        WorldPacketHandlerContext context, WorldSession session, LegacyEquipmentQueryPublicationScope scope,
        Func<WorldSession, WorldSession> update)
    {
        var current = RefreshLegacyEquipmentQueryPublication(context, session, scope);
        if (!current.Completed) return current;
        if (context.Sessions is { } world && scope.OwnerNativeClientIdentity is { } identity)
        {
            // Expected client identity and CURRENT record are checked/updated under ONE connection gate.
            if (!LegacyNativeClientPublication.TryUpdate(world, context.Connection, scope.OwnerUid,
                    identity, update, out var written))
                return new(null, current.RequiredParticipantSession,
                    current.Loss | LegacyEquipmentQueryPublicationLoss.OwnerLost);
            session = written;
        }
        else
        {
            session = update(current.Session!);
            context.Sessions?.Set(context.Connection, session); // Existing unbound/loading adapter, not native cache authority.
        }
        return RefreshLegacyEquipmentQueryPublication(context with { Session = session }, session, scope);
    }
}
