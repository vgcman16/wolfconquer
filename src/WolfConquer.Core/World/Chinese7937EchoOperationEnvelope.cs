using System.Collections.ObjectModel;

namespace WolfConquer.Core.World;

/// <summary>Explicit7937 Echo2132 data with owned, uninterpreted parent and child9 extension records.</summary>
/// <remarks>Derived from retained same-engine text; no matching7937 capture, runtime binding or action semantics.</remarks>
public sealed partial class Chinese7937EchoOperationEnvelope
{
    public const int ClientVersion = 7937;
    public const ushort Opcode = Chinese7935XuanbaoOperationPayload.Opcode;
    public const int MaximumPayloadBytes = Chinese7937ProtoWireScanner.MaximumPayloadBytes;
    public const int MaximumExtensions = Chinese7937ProtoWireScanner.MaximumExtensions;
    public const int MaximumGroupDepth = Chinese7937ProtoWireScanner.MaximumGroupDepth;
    private readonly byte[] payload;
    private readonly ReadOnlyCollection<Chinese7937UnknownEchoField> unknownFields;

    private Chinese7937EchoOperationEnvelope(byte[] captured, Chinese7935XuanbaoOperationPayload known,
        Chinese7937UnknownEchoField[] extensions)
    {
        payload = captured;
        KnownProjection = known;
        unknownFields = Array.AsReadOnly(extensions);
    }

    /// <summary>Known data only; serializing this projection does not retain extensions or original wire spellings.</summary>
    public Chinese7935XuanbaoOperationPayload KnownProjection { get; }

    /// <summary>Opaque records in wire encounter order, including the known child9 scope in which each occurs.</summary>
    public IReadOnlyList<Chinese7937UnknownEchoField> UnknownFields => unknownFields;

    /// <summary>Fresh exact original payload, retaining all record order, duplicates and encoded spellings.</summary>
    public byte[] ToPayloadBytes() => payload.ToArray();
}

/// <summary>Complete opaque encoded record, with null child index for parent scope and zero-based child9 index otherwise.</summary>
public sealed class Chinese7937UnknownEchoField
{
    private readonly byte[] encoded;

    internal Chinese7937UnknownEchoField(int fieldNumber, int wireType, int? childIndex, byte[] captured)
    { FieldNumber = fieldNumber; WireType = wireType; ChildIndex = childIndex; encoded = captured; }

    public int FieldNumber { get; }
    public int WireType { get; }
    public int? ChildIndex { get; }
    public byte[] CopyEncodedBytes() => encoded.ToArray();
}

public enum Chinese7937EchoEnvelopeFailure
{
    None, UnsupportedVersion, InputBounds, MalformedFrame, InvalidWire, KnownSchema, ExtensionBounds,
}
