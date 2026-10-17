namespace WolfConquer.Tests;

public sealed class DataToolActiveBridgeCaptureStatusTests
{
    private const string ClientExePath = @"client\Env_DX8\Conquer.exe";
    private const string OutputPath = @".artifacts/active-bridge.json";

    [Fact]
    public void ReportsReadyWhenBridgeAccountAndWorldListenersArePresent()
    {
        var report = ActiveBridgeCaptureStatusLogic.CreateReport(
            OutputPath,
            ClientExePath,
            [],
            [
                Listener(100, 9960),
                Listener(101, 15819),
                Listener(102, 5818)
            ],
            ActiveBridgeCaptureArtifactStatus.Missing(OutputPath));

        var formatted = ActiveBridgeCaptureStatusLogic.FormatReport(report);

        Assert.True(report.ReadyForManualLogin);
        Assert.Contains("Service listeners: 9960(account) present", formatted);
        Assert.Contains("Bridge listener: 15819(relay) present", formatted);
        Assert.Contains("World listener: 5818(world) present", formatted);
        Assert.Contains("Client running: False", formatted);
        Assert.Contains("Artifact: missing", formatted);
        Assert.Contains("ReadyForManualLogin: True", formatted);
        Assert.Contains("Submit one client login", formatted);
    }

    [Fact]
    public void ReportsNotReadyWhenClientAlreadyHasWorldConnection()
    {
        var report = ActiveBridgeCaptureStatusLogic.CreateReport(
            OutputPath,
            ClientExePath,
            [new WorldRelayCapturePreflightProcess(42, ClientExePath)],
            [
                Listener(100, 9960),
                Listener(101, 15819),
                Listener(102, 5818),
                new WorldRelayCaptureTcpConnection(
                    42,
                    "192.168.84.1",
                    50000,
                    "127.0.0.1",
                    5818,
                    "Established")
            ],
            ActiveBridgeCaptureArtifactStatus.Found(
                OutputPath,
                1234,
                "abc123",
                "completed",
                2,
                "clientToServer-closed"));

        var formatted = ActiveBridgeCaptureStatusLogic.FormatReport(report);

        Assert.False(report.ReadyForManualLogin);
        Assert.Contains("Client running: True", formatted);
        Assert.Contains("WARNING: active client world/bridge TCP connection(s) already open: 1", formatted);
        Assert.Contains("Artifact: exists", formatted);
        Assert.Contains("handshakeStatus=completed frames=2 stopReason=clientToServer-closed", formatted);
        Assert.Contains("ReadyForManualLogin: False", formatted);
        Assert.Contains("Log out, close, or restart the client", formatted);
    }

    [Fact]
    public void ReportsMissingBridgeBeforeManualLoginCanStart()
    {
        var report = ActiveBridgeCaptureStatusLogic.CreateReport(
            OutputPath,
            ClientExePath,
            [],
            [
                Listener(100, 9960),
                Listener(102, 5818)
            ],
            ActiveBridgeCaptureArtifactStatus.Missing(OutputPath));

        var formatted = ActiveBridgeCaptureStatusLogic.FormatReport(report);

        Assert.False(report.ReadyForManualLogin);
        Assert.Contains("Bridge listener: 15819(relay) missing", formatted);
        Assert.Contains("Start legacy-world-active-bridge-capture with --progress", formatted);
    }

    private static WorldRelayCaptureTcpConnection Listener(int pid, int port) =>
        new(pid, "0.0.0.0", port, "0.0.0.0", 0, "Listen");
}
