using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace WolfConquer.Updates.Tests;

// Real loopback HTTP and shared CLI/GUI workflows. No game-client or source builds.
// Working-directory controls may launch an owned signed copy of the OS command tool.
public sealed partial class UpdaterPipelineTests
{
    [WindowsFact]
    public async Task FullInstallMetadataOnlyUpdateConflictAndFreshLatestInstallUseOnePipeline()
    {
        await using var f = await Fixture.CreateAsync();
        var first = f.Publish(1, "data-v1", null);
        string config = f.WriteConfig(f.Client);
        var install = await f.Run("client", "--config", config, "--install");
        Assert.Equal(0, install.ExitCode); Assert.Equal("", install.Error);
        Assert.Equal(f.Executable, File.ReadAllBytes(f.ClientFile("FixtureClient.exe")));
        Assert.Equal("data-v1", File.ReadAllText(f.ClientFile("data/catalog.dat")));
        var before = Fixture.Snapshot(f.Client);
        var cacheBefore = Fixture.Snapshot(Path.Combine(f.Bootstrap, ".wolfconquer-cache"));
        var unchanged = await f.Run("client", "--config", config);
        Assert.Equal(0, unchanged.ExitCode);
        Assert.Contains("unchanged (metadata-only", unchanged.Output, StringComparison.Ordinal);
        Fixture.AssertSnapshot(before, Fixture.Snapshot(f.Client));
        Fixture.AssertSnapshot(cacheBefore, Fixture.Snapshot(Path.Combine(f.Bootstrap, ".wolfconquer-cache")));

        string firstDataHash = first.Manifest.Files.Single(file => file.Path == "data/catalog.dat").Sha256;
        var second = f.Publish(2, "data-v2", firstDataHash);
        var update = await f.Run("client", "--config", config);
        Assert.Equal(0, update.ExitCode); Assert.Equal("", update.Error);
        Assert.Equal("data-v2", File.ReadAllText(f.ClientFile("data/catalog.dat")));
        Assert.Equal(f.Executable, File.ReadAllBytes(f.ClientFile("FixtureClient.exe")));
        byte[] committedState = File.ReadAllBytes(f.ClientFile(".wolfconquer-updates/state.json"));
        File.WriteAllText(f.ClientFile("data/catalog.dat"), "owned local edit", new UTF8Encoding(false));
        string secondDataHash = second.Manifest.Files.Single(file => file.Path == "data/catalog.dat").Sha256;
        var latest = f.Publish(3, "data-v3", secondDataHash);
        var refused = await f.Run("client", "--config", config);
        Assert.Equal(1, refused.ExitCode);
        Assert.Contains("modified file", refused.Error, StringComparison.Ordinal);
        Assert.Equal("owned local edit", File.ReadAllText(f.ClientFile("data/catalog.dat")));
        Assert.Equal(committedState, File.ReadAllBytes(f.ClientFile(".wolfconquer-updates/state.json")));

        string freshClient = Path.Combine(f.Root, "fresh-latest-client");
        string freshConfig = f.WriteConfig(freshClient, "fresh.json");
        var fresh = await f.Run("client", "--config", freshConfig, "--install");
        Assert.Equal(0, fresh.ExitCode); Assert.Equal("", fresh.Error);
        Assert.NotNull(latest.Manifest.Bundle);
        Assert.NotNull(latest.Manifest.Files.Single(file => file.Path == "data/catalog.dat").BaseSha256);
        Assert.Equal("data-v3", File.ReadAllText(Path.Combine(freshClient, "data/catalog.dat")));
        Assert.Equal(f.Executable, File.ReadAllBytes(Path.Combine(freshClient, "FixtureClient.exe")));
        Assert.DoesNotContain("Started configured", install.Output + update.Output + fresh.Output, StringComparison.Ordinal);
    }

    [WindowsFact]
    public async Task CheckWritesSafeReceiptAndDetectsMissingPublishedBundleViaRealHead()
    {
        await using var f = await Fixture.CreateAsync();
        var release = f.Publish(1, "receipt-data", null);
        string config = f.WriteConfig(f.Client);
        string receipt = Path.Combine(f.Root, "receipt.json");
        using var http = new HttpClient();
        foreach (var payload in new[] { (release.Manifest.Bundle!.Sha256, release.Manifest.Bundle.Length),
                     (release.Manifest.Files[0].Sha256, release.Manifest.Files[0].Length) })
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, new Uri(f.Host.BaseAddress, "blobs/" + payload.Sha256.ToLowerInvariant()));
            using var response = await http.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(payload.Length, response.Content.Headers.ContentLength);
        }
        var passed = await f.Run("check", "--config", config, "--receipt", receipt);
        Assert.Equal(0, passed.ExitCode); Assert.Equal("", passed.Error);
        AssertReceipt(receipt, "passed", true, "verified");
        Assert.False(Directory.Exists(f.Client)); // Availability checks do not install a client.
        await f.RemoveBlob(release.Manifest.Bundle.Sha256);
        var failed = await f.Run("check", "--config", config, "--receipt", receipt);
        Assert.Equal(1, failed.ExitCode); Assert.Equal("", failed.Error);
        AssertReceipt(receipt, "failed", false, "downloads-unreachable");
        Assert.DoesNotContain(f.Root, File.ReadAllText(receipt), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(f.PublicKey, File.ReadAllText(receipt), StringComparison.Ordinal);
    }

    [WindowsTheory]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    [InlineData("reparse")]
    public async Task UntrustedConfigurationShapesAreRefusedBeforePayloadMutation(string kind)
    {
        await using var f = await Fixture.CreateAsync();
        string config = f.WriteConfig(f.Client);
        string? link = null;
        if (kind == "duplicate")
            File.WriteAllText(config, File.ReadAllText(config).Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1", StringComparison.Ordinal));
        else if (kind == "unknown")
        {
            var json = JsonNode.Parse(File.ReadAllText(config))!.AsObject(); json["untrustedExtra"] = true;
            File.WriteAllText(config, json.ToJsonString());
        }
        else
        {
            link = Path.Combine(f.Bootstrap, "linked-config.json");
            try { File.CreateSymbolicLink(link, config); }
            catch (UnauthorizedAccessException)
            { throw Xunit.Sdk.SkipException.ForSkip("Windows symlink privilege/developer mode is unavailable for this owned reparse fixture."); }
            config = link;
        }
        try
        {
            var result = await f.Run("client", "--config", config, "--install");
            Assert.Equal(1, result.ExitCode);
            Assert.Contains("WolfConquer updater refused", result.Error, StringComparison.Ordinal);
            Assert.False(Directory.Exists(f.Client));
            Assert.False(Directory.Exists(Path.Combine(f.Bootstrap, ".wolfconquer-cache")));
        }
        finally { if (link is not null) File.Delete(link); }
    }

    [WindowsFact]
    public async Task AlteredSyntheticLaunchTargetIsRefusedAtHashGateBeforeProcessStart()
    {
        await using var f = await Fixture.CreateAsync(); f.Publish(1, "one", null);
        string config = f.WriteConfig(f.Client);
        Assert.Equal(0, (await f.Run("client", "--config", config, "--install")).ExitCode);
        const string modified = "not-an-executable: local fixture edit";
        File.WriteAllText(f.ClientFile("FixtureClient.exe"), modified, new UTF8Encoding(false));
        var result = await f.Run("client", "--config", config, "--launch");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Launch executable differs from the signed release; no process started.", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("Started configured", result.Output, StringComparison.Ordinal);
        Assert.Equal(modified, File.ReadAllText(f.ClientFile("FixtureClient.exe")));
    }

    private static void AssertReceipt(string path, string outcome, bool reachable, string reason)
    {
        using var json = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = json.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("fixture-client", root.GetProperty("productId").GetString());
        Assert.Equal("preview", root.GetProperty("channel").GetString());
        Assert.Equal(1, root.GetProperty("sequence").GetInt64());
        Assert.True(root.GetProperty("indexValid").GetBoolean());
        Assert.True(root.GetProperty("manifestValid").GetBoolean());
        Assert.Equal(reachable, root.GetProperty("downloadsReachable").GetBoolean());
        Assert.Equal(outcome, root.GetProperty("outcome").GetString());
        Assert.Equal(reason, root.GetProperty("reason").GetString());
    }
}
