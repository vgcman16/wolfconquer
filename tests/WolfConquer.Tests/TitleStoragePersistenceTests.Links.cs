using System.Diagnostics;
using WolfConquer.Core.World;
using Xunit.Sdk;

namespace WolfConquer.Tests;

public sealed partial class TitleStoragePersistenceTests
{
    [WindowsSecondaryPasswordPersistenceFact]
    public async Task ActualJunctionAncestorRejectsWithoutChangingTheOwnedTarget()
    {
        var fixture = new TitlePersistenceFileFixture();
        var link = Path.Combine(fixture.Root, "owned-users-junction");
        await TitleStorageLinkTestFailures.RunAsync(async () =>
        {
            var original = fixture.Seed();
            var before = File.ReadAllBytes(fixture.PathName);
            var written = File.GetLastWriteTimeUtc(fixture.PathName);
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"))
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }
            };
            foreach (var argument in new[] { "/d", "/c", "mklink", "/J", link, fixture.Root })
                process.StartInfo.ArgumentList.Add(argument);
            Assert.True(process.Start());
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            try { await process.WaitForExitAsync(deadline.Token); }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                Assert.Fail("Owned junction creation exceeded its bounded deadline.");
            }
            Assert.True(process.ExitCode == 0, "Owned junction creation failed: " + await stdout + await stderr);
            Assert.True((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0);
            original = original with { UsersRoot = link };
            var updated = TitlePersistenceFileFixture.Updated(original);
            Assert.False(LegacyTitleStoragePersistence.TryPersist(original, updated,
                TitlePersistenceFileFixture.Values(updated), out var reason));
            Assert.Equal("titleStorageReparsePathRejected", reason);
            Assert.Equal(before, File.ReadAllBytes(fixture.PathName));
            Assert.Equal(written, File.GetLastWriteTimeUtc(fixture.PathName));
        }, () => RemoveOwnedLink(link, fixture.Root, directory: true), fixture.Dispose);
    }

    [WindowsSecondaryPasswordPersistenceFact]
    public async Task ActualSymbolicFileRejectsWithExistingPrivilegeOnly()
    {
        var fixture = new TitlePersistenceFileFixture();
        var aliasRoot = Path.Combine(fixture.Root, "owned-file-alias");
        var link = Path.Combine(aliasRoot, Path.GetFileName(fixture.PathName));
        await TitleStorageLinkTestFailures.RunAsync(() =>
        {
            var original = fixture.Seed();
            Directory.CreateDirectory(aliasRoot);
            var before = File.ReadAllBytes(fixture.PathName);
            var written = File.GetLastWriteTimeUtc(fixture.PathName);
            try { File.CreateSymbolicLink(link, fixture.PathName); }
            catch (UnauthorizedAccessException)
            { throw SkipException.ForSkip("Existing symbolic-link privilege unavailable; no settings changed."); }
            catch (IOException error) when ((error.HResult & 0xFFFF) == 1314)
            { throw SkipException.ForSkip("Existing symbolic-link privilege unavailable; no settings changed."); }
            original = original with { UsersRoot = aliasRoot };
            var updated = TitlePersistenceFileFixture.Updated(original);
            Assert.False(LegacyTitleStoragePersistence.TryPersist(original, updated,
                TitlePersistenceFileFixture.Values(updated), out var reason));
            Assert.Equal("titleStorageReparsePathRejected", reason);
            Assert.Equal(before, File.ReadAllBytes(fixture.PathName));
            Assert.Equal(written, File.GetLastWriteTimeUtc(fixture.PathName));
            return Task.CompletedTask;
        }, () => RemoveOwnedLink(link, fixture.PathName, directory: false),
            () => RemoveOwnedAliasDirectory(aliasRoot), fixture.Dispose);
    }

    private static void RemoveOwnedAliasDirectory(string aliasRoot)
    {
        FileAttributes attributes;
        try { attributes = File.GetAttributes(aliasRoot); }
        catch (FileNotFoundException) { return; }
        catch (DirectoryNotFoundException) { return; }
        if ((attributes & FileAttributes.ReparsePoint) != 0 ||
            Directory.GetFileSystemEntries(aliasRoot).Length != 0)
            throw new IOException("Changed alias directory preserved.");
        Directory.Delete(aliasRoot, recursive: false);
    }

    private static void RemoveOwnedLink(string link, string target, bool directory)
    {
        FileAttributes attributes;
        try { attributes = File.GetAttributes(link); }
        catch (FileNotFoundException) { return; }
        catch (DirectoryNotFoundException) { return; }
        if ((attributes & FileAttributes.ReparsePoint) == 0 ||
            ((attributes & FileAttributes.Directory) != 0) != directory)
            throw new IOException("Refusing cleanup of changed owned link.");
        FileSystemInfo entry = directory ? new DirectoryInfo(link) : new FileInfo(link);
        var actual = entry.LinkTarget ?? throw new IOException("Link identity unavailable; preserved.");
        if (actual.StartsWith(@"\??\", StringComparison.Ordinal) || actual.StartsWith(@"\\?\", StringComparison.Ordinal))
            actual = actual[4..];
        if (!string.Equals(Path.GetFullPath(actual), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
            throw new IOException("Changed link target preserved.");
        if (directory) Directory.Delete(link, recursive: false); else File.Delete(link);
    }
}
