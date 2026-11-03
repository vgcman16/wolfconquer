using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial class Chinese7937EchoResponse
{
    /// <summary>Projects supported raw arguments without stripping a parsed envelope's opaque records.</summary>
    /// <remarks>The source is immutable parsed data; it grants no runtime or gameplay authority.</remarks>
    public static bool TryProjectEnvelope(Chinese7937EchoOperationEnvelope? envelope,
        out Chinese7937EchoResponse? response, out Chinese7937EchoResponseFailure failure)
    {
        response = null;
        if (envelope is null)
        { failure = Chinese7937EchoResponseFailure.SourceRequired; return false; }
        return TryCreateResponse(envelope.ToPayloadBytes(), envelope.KnownProjection, envelope, out response, out failure);
    }

    /// <summary>Replays the exact owned payload, including extensions and original wire spellings.</summary>
    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, payload.Length, ushort.MaxValue);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static bool TryCreateResponse(byte[] captured, Chinese7935XuanbaoOperationPayload known,
        Chinese7937EchoOperationEnvelope? envelope, out Chinese7937EchoResponse? response,
        out Chinese7937EchoResponseFailure failure)
    {
        response = null;
        Chinese7937EchoArgument[]? arguments = known.Operation switch
        {
            8 => [new(7, known.Fields.Field7), new(5, known.Fields.Field5)],
            10 => [new(2, known.Fields.Field2), new(3, known.Fields.Field3), new(4, known.Fields.Field4)],
            _ => null,
        };
        if (arguments is null)
        { failure = Chinese7937EchoResponseFailure.UnsupportedOperation; return false; }
        response = new(captured, known, arguments, envelope);
        failure = Chinese7937EchoResponseFailure.None;
        return true;
    }
}
