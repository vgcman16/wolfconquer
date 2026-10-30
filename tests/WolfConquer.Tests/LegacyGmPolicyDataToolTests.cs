using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyGmPolicyDataToolTests
{
    [Theory]
    [InlineData(null, 2)]
    [InlineData("both", 2)]
    [InlineData("inspect", 1)]
    [InlineData("mail", 1)]
    public async Task ActualCommandCreatesOnlyReviewedPolicyFromExistingDomainAndSavedIdentity(string? selection, int count)
    {
        await WithFixtureAsync(async f =>
        {
            var before = f.Files(); var output = Path.Combine(f.Root, "gm-policy.json");
            var args = Arguments(f, output).ToList(); if (selection is not null) args.Add(selection);
            var result = await RunAsync(args.ToArray());
            Assert.Equal(0, result.Code); Assert.Empty(result.Error);
            using var report = JsonDocument.Parse(result.Output);
            Assert.True(report.RootElement.GetProperty("policyCreated").GetBoolean());
            Assert.False(report.RootElement.GetProperty("activated").GetBoolean());
            var bytes = File.ReadAllBytes(output);
            Assert.False(bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }));
            var policy = LegacyGmPolicy.Parse(bytes);
            Assert.True(LegacyOwnedAdmissionOptions.TryReadFile(f.ConfigPath, out var options, out _));
            Assert.Equal(options!.ExpectedDomain, policy.ExpectedDomain);
            var grant = Assert.Single(policy.Grants); Assert.Equal(LegacyOwnedAdmissionRuntimeFixture.Uid, grant.AccountId);
            Assert.Equal(LegacyOwnedAdmissionRuntimeFixture.Uid, grant.CharacterUid); Assert.Equal(count, grant.Capabilities.Count);
            Assert.Equal(selection != "mail", grant.Capabilities.Contains(LegacyGmCapability.InspectSelf));
            Assert.Equal(selection != "inspect", grant.Capabilities.Contains(LegacyGmCapability.MailTestKitSelf));
            var after = f.Files(); Assert.True(after.Remove("gm-policy.json")); AssertImages(before, after);
            var repeated = await RunAsync(args.ToArray());
            Assert.Equal(1, repeated.Code); Assert.Equal(bytes, File.ReadAllBytes(output));
        });
    }

    [Theory]
    [InlineData(3, "0")]
    [InlineData(3, "-1")]
    [InlineData(3, "4294967296")]
    [InlineData(3, "[GM]Fixture")]
    [InlineData(4, "0")]
    [InlineData(4, "-1")]
    [InlineData(4, "4294967296")]
    [InlineData(4, "2399701")]
    public async Task InvalidOrWrongNumericIdentityRefusesWithoutSourceChanges(int index, string value)
    {
        await WithFixtureAsync(async f =>
        {
            var before = f.Files(); var output = Path.Combine(f.Root, "gm-policy.json");
            var args = Arguments(f, output); args[index] = value;
            var result = await RunAsync(args); Assert.Equal(1, result.Code);
            Assert.False(File.Exists(output)); f.AssertFiles(before);
        });
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("Inspect")]
    [InlineData("")]
    public async Task UnknownCapabilityRefusesBeforeOutput(string capability)
    {
        await WithFixtureAsync(async f =>
        {
            var before = f.Files(); var output = Path.Combine(f.Root, "gm-policy.json");
            var result = await RunAsync(Arguments(f, output).Append(capability).ToArray());
            Assert.Equal(1, result.Code); Assert.False(File.Exists(output)); f.AssertFiles(before);
        });
    }

    [Theory]
    [InlineData("missing-config")]
    [InlineData("missing-account")]
    [InlineData("duplicate-uid")]
    [InlineData("duplicate-section")]
    [InlineData("malformed-character")]
    [InlineData("oversized-character")]
    public async Task MissingAmbiguousOrMalformedRequiredSavedStateRefuses(string fault)
    {
        await WithFixtureAsync(async f =>
        {
            if (fault == "missing-config") File.Delete(f.ConfigPath);
            if (fault == "missing-account") File.Delete(f.UserPath);
            if (fault == "duplicate-uid") File.WriteAllText(f.UserPath,
                File.ReadAllText(f.UserPath).Replace("Name=", $"UID={LegacyOwnedAdmissionRuntimeFixture.Uid}\r\nName=", StringComparison.Ordinal));
            if (fault == "duplicate-section") File.AppendAllText(f.UserPath, "\r\n[Character]\r\nUID=2399700\r\n");
            if (fault == "malformed-character") File.WriteAllText(f.UserPath, "[Character]\r\nUID=2399700\r\n");
            if (fault == "oversized-character")
            {
                using var file = new FileStream(f.UserPath, FileMode.Open, FileAccess.Write, FileShare.None);
                file.SetLength(16 * 1024 * 1024 + 1L);
            }
            var before = f.Files(); var output = Path.Combine(f.Root, "gm-policy.json");
            var result = await RunAsync(Arguments(f, output)); Assert.Equal(1, result.Code);
            Assert.False(File.Exists(output)); f.AssertFiles(before);
        });
    }

    [Theory]
    [InlineData("relative.json")]
    [InlineData("wrong-extension")]
    [InlineData("existing-file")]
    [InlineData("existing-directory")]
    [InlineData("missing-parent")]
    public async Task OutputMustBeAnOrdinaryNewAbsoluteJsonFile(string kind)
    {
        await WithFixtureAsync(async f =>
        {
            var output = kind switch
            {
                "relative.json" => "relative.json",
                "wrong-extension" => Path.Combine(f.Root, "policy.txt"),
                "existing-file" => f.ConfigPath,
                "existing-directory" => Path.Combine(f.Root, "directory.json"),
                _ => Path.Combine(f.Root, "missing", "gm-policy.json")
            };
            if (kind == "existing-directory") Directory.CreateDirectory(output);
            var before = f.Files(); var result = await RunAsync(Arguments(f, output));
            Assert.Equal(1, result.Code); f.AssertFiles(before);
        });
    }

    [Fact]
    public async Task HelpDocumentsRegisteredInertPolicyCommand()
    {
        var help = await RunAsync("help"); Assert.Equal(0, help.Code);
        Assert.Contains("WolfConquer.DataTool create-gm-policy", help.Output, StringComparison.Ordinal);
        Assert.Contains("creates inert policy only", help.Output, StringComparison.Ordinal);
        var missing = await RunAsync("create-gm-policy"); Assert.Equal(1, missing.Code);
    }

    private static string[] Arguments(LegacyOwnedAdmissionRuntimeFixture f, string output) =>
        ["create-gm-policy", output, f.ConfigPath, "2399700", "2399700"];

    private static void AssertImages(Dictionary<string, byte[]> before, Dictionary<string, byte[]> after)
    {
        Assert.Equal(before.Keys.Order(StringComparer.OrdinalIgnoreCase), after.Keys.Order(StringComparer.OrdinalIgnoreCase));
        foreach (var (path, bytes) in before) Assert.Equal(bytes, after[path]);
    }

    private static async Task WithFixtureAsync(Func<LegacyOwnedAdmissionRuntimeFixture, Task> body)
    {
        LegacyOwnedAdmissionRuntimeFixture? fixture = null; Exception? primary = null; Exception? cleanup = null;
        try
        {
            fixture = new(bootstrap: false);
            fixture.WriteConfiguration(fixture.Options() with { ExpectedDomain = Guid.NewGuid() });
            await body(fixture);
            Assert.False(Directory.Exists(fixture.Registry));
        }
        catch (Exception error) { primary = error; }
        if (fixture is not null) try { fixture.Dispose(); } catch (Exception error) { cleanup = error; }
        if (primary is not null && cleanup is not null) throw new AggregateException(primary, cleanup);
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        if (cleanup is not null) ExceptionDispatchInfo.Capture(cleanup).Throw();
    }

    private static async Task<(int Code, string Output, string Error)> RunAsync(params string[] args)
    {
        using var process = TestProjectProcess.Create(FindRoot(), "WolfConquer.DataTool");
        foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
        Assert.True(process.Start());
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException primary)
        {
            try { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
            catch (Exception cleanup) { throw new AggregateException(primary, cleanup); }
            throw new TimeoutException("Owned DataTool command exceeded its deadline.", primary);
        }
        return (process.ExitCode, await output, await error);
    }

    private static string FindRoot([CallerFilePath] string source = "")
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory, Path.GetDirectoryName(source)! })
            for (DirectoryInfo? folder = new(start); folder is not null; folder = folder.Parent)
                if (File.Exists(Path.Combine(folder.FullName, "WolfConquer.sln"))) return folder.FullName;
        throw new DirectoryNotFoundException("Could not locate the built owned test solution.");
    }
}
