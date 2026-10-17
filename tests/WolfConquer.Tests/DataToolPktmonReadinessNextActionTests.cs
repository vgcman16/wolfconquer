using System.Buffers.Binary;

namespace WolfConquer.Tests;

public sealed class DataToolPktmonReadinessNextActionTests
{
    [Fact]
    public async Task PktmonCaptureReadinessExplainsHttpOnlyOriginalFlowCaptureNeedsRetry()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-pktmon-next-action-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OriginalFlowTimedScriptPath(tempRoot))!);
            Directory.CreateDirectory(Path.GetDirectoryName(OriginalFlowPcapngPath(tempRoot))!);

            await File.WriteAllTextAsync(OriginalFlowTimedScriptPath(tempRoot), "# timed original-flow capture");
            await File.WriteAllBytesAsync(OriginalFlowPcapngPath(tempRoot), HttpOnlyOriginalFlowPcapngBytes());

            var result = await RunDataToolAsync("pktmon-capture-readiness", tempRoot);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains(
                $"original-flow port=80 scriptExists=False script={OriginalFlowScriptPath(tempRoot)} timedScriptExists=True timedScript={OriginalFlowTimedScriptPath(tempRoot)} pcapngExists=True pcapng={OriginalFlowPcapngPath(tempRoot)}",
                result.StandardOutput);
            Assert.Contains("pcapngHeaderValid=True", result.StandardOutput);
            Assert.Contains("pcapngValidation=ok", result.StandardOutput);
            Assert.Contains("tcpServicePorts=80", result.StandardOutput);
            Assert.Contains("ReadyMode: missing", result.StandardOutput);
            Assert.Contains("Ready: False", result.StandardOutput);
            AssertHttpOnlyOriginalFlowRetryGuidance(GetNextAction(result.StandardOutput));
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static string OriginalFlowScriptPath(string rootPath)
    {
        return Path.Combine(rootPath, ".setup_work", "capture-scripts", "original-flow-pktmon-capture.ps1");
    }

    private static string OriginalFlowTimedScriptPath(string rootPath)
    {
        return Path.Combine(rootPath, ".setup_work", "capture-scripts", "original-flow-pktmon-capture-timed.ps1");
    }

    private static string OriginalFlowPcapngPath(string rootPath)
    {
        return Path.Combine(rootPath, ".setup_work", "captures", "original-flow-pktmon", "co_original_flow_pktmon.pcapng");
    }

    private static byte[] HttpOnlyOriginalFlowPcapngBytes()
    {
        var bytes = new List<byte>();
        AddLittleEndianSectionHeaderBlock(bytes);
        AddLittleEndianBlock(
            bytes,
            0x00000001,
            [
                0x01, 0x00,
                0x00, 0x00,
                0xFF, 0xFF, 0x00, 0x00,
            ]);
        AddLittleEndianBlock(
            bytes,
            0x00000006,
            EnhancedPacketBody(EthernetIpv4TcpPacketBytes(40000, 80, [0x47, 0x45, 0x54])));

        return bytes.ToArray();
    }

    private static byte[] EnhancedPacketBody(byte[] packetBytes)
    {
        var body = new List<byte>();
        AddUInt32LittleEndian(body, 0);
        AddUInt32LittleEndian(body, 0);
        AddUInt32LittleEndian(body, 0);
        AddUInt32LittleEndian(body, (uint)packetBytes.Length);
        AddUInt32LittleEndian(body, (uint)packetBytes.Length);
        body.AddRange(packetBytes);
        return body.ToArray();
    }

    private static byte[] EthernetIpv4TcpPacketBytes(ushort sourcePort, ushort destinationPort, byte[] payload)
    {
        var packet = new List<byte>();
        packet.AddRange([0x02, 0x00, 0x00, 0x00, 0x00, 0x01]);
        packet.AddRange([0x02, 0x00, 0x00, 0x00, 0x00, 0x02]);
        packet.AddRange([0x08, 0x00]);
        packet.AddRange(RawIpv4TcpPacketBytes(sourcePort, destinationPort, payload));
        return packet.ToArray();
    }

    private static byte[] RawIpv4TcpPacketBytes(ushort sourcePort, ushort destinationPort, byte[] payload)
    {
        var totalLength = 20 + 20 + payload.Length;
        var packet = new List<byte>
        {
            0x45,
            0x00,
            (byte)(totalLength >> 8),
            (byte)totalLength,
            0x00,
            0x01,
            0x40,
            0x00,
            0x40,
            0x06,
            0x00,
            0x00,
            127,
            0,
            0,
            1,
            127,
            0,
            0,
            1,
        };

        AddUInt16BigEndian(packet, sourcePort);
        AddUInt16BigEndian(packet, destinationPort);
        AddUInt32BigEndian(packet, 1);
        AddUInt32BigEndian(packet, 1);
        packet.AddRange([0x50, 0x18, 0x20, 0x00, 0x00, 0x00, 0x00, 0x00]);
        packet.AddRange(payload);
        return packet.ToArray();
    }

    private static void AddLittleEndianSectionHeaderBlock(List<byte> bytes)
    {
        AddUInt32LittleEndian(bytes, 0x0A0D0D0A);
        AddUInt32LittleEndian(bytes, 28);
        AddUInt32LittleEndian(bytes, 0x1A2B3C4D);
        AddUInt16LittleEndian(bytes, 1);
        AddUInt16LittleEndian(bytes, 0);
        AddUInt32LittleEndian(bytes, 0xFFFFFFFF);
        AddUInt32LittleEndian(bytes, 0xFFFFFFFF);
        AddUInt32LittleEndian(bytes, 28);
    }

    private static void AddLittleEndianBlock(List<byte> bytes, uint blockType, byte[] body)
    {
        var paddedBodyLength = body.Length + ((4 - body.Length % 4) % 4);
        var blockLength = 12 + paddedBodyLength;

        AddUInt32LittleEndian(bytes, blockType);
        AddUInt32LittleEndian(bytes, (uint)blockLength);
        bytes.AddRange(body);
        for (var index = body.Length; index < paddedBodyLength; index++)
        {
            bytes.Add(0x00);
        }

        AddUInt32LittleEndian(bytes, (uint)blockLength);
    }

    private static void AddUInt16LittleEndian(List<byte> bytes, ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
        bytes.AddRange(buffer.ToArray());
    }

    private static void AddUInt32LittleEndian(List<byte> bytes, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        bytes.AddRange(buffer.ToArray());
    }

    private static void AddUInt16BigEndian(List<byte> bytes, ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        bytes.AddRange(buffer.ToArray());
    }

    private static void AddUInt32BigEndian(List<byte> bytes, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        bytes.AddRange(buffer.ToArray());
    }

    private static string GetNextAction(string output)
    {
        using var reader = new StringReader(output);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            const string prefix = "NextAction: ";
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                return line[prefix.Length..];
            }
        }

        Assert.Fail($"Expected a NextAction line in output:{Environment.NewLine}{output}");
        return string.Empty;
    }

    private static void AssertHttpOnlyOriginalFlowRetryGuidance(string nextAction)
    {
        var guidance = nextAction.ToLowerInvariant();
        var mentionsMissingServicePayloads =
            guidance.Contains("missing") &&
            guidance.Contains("account") &&
            guidance.Contains("world") &&
            guidance.Contains("payload") &&
            guidance.Contains("9960") &&
            guidance.Contains("5818");
        var instructsRerunDuringClientFlow =
            (guidance.Contains("rerun") ||
                guidance.Contains("run again") ||
                guidance.Contains("capture again") ||
                guidance.Contains("recapture")) &&
            guidance.Contains("login") &&
            (guidance.Contains("world-entry") || guidance.Contains("world entry"));

        Assert.True(
            mentionsMissingServicePayloads || instructsRerunDuringClientFlow,
            "NextAction should identify missing account/world service payloads for 9960 and 5818 " +
            "or explicitly instruct a rerun during client login/world-entry. " +
            $"Actual NextAction: {nextAction}");
    }

    private static async Task<ConsoleRunResult> RunDataToolAsync(params string[] arguments)
    {
        var repositoryRoot = FindRepositoryRoot();
        using var process = TestProjectProcess.Create(repositoryRoot, "WolfConquer.DataTool");
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        Assert.True(process.Start(), "Could not start DataTool.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

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

        return new ConsoleRunResult(process.ExitCode, await outputTask, await errorTask);
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

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);
}
