using System.Buffers;
using System.Diagnostics;
using System.Text.Json;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

/// <summary>Optional observational diagnostics. It never sends a response or updates gameplay state.</summary>
public sealed partial class UnknownPacketAndFieldLogger : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly object gate = new();
    private readonly UnknownPacketAndFieldLoggerOptions options;
    private readonly HashSet<string> signatures = new(StringComparer.Ordinal);
    private FileStream? output;
    private long windowStart = Stopwatch.GetTimestamp();
    private int windowRecords;
    private int records;
    private int bytesWritten;

    private UnknownPacketAndFieldLogger(UnknownPacketAndFieldLoggerOptions options, FileStream? output = null)
    {
        this.options = options;
        this.output = output;
        OutputPath = output?.Name;
    }

    public string? OutputPath { get; }
    public bool IsEnabled { get { lock (gate) return output is not null; } }

    public static bool TryCreate(UnknownPacketAndFieldLoggerOptions options,
        out UnknownPacketAndFieldLogger? logger, out string? reason)
    {
        logger = null;
        reason = null;
        try
        {
            if (options is null) { reason = "invalid_options"; return false; }
            if (!options.Enabled) { logger = new(new()); return true; }
            if (!options.TrySnapshot(out var snapshot)) { reason = "invalid_limits_or_opcode_allowlist"; return false; }
            logger = new(snapshot!, OpenPrivateOutput(snapshot!.OutputDirectory));
            return true;
        }
        catch (Exception)
        {
            // Configuration/open failures are diagnostics-only; never log the
            // supplied path, environment text, packet data or recursive exception.
            reason = "private_output_unavailable";
            return false;
        }
    }

    public void RecordPacket(ConquerProtocolProfile profile, PacketFrame frame, bool unknownOpcode,
        ConquerTcpConnection connection, WorldSessionStore sessions)
    {
        if (connection is null || sessions is null || !Enum.IsDefined(profile) || IsSensitiveOpcode(frame.Header.Opcode)) return;
        lock (gate)
        {
            if (output is null || !WithinRateLimit()) return;
            try
            {
                var record = DescribePacket(profile, frame, unknownOpcode, connection, sessions);
                if (record is not null) AppendRecord(record);
            }
            catch (Exception) { DisableOutput(); }
        }
    }

    private DiagnosticRecord? DescribePacket(ConquerProtocolProfile profile, PacketFrame frame, bool unknownOpcode,
        ConquerTcpConnection connection, WorldSessionStore sessions)
    {
        var coherent = IsCoherent(frame);
        var hasContract = HasDailyTaskContract(profile, frame.Header.Opcode);
        var scanRequested = options.ProtobufOpcodes.Contains(frame.Header.Opcode);
        var scan = new ScanResult(scanRequested ? "opaque_profile_or_opcode" : "not_requested", [], false);
        string? schema = null;
        if (scanRequested && hasContract)
        {
            schema = DailyTaskSchema;
            if (!coherent) scan = new("malformed", [], true);
            else
            {
                var length = (int)Math.Min(frame.Payload.Length, options.MaxPayloadBytes);
                scan = ScanDailyTask(frame.Payload.Slice(0, length).ToArray(), frame.Payload.Length > length, options.MaxFields);
            }
        }
        var raw = CaptureRaw(profile, frame, coherent, connection, sessions);
        if (!unknownOpcode && !scan.Unexpected && raw.Hex is null) return null;
        return new(1, DateTimeOffset.UtcNow, profile.ToString(), frame.Header.Opcode, frame.Header.Length,
            frame.Payload.Length, unknownOpcode, coherent ? "coherent" : "inconsistent", schema,
            scan.Status, scan.Fields, raw.Hex, raw.Truncated);
    }

    private (string? Hex, bool Truncated) CaptureRaw(ConquerProtocolProfile profile, PacketFrame frame,
        bool coherent, ConquerTcpConnection connection, WorldSessionStore sessions)
    {
        // A separate explicit allowlist is necessary. Schema scanning never
        // enables raw data; unknown opcode/profile coincidence never enables it.
        if (!coherent || !HasDailyTaskContract(profile, frame.Header.Opcode) ||
            !options.RawPayloadOpcodes.Contains(frame.Header.Opcode) ||
            !sessions.TryGet(connection, out var session) || session is null ||
            session.AccountId <= 0 || session.Character is null || session.Character.Uid <= 0) return (null, false);
        var length = (int)Math.Min(frame.Payload.Length, options.MaxRawPayloadBytes);
        var hex = Convert.ToHexString(frame.Payload.Slice(0, length).ToArray());
        if (!sessions.TryGet(connection, out var current) || !ReferenceEquals(current, session)) return (null, false);
        return (hex, frame.Payload.Length > length);
    }

    public void Dispose()
    {
        lock (gate) DisableOutput();
    }

    private void DisableOutput()
    {
        var owned = output;
        output = null;
        try { owned?.Dispose(); }
        catch (Exception) { /* Diagnostics must not replace the gameplay outcome. */ }
    }
}
