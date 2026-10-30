using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyGmPolicyConfigurationTests
{
    [Fact]
    public void LeafSymbolicPolicyRefusesWithoutChangingItsTarget() => WithDirectory(root =>
    {
        var target = Path.Combine(root, "target.json");
        var link = Path.Combine(root, "policy.json");
        var bytes = Encoding.UTF8.GetBytes(Document);
        File.WriteAllBytes(target, bytes);
        Exception? primary = null;
        try
        {
            try { File.CreateSymbolicLink(link, target); }
            catch (UnauthorizedAccessException)
            { throw Xunit.Sdk.SkipException.ForSkip("Symbolic-link privilege is unavailable; no host setting changed."); }
            catch (IOException error) when ((error.HResult & 0xffff) == 1314)
            { throw Xunit.Sdk.SkipException.ForSkip("Symbolic-link privilege is unavailable; no host setting changed."); }
            Assert.NotEqual((FileAttributes)0, File.GetAttributes(link) & FileAttributes.ReparsePoint);
            Assert.False(LegacyGmPolicyConfiguration.TryReadFile(link, out var policy, out var reason));
            Assert.Null(policy);
            Assert.Contains("ReparsePathRejected", reason, StringComparison.Ordinal);
            Assert.Equal(bytes, File.ReadAllBytes(target));
        }
        catch (Exception error) { primary = error; }
        CompleteOwnedLinkTest(primary, () => RetireOwnedPolicyLink(root, link, target, directory: false, bytes));
    });

    [Fact]
    public Task RealLinkedAncestorCannotBeAcceptedAsAnOrdinaryPolicyDirectory() => WithDirectoryAsync(async root =>
    {
        var target = Path.Combine(root, "target");
        var link = Path.Combine(root, "linked");
        Directory.CreateDirectory(target);
        var bytes = Encoding.UTF8.GetBytes(Document);
        var path = Path.Combine(target, "policy.json");
        File.WriteAllBytes(path, bytes);
        Exception? primary = null;
        try
        {
            await CreateDirectoryLinkAsync(link, target);
            Assert.NotEqual((FileAttributes)0, File.GetAttributes(link) & FileAttributes.ReparsePoint);
            Assert.False(LegacyGmPolicyConfiguration.TryReadFile(Path.Combine(link, "policy.json"), out var policy, out var reason));
            Assert.Null(policy);
            Assert.Contains("ReparsePathRejected", reason, StringComparison.Ordinal);
            Assert.Equal(bytes, File.ReadAllBytes(path));
            Assert.Equal(new[] { "policy.json" }, Directory.GetFiles(target).Select(Path.GetFileName));
        }
        catch (Exception error) { primary = error; }
        CompleteOwnedLinkTest(primary, () => RetireOwnedPolicyLink(root, link, target, directory: true, bytes));
    });

    [Fact]
    public void ExplicitControlCharactersAndWindowsAlternateStreamsRefuse() => WithDirectory(root =>
    {
        var path = Path.Combine(root, "policy.json");
        File.WriteAllText(path, Document, new UTF8Encoding(false));
        var invalid = new List<string> { path + "\n", path + "\0" };
        if (OperatingSystem.IsWindows()) invalid.Add(path + ":hidden-policy");
        foreach (var input in invalid)
        {
            Assert.False(LegacyGmPolicyConfiguration.TryLoad(input, out var policy, out var reason));
            Assert.Null(policy);
            Assert.NotEmpty(reason);
        }
        Assert.Equal(Encoding.UTF8.GetBytes(Document), File.ReadAllBytes(path));
    });

    private static async Task CreateDirectoryLinkAsync(string link, string target)
    {
        if (!OperatingSystem.IsWindows()) { Directory.CreateSymbolicLink(link, target); return; }
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("cmd.exe")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }
        };
        foreach (var argument in new[] { "/c", "mklink", "/J", link, target }) process.StartInfo.ArgumentList.Add(argument);
        Assert.True(process.Start());
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException primary)
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (Exception cleanup) { throw new AggregateException("Owned junction creation and cleanup failed.", primary, cleanup); }
            throw new TimeoutException("Owned junction creation timed out.", primary);
        }
        Assert.True(process.ExitCode == 0, (await output) + (await error));
    }

    private static void RetireOwnedPolicyLink(string root, string link, string target, bool directory, byte[] sentinel)
    {
        var full = ValidateOwnedDirectoryRoot(root);
        if (!SamePolicyPath(link, Path.Combine(full, directory ? "linked" : "policy.json")) ||
            !SamePolicyPath(target, Path.Combine(full, directory ? "target" : "target.json")) ||
            (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Refusing retirement of an unowned GM policy link.");
        var sentinelPath = directory ? Path.Combine(target, "policy.json") : target;
        Assert.Equal(sentinel, File.ReadAllBytes(sentinelPath));
        FileSystemInfo info = directory ? new DirectoryInfo(link) : new FileInfo(link);
        info.Refresh();
        if (info.Exists || info.LinkTarget is not null)
        {
            if ((info.Attributes & FileAttributes.ReparsePoint) == 0 ||
                info.ResolveLinkTarget(returnFinalTarget: true) is not { } resolved || !SamePolicyPath(resolved.FullName, target))
                throw new InvalidOperationException("Refusing retirement of a changed GM policy link target.");
            if (directory) Directory.Delete(link, recursive: false);
            else File.Delete(link);
            Assert.False(File.Exists(link) || Directory.Exists(link));
        }
        Assert.Equal(sentinel, File.ReadAllBytes(sentinelPath));
    }

    private static void CompleteOwnedLinkTest(Exception? primary, Action cleanup)
    {
        try { cleanup(); }
        catch (Exception error)
        {
            if (primary is not null) throw new AggregateException("GM policy link test and exact link retirement failed.", primary, error);
            throw;
        }
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
    }
}
