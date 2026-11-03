namespace WolfConquer.Core.World;

/// <summary>One raw UI argument, preserving wire presence separately from native zero-initialized storage.</summary>
/// <remarks>No business meaning, permission, state ownership or arithmetic is inferred.</remarks>
public readonly record struct Chinese7937EchoArgument(int FieldNumber, uint? WireValue)
{
    public bool IsPresent => WireValue.HasValue;
    public uint NativeStorageValue => WireValue.GetValueOrDefault();
}

/// <summary>Read-only operation-8/10 response data derived from the retained engine used by the 7937 reference.</summary>
/// <remarks>No matching 7937 wire capture, handler, UI owner or gameplay activation is established.</remarks>
public sealed partial class Chinese7937EchoResponse
{
    public const int ClientVersion = 7937;
    public const ushort Opcode = Chinese7935XuanbaoOperationPayload.Opcode;
    public const int MaximumPayloadBytes = Chinese7935XuanbaoOperationPayload.MaximumPayloadBytes;
    private readonly byte[] payload;
    private readonly Chinese7937EchoArgument[] arguments;

    private Chinese7937EchoResponse(byte[] captured, Chinese7935XuanbaoOperationPayload known,
        Chinese7937EchoArgument[] ownedArguments, Chinese7937EchoOperationEnvelope? envelope)
    {
        payload = captured;
        KnownPayload = known;
        arguments = ownedArguments;
        SourceEnvelope = envelope;
        InternalUiEventId = known.Operation == 8 ? 0x36U : 0x37U;
    }

    /// <summary>Known fields only; use response payload/frame replay to preserve opaque extensions and wire spellings.</summary>
    public Chinese7935XuanbaoOperationPayload KnownPayload { get; }

    /// <summary>The immutable extension-preserving source, or null when read by the existing strict APIs.</summary>
    public Chinese7937EchoOperationEnvelope? SourceEnvelope { get; }
    public uint Operation => KnownPayload.Operation;
    public uint InternalUiEventId { get; }
    public ReadOnlySpan<Chinese7937EchoArgument> Arguments => arguments;

    /// <summary>Returns the exact captured payload, including extensions, original order and duplicate wire spellings.</summary>
    public byte[] CopyPayloadBytes() => payload.ToArray();
}

public enum Chinese7937EchoResponseFailure
{
    None, UnsupportedVersion, InputBounds, MalformedFrame, KnownSchema, UnsupportedOperation, SourceRequired,
}
