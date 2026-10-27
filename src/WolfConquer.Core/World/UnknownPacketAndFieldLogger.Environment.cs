using System.Globalization;

namespace WolfConquer.Core.World;

public sealed partial class UnknownPacketAndFieldLogger
{
    public const string EnabledEnvironmentVariable = "WOLFCONQUER_WORLD_WIRE_DIAGNOSTICS";
    public const string DirectoryEnvironmentVariable = "WOLFCONQUER_WORLD_WIRE_DIAGNOSTICS_DIRECTORY";
    public const string ProtobufOpcodesEnvironmentVariable = "WOLFCONQUER_WORLD_WIRE_DIAGNOSTICS_PROTOBUF_OPCODES";
    public const string RawOpcodesEnvironmentVariable = "WOLFCONQUER_WORLD_WIRE_DIAGNOSTICS_RAW_OPCODES";

    public static bool TryCreateFromEnvironment(out UnknownPacketAndFieldLogger? logger, out string? reason)
    {
        logger = null;
        reason = null;
        try
        {
            var enabled = Environment.GetEnvironmentVariable(EnabledEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(enabled) || enabled == "0")
                return TryCreate(new(), out logger, out reason);
            if (enabled != "1" ||
                !TryReadOpcodeList(Environment.GetEnvironmentVariable(ProtobufOpcodesEnvironmentVariable), out var protobuf) ||
                !TryReadOpcodeList(Environment.GetEnvironmentVariable(RawOpcodesEnvironmentVariable), out var raw))
            {
                reason = "invalid_diagnostic_environment";
                return false;
            }
            return TryCreate(new()
            {
                Enabled = true,
                OutputDirectory = Environment.GetEnvironmentVariable(DirectoryEnvironmentVariable),
                ProtobufOpcodes = protobuf,
                RawPayloadOpcodes = raw,
            }, out logger, out reason);
        }
        catch (Exception) { reason = "diagnostic_environment_unavailable"; return false; }
    }

    private static bool TryReadOpcodeList(string? text, out ushort[] opcodes)
    {
        opcodes = [];
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (text.Length > 256) return false;
        var rows = text.Split(',');
        if (rows.Length > 32) return false;
        var values = new List<ushort>();
        foreach (var row in rows)
        {
            // No names, wildcards, signs, Unicode digits or ambient culture.
            if (row.Length == 0 || row.Any(character => character is < '0' or > '9') ||
                !ushort.TryParse(row, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ||
                value != D13RandomDailyTaskPacket.Opcode) return false;
            values.Add(value);
        }
        opcodes = values.ToArray();
        return true;
    }
}
