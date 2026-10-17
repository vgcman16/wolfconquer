using System.Net;
using System.Net.Sockets;
using System.Text.Json;

internal static partial class Program
{
    static int TcpRelayCapture(string[] args)
    {
        TcpRelayCaptureOptions options;
        try
        {
            options = ResolveTcpRelayCaptureOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        try
        {
            var capture = RunTcpRelayCaptureAsync(options).GetAwaiter().GetResult();
            var outputDirectory = Path.GetDirectoryName(options.OutputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            WriteJson(options.OutputPath, capture);

            Console.WriteLine("TCP relay capture:");
            Console.WriteLine($"Listen: {options.ListenAddress}:{options.ListenPort}");
            Console.WriteLine($"Target: {options.TargetAddress}:{options.TargetPort}");
            Console.WriteLine($"Output JSON: {options.OutputPath}");
            Console.WriteLine($"Timeouts: acceptSeconds={(int)options.AcceptTimeout.TotalSeconds} idleSeconds={(int)options.IdleTimeout.TotalSeconds}");
            Console.WriteLine(
                $"Totals: clientToServer={capture.TotalClientToServerBytes:N0} " +
                $"serverToClient={capture.TotalServerToClientBytes:N0} chunks={capture.Chunks.Count:N0} " +
                $"stopReason={capture.StopReason}");
            return 0;
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException)
        {
            Console.Error.WriteLine($"TCP relay capture failed: {ex.Message}");
            return 2;
        }
    }

    static int TcpRelayCaptureSummary(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: WolfConquer.DataTool tcp-relay-capture-summary <relayCaptureJsonPath>");
            return 1;
        }

        try
        {
            var summary = TcpRelayCaptureAnalyzer.Analyze(args[1], firstChunkCount: 8);
            Console.WriteLine("TCP relay capture summary:");
            Console.WriteLine($"Path: {summary.Path}");
            Console.WriteLine($"fileLength={summary.FileLength} sha256={summary.Sha256}");
            Console.WriteLine(
                $"listen={summary.ListenAddress}:{summary.ListenPort} target={summary.TargetAddress}:{summary.TargetPort}");
            Console.WriteLine(
                $"maxBytes={summary.MaxBytes} acceptSeconds={summary.AcceptSeconds} idleSeconds={summary.IdleSeconds}");
            Console.WriteLine(
                $"totalClientToServerBytes={summary.TotalClientToServerBytes} " +
                $"totalServerToClientBytes={summary.TotalServerToClientBytes} " +
                $"chunks={summary.ChunkCount} stopReason={summary.StopReason}");
            Console.WriteLine($"directionTotals={FormatTcpRelayCaptureDirectionTotals(summary.DirectionTotalsByChunks)}");
            Console.WriteLine($"firstChunks={FormatTcpRelayCaptureFirstChunks(summary.FirstChunkDescriptors)}");
            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or JsonException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"TCP relay capture summary failed: {ex.Message}");
            return 1;
        }
    }

    static string FormatTcpRelayCaptureDirectionTotals(IReadOnlyDictionary<string, long> totals)
    {
        return totals.Count == 0
            ? "<none>"
            : string.Join(", ", totals.Select(pair => $"{pair.Key}:{pair.Value}"));
    }

    static string FormatTcpRelayCaptureFirstChunks(IReadOnlyList<string> chunks)
    {
        return chunks.Count == 0
            ? "<none>"
            : string.Join(", ", chunks);
    }

    static TcpRelayCaptureOptions ResolveTcpRelayCaptureOptions(string[] args)
    {
        if (args.Length < 6)
        {
            throw new ArgumentException(
                "Usage: WolfConquer.DataTool tcp-relay-capture <listenAddress> <listenPort> <targetAddress> <targetPort> <outputPath> [--max-bytes N] [--idle-seconds N] [--accept-seconds N]");
        }

        if (!IPAddress.TryParse(args[1], out var listenAddress))
        {
            throw new FormatException($"Invalid listen address: {args[1]}");
        }

        if (!int.TryParse(args[2], out var listenPort) || listenPort is < 0 or > 65535)
        {
            throw new FormatException($"Invalid listen port: {args[2]}");
        }

        if (!int.TryParse(args[4], out var targetPort) || targetPort is < 1 or > 65535)
        {
            throw new FormatException($"Invalid target port: {args[4]}");
        }

        var maxBytes = 4096;
        var idleSeconds = 30;
        int? acceptSeconds = null;
        for (var index = 6; index < args.Length; index++)
        {
            var option = args[index];
            if (string.Equals(option, "--max-bytes", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length || !int.TryParse(args[index], out maxBytes) || maxBytes < 0)
                {
                    throw new FormatException("--max-bytes requires a non-negative integer.");
                }
            }
            else if (string.Equals(option, "--idle-seconds", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length || !int.TryParse(args[index], out idleSeconds) || idleSeconds <= 0)
                {
                    throw new FormatException("--idle-seconds requires a positive integer.");
                }
            }
            else if (string.Equals(option, "--accept-seconds", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length || !int.TryParse(args[index], out var parsedAcceptSeconds) || parsedAcceptSeconds <= 0)
                {
                    throw new FormatException("--accept-seconds requires a positive integer.");
                }

                acceptSeconds = parsedAcceptSeconds;
            }
            else
            {
                throw new ArgumentException($"Unknown tcp-relay-capture option: {option}");
            }
        }

        return new TcpRelayCaptureOptions(
            listenAddress,
            listenPort,
            args[3],
            targetPort,
            Path.GetFullPath(args[5]),
            maxBytes,
            TimeSpan.FromSeconds(acceptSeconds ?? idleSeconds),
            TimeSpan.FromSeconds(idleSeconds));
    }

    static async Task<TcpRelayCaptureFile> RunTcpRelayCaptureAsync(TcpRelayCaptureOptions options)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var chunks = new List<TcpRelayCaptureChunk>();
        var gate = new object();
        var sampledClientToServerBytes = 0L;
        var sampledServerToClientBytes = 0L;
        var totalClientToServerBytes = 0L;
        var totalServerToClientBytes = 0L;
        var stopReason = "completed";

        using var acceptTimeout = new CancellationTokenSource(options.AcceptTimeout);
        using var idleTimeout = new CancellationTokenSource();

        var listener = new TcpListener(options.ListenAddress, options.ListenPort);
        listener.Start(1);

        TcpClient? client = null;
        TcpClient? server = null;
        var acceptedClient = false;
        try
        {
            client = await listener.AcceptTcpClientAsync(acceptTimeout.Token);
            acceptedClient = true;
            acceptTimeout.CancelAfter(Timeout.InfiniteTimeSpan);
            idleTimeout.CancelAfter(options.IdleTimeout);
            server = new TcpClient();
            await server.ConnectAsync(options.TargetAddress, options.TargetPort, idleTimeout.Token);

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(idleTimeout.Token);
            var clientToServer = RelayTcpDirectionAsync(
                client.GetStream(),
                server.GetStream(),
                "clientToServer",
                linked.Token,
                () => idleTimeout.CancelAfter(options.IdleTimeout),
                bytes => totalClientToServerBytes += bytes,
                sample =>
                {
                    if (sampledClientToServerBytes >= options.MaxBytes)
                    {
                        return;
                    }

                    var sampleLength = (int)Math.Min(sample.Length, options.MaxBytes - sampledClientToServerBytes);
                    sampledClientToServerBytes += sampleLength;
                    chunks.Add(CreateTcpRelayCaptureChunk("clientToServer", sample, sampleLength));
                },
                gate);
            var serverToClient = RelayTcpDirectionAsync(
                server.GetStream(),
                client.GetStream(),
                "serverToClient",
                linked.Token,
                () => idleTimeout.CancelAfter(options.IdleTimeout),
                bytes => totalServerToClientBytes += bytes,
                sample =>
                {
                    if (sampledServerToClientBytes >= options.MaxBytes)
                    {
                        return;
                    }

                    var sampleLength = (int)Math.Min(sample.Length, options.MaxBytes - sampledServerToClientBytes);
                    sampledServerToClientBytes += sampleLength;
                    chunks.Add(CreateTcpRelayCaptureChunk("serverToClient", sample, sampleLength));
                },
                gate);

            var completed = await Task.WhenAny(clientToServer, serverToClient);
            if (completed.IsCanceled || completed.IsFaulted)
            {
                stopReason = idleTimeout.IsCancellationRequested ? "idle-timeout" : "error";
            }
            else
            {
                stopReason = await completed;
            }

            linked.Cancel();
            client.Close();
            server.Close();
            await IgnoreRelayShutdownAsync(clientToServer);
            await IgnoreRelayShutdownAsync(serverToClient);
        }
        catch (OperationCanceledException) when (acceptTimeout.IsCancellationRequested || idleTimeout.IsCancellationRequested)
        {
            stopReason = acceptTimeout.IsCancellationRequested && !acceptedClient
                ? "accept-timeout"
                : "idle-timeout";
        }
        finally
        {
            client?.Close();
            server?.Close();
            listener.Stop();
        }

        return new TcpRelayCaptureFile(
            startedAtUtc,
            DateTimeOffset.UtcNow,
            options.ListenAddress.ToString(),
            options.ListenPort,
            options.TargetAddress,
            options.TargetPort,
            options.MaxBytes,
            (int)options.AcceptTimeout.TotalSeconds,
            (int)options.IdleTimeout.TotalSeconds,
            totalClientToServerBytes,
            totalServerToClientBytes,
            stopReason,
            chunks);
    }

    static async Task<string> RelayTcpDirectionAsync(
        NetworkStream input,
        NetworkStream output,
        string direction,
        CancellationToken cancellationToken,
        Action resetIdleTimeout,
        Action<int> addTotalBytes,
        Action<byte[]> recordSample,
        object gate)
    {
        var buffer = new byte[8192];
        while (true)
        {
            var bytesRead = await input.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
            {
                return $"{direction}-closed";
            }

            await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            await output.FlushAsync(cancellationToken);

            var sample = buffer.AsSpan(0, bytesRead).ToArray();
            lock (gate)
            {
                addTotalBytes(bytesRead);
                recordSample(sample);
            }

            resetIdleTimeout();
        }
    }

    static TcpRelayCaptureChunk CreateTcpRelayCaptureChunk(string direction, byte[] bytes, int sampleLength)
    {
        var sampleBytes = bytes.AsSpan(0, sampleLength).ToArray();
        return new TcpRelayCaptureChunk(
            DateTimeOffset.UtcNow,
            direction,
            bytes.Length,
            sampleLength,
            Convert.ToHexString(sampleBytes).ToLowerInvariant(),
            Convert.ToBase64String(sampleBytes));
    }

    static async Task IgnoreRelayShutdownAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
        {
        }
    }
}

internal sealed record TcpRelayCaptureOptions(
    IPAddress ListenAddress,
    int ListenPort,
    string TargetAddress,
    int TargetPort,
    string OutputPath,
    int MaxBytes,
    TimeSpan AcceptTimeout,
    TimeSpan IdleTimeout);

internal sealed record TcpRelayCaptureFile(
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string ListenAddress,
    int ListenPort,
    string TargetAddress,
    int TargetPort,
    int MaxBytes,
    int AcceptSeconds,
    int IdleSeconds,
    long TotalClientToServerBytes,
    long TotalServerToClientBytes,
    string StopReason,
    IReadOnlyList<TcpRelayCaptureChunk> Chunks);

internal sealed record TcpRelayCaptureChunk(
    DateTimeOffset TimestampUtc,
    string Direction,
    int Length,
    int SampleLength,
    string FirstHex,
    string FirstBase64);
