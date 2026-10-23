using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // A real native receiver exists. This is an explicit target adoption gap, not source silence.
    [PacketHandler(WorldPacketOpcodes.LegacyAchievement)]
    private static async ValueTask HandleLegacyAchievementUnadoptedAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await context.Log.WriteLineAsync(
                LegacyParityGapDiagnostics.UnsupportedPacket(
                    frame.Header.Opcode,
                    packet: "ClientAchievement",
                    reason: "native ClientAchievement.AchievementHandler [Packet(2307)] exists; genuine achievement persistence/load and host owner adoption remain unintegrated")
                .FormatLogMessage())
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "PARITY-GAP native achievement host adoption " +
                $"{FormatSession(context)} {FormatFrameDiagnostic(frame)} " +
                "source=MrRayzo.Game.MsgServer.ClientAchievement.AchievementHandler " +
                "actions=CheakView,SendQueue; no native owner or response fabricated")
            .ConfigureAwait(false);
    }
}
