using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace WolfConquer.Core.World;

public sealed partial class UnknownPacketAndFieldLogger
{
    private bool WithinRateLimit()
    {
        if (records >= options.MaxRecords || signatures.Count >= options.MaxDistinctSignatures) return false;
        if (Stopwatch.GetElapsedTime(windowStart) >= TimeSpan.FromMinutes(1))
        {
            windowStart = Stopwatch.GetTimestamp();
            windowRecords = 0;
        }
        return windowRecords < options.MaxRecordsPerMinute;
    }

    private void AppendRecord(DiagnosticRecord record)
    {
        // Metadata-only records dedupe by structure, without scalar values or
        // payload bytes. Explicit authorized raw capture also distinguishes its
        // already-captured bounded prefix, without any additional raw reads.
        var shape = JsonSerializer.SerializeToUtf8Bytes(new
        {
            record.Profile, record.Opcode, record.HeaderLength, record.PayloadLength, record.UnknownOpcode,
            record.FrameStatus, record.Schema, record.ScanStatus, record.Fields,
            RawCaptured = record.RawPayloadHex is not null, record.RawPayloadTruncated,
            RawPayloadPrefix = record.RawPayloadHex,
        }, JsonOptions);
        // The fixed-size internal digest is never emitted. Only opted-in raw
        // records contribute captured bytes; default records remain structural.
        // The bounded set must not retain MaxFields-sized JSON strings.
        var signature = Convert.ToHexString(SHA256.HashData(shape));
        if (signatures.Contains(signature)) return;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(record, JsonOptions);
        if (bytes.Length + 1 > options.MaxFileBytes - bytesWritten) return;
        // FileMode.CreateNew and FileShare.Read retain ownership of this one
        // output. No rotation, rename, existing-file truncation or recursive IO.
        output!.Write(bytes);
        output.WriteByte((byte)'\n');
        output.Flush();
        bytesWritten += bytes.Length + 1;
        records++;
        windowRecords++;
        signatures.Add(signature);
    }
}
