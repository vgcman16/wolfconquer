using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial class Chinese7937ItemInfoEnvelope
{
    /// <summary>Requires the actual complete2350 frame and matching header/payload; does not reconcile forged components.</summary>
    public static bool TryParseFrame(PacketFrame frame, int declaredClientVersion,
        out Chinese7937ItemInfoEnvelope? envelope, out Chinese7937ItemInfoEnvelopeFailure failure)
    {
        envelope = null;
        if (declaredClientVersion != ClientVersion)
        { failure = Chinese7937ItemInfoEnvelopeFailure.UnsupportedVersion; return false; }
        if (frame.Header.Opcode != Opcode || frame.Frame.Length is < PacketHeader.Size or > ushort.MaxValue ||
            frame.Payload.Length != frame.Frame.Length - PacketHeader.Size)
        { failure = Chinese7937ItemInfoEnvelopeFailure.MalformedFrame; return false; }
        var bytes = frame.Frame.ToArray();
        if (!PacketHeader.TryRead(bytes, out var actual) || actual != frame.Header || actual.Length != bytes.Length ||
            !bytes.AsSpan(PacketHeader.Size).SequenceEqual(frame.Payload.ToArray()))
        { failure = Chinese7937ItemInfoEnvelopeFailure.MalformedFrame; return false; }
        return TryParsePayload(bytes.AsSpan(PacketHeader.Size), declaredClientVersion, out envelope, out failure);
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, payload.Length, ushort.MaxValue);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }
}
