namespace WolfConquer.Core.World;

public sealed partial class Chinese7937EchoOperationEnvelope
{
    public static bool TryParsePayload(ReadOnlySpan<byte> payload, int declaredClientVersion,
        out Chinese7937EchoOperationEnvelope? envelope, out Chinese7937EchoEnvelopeFailure failure)
    {
        envelope = null;
        if (declaredClientVersion != ClientVersion)
        { failure = Chinese7937EchoEnvelopeFailure.UnsupportedVersion; return false; }
        if (payload.Length is <= 0 or > MaximumPayloadBytes)
        { failure = Chinese7937EchoEnvelopeFailure.InputBounds; return false; }
        var captured = payload.ToArray();
        List<Chinese7937UnknownEchoField> extensions = [];
        if (!TryProjectParent(captured, extensions, out var projected, out failure)) return false;
        if (!Chinese7935XuanbaoOperationPayload.TryParseKnownPayload(projected, out var known) || known is null)
        { failure = Chinese7937EchoEnvelopeFailure.KnownSchema; return false; }
        envelope = new(captured, known, extensions.ToArray());
        failure = Chinese7937EchoEnvelopeFailure.None;
        return true;
    }

    private static bool TryProjectParent(ReadOnlySpan<byte> bytes, List<Chinese7937UnknownEchoField> extensions,
        out byte[] projected, out Chinese7937EchoEnvelopeFailure failure)
    {
        projected = [];
        if (!TryScan(bytes, Chinese7937ProtoScope.Echo2132, out var ranges, out failure)) return false;
        List<byte> known = new(bytes.Length);
        var childIndex = 0;
        foreach (var range in ranges)
        {
            if (range.FieldNumber > 10)
            {
                if (!TryRetain(bytes, range, null, extensions, out failure)) return false;
            }
            else if (range.FieldNumber == 9)
            {
                if (range.WireType != 2 || childIndex == Chinese7935XuanbaoOperationPayload.MaximumAccInfoEntries)
                { failure = Chinese7937EchoEnvelopeFailure.KnownSchema; return false; }
                if (!TryProjectChild(bytes.Slice(range.DataOffset, range.DataLength), childIndex++, extensions,
                        out var child, out failure)) return false;
                LegacyProtoWire.WriteBytesField(known, 9, child);
            }
            else known.AddRange(bytes.Slice(range.Offset, range.Length).ToArray());
        }
        projected = known.ToArray();
        return true;
    }

    private static bool TryProjectChild(ReadOnlySpan<byte> bytes, int index,
        List<Chinese7937UnknownEchoField> extensions, out byte[] projected, out Chinese7937EchoEnvelopeFailure failure)
    {
        projected = [];
        if (!TryScan(bytes, Chinese7937ProtoScope.EchoChild2132, out var ranges, out failure)) return false;
        List<byte> known = new(bytes.Length);
        foreach (var range in ranges)
        {
            if (range.FieldNumber <= 2) known.AddRange(bytes.Slice(range.Offset, range.Length).ToArray());
            else if (!TryRetain(bytes, range, index, extensions, out failure)) return false;
        }
        projected = known.ToArray();
        return true;
    }

    private static bool TryRetain(ReadOnlySpan<byte> bytes, Chinese7937ProtoRange range, int? index,
        List<Chinese7937UnknownEchoField> extensions, out Chinese7937EchoEnvelopeFailure failure)
    {
        failure = Chinese7937EchoEnvelopeFailure.ExtensionBounds;
        if (extensions.Count == MaximumExtensions) return false;
        extensions.Add(new(range.FieldNumber, range.WireType, index, bytes.Slice(range.Offset, range.Length).ToArray()));
        failure = Chinese7937EchoEnvelopeFailure.None;
        return true;
    }

    private static bool TryScan(ReadOnlySpan<byte> bytes, Chinese7937ProtoScope scope,
        out Chinese7937ProtoRange[] ranges, out Chinese7937EchoEnvelopeFailure failure)
    {
        var success = Chinese7937ProtoWireScanner.TryScan(bytes, scope, out ranges, out var reason);
        failure = reason switch
        {
            Chinese7937ProtoFailure.None => Chinese7937EchoEnvelopeFailure.None,
            Chinese7937ProtoFailure.InputBounds => Chinese7937EchoEnvelopeFailure.InputBounds,
            Chinese7937ProtoFailure.ExtensionBounds => Chinese7937EchoEnvelopeFailure.ExtensionBounds,
            _ => Chinese7937EchoEnvelopeFailure.InvalidWire,
        };
        return success;
    }
}
