namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleTeamSearchForMemberActionAsync(
        WorldPacketHandlerContext context,
        MsgActionPacket packet,
        CancellationToken cancellationToken)
    {
        if (context.Session is not { } session ||
            packet.DwParam == 0)
        {
            return;
        }

        var teamStates = context.TeamStates ?? new TeamSessionStateStore();
        if (!teamStates.HasTeam(context.Connection) ||
            !TryGetTeamConnectionByUid(context, teamStates, context.Connection, packet.DwParam, out var targetConnection) ||
            context.Sessions?.TryGet(targetConnection, out var targetSession) != true ||
            targetSession is null ||
            targetSession.Character.MapId != session.Character.MapId)
        {
            return;
        }

        var response = packet with
        {
            PositionX = CheckedUInt32(targetSession.Character.X, nameof(targetSession.Character.X)),
            PositionY = CheckedUInt32(targetSession.Character.Y, nameof(targetSession.Character.Y)),
        };

        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled MsgAction TeamSearchForMember " +
                $"{FormatSession(context)} requestedUid={packet.DwParam} " +
                $"x={response.PositionX} y={response.PositionY}; " +
                "source=MsgDataPacket.TeamSearchForMember [Data(106)] sends ActionCreate(data) with member coordinates when requester and target share a map.")
            .ConfigureAwait(false);
    }
}
