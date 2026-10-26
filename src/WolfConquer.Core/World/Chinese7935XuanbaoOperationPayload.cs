namespace WolfConquer.Core.World;

/// <summary>A closed known-schema codec for the inspected official Chinese 7935-chain engine.</summary>
/// <remarks>
/// Engine supplied by official patch 7931, SHA256 A7E1AAC88A999B1F4723E6A1F197B96924E24022FD681FB2C9CF79C1BB413422.
/// Native CMsgXuanBaoOptPB has required operation1, optional uint32 fields2..7/10,
/// length-delimited string bytes8 and ordered repeated CXuanBaoAccInfoPB messages9.
/// This does not register a handler, grant items, interpret slot counts or change the D13 profile.
/// </remarks>
public sealed partial class Chinese7935XuanbaoOperationPayload
{
    public const ushort Opcode = 2132;
    // Finite codec bounds, not evidence of native allocation limits or a gameplay slot cap.
    public const int MaximumPayloadBytes = ushort.MaxValue - 4;
    public const int MaximumAccInfoEntries = 1024;
    private readonly byte[]? _data8;
    private readonly Chinese7935XuanbaoAccInfo[] _accInfo;

    public Chinese7935XuanbaoOperationPayload(uint operation, Chinese7935XuanbaoFields fields = default,
        ReadOnlyMemory<byte>? data8 = null, ReadOnlySpan<Chinese7935XuanbaoAccInfo> accInfo = default)
    {
        if (data8 is { Length: > MaximumPayloadBytes })
            throw new ArgumentOutOfRangeException(nameof(data8));
        if (accInfo.Length > MaximumAccInfoEntries)
            throw new ArgumentOutOfRangeException(nameof(accInfo));
        Operation = operation;
        Fields = fields;
        _data8 = data8?.ToArray();
        _accInfo = accInfo.ToArray();
        if (EncodedLength() > MaximumPayloadBytes)
            throw new ArgumentException("The known Xuanbao payload exceeds its finite codec bound.");
    }

    public uint Operation { get; }
    public Chinese7935XuanbaoFields Fields { get; }
    public bool HasData8 => _data8 is not null;
    public byte[]? CopyData8() => _data8?.ToArray();
    public ReadOnlySpan<Chinese7935XuanbaoAccInfo> AccInfo => _accInfo;

    private int EncodedLength()
    {
        var length = 1 + Chinese7935XuanbaoWireSize.Varint(Operation);
        foreach (var scalar in new[] { Fields.Field2, Fields.Field3, Fields.Field4, Fields.Field5,
                     Fields.Field6, Fields.Field7, Fields.Field10 })
            length += Chinese7935XuanbaoWireSize.Optional(scalar);
        if (_data8 is not null)
            length += 1 + Chinese7935XuanbaoWireSize.Varint((uint)_data8.Length) + _data8.Length;
        foreach (var child in _accInfo)
        {
            var childLength = Chinese7935XuanbaoWireSize.Child(child);
            length += 1 + Chinese7935XuanbaoWireSize.Varint((uint)childLength) + childLength;
        }
        return length;
    }
}
