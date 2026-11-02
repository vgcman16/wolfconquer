using System.Collections.ObjectModel;

namespace WolfConquer.Core.World;

/// <summary>Explicit7937 item2350 known fields plus owned, uninterpreted wire extensions.</summary>
/// <remarks>No profile registration, item-type interpretation of field2, inventory owner or gameplay permission.</remarks>
public sealed partial class Chinese7937ItemInfoEnvelope
{
    public const int ClientVersion = 7937;
    public const ushort Opcode = Chinese7935ItemInfoView.Opcode;
    public const int MaximumPayloadBytes = Chinese7935ItemInfoView.MaximumPayloadBytes;
    public const int MaximumExtensions = 1024;
    public const int MaximumGroupDepth = 16;
    private readonly byte[] payload;
    private readonly ReadOnlyCollection<Chinese7937UnknownItemField> unknownFields;

    private Chinese7937ItemInfoEnvelope(byte[] ownedPayload, Chinese7935ItemInfoView knownFields,
        Chinese7937UnknownItemField[] ownedUnknownFields)
    {
        payload = ownedPayload;
        KnownFields = knownFields;
        unknownFields = Array.AsReadOnly(ownedUnknownFields);
    }

    public Chinese7935ItemInfoView KnownFields { get; }
    public IReadOnlyList<Chinese7937UnknownItemField> UnknownFields => unknownFields;

    /// <summary>Fresh exact original payload, including record order, duplicates and extension spellings.</summary>
    public byte[] ToPayloadBytes() => payload.ToArray();

    public static bool TryParsePayload(ReadOnlySpan<byte> payload, int declaredClientVersion,
        out Chinese7937ItemInfoEnvelope? envelope, out Chinese7937ItemInfoEnvelopeFailure failure)
    {
        envelope = null;
        if (declaredClientVersion != ClientVersion)
        { failure = Chinese7937ItemInfoEnvelopeFailure.UnsupportedVersion; return false; }
        if (payload.Length is <= 0 or > MaximumPayloadBytes)
        { failure = Chinese7937ItemInfoEnvelopeFailure.InputBounds; return false; }
        var captured = payload.ToArray();
        if (!Chinese7937ItemInfoWireScanner.TryScan(captured, out var ranges, out failure)) return false;

        var knownBytes = new byte[captured.Length];
        var knownLength = 0;
        foreach (var range in ranges)
        {
            if (!range.IsKnown) continue;
            captured.AsSpan(range.Offset, range.Length).CopyTo(knownBytes.AsSpan(knownLength));
            knownLength += range.Length;
        }
        if (!Chinese7935ItemInfoView.TryParseKnownPayload(knownBytes.AsSpan(0, knownLength), out var known))
        { failure = Chinese7937ItemInfoEnvelopeFailure.KnownSchema; return false; }
        var extensions = ranges.Where(range => !range.IsKnown).Select(range => new Chinese7937UnknownItemField(
            range.FieldNumber, range.WireType, captured.AsSpan(range.Offset, range.Length).ToArray())).ToArray();
        envelope = new(captured, known!, extensions);
        failure = Chinese7937ItemInfoEnvelopeFailure.None;
        return true;
    }
}

/// <summary>A complete opaque encoded extension record; no nested fields or values are assigned meanings.</summary>
public sealed class Chinese7937UnknownItemField
{
    private readonly byte[] encoded;

    internal Chinese7937UnknownItemField(int fieldNumber, int wireType, byte[] ownedEncoded)
    { FieldNumber = fieldNumber; WireType = wireType; encoded = ownedEncoded; }

    public int FieldNumber { get; }
    public int WireType { get; }
    public byte[] CopyEncodedBytes() => encoded.ToArray();
}

public enum Chinese7937ItemInfoEnvelopeFailure
{
    None, UnsupportedVersion, InputBounds, MalformedFrame, InvalidWire, KnownSchema, ExtensionBounds,
}
