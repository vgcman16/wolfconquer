using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial class Chinese7937EchoResponse
{
    /// <summary>Fully validates the existing known schema before selecting a supported raw argument mapping.</summary>
    public static bool TryParsePayload(ReadOnlySpan<byte> payload, int declaredClientVersion,
        out Chinese7937EchoResponse? response, out Chinese7937EchoResponseFailure failure)
    {
        response = null;
        if (declaredClientVersion != ClientVersion)
        { failure = Chinese7937EchoResponseFailure.UnsupportedVersion; return false; }
        if (payload.Length is <= 0 or > MaximumPayloadBytes)
        { failure = Chinese7937EchoResponseFailure.InputBounds; return false; }
        var captured = payload.ToArray();
        if (!Chinese7935XuanbaoOperationPayload.TryParseKnownPayload(captured, out var known) || known is null)
        { failure = Chinese7937EchoResponseFailure.KnownSchema; return false; }
        return TryCreateResponse(captured, known, null, out response, out failure);
    }

    /// <summary>Requires the complete 2132 frame and matching header/payload; never reconciles caller disagreements.</summary>
    public static bool TryParseFrame(PacketFrame frame, int declaredClientVersion,
        out Chinese7937EchoResponse? response, out Chinese7937EchoResponseFailure failure)
    {
        response = null;
        if (declaredClientVersion != ClientVersion)
        { failure = Chinese7937EchoResponseFailure.UnsupportedVersion; return false; }
        if (frame.Header.Opcode != Opcode || frame.Frame.Length is < PacketHeader.Size or > ushort.MaxValue ||
            frame.Payload.Length != frame.Frame.Length - PacketHeader.Size)
        { failure = Chinese7937EchoResponseFailure.MalformedFrame; return false; }
        var captured = frame.Frame.ToArray();
        if (!PacketHeader.TryRead(captured, out var actual) || actual != frame.Header || actual.Length != captured.Length ||
            !captured.AsSpan(PacketHeader.Size).SequenceEqual(frame.Payload.ToArray()))
        { failure = Chinese7937EchoResponseFailure.MalformedFrame; return false; }
        return TryParsePayload(captured.AsSpan(PacketHeader.Size), declaredClientVersion, out response, out failure);
    }
}
