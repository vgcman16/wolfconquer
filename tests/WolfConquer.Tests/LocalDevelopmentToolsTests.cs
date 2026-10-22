using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace WolfConquer.Tests;

// Only PS7 plan/status calls over owned synthetic files. Never invokes a nested
// restore/build/test, service process, SQL database, game client or deployment.
public sealed class LocalDevelopmentToolsTests : IDisposable
{
    private const string SolutionMarker = "Synthetic marker; must never be built.";
    private const string LauncherProjectMarker = "<Project><!-- Synthetic plan-only metadata; must never be built or executed. --></Project>";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wolf local-tools ;$ " + Guid.NewGuid().ToString("N"));
    private readonly DateTime _solutionWriteTime;
    private readonly DateTime _launcherWriteTime;
    private string LauncherProject => Path.Combine(_root, "tests", "WolfConquer.Launcher.Tests", "WolfConquer.Launcher.Tests.csproj");

    public LocalDevelopmentToolsTests()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "WolfConquer.sln"), SolutionMarker);
        Directory.CreateDirectory(Path.GetDirectoryName(LauncherProject)!);
        File.WriteAllText(LauncherProject, LauncherProjectMarker);
        _solutionWriteTime = File.GetLastWriteTimeUtc(Path.Combine(_root, "WolfConquer.sln"));
        _launcherWriteTime = File.GetLastWriteTimeUtc(LauncherProject);
    }

    [Theory]
    [InlineData("-Plan")]
    [InlineData("-DryRun")]
    public async Task PlanKeepsSpacedArgumentsSeparateMeasuresProbeAndWritesNothing(string option)
    {
        var pwsh = PowerShellPath();
        var result = await RunAsync("Invoke-LocalCheck.ps1", "-RepositoryRoot", _root, option,
            "-PowerShellPath", pwsh);
        using var json = ParseSuccess(result);
        var plan = json.RootElement;
        Assert.Equal("windows-local-v2", plan.GetProperty("suite").GetString());
        Assert.Equal("plan", plan.GetProperty("mode").GetString());
        Assert.Equal("not-run", plan.GetProperty("outcome").GetString());
        Assert.Equal(_root, plan.GetProperty("repositoryRoot").GetString());
        var commands = plan.GetProperty("commands").EnumerateArray().ToArray();
        Assert.Equal(8, commands.Length);
        Assert.Equal(new[] { "restore", "build", "test", "launcher-restore", "launcher-build", "launcher-test",
            "updater-status-test", "updater-smoke-parser-test" }, commands.Select(value => value.GetProperty("name").GetString()));
        Assert.All(commands.Take(3), command => Assert.Equal(Path.Combine(_root, "WolfConquer.sln"),
            command.GetProperty("arguments")[1].GetString()));
        Assert.All(commands.Skip(3).Take(3), command => Assert.Equal(LauncherProject,
            command.GetProperty("arguments")[1].GetString()));
        Assert.Equal(new[] { "restore", "build", "test", "restore", "build", "test" },
            commands.Take(6).Select(command => command.GetProperty("arguments")[0].GetString()));
        Assert.Contains("--no-restore", commands[1].GetProperty("arguments").EnumerateArray().Select(value => value.GetString()));
        Assert.Contains("--no-build", commands[2].GetProperty("arguments").EnumerateArray().Select(value => value.GetString()));
        Assert.Contains("--no-restore", commands[4].GetProperty("arguments").EnumerateArray().Select(value => value.GetString()));
        Assert.Contains("--no-build", commands[5].GetProperty("arguments").EnumerateArray().Select(value => value.GetString()));
        foreach (var index in new[] { 1, 2, 4, 5 })
            Assert.Equal("Release", commands[index].GetProperty("arguments")[3].GetString());
        foreach (var index in new[] { 2, 5 })
        {
            var arguments = commands[index].GetProperty("arguments").EnumerateArray().Select(value => value.GetString()).ToArray();
            Assert.Contains("--no-restore", arguments);
            Assert.Equal(new[] { "--logger", "trx" }, arguments.Skip(7).Take(2));
        }
        var solutionArguments = commands[2].GetProperty("arguments").EnumerateArray().Select(value => value.GetString()).ToArray();
        Assert.Equal(10, solutionArguments.Length);
        Assert.DoesNotContain("--results-directory", solutionArguments);
        const string isolationProperty = "-p:WolfConquerLocalResultsId=";
        Assert.StartsWith(isolationProperty, solutionArguments[9]!, StringComparison.Ordinal);
        var runId = solutionArguments[9]![isolationProperty.Length..];
        Assert.Matches("^[0-9]{8}T[0-9]{9}Z-[0-9a-f]{8}$", runId);
        Assert.Equal(Path.Combine(_root, ".artifacts", "local-dev", runId + "-solution-results"),
            plan.GetProperty("updaterSmokeSource").GetString());
        Assert.Equal("project/target-framework", plan.GetProperty("solutionResultsLayout").GetString());
        Assert.Equal("--results-directory", commands[5].GetProperty("arguments")[9].GetString());
        Assert.Equal(Path.Combine(_root, ".artifacts", "local-dev", runId + "-launcher-results"),
            commands[5].GetProperty("arguments")[10].GetString());
        Assert.Equal("WolfConquer.Updates.Tests.dll", plan.GetProperty("updaterSmokeAssembly").GetString());
        Assert.Equal(2, commands.Count(command => command.GetProperty("arguments")[0].GetString() == "test"));
        Assert.DoesNotContain(commands, command => command.GetProperty("arguments").EnumerateArray()
            .Any(value => value.GetString()!.EndsWith("WolfConquer.Updates.Tests.csproj", StringComparison.OrdinalIgnoreCase)));
        for (var index = 6; index < 8; index++)
        {
            Assert.Equal(pwsh, commands[index].GetProperty("executable").GetString());
            Assert.Equal(new[] { "-NoProfile", "-File", ScriptPath(index == 6 ? "Test-UpdaterStatus.ps1" : "Test-UpdaterSmokeResult.ps1") },
                commands[index].GetProperty("arguments").EnumerateArray().Select(value => value.GetString()));
        }
        var environment = plan.GetProperty("environment");
        Assert.Equal(pwsh, environment.GetProperty("WOLFCONQUER_NATIVE_CONSOLE_PROBE_PWSH").GetString());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(pwsh))),
            environment.GetProperty("WOLFCONQUER_NATIVE_CONSOLE_PROBE_PWSH_SHA256").GetString());
        AssertPlanFixtureUnchanged();
    }

    [Fact]
    public async Task PlanDoesNotOverwriteAnExistingStatusOrCreateLogs()
    {
        var path = WriteStatus("retained non-JSON sentinel");
        var entries = EntryPaths();
        using var json = ParseSuccess(await RunAsync("Invoke-LocalCheck.ps1", "-RepositoryRoot", _root,
            "-Plan", "-PowerShellPath", PowerShellPath(), "-Configuration", "Debug"));
        Assert.Equal("retained non-JSON sentinel", File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!));
        Assert.Equal(entries, EntryPaths());
        Assert.Contains("Debug", json.RootElement.GetProperty("commands")[1].GetProperty("arguments")
            .EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public async Task DefaultRootIsRelativeToScriptLocationRatherThanWorkingDirectory()
    {
        var scripts = Path.Combine(_root, "tools", "dev");
        Directory.CreateDirectory(scripts);
        var copied = Path.Combine(scripts, "Invoke-LocalCheck.ps1");
        File.Copy(ScriptPath("Invoke-LocalCheck.ps1"), copied);
        var entries = EntryPaths();
        var scriptHash = SHA256.HashData(File.ReadAllBytes(copied));
        using var json = ParseSuccess(await RunPathAsync(copied, "-Plan", "-PowerShellPath", PowerShellPath()));
        Assert.Equal(_root, json.RootElement.GetProperty("repositoryRoot").GetString()!.TrimEnd(Path.DirectorySeparatorChar));
        Assert.False(Directory.Exists(Path.Combine(_root, ".artifacts")));
        Assert.Equal(entries, EntryPaths());
        Assert.Equal(scriptHash, SHA256.HashData(File.ReadAllBytes(copied)));
        Assert.Equal(LauncherProjectMarker, File.ReadAllText(LauncherProject));
    }

    [Fact]
    public async Task MissingSolutionFailsWithoutCreatingArtifactsOrRunningCommands()
    {
        File.Delete(Path.Combine(_root, "WolfConquer.sln"));
        var result = await RunAsync("Invoke-LocalCheck.ps1", "-RepositoryRoot", _root, "-Plan");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("WolfConquer.sln", result.Error);
        AssertPlanFixtureUnchanged(includeSolution: false);
    }

    [Fact]
    public async Task MissingStatusIsUnknownAndReadingItDoesNotCreateAnyArtifact()
    {
        using var json = ParseSuccess(await StatusAsync());
        Assert.Equal("unknown", json.RootElement.GetProperty("lastCheck").GetProperty("state").GetString());
        Assert.Equal("unknown", json.RootElement.GetProperty("services").GetProperty("state").GetString());
        Assert.Empty(json.RootElement.GetProperty("services").GetProperty("items").EnumerateArray());
        AssertPlanFixtureUnchanged();
    }

    [Fact]
    public async Task ExplicitServiceRootDoesNotReplaceTheSavedCheckRepositoryIdentity()
    {
        var services = Path.Combine(_root, "separate service files");
        Directory.CreateDirectory(services);
        WriteStatus(JsonSerializer.Serialize(SavedCheck()));
        using var json = ParseSuccess(await RunAsync("Get-LocalStatus.ps1", "-RepositoryRoot", _root,
            "-ServiceRoot", services, "-SkipServiceProbe"));
        Assert.Equal(_root, json.RootElement.GetProperty("repositoryRoot").GetString());
        Assert.Equal(services, json.RootElement.GetProperty("serviceRoot").GetString());
        Assert.Equal("passed", json.RootElement.GetProperty("lastCheck").GetProperty("state").GetString());
        Assert.Empty(json.RootElement.GetProperty("services").GetProperty("items").EnumerateArray());
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("foreign-root")]
    [InlineData("incomplete-pass")]
    [InlineData("bad-pass-exit")]
    [InlineData("unsafe-log")]
    public async Task InvalidStatusNeverBecomesAGreenResult(string scenario)
    {
        var saved = SavedCheck();
        if (scenario == "foreign-root") saved["repositoryRoot"] = Path.GetTempPath();
        if (scenario == "incomplete-pass") saved["steps"] = Array.Empty<object>();
        if (scenario == "bad-pass-exit") saved["exitCode"] = 41;
        if (scenario == "unsafe-log") ((Dictionary<string, object?>[])saved["steps"]!)[0]["stdoutLog"] = "../../private.log";
        var bytes = scenario == "malformed" ? "{not JSON" : JsonSerializer.Serialize(saved);
        var path = WriteStatus(bytes);

        using var json = ParseSuccess(await StatusAsync());

        Assert.Equal("unknown", json.RootElement.GetProperty("lastCheck").GetProperty("state").GetString());
        Assert.Equal(bytes, File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!));
    }

    [Fact]
    public async Task CompleteSavedResultRetainsRelativeLogReferencesButNotExtraSecretOrLogContents()
    {
        var saved = SavedCheck();
        saved["privateToken"] = "SYNTHETIC_DO_NOT_EXPOSE";
        var bytes = JsonSerializer.Serialize(saved);
        var path = WriteStatus(bytes);
        var result = await StatusAsync();
        using var json = ParseSuccess(result);
        var check = json.RootElement.GetProperty("lastCheck");
        Assert.Equal("passed", check.GetProperty("state").GetString());
        Assert.Equal(0, check.GetProperty("exitCode").GetInt32());
        Assert.Equal(3, check.GetProperty("steps").GetArrayLength());
        Assert.Equal(".artifacts/local-dev/synthetic-test.stdout.log", check.GetProperty("steps")[2].GetProperty("stdoutLog").GetString());
        Assert.DoesNotContain("SYNTHETIC_DO_NOT_EXPOSE", result.Output);
        Assert.Equal(bytes, File.ReadAllText(path));
    }

    [Theory]
    [InlineData("running")]
    [InlineData("failed")]
    public async Task PendingOrFailedSavedCheckIsNotPromotedToPassed(string outcome)
    {
        var saved = SavedCheck();
        saved["outcome"] = outcome;
        saved["exitCode"] = outcome == "failed" ? 37 : null;
        saved["finishedAtUtc"] = null;
        saved["steps"] = Array.Empty<object>();
        WriteStatus(JsonSerializer.Serialize(saved));
        using var json = ParseSuccess(await StatusAsync());
        var check = json.RootElement.GetProperty("lastCheck");
        Assert.Equal(outcome, check.GetProperty("state").GetString());
        if (outcome == "failed") Assert.Equal(37, check.GetProperty("exitCode").GetInt32());
        else Assert.Equal(JsonValueKind.Null, check.GetProperty("exitCode").ValueKind);
    }

    private Dictionary<string, object?> SavedCheck() => new()
    {
        ["schemaVersion"] = 1, ["repositoryRoot"] = _root, ["outcome"] = "passed", ["exitCode"] = 0,
        ["startedAtUtc"] = "2026-01-01T00:00:00Z", ["finishedAtUtc"] = "2026-01-01T00:00:01Z",
        ["steps"] = new[] { "restore", "build", "test" }.Select(name => new Dictionary<string, object?>
        {
            ["name"] = name, ["outcome"] = "passed", ["exitCode"] = 0,
            ["stdoutLog"] = $".artifacts/local-dev/synthetic-{name}.stdout.log",
            ["stderrLog"] = $".artifacts/local-dev/synthetic-{name}.stderr.log",
        }).ToArray(),
    };

    private string[] EntryPaths() => Directory.GetFileSystemEntries(_root, "*", SearchOption.AllDirectories)
        .Select(path => Path.GetRelativePath(_root, path)).Order(StringComparer.Ordinal).ToArray();

    private void AssertPlanFixtureUnchanged(bool includeSolution = true)
    {
        var expected = new List<string> { "tests", Path.Combine("tests", "WolfConquer.Launcher.Tests"),
            Path.GetRelativePath(_root, LauncherProject) };
        if (includeSolution)
        {
            expected.Add("WolfConquer.sln");
            Assert.Equal(SolutionMarker, File.ReadAllText(Path.Combine(_root, "WolfConquer.sln")));
            Assert.Equal(_solutionWriteTime, File.GetLastWriteTimeUtc(Path.Combine(_root, "WolfConquer.sln")));
        }
        Assert.Equal(expected.Order(StringComparer.Ordinal), EntryPaths());
        Assert.Equal(LauncherProjectMarker, File.ReadAllText(LauncherProject));
        Assert.Equal(_launcherWriteTime, File.GetLastWriteTimeUtc(LauncherProject));
    }

    private string WriteStatus(string content)
    {
        var directory = Path.Combine(_root, ".artifacts", "local-dev");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "last-check.json");
        File.WriteAllText(path, content);
        return path;
    }

    private Task<RunResult> StatusAsync() => RunAsync("Get-LocalStatus.ps1", "-RepositoryRoot", _root, "-SkipServiceProbe");
    private Task<RunResult> RunAsync(string script, params string[] arguments) => RunPathAsync(ScriptPath(script), arguments);

    private async Task<RunResult> RunPathAsync(string script, params string[] arguments)
    {
        var start = new ProcessStartInfo(PowerShellPath())
        {
            UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = _root,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (var argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-File", script }.Concat(arguments))
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); throw; }
        return new RunResult(process.ExitCode, await stdout, await stderr);
    }

    private static string PowerShellPath()
    {
        var measured = Environment.GetEnvironmentVariable("WOLFCONQUER_NATIVE_CONSOLE_PROBE_PWSH");
        if (!string.IsNullOrWhiteSpace(measured) && File.Exists(measured)) return Path.GetFullPath(measured);
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            var path = Path.Combine(directory.Trim('"'), OperatingSystem.IsWindows() ? "pwsh.exe" : "pwsh");
            if (File.Exists(path)) return Path.GetFullPath(path);
        }
        throw new FileNotFoundException("PowerShell 7 is required for local-development tool tests.");
    }

    private static string ScriptPath(string name, [CallerFilePath] string source = "")
    {
        for (DirectoryInfo? directory = new(Path.GetDirectoryName(source)!); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln"))) return Path.Combine(directory.FullName, "tools", "dev", name);
        throw new DirectoryNotFoundException("Could not locate the repository tools.");
    }

    private static JsonDocument ParseSuccess(RunResult result)
    {
        Assert.True(result.ExitCode == 0, $"Exit {result.ExitCode}; stderr: {result.Error}; stdout: {result.Output}");
        Assert.Equal("", result.Error);
        return JsonDocument.Parse(result.Output);
    }

    public void Dispose()
    {
        var path = Path.GetFullPath(_root);
        Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), path, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("wolf local-tools ;$ ", Path.GetFileName(path), StringComparison.Ordinal);
        Directory.Delete(path, recursive: true);
    }

    private sealed record RunResult(int ExitCode, string Output, string Error);
}
