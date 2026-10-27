using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

/// <summary>The inspected VisibleDx8D13 CMsgRandomDailyTaskPB known schema.</summary>
/// <remarks>
/// Target PE SHA256 D13B45BD2681DB7D3D8548EA575D89E0D05613BA00E8F9763DBF3F89030402A5.
/// Field1 is required even when zero; optional scalar presence is retained.
/// Unsupported fields are refused, not discarded. Native unknown-field retention
/// is outside this closed codec. No handler, assignment owner or grant is created.
/// </remarks>
public sealed partial class D13RandomDailyTaskPacket
{
    public const ushort Opcode = 2419;
    public const int MaximumPayloadBytes = ushort.MaxValue - PacketHeader.Size;
    // Managed envelope bound: required action costs at least2 bytes, each unpacked8 at least2.
    // This is not a native allocation limit, objective count or gameplay entitlement.
    public const int MaximumRepeatedValues = (MaximumPayloadBytes - 2) / 2;
    private readonly uint[] _values8;
    private readonly int _encodedLength;

    public D13RandomDailyTaskPacket(uint action, D13RandomDailyTaskFields fields = default,
        ReadOnlySpan<uint> values8 = default)
    {
        if (values8.Length > MaximumRepeatedValues)
            throw new ArgumentOutOfRangeException(nameof(values8));
        if (GetEncodedLength(action, fields, values8) > MaximumPayloadBytes)
            throw new ArgumentException("The canonical RandomDailyTask payload exceeds the managed envelope.", nameof(values8));

        var ownedValues = values8.ToArray();
        var ownedLength = GetEncodedLength(action, fields, ownedValues);
        // Recheck the owned image in case a caller changed its borrowed memory during capture.
        if (ownedLength > MaximumPayloadBytes)
            throw new ArgumentException("The captured RandomDailyTask payload exceeds the managed envelope.", nameof(values8));
        Action = action;
        Fields = fields;
        _values8 = ownedValues;
        _encodedLength = ownedLength;
    }

    public uint Action { get; }
    public D13RandomDailyTaskFields Fields { get; }
    public ReadOnlySpan<uint> Values8 => _values8;
}

/// <summary>Numeric field names preserve the known wire schema without inventing business authority.</summary>
public readonly record struct D13RandomDailyTaskFields(
    uint? Field2 = null,
    uint? Field3 = null,
    uint? Field4 = null,
    uint? Field5 = null,
    int? Field6 = null,
    uint? Field7 = null);