using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask SendLegacyTeleportEnvironmentAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        CancellationToken cancellationToken)
    {
        var color = ReadAttributeUInt64(character.Attributes, "color", "Color");
        if (color != 0)
        {
            await context.Connection.SendAsync(
                    new MsgActionPacket
                    {
                        ObjId = CheckedUInt32(character.Uid, nameof(character.Uid)),
                        DwParam = CheckedUInt32(checked((long)color), "color"),
                        Type = MsgActionType.LegacyColor,
                        PositionX = CheckedUInt32(character.X, nameof(character.X)),
                        PositionY = CheckedUInt32(character.Y, nameof(character.Y)),
                    }.ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (LegacyWeatherPacket.TryCreateStartup(character, out var weatherPacket))
        {
            await context.Connection.SendAsync(weatherPacket.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
