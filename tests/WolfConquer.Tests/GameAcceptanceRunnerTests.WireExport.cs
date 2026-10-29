using System.Text.Json;

namespace WolfConquer.Tests;

public sealed partial class GameAcceptanceRunnerTests
{
    private const string CapturedMailContent = "1600D808010001000000000000000000000000000000";
    private const string CapturedSecondPage = "2500D808070001000000020000000800000000000000090000000000000000000000000000";

    [Fact]
    public async Task WireExportSelectsCompletePlainFramesWithoutLeakingOtherLogRecords()
    {
        using var fixture = new WireExportFixture();
        var log = "LegacyGameWireTrace handshake secret-not-for-fixture\n" +
            WireLine(CapturedMailContent, 22, 2264) + "\n" +
            "WorldServer arbitrary diagnostic secret-not-for-fixture\n" +
            "LegacyGameWireTrace outbound-frame frameLength=22 opcode=2264 payloadLength=18 " +
            $"plainHex={CapturedMailContent} encryptedHex=secret-not-for-fixture\n" +
            "LegacyGameWireTrace inbound-frame frameLength=6 opcode=2183 payloadLength=2 expectedSeal=Client frameHex=060087080800\n" +
            WireLine(CapturedSecondPage, 37, 2264) + "\n";
        File.WriteAllText(fixture.Log, log);

        var result = await RunScriptAsync("Export-GameWireFixture.ps1", fixture.Arguments());

        Assert.Equal(0, result.ExitCode); Assert.Empty(result.Error);
        Assert.Equal(log, File.ReadAllText(fixture.Log));
        var text = File.ReadAllText(fixture.Output);
        Assert.DoesNotContain("secret-not-for-fixture", text);
        using var json = JsonDocument.Parse(text);
        var root = json.RootElement;
        Assert.False(root.GetProperty("liveCapturePerformed").GetBoolean());
        Assert.False(root.GetProperty("liveGameplayProven").GetBoolean());
        Assert.False(root.GetProperty("sourceGateAccepted").GetBoolean());
        Assert.Equal(new[] { CapturedMailContent, CapturedSecondPage }, root.GetProperty("frames")
            .EnumerateArray().Select(row => row.GetProperty("frameHex").GetString()).ToArray());
        Assert.Equal(new[] { 2, 6 }, root.GetProperty("frames")
            .EnumerateArray().Select(row => row.GetProperty("sourceLine").GetInt32()).ToArray());
    }

    [Theory]
    [InlineData("1600D8080100...(total 22 bytes)", 22, 2264)]
    [InlineData(CapturedMailContent, 23, 2264)]
    [InlineData(CapturedMailContent, 22, 2263)]
    [InlineData("1500D808010001000000000000000000000000000000", 22, 2264)]
    public async Task WireExportRejectsSelectedTruncatedOrInconsistentFrames(string hex, int length, int opcode)
    {
        using var fixture = new WireExportFixture();
        File.WriteAllText(fixture.Log, WireLine(hex, length, opcode) + "\n");
        var args = fixture.Arguments();
        args[Array.IndexOf(args, "-Opcode") + 1] = opcode.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var result = await RunScriptAsync("Export-GameWireFixture.ps1", args);

        Assert.Equal(1, result.ExitCode); Assert.Empty(result.Error);
        Assert.False(File.Exists(fixture.Output));
        using var json = JsonDocument.Parse(result.Output);
        Assert.Equal("WireFixtureExportFailed", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task WireExportReportsBoundsIgnoresPartialTailAndNeverOverwrites()
    {
        using var fixture = new WireExportFixture();
        File.WriteAllText(fixture.Log, WireLine(CapturedMailContent, 22, 2264) + "\n" +
            WireLine(CapturedSecondPage, 37, 2264) + "\npartial secret-not-for-fixture");

        var result = await RunScriptAsync("Export-GameWireFixture.ps1",
            fixture.Arguments().Concat(new[] { "-MaximumFrames", "1" }).ToArray());

        Assert.Equal(0, result.ExitCode); Assert.Empty(result.Error);
        var before = File.ReadAllBytes(fixture.Output);
        using (var json = JsonDocument.Parse(before))
        {
            Assert.True(json.RootElement.GetProperty("frameLimitReached").GetBoolean());
            Assert.True(json.RootElement.GetProperty("partialFinalLineIgnored").GetBoolean());
            Assert.Single(json.RootElement.GetProperty("frames").EnumerateArray());
        }
        var retry = await RunScriptAsync("Export-GameWireFixture.ps1", fixture.Arguments());
        Assert.Equal(1, retry.ExitCode); Assert.Empty(retry.Error);
        Assert.Equal(before, File.ReadAllBytes(fixture.Output));
    }

    [Fact]
    public async Task WireExportSupportsOutboundPlaintextAndNoMatchDoesNotCreateAFile()
    {
        using var fixture = new WireExportFixture();
        File.WriteAllText(fixture.Log,
            "LegacyGameWireTrace outbound-frame frameLength=22 opcode=2264 payloadLength=18 " +
            $"plainHex={CapturedMailContent} encryptedHex=private-encrypted-field\n");
        var noMatch = await RunScriptAsync("Export-GameWireFixture.ps1", fixture.Arguments());
        Assert.Equal(1, noMatch.ExitCode); Assert.Empty(noMatch.Error);
        Assert.False(File.Exists(fixture.Output));

        var result = await RunScriptAsync("Export-GameWireFixture.ps1",
            fixture.Arguments().Concat(new[] { "-Direction", "Outbound" }).ToArray());

        Assert.Equal(0, result.ExitCode); Assert.Empty(result.Error);
        var text = File.ReadAllText(fixture.Output);
        Assert.DoesNotContain("private-encrypted-field", text);
        using var json = JsonDocument.Parse(text);
        Assert.Equal("outbound", json.RootElement.GetProperty("frames")[0].GetProperty("direction").GetString());
    }

    private static string WireLine(string hex, int length, int opcode) =>
        $"LegacyGameWireTrace inbound-frame frameLength={length} opcode={opcode} payloadLength={length - 4} expectedSeal=Client frameHex={hex}";

    [Fact]
    public async Task WireExportDoesNotSilentlySkipMalformedSelectedMetadataAfterAValidFrame()
    {
        using var fixture = new WireExportFixture();
        File.WriteAllText(fixture.Log, WireLine(CapturedMailContent, 22, 2264) + "\n" +
            "LegacyGameWireTrace inbound-frame frameLength=22 opcode=2264 payloadLength=18\n");
        var result = await RunScriptAsync("Export-GameWireFixture.ps1", fixture.Arguments());
        Assert.Equal(1, result.ExitCode); Assert.Empty(result.Error);
        Assert.False(File.Exists(fixture.Output));
        using var json = JsonDocument.Parse(result.Output);
        Assert.Equal("selectedFrameMetadataInvalid", json.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task WireExportCutsPartialUtf8AtTheLastCompleteLogLine()
    {
        using var fixture = new WireExportFixture();
        var complete = System.Text.Encoding.UTF8.GetBytes(WireLine(CapturedMailContent, 22, 2264) + "\n");
        File.WriteAllBytes(fixture.Log, [.. complete, 0xe4, 0xb8]);
        var result = await RunScriptAsync("Export-GameWireFixture.ps1", fixture.Arguments());
        Assert.Equal(0, result.ExitCode); Assert.Empty(result.Error);
        using var json = JsonDocument.Parse(File.ReadAllBytes(fixture.Output));
        Assert.True(json.RootElement.GetProperty("partialFinalLineIgnored").GetBoolean());
        Assert.Equal(CapturedMailContent, json.RootElement.GetProperty("frames")[0].GetProperty("frameHex").GetString());
    }

    [Fact]
    public async Task WireExportRejectsUncAndNamedStreamOutputBeforeTouchingTheDestination()
    {
        using var fixture = new WireExportFixture();
        File.WriteAllText(fixture.Log, WireLine(CapturedMailContent, 22, 2264) + "\n");
        foreach (var destination in new[] { @"\\invalid.example\share\fixture.json", fixture.Log + ":stream.json" })
        {
            var args = fixture.Arguments(); args[Array.IndexOf(args, "-OutputPath") + 1] = destination;
            var result = await RunScriptAsync("Export-GameWireFixture.ps1", args);
            Assert.Equal(1, result.ExitCode); Assert.Empty(result.Error);
            using var json = JsonDocument.Parse(result.Output);
            Assert.Contains(json.RootElement.GetProperty("reason").GetString(),
                new[] { "networkOrDevicePathRejected", "namedStreamPathRejected" });
        }
        Assert.Equal(WireLine(CapturedMailContent, 22, 2264) + "\n", File.ReadAllText(fixture.Log));
        Assert.False(File.Exists(fixture.Output));
    }

    private sealed class WireExportFixture : IDisposable
    {
        private string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-wire-export-" + Guid.NewGuid().ToString("N"));
        internal string Log => Path.Combine(Root, "world.log");
        internal string Output => Path.Combine(Root, "fixture.json");
        internal WireExportFixture() => Directory.CreateDirectory(Root);
        internal string[] Arguments() => ["-TraceLogPath", Log, "-OutputPath", Output, "-Opcode", "2264",
            "-ClientBuild", "7899", "-ProtocolProfile", "VisibleDx8D13", "-Observation", "Synthetic exporter control"];
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
