using System.Text;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using WolfConquer.Updater;
using WolfConquer.Updates.Progress;
using WolfConquer.Updates.Publishing;
using Xunit;

namespace WolfConquer.Updates.Tests;

public sealed partial class UpdaterPipelineTests
{
    [WindowsFact]
    public async Task TypedFreshInstallOverrideUsesSelectedTargetWithoutChangingDefaultOrConfiguration()
    {
        await using var f = await Fixture.CreateAsync();
        f.Publish(1, "selected-target-data", null);
        var config = f.WriteConfig(f.Client);
        var configBytes = File.ReadAllBytes(config);
        Directory.CreateDirectory(f.Client);
        File.WriteAllText(f.ClientFile("keep-local.txt"), "default client remains local", new UTF8Encoding(false));
        var defaultBefore = Fixture.Snapshot(f.Client);
        var selected = Path.Combine(f.Root, "selected fresh client");
        Assert.False(Directory.Exists(selected));

        var result = await LauncherWorkflow.RunClientAsync(config, fresh: true, installRootOverride: selected);

        Assert.Equal("updated", result.Status);
        Assert.Equal("fixture-client", result.ProductId);
        Assert.Equal("preview", result.Channel);
        Assert.Equal(1, result.Sequence);
        Assert.Equal(2, result.Installation!.InstalledFiles);
        Assert.False(result.MetadataOnly);
        Assert.True(result.LaunchCandidateAvailable);
        Assert.Null(result.LaunchedProcessId);
        Assert.Equal(f.Executable, File.ReadAllBytes(Path.Combine(selected, "FixtureClient.exe")));
        Assert.Equal("selected-target-data", File.ReadAllText(Path.Combine(selected, "data/catalog.dat")));
        Fixture.AssertSnapshot(defaultBefore, Fixture.Snapshot(f.Client));
        Assert.Equal(configBytes, File.ReadAllBytes(config));
        var settings = LauncherWorkflow.ReadClientSettings(config);
        Assert.Equal(f.Client, settings.InstallDirectory);
        Assert.Equal(config, settings.ConfigurationPath);
        Assert.Equal("FixtureClient.exe", settings.LaunchRelativePath);
    }

    [WindowsTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TypedFullManifestWithoutExactConfiguredLaunchPathDoesNotReportACandidate(bool caseMismatch)
    {
        await using var f = await Fixture.CreateAsync();
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        File.WriteAllText(Path.Combine(f.Source, "data/catalog.dat"), "data-only-release", new UTF8Encoding(false));
        var plan = new List<PublishEntry> { new("data/catalog.dat", null) };
        if (caseMismatch)
        {
            File.WriteAllBytes(Path.Combine(f.Source, "fixtureclient.exe"), f.Executable);
            plan.Add(new("fixtureclient.exe", null));
        }
        var now = DateTimeOffset.UtcNow;
        ReleasePublisher.Publish(new ReleasePublishOptions(f.Source, f.Published, "fixture-client", "preview", 1,
            f.Host.BaseAddress, now.AddMinutes(-1), now.AddHours(1), includeBundle: true, allowLoopbackHttp: true),
            plan, signingKey, "pipeline-test-key");
        string config = f.WriteConfig(f.Client);
        var json = JsonNode.Parse(File.ReadAllBytes(config))!.AsObject();
        json["publicKeySpkiPem"] = signingKey.ExportSubjectPublicKeyInfoPem();
        File.WriteAllText(config, json.ToJsonString());

        var result = await LauncherWorkflow.RunClientAsync(config, fresh: true);

        Assert.Equal("updated", result.Status);
        Assert.False(result.MetadataOnly);
        Assert.False(result.LaunchCandidateAvailable);
        Assert.Null(result.LaunchedProcessId);
        Assert.Equal(plan.Count, result.Installation!.InstalledFiles);
        Assert.Equal("data-only-release", File.ReadAllText(f.ClientFile("data/catalog.dat")));
        Assert.Equal(caseMismatch, File.Exists(f.ClientFile("fixtureclient.exe")));
    }

    [WindowsFact]
    public async Task TypedMetadataOnlyAndRecoveryResultsKeepLaunchCandidateUnknownUntilFullVerification()
    {
        await using var f = await Fixture.CreateAsync();
        f.Publish(1, "candidate-data", null);
        string config = f.WriteConfig(f.Client);
        var installed = await LauncherWorkflow.RunClientAsync(config, fresh: true);
        Assert.True(installed.LaunchCandidateAvailable);
        var before = Fixture.Snapshot(f.Client);

        var current = await LauncherWorkflow.RunClientAsync(config);
        Assert.Equal("unchanged", current.Status);
        Assert.True(current.MetadataOnly);
        Assert.False(current.LaunchCandidateAvailable);
        Assert.Null(current.Installation);
        Assert.Null(current.LaunchedProcessId);
        Fixture.AssertSnapshot(before, Fixture.Snapshot(f.Client));

        var recovered = await LauncherWorkflow.RecoverClientAsync(config);
        Assert.Equal("recovered", recovered.Status);
        Assert.False(recovered.LaunchCandidateAvailable);
        Assert.Null(recovered.LaunchedProcessId);
        var verified = await LauncherWorkflow.RunClientAsync(config, repair: true);
        Assert.Equal("verified", verified.Status);
        Assert.False(verified.MetadataOnly);
        Assert.True(verified.LaunchCandidateAvailable);
        Assert.Null(verified.LaunchedProcessId);
    }

    [WindowsTheory]
    [InlineData("config")]
    [InlineData("bootstrap")]
    [InlineData("relative")]
    [InlineData("unc")]
    [InlineData("volume")]
    public async Task TypedForbiddenOverrideRefusesBeforeOwnedPayloadCacheOrControlWrites(string kind)
    {
        await using var f = await Fixture.CreateAsync();
        f.Publish(1, "must-not-install", null);
        var config = f.WriteConfig(f.Client);
        var target = kind switch
        {
            "config" => config,
            "bootstrap" => f.Bootstrap,
            "relative" => Path.Combine("..", "client"), // A broken base-path guard would still resolve inside this owned fixture.
            "unc" => "\\\\invalid.example.test\\synthetic-share\\client",
            "volume" => Path.GetPathRoot(f.Root)!,
            _ => throw new InvalidOperationException("Unknown synthetic override case.")
        };
        var before = Fixture.Snapshot(f.Root);

        var error = await Record.ExceptionAsync(() => LauncherWorkflow.RunClientAsync(config, fresh: true, installRootOverride: target));

        Assert.NotNull(error);
        Assert.True(error is InvalidDataException or IOException, "Expected a path-safety or existing-file refusal.");
        Fixture.AssertSnapshot(before, Fixture.Snapshot(f.Root));
        Assert.False(Directory.Exists(f.Client));
        Assert.False(Directory.Exists(Path.Combine(f.Bootstrap, ".wolfconquer-cache")));
        Assert.False(Directory.Exists(Path.Combine(f.Bootstrap, ".wolfconquer-updates")));
    }

    [WindowsFact]
    public async Task TypedAlreadyCanceledInstallCreatesNoTargetCacheOrControlFiles()
    {
        await using var f = await Fixture.CreateAsync();
        var config = f.WriteConfig(f.Client);
        var target = Path.Combine(f.Root, "cancelled fresh client");
        var before = Fixture.Snapshot(f.Root);
        var observer = new WorkflowProgress();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => LauncherWorkflow.RunClientAsync(config,
            fresh: true, progress: observer, cancellationToken: cancellation.Token, installRootOverride: target));

        Fixture.AssertSnapshot(before, Fixture.Snapshot(f.Root));
        Assert.False(Directory.Exists(target));
        Assert.False(Directory.Exists(f.Client));
        Assert.False(Directory.Exists(Path.Combine(f.Bootstrap, ".wolfconquer-cache")));
        Assert.Empty(observer.Events);
    }

    [WindowsFact]
    public async Task TypedSynchronousThrowingObserverCannotInterruptFullSignedInstall()
    {
        await using var f = await Fixture.CreateAsync();
        f.Publish(1, "observer-safe-data", null);
        var config = f.WriteConfig(f.Client);
        var beforeConfig = File.ReadAllBytes(config);
        var observer = new WorkflowProgress(throws: true);

        var result = await LauncherWorkflow.RunClientAsync(config, fresh: true, progress: observer);

        Assert.Equal("updated", result.Status);
        Assert.Equal(2, result.Installation!.InstalledFiles);
        Assert.Null(result.LaunchedProcessId);
        Assert.Equal(f.Executable, File.ReadAllBytes(f.ClientFile("FixtureClient.exe")));
        Assert.Equal("observer-safe-data", File.ReadAllText(f.ClientFile("data/catalog.dat")));
        Assert.Equal(beforeConfig, File.ReadAllBytes(config));
        Assert.Contains(observer.Events, p => p.Phase == UpdateOperationPhase.Downloading && p.Completed > 0);
        Assert.Equal(UpdateOperationPhase.Completed, observer.Events[^1].Phase);
        Assert.Contains("committed", File.ReadAllText(Path.Combine(result.Installation.TransactionDirectory!, "journal.json")));
    }

    [WindowsFact]
    public async Task TypedUpdatePreservesCustomEditsAndPreviousCommittedStateAndCache()
    {
        await using var f = await Fixture.CreateAsync();
        var first = f.Publish(1, "original-data", null);
        var config = f.WriteConfig(f.Client);
        var installed = await LauncherWorkflow.RunClientAsync(config, fresh: true);
        Assert.Equal("updated", installed.Status);
        File.WriteAllText(f.ClientFile("data/catalog.dat"), "local custom edit", new UTF8Encoding(false));
        f.Publish(2, "new-release-data", first.Manifest.Files.Single(file => file.Path == "data/catalog.dat").Sha256);
        var payloadBefore = Fixture.Snapshot(f.Client);
        var bootstrapBefore = Fixture.Snapshot(f.Bootstrap);

        await Assert.ThrowsAsync<InvalidDataException>(() => LauncherWorkflow.RunClientAsync(config));

        Fixture.AssertSnapshot(payloadBefore, Fixture.Snapshot(f.Client));
        Fixture.AssertSnapshot(bootstrapBefore, Fixture.Snapshot(f.Bootstrap));
        Assert.Equal("local custom edit", File.ReadAllText(f.ClientFile("data/catalog.dat")));
        Assert.Equal(f.Executable, File.ReadAllBytes(f.ClientFile("FixtureClient.exe")));
    }

    private sealed class WorkflowProgress(bool throws = false) : IProgress<UpdateOperationProgress>
    {
        internal List<UpdateOperationProgress> Events { get; } = [];
        public void Report(UpdateOperationProgress value)
        {
            Events.Add(value);
            if (throws) throw new InvalidOperationException("Synthetic synchronous workflow observer failure.");
        }
    }
}
