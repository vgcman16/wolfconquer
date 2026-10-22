using System.Buffers;
using System.Globalization;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask LogParityGapAsync(
        WorldPacketHandlerContext context,
        LegacyParityGapDiagnostic diagnostic)
    {
        await context.Log.WriteLineAsync(diagnostic.FormatLogMessage())
            .ConfigureAwait(false);
    }

    private static string FormatSession(WorldPacketHandlerContext context)
    {
        var session = context.Session;
        if (session is null)
        {
            return "session=none";
        }

        return "session=" +
            $"accountId={session.AccountId} " +
            $"username={session.Username} " +
            $"characterUid={session.Character.Uid} " +
            $"characterName={session.Character.Name} " +
            $"map={session.Character.MapId} " +
            $"x={session.Character.X} " +
            $"y={session.Character.Y} " +
            $"hp={session.Character.MinHitPoints} " +
            $"money={session.Character.Money} " +
            $"cp={session.Character.ConquerPoints}";
    }

    private static string FormatFrameDiagnostic(PacketFrame frame)
    {
        if (frame.Header.Opcode == WorldPacketOpcodes.LegacySecondaryPassword)
            return "opcode=" + frame.Header.Opcode + " payloadLength=" +
                frame.Payload.Length.ToString(CultureInfo.InvariantCulture) + " payloadSampleHex=[redacted]";
        var sampleLength = (int)Math.Min(frame.Payload.Length, MalformedPayloadSampleBytes);
        var sample = sampleLength == 0
            ? string.Empty
            : Convert.ToHexString(frame.Payload.Slice(0, sampleLength).ToArray());
        var truncated = frame.Payload.Length > sampleLength
            ? "..."
            : string.Empty;

        return "opcode=" + frame.Header.Opcode +
            " payloadLength=" + frame.Payload.Length.ToString(CultureInfo.InvariantCulture) +
            " payloadSampleHex=" + sample + truncated;
    }
}
