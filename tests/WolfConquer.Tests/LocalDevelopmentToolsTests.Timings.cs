using System.Security;

namespace WolfConquer.Tests;

public sealed partial class LocalDevelopmentToolsTests
{
    private const string TimingTimes = "<Times start=\"2026-11-03T00:00:00Z\" finish=\"2026-11-03T00:00:10Z\" />";
    private const string TimingRows = """
        <UnitTestResult testId="a1" testName="Display.One" outcome="Passed" duration="00:00:08" startTime="2026-11-03T00:00:00Z" endTime="2026-11-03T00:00:08Z" />
        <UnitTestResult testId="a2" testName="Display.Two" outcome="Failed" duration="00:00:04" startTime="2026-11-03T00:00:00Z" endTime="2026-11-03T00:00:04Z" />
        <UnitTestResult testId="b1" testName="Display.NotRun" outcome="NotExecuted" />
        <UnitTestResult testId="b2" testName="Display.Zero" outcome="Passed" duration="00:00:00" />
        <UnitTestResult testId="b3" testName="Display.Skip" outcome="Skipped" />
        """;

    private static string TimingXml(string rows = TimingRows, string times = TimingTimes) => $$"""
        <?xml version="1.0" encoding="utf-8"?>
        <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          {{times}}
          <Results>{{rows}}</Results>
          <TestDefinitions>
            <UnitTest id="a1"><TestMethod className="Mapped.A" /></UnitTest>
            <UnitTest id="a2"><TestMethod className="Mapped.A" /></UnitTest>
            <UnitTest id="b1"><TestMethod className="Mapped.B" /></UnitTest>
            <UnitTest id="b2"><TestMethod className="Mapped.B" /></UnitTest>
            <UnitTest id="b3"><TestMethod className="Mapped.B" /></UnitTest>
          </TestDefinitions>
          <ResultSummary outcome="Passed"><Counters total="999" passed="999" failed="0" notExecuted="0" /></ResultSummary>
        </TestRun>
        """;

    [Fact]
    public async Task StrictTimingReaderCountsActualRowsAndKeepsUnknownAndOverlappingTimesSeparate()
    {
        var path = Path.Combine(_root, "timing ;$ report.trx"); File.WriteAllText(path, TimingXml());
        var before = CaptureTimingFiles();
        using var json = ParseSuccess(await RunAsync("Get-TestTiming.ps1", "-ResultsPath", path, "-StrictReport", "-Top", "2", "-AsJson"));
        var report = json.RootElement;
        Assert.True(report.GetProperty("strictReport").GetBoolean());
        Assert.Equal(5, report.GetProperty("rowCount").GetInt32());
        var outcomes = report.GetProperty("outcomes");
        Assert.Equal(2, outcomes.GetProperty("Passed").GetInt32()); Assert.Equal(1, outcomes.GetProperty("Failed").GetInt32());
        Assert.Equal(1, outcomes.GetProperty("NotExecuted").GetInt32()); Assert.Equal(1, outcomes.GetProperty("Skipped").GetInt32());
        Assert.Equal(2, report.GetProperty("rowsWithoutDuration").GetInt32());
        Assert.Equal(3, report.GetProperty("rowsWithDuration").GetInt32());
        Assert.Equal(12, report.GetProperty("sumTestDurationSeconds").GetDouble());
        var file = Assert.Single(report.GetProperty("files").EnumerateArray());
        Assert.Equal(path, file.GetProperty("path").GetString()); Assert.Equal(10, file.GetProperty("wallSeconds").GetDouble());
        var top = report.GetProperty("slowestTests").EnumerateArray().ToArray();
        Assert.Equal(new[] { "Display.One", "Display.Two" }, top.Select(row => row.GetProperty("name").GetString()));
        Assert.Equal(new double[] { 8, 4 }, top.Select(row => row.GetProperty("durationSeconds").GetDouble()));
        var groups = report.GetProperty("largestClasses").EnumerateArray().ToDictionary(row => row.GetProperty("className").GetString()!);
        Assert.Equal(2, groups["Mapped.A"].GetProperty("knownDurationRows").GetInt32());
        Assert.Equal(0, groups["Mapped.A"].GetProperty("rowsWithoutDuration").GetInt32());
        Assert.Equal(12, groups["Mapped.A"].GetProperty("durationSeconds").GetDouble());
        Assert.Equal(3, groups["Mapped.B"].GetProperty("rows").GetInt32());
        Assert.Equal(1, groups["Mapped.B"].GetProperty("knownDurationRows").GetInt32());
        Assert.Equal(2, groups["Mapped.B"].GetProperty("rowsWithoutDuration").GetInt32());
        Assert.Equal(0, groups["Mapped.B"].GetProperty("durationSeconds").GetDouble()); // One actual measured zero; two unknowns.
        Assert.Contains("not wall time", report.GetProperty("timingLimit").GetString(), StringComparison.Ordinal);
        Assert.Contains("full CI", report.GetProperty("timingLimit").GetString(), StringComparison.Ordinal);
        AssertTimingFilesUnchanged(before);
    }

    [Fact]
    public async Task TimingReaderDefaultDirectoryAndMissingTimesRemainCompatibleWhileStrictRequiresBoth()
    {
        var reports = Path.Combine(_root, "reports"); Directory.CreateDirectory(reports);
        var missing = Path.Combine(reports, "missing-times.trx");
        File.WriteAllText(missing, TimingXml("<UnitTestResult testName=\"Fallback.Case\" outcome=\"NotExecuted\" />", ""));
        File.WriteAllText(Path.Combine(reports, "normal.trx"), TimingXml("<UnitTestResult testName=\"Fallback.Other\" outcome=\"Passed\" duration=\"00:00:02\" />"));
        var before = CaptureTimingFiles();
        using (var json = ParseSuccess(await RunAsync("Get-TestTiming.ps1", "-ResultsPath", reports, "-AsJson")))
        {
            Assert.False(json.RootElement.GetProperty("strictReport").GetBoolean());
            Assert.Equal(2, json.RootElement.GetProperty("rowCount").GetInt32());
            Assert.Equal(2, json.RootElement.GetProperty("files").GetArrayLength());
            Assert.Equal(1, json.RootElement.GetProperty("rowsWithoutDuration").GetInt32());
        }
        using (var json = ParseSuccess(await RunAsync("Get-TestTiming.ps1", "-ResultsPath", missing, "-AsJson")))
            Assert.Equal(System.Text.Json.JsonValueKind.Null, json.RootElement.GetProperty("files")[0].GetProperty("wallSeconds").ValueKind);
        foreach (var path in new[] { reports, missing })
        {
            var refused = await RunAsync("Get-TestTiming.ps1", "-ResultsPath", path, "-StrictReport", "-AsJson");
            Assert.NotEqual(0, refused.ExitCode); Assert.Equal("", refused.Output);
            Assert.Contains("StrictReport", refused.Error, StringComparison.Ordinal);
        }
        AssertTimingFilesUnchanged(before);
    }

    [Theory]
    [InlineData("missing-start")] [InlineData("implicit-offset")] [InlineData("invalid-finish")]
    [InlineData("reversed-times")] [InlineData("negative-duration")] [InlineData("bad-duration")] [InlineData("malformed-xml")]
    public async Task StrictTimingReaderRejectsInvalidRequiredValuesWithoutPartialOutputOrWrites(string scenario)
    {
        string times = TimingTimes, rows = TimingRows;
        if (scenario == "missing-start") times = times.Replace("start=\"2026-11-03T00:00:00Z\"", "", StringComparison.Ordinal);
        if (scenario == "implicit-offset") times = times.Replace("start=\"2026-11-03T00:00:00Z\"", "start=\"2026-11-03T00:00:00\"", StringComparison.Ordinal);
        if (scenario == "invalid-finish") times = times.Replace("2026-11-03T00:00:10Z", "not-a-time", StringComparison.Ordinal);
        if (scenario == "reversed-times") times = times.Replace("2026-11-03T00:00:10Z", "2026-11-02T23:59:59Z", StringComparison.Ordinal);
        if (scenario == "negative-duration") rows = rows.Replace("duration=\"00:00:08\"", "duration=\"-00:00:08\"", StringComparison.Ordinal);
        if (scenario == "bad-duration") rows = rows.Replace("duration=\"00:00:08\"", "duration=\"not-a-duration\"", StringComparison.Ordinal);
        var path = Path.Combine(_root, "invalid.trx");
        File.WriteAllText(path, scenario == "malformed-xml" ? "<TestRun>" : TimingXml(rows, times));
        var before = CaptureTimingFiles();
        var refused = await RunAsync("Get-TestTiming.ps1", "-ResultsPath", path, "-StrictReport", "-AsJson");
        Assert.NotEqual(0, refused.ExitCode); Assert.Equal("", refused.Output);
        AssertTimingFilesUnchanged(before);
    }

    [Fact]
    public async Task TimingReaderProhibitsDtdAndNeverExpandsAnExternalOwnedSentinel()
    {
        const string sentinel = "SYNTHETIC_PRIVATE_TRX_ENTITY_NOT_TO_BE_READ";
        var secret = Path.Combine(_root, "owned-entity.txt"); File.WriteAllText(secret, sentinel);
        var uri = SecurityElement.Escape(new Uri(secret).AbsoluteUri)!;
        var text = TimingXml().Replace("<TestRun ", $"<!DOCTYPE TestRun [<!ENTITY leak SYSTEM \"{uri}\">]>\n<TestRun ", StringComparison.Ordinal)
            .Replace("</TestRun>", "<Output>&leak;</Output></TestRun>", StringComparison.Ordinal);
        var paths = new[] { Path.Combine(_root, "plain-dtd.trx"), Path.Combine(_root, "entity.trx") };
        File.WriteAllText(paths[0], TimingXml().Replace("<TestRun ", "<!DOCTYPE TestRun []>\n<TestRun ", StringComparison.Ordinal));
        File.WriteAllText(paths[1], text);
        var before = CaptureTimingFiles();
        foreach (var path in paths)
        {
            foreach (bool strict in new[] { false, true })
            {
                var args = new List<string> { "-ResultsPath", path, "-AsJson" }; if (strict) args.Add("-StrictReport");
                var refused = await RunAsync("Get-TestTiming.ps1", args.ToArray());
                Assert.NotEqual(0, refused.ExitCode); Assert.Equal("", refused.Output);
                Assert.DoesNotContain(sentinel, refused.Output + refused.Error, StringComparison.Ordinal);
                AssertTimingFilesUnchanged(before);
            }
        }
    }

    private sealed record TimingFiles(string[] Entries, Dictionary<string, byte[]> Bytes, Dictionary<string, DateTime> Writes);
    private TimingFiles CaptureTimingFiles()
    {
        var files = Directory.GetFiles(_root, "*", SearchOption.AllDirectories);
        return new(EntryPaths(), files.ToDictionary(path => path, File.ReadAllBytes), files.ToDictionary(path => path, File.GetLastWriteTimeUtc));
    }
    private void AssertTimingFilesUnchanged(TimingFiles before)
    {
        Assert.Equal(before.Entries, EntryPaths());
        foreach (var (path, bytes) in before.Bytes)
        { Assert.Equal(bytes, File.ReadAllBytes(path)); Assert.Equal(before.Writes[path], File.GetLastWriteTimeUtc(path)); }
    }
}
