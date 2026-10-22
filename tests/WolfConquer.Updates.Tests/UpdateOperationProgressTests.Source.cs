using System.Diagnostics;
using WolfConquer.Updates.Progress;
using WolfConquer.Updates.Source;
using Xunit;

namespace WolfConquer.Updates.Tests;

public sealed partial class UpdateOperationProgressTests
{
    [Theory]
    [InlineData("updated", false)]
    [InlineData("updated", true)]
    [InlineData("current", false)]
    [InlineData("current", true)]
    [InlineData("local-changes", false)]
    [InlineData("local-changes", true)]
    public async Task SourceReportsHonestIndeterminatePhasesAndThrowingObserversPreserveSemantics(string status, bool throws)
    {
        using var f = await SourceFixture.Create();
        var before = await f.Git(f.Checkout, "rev-parse", "HEAD");
        if (status == "updated") await f.Advance();
        if (status == "local-changes") File.WriteAllText(Path.Combine(f.Checkout, "version.txt"), "local work");
        var progress = new Observer(throws);
        var result = await new SourceCheckoutUpdater(true).UpdateAsync(f.Checkout, f.Origin, progress: progress);
        Assert.Equal(status, result.Status);
        Assert.Equal(before, result.BeforeCommit);
        Assert.Equal(result.AfterCommit, await f.Git(f.Checkout, "rev-parse", "HEAD"));
        Assert.Equal(UpdateOperationPhase.Checking, progress.Events[0].Phase);
        Assert.Equal(status switch { "updated" => UpdateOperationPhase.Completed, "current" => UpdateOperationPhase.Current,
            _ => UpdateOperationPhase.Skipped }, progress.Events[^1].Phase);
        Assert.All(progress.Events, p =>
        {
            Assert.Null(p.Completed); Assert.Null(p.Total); Assert.Null(p.RelativePath);
            Assert.Equal(UpdateProgressUnit.Steps, p.Unit);
        });
        AssertSafeMessages(progress, f.Root);
        if (status == "updated")
        {
            Assert.Contains(progress.Events, p => p.Phase == UpdateOperationPhase.Fetching);
            Assert.Contains(progress.Events, p => p.Phase == UpdateOperationPhase.FastForwarding);
            Assert.NotEqual(before, result.AfterCommit);
            Assert.Equal("remote update", File.ReadAllText(Path.Combine(f.Checkout, "version.txt")));
        }
        else
        {
            Assert.Equal(before, result.AfterCommit);
            Assert.DoesNotContain(progress.Events, p => p.Phase == UpdateOperationPhase.FastForwarding);
            Assert.Equal(status == "current" ? "initial" : "local work", File.ReadAllText(Path.Combine(f.Checkout, "version.txt")));
        }
    }

    [Fact]
    public async Task RefusedCredentialsAreNeverEchoedIntoProgressEvenWhenObserverThrows()
    {
        var progress = new Observer(throws: true);
        var result = await new SourceCheckoutUpdater().UpdateAsync(Path.GetTempPath(),
            "https://user:secret@github.com/synthetic/source.git", progress: progress);
        Assert.Equal("refused", result.Status);
        Assert.Equal(UpdateOperationPhase.Skipped, progress.Events[^1].Phase);
        AssertSafeMessages(progress, Path.GetTempPath());
        Assert.DoesNotContain(progress.Events, p => p.Phase == UpdateOperationPhase.Fetching);
    }

    private sealed class SourceFixture : IDisposable
    {
        internal string Root { get; } = Directory.CreateTempSubdirectory("wolf progress-source ").FullName;
        internal string Seed => Path.Combine(Root, "seed");
        internal string Checkout => Path.Combine(Root, "checkout");
        private string Remote => Path.Combine(Root, "remote.git");
        internal string Origin { get; private set; } = "";
        internal static async Task<SourceFixture> Create()
        {
            var f = new SourceFixture();
            try
            {
                File.WriteAllText(Path.Combine(f.Root, "empty.config"), "");
                Directory.CreateDirectory(Path.Combine(f.Root, "hooks"));
                await f.Git(f.Root, "init", "--bare", f.Remote);
                await f.Git(f.Root, "init", "--initial-branch=main", f.Seed);
                File.WriteAllText(Path.Combine(f.Seed, "version.txt"), "initial");
                await f.Git(f.Seed, "add", "version.txt");
                await f.Git(f.Seed, "commit", "-m", "Synthetic initial");
                await f.Git(f.Seed, "remote", "add", "origin", f.Remote);
                await f.Git(f.Seed, "push", "origin", "main");
                await f.Git(f.Root, "clone", "--branch", "main", f.Remote, f.Checkout);
                f.Origin = await f.Git(f.Checkout, "config", "--get", "remote.origin.url");
                return f;
            }
            catch { f.Dispose(); throw; }
        }
        internal async Task Advance()
        {
            File.WriteAllText(Path.Combine(Seed, "version.txt"), "remote update");
            await Git(Seed, "add", "version.txt");
            await Git(Seed, "commit", "-m", "Synthetic remote update");
            await Git(Seed, "push", "origin", "main");
        }
        internal async Task<string> Git(string directory, params string[] arguments)
        {
            var start = new ProcessStartInfo("git") { WorkingDirectory = directory, UseShellExecute = false,
                CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var key in start.Environment.Keys.Where(k => k.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToArray())
                start.Environment.Remove(key);
            start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
            start.Environment["GIT_CONFIG_GLOBAL"] = Path.Combine(Root, "empty.config");
            start.Environment["GIT_CONFIG_SYSTEM"] = Path.Combine(Root, "empty.config");
            start.Environment["GIT_TERMINAL_PROMPT"] = "0";
            foreach (var setting in new[] { "core.hooksPath=" + Path.Combine(Root, "hooks"), "core.autocrlf=false",
                "user.name=Synthetic Fixture", "user.email=fixture@example.invalid", "commit.gpgsign=false" })
            { start.ArgumentList.Add("-c"); start.ArgumentList.Add(setting); }
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); throw; }
            await error;
            Assert.Equal(0, process.ExitCode);
            return (await output).TrimEnd('\r', '\n');
        }
        public void Dispose()
        {
            var resolved = Path.GetFullPath(Root);
            Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), resolved, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith("wolf progress-source ", Path.GetFileName(resolved), StringComparison.Ordinal);
            foreach (var file in Directory.EnumerateFiles(resolved, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(resolved, recursive: true);
        }
    }
}
