using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace WolfConquer.Tests;

// Only PS7 plan/status calls over owned synthetic files. Never invokes a nested
// restore/build/test, service process, SQL database, game client or deployment.
public sealed class LocalDevelopmentToolsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wolf local-tools ;$ " + Guid.NewGuid().ToString("N"));

    public LocalDevelopmentToolsTests()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "WolfConquer.sln"), "Synthetic marker; must never be built.");
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
        Assert.Equal("plan", plan.GetProperty("mode").GetString());
        Assert.Equal("not-run", plan.GetProperty("outcome").GetString());
        Assert.Equal(_root, plan.GetProperty("repositoryRoot").GetString());
        var commands = plan.GetProperty("commands").EnumerateArray().ToArray();
        Assert.Equal(new[] { "restore", "build", "test" }, commands.Select(value => value.GetProperty("name").GetString()));
        Assert.All(commands, command => Assert.Equal(Path.Combine(_root, "WolfConquer.sln"),
            command.GetProperty("arguments")[1].GetString()));
        Assert.Contains("--no-restore", commands[1].GetProperty("arguments").EnumerateArray().Select(value => value.GetString()));
        Assert.Contains("--no-build", commands[2].GetProperty("arguments").EnumerateArray().Select(value => value.GetString()));
        var environment = plan.GetProperty("environment");
        Assert.Equal(pwsh, environment.GetProperty("WOLFCONQUER_NATIVE_CONSOLE_PROBE_PWSH").GetString());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(pwsh))),
            environment.GetProperty("WOLFCONQUER_NATIVE_CONSOLE_PROBE_PWSH_SHA256").GetString());
        Assert.Single(Directory.GetFileSystemEntries(_root));
    }

    [Fact]
    public async Task PlanDoesNotOverwriteAnExistingStatusOrCreateLogs()
    {
        var path = WriteStatus("retained non-JSON sentinel");
        using var json = ParseSuccess(await RunAsync("Invoke-LocalCheck.ps1", "-RepositoryRoot", _root,
            "-Plan", "-PowerShellPath", PowerShellPath(), "-Configuration", "Debug"));
        Assert.Equal("retained non-JSON sentinel", File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!));
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
        using var json = ParseSuccess(await RunPathAsync(copied, "-Plan", "-PowerShellPath", PowerShellPath()));
        Assert.Equal(_root, json.RootElement.GetProperty("repositoryRoot").GetString()!.TrimEnd(Path.DirectorySeparatorChar));
        Assert.False(Directory.Exists(Path.Combine(_root, ".artifacts")));
    }

    [Fact]
    public async Task MissingSolutionFailsWithoutCreatingArtifactsOrRunningCommands()
    {
        File.Delete(Path.Combine(_root, "WolfConquer.sln"));
        var result = await RunAsync("Invoke-LocalCheck.ps1", "-RepositoryRoot", _root, "-Plan");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("WolfConquer.sln", result.Error);
        Assert.Empty(Directory.GetFileSystemEntries(_root));
    }

    [Fact]
    public async Task MissingStatusIsUnknownAndReadingItDoesNotCreateAnyArtifact()
    {
        using var json = ParseSuccess(await StatusAsync());
        Assert.Equal("unknown", json.RootElement.GetProperty("lastCheck").GetProperty("state").GetString());
        Assert.Equal("unknown", json.RootElement.GetProperty("services").GetProperty("state").GetString());
        Assert.Empty(json.RootElement.GetProperty("services").GetProperty("items").EnumerateArray());
        Assert.Single(Directory.GetFileSystemEntries(_root));
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
