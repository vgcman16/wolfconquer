namespace WolfConquer.Core.World;

internal readonly record struct Chinese7937ItemWireRange(int FieldNumber, int WireType, int Offset, int Length)
{
    internal bool IsKnown => FieldNumber <= 43;
}

/// <summary>Preserves the item scanner API over the shared finite structural wire delimiter.</summary>
internal static class Chinese7937ItemInfoWireScanner
{
    internal const int MaximumPayloadBytes = Chinese7937ItemInfoEnvelope.MaximumPayloadBytes;
    internal const int MaximumExtensions = Chinese7937ItemInfoEnvelope.MaximumExtensions;
    internal const int MaximumGroupDepth = Chinese7937ItemInfoEnvelope.MaximumGroupDepth;

    internal static bool TryScan(ReadOnlySpan<byte> payload, out Chinese7937ItemWireRange[] ranges,
        out Chinese7937ItemInfoEnvelopeFailure failure)
    {
        ranges = [];
        if (!Chinese7937ProtoWireScanner.TryScan(payload, Chinese7937ProtoScope.Item2350, out var scanned, out var reason))
        {
            failure = reason switch
            {
                Chinese7937ProtoFailure.InputBounds => Chinese7937ItemInfoEnvelopeFailure.InputBounds,
                Chinese7937ProtoFailure.ExtensionBounds => Chinese7937ItemInfoEnvelopeFailure.ExtensionBounds,
                _ => Chinese7937ItemInfoEnvelopeFailure.InvalidWire,
            };
            return false;
        }
        ranges = scanned.Select(range => new Chinese7937ItemWireRange(
            range.FieldNumber, range.WireType, range.Offset, range.Length)).ToArray();
        failure = Chinese7937ItemInfoEnvelopeFailure.None;
        return true;
    }
}
