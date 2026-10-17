namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static void CaptureLegacyTeamMemberInfo(
        WorldPacketHandlerContext context,
        TeamSessionStateStore teams,
        WorldSession session,
        Networking.ConquerTcpConnection connection)
    {
        // Team.MemberInfo41-50 snapshots identity/mesh at the REAL membership point.
        // Constructor clamps; later HitPoints setter deliberately uses ushort casts.
        teams.InitializeLegacyMemberInfo(connection, session.Character.Uid,
            new LegacyTeamMemberInfoEntry(
                session.Character.Name,
                CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
                BuildLegacyCharacterMesh(session.Character),
                Math.Min(ushort.MaxValue, GetLegacyEquipmentRuntimeMaxHitpoints(session, context.WorldData)),
                unchecked((ushort)Math.Min(ushort.MaxValue, session.Character.MinHitPoints))));
    }

    private static async ValueTask<LegacyEquipmentQueryPublicationResult>
        SendLegacyAssignedHitpointsTeamInfoAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            LegacyEquipmentQueryPublicationScope scope,
            int assignedValue,
            CancellationToken cancellationToken)
    {
        var current = RefreshLegacyEquipmentQueryPublication(context, session, scope);
        if (!current.Completed) return current;
        session = current.Session!;
        if (context.TeamStates is not { } teams ||
            !teams.TryUpdateLegacyMemberHitpoints(
                context.Connection, scope.OwnerUid,
                GetLegacyEquipmentRuntimeMaxHitpoints(session, context.WorldData),
                assignedValue, out var member))
        {
            return current; // Source Team==null or GetMember(UID)==null.
        }

        // One-member packet is captured once before source SendTeamInfo fanout.
        var bytes = new LegacyTeamMemberInfoPacket(
            LegacyTeamMemberInfoAction.AddMember, Count: 1, Unknown: 1, [member!]).ToFrameBytes();
        // Member identity comes from membership, never a rebound current occupant.
        var recipients = teams.SnapshotLegacyMemberRecipients(context.Connection);
        foreach (var recipient in recipients)
        {
            current = RefreshLegacyEquipmentQueryPublication(context, session, scope);
            if (!current.Completed) return current;
            session = current.Session!;
            if (context.Sessions is { } sessions &&
                (!sessions.TryGet(recipient.Connection, out var viewer) || viewer is null ||
                 viewer.Character.Uid != recipient.Uid))
            {
                continue;
            }

            await recipient.Connection.SendAsync(bytes, cancellationToken).ConfigureAwait(false);
            current = RefreshLegacyEquipmentQueryPublication(context, session, scope);
            if (!current.Completed) return current;
            session = current.Session!;
        }

        return current;
    }
}
