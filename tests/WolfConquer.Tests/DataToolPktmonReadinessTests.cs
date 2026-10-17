using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;

namespace WolfConquer.Tests;

public sealed class DataToolPktmonReadinessTests
{
    [Fact]
    public async Task PktmonCaptureReadinessReportsMissingScriptsAndCaptures()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-pktmon-readiness-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempRoot);

            var result = await RunDataToolAsync("pktmon-capture-readiness", tempRoot);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("Pktmon capture readiness:", result.StandardOutput);
            Assert.Contains($"Root: {tempRoot}", result.StandardOutput);
            Assert.Contains(
                $"login port=9960 scriptExists=False script={LoginScriptPath(tempRoot)} timedScriptExists=False timedScript={TimedLoginScriptPath(tempRoot)} pcapngExists=False pcapng={LoginPcapngPath(tempRoot)}",
                result.StandardOutput);
            Assert.Contains(
                $"world port=5818 scriptExists=False script={WorldScriptPath(tempRoot)} timedScriptExists=False timedScript={TimedWorldScriptPath(tempRoot)} pcapngExists=False pcapng={WorldPcapngPath(tempRoot)}",
                result.StandardOutput);
            Assert.Contains(
                $"original-flow port=80 scriptExists=False script={OriginalFlowScriptPath(tempRoot)} timedScriptExists=False timedScript={OriginalFlowTimedScriptPath(tempRoot)} pcapngExists=False pcapng={OriginalFlowPcapngPath(tempRoot)}",
                result.StandardOutput);
            Assert.Contains("ReadyMode: missing", result.StandardOutput);
            Assert.Contains(
                "NextAction: Run the combined original-flow pktmon script from an elevated PowerShell, then perform one normal client loader/login/world-entry flow.",
                result.StandardOutput);
            Assert.Contains("Ready: False", result.StandardOutput);
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

    [Fact]
    public async Task PktmonCaptureReadinessReportsReadyWhenExpectedFilesExist()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-pktmon-readiness-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LoginScriptPath(tempRoot))!);
            Directory.CreateDirectory(Path.GetDirectoryName(LoginPcapngPath(tempRoot))!);
            Directory.CreateDirectory(Path.GetDirectoryName(WorldPcapngPath(tempRoot))!);

            await File.WriteAllTextAsync(LoginScriptPath(tempRoot), "# login capture");
            await File.WriteAllTextAsync(WorldScriptPath(tempRoot), "# world capture");
            await File.WriteAllTextAsync(TimedLoginScriptPath(tempRoot), "# timed login capture");
            await File.WriteAllTextAsync(TimedWorldScriptPath(tempRoot), "# timed world capture");
            var pcapngBytes = TcpPayloadPcapngBytes();
            await File.WriteAllBytesAsync(LoginPcapngPath(tempRoot), pcapngBytes);
            await File.WriteAllBytesAsync(WorldPcapngPath(tempRoot), pcapngBytes);

            var result = await RunDataToolAsync("pktmon-capture-readiness", tempRoot);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains(
                $"login port=9960 scriptExists=True script={LoginScriptPath(tempRoot)} timedScriptExists=True timedScript={TimedLoginScriptPath(tempRoot)} pcapngExists=True pcapng={LoginPcapngPath(tempRoot)}",
                result.StandardOutput);
            Assert.Contains(
                $"world port=5818 scriptExists=True script={WorldScriptPath(tempRoot)} timedScriptExists=True timedScript={TimedWorldScriptPath(tempRoot)} pcapngExists=True pcapng={WorldPcapngPath(tempRoot)}",
                result.StandardOutput);
            Assert.Contains(
                $"pcapng={LoginPcapngPath(tempRoot)} {ExpectedReadinessValidation(pcapngBytes)}",
                result.StandardOutput);
            Assert.Contains(
                $"pcapng={WorldPcapngPath(tempRoot)} {ExpectedReadinessValidation(pcapngBytes)}",
                result.StandardOutput);
            Assert.Contains("ReadyMode: login-world", result.StandardOutput);
            Assert.Contains("login port=9960", result.StandardOutput);
            Assert.Contains("tcpServicePorts=9960", result.StandardOutput);
            Assert.Contains("world port=5818", result.StandardOutput);
            Assert.Contains("tcpServicePorts=5818", result.StandardOutput);
            Assert.DoesNotContain("NextAction:", result.StandardOutput);
            Assert.Contains("Ready: True", result.StandardOutput);
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

    [Fact]
    public async Task PktmonCaptureReadinessReportsReadyForSingleOriginalFlowCapture()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-pktmon-readiness-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OriginalFlowTimedScriptPath(tempRoot))!);
            Directory.CreateDirectory(Path.GetDirectoryName(OriginalFlowPcapngPath(tempRoot))!);

            await File.WriteAllTextAsync(OriginalFlowTimedScriptPath(tempRoot), "# original flow capture");
            var pcapngBytes = OriginalFlowTcpPayloadPcapngBytes();
            await File.WriteAllBytesAsync(OriginalFlowPcapngPath(tempRoot), pcapngBytes);

            var result = await RunDataToolAsync("pktmon-capture-readiness", tempRoot);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains(
                $"original-flow port=80 scriptExists=False script={OriginalFlowScriptPath(tempRoot)} timedScriptExists=True timedScript={OriginalFlowTimedScriptPath(tempRoot)} pcapngExists=True pcapng={OriginalFlowPcapngPath(tempRoot)}",
                result.StandardOutput);
            Assert.Contains(
                $"pcapng={OriginalFlowPcapngPath(tempRoot)} {ExpectedReadinessValidation(pcapngBytes, enhancedPacketBlocks: 2, simplePacketBlocks: 1, totalBlocks: 5)}",
                result.StandardOutput);
            Assert.Contains("ReadyMode: original-flow", result.StandardOutput);
            Assert.Contains("tcpServicePorts=80,5818,9960", result.StandardOutput);
            Assert.DoesNotContain("NextAction:", result.StandardOutput);
            Assert.Contains("Ready: True", result.StandardOutput);
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

    [Fact]
    public async Task PktmonCaptureReadinessRejectsOriginalFlowCaptureWithoutAccountAndWorldPayload()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-pktmon-readiness-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OriginalFlowTimedScriptPath(tempRoot))!);
            Directory.CreateDirectory(Path.GetDirectoryName(OriginalFlowPcapngPath(tempRoot))!);

            await File.WriteAllTextAsync(OriginalFlowTimedScriptPath(tempRoot), "# original flow capture");
            var pcapngBytes = HttpOnlyPcapngBytes();
            await File.WriteAllBytesAsync(OriginalFlowPcapngPath(tempRoot), pcapngBytes);

            var result = await RunDataToolAsync("pktmon-capture-readiness", tempRoot);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains(
                $"pcapng={OriginalFlowPcapngPath(tempRoot)} {ExpectedReadinessValidation(pcapngBytes, enhancedPacketBlocks: 1, simplePacketBlocks: 0, totalBlocks: 3)}",
                result.StandardOutput);
            Assert.Contains("tcpServicePorts=80", result.StandardOutput);
            Assert.Contains("ReadyMode: missing", result.StandardOutput);
            Assert.Contains(
                "NextAction: Rerun the combined original-flow pktmon script while performing one normal client loader/login/world-entry flow; existing original-flow capture is missing TCP payloads for service ports 9960,5818 (observed service ports: 80).",
                result.StandardOutput);
            Assert.Contains("Ready: False", result.StandardOutput);
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

    [Fact]
    public async Task PktmonCaptureReadinessReportsMisplacedCaptureFiles()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-pktmon-readiness-" + Guid.NewGuid().ToString("N"));
        var misplacedRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-pktmon-misplaced-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(LoginPcapngPath(misplacedRoot))!);

            var pcapngBytes = PacketContainingPcapngBytes();
            await File.WriteAllBytesAsync(LoginPcapngPath(misplacedRoot), pcapngBytes);

            var result = await RunDataToolAsync(
                "pktmon-capture-readiness",
                tempRoot,
                "--misplaced-root",
                misplacedRoot);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains(
                $"misplaced login port=9960 root={misplacedRoot} pcapng={LoginPcapngPath(misplacedRoot)} pcapngLength={pcapngBytes.Length}",
                result.StandardOutput);
            Assert.Contains("pcapngReady=True pcapngValidation=ok", result.StandardOutput);
            Assert.Contains("MisplacedCaptureHint:", result.StandardOutput);
            Assert.Contains("ReadyMode: missing", result.StandardOutput);
            Assert.Contains("Ready: False", result.StandardOutput);
            Assert.Equal(string.Empty, result.StandardError);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }

            if (Directory.Exists(misplacedRoot))
            {
                Directory.Delete(misplacedRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PktmonCaptureReadinessRejectsHeaderOnlyPcapngFixtures()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-pktmon-readiness-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LoginScriptPath(tempRoot))!);
            Directory.CreateDirectory(Path.GetDirectoryName(LoginPcapngPath(tempRoot))!);
            Directory.CreateDirectory(Path.GetDirectoryName(WorldPcapngPath(tempRoot))!);

            await File.WriteAllTextAsync(LoginScriptPath(tempRoot), "# login capture");
            await File.WriteAllTextAsync(WorldScriptPath(tempRoot), "# world capture");
            var pcapngBytes = HeaderOnlyPcapngBytes();
            await File.WriteAllBytesAsync(LoginPcapngPath(tempRoot), pcapngBytes);
            await File.WriteAllBytesAsync(WorldPcapngPath(tempRoot), pcapngBytes);

            var result = await RunDataToolAsync("pktmon-capture-readiness", tempRoot);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains(
                $"pcapng={LoginPcapngPath(tempRoot)} {ExpectedReadinessValidation(pcapngBytes, "no-packet-blocks", 1, 0, 0, 0, 1)}",
                result.StandardOutput);
            Assert.Contains(
                $"pcapng={WorldPcapngPath(tempRoot)} {ExpectedReadinessValidation(pcapngBytes, "no-packet-blocks", 1, 0, 0, 0, 1)}",
                result.StandardOutput);
            Assert.Contains("ReadyMode: missing", result.StandardOutput);
            Assert.Contains("Ready: False", result.StandardOutput);
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

    [Fact]
    public async Task PktmonCaptureReadinessRejectsInvalidPcapngFixtures()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-pktmon-readiness-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LoginScriptPath(tempRoot))!);
            Directory.CreateDirectory(Path.GetDirectoryName(LoginPcapngPath(tempRoot))!);
            Directory.CreateDirectory(Path.GetDirectoryName(WorldPcapngPath(tempRoot))!);

            await File.WriteAllTextAsync(LoginScriptPath(tempRoot), "# login capture");
            await File.WriteAllTextAsync(WorldScriptPath(tempRoot), "# world capture");
            await File.WriteAllBytesAsync(LoginPcapngPath(tempRoot), []);
            await File.WriteAllBytesAsync(WorldPcapngPath(tempRoot), new byte[28]);

            var result = await RunDataToolAsync("pktmon-capture-readiness", tempRoot);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains(
                $"pcapng={LoginPcapngPath(tempRoot)} {ExpectedReadinessValidation([], "too-short", 0, 0, 0, 0, 0, headerValid: false)}",
                result.StandardOutput);
            Assert.Contains(
                $"pcapng={WorldPcapngPath(tempRoot)} {ExpectedReadinessValidation(new byte[28], "wrong-section-header-block-magic", 0, 0, 0, 0, 0, headerValid: false)}",
                result.StandardOutput);
            Assert.Contains("ReadyMode: missing", result.StandardOutput);
            Assert.Contains("Ready: False", result.StandardOutput);
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

    [Fact]
    public async Task PktmonFixtureSummaryReportsExpectedPcapngFixtures()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-pktmon-summary-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LoginPcapngPath(tempRoot))!);
            Directory.CreateDirectory(Path.GetDirectoryName(WorldPcapngPath(tempRoot))!);
            Directory.CreateDirectory(Path.GetDirectoryName(OriginalFlowPcapngPath(tempRoot))!);

            var pcapngBytes = PacketContainingPcapngBytes();
            await File.WriteAllBytesAsync(LoginPcapngPath(tempRoot), pcapngBytes);
            await File.WriteAllBytesAsync(WorldPcapngPath(tempRoot), pcapngBytes);
            await File.WriteAllBytesAsync(OriginalFlowPcapngPath(tempRoot), pcapngBytes);

            var result = await RunDataToolAsync("pktmon-fixture-summary", tempRoot);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Pktmon pcapng fixture summary:", result.StandardOutput);
            Assert.Contains($"Root: {tempRoot}", result.StandardOutput);
            Assert.Contains($"login port=9960 {ExpectedSummary(LoginPcapngPath(tempRoot), pcapngBytes)}", result.StandardOutput);
            Assert.Contains($"world port=5818 {ExpectedSummary(WorldPcapngPath(tempRoot), pcapngBytes)}", result.StandardOutput);
            Assert.Contains($"original-flow port=80 {ExpectedSummary(OriginalFlowPcapngPath(tempRoot), pcapngBytes)}", result.StandardOutput);
            Assert.Contains("Ready: True", result.StandardOutput);
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

    [Fact]
    public async Task PktmonFixtureSummaryReportsSuppliedPcapngPath()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-pktmon-summary-" + Guid.NewGuid().ToString("N"));
        var pcapngPath = Path.Combine(tempRoot, "custom.pcapng");

        try
        {
            Directory.CreateDirectory(tempRoot);

            var pcapngBytes = PacketContainingPcapngBytes();
            await File.WriteAllBytesAsync(pcapngPath, pcapngBytes);

            var result = await RunDataToolAsync("pktmon-fixture-summary", pcapngPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Pktmon pcapng fixture summary:", result.StandardOutput);
            Assert.Contains(ExpectedSummary(pcapngPath, pcapngBytes), result.StandardOutput);
            Assert.Contains("Ready: True", result.StandardOutput);
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

    [Fact]
    public async Task PktmonFixtureSummaryFlagsHeaderOnlySuppliedPcapngPath()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-pktmon-summary-" + Guid.NewGuid().ToString("N"));
        var pcapngPath = Path.Combine(tempRoot, "header-only.pcapng");

        try
        {
            Directory.CreateDirectory(tempRoot);

            var pcapngBytes = HeaderOnlyPcapngBytes();
            await File.WriteAllBytesAsync(pcapngPath, pcapngBytes);

            var result = await RunDataToolAsync("pktmon-fixture-summary", pcapngPath);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("Pktmon pcapng fixture summary:", result.StandardOutput);
            Assert.Contains(ExpectedSummary(pcapngPath, pcapngBytes, "no-packet-blocks", 1, 0, 0, 0, 1), result.StandardOutput);
            Assert.Contains("Ready: False", result.StandardOutput);
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

    [Fact]
    public async Task PktmonFixtureSummaryFlagsEmptySuppliedPcapngPath()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-pktmon-summary-" + Guid.NewGuid().ToString("N"));
        var pcapngPath = Path.Combine(tempRoot, "empty.pcapng");

        try
        {
            Directory.CreateDirectory(tempRoot);
            await File.WriteAllBytesAsync(pcapngPath, []);

            var result = await RunDataToolAsync("pktmon-fixture-summary", pcapngPath);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains(
                ExpectedSummary(pcapngPath, [], "too-short", 0, 0, 0, 0, 0, headerValid: false),
                result.StandardOutput);
            Assert.Contains("Ready: False", result.StandardOutput);
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

    [Fact]
    public async Task PktmonPayloadSummaryReportsTcpPayloadSamplesForSuppliedPcapngPath()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-pktmon-payload-" + Guid.NewGuid().ToString("N"));
        var pcapngPath = Path.Combine(tempRoot, "payload.pcapng");

        try
        {
            Directory.CreateDirectory(tempRoot);

            var pcapngBytes = TcpPayloadPcapngBytes();
            await File.WriteAllBytesAsync(pcapngPath, pcapngBytes);

            var result = await RunDataToolAsync("pktmon-payload-summary", pcapngPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Pktmon pcapng payload summary:", result.StandardOutput);
            Assert.Contains("Extractor: bounded Ethernet/IPv4/TCP and raw IPv4/TCP packet bytes", result.StandardOutput);
            Assert.Contains(
                $"pcapng={pcapngPath} exists=True length={pcapngBytes.Length} validation=ok enhancedPacketBlocks=1 simplePacketBlocks=1 totalBlocks=4 tcpPayloadCandidates=2 tcpPayloadLengths=2x1,5x1 tcpPorts=40001->5818x1,9960->40000x1",
                result.StandardOutput);
            Assert.Contains("enhanced#1:packetLength=56,sourcePort=9960,destinationPort=40000,payloadLength=2,firstHex=434f,firstBase64=Q08=", result.StandardOutput);
            Assert.Contains("simple#2:packetLength=45,sourcePort=40001,destinationPort=5818,payloadLength=5,firstHex=4c4f47494e,firstBase64=TE9HSU4=", result.StandardOutput);
            Assert.Contains("Ready: True", result.StandardOutput);
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

    [Fact]
    public async Task PktmonPayloadSummaryReportsExpectedRootCaptures()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-pktmon-payload-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LoginPcapngPath(tempRoot))!);
            Directory.CreateDirectory(Path.GetDirectoryName(WorldPcapngPath(tempRoot))!);
            Directory.CreateDirectory(Path.GetDirectoryName(OriginalFlowPcapngPath(tempRoot))!);

            var pcapngBytes = TcpPayloadPcapngBytes();
            await File.WriteAllBytesAsync(LoginPcapngPath(tempRoot), pcapngBytes);
            await File.WriteAllBytesAsync(WorldPcapngPath(tempRoot), pcapngBytes);
            await File.WriteAllBytesAsync(OriginalFlowPcapngPath(tempRoot), pcapngBytes);

            var result = await RunDataToolAsync("pktmon-payload-summary", tempRoot);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Pktmon pcapng payload summary:", result.StandardOutput);
            Assert.Contains($"Root: {tempRoot}", result.StandardOutput);
            Assert.Contains(
                $"login port=9960 pcapng={LoginPcapngPath(tempRoot)} exists=True length={pcapngBytes.Length} validation=ok enhancedPacketBlocks=1 simplePacketBlocks=1 totalBlocks=4 tcpPayloadCandidates=2 tcpPayloadLengths=2x1,5x1 tcpPorts=40001->5818x1,9960->40000x1",
                result.StandardOutput);
            Assert.Contains(
                $"world port=5818 pcapng={WorldPcapngPath(tempRoot)} exists=True length={pcapngBytes.Length} validation=ok enhancedPacketBlocks=1 simplePacketBlocks=1 totalBlocks=4 tcpPayloadCandidates=2 tcpPayloadLengths=2x1,5x1 tcpPorts=40001->5818x1,9960->40000x1",
                result.StandardOutput);
            Assert.Contains(
                $"original-flow port=80 pcapng={OriginalFlowPcapngPath(tempRoot)} exists=True length={pcapngBytes.Length} validation=ok enhancedPacketBlocks=1 simplePacketBlocks=1 totalBlocks=4 tcpPayloadCandidates=2 tcpPayloadLengths=2x1,5x1 tcpPorts=40001->5818x1,9960->40000x1",
                result.StandardOutput);
            Assert.Contains("Ready: True", result.StandardOutput);
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

    [Fact]
    public async Task PktmonCaptureRunbookPrintsElevatedLaunchCommands()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-pktmon-runbook-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempRoot);

            var result = await RunDataToolAsync("pktmon-capture-runbook", tempRoot);
            var projectPath = Path.Combine(tempRoot, "src", "WolfConquer.DataTool", "WolfConquer.DataTool.csproj");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Pktmon capture runbook:", result.StandardOutput);
            Assert.Contains($"Root: {tempRoot}", result.StandardOutput);
            Assert.Contains(
                $"login port=9960 scriptExists=False script={LoginScriptPath(tempRoot)} pcapng={LoginPcapngPath(tempRoot)}",
                result.StandardOutput);
            Assert.Contains(
                $"world port=5818 scriptExists=False script={WorldScriptPath(tempRoot)} pcapng={WorldPcapngPath(tempRoot)}",
                result.StandardOutput);
            Assert.Contains(
                $"original-flow port=80 scriptExists=False script={OriginalFlowScriptPath(tempRoot)} pcapng={OriginalFlowPcapngPath(tempRoot)}",
                result.StandardOutput);
            Assert.Contains(
                $"dotnet run --project '{projectPath}' -- write-pktmon-capture-script login '{LoginScriptPath(tempRoot)}'",
                result.StandardOutput);
            Assert.Contains(
                $"dotnet run --project '{projectPath}' -- write-pktmon-capture-script world '{WorldScriptPath(tempRoot)}'",
                result.StandardOutput);
            Assert.Contains(
                $"dotnet run --project '{projectPath}' -- write-pktmon-capture-script login '{TimedLoginScriptPath(tempRoot)}' --duration-seconds 150",
                result.StandardOutput);
            Assert.Contains(
                $"dotnet run --project '{projectPath}' -- write-pktmon-capture-script world '{TimedWorldScriptPath(tempRoot)}' --duration-seconds 180",
                result.StandardOutput);
            Assert.Contains(
                $"dotnet run --project '{projectPath}' -- write-pktmon-capture-script original-flow '{OriginalFlowTimedScriptPath(tempRoot)}' --duration-seconds 240",
                result.StandardOutput);
            Assert.DoesNotContain(
                $"write-pktmon-capture-script original-flow '{OriginalFlowTimedScriptPath(tempRoot)}' --duration-seconds 180",
                result.StandardOutput);
            Assert.Contains(
                $"login: Start-Process powershell.exe -Verb RunAs -WorkingDirectory '{tempRoot}' -ArgumentList '-NoExit -ExecutionPolicy Bypass -File \"{LoginScriptPath(tempRoot)}\"'",
                result.StandardOutput);
            Assert.Contains(
                $"world: Start-Process powershell.exe -Verb RunAs -WorkingDirectory '{tempRoot}' -ArgumentList '-NoExit -ExecutionPolicy Bypass -File \"{WorldScriptPath(tempRoot)}\"'",
                result.StandardOutput);
            Assert.Contains(
                $"login: Start-Process powershell.exe -Verb RunAs -WorkingDirectory '{tempRoot}' -ArgumentList '-NoExit -ExecutionPolicy Bypass -File \"{TimedLoginScriptPath(tempRoot)}\"'",
                result.StandardOutput);
            Assert.Contains(
                $"world: Start-Process powershell.exe -Verb RunAs -WorkingDirectory '{tempRoot}' -ArgumentList '-NoExit -ExecutionPolicy Bypass -File \"{TimedWorldScriptPath(tempRoot)}\"'",
                result.StandardOutput);
            Assert.Contains(
                $"original-flow: Start-Process powershell.exe -Verb RunAs -WorkingDirectory '{tempRoot}' -ArgumentList '-NoExit -ExecutionPolicy Bypass -File \"{OriginalFlowTimedScriptPath(tempRoot)}\"'",
                result.StandardOutput);
            Assert.Contains(
                $"Set-Location '{tempRoot}'",
                result.StandardOutput);
            Assert.Contains(
                $"powershell.exe -ExecutionPolicy Bypass -File '{OriginalFlowTimedScriptPath(tempRoot)}'",
                result.StandardOutput);
            Assert.Contains(
                $"dotnet run --project '{projectPath}' -- pktmon-capture-readiness '{tempRoot}'",
                result.StandardOutput);
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

    [Fact]
    public async Task WritePktmonCaptureScriptPreservesExistingCaptureTargets()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-pktmon-script-" + Guid.NewGuid().ToString("N"));

        try
        {
            var manualScriptPath = LoginScriptPath(tempRoot);
            var timedScriptPath = OriginalFlowTimedScriptPath(tempRoot);

            var manualResult = await RunDataToolAsync(
                "write-pktmon-capture-script",
                "login",
                manualScriptPath);
            var timedResult = await RunDataToolAsync(
                "write-pktmon-capture-script",
                "original-flow",
                timedScriptPath,
                "--duration-seconds",
                "240");

            Assert.Equal(0, manualResult.ExitCode);
            Assert.Equal(0, timedResult.ExitCode);

            AssertCaptureScriptPreservesExistingTargets(await File.ReadAllTextAsync(manualScriptPath));
            AssertCaptureScriptPreservesExistingTargets(await File.ReadAllTextAsync(timedScriptPath));
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
    public async Task WorldRelayCaptureRunbookPrintsStagedRelayAndRestoreCommands()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-world-relay-runbook-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempRoot);

            var result = await RunDataToolAsync("world-relay-capture-runbook", tempRoot);
            var dataToolProjectPath = Path.Combine(tempRoot, "src", "WolfConquer.DataTool", "WolfConquer.DataTool.csproj");
            var accountServerProjectPath = Path.Combine(tempRoot, "src", "WolfConquer.AccountServer", "WolfConquer.AccountServer.csproj");
            var outputPath = Path.Combine(
                tempRoot,
                ".setup_work",
                "runtime-smoke",
                "real-client-world-relay-capture",
                "world-entry-relay.json");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Real-client world relay capture runbook:", result.StandardOutput);
            Assert.Contains($"Root: {tempRoot}", result.StandardOutput);
            Assert.Contains("Relay listen: 127.0.0.1:15819", result.StandardOutput);
            Assert.Contains("Relay target: 127.0.0.1:5818", result.StandardOutput);
            Assert.Contains($"Output JSON: {outputPath}", result.StandardOutput);
            Assert.Contains("`$_.Name -eq 'dotnet.exe'", result.StandardOutput);
            Assert.Contains("Stop-Process -Id `$_.ProcessId -Force", result.StandardOutput);
            Assert.Contains(
                "Optional account DB configuration (not bundled): config/GuardShield.ini.",
                result.StandardOutput);
            Assert.Contains("$env:WOLFCONQUER_ACCOUNT_MYSQL =", result.StandardOutput);
            var preflightCommand =
                $"dotnet run --project '{dataToolProjectPath}' -- world-relay-capture-preflight";
            var relayCommand =
                $"dotnet run --project '{dataToolProjectPath}' -- tcp-relay-capture 127.0.0.1 15819 127.0.0.1 5818 '{outputPath}' --max-bytes 65536 --accept-seconds 180 --idle-seconds 45";

            Assert.Contains(preflightCommand, result.StandardOutput);
            Assert.Contains("do not continue until it reports Ready: True", result.StandardOutput);
            Assert.Contains("log out or restart the client", result.StandardOutput);
            Assert.True(
                result.StandardOutput.IndexOf(preflightCommand, StringComparison.Ordinal) <
                result.StandardOutput.IndexOf(relayCommand, StringComparison.Ordinal),
                "The runbook should print the world relay capture preflight before the relay capture command.");
            Assert.Contains(
                relayCommand,
                result.StandardOutput);
            Assert.Contains(
                $"dotnet run --project '{accountServerProjectPath}' -- --listen --address 0.0.0.0 --port 9960 --legacy-account-wire --world-endpoint 127.0.0.1:15819",
                result.StandardOutput);
            Assert.Contains(
                "legacy-account-smoke 127.0.0.1 9960 $smokeUsername $smokePassword $serverName --expect-ip 127.0.0.1 --expect-port 15819",
                result.StandardOutput);
            Assert.Contains(
                $"dotnet run --project '{accountServerProjectPath}' -- --listen --address 0.0.0.0 --port 9960 --legacy-account-wire --world-endpoint 127.0.0.1:5818",
                result.StandardOutput);
            Assert.Contains(
                "legacy-account-smoke 127.0.0.1 9960 $smokeUsername $smokePassword $serverName --expect-ip 127.0.0.1 --expect-port 5818",
                result.StandardOutput);
            Assert.Contains(
                "Log in once with the live client only while the relay and forwarded AccountServer are active",
                result.StandardOutput);
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

    [Fact]
    public async Task HelpMentionsPktmonCommands()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("WolfConquer.DataTool pktmon-capture-readiness [rootPath]", result.StandardOutput);
        Assert.Contains("WolfConquer.DataTool pktmon-capture-runbook [rootPath]", result.StandardOutput);
        Assert.Contains("WolfConquer.DataTool world-relay-capture-runbook [rootPath]", result.StandardOutput);
        Assert.Contains("WolfConquer.DataTool world-relay-capture-preflight [clientExePath]", result.StandardOutput);
        Assert.Contains("WolfConquer.DataTool pktmon-fixture-summary [rootPath|pcapngPath]", result.StandardOutput);
        Assert.Contains("WolfConquer.DataTool pktmon-payload-summary [rootPath|pcapngPath]", result.StandardOutput);
    }

    private static string LoginScriptPath(string rootPath)
    {
        return Path.Combine(rootPath, ".setup_work", "capture-scripts", "login-pktmon-capture.ps1");
    }

    private static string WorldScriptPath(string rootPath)
    {
        return Path.Combine(rootPath, ".setup_work", "capture-scripts", "world-pktmon-capture.ps1");
    }

    private static string TimedLoginScriptPath(string rootPath)
    {
        return Path.Combine(rootPath, ".setup_work", "capture-scripts", "login-pktmon-capture-timed.ps1");
    }

    private static string TimedWorldScriptPath(string rootPath)
    {
        return Path.Combine(rootPath, ".setup_work", "capture-scripts", "world-pktmon-capture-timed.ps1");
    }

    private static string OriginalFlowTimedScriptPath(string rootPath)
    {
        return Path.Combine(rootPath, ".setup_work", "capture-scripts", "original-flow-pktmon-capture-timed.ps1");
    }

    private static string OriginalFlowScriptPath(string rootPath)
    {
        return Path.Combine(rootPath, ".setup_work", "capture-scripts", "original-flow-pktmon-capture.ps1");
    }

    private static string LoginPcapngPath(string rootPath)
    {
        return Path.Combine(rootPath, ".setup_work", "captures", "login-pktmon", "co_login_9960_pktmon.pcapng");
    }

    private static string WorldPcapngPath(string rootPath)
    {
        return Path.Combine(rootPath, ".setup_work", "captures", "world-pktmon", "co_world_5818_pktmon.pcapng");
    }

    private static string OriginalFlowPcapngPath(string rootPath)
    {
        return Path.Combine(rootPath, ".setup_work", "captures", "original-flow-pktmon", "co_original_flow_pktmon.pcapng");
    }

    private static void AssertCaptureScriptPreservesExistingTargets(string script)
    {
        Assert.Contains("function Move-ExistingCaptureFile", script);
        Assert.Contains("$Backup = \"$Path.previous-$Stamp\"", script);
        Assert.Contains("Move-Item -LiteralPath $Path -Destination $Backup -Force", script);
        Assert.Contains("Move-ExistingCaptureFile $Etl", script);
        Assert.Contains("Move-ExistingCaptureFile $Pcap", script);
        Assert.DoesNotContain("Remove-Item -LiteralPath $Pcap", script);
    }

    private static byte[] HeaderOnlyPcapngBytes()
    {
        var bytes = new List<byte>();
        AddLittleEndianSectionHeaderBlock(bytes);
        return bytes.ToArray();
    }

    private static byte[] PacketContainingPcapngBytes()
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
            [
                0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,
                0x01, 0x00, 0x00, 0x00,
                0x04, 0x00, 0x00, 0x00,
                0x04, 0x00, 0x00, 0x00,
                0xDE, 0xAD, 0xBE, 0xEF,
            ]);
        AddLittleEndianBlock(
            bytes,
            0x00000003,
            [
                0x04, 0x00, 0x00, 0x00,
                0x01, 0x02, 0x03, 0x04,
            ]);
        return bytes.ToArray();
    }

    private static byte[] TcpPayloadPcapngBytes()
    {
        return ServicePayloadPcapngBytes(
            ((ushort)9960, (ushort)40000, [0x43, 0x4F]),
            ((ushort)40001, (ushort)5818, [0x4C, 0x4F, 0x47, 0x49, 0x4E]));
    }

    private static byte[] OriginalFlowTcpPayloadPcapngBytes()
    {
        return ServicePayloadPcapngBytes(
            ((ushort)40000, (ushort)80, [0x47, 0x45, 0x54]),
            ((ushort)9960, (ushort)40001, [0x43, 0x4F]),
            ((ushort)40002, (ushort)5818, [0x4C, 0x4F, 0x47, 0x49, 0x4E]));
    }

    private static byte[] HttpOnlyPcapngBytes()
    {
        return ServicePayloadPcapngBytes(
            ((ushort)40000, (ushort)80, [0x47, 0x45, 0x54]));
    }

    private static byte[] ServicePayloadPcapngBytes(
        params (ushort SourcePort, ushort DestinationPort, byte[] Payload)[] packets)
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

        for (var index = 0; index < packets.Length; index++)
        {
            var packet = packets[index];
            var packetBytes = index % 2 == 0
                ? EthernetIpv4TcpPacketBytes(packet.SourcePort, packet.DestinationPort, packet.Payload)
                : RawIpv4TcpPacketBytes(packet.SourcePort, packet.DestinationPort, packet.Payload);
            AddLittleEndianBlock(
                bytes,
                index % 2 == 0 ? 0x00000006u : 0x00000003u,
                index % 2 == 0
                    ? EnhancedPacketBody(packetBytes)
                    : SimplePacketBody(packetBytes));
        }

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

    private static byte[] SimplePacketBody(byte[] packetBytes)
    {
        var body = new List<byte>();
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

    private static string ExpectedReadinessValidation(
        byte[] bytes,
        string validation = "ok",
        int sectionHeaderBlocks = 1,
        int interfaceDescriptionBlocks = 1,
        int enhancedPacketBlocks = 1,
        int simplePacketBlocks = 1,
        int totalBlocks = 4,
        bool headerValid = true,
        string version = "1.0",
        string byteOrder = "little")
    {
        if (!headerValid)
        {
            version = "<unknown>";
            byteOrder = "<unknown>";
        }

        return $"pcapngLength={bytes.Length} pcapngSha256={Sha256Hex(bytes)} " +
            $"pcapngHeaderValid={headerValid} pcapngVersion={version} pcapngByteOrder={byteOrder} " +
            $"pcapngSectionHeaderBlocks={sectionHeaderBlocks} " +
            $"pcapngInterfaceDescriptionBlocks={interfaceDescriptionBlocks} " +
            $"pcapngEnhancedPacketBlocks={enhancedPacketBlocks} " +
            $"pcapngSimplePacketBlocks={simplePacketBlocks} " +
            $"pcapngTotalBlocks={totalBlocks} " +
            $"pcapngValidation={validation}";
    }

    private static string ExpectedSummary(
        string path,
        byte[] bytes,
        string validation = "ok",
        int sectionHeaderBlocks = 1,
        int interfaceDescriptionBlocks = 1,
        int enhancedPacketBlocks = 1,
        int simplePacketBlocks = 1,
        int totalBlocks = 4,
        bool headerValid = true,
        string version = "1.0",
        string byteOrder = "little")
    {
        if (!headerValid)
        {
            version = "<unknown>";
            byteOrder = "<unknown>";
        }

        return $"pcapng={path} exists=True length={bytes.Length} sha256={Sha256Hex(bytes)} " +
            $"headerValid={headerValid} version={version} byteOrder={byteOrder} " +
            $"sectionHeaderBlocks={sectionHeaderBlocks} " +
            $"interfaceDescriptionBlocks={interfaceDescriptionBlocks} " +
            $"enhancedPacketBlocks={enhancedPacketBlocks} " +
            $"simplePacketBlocks={simplePacketBlocks} " +
            $"totalBlocks={totalBlocks} " +
            $"validation={validation}";
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

    private static string Sha256Hex(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
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
