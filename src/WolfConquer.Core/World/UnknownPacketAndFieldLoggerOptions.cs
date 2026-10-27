namespace WolfConquer.Core.World;

/// <summary>Opt-in diagnostic limits, not packet or gameplay acceptance rules.</summary>
public sealed record UnknownPacketAndFieldLoggerOptions
{
    public bool Enabled { get; init; }
    public string? OutputDirectory { get; init; }
    public int MaxRecords { get; init; } = 1000;
    public int MaxFileBytes { get; init; } = 1_048_576;
    public int MaxRecordsPerMinute { get; init; } = 60;
    public int MaxDistinctSignatures { get; init; } = 256;
    public int MaxPayloadBytes { get; init; } = 1024;
    public int MaxFields { get; init; } = 64;
    public int MaxRawPayloadBytes { get; init; } = 64;
    public IReadOnlyCollection<ushort> ProtobufOpcodes { get; init; } = Array.Empty<ushort>();
    public IReadOnlyCollection<ushort> RawPayloadOpcodes { get; init; } = Array.Empty<ushort>();

    internal bool TrySnapshot(out UnknownPacketAndFieldLoggerOptions? snapshot)
    {
        snapshot = null;
        if (MaxRecords is < 1 or > 100_000 || MaxFileBytes is < 256 or > 16_777_216 ||
            MaxRecordsPerMinute is < 1 or > 600 || MaxDistinctSignatures is < 1 or > 4096 ||
            MaxPayloadBytes is < 1 or > 16_384 || MaxFields is < 1 or > 256 ||
            MaxRawPayloadBytes is < 1 or > 256 || !TryCopyOpcodes(ProtobufOpcodes, out var protobuf) ||
            !TryCopyOpcodes(RawPayloadOpcodes, out var raw)) return false;
        snapshot = this with
        {
            ProtobufOpcodes = protobuf,
            RawPayloadOpcodes = raw,
        };
        return true;
    }

    private static bool TryCopyOpcodes(IReadOnlyCollection<ushort>? opcodes, out ushort[] copy)
    {
        copy = [];
        if (opcodes is null || opcodes.Count > 32) return false;
        var rows = new List<ushort>();
        foreach (var opcode in opcodes)
        {
            if (rows.Count == 32 || opcode != D13RandomDailyTaskPacket.Opcode) return false;
            rows.Add(opcode);
        }
        if (rows.Count != opcodes.Count) return false;
        copy = rows.ToArray();
        return true;
    }
}
