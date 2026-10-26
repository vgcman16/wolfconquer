namespace WolfConquer.Core.World;

/// <summary>Owned read-only view of the inspected Chinese 7935-chain CMsgItemInfoPB known schema.</summary>
/// <remarks>
/// Official 7931 DX8 engine used by the 7935 chain, SHA256 A7E1AAC88A999B1F4723E6A1F197B96924E24022FD681FB2C9CF79C1BB413422.
/// No handler, inventory publication, server authority or D13/1008 compatibility is implied.
/// </remarks>
public sealed partial class Chinese7935ItemInfoView
{
    public const ushort Opcode = 2350;
    public const int MaximumPayloadBytes = ushort.MaxValue - 4;
    public const int MaximumRepeated31Values = 1024;
    private readonly ulong?[] _scalars;
    private readonly byte[]? _data27;
    private readonly byte[]? _data28;
    private readonly int[] _values31;

    private Chinese7935ItemInfoView(ulong?[] scalars, byte[]? data27, byte[]? data28, int[] values31)
    {
        _scalars = scalars;
        _data27 = data27;
        _data28 = data28;
        _values31 = values31;
    }

    public uint ItemUid => (uint)_scalars[1]!.Value;
    public uint Field2 => (uint)_scalars[2]!.Value;
    public uint Field3 => (uint)_scalars[3]!.Value;
    public ReadOnlySpan<int> Values31 => _values31;
    public byte[]? CopyData27() => _data27?.ToArray();
    public byte[]? CopyData28() => _data28?.ToArray();

    public bool HasField(int field) => FieldKind(field) switch
    {
        Chinese7935ItemInfoFieldKind.UInt32 or Chinese7935ItemInfoFieldKind.Int32 or Chinese7935ItemInfoFieldKind.UInt64 => _scalars[field].HasValue,
        Chinese7935ItemInfoFieldKind.Bytes => field == 27 ? _data27 is not null : _data28 is not null,
        Chinese7935ItemInfoFieldKind.RepeatedInt32 => _values31.Length != 0,
        _ => false,
    };

    public bool TryGetUInt32(int field, out uint value)
    {
        value = 0;
        if (FieldKind(field) != Chinese7935ItemInfoFieldKind.UInt32 || _scalars[field] is not { } raw) return false;
        value = (uint)raw;
        return true;
    }

    public bool TryGetInt32(int field, out int value)
    {
        value = 0;
        if (FieldKind(field) != Chinese7935ItemInfoFieldKind.Int32 || _scalars[field] is not { } raw) return false;
        value = unchecked((int)(uint)raw);
        return true;
    }

    public bool TryGetUInt64(int field, out ulong value)
    {
        value = 0;
        if (FieldKind(field) != Chinese7935ItemInfoFieldKind.UInt64 || _scalars[field] is not { } raw) return false;
        value = raw;
        return true;
    }

    /// <summary>Reads an explicitly present item/placement tuple; does not choose a Process branch or admit an action.</summary>
    public bool TryGetEchoRelatedFields(out Chinese7935EchoItemFields fields)
    {
        fields = default;
        if (!TryGetUInt32(7, out var category) || !TryGetInt32(40, out var position)) return false;
        fields = new(ItemUid, category, position);
        return true;
    }
}

public readonly record struct Chinese7935EchoItemFields(uint ItemUid, uint CategoryField7, int PositionField40)
{
    // The native materializer stores the low byte; this is wire/display projection, not server admission.
    public byte ClientCategoryByte => unchecked((byte)CategoryField7);
}
