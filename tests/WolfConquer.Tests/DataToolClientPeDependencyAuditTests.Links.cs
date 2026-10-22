using System.Diagnostics;
using Xunit.Sdk;

namespace WolfConquer.Tests;

public sealed partial class DataToolClientPeDependencyAuditTests
{
    [Fact]
    public async Task ActualDirectoryJunctionAncestorRefusesWithoutChangingItsTargetPe()
    {
        RequireDependencyLinkWindows();
        var f = new AuditFixture(Image(["Owned.dll"]));
        string target = Directory.CreateDirectory(Path.Combine(f.Root, "ordinary-target")).FullName;
        string targetPe = Path.Combine(target, "fixture.dll");
        File.Copy(f.Path, targetPe);
        byte[] original = File.ReadAllBytes(targetPe); DateTime written = File.GetLastWriteTimeUtc(targetPe);
        string link = Path.Combine(f.Root, "junction-ancestor");
        try
        {
            await CreateDependencyTestJunctionAsync(link, target);
            Assert.True((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0);
            Failure(await f.Run(Path.Combine(link, "fixture.dll")));
            Assert.Equal(original, File.ReadAllBytes(targetPe));
            Assert.Equal(written, File.GetLastWriteTimeUtc(targetPe));
        }
        finally
        {
            RemoveDependencyTestLink(link, target, directory: true);
            Assert.Equal(original, File.ReadAllBytes(targetPe));
            Assert.Equal(written, File.GetLastWriteTimeUtc(targetPe));
            f.Dispose(); // Reached only after exact, nonrecursive link removal; unverified remnants preserve the GUID fixture.
        }
    }

    [Fact]
    public async Task ActualFileSymbolicAliasRefusesWithExistingWindowsPrivilegeOnly()
    {
        RequireDependencyLinkWindows();
        var f = new AuditFixture(Image(delayed: ["OwnedDelay.dll"]));
        string link = Path.Combine(f.Root, "symbolic-alias.exe");
        byte[] original = File.ReadAllBytes(f.Path); DateTime written = File.GetLastWriteTimeUtc(f.Path);
        try
        {
            try { File.CreateSymbolicLink(link, f.Path); }
            catch (UnauthorizedAccessException)
            { throw SkipException.ForSkip("Existing Windows symbolic-link privilege is unavailable; no settings changed."); }
            catch (IOException error) when ((error.HResult & 0xFFFF) == 1314)
            { throw SkipException.ForSkip("Existing Windows symbolic-link privilege is unavailable; no settings changed."); }
            Failure(await f.Run(link));
            Assert.Equal(original, File.ReadAllBytes(f.Path));
            Assert.Equal(written, File.GetLastWriteTimeUtc(f.Path));
        }
        finally
        {
            RemoveDependencyTestLink(link, f.Path, directory: false);
            Assert.Equal(original, File.ReadAllBytes(f.Path));
            Assert.Equal(written, File.GetLastWriteTimeUtc(f.Path));
            f.Dispose();
        }
    }

    private static void RequireDependencyLinkWindows()
    {
        if (!OperatingSystem.IsWindows()) throw SkipException.ForSkip("Actual NTFS junction and Windows symbolic-link controls require Windows.");
    }

    private static async Task CreateDependencyTestJunctionAsync(string link, string target)
    {
        // The existing catalog tests use this same bounded child-process pattern;
        // their helpers are private. No command string, redirection, or deletion is constructed.
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"))
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }
        };
        foreach (string argument in new[] { "/d", "/c", "mklink", "/J", link, target }) process.StartInfo.ArgumentList.Add(argument);
        Assert.True(process.Start(), "Could not start the owned junction-creation helper.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try { await process.WaitForExitAsync(deadline.Token); }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true); await process.WaitForExitAsync();
            Assert.Fail($"Owned junction helper timed out. stdout: {await output} stderr: {await error}");
        }
        Assert.True(process.ExitCode == 0, $"Owned junction creation failed. stdout: {await output} stderr: {await error}");
    }

    private static void RemoveDependencyTestLink(string link, string target, bool directory)
    {
        FileAttributes attributes;
        try { attributes = File.GetAttributes(link); }
        catch (FileNotFoundException) { return; }
        catch (DirectoryNotFoundException) { return; }
        Assert.True((attributes & FileAttributes.ReparsePoint) != 0, "Refusing removal of an ordinary or replaced link entry.");
        Assert.Equal(directory, (attributes & FileAttributes.Directory) != 0);
        FileSystemInfo entry = directory ? new DirectoryInfo(link) : new FileInfo(link);
        string actual = entry.LinkTarget ?? throw new IOException("Owned reparse target identity is unavailable; fixture retained.");
        if (actual.StartsWith(@"\??\", StringComparison.Ordinal) || actual.StartsWith(@"\\?\", StringComparison.Ordinal)) actual = actual[4..];
        Assert.Equal(Path.GetFullPath(target), Path.GetFullPath(actual), ignoreCase: true);
        if (directory) Directory.Delete(link, recursive: false); else File.Delete(link);
    }
}
