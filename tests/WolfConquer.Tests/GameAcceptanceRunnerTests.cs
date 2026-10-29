using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class WindowsGameAcceptanceFactAttribute : FactAttribute
{
    public WindowsGameAcceptanceFactAttribute()
    { if (!OperatingSystem.IsWindows()) Skip = "The acceptance inspector requires Windows physical owned paths."; }
}

public sealed partial class GameAcceptanceRunnerTests
{
    private static readonly string[] ScenarioIds =
    ["login-walk-save", "reconnect", "mailbox-gear", "equip-unequip", "upgrade-socket", "combat",
     "npc-shop-rewards", "trade-two-clients", "persistence-restart", "events-social", "updater"];

    [Fact]
    public async Task ListReturnsExactCatalogWithoutRunningAScenario()
    {
        var result = await RunAsync("-List");
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        using var json = JsonDocument.Parse(result.Output);
        Assert.Equal("Pending", json.RootElement.GetProperty("defaultState").GetString());
        Assert.Equal(ScenarioIds, json.RootElement.GetProperty("scenarios").EnumerateArray()
            .Select(row => row.GetProperty("id").GetString()).ToArray());
    }

    [WindowsGameAcceptanceFact]
    public async Task GuidedInspectionResumesWithoutGrantOrClaimAndRetainsOriginalEvidence()
    {
        var parent = Path.GetFullPath(Path.GetTempPath());
        var fixtureRoot = Path.Combine(parent, "wolf-owned-admission-" + Guid.NewGuid().ToString("N"));
        var evidenceRoot = Path.Combine(parent, "wolf-game-acceptance-" + Guid.NewGuid().ToString("N"));
        const uint account = 2_739_100;
        const uint item = 8_013_201;
        var complete = false;
        try
        {
            Assert.True(LegacyOwnedAdmissionFixture.TryCreate(fixtureRoot, account, account, item,
                ConquerProtocolProfile.VisibleDx8D13, out var created, out var reason), reason);
            Assert.NotNull(created);
            var initialFiles = Files(fixtureRoot);
            var statePath = Path.Combine(fixtureRoot, "PlayersItems", ".managed-item-uids", "state.json");
            var stateBefore = File.ReadAllBytes(statePath);
            var repository = RepositoryRoot();
            using var dataTool = TestProjectProcess.Create(repository, "WolfConquer.DataTool");
            var dll = dataTool.StartInfo.ArgumentList[0];
            var dotnet = FindApplication(dataTool.StartInfo.FileName, "DOTNET_HOST_PATH");
            var args = new[] { "-Scenario", "reconnect", "-DataToolDll", dll,
                "-OwnedAdmissionConfig", created.ConfigurationPath, "-AccountId", account.ToString(),
                "-EvidenceRoot", evidenceRoot, "-DotNetPath", dotnet };

            var first = await RunAsync(args);
            Assert.Equal(2, first.ExitCode);
            using (var output = JsonDocument.Parse(first.Output))
                Assert.Equal("GuidedActionsPending", output.RootElement.GetProperty("status").GetString());
            var before = File.ReadAllBytes(Path.Combine(evidenceRoot, "before.json"));
            var receipt = File.ReadAllBytes(Path.Combine(evidenceRoot, "receipt.json"));
            using (var initial = JsonDocument.Parse(receipt))
            {
                Assert.Equal("GuidedActionsPending", initial.RootElement.GetProperty("state").GetProperty("status").GetString());
                Assert.Equal("LiveClientVisualPending", initial.RootElement.GetProperty("visualAcceptance").GetString());
            }
            AssertFiles(fixtureRoot, initialFiles);
            Assert.Equal(stateBefore, File.ReadAllBytes(statePath));

            var resumed = await RunAsync(args.Concat(new[] { "-Resume" }).ToArray());
            Assert.Equal(2, resumed.ExitCode);
            string resumeReceipt;
            using (var output = JsonDocument.Parse(resumed.Output))
            {
                Assert.Equal("GuidedAssertionsPending", output.RootElement.GetProperty("status").GetString());
                resumeReceipt = output.RootElement.GetProperty("receipt").GetString()!;
            }
            using (var data = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(evidenceRoot, resumeReceipt))))
            {
                var after = data.RootElement.GetProperty("after").GetString();
                Assert.NotNull(after);
                Assert.True(File.Exists(Path.Combine(evidenceRoot, after)));
                Assert.Equal("LiveClientVisualPending", data.RootElement.GetProperty("visualAcceptance").GetString());
            }
            Assert.Equal(before, File.ReadAllBytes(Path.Combine(evidenceRoot, "before.json")));
            Assert.Equal(receipt, File.ReadAllBytes(Path.Combine(evidenceRoot, "receipt.json")));
            AssertFiles(fixtureRoot, initialFiles);
            Assert.Equal(stateBefore, File.ReadAllBytes(statePath));

            var evidenceBefore = Files(evidenceRoot);
            var refused = await RunAsync(args.Concat(new[] { "-Resume", "-SendTestKit" }).ToArray());
            Assert.Equal(1, refused.ExitCode);
            using (var output = JsonDocument.Parse(refused.Output))
                Assert.Equal("PrerequisiteMissingOrOutcomeUnproven", output.RootElement.GetProperty("status").GetString());
            AssertFiles(evidenceRoot, evidenceBefore);
            AssertFiles(fixtureRoot, initialFiles);
            Assert.Equal(stateBefore, File.ReadAllBytes(statePath));
            complete = true;
        }
        finally
        {
            // Failed assertions retain both owned roots for inspection.
            if (complete)
            {
                DeleteOwned(fixtureRoot, "wolf-owned-admission-");
                DeleteOwned(evidenceRoot, "wolf-game-acceptance-");
            }
        }
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunAsync(params string[] args)
        => await RunScriptAsync("Invoke-GameAcceptance.ps1", args);

    private static async Task<(int ExitCode, string Output, string Error)> RunScriptAsync(string script, params string[] args)
    {
        var start = new ProcessStartInfo(FindApplication("pwsh", "WOLFCONQUER_NATIVE_CONSOLE_PROBE_PWSH"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-File", Path.Combine(RepositoryRoot(), "tools", "testing", script) }.Concat(args))
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException primary)
        {
            try { process.Kill(entireProcessTree: true); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (Exception cleanup) { throw new AggregateException("Acceptance child timeout and cleanup both failed.", primary, cleanup); }
            throw;
        }
        return (process.ExitCode, await output, await error);
    }

    private static string FindApplication(string command, string variable)
    {
        var configured = Environment.GetEnvironmentVariable(variable);
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return Path.GetFullPath(configured);
        if (Path.IsPathFullyQualified(command) && File.Exists(command)) return command;
        var name = OperatingSystem.IsWindows() && !command.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? command + ".exe" : command;
        foreach (var part in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(part)) continue;
            var candidate = Path.Combine(part.Trim('"'), name);
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);
        }
        throw new FileNotFoundException("Required actual executable not found: " + name);
    }
    private static string RepositoryRoot([CallerFilePath] string source = "")
    {
        for (var path = new DirectoryInfo(Path.GetDirectoryName(source)!); path is not null; path = path.Parent)
            if (File.Exists(Path.Combine(path.FullName, "WolfConquer.sln"))) return path.FullName;
        throw new DirectoryNotFoundException("Repository root unavailable.");
    }
    private static Dictionary<string, byte[]> Files(string root) => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
        .ToDictionary(path => Path.GetRelativePath(root, path), File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);
    private static void AssertFiles(string root, Dictionary<string, byte[]> before)
    {
        var after = Files(root);
        Assert.Equal(before.Keys.Order(StringComparer.OrdinalIgnoreCase), after.Keys.Order(StringComparer.OrdinalIgnoreCase));
        foreach (var pair in before) Assert.Equal(pair.Value, after[pair.Key]);
    }
    private static void DeleteOwned(string root, string prefix)
    {
        var full = Path.GetFullPath(root);
        var leaf = Path.GetFileName(full);
        Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), Path.GetDirectoryName(full), ignoreCase: true);
        Assert.StartsWith(prefix, leaf, StringComparison.Ordinal);
        Assert.True(Guid.TryParseExact(leaf[prefix.Length..], "N", out var id) && id != Guid.Empty);
        if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
    }
}
