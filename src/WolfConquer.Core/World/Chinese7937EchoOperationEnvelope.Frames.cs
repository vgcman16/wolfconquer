using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial class Chinese7937EchoOperationEnvelope
{
    /// <summary>Requires an actual complete2132 frame and exact matching header/payload, with explicit7937 selection.</summary>
    public static bool TryParseFrame(PacketFrame frame, int declaredClientVersion,
        out Chinese7937EchoOperationEnvelope? envelope, out Chinese7937EchoEnvelopeFailure failure)
    {
        envelope = null;
        if (declaredClientVersion != ClientVersion)
        { failure = Chinese7937EchoEnvelopeFailure.UnsupportedVersion; return false; }
        if (frame.Header.Opcode != Opcode || frame.Frame.Length is < PacketHeader.Size or > ushort.MaxValue ||
            frame.Payload.Length != frame.Frame.Length - PacketHeader.Size)
        { failure = Chinese7937EchoEnvelopeFailure.MalformedFrame; return false; }
        var captured = frame.Frame.ToArray();
        if (!PacketHeader.TryRead(captured, out var actual) || actual != frame.Header || actual.Length != captured.Length ||
            !captured.AsSpan(PacketHeader.Size).SequenceEqual(frame.Payload.ToArray()))
        { failure = Chinese7937EchoEnvelopeFailure.MalformedFrame; return false; }
        return TryParsePayload(captured.AsSpan(PacketHeader.Size), declaredClientVersion, out envelope, out failure);
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, payload.Length, ushort.MaxValue);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }
}
