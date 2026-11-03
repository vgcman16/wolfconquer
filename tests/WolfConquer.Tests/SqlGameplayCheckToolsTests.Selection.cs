using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;

namespace WolfConquer.Tests;

public sealed partial class SqlGameplayCheckToolsTests
{
    [Fact]
    public async Task SelectedPlanUsesOnlyExactDiscoveredFactAndDoesNotRunInputs()
    {
        WriteSyntheticSqlFacts("[ModernSqlAuthFixtureFact] public Task FirstFact() => Task.CompletedTask;\n" +
            "[ModernSqlAuthFixtureFact] public async Task SecondFact() { await Task.CompletedTask; }");
        var before = Snapshot();
        var selected = await RunAsync("-Port", "34121", "-OnlyFact", "SecondFact", "-Plan");
        Assert.True(selected.ExitCode == 0, $"Exit {selected.ExitCode}; stderr: {selected.Error}");
        using (var json = JsonDocument.Parse(selected.Output))
        {
            var plan = json.RootElement;
            Assert.False(plan.GetProperty("fullGate").GetBoolean());
            Assert.False(plan.GetProperty("fullProviderFamily").GetBoolean());
            Assert.Equal(new[] { "SecondFact" }, plan.GetProperty("selectedFactNames").EnumerateArray().Select(value => value.GetString()));
            const string filter = "FullyQualifiedName=WolfConquer.Tests.ModernSqlAuthFileRoundtripTests.SecondFact";
            Assert.Equal(filter, plan.GetProperty("filter").GetString());
            var args = plan.GetProperty("command").GetProperty("arguments").EnumerateArray().Select(value => value.GetString()).ToArray();
            Assert.Equal(filter, args[Array.IndexOf(args, "--filter") + 1]);
            Assert.DoesNotContain("--no-build", args); Assert.DoesNotContain("--no-restore", args);
        }
        AssertSnapshot(before);
        var full = await RunAsync("-Port", "34121", "-Plan");
        Assert.Equal(0, full.ExitCode);
        using (var json = JsonDocument.Parse(full.Output))
        {
            Assert.True(json.RootElement.GetProperty("fullProviderFamily").GetBoolean());
            Assert.Empty(json.RootElement.GetProperty("selectedFactNames").EnumerateArray());
            Assert.Equal("FullyQualifiedName~WolfConquer.Tests.ModernSqlAuthFileRoundtripTests", json.RootElement.GetProperty("filter").GetString());
        }
        AssertSnapshot(before);
    }

    [Fact]
    public async Task SelectedPlanRejectsAbsentMalformedCaseMismatchAndDuplicateInventory()
    {
        WriteSyntheticSqlFacts("[ModernSqlAuthFixtureFact] public Task FirstFact() => Task.CompletedTask;");
        var before = Snapshot();
        foreach (string name in new[] { "", "firstfact", "MissingFact", "FirstFact|FullyQualifiedName~Other", "FirstFact*" })
        {
            var result = await RunAsync("-Port", "34121", "-OnlyFact", name, "-Plan");
            Assert.NotEqual(0, result.ExitCode); AssertSnapshot(before);
        }
        WriteSyntheticSqlFacts("[ModernSqlAuthFixtureFact] public Task FirstFact() => Task.CompletedTask;\n" +
            "[ModernSqlAuthFixtureFact] public Task FirstFact() => Task.CompletedTask;");
        before = Snapshot();
        var duplicate = await RunAsync("-Port", "34121", "-OnlyFact", "FirstFact", "-Plan");
        Assert.NotEqual(0, duplicate.ExitCode); Assert.Contains("Duplicate", duplicate.Error, StringComparison.OrdinalIgnoreCase);
        AssertSnapshot(before);
        WriteSyntheticSqlFacts("[ModernSqlAuthFixtureFact] public void UnrecognizedFact() { }");
        before = Snapshot();
        var incomplete = await RunAsync("-Port", "34121", "-OnlyFact", "UnrecognizedFact", "-Plan");
        Assert.NotEqual(0, incomplete.ExitCode); AssertSnapshot(before);
    }

    private void WriteSyntheticSqlFacts(string text) => File.WriteAllText(
        Path.Combine(Path.GetDirectoryName(Project)!, "ModernSqlAuthFileRoundtripTests.Synthetic.cs"), text);

    [Fact]
    public async Task SelectedTrxRequiresOneExactPassedExecutionAndFullInventoryStillRequiresEveryFact()
    {
        var good = WriteSyntheticTrx("Passed", duplicateResult: false);
        Assert.Equal(0, (await CheckTrxAsync(good, "FirstFact", selected: true)).ExitCode);
        Assert.NotEqual(0, (await CheckTrxAsync(good, "SecondFact", selected: true)).ExitCode);
        Assert.NotEqual(0, (await CheckTrxAsync(good, "FirstFact,SecondFact", selected: false)).ExitCode);
        var skipped = WriteSyntheticTrx("NotExecuted", duplicateResult: false);
        Assert.NotEqual(0, (await CheckTrxAsync(skipped, "FirstFact", selected: true)).ExitCode);
        var extraSkip = MutateSyntheticTrx(good, document =>
        {
            XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
            string testId = Guid.NewGuid().ToString("D"), executionId = Guid.NewGuid().ToString("D");
            document.Root!.Element(ns + "ResultSummary")!.Element(ns + "Counters")!.SetAttributeValue("total", "2");
            document.Root.Element(ns + "TestDefinitions")!.Add(new XElement(ns + "UnitTest",
                new XAttribute("id", testId), new XAttribute("name", "Other"),
                new XElement(ns + "TestMethod", new XAttribute("className", "Other"), new XAttribute("name", "Other"))));
            document.Root.Element(ns + "Results")!.Add(new XElement(ns + "UnitTestResult",
                new XAttribute("testId", testId), new XAttribute("executionId", executionId), new XAttribute("outcome", "NotExecuted")));
        });
        Assert.NotEqual(0, (await CheckTrxAsync(extraSkip, "FirstFact", selected: true)).ExitCode);
        var duplicate = WriteSyntheticTrx("Passed", duplicateResult: true);
        Assert.NotEqual(0, (await CheckTrxAsync(duplicate, "FirstFact", selected: true)).ExitCode);
        var duplicateDefinition = MutateSyntheticTrx(good, document =>
        {
            XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
            var definitions = document.Root!.Element(ns + "TestDefinitions")!;
            definitions.Add(new XElement(definitions.Element(ns + "UnitTest")!));
        });
        Assert.NotEqual(0, (await CheckTrxAsync(duplicateDefinition, "FirstFact", selected: true)).ExitCode);
        var duplicateEntry = MutateSyntheticTrx(good, document =>
        {
            XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
            var entries = document.Root!.Element(ns + "TestEntries")!;
            entries.Add(new XElement(entries.Element(ns + "TestEntry")!));
        });
        Assert.NotEqual(0, (await CheckTrxAsync(duplicateEntry, "FirstFact", selected: true)).ExitCode);
        var wrongExecution = MutateSyntheticTrx(good, document =>
        {
            XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
            document.Root!.Element(ns + "TestEntries")!.Element(ns + "TestEntry")!
                .SetAttributeValue("executionId", Guid.NewGuid().ToString("D"));
        });
        Assert.NotEqual(0, (await CheckTrxAsync(wrongExecution, "FirstFact", selected: true)).ExitCode);
        Assert.False(Directory.Exists(Path.Combine(root, ".artifacts")));
    }

    private string MutateSyntheticTrx(string source, Action<XDocument> mutate)
    {
        var document = XDocument.Load(source); mutate(document);
        string path = Path.Combine(root, Guid.NewGuid().ToString("N") + ".trx");
        document.Save(path); return path;
    }

    private string WriteSyntheticTrx(string outcome, bool duplicateResult)
    {
        string path = Path.Combine(root, Guid.NewGuid().ToString("N") + ".trx");
        string test = Guid.NewGuid().ToString("D"), execution = Guid.NewGuid().ToString("D");
        string row = $"<UnitTestResult testId=\"{test}\" executionId=\"{execution}\" outcome=\"{outcome}\" />";
        int total = duplicateResult ? 2 : 1, passed = outcome == "Passed" ? total : 0;
        string rows = duplicateResult ? row + row : row;
        File.WriteAllText(path, $"""
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <Times finish="2026-01-01T00:00:00Z" />
              <ResultSummary outcome="Completed"><Counters total="{total}" executed="{passed}" passed="{passed}" failed="0" error="0" timeout="0" aborted="0" inProgress="0" pending="0" passedButRunAborted="0" /></ResultSummary>
              <TestDefinitions><UnitTest id="{test}" name="FirstFact"><Execution id="{execution}" /><TestMethod className="WolfConquer.Tests.ModernSqlAuthFileRoundtripTests" name="FirstFact" /></UnitTest></TestDefinitions>
              <TestEntries><TestEntry testId="{test}" executionId="{execution}" /></TestEntries>
              <Results>{rows}</Results>
            </TestRun>
            """);
        return path;
    }

    private async Task<Result> CheckTrxAsync(string trx, string required, bool selected)
    {
        string harness = Path.Combine(root, "check-trx.ps1");
        File.WriteAllText(harness, """
            param([string] $Support, [string] $Results, [string] $Trx, [string] $Required, [switch] $Selected)
            $ErrorActionPreference = 'Stop'
            Import-Module $Support -Force -DisableNameChecking
            . $Results
            Assert-OwnedSqlTrx -TrxPath $Trx -RequiredFacts @($Required.Split(',')) -Selected $Selected.IsPresent
            """);
        var start = new ProcessStartInfo(PowerShellPath())
        {
            UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = OtherDirectory,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (var arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-File", harness,
            "-Support", FindTool("FocusedCheckSupport.psm1"), "-Results", FindTool("owned-sql/Results.ps1"),
            "-Trx", trx, "-Required", required }.Concat(selected ? new[] { "-Selected" } : Array.Empty<string>()))
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); throw; }
        return new(process.ExitCode, await output, await error);
    }
}
