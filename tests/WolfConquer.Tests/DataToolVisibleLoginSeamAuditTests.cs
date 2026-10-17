using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class DataToolVisibleLoginSeamAuditTests
{
    private const string ServerConfigPlainHex = "1900FF0700000000000000000F0000000F2700000300000001";

    [Fact]
    public async Task ReportsVisibleLoginSuccessThenWorldCloseWithClientWarnings()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-visible-login-seam-audit-" + Guid.NewGuid().ToString("N"));
        var accountLog = Path.Combine(tempRoot, "AccountServer.out.log");
        var gatewayLog = Path.Combine(tempRoot, "Gateway.out.log");
        var worldLog = Path.Combine(tempRoot, "WorldServer.out.log");
        var clientLog = Path.Combine(tempRoot, "debug.log");
        var gameItemPlainHex = CreateLegacyGameItemPlainHex();

        try
        {
            await WriteFixtureFileAsync(
                gatewayLog,
                """
                Gateway accepted client 127.0.0.1:50000 on 127.0.0.1:2021
                Gateway GuardShield command command=Welcome status=Handled fields=4 responseBytes=24 usage=<none>
                Gateway closed client 127.0.0.1:50000 on 127.0.0.1:2021
                """);
            await WriteFixtureFileAsync(
                accountLog,
                """
                AccountServer accepted client 127.0.0.1:50001 on 127.0.0.1:9960
                AccountServer legacy login Ready reason=Success for 2.
                AccountServer closed client 127.0.0.1:50001 on 127.0.0.1:9960
                """);
            await WriteFixtureFileAsync(
                worldLog,
                $"""
                WorldServer accepted client 127.0.0.1:50002 on 127.0.0.1:15818
                {OutboundFrame(WorldPacketOpcodes.LegacyMsgUpdate, 44)}
                {OutboundFrame(WorldPacketOpcodes.LegacyServerVersion, 16)}
                {OutboundFrame(WorldPacketOpcodes.LegacyServerTimer, 0)}
                {OutboundFrame(WorldPacketOpcodes.LegacyServerConfig, 21, ServerConfigPlainHex)}
                WorldServer legacy MsgLoginGame Success key=1293600 uid=1000002 map=1002.
                {OutboundFrame(WorldPacketOpcodes.LegacyMsgUpdate, 104)}
                {OutboundFrame(WorldPacketOpcodes.LegacyQuestList, 28)}
                {OutboundFrame(WorldPacketOpcodes.LegacyMsgMessage, 8)}
                {OutboundFrame(LegacyGameItemPacket.Opcode, LegacyGameItemPacket.PayloadLength, gameItemPlainHex)}
                WorldServer closed client 127.0.0.1:50002 on 127.0.0.1:15818
                """);
            await WriteFixtureFileAsync(
                clientLog,
                """
                normal debug line
                WARNING ToStringEx failed during prompt reset
                MicroSpotData warning after world close
                """);

            var result = await RunDataToolAsync(
                "visible-login-seam-audit",
                "--account-log",
                accountLog,
                "--gateway-log",
                gatewayLog,
                "--world-log",
                worldLog,
                "--client-log",
                clientLog);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Visible login seam audit:", result.StandardOutput);
            Assert.Contains($"Gateway log: {Path.GetFullPath(gatewayLog)}", result.StandardOutput);
            Assert.Contains($"Account log: {Path.GetFullPath(accountLog)}", result.StandardOutput);
            Assert.Contains($"World log: {Path.GetFullPath(worldLog)}", result.StandardOutput);
            Assert.Contains($"Client log: {Path.GetFullPath(clientLog)}", result.StandardOutput);
            Assert.Contains("Gateway Welcome handled: True line=2", result.StandardOutput);
            Assert.Contains("Account Ready for account 2: True line=2", result.StandardOutput);
            Assert.Contains("World MsgLoginGame Success: True line=6 key=1293600 uid=1000002 map=1002", result.StandardOutput);
            Assert.Contains(
                "World outbound before latest success: count=4 frames=2176:44, 2161:16, 2224:0, 2047:21[config=0,0,15,9999,3,1]",
                result.StandardOutput);
            Assert.Contains("World startup prefix before latest success: source-compatible:2161->2224->2047", result.StandardOutput);
            Assert.Contains("World outbound after startup prefix before latest success: count=0 frames=<none>", result.StandardOutput);
            Assert.Contains("World client closed after success: True line=11", result.StandardOutput);
            Assert.Contains("World outbound after success before close: count=4 frames=2176:104, 2120:28, 2179:8, 2116:202", result.StandardOutput);
            Assert.Contains(
                "World last outbound before close: line=10 opcode=2116 payloadLength=202 " +
                "item=uid=5204369 itemId=3324226 durability=1 maxDurability=1 mode=1 position=0 " +
                "spellId=4010101 effect=1 plus=2 bless=3 bound=1 enchant=4 " +
                "stackSize=10 timeLeftInMinutes=1440 runeExperience=777",
                result.StandardOutput);
            Assert.Contains("Client ToStringEx/MicroSpotData warnings: 2", result.StandardOutput);
            Assert.Contains("line=2 kind=ToStringEx preview=WARNING ToStringEx failed during prompt reset", result.StandardOutput);
            Assert.Contains("line=3 kind=MicroSpotData preview=MicroSpotData warning after world close", result.StandardOutput);
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
    public async Task DecodesSkipTimerServerConfigClosePoint()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-visible-login-seam-audit-skip-timer-" + Guid.NewGuid().ToString("N"));
        var accountLog = Path.Combine(tempRoot, "AccountServer.out.log");
        var gatewayLog = Path.Combine(tempRoot, "Gateway.out.log");
        var worldLog = Path.Combine(tempRoot, "WorldServer.out.log");

        try
        {
            await WriteFixtureFileAsync(gatewayLog, "Gateway GuardShield command command=Welcome status=Handled fields=4 responseBytes=24 usage=<none>");
            await WriteFixtureFileAsync(accountLog, "AccountServer legacy login Ready reason=Success for 2.");
            await WriteFixtureFileAsync(
                worldLog,
                $"""
                {OutboundFrame(WorldPacketOpcodes.LegacyQuestList, 32)}
                {OutboundFrame(WorldPacketOpcodes.LegacyMsgMessage, 61)}
                {OutboundFrame(WorldPacketOpcodes.LegacyServerVersion, 8)}
                {OutboundFrame(WorldPacketOpcodes.LegacyServerConfig, 21, ServerConfigPlainHex)}
                WorldServer legacy MsgLoginGame Success key=1293600 uid=1293600 map=1002.
                WorldServer closed client 127.0.0.1:50002 on 127.0.0.1:15818
                """);

            var result = await RunDataToolAsync(
                "visible-login-seam-audit",
                "--account-log",
                accountLog,
                "--gateway-log",
                gatewayLog,
                "--world-log",
                worldLog);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains(
                "World outbound before latest success: count=4 frames=2120:32, 2179:61, 2161:8, 2047:21[config=0,0,15,9999,3,1]",
                result.StandardOutput);
            Assert.Contains("World startup prefix before latest success: skip-timer:2161->2047", result.StandardOutput);
            Assert.Contains("World outbound after startup prefix before latest success: count=0 frames=<none>", result.StandardOutput);
            Assert.Contains(
                "World last outbound before close: line=4 opcode=2047 payloadLength=21 item=<none> config=0,0,15,9999,3,1",
                result.StandardOutput);
            Assert.Contains("Ready: True", result.StandardOutput);
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
    public async Task ReportsCompactLegacyGameItemPreviewWhenPlainHexStopsAfterPosition()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-visible-login-seam-audit-short-item-" + Guid.NewGuid().ToString("N"));
        var accountLog = Path.Combine(tempRoot, "AccountServer.out.log");
        var gatewayLog = Path.Combine(tempRoot, "Gateway.out.log");
        var worldLog = Path.Combine(tempRoot, "WorldServer.out.log");

        try
        {
            await WriteFixtureFileAsync(gatewayLog, "Gateway GuardShield command command=Welcome status=Handled fields=4 responseBytes=24 usage=<none>");
            await WriteFixtureFileAsync(accountLog, "AccountServer legacy login Ready reason=Success for 2.");
            await WriteFixtureFileAsync(
                worldLog,
                """
                LegacyGameWireTrace outbound-frame frameLength=206 opcode=2116 payloadLength=202 plainHex=CE00440891694F0042B9320001000100010000
                WorldServer legacy MsgLoginGame Success key=1293600 uid=1000002 map=1002.
                WorldServer closed client 127.0.0.1:50002 on 127.0.0.1:15818
                """);

            var report = VisibleLoginSeamLogAuditor.Audit(new VisibleLoginSeamAuditOptions(
                accountLog,
                gatewayLog,
                worldLog,
                ClientLogPath: null));
            var output = VisibleLoginSeamLogAuditor.FormatReport(report);

            Assert.Contains(
                "World last outbound before close: line=1 opcode=2116 payloadLength=202 " +
                "item=uid=5204369 itemId=3324226 durability=1 maxDurability=1 mode=1 position=0",
                output);
            Assert.DoesNotContain("spellId=", output);
            Assert.DoesNotContain("stackSize=", output);
            Assert.DoesNotContain("runeExperience=", output);
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
    public async Task MissingWorldCloseAfterSuccessFailsReadyButStillReportsProofs()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-visible-login-seam-audit-missing-" + Guid.NewGuid().ToString("N"));
        var accountLog = Path.Combine(tempRoot, "AccountServer.out.log");
        var gatewayLog = Path.Combine(tempRoot, "Gateway.out.log");
        var worldLog = Path.Combine(tempRoot, "WorldServer.out.log");

        try
        {
            await WriteFixtureFileAsync(gatewayLog, "Gateway GuardShield command command=Welcome status=Handled fields=4 responseBytes=24 usage=<none>");
            await WriteFixtureFileAsync(accountLog, "AccountServer legacy login Ready reason=Success for 2.");
            await WriteFixtureFileAsync(worldLog, "WorldServer legacy MsgLoginGame Success key=1293600 uid=1000002 map=1002.");

            var result = await RunDataToolAsync(
                "visible-login-seam-audit",
                "--account-log",
                accountLog,
                "--gateway-log",
                gatewayLog,
                "--world-log",
                worldLog);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("Gateway Welcome handled: True line=1", result.StandardOutput);
            Assert.Contains("Account Ready for account 2: True line=1", result.StandardOutput);
            Assert.Contains("World MsgLoginGame Success: True line=1 key=1293600 uid=1000002 map=1002", result.StandardOutput);
            Assert.Contains("World outbound before latest success: count=0 frames=<none>", result.StandardOutput);
            Assert.Contains("World outbound after startup prefix before latest success: count=0 frames=<none>", result.StandardOutput);
            Assert.Contains("World client closed after success: False line=<none>", result.StandardOutput);
            Assert.Contains("World outbound after success before close: count=0 frames=<none>", result.StandardOutput);
            Assert.Contains("World last outbound before close: line=<none> opcode=<none> payloadLength=<none> item=<none>", result.StandardOutput);
            Assert.Contains("Client log: <none>", result.StandardOutput);
            Assert.Contains("Client ToStringEx/MicroSpotData warnings: 0", result.StandardOutput);
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
    public async Task UsesLatestWorldSuccessInsteadOfStaleEarlierClose()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-visible-login-seam-audit-latest-" + Guid.NewGuid().ToString("N"));
        var accountLog = Path.Combine(tempRoot, "AccountServer.out.log");
        var gatewayLog = Path.Combine(tempRoot, "Gateway.out.log");
        var worldLog = Path.Combine(tempRoot, "WorldServer.out.log");

        try
        {
            await WriteFixtureFileAsync(
                gatewayLog,
                """
                Gateway GuardShield command command=Welcome status=Handled fields=4 responseBytes=24 usage=<none>
                Gateway GuardShield command command=Welcome status=Handled fields=5 responseBytes=24 usage=<none>
                """);
            await WriteFixtureFileAsync(
                accountLog,
                """
                AccountServer legacy login Ready reason=Success for 2.
                AccountServer legacy login Ready reason=Success for 2.
                """);
            await WriteFixtureFileAsync(
                worldLog,
                """
                LegacyGameWireTrace outbound-frame frameLength=8 opcode=2179 payloadLength=4 plainHex=00000000
                WorldServer legacy MsgLoginGame Success key=111 uid=111 map=1002.
                LegacyGameWireTrace outbound-frame frameLength=28 opcode=2120 payloadLength=24 plainHex=00000000
                WorldServer closed client 127.0.0.1:50002 on 127.0.0.1:15818
                LegacyGameWireTrace outbound-frame frameLength=20 opcode=2161 payloadLength=16 plainHex=00000000
                LegacyGameWireTrace outbound-frame frameLength=4 opcode=2224 payloadLength=0 plainHex=00000000
                WorldServer legacy MsgLoginGame Success key=222 uid=222 map=1002.
                """);

            var result = await RunDataToolAsync(
                "visible-login-seam-audit",
                "--account-log",
                accountLog,
                "--gateway-log",
                gatewayLog,
                "--world-log",
                worldLog);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("Gateway Welcome handled: True line=2", result.StandardOutput);
            Assert.Contains("Account Ready for account 2: True line=2", result.StandardOutput);
            Assert.Contains("World MsgLoginGame Success: True line=7 key=222 uid=222 map=1002", result.StandardOutput);
            Assert.Contains("World outbound before latest success: count=4 frames=2179:4, 2120:24, 2161:16, 2224:0", result.StandardOutput);
            Assert.Contains("World startup prefix before latest success: timer-only:2161->2224", result.StandardOutput);
            Assert.Contains("World outbound after startup prefix before latest success: count=0 frames=<none>", result.StandardOutput);
            Assert.Contains("World client closed after success: False line=<none>", result.StandardOutput);
            Assert.Contains("World outbound after success before close: count=0 frames=<none>", result.StandardOutput);
            Assert.Contains("World last outbound before close: line=<none> opcode=<none> payloadLength=<none> item=<none>", result.StandardOutput);
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
    public async Task ReportsStartupFramesAfterSourceCompatiblePrefix()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-visible-login-seam-audit-startup-tail-" + Guid.NewGuid().ToString("N"));
        var accountLog = Path.Combine(tempRoot, "AccountServer.out.log");
        var gatewayLog = Path.Combine(tempRoot, "Gateway.out.log");
        var worldLog = Path.Combine(tempRoot, "WorldServer.out.log");

        try
        {
            await WriteFixtureFileAsync(gatewayLog, "Gateway GuardShield command command=Welcome status=Handled fields=4 responseBytes=24 usage=<none>");
            await WriteFixtureFileAsync(accountLog, "AccountServer legacy login Ready reason=Success for 2.");
            await WriteFixtureFileAsync(
                worldLog,
                $"""
                {OutboundFrame(WorldPacketOpcodes.LegacyServerVersion, 16)}
                {OutboundFrame(WorldPacketOpcodes.LegacyServerTimer, 0)}
                {OutboundFrame(WorldPacketOpcodes.LegacyServerConfig, 21, ServerConfigPlainHex)}
                {OutboundFrame(WorldPacketOpcodes.LegacyLoaderHeroInfo, 28)}
                {OutboundFrame(WorldPacketOpcodes.LegacyProtoHeroInfo, 80)}
                WorldServer legacy MsgLoginGame Success key=1293600 uid=1000002 map=1002.
                """);

            var result = await RunDataToolAsync(
                "visible-login-seam-audit",
                "--account-log",
                accountLog,
                "--gateway-log",
                gatewayLog,
                "--world-log",
                worldLog);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains(
                "World outbound before latest success: count=5 frames=2161:16, 2224:0, 2047:21[config=0,0,15,9999,3,1], 22210:28, 2221:80",
                result.StandardOutput);
            Assert.Contains("World startup prefix before latest success: source-compatible:2161->2224->2047", result.StandardOutput);
            Assert.Contains("World outbound after startup prefix before latest success: count=2 frames=22210:28, 2221:80", result.StandardOutput);
            Assert.Contains("Ready: False", result.StandardOutput);
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
    public async Task ReportsSkippedStartupFramesAfterSourceCompatiblePrefix()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-visible-login-seam-audit-skipped-startup-" + Guid.NewGuid().ToString("N"));
        var accountLog = Path.Combine(tempRoot, "AccountServer.out.log");
        var gatewayLog = Path.Combine(tempRoot, "Gateway.out.log");
        var worldLog = Path.Combine(tempRoot, "WorldServer.out.log");

        try
        {
            await WriteFixtureFileAsync(gatewayLog, "Gateway GuardShield command command=Welcome status=Handled fields=4 responseBytes=24 usage=<none>");
            await WriteFixtureFileAsync(accountLog, "AccountServer legacy login Ready reason=Success for 2.");
            await WriteFixtureFileAsync(
                worldLog,
                $"""
                {OutboundFrame(WorldPacketOpcodes.LegacyServerVersion, 16)}
                {OutboundFrame(WorldPacketOpcodes.LegacyServerTimer, 0)}
                {OutboundFrame(WorldPacketOpcodes.LegacyServerConfig, 21, ServerConfigPlainHex)}
                LegacyGameWireTrace outbound-frame-skipped reason=closed-before-gate frameLength=4 opcode=2450 payloadLength=0 plainHex=04009209
                LegacyGameWireTrace outbound-frame-skipped reason=closed-before-gate frameLength=4 opcode=22210 payloadLength=0 plainHex=0400C256
                LegacyGameWireTrace outbound-frame-skipped reason=closed-before-gate frameLength=4 opcode=2221 payloadLength=0 plainHex=0400AD08
                WorldServer legacy MsgLoginGame Success key=1293600 uid=1000002 map=1002.
                WorldServer closed client 127.0.0.1:50002 on 127.0.0.1:15818
                """);

            var result = await RunDataToolAsync(
                "visible-login-seam-audit",
                "--account-log",
                accountLog,
                "--gateway-log",
                gatewayLog,
                "--world-log",
                worldLog);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("World startup prefix before latest success: source-compatible:2161->2224->2047", result.StandardOutput);
            Assert.Contains(
                "World skipped outbound before latest success: count=3 frames=closed-before-gate:2450:0, closed-before-gate:22210:0, closed-before-gate:2221:0",
                result.StandardOutput);
            Assert.Contains(
                "World skipped outbound after startup prefix before latest success: count=3 frames=closed-before-gate:2450:0, closed-before-gate:22210:0, closed-before-gate:2221:0",
                result.StandardOutput);
            Assert.Contains("Ready: True", result.StandardOutput);
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
    public async Task SinceLineFiltersIgnoreStaleEarlierLoginAttempts()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-visible-login-seam-audit-since-line-" + Guid.NewGuid().ToString("N"));
        var accountLog = Path.Combine(tempRoot, "AccountServer.out.log");
        var gatewayLog = Path.Combine(tempRoot, "Gateway.out.log");
        var worldLog = Path.Combine(tempRoot, "WorldServer.out.log");
        var clientLog = Path.Combine(tempRoot, "debug.log");

        try
        {
            await WriteFixtureFileAsync(
                gatewayLog,
                """
                Gateway GuardShield command command=Welcome status=Handled fields=4 responseBytes=24 usage=<none>
                Gateway GuardShield command command=Welcome status=Handled fields=5 responseBytes=24 usage=<none>
                """);
            await WriteFixtureFileAsync(
                accountLog,
                """
                AccountServer legacy login Ready reason=Success for 1.
                AccountServer legacy login Ready reason=Success for 2.
                """);
            await WriteFixtureFileAsync(
                worldLog,
                $"""
                {OutboundFrame(WorldPacketOpcodes.LegacyMsgMessage, 4)}
                WorldServer legacy MsgLoginGame Success key=111 uid=111 map=1002.
                WorldServer closed client 127.0.0.1:50002 on 127.0.0.1:15818
                {OutboundFrame(WorldPacketOpcodes.LegacyServerVersion, 16)}
                {OutboundFrame(WorldPacketOpcodes.LegacyServerConfig, 21, ServerConfigPlainHex)}
                LegacyGameWireTrace outbound-frame-skipped reason=closed-before-gate frameLength=4 opcode=22210 payloadLength=0 plainHex=0400C256
                WorldServer legacy MsgLoginGame Success key=222 uid=222 map=1002.
                WorldServer closed client 127.0.0.1:50003 on 127.0.0.1:15818
                """);
            await WriteFixtureFileAsync(
                clientLog,
                """
                =========Lua error: stale ToStringEx warning
                [MicroSpotData] Error: fresh warning
                """);

            var result = await RunDataToolAsync(
                "visible-login-seam-audit",
                "--account-log",
                accountLog,
                "--gateway-log",
                gatewayLog,
                "--world-log",
                worldLog,
                "--client-log",
                clientLog,
                "--gateway-since-line",
                "1",
                "--account-since-line",
                "1",
                "--world-since-line",
                "3",
                "--client-since-line",
                "1");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Line filters: gateway>1 account>1 world>3 client>1", result.StandardOutput);
            Assert.Contains("Gateway Welcome handled: True line=2", result.StandardOutput);
            Assert.Contains("Account Ready for account 2: True line=2", result.StandardOutput);
            Assert.Contains("World MsgLoginGame Success: True line=7 key=222 uid=222 map=1002", result.StandardOutput);
            Assert.Contains("World outbound before latest success: count=2 frames=2161:16, 2047:21[config=0,0,15,9999,3,1]", result.StandardOutput);
            Assert.Contains("World skipped outbound before latest success: count=1 frames=closed-before-gate:22210:0", result.StandardOutput);
            Assert.Contains("World client closed after success: True line=8", result.StandardOutput);
            Assert.Contains("Client ToStringEx/MicroSpotData warnings: 1", result.StandardOutput);
            Assert.Contains("line=2 kind=MicroSpotData preview=[MicroSpotData] Error: fresh warning", result.StandardOutput);
            Assert.DoesNotContain("key=111", result.StandardOutput);
            Assert.DoesNotContain("stale ToStringEx warning", result.StandardOutput);
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
    public async Task LineMarkersReportCurrentCountsAndAuditCommand()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-visible-login-line-markers-" + Guid.NewGuid().ToString("N"));
        var accountLog = Path.Combine(tempRoot, "AccountServer.out.log");
        var gatewayLog = Path.Combine(tempRoot, "Gateway.out.log");
        var worldLog = Path.Combine(tempRoot, "WorldServer.out.log");
        var clientLog = Path.Combine(tempRoot, "debug.log");

        try
        {
            await WriteFixtureFileAsync(
                gatewayLog,
                """
                gateway 1
                gateway 2
                """);
            await WriteFixtureFileAsync(
                accountLog,
                """
                account 1
                account 2
                account 3
                """);
            await WriteFixtureFileAsync(worldLog, "world 1");
            await WriteFixtureFileAsync(
                clientLog,
                """
                client 1
                client 2
                client 3
                client 4
                """);

            var result = await RunDataToolAsync(
                "visible-login-line-markers",
                "--account-log",
                accountLog,
                "--gateway-log",
                gatewayLog,
                "--world-log",
                worldLog,
                "--client-log",
                clientLog);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Visible login line markers:", result.StandardOutput);
            Assert.Contains($"Gateway lineMarker=2 path={Path.GetFullPath(gatewayLog)}", result.StandardOutput);
            Assert.Contains($"Account lineMarker=3 path={Path.GetFullPath(accountLog)}", result.StandardOutput);
            Assert.Contains($"World lineMarker=1 path={Path.GetFullPath(worldLog)}", result.StandardOutput);
            Assert.Contains($"Client lineMarker=4 path={Path.GetFullPath(clientLog)}", result.StandardOutput);
            Assert.Contains("After the manual 7899 login, run:", result.StandardOutput);
            Assert.Contains("--gateway-since-line \"2\"", result.StandardOutput);
            Assert.Contains("--account-since-line \"3\"", result.StandardOutput);
            Assert.Contains("--world-since-line \"1\"", result.StandardOutput);
            Assert.Contains("--client-since-line \"4\"", result.StandardOutput);
            Assert.Contains($"--client-log \"{Path.GetFullPath(clientLog)}\"", result.StandardOutput);
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
    public async Task LineMarkerJsonDrivesFreshAuditWithoutCopyingLineNumbers()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-visible-login-line-marker-json-" + Guid.NewGuid().ToString("N"));
        var accountLog = Path.Combine(tempRoot, "AccountServer.out.log");
        var gatewayLog = Path.Combine(tempRoot, "Gateway.out.log");
        var worldLog = Path.Combine(tempRoot, "WorldServer.out.log");
        var clientLog = Path.Combine(tempRoot, "debug.log");
        var markerJson = Path.Combine(tempRoot, "visible-login-markers.json");

        try
        {
            await WriteFixtureFileAsync(
                gatewayLog,
                "Gateway GuardShield command command=Welcome status=Handled fields=4 responseBytes=24 usage=<none>");
            await WriteFixtureFileAsync(
                accountLog,
                "AccountServer legacy login Ready reason=Success for 1.");
            await WriteFixtureFileAsync(
                worldLog,
                $"""
                {OutboundFrame(WorldPacketOpcodes.LegacyMsgMessage, 4)}
                WorldServer legacy MsgLoginGame Success key=111 uid=111 map=1002.
                WorldServer closed client 127.0.0.1:50002 on 127.0.0.1:15818
                """);
            await WriteFixtureFileAsync(clientLog, "WARNING ToStringEx stale warning");

            var markerResult = await RunDataToolAsync(
                "visible-login-line-markers",
                "--account-log",
                accountLog,
                "--gateway-log",
                gatewayLog,
                "--world-log",
                worldLog,
                "--client-log",
                clientLog,
                "--output-json",
                markerJson);

            Assert.Equal(0, markerResult.ExitCode);
            Assert.True(File.Exists(markerJson));
            Assert.Contains($"Marker JSON: {Path.GetFullPath(markerJson)}", markerResult.StandardOutput);
            Assert.Contains($"--line-markers \"{Path.GetFullPath(markerJson)}\"", markerResult.StandardOutput);
            Assert.DoesNotContain("--gateway-since-line", markerResult.StandardOutput);
            using (var markerDocument = JsonDocument.Parse(await File.ReadAllTextAsync(markerJson)))
            {
                var root = markerDocument.RootElement;
                Assert.Equal(Path.GetFullPath(gatewayLog), root.GetProperty("GatewayLogPath").GetString());
                Assert.Equal(Path.GetFullPath(accountLog), root.GetProperty("AccountLogPath").GetString());
                Assert.Equal(Path.GetFullPath(worldLog), root.GetProperty("WorldLogPath").GetString());
                Assert.Equal(Path.GetFullPath(clientLog), root.GetProperty("ClientLogPath").GetString());
                Assert.Equal(1, root.GetProperty("GatewayLineMarker").GetInt32());
                Assert.Equal(1, root.GetProperty("AccountLineMarker").GetInt32());
                Assert.Equal(3, root.GetProperty("WorldLineMarker").GetInt32());
                Assert.Equal(1, root.GetProperty("ClientLineMarker").GetInt32());
            }

            await File.AppendAllTextAsync(
                gatewayLog,
                Environment.NewLine +
                "Gateway GuardShield command command=Welcome status=Handled fields=5 responseBytes=24 usage=<none>");
            await File.AppendAllTextAsync(
                accountLog,
                Environment.NewLine +
                "AccountServer legacy login Ready reason=Success for 2.");
            await File.AppendAllTextAsync(
                worldLog,
                Environment.NewLine +
                $"""
                {OutboundFrame(WorldPacketOpcodes.LegacyServerVersion, 16)}
                {OutboundFrame(WorldPacketOpcodes.LegacyServerConfig, 21, ServerConfigPlainHex)}
                LegacyGameWireTrace outbound-frame-skipped reason=closed-before-gate frameLength=4 opcode=22210 payloadLength=0 plainHex=0400C256
                WorldServer legacy MsgLoginGame Success key=222 uid=222 map=1002.
                WorldServer closed client 127.0.0.1:50003 on 127.0.0.1:15818
                """);
            await File.AppendAllTextAsync(
                clientLog,
                Environment.NewLine +
                "[MicroSpotData] Error: fresh warning");

            var auditResult = await RunDataToolAsync(
                "visible-login-seam-audit",
                "--line-markers",
                markerJson);

            Assert.Equal(0, auditResult.ExitCode);
            Assert.Contains("Line filters: gateway>1 account>1 world>3 client>1", auditResult.StandardOutput);
            Assert.Contains("Gateway Welcome handled: True line=2", auditResult.StandardOutput);
            Assert.Contains("Account Ready for account 2: True line=2", auditResult.StandardOutput);
            Assert.Contains("World MsgLoginGame Success: True line=7 key=222 uid=222 map=1002", auditResult.StandardOutput);
            Assert.Contains("World outbound before latest success: count=2 frames=2161:16, 2047:21[config=0,0,15,9999,3,1]", auditResult.StandardOutput);
            Assert.Contains("World skipped outbound before latest success: count=1 frames=closed-before-gate:22210:0", auditResult.StandardOutput);
            Assert.Contains("World client closed after success: True line=8", auditResult.StandardOutput);
            Assert.Contains("Client ToStringEx/MicroSpotData warnings: 1", auditResult.StandardOutput);
            Assert.Contains("line=2 kind=MicroSpotData preview=[MicroSpotData] Error: fresh warning", auditResult.StandardOutput);
            Assert.DoesNotContain("key=111", auditResult.StandardOutput);
            Assert.DoesNotContain("stale warning", auditResult.StandardOutput);
            Assert.Equal(string.Empty, auditResult.StandardError);
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
    public async Task HelpIncludesVisibleLoginSeamAuditUsage()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool visible-login-seam-audit --account-log path --gateway-log path --world-log path [--client-log path]",
            result.StandardOutput);
        Assert.Contains("[--line-markers path]", result.StandardOutput);
        Assert.Contains(
            "WolfConquer.DataTool visible-login-line-markers --account-log path --gateway-log path --world-log path [--client-log path]",
            result.StandardOutput);
        Assert.Contains("[--output-json path]", result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    private static string CreateLegacyGameItemPlainHex()
    {
        var packet = new LegacyGameItemPacket(
            Uid: 5_204_369,
            ItemId: 3_324_226,
            Durability: 1,
            MaximumDurability: 1,
            Mode: LegacyGameItemPacket.AddItemMode,
            Position: 0,
            SocketProgress: 0,
            SocketOne: 0,
            SocketTwo: 0,
            SpellId: 4_010_101,
            Effect: 1,
            Plus: 2,
            Bless: 3,
            Bound: 1,
            Enchant: 4,
            ProgressGreen: 0,
            Suspicious: 0,
            Locked: 0,
            PlusProgress: 0,
            RemainingTime: 0,
            TimeLeftInMinutes: 1_440,
            StackSize: 10,
            PerfectionLevel: 0,
            PerfectionProgress: 0,
            OwnerUid: 0,
            OwnerName: "",
            Signature: "",
            RuneExperience: 777,
            Attribute1: 0,
            Attribute2: 0,
            Attribute3: 0,
            Attribute4: 0,
            Attribute5: 0,
            AnimaItemId: 0,
            MythSoul: 0,
            MythSoulPoints: 0);
        return Convert.ToHexString(packet.ToFrameBytes());
    }

    private static string OutboundFrame(ushort opcode, int payloadLength, string plainHex = "00000000") =>
        $"LegacyGameWireTrace outbound-frame frameLength={payloadLength + 4} opcode={opcode} payloadLength={payloadLength} plainHex={plainHex}";

    private static async Task<ConsoleRunResult> RunDataToolAsync(params string[] arguments)
    {
        var repositoryRoot = FindRepositoryRoot();
        using var process = CreateDataToolProcess(repositoryRoot);
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        Assert.True(process.Start(), "Could not start DataTool.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
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

    private static Process CreateDataToolProcess(string repositoryRoot)
    {
        var copiedDataTool = Path.Combine(AppContext.BaseDirectory, "WolfConquer.DataTool.dll");
        if (!File.Exists(copiedDataTool))
        {
            return TestProjectProcess.Create(repositoryRoot, "WolfConquer.DataTool");
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = repositoryRoot,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.StartInfo.ArgumentList.Add(copiedDataTool);
        process.StartInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        process.StartInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        return process;
    }

    private static async Task WriteFixtureFileAsync(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, contents);
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourcePath = "")
    {
        var fromOutputDirectory = TryFindRepositoryRoot(AppContext.BaseDirectory);
        if (fromOutputDirectory is not null)
        {
            return fromOutputDirectory;
        }

        var fromWorkingDirectory = TryFindRepositoryRoot(Environment.CurrentDirectory);
        if (fromWorkingDirectory is not null)
        {
            return fromWorkingDirectory;
        }

        var sourceDirectory = Path.GetDirectoryName(sourcePath);
        if (sourceDirectory is not null)
        {
            var fromSourceDirectory = TryFindRepositoryRoot(sourceDirectory);
            if (fromSourceDirectory is not null)
            {
                return fromSourceDirectory;
            }
        }

        throw new InvalidOperationException("Could not locate WolfConquer.sln from the test output or working directory.");
    }

    private static string? TryFindRepositoryRoot(string startDirectory)
    {
        DirectoryInfo? directory = new(startDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);
}
