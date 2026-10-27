using System.Buffers;
using System.Text;
using System.Text.Json;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class UnknownPacketAndFieldLoggerTests
{
    internal static PacketFrame Frame(ushort opcode, byte[] payload, bool segmented = false)
    {
        var writer = new PacketWriter(opcode, payload.Length);
        payload.CopyTo(writer.Payload);
        var bytes = writer.ToArray();
        ReadOnlySequence<byte> input = new(bytes);
        if (segmented)
        {
            var first = new WireSegment(bytes.AsMemory(0, 1));
            var last = first;
            for (var index = 1; index < bytes.Length; index++) last = last.Append(bytes.AsMemory(index, 1));
            input = new(first, 0, last, 1);
        }
        Assert.True(PacketFrameDecoder.TryRead(input, out var frame, out _, out _));
        return frame;
    }

    private sealed class WireSegment : ReadOnlySequenceSegment<byte>
    {
        internal WireSegment(ReadOnlyMemory<byte> memory) => Memory = memory;
        internal WireSegment Append(ReadOnlyMemory<byte> memory)
        {
            var next = new WireSegment(memory) { RunningIndex = RunningIndex + Memory.Length };
            Next = next;
            return next;
        }
    }

    private static void Observe(UnknownPacketAndFieldLogger logger, ushort opcode, byte[] payload,
        bool unknown = true, ConquerProtocolProfile profile = ConquerProtocolProfile.VisibleDx8D13,
        bool segmented = false) => logger.RecordPacket(profile, Frame(opcode, payload, segmented), unknown,
            new ConquerTcpConnection(null, null), new WorldSessionStore());

    private static void AssertMetadataOnly(JsonElement record)
    {
        Assert.Equal(JsonValueKind.Null, record.GetProperty("rawPayloadHex").ValueKind);
        Assert.False(record.GetProperty("rawPayloadTruncated").GetBoolean());
        Assert.DoesNotContain(record.EnumerateObject(), property => property.Name is
            "accountId" or "uid" or "characterUid" or "username" or "characterName" or "endpoint" or "payload");
        AssertRecordKeys(record);
    }

    internal static void AssertRecordKeys(JsonElement record)
    {
        Assert.Equal(new[] { "fields", "frameStatus", "headerLength", "opcode", "payloadLength", "profile", "rawPayloadHex",
                "rawPayloadTruncated", "scanStatus", "schema", "timeUtc", "unknownOpcode", "version" },
            record.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
    }
}

internal sealed class WorldWireDiagnosticTestFiles : IDisposable
{
    private readonly List<UnknownPacketAndFieldLogger> loggers = [];
    private readonly HashSet<string> owned = new(StringComparer.OrdinalIgnoreCase);
    internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-world-wire-tests-" + Guid.NewGuid().ToString("N"));

    private WorldWireDiagnosticTestFiles()
    {
        Guard();
        if (Directory.Exists(Root) || File.Exists(Root)) throw new IOException("Owned diagnostics root already exists.");
        Directory.CreateDirectory(Root);
        owned.Add(Path.GetFullPath(Root));
    }

    internal static void Run(Action<WorldWireDiagnosticTestFiles> body) =>
        RunAsync(files => { body(files); return Task.CompletedTask; }).GetAwaiter().GetResult();

    internal static async Task RunAsync(Func<WorldWireDiagnosticTestFiles, Task> body)
    {
        var files = new WorldWireDiagnosticTestFiles();
        Exception? primary = null;
        try { await body(files); }
        catch (Exception error) { primary = error; throw; }
        finally
        {
            try { files.Dispose(); }
            catch (Exception cleanup)
            {
                if (primary is null) throw;
                if (!ReferenceEquals(primary, cleanup)) throw new AggregateException(primary, cleanup);
            }
        }
    }

    internal UnknownPacketAndFieldLogger Open(UnknownPacketAndFieldLoggerOptions? options = null)
    {
        Guard();
        var configured = (options ?? new()) with { Enabled = true, OutputDirectory = Root };
        Assert.True(UnknownPacketAndFieldLogger.TryCreate(configured, out var logger, out var reason), reason);
        Assert.NotNull(logger);
        Track(logger);
        return logger;
    }

    internal void Track(UnknownPacketAndFieldLogger logger)
    {
        Guard();
        loggers.Add(logger);
        Assert.True(logger.IsEnabled);
        Assert.NotNull(logger.OutputPath);
        var output = Path.GetFullPath(logger.OutputPath);
        var directory = Path.GetDirectoryName(output)!;
        Assert.Equal(Path.GetFullPath(Root), Path.GetDirectoryName(directory), ignoreCase: true);
        Assert.Equal("packets.ndjson", Path.GetFileName(output));
        owned.Add(directory);
        owned.Add(output);
    }

    internal JsonElement[] Read(UnknownPacketAndFieldLogger logger)
    {
        logger.Dispose();
        Guard();
        var bytes = File.ReadAllBytes(logger.OutputPath!);
        Assert.False(bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
        var text = new UTF8Encoding(false, true).GetString(bytes);
        Assert.DoesNotContain('\r', text);
        if (text.Length != 0) Assert.EndsWith("\n", text);
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line =>
        {
            using var document = JsonDocument.Parse(line);
            return document.RootElement.Clone();
        }).ToArray();
    }

    internal string SeedFile(string name, byte[] bytes)
    {
        Guard();
        if (Path.GetFileName(name) != name) throw new InvalidOperationException("Invalid owned filename.");
        var path = Path.Combine(Root, name);
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) stream.Write(bytes);
        owned.Add(Path.GetFullPath(path));
        return path;
    }

    private void Guard()
    {
        var path = Path.GetFullPath(Root);
        const string prefix = "wolf-world-wire-tests-";
        var name = Path.GetFileName(path);
        if (!string.Equals(Path.GetDirectoryName(path), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())),
                StringComparison.OrdinalIgnoreCase) || !name.StartsWith(prefix, StringComparison.Ordinal) ||
            !Guid.TryParseExact(name[prefix.Length..], "N", out _))
            throw new InvalidOperationException("Invalid diagnostics fixture root.");
        if (Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Diagnostics fixture retained a reparse root.");
    }

    public void Dispose()
    {
        var errors = new List<Exception>();
        foreach (var logger in loggers)
            try { logger.Dispose(); } catch (Exception error) { errors.Add(error); }
        try
        {
            Guard();
            var pending = new Stack<string>();
            pending.Push(Root);
            while (pending.Count != 0)
            {
                var path = pending.Pop();
                if (!owned.Contains(Path.GetFullPath(path)) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException($"Diagnostics fixture retained unexpected path '{path}'.");
                if (Directory.Exists(path)) foreach (var child in Directory.EnumerateFileSystemEntries(path)) pending.Push(child);
            }
            if (errors.Count == 0) Directory.Delete(Path.GetFullPath(Root), recursive: true);
        }
        catch (Exception error) { errors.Add(error); }
        if (errors.Count != 0) throw new AggregateException("Diagnostics fixture cleanup failed; owned files may be retained.", errors);
    }
}
