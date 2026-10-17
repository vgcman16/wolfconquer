using System.Net;
using System.Net.Sockets;
using System.IO.Pipelines;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed class LegacyGuardShieldTextProtocolTests
{
    [Fact]
    public void WelcomeHashReturnsHashesInLegacyOrder()
    {
        var hashes = new LegacyGuardShieldHashSet(
            "magic-effect",
            "magic-type",
            "str-res",
            "conquer-exe");
        var result = LegacyGuardShieldTextProtocol.HandleCommand(
            "Welcome:Hash:HD123:HWID456",
            new LegacyGuardShieldTextProtocolOptions(hashes));

        Assert.Equal(LegacyGuardShieldTextCommandStatus.Handled, result.Status);
        Assert.Equal("magic-effect | magic-type | str-res | conquer-exe", result.Response);
    }

    [Fact]
    public void WelcomeHashReportsCommandDiagnostics()
    {
        LegacyGuardShieldCommandReport? report = null;
        var hashes = new LegacyGuardShieldHashSet(
            "magic-effect",
            "magic-type",
            "str-res",
            "conquer-exe");

        var result = LegacyGuardShieldTextProtocol.HandleCommand(
            "Welcome:Hash:HD123:HWID456",
            new LegacyGuardShieldTextProtocolOptions(
                hashes,
                ReportCommand: command => report = command));

        Assert.Equal(LegacyGuardShieldTextCommandStatus.Handled, result.Status);
        var captured = report ?? throw new InvalidOperationException("Expected command report.");
        Assert.Equal("Welcome", captured.Command);
        Assert.Equal(LegacyGuardShieldTextCommandStatus.Handled, captured.Status);
        Assert.Equal(4, captured.FieldCount);
        Assert.Equal(hashes.ToLegacyResponse().Length, captured.ResponseLength);
        Assert.Null(captured.UsageKind);
    }

    [Theory]
    [InlineData("Welcome:Ha", "Welcome", LegacyGuardShieldTextCommandStatus.Incomplete)]
    [InlineData("Unknown:Thing", "Unknown", LegacyGuardShieldTextCommandStatus.Ignored)]
    public void CommandDiagnosticsCaptureIncompleteAndIgnoredCommands(
        string command,
        string expectedName,
        LegacyGuardShieldTextCommandStatus expectedStatus)
    {
        LegacyGuardShieldCommandReport? report = null;

        var result = LegacyGuardShieldTextProtocol.HandleCommand(
            command,
            new LegacyGuardShieldTextProtocolOptions(
                LegacyGuardShieldHashSet.Empty,
                ReportCommand: captured => report = captured));

        Assert.Equal(expectedStatus, result.Status);
        var captured = report ?? throw new InvalidOperationException("Expected command report.");
        Assert.Equal(expectedName, captured.Command);
        Assert.Equal(expectedStatus, captured.Status);
        Assert.Equal(2, captured.FieldCount);
        Assert.Equal(0, captured.ResponseLength);
        Assert.Null(captured.UsageKind);
    }

    [Theory]
    [InlineData("Welcome:")]
    [InlineData("Welcome:Ha")]
    [InlineData("Used:")]
    [InlineData("Used:Spe")]
    public void PartialKnownCommandsStayIncomplete(string command)
    {
        var result = LegacyGuardShieldTextProtocol.HandleCommand(command);

        Assert.Equal(LegacyGuardShieldTextCommandStatus.Incomplete, result.Status);
        Assert.Null(result.Response);
    }

    [Theory]
    [InlineData("Clicker")]
    [InlineData("Program")]
    [InlineData("Speed")]
    public void UsedReportCapturesLegacyFieldsWithoutResponse(string kind)
    {
        LegacyGuardShieldUsageReport? report = null;
        var result = LegacyGuardShieldTextProtocol.HandleCommand(
            $"Used:{kind}:detected-window:user02:TestSrv",
            new LegacyGuardShieldTextProtocolOptions(
                LegacyGuardShieldHashSet.Empty,
                usage => report = usage));

        Assert.Equal(LegacyGuardShieldTextCommandStatus.Handled, result.Status);
        Assert.Null(result.Response);
        var captured = result.UsageReport ?? throw new InvalidOperationException("Expected usage report.");
        Assert.Same(captured, report);
        Assert.Equal(kind, captured.Kind);
        Assert.Equal("detected-window", captured.Detail);
        Assert.Equal("user02", captured.Username);
        Assert.Equal("TestSrv", captured.Server);
    }

    [Fact]
    public void UsedReportTrimsKindDetailUsernameAndServer()
    {
        var result = LegacyGuardShieldTextProtocol.HandleCommand(
            "Used: Speed : detected-window : user02 : TestSrv ");

        Assert.Equal(LegacyGuardShieldTextCommandStatus.Handled, result.Status);
        var captured = result.UsageReport ?? throw new InvalidOperationException("Expected usage report.");
        Assert.Equal("Speed", captured.Kind);
        Assert.Equal("detected-window", captured.Detail);
        Assert.Equal("user02", captured.Username);
        Assert.Equal("TestSrv", captured.Server);
    }

    [Theory]
    [InlineData("Clicker", 2, 2, "Use Auto CLicker")]
    [InlineData("Program", 4, 2, "Use Auto Program")]
    [InlineData("Speed", 6, 2, "Use Speed")]
    public void UsedReportCapturesLegacyBanDecisionFromConfiguredPolicy(
        string kind,
        int expectedConfiguredHours,
        int expectedEffectiveHours,
        string expectedReason)
    {
        LegacyGuardShieldUsageReport? report = null;
        var result = LegacyGuardShieldTextProtocol.HandleCommand(
            $"Used:{kind}:detected-window:user02:TestSrv",
            new LegacyGuardShieldTextProtocolOptions(
                LegacyGuardShieldHashSet.Empty,
                usage => report = usage,
                new LegacyGuardShieldBanPolicy(
                    UseSpeedHours: 6,
                    OpenProgramHours: 4,
                    AutoClickerHours: 2)));

        Assert.Equal(LegacyGuardShieldTextCommandStatus.Handled, result.Status);
        var captured = report ?? throw new InvalidOperationException("Expected usage report.");
        var decision = captured.BanDecision ?? throw new InvalidOperationException("Expected ban decision.");
        Assert.Equal(kind, decision.Kind);
        Assert.Equal((uint)expectedConfiguredHours, decision.ConfiguredHours);
        Assert.Equal((uint)expectedEffectiveHours, decision.EffectiveHours);
        Assert.Equal(expectedReason, decision.Reason);
        Assert.True(decision.WouldBan);
    }

    [Fact]
    public void UsedReportWithoutConfiguredPolicyDoesNotWouldBan()
    {
        var result = LegacyGuardShieldTextProtocol.HandleCommand("Used:Speed:detected-window:user02:TestSrv");

        Assert.Equal(LegacyGuardShieldTextCommandStatus.Handled, result.Status);
        var decision = result.UsageReport?.BanDecision ?? throw new InvalidOperationException("Expected ban decision.");
        Assert.False(decision.WouldBan);
        Assert.Equal(0u, decision.EffectiveHours);
        Assert.Equal("Use Speed", decision.Reason);
    }

    [Theory]
    [InlineData("Used:Speed:detected-window:user02", LegacyGuardShieldTextCommandStatus.Incomplete)]
    [InlineData("Used:Speed:detected-window:user02:", LegacyGuardShieldTextCommandStatus.Incomplete)]
    [InlineData("Used:Speed::user02:TestSrv", LegacyGuardShieldTextCommandStatus.Ignored)]
    [InlineData("Used:Speed:detected-window::TestSrv", LegacyGuardShieldTextCommandStatus.Ignored)]
    [InlineData("Used:   :detected-window:user02:TestSrv", LegacyGuardShieldTextCommandStatus.Incomplete)]
    public void UsedReportsRejectIncompleteOrBlankNormalizedEvidence(
        string command,
        LegacyGuardShieldTextCommandStatus expectedStatus)
    {
        LegacyGuardShieldUsageReport? report = null;

        var result = LegacyGuardShieldTextProtocol.HandleCommand(
            command,
            new LegacyGuardShieldTextProtocolOptions(
                LegacyGuardShieldHashSet.Empty,
                usage => report = usage));

        Assert.Equal(expectedStatus, result.Status);
        Assert.Null(result.Response);
        Assert.Null(result.UsageReport);
        Assert.Null(report);
    }

    [Theory]
    [InlineData("Used:Trainer:detected-window:user02:TestSrv")]
    [InlineData("Used:Clickers:detected-window:user02:TestSrv")]
    [InlineData("Used:Speeder:detected-window:user02:TestSrv")]
    public void UnknownUsageKindsAreIgnoredWithoutReport(string command)
    {
        LegacyGuardShieldUsageReport? report = null;

        var result = LegacyGuardShieldTextProtocol.HandleCommand(
            command,
            new LegacyGuardShieldTextProtocolOptions(
                LegacyGuardShieldHashSet.Empty,
                usage => report = usage));

        Assert.Equal(LegacyGuardShieldTextCommandStatus.Ignored, result.Status);
        Assert.Null(result.Response);
        Assert.Null(result.UsageReport);
        Assert.Null(report);
    }

    [Fact]
    public void HashOptionsLoadLegacyHashSystemIniSection()
    {
        var path = Path.Combine(Path.GetTempPath(), "wolf-guards-" + Guid.NewGuid() + ".ini");
        try
        {
            File.WriteAllText(
                path,
                """
                [LoaderSystem]
                ServerIP=127.0.0.1

                [HashSystem]
                ConquerEXEHash=exe-md5
                MagicEffectHash=effect-md5
                MagicTypeHash=type-md5
                StrResHash=strres-md5
                """);

            Assert.True(LegacyGuardShieldTextProtocolOptions.TryLoadHashSetFromIni(path, out var hashes));
            Assert.Equal("effect-md5", hashes.MagicEffect);
            Assert.Equal("type-md5", hashes.MagicType);
            Assert.Equal("strres-md5", hashes.StrRes);
            Assert.Equal("exe-md5", hashes.ConquerExe);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void BanOptionsLoadLegacyBanHourSystemIniSection()
    {
        var path = Path.Combine(Path.GetTempPath(), "wolf-guards-" + Guid.NewGuid() + ".ini");
        try
        {
            File.WriteAllText(
                path,
                """
                [LoaderSystem]
                ServerIP=127.0.0.1

                [BanHourSystem]
                UseSpeed=6
                OpenProgram=4
                AutoClicker=2
                """);

            Assert.True(LegacyGuardShieldTextProtocolOptions.TryLoadBanPolicyFromIni(path, out var banPolicy));
            Assert.Equal(6u, banPolicy.UseSpeedHours);
            Assert.Equal(4u, banPolicy.OpenProgramHours);
            Assert.Equal(2u, banPolicy.AutoClickerHours);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task GuardShieldWireModeRespondsToWelcomeHashTextCommand()
    {
        var hashes = new LegacyGuardShieldHashSet(
            "859879c1e0879ffaf0103d692357eca3",
            "b27bac732463dfe89bf33165aaef7a77",
            "13e7b5b53c49fc035c8c88a3b07c21bb",
            "ffd82341e15852535a808ece35429673");
        await using var server = new ConquerTcpServer(
            new ConquerTcpServerOptions(
                ConquerRuntimeDefaults.Gateway,
                IPAddress.Loopback,
                ListenPort: 0,
                WireMode: ConquerTcpWireMode.LegacyGuardShield,
                GuardShieldTextProtocol: new LegacyGuardShieldTextProtocolOptions(hashes)),
            (_, _, _) => throw new InvalidOperationException("GuardShield text mode must not dispatch packet frames."));

        server.Start();
        using var client = await ConnectAsync(server);
        var stream = client.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes("Welcome:Hash:HD123:HWID456"));

        var expected = Encoding.ASCII.GetBytes(hashes.ToLegacyResponse());
        var response = await ReadExactlyAsync(stream, expected.Length);
        Assert.Equal(expected, response);
    }

    [Fact]
    public async Task ReadCommandsHandlesMultipleNewlineDelimitedCommands()
    {
        var hashes = new LegacyGuardShieldHashSet(
            "magic-effect",
            "magic-type",
            "str-res",
            "conquer-exe");
        List<LegacyGuardShieldUsageReport> reports = [];
        List<string> responses = [];
        var pipe = new Pipe();
        var commands = string.Join(
            "\n",
            [
                "Used: Speed : detected-window : user02 : TestSrv",
                "Used:Trainer:ignored-window:user02:TestSrv",
                "Welcome:Hash:HD123:HWID456",
                "Used: Program : external-tool : demon3 : TestSrv",
                string.Empty,
            ]);

        await pipe.Writer.WriteAsync(Encoding.ASCII.GetBytes(commands));
        await pipe.Writer.CompleteAsync();

        await LegacyGuardShieldTextProtocol.ReadCommandsAsync(
            pipe.Reader,
            (bytes, _) =>
            {
                responses.Add(Encoding.ASCII.GetString(bytes.Span));
                return ValueTask.CompletedTask;
            },
            new LegacyGuardShieldTextProtocolOptions(
                hashes,
                reports.Add,
                new LegacyGuardShieldBanPolicy(
                    UseSpeedHours: 6,
                    OpenProgramHours: 4,
                    AutoClickerHours: 2)),
            maxCommandSize: 4096,
            CancellationToken.None);
        await pipe.Reader.CompleteAsync();

        Assert.Equal(["magic-effect | magic-type | str-res | conquer-exe"], responses);
        Assert.Collection(
            reports,
            report =>
            {
                Assert.Equal("Speed", report.Kind);
                Assert.Equal("detected-window", report.Detail);
                Assert.Equal("user02", report.Username);
                Assert.Equal("TestSrv", report.Server);
                Assert.True(report.BanDecision?.WouldBan);
                Assert.Equal("Use Speed", report.BanDecision?.Reason);
            },
            report =>
            {
                Assert.Equal("Program", report.Kind);
                Assert.Equal("external-tool", report.Detail);
                Assert.Equal("demon3", report.Username);
                Assert.Equal("TestSrv", report.Server);
                Assert.True(report.BanDecision?.WouldBan);
                Assert.Equal("Use Auto Program", report.BanDecision?.Reason);
            });
    }

    private static async Task<TcpClient> ConnectAsync(ConquerTcpServer server)
    {
        var endpoint = server.BoundEndPoint;
        var client = new TcpClient();
        await client.ConnectAsync(endpoint.Address, endpoint.Port);
        return client;
    }

    private static async Task<byte[]> ReadExactlyAsync(NetworkStream stream, int length)
    {
        var buffer = new byte[length];
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await stream.ReadExactlyAsync(buffer, timeout.Token);
        return buffer;
    }
}
