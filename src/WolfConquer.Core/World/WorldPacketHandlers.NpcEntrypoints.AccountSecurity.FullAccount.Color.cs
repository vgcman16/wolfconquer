namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyFullAccountSelectedColor = 5_855_577;

    private static async ValueTask SendLegacyAccountSecurityFullAccountColorAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var character = session.Character;
        var color = optionId == 20 ? LegacyFullAccountSelectedColor : 0U;
        var action = new MsgActionPacket
        {
            ObjId = CheckedUInt32(character.Uid, nameof(character.Uid)),
            Type = MsgActionType.LegacyColor,
            DwParam = color,
            PositionX = CheckedUInt32(character.X, nameof(character.X)),
            PositionY = CheckedUInt32(character.Y, nameof(character.Y)),
        };

        await context.Connection.SendAsync(action.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc AccountSecurity FullAccount color action " +
                $"{FormatSession(context)} source={LegacyAccountSecurityFullAccountSource} " +
                $"sourceLines={LegacyAccountSecurityFullAccountSourceLines} optionId={optionId} " +
                $"actionType={(ushort)MsgActionType.LegacyColor} color={color} " +
                $"position={character.X},{character.Y} packetOrder=MsgAction.")
            .ConfigureAwait(false);
    }
}
