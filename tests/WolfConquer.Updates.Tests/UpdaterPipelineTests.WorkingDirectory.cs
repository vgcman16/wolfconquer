using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using WolfConquer.Updater;
using Xunit;

namespace WolfConquer.Updates.Tests;

public sealed partial class UpdaterPipelineTests
{
    [WindowsTheory]
    [InlineData(null)]
    [InlineData("data")]
    public async Task SignedOwnedWindowsCommandObservesConfiguredDirectoryOrLegacyPayloadRoot(string? relative)
    {
        var f = await Fixture.CreateAsync();
        Exception? bodyFailure = null;
        try
        {
            string systemCommand = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            Assert.True(File.Exists(systemCommand), "The harmless Windows command fixture requires the OS cmd.exe.");
            // Copy the OS executable into this fixture's real signed release. Never launch the synthetic bytes.
            File.Copy(systemCommand, Path.Combine(f.Source, f.ExecutableName), overwrite: true);
            f.Publish(1, "owned working-directory fixture", null);
            string config = f.WriteConfig(f.Client);
            string expected = relative is null ? f.Client : Path.Combine(f.Client, relative);
            string observed = Path.Combine(expected, "observed-working-directory.txt");
            // Static relative output avoids cmd's non-CRT embedded-quote rules; /u preserves Unicode paths.
            ConfigureWorkingDirectory(config, relative, ["/d", "/q", "/u", "/c", "cd > observed-working-directory.txt"]);
            byte[] originalConfig = File.ReadAllBytes(config);
            var settings = LauncherWorkflow.ReadClientSettings(config);
            Assert.Equal(relative, settings.LaunchWorkingDirectoryRelativePath);
            Assert.Equal(f.Client, settings.InstallDirectory);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));

            var result = await LauncherWorkflow.RunClientAsync(config, fresh: true, launch: true,
                cancellationToken: deadline.Token);

            Assert.NotNull(result.LaunchedProcessId);
            await WaitForOwnedCommand(result.LaunchedProcessId.Value, f.ClientFile(f.ExecutableName));
            Assert.Equal("launched", result.Status);
            Assert.True(result.LaunchCandidateAvailable);
            Assert.Equal(expected, File.ReadAllText(observed, Encoding.Unicode).TrimEnd('\r', '\n'), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(originalConfig, File.ReadAllBytes(config));
            Assert.Equal(File.ReadAllBytes(systemCommand), File.ReadAllBytes(f.ClientFile(f.ExecutableName)));
            Assert.Equal("owned working-directory fixture", File.ReadAllText(f.ClientFile("data/catalog.dat")));
            await WaitForOwnedDirectoryRelease(f, expected);
            Assert.True(Directory.Exists(expected));
        }
        catch (Exception error) { bodyFailure = error; throw; }
        finally
        {
            try { await f.DisposeAsync(); }
            catch (Exception cleanupError) when (bodyFailure is not null)
            {
                throw new AggregateException("Owned command test failed and its fixture cleanup also failed; preserve both diagnostics.",
                    bodyFailure, cleanupError);
            }
        }
    }

    [WindowsTheory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../outside")]
    [InlineData("data/../outside")]
    [InlineData("data//nested")]
    [InlineData("data/")]
    [InlineData("C:/Windows")]
    [InlineData("/Windows")]
    [InlineData("\\\\invalid.example.test\\share")]
    [InlineData("\\\\?\\C:\\Windows")]
    [InlineData("data\\nested")]
    [InlineData("data:stream")]
    [InlineData("NUL")]
    [InlineData("data/CON.txt")]
    [InlineData(".wolfconquer-updates")]
    public async Task UnsafeWorkingDirectoryIsRefusedBeforeAnyPayloadCacheOrConfigurationMutation(string relative)
    {
        await using var f = await Fixture.CreateAsync();
        string config = f.WriteConfig(f.Client);
        ConfigureWorkingDirectory(config, relative);
        var before = Fixture.Snapshot(f.Root);

        Assert.Throws<InvalidDataException>(() => LauncherWorkflow.ReadClientSettings(config));
        await Assert.ThrowsAsync<InvalidDataException>(() => LauncherWorkflow.RunClientAsync(config, fresh: true, launch: true));

        Fixture.AssertSnapshot(before, Fixture.Snapshot(f.Root));
        Assert.False(Directory.Exists(f.Client));
        Assert.False(Directory.Exists(Path.Combine(f.Bootstrap, ".wolfconquer-cache")));
    }

    [WindowsFact]
    public async Task ExistingFileCannotBecomeWorkingDirectoryAndIsNotModified()
    {
        await using var f = await Fixture.CreateAsync();
        Directory.CreateDirectory(f.Client);
        File.WriteAllText(f.ClientFile("not-a-directory"), "preserve this owned local file");
        string config = f.WriteConfig(f.Client);
        ConfigureWorkingDirectory(config, "not-a-directory");
        var before = Fixture.Snapshot(f.Root);

        await Assert.ThrowsAsync<InvalidDataException>(() => LauncherWorkflow.RunClientAsync(config, launch: true));

        Fixture.AssertSnapshot(before, Fixture.Snapshot(f.Root));
        Assert.False(Directory.Exists(f.ClientFile(".wolfconquer-updates")));
        Assert.False(Directory.Exists(Path.Combine(f.Bootstrap, ".wolfconquer-cache")));
    }

    [WindowsFact]
    public async Task MissingDirectoryRefusesLaunchBeforeCacheRewriteAndKeepsInstalledBytes()
    {
        await using var f = await Fixture.CreateAsync();
        f.Publish(1, "installed synthetic fixture; never launch", null);
        string config = f.WriteConfig(f.Client);
        await LauncherWorkflow.RunClientAsync(config, fresh: true);
        ConfigureWorkingDirectory(config, "not-installed");
        var beforePayload = Fixture.Snapshot(f.Client);
        var beforeBootstrap = Fixture.Snapshot(f.Bootstrap);

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => LauncherWorkflow.RunClientAsync(config, launch: true));

        Fixture.AssertSnapshot(beforePayload, Fixture.Snapshot(f.Client));
        Fixture.AssertSnapshot(beforeBootstrap, Fixture.Snapshot(f.Bootstrap));
        Assert.False(Directory.Exists(f.ClientFile("not-installed")));
    }

    [WindowsTheory]
    [InlineData("linked")]
    [InlineData("linked/nested")]
    public async Task WorkingDirectoryReparseTargetOrAncestorIsRefusedWithoutTouchingItsDestination(string relative)
    {
        await using var f = await Fixture.CreateAsync();
        Directory.CreateDirectory(f.Client);
        string outside = Path.Combine(f.Root, "owned-link-destination");
        Directory.CreateDirectory(Path.Combine(outside, "nested"));
        string preserved = Path.Combine(outside, "keep.txt");
        File.WriteAllText(preserved, "owned destination remains untouched");
        string link = f.ClientFile("linked");
        try { Directory.CreateSymbolicLink(link, outside); }
        catch (UnauthorizedAccessException)
        { throw Xunit.Sdk.SkipException.ForSkip("Windows symlink privilege/developer mode is unavailable for this owned working-directory fixture."); }
        try
        {
            string config = f.WriteConfig(f.Client);
            ConfigureWorkingDirectory(config, relative);
            var beforeConfig = File.ReadAllBytes(config);
            var beforeDestination = Fixture.Snapshot(outside);

            await Assert.ThrowsAsync<InvalidDataException>(() => LauncherWorkflow.RunClientAsync(config, launch: true));

            Fixture.AssertSnapshot(beforeDestination, Fixture.Snapshot(outside));
            Assert.Equal(beforeConfig, File.ReadAllBytes(config));
            Assert.False(Directory.Exists(f.ClientFile(".wolfconquer-updates")));
            Assert.False(Directory.Exists(Path.Combine(f.Bootstrap, ".wolfconquer-cache")));
        }
        finally { Directory.Delete(link); } // Remove only the owned link, never its destination tree.
    }

    private static void ConfigureWorkingDirectory(string config, string? relative, string[]? arguments = null)
    {
        var json = JsonNode.Parse(File.ReadAllBytes(config))!.AsObject();
        if (relative is not null) json["launchWorkingDirectoryRelativePath"] = relative;
        else json.Remove("launchWorkingDirectoryRelativePath"); // The original configuration shape stays supported.
        if (arguments is not null) json["launchArguments"] = new JsonArray(arguments.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
        File.WriteAllText(config, json.ToJsonString());
    }

    private static async Task WaitForOwnedDirectoryRelease(Fixture fixture, string expected)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(fixture.Root));
        string temporaryRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        string source = Path.GetFullPath(expected);
        if (!root.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(root).StartsWith("wolf-updater-pipeline-", StringComparison.Ordinal) ||
            !source.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refusing a directory-release probe outside the owned fixture.");
        string moved = source + "-released-" + Guid.NewGuid().ToString("N");
        var elapsed = Stopwatch.StartNew();
        bool movedAway = false;
        Exception? lastError = null;
        while (elapsed.Elapsed < TimeSpan.FromSeconds(5))
        {
            try
            {
                if (!movedAway) { Directory.Move(source, moved); movedAway = true; }
                Directory.Move(moved, source);
                return; // Both operations completed; a killed child or a deletion retry cannot satisfy this check.
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                lastError = error;
                var remaining = TimeSpan.FromSeconds(5) - elapsed.Elapsed;
                if (remaining > TimeSpan.Zero)
                    await Task.Delay(remaining < TimeSpan.FromMilliseconds(25) ? remaining : TimeSpan.FromMilliseconds(25));
            }
        }
        throw new IOException("After command exit, the owned working directory did not complete its move-and-back release check within five seconds" +
            (movedAway ? " (original path restoration is pending)." : "."), lastError);
    }

}
