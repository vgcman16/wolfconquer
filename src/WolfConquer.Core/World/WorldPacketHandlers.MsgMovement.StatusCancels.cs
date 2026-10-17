using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask SendMovementAzureShieldRemovalIfNeededAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        bool removedMagicDefenderFlag,
        CancellationToken cancellationToken)
    {
        if (!removedMagicDefenderFlag)
        {
            return;
        }

        var packet = new LegacyMsgUpdatePacket(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            [
                new LegacyMsgUpdateEntry(
                    LegacyMsgUpdateDataType.AzureShield,
                    Value: 0,
                    Flag: LegacyMagicDefenderFlag),
            ]);
        var frame = packet.ToFrameBytes();
        await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);

        var visiblePeerCount = 0;
        if (context.Sessions is { } sessions)
        {
            foreach (var active in sessions.Snapshot())
            {
                if (ReferenceEquals(active.Connection, context.Connection) ||
                    active.Session.Character.Uid == session.Character.Uid ||
                    !CanSeePlayer(active.Session.Character, session.Character))
                {
                    continue;
                }

                await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                visiblePeerCount++;
            }
        }

        await context.Log.WriteLineAsync(
                "WorldServer sent MsgMovement AzureShield removal " +
                $"{FormatSession(context)} playerCount={visiblePeerCount}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendMovementFocusedCancelIfNeededAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        bool removedFocusedFlag,
        CancellationToken cancellationToken)
    {
        await SendLegacyFocusedCancelIfNeededAsync(
                context,
                session,
                removedFocusedFlag,
                "MsgMovement",
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyFocusedCancelIfNeededAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        bool removedFocusedFlag,
        string packetName,
        CancellationToken cancellationToken)
    {
        var focusClientSpellId = ReadAttributeUInt64(session.Character.Attributes, "FocusClientSpellId", "FocusClientSpellID");
        if (!removedFocusedFlag || focusClientSpellId == 0 || focusClientSpellId > uint.MaxValue)
        {
            return;
        }

        var action = new MsgActionPacket
        {
            ObjId = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            TargetUid = (uint)focusClientSpellId,
            Timestamp = unchecked((uint)Environment.TickCount),
            Type = MsgActionType.CancelFocusedSpell,
            Facing = ReadAttributeUInt16(session.Character.Attributes, "Angle"),
            PositionX = CheckedUInt32(session.Character.X, nameof(session.Character.X)),
            PositionY = CheckedUInt32(session.Character.Y, nameof(session.Character.Y)),
        };

        await context.Connection.SendAsync(action.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer sent {packetName} focused cancel " +
                $"{FormatSession(context)} targetUid={(uint)focusClientSpellId}.")
            .ConfigureAwait(false);
    }
}
