using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace WolfConquer.Tests;

public sealed class DataToolTcpRelayCaptureTests
{
    [Fact]
    public async Task TcpRelayCaptureForwardsBothDirectionsAndWritesBoundedSamples()
    {
        var targetListener = new TcpListener(IPAddress.Loopback, 0);
        targetListener.Start();
        var targetEndpoint = (IPEndPoint)targetListener.LocalEndpoint;
        var relayPort = GetAvailableLoopbackPort();
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-tcp-relay-" + Guid.NewGuid().ToString("N"));
        var outputPath = Path.Combine(tempRoot, "relay-capture.json");
        var targetTask = AcceptReadAndReplyAsync(
            targetListener,
            [0x01, 0x02, 0x03, 0x04, 0x05],
            [0xA0, 0xA1, 0xA2, 0xA3, 0xA4]);

        try
        {
            using var process = StartDataTool(
                "tcp-relay-capture",
                "127.0.0.1",
                relayPort.ToString(),
                "127.0.0.1",
                targetEndpoint.Port.ToString(),
                outputPath,
                "--max-bytes",
                "3",
                "--accept-seconds",
                "7",
                "--idle-seconds",
                "5");
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            using (var relayClient = await ConnectWithRetryAsync(IPAddress.Loopback, relayPort))
            {
                var stream = relayClient.GetStream();
                await stream.WriteAsync(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 });
                await stream.FlushAsync();

                var reply = new byte[5];
                await ReadExactlyAsync(stream, reply);
                Assert.Equal([0xA0, 0xA1, 0xA2, 0xA3, 0xA4], reply);
            }

            await targetTask.WaitAsync(TimeSpan.FromSeconds(5));
            await WaitForDataToolExitAsync(process, outputTask, errorTask);

            var standardOutput = await outputTask;
            var standardError = await errorTask;
            Assert.Equal(0, process.ExitCode);
            Assert.Contains("TCP relay capture:", standardOutput);
            Assert.Contains($"Listen: 127.0.0.1:{relayPort}", standardOutput);
            Assert.Contains($"Target: 127.0.0.1:{targetEndpoint.Port}", standardOutput);
            Assert.Contains("Timeouts: acceptSeconds=7 idleSeconds=5", standardOutput);
            Assert.Contains("Totals: clientToServer=5 serverToClient=5 chunks=2", standardOutput);
            Assert.Equal(string.Empty, standardError);

            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(outputPath));
            var root = document.RootElement;
            Assert.Equal(7, root.GetProperty("acceptSeconds").GetInt32());
            Assert.Equal(5, root.GetProperty("idleSeconds").GetInt32());
            Assert.Equal(5, root.GetProperty("totalClientToServerBytes").GetInt64());
            Assert.Equal(5, root.GetProperty("totalServerToClientBytes").GetInt64());

            var chunks = root.GetProperty("chunks");
            Assert.Equal(2, chunks.GetArrayLength());
            Assert.Equal("clientToServer", chunks[0].GetProperty("direction").GetString());
            Assert.Equal(5, chunks[0].GetProperty("length").GetInt32());
            Assert.Equal(3, chunks[0].GetProperty("sampleLength").GetInt32());
            Assert.Equal("010203", chunks[0].GetProperty("firstHex").GetString());
            Assert.Equal("AQID", chunks[0].GetProperty("firstBase64").GetString());
            Assert.Equal("serverToClient", chunks[1].GetProperty("direction").GetString());
            Assert.Equal(5, chunks[1].GetProperty("length").GetInt32());
            Assert.Equal(3, chunks[1].GetProperty("sampleLength").GetInt32());
            Assert.Equal("a0a1a2", chunks[1].GetProperty("firstHex").GetString());
            Assert.Equal("oKGi", chunks[1].GetProperty("firstBase64").GetString());
        }
        finally
        {
            targetListener.Stop();
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task TcpRelayCaptureDefaultsAcceptTimeoutToIdleTimeout()
    {
        var relayPort = GetAvailableLoopbackPort();
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-tcp-relay-default-timeout-" + Guid.NewGuid().ToString("N"));
        var outputPath = Path.Combine(tempRoot, "relay-capture.json");

        try
        {
            using var process = StartDataTool(
                "tcp-relay-capture",
                "127.0.0.1",
                relayPort.ToString(),
                "127.0.0.1",
                "1",
                outputPath,
                "--idle-seconds",
                "1");
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await WaitForDataToolExitAsync(process, outputTask, errorTask);

            Assert.Equal(0, process.ExitCode);
            var standardOutput = await outputTask;
            Assert.Contains("Timeouts: acceptSeconds=1 idleSeconds=1", standardOutput);
            Assert.Contains("stopReason=accept-timeout", standardOutput);
            Assert.Equal(string.Empty, await errorTask);

            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(outputPath));
            var root = document.RootElement;
            Assert.Equal(1, root.GetProperty("acceptSeconds").GetInt32());
            Assert.Equal(1, root.GetProperty("idleSeconds").GetInt32());
            Assert.Equal("accept-timeout", root.GetProperty("stopReason").GetString());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HelpMentionsTcpRelayCaptureCommand()
    {
        using var process = StartDataTool("help");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await WaitForDataToolExitAsync(process, outputTask, errorTask);

        Assert.Equal(0, process.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool tcp-relay-capture <listenAddress> <listenPort> <targetAddress> <targetPort> <outputPath> [--max-bytes N] [--idle-seconds N] [--accept-seconds N]",
            await outputTask);
        Assert.Equal(string.Empty, await errorTask);
    }

    private static async Task AcceptReadAndReplyAsync(TcpListener listener, byte[] expectedRequest, byte[] reply)
    {
        using var client = await listener.AcceptTcpClientAsync();
        var stream = client.GetStream();
        var request = new byte[expectedRequest.Length];
        await ReadExactlyAsync(stream, request);
        Assert.Equal(expectedRequest, request);
        await stream.WriteAsync(reply);
        await stream.FlushAsync();
    }

    private static async Task<TcpClient> ConnectWithRetryAsync(IPAddress address, int port)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        Exception? lastError = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var client = new TcpClient();
            try
            {
                await client.ConnectAsync(address, port);
                return client;
            }
            catch (SocketException ex)
            {
                client.Dispose();
                lastError = ex;
                await Task.Delay(50);
            }
        }

        throw new TimeoutException($"Could not connect to relay port {port}.", lastError);
    }

    private static async Task ReadExactlyAsync(NetworkStream stream, byte[] buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset));
            if (read == 0)
            {
                throw new EndOfStreamException("The TCP stream closed before the expected bytes arrived.");
            }

            offset += read;
        }
    }

    private static int GetAvailableLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static Process StartDataTool(params string[] arguments)
    {
        var repositoryRoot = FindRepositoryRoot();
        var process = TestProjectProcess.Create(repositoryRoot, "WolfConquer.DataTool");
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        Assert.True(process.Start(), "Could not start DataTool.");
        return process;
    }

    private static async Task WaitForDataToolExitAsync(
        Process process,
        Task<string> outputTask,
        Task<string> errorTask)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail($"DataTool did not exit. stdout: {await outputTask} stderr: {await errorTask}");
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate WolfConquer.sln from the test output directory.");
    }
}
