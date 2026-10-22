using System.Diagnostics;
using WolfConquer.Updates.Source;
using Xunit;

namespace WolfConquer.Updates.Tests;

// Real Git over GUID-owned local repositories only; no GitHub writes or service processes.
public sealed class SourceCheckoutUpdaterTests
{
    [Fact]
    public async Task AlreadyCanceledRequestStopsBeforeAnyGitOperation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new SourceCheckoutUpdater()
            .UpdateAsync(Path.GetTempPath(), "https://github.com/synthetic/repository.git", cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task CleanCheckoutFastForwardsWithoutHooksOrIgnoredPayloadChanges()
    {
        using var fixture = await Fixture.CreateAsync();
        var before = await fixture.HeadAsync();
        var marker = Path.Combine(fixture.Root, "hook-ran");
        File.WriteAllText(Path.Combine(fixture.Checkout, ".git", "hooks", "post-merge"),
            "#!/bin/sh\necho unwanted > '" + marker.Replace('\\', '/') + "'\n");
        var payload = Path.Combine(fixture.Checkout, "payload", "client.dat");
        Directory.CreateDirectory(Path.GetDirectoryName(payload)!);
        File.WriteAllBytes(payload, [0, 0xFE, 0xFF, 7]);
        var target = await fixture.AdvanceRemoteAsync();

        var result = await fixture.UpdateAsync();

        Assert.Equal("updated", result.Status);
        Assert.Equal(before, result.BeforeCommit);
        Assert.Equal(target, result.AfterCommit);
        Assert.Equal(target, await fixture.HeadAsync());
        Assert.Equal("remote update", File.ReadAllText(Path.Combine(fixture.Checkout, "version.txt")));
        Assert.Equal(new byte[] { 0, 0xFE, 0xFF, 7 }, File.ReadAllBytes(payload));
        Assert.False(File.Exists(marker));
    }

    [Fact]
    public async Task MatchingBranchReportsCurrentWithoutChangingFiles()
    {
        using var fixture = await Fixture.CreateAsync();
        var before = await fixture.HeadAsync();
        var result = await fixture.UpdateAsync();
        Assert.Equal("current", result.Status);
        Assert.Equal(before, result.BeforeCommit);
        Assert.Equal(before, result.AfterCommit);
        Assert.Equal("initial", File.ReadAllText(Path.Combine(fixture.Checkout, "version.txt")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TrackedAndUntrackedLocalChangesArePreservedWithoutFetchingOverThem(bool untracked)
    {
        using var fixture = await Fixture.CreateAsync();
        var before = await fixture.HeadAsync();
        var path = Path.Combine(fixture.Checkout, untracked ? "untracked.txt" : "version.txt");
        File.WriteAllText(path, "local work");
        await fixture.AdvanceRemoteAsync();
        var result = await fixture.UpdateAsync();
        Assert.Equal("local-changes", result.Status);
        Assert.Equal(before, result.AfterCommit);
        Assert.Equal(before, await fixture.HeadAsync());
        Assert.Equal("local work", File.ReadAllText(path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AheadOrDivergentLocalCommitsAreNeverRebasedOrReset(bool diverged)
    {
        using var fixture = await Fixture.CreateAsync();
        File.WriteAllText(Path.Combine(fixture.Checkout, "local.txt"), "local committed work");
        await fixture.GitAsync(fixture.Checkout, "add", "local.txt");
        await fixture.GitAsync(fixture.Checkout, "commit", "-m", "Synthetic local commit");
        var before = await fixture.HeadAsync();
        if (diverged) await fixture.AdvanceRemoteAsync();
        var result = await fixture.UpdateAsync();
        Assert.Equal("local-commits", result.Status);
        Assert.Equal(before, result.BeforeCommit);
        Assert.Equal(before, result.AfterCommit);
        Assert.Equal(before, await fixture.HeadAsync());
        Assert.Equal("local committed work", File.ReadAllText(Path.Combine(fixture.Checkout, "local.txt")));
    }

    [Fact]
    public async Task WrongRemoteIsRefusedWithoutEchoingTheUntrustedUrl()
    {
        using var fixture = await Fixture.CreateAsync();
        var result = await new SourceCheckoutUpdater(true).UpdateAsync(fixture.Checkout, fixture.Remote + ".other");
        Assert.Equal("refused", result.Status);
        Assert.DoesNotContain(fixture.Remote, result.Message);
        Assert.Equal(result.BeforeCommit, await fixture.HeadAsync());
    }

    [Fact]
    public async Task WrongBranchOrNestedWorktreePathIsRefused()
    {
        using var fixture = await Fixture.CreateAsync();
        await fixture.GitAsync(fixture.Checkout, "checkout", "-b", "local-work");
        Assert.Equal("refused", (await fixture.UpdateAsync()).Status);
        var nested = Directory.CreateDirectory(Path.Combine(fixture.Checkout, "nested")).FullName;
        var result = await new SourceCheckoutUpdater(true).UpdateAsync(nested, fixture.Origin);
        Assert.Equal("refused", result.Status);
        Assert.Equal("local-work", await fixture.GitAsync(fixture.Checkout, "branch", "--show-current"));
    }

    [Theory]
    [InlineData("multiple-urls")]
    [InlineData("rewrite")]
    [InlineData("filter")]
    public async Task RemoteOrExecutableConfigurationOverridesAreRefused(string scenario)
    {
        using var fixture = await Fixture.CreateAsync();
        if (scenario == "multiple-urls")
            await fixture.GitAsync(fixture.Checkout, "config", "--add", "remote.origin.url", fixture.Remote + ".other");
        else if (scenario == "rewrite")
            await fixture.GitAsync(fixture.Checkout, "config", "url.https://untrusted.invalid/.insteadOf", fixture.Origin);
        else
            await fixture.GitAsync(fixture.Checkout, "config", "filter.synthetic.process", "must-not-execute");
        var before = await fixture.HeadAsync();
        var result = await fixture.UpdateAsync();
        Assert.Equal("refused", result.Status);
        Assert.Equal(before, await fixture.HeadAsync());
    }

    [Theory]
    [InlineData("MERGE_HEAD")]
    [InlineData("CHERRY_PICK_HEAD")]
    [InlineData("rebase-merge")]
    public async Task UnfinishedGitOperationsAreRefusedWithoutRemovingTheirState(string name)
    {
        using var fixture = await Fixture.CreateAsync();
        var state = Path.Combine(fixture.Checkout, ".git", name);
        if (name == "rebase-merge") Directory.CreateDirectory(state);
        else File.WriteAllText(state, await fixture.HeadAsync());
        Assert.Equal("refused", (await fixture.UpdateAsync()).Status);
        Assert.True(File.Exists(state) || Directory.Exists(state));
    }

    [Fact]
    public async Task GitlinkIndexEntriesRefuseSubmoduleUpdates()
    {
        using var fixture = await Fixture.CreateAsync();
        var before = await fixture.HeadAsync();
        await fixture.GitAsync(fixture.Checkout, "update-index", "--add", "--cacheinfo", "160000", before, "synthetic-module");
        Assert.Equal("refused", (await fixture.UpdateAsync()).Status);
        Assert.Contains("160000", await fixture.GitAsync(fixture.Checkout, "ls-files", "--stage"));
        Assert.Equal(before, await fixture.HeadAsync());
    }

    [Fact]
    public async Task IncomingGitlinkIsRefusedBeforeWorktreePublication()
    {
        using var fixture = await Fixture.CreateAsync();
        var before = await fixture.HeadAsync();
        await fixture.GitAsync(fixture.Seed, "update-index", "--add", "--cacheinfo", "160000", before, "synthetic-module");
        await fixture.GitAsync(fixture.Seed, "commit", "-m", "Synthetic incoming gitlink");
        await fixture.GitAsync(fixture.Seed, "push", "origin", "main");
        Assert.Equal("refused", (await fixture.UpdateAsync()).Status);
        Assert.Equal(before, await fixture.HeadAsync());
        Assert.False(Directory.Exists(Path.Combine(fixture.Checkout, "synthetic-module")));
    }

    [Theory]
    [InlineData("--assume-unchanged")]
    [InlineData("--skip-worktree")]
    public async Task HiddenIndexChangesAreRefusedWithoutClearingTheBitsOrOverwritingFiles(string flag)
    {
        using var fixture = await Fixture.CreateAsync();
        var before = await fixture.HeadAsync();
        await fixture.GitAsync(fixture.Checkout, "update-index", flag, "version.txt");
        File.WriteAllText(Path.Combine(fixture.Checkout, "version.txt"), "hidden local work");
        await fixture.AdvanceRemoteAsync();
        Assert.Equal("refused", (await fixture.UpdateAsync()).Status);
        Assert.Equal(before, await fixture.HeadAsync());
        Assert.Equal("hidden local work", File.ReadAllText(Path.Combine(fixture.Checkout, "version.txt")));
        Assert.StartsWith(flag == "--assume-unchanged" ? "h " : "S ",
            await fixture.GitAsync(fixture.Checkout, "ls-files", "-v", "version.txt"));
    }

    [Fact]
    public async Task IncomingTrackedFileCannotOverwriteAnIgnoredLocalPayload()
    {
        using var fixture = await Fixture.CreateAsync();
        var before = await fixture.HeadAsync();
        foreach (var root in new[] { fixture.Checkout, fixture.Seed }) Directory.CreateDirectory(Path.Combine(root, "payload"));
        var local = Path.Combine(fixture.Checkout, "payload", "client.dat");
        File.WriteAllText(local, "local private payload");
        File.WriteAllText(Path.Combine(fixture.Seed, "payload", "client.dat"), "incoming source collision");
        await fixture.GitAsync(fixture.Seed, "add", "-f", "payload/client.dat");
        await fixture.GitAsync(fixture.Seed, "commit", "-m", "Synthetic collision");
        await fixture.GitAsync(fixture.Seed, "push", "origin", "main");
        var result = await fixture.UpdateAsync();
        Assert.Equal("refused", result.Status);
        Assert.Equal(before, await fixture.HeadAsync());
        Assert.Equal("local private payload", File.ReadAllText(local));
    }

    [Theory]
    [InlineData("http://github.com/owner/repo.git")]
    [InlineData("https://user:secret@github.com/owner/repo.git")]
    [InlineData("https://localhost/repo.git")]
    [InlineData("https://127.0.0.1/repo.git")]
    [InlineData("https://192.168.1.1/repo.git")]
    [InlineData("https://github.com/owner/repo.git?token=secret")]
    public async Task ProductionApiRefusesUnsafeRemotesBeforeGitRuns(string remote)
    {
        var result = await new SourceCheckoutUpdater().UpdateAsync(Path.GetTempPath(), remote);
        Assert.Equal("refused", result.Status);
        Assert.Equal("", result.BeforeCommit);
        Assert.DoesNotContain("secret", result.Message);
    }

    private sealed class Fixture : IDisposable
    {
        internal string Root { get; } = Directory.CreateTempSubdirectory("wolf source-fixture ").FullName;
        internal string Remote => Path.Combine(Root, "remote.git");
        internal string Seed => Path.Combine(Root, "seed");
        internal string Checkout => Path.Combine(Root, "checkout");
        internal string Origin { get; private set; } = "";
        private string EmptyConfig => Path.Combine(Root, "empty.config");

        internal static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            File.WriteAllText(fixture.EmptyConfig, "");
            try
            {
                await fixture.GitAsync(fixture.Root, "init", "--bare", fixture.Remote);
                await fixture.GitAsync(fixture.Root, "init", "--initial-branch=main", fixture.Seed);
                await fixture.ConfigureAsync(fixture.Seed);
                File.WriteAllText(Path.Combine(fixture.Seed, "version.txt"), "initial");
                File.WriteAllText(Path.Combine(fixture.Seed, ".gitignore"), "payload/\n");
                await fixture.GitAsync(fixture.Seed, "add", ".");
                await fixture.GitAsync(fixture.Seed, "commit", "-m", "Synthetic initial commit");
                await fixture.GitAsync(fixture.Seed, "remote", "add", "origin", fixture.Remote);
                await fixture.GitAsync(fixture.Seed, "push", "origin", "main");
                await fixture.GitAsync(fixture.Root, "clone", "--branch", "main", fixture.Remote, fixture.Checkout);
                await fixture.ConfigureAsync(fixture.Checkout);
                fixture.Origin = await fixture.GitAsync(fixture.Checkout, "config", "--get", "remote.origin.url");
                return fixture;
            }
            catch { fixture.Dispose(); throw; }
        }

        private async Task ConfigureAsync(string root)
        {
            await GitAsync(root, "config", "user.name", "Synthetic Fixture");
            await GitAsync(root, "config", "user.email", "fixture@example.invalid");
            await GitAsync(root, "config", "commit.gpgsign", "false");
        }

        internal Task<SourceUpdateResult> UpdateAsync() => new SourceCheckoutUpdater(true).UpdateAsync(Checkout, Origin);
        internal Task<string> HeadAsync() => GitAsync(Checkout, "rev-parse", "HEAD");
        internal async Task<string> AdvanceRemoteAsync()
        {
            File.WriteAllText(Path.Combine(Seed, "version.txt"), "remote update");
            await GitAsync(Seed, "add", "version.txt");
            await GitAsync(Seed, "commit", "-m", "Synthetic remote update");
            await GitAsync(Seed, "push", "origin", "main");
            return await GitAsync(Seed, "rev-parse", "HEAD");
        }

        internal async Task<string> GitAsync(string directory, params string[] arguments)
        {
            var start = new ProcessStartInfo("git") { WorkingDirectory = directory, UseShellExecute = false,
                CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var key in start.Environment.Keys.Where(key => key.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToArray())
                start.Environment.Remove(key);
            start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
            start.Environment["GIT_CONFIG_GLOBAL"] = EmptyConfig;
            start.Environment["GIT_CONFIG_SYSTEM"] = EmptyConfig;
            start.Environment["GIT_TERMINAL_PROMPT"] = "0";
            start.ArgumentList.Add("-c"); start.ArgumentList.Add("core.autocrlf=false");
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); throw; }
            var output = await stdout;
            Assert.True(process.ExitCode == 0, $"Fixture Git {arguments[0]} failed: {await stderr}");
            return output.TrimEnd('\r', '\n');
        }

        public void Dispose()
        {
            var resolved = Path.GetFullPath(Root);
            Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), resolved, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith("wolf source-fixture ", Path.GetFileName(resolved), StringComparison.Ordinal);
            foreach (var path in Directory.EnumerateFiles(resolved, "*", SearchOption.AllDirectories)) File.SetAttributes(path, FileAttributes.Normal);
            Directory.Delete(resolved, recursive: true);
        }
    }
}
