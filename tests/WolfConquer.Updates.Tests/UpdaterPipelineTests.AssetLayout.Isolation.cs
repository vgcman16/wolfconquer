using System.Security.Cryptography;
using System.Text.Json.Nodes;
using WolfConquer.Updater;

namespace WolfConquer.Updates.Tests;

public sealed partial class UpdaterPipelineTests
{
    [WindowsFact]
    public async Task GenericPipelineFixturesUseIndependentSignedConfiguredAndProtectedExecutableNames()
    {
        await using var first = await Fixture.CreateAsync();
        await using var second = await Fixture.CreateAsync();
        Assert.NotEqual(first.ExecutableName, second.ExecutableName);
        foreach (var fixture in new[] { first, second })
        {
            string executable = fixture.ExecutableName;
            Assert.StartsWith("WolfPipelineFixture-", executable, StringComparison.Ordinal);
            Assert.EndsWith(".exe", executable, StringComparison.Ordinal);
            Assert.True(Guid.TryParseExact(Path.GetFileNameWithoutExtension(executable)["WolfPipelineFixture-".Length..], "N", out _));
            Assert.NotEqual("FixtureClient.exe", executable, StringComparer.OrdinalIgnoreCase);
            Assert.NotEqual(executable, Path.GetFileName(LayoutEngine(fixture)), StringComparer.OrdinalIgnoreCase);
            var release = fixture.Publish(1, "independent generic fixture", null);
            Assert.Single(release.Manifest.Files, entry => entry.Path == executable);
            Assert.DoesNotContain(release.Manifest.Files, entry => entry.Path.Equals("FixtureClient.exe", StringComparison.OrdinalIgnoreCase));
            string config = fixture.WriteConfig(fixture.Client);
            var settings = LauncherWorkflow.ReadClientSettings(config);
            Assert.Equal(executable, settings.LaunchRelativePath);
            var json = JsonNode.Parse(File.ReadAllBytes(config))!.AsObject();
            Assert.Equal(executable, json["protectedExecutables"]!.AsArray().Single()!.GetValue<string>());
            Assert.Equal(fixture.Executable, File.ReadAllBytes(Path.Combine(fixture.Source, executable)));
            Assert.False(File.Exists(Path.Combine(fixture.Source, "FixtureClient.exe")));
        }
    }

    [WindowsFact]
    public async Task NeverLaunchedPipelineLayoutsUseIndependentSignedAndProtectedExecutableNames()
    {
        await WithAssetLayoutFixture(async first =>
        {
            await WithAssetLayoutFixture(second =>
            {
                Assert.NotEqual(LayoutEngine(first), LayoutEngine(second));
                foreach (var fixture in new[] { first, second })
                {
                    string engine = LayoutEngine(fixture);
                    Assert.StartsWith("Env_DX8/WolfPipelineLayout-", engine, StringComparison.Ordinal);
                    Assert.EndsWith(".exe", engine, StringComparison.Ordinal);
                    Assert.True(Guid.TryParseExact(Path.GetFileNameWithoutExtension(engine)["WolfPipelineLayout-".Length..], "N", out _));
                    Assert.NotEqual("FixtureClient.exe", Path.GetFileName(engine), StringComparer.OrdinalIgnoreCase);
                    using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                    PublishLayout(fixture, key);
                    string config = WriteLayoutConfig(fixture, key);
                    var settings = LauncherWorkflow.ReadClientSettings(config);
                    Assert.Equal(engine, settings.LaunchRelativePath);
                    var json = JsonNode.Parse(File.ReadAllBytes(config))!.AsObject();
                    Assert.Equal(engine, json["protectedExecutables"]!.AsArray().Single()!.GetValue<string>());
                    Assert.Equal(fixture.Executable, File.ReadAllBytes(Path.Combine(fixture.Source, engine)));
                }
                return Task.CompletedTask;
            });
        });
    }
}
