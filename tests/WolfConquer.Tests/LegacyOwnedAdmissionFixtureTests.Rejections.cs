using System.Diagnostics;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedAdmissionFixtureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingTargetOrStagingNeverBecomesOwnedOrOverwritten(bool staging)
    {
        var existing = staging ? Staging : Destination;
        Directory.CreateDirectory(existing);
        File.WriteAllBytes(Path.Combine(existing, "preserve.bin"), [0, 255, 9, 7]);
        var files = Files(parent);

        Assert.False(LegacyOwnedAdmissionFixture.TryCreate(Destination, AccountUid, AccountUid, ItemUid, out var result, out var reason));

        Assert.Null(result);
        Assert.NotEmpty(reason);
        AssertFiles(parent, files);
        if (staging) Assert.False(Directory.Exists(Destination));
        else Assert.False(Directory.Exists(Staging));
    }

    [Fact]
    public void ExistingTargetFileIsNotReplacedByAnOfflineFixture()
    {
        File.WriteAllBytes(Destination, [3, 4, 5]);
        var files = Files(parent);

        Assert.False(LegacyOwnedAdmissionFixture.TryCreate(Destination, AccountUid, AccountUid, ItemUid, out var result, out var reason));

        Assert.Null(result);
        Assert.NotEmpty(reason);
        AssertFiles(parent, files);
        Assert.False(Directory.Exists(Staging));
    }

    [Theory]
    [InlineData(0u, 0u, ItemUid)]
    [InlineData(AccountUid, AccountUid + 1, ItemUid)]
    [InlineData(AccountUid, AccountUid, 0u)]
    [InlineData(AccountUid, AccountUid, AccountUid)]
    [InlineData(AccountUid, AccountUid, uint.MaxValue)]
    public void InvalidMappingOrUnusableItemNamespaceCannotMaterializeAnyFiles(uint account, uint character, uint item)
    {
        Assert.False(LegacyOwnedAdmissionFixture.TryCreate(Destination, account, character, item, out var result, out var reason));

        Assert.Null(result);
        Assert.NotEmpty(reason);
        Assert.Empty(Directory.GetFileSystemEntries(parent));
    }

    [Theory]
    [InlineData("basename")]
    [InlineData("relative")]
    [InlineData("missing-parent")]
    [InlineData("dot-segment")]
    public void InvalidOrAliasedTargetCannotCreateAStagingRoot(string fault)
    {
        var target = fault switch
        {
            "basename" => Path.Combine(parent, "looks-like-a-live-save"),
            "relative" => "wolf-owned-admission-" + Guid.NewGuid().ToString("N"),
            "missing-parent" => Path.Combine(parent, "missing", basename),
            _ => parent + Path.DirectorySeparatorChar + "." + Path.DirectorySeparatorChar + basename,
        };
        // The relative case uses read-only validation, so even a regression
        // cannot write to the shared test runner's working directory.
        if (fault == "relative") Assert.False(LegacyOwnedAdmissionFixture.TryValidateTarget(target, out _, out _));
        else
        {
            Assert.False(LegacyOwnedAdmissionFixture.TryCreate(target, AccountUid, AccountUid, ItemUid, out var result, out var reason));
            Assert.Null(result);
            Assert.NotEmpty(reason);
        }
        Assert.Empty(Directory.GetFileSystemEntries(parent));
    }

    [Fact]
    public void AnOwnedSaveShapedAncestorIsProtectedWithoutTouchingAnyOriginalSaves()
    {
        Directory.CreateDirectory(Path.Combine(parent, "Users"));
        Directory.CreateDirectory(Path.Combine(parent, "PlayersItems"));
        File.WriteAllBytes(Path.Combine(parent, "Users", "preserve.ini"), [1, 2, 3]);
        var files = Files(parent);

        Assert.False(LegacyOwnedAdmissionFixture.TryCreate(Destination, AccountUid, AccountUid, ItemUid, out var result, out var reason));

        Assert.Null(result);
        Assert.NotEmpty(reason);
        AssertFiles(parent, files);
        Assert.False(Directory.Exists(Destination));
        Assert.False(Directory.Exists(Staging));
    }

    [Fact]
    public void RealProtectedSystemLocationsAreRefusedByTheReadOnlyValidator()
    {
        var roots = new[] { Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.System), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) }.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct().ToArray();
        Assert.NotEmpty(roots);
        foreach (var root in roots)
        {
            Assert.False(LegacyOwnedAdmissionFixture.TryValidateTarget(Path.Combine(root, basename), out _, out var reason));
            Assert.NotEmpty(reason);
        }
        Assert.Empty(Directory.GetFileSystemEntries(parent));
    }

    [Fact]
    public async Task ReparseAncestorCannotRedirectFixtureWritesAndItsTargetRemainsExact()
    {
        var target = Path.Combine(parent, "physical-target");
        var link = Path.Combine(parent, "linked");
        Directory.CreateDirectory(target);
        File.WriteAllBytes(Path.Combine(target, "preserve.bin"), [2, 7, 9]);
        var files = Files(target);
        await CreateLinkAsync(link, target);
        try
        {
            Assert.NotEqual((FileAttributes)0, File.GetAttributes(link) & FileAttributes.ReparsePoint);
            Assert.False(LegacyOwnedAdmissionFixture.TryCreate(Path.Combine(link, basename), AccountUid, AccountUid, ItemUid,
                out var result, out var reason));
            Assert.Null(result);
            Assert.NotEmpty(reason);
            AssertFiles(target, files);
            Assert.False(Directory.Exists(Path.Combine(target, basename)));
            Assert.Equal(new[] { "preserve.bin" }, Directory.GetFileSystemEntries(target).Select(Path.GetFileName));
        }
        finally
        {
            Assert.NotEqual((FileAttributes)0, File.GetAttributes(link) & FileAttributes.ReparsePoint);
            Directory.Delete(link, recursive: false); // Remove this owned link only.
        }
    }

    private static async Task CreateLinkAsync(string link, string target)
    {
        if (!OperatingSystem.IsWindows()) { Directory.CreateSymbolicLink(link, target); return; }
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("cmd.exe")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true },
        };
        foreach (var argument in new[] { "/c", "mklink", "/J", link, target }) process.StartInfo.ArgumentList.Add(argument);
        Assert.True(process.Start());
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail("Owned junction creator did not terminate.");
        }
        Assert.True(process.ExitCode == 0, (await output) + (await error));
    }
}
