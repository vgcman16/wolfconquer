using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string RuntimeAutoHuntAttribute = "OnAutoHunt";
    private const long LegacyAutoHuntBlockedMapId = 10_250;

    [PacketHandler(WorldPacketOpcodes.LegacyAutoHunt)]
    private static async ValueTask HandleLegacyAutoHuntAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyAutoHuntPacket.TryParse(frame, out _))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyAutoHunt packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyAutoHunt {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (session.Character.MapId == LegacyAutoHuntBlockedMapId)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("You cannot use Auto Hunt in here.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    $"WorldServer blocked LegacyAutoHunt {FormatSession(context)} map={session.Character.MapId}.")
                .ConfigureAwait(false);
            return;
        }

        if (ReadAttributeBool(session.Character.Attributes, RuntimeAutoHuntAttribute))
        {
            await context.Connection.SendAsync(new LegacyAutoHuntPacket(3, 0).ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
            var updatedCharacter = WithCharacterAttribute(session.Character, RuntimeAutoHuntAttribute, "0");
            context.Sessions?.Set(context.Connection, session with { Character = updatedCharacter });
            await context.Log.WriteLineAsync(
                    $"WorldServer stopped LegacyAutoHunt {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Connection.SendAsync(LegacyAutoHuntPacket.Startup.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                new LegacyAutoHuntPacket(1, LegacyAutoHuntPacket.StartupParameter).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        context.Sessions?.Set(
            context.Connection,
            session with { Character = WithCharacterAttribute(session.Character, RuntimeAutoHuntAttribute, "1") });
        await context.Log.WriteLineAsync(
                $"WorldServer started LegacyAutoHunt {FormatSession(context)}.")
            .ConfigureAwait(false);
    }

}
