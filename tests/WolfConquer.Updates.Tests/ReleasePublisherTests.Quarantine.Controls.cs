using System.Reflection;
using System.Text;
using System.Text.Json;
using WolfConquer.Updates.Publishing;

namespace WolfConquer.Updates.Tests;

public sealed partial class ReleasePublisherTests
{
    [Theory]
    [InlineData("Conquer.exe")]
    [InlineData("Env_DX8/._cache_Conquer.exe")]
    [InlineData("ini/catalog.dat")]
    [InlineData("data/logistics.bmp")]
    [InlineData("ani/accountant.dat")]
    [InlineData("map/cachet.dat")]
    [InlineData("c3/binoculars.dat")]
    [InlineData("data/objectives.dat")]
    [InlineData("ini/oldworld.dat")]
    [InlineData("data/keyframe.dat")]
    [InlineData("data/secretary.dat")]
    [InlineData("tools/WolfConquer.UpdaterNotes.txt")]
    [InlineData("AutoPainter.dat")]
    [InlineData("sound/login_theme.wav")]
    public void OrdinaryAssetsAndNearNamesRemainPublishable(string relative)
    {
        using var f = new Fixture();
        f.Put(relative, "synthetic ordinary asset");
        byte[] before = File.ReadAllBytes(Path.Combine(f.Source, relative));

        var result = ReleasePublisher.Publish(f.Options(bundle: true),
            [new(relative, null)], f.Key, Fixture.KeyId);

        var verified = f.Verify(result.FullEnvelope);
        var entry = Assert.Single(verified.Manifest.Files);
        Assert.Equal(relative, entry.Path);
        Assert.Equal(before.LongLength, entry.Length);
        Assert.Equal(Fixture.Hash(before), entry.Sha256);
        Assert.Equal(before, File.ReadAllBytes(f.Blob(entry.Sha256)));
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(f.Source, relative)));
        Assert.Equal(result.IndexEnvelope, File.ReadAllBytes(result.FeedPath));
        Assert.Empty(Directory.GetFiles(f.Publish, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ActualPublishCommandRejectsUnsafeValidPlanBeforeReadingAbsentExternalKey()
    {
        using var f = new Fixture();
        const string unsafePath = "data/private-keys.txt";
        f.Put(unsafePath, "synthetic private-named bytes; no real key material");
        string feed = ExistingFeed(f);
        string planPath = Path.Combine(f.Root, "publication-plan.json");
        string absentKey = Path.Combine(f.Root, "absent-external-signing-key.pem");
        string plan = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            sourceRoot = f.Source,
            publishRoot = f.Publish,
            productId = "game",
            channel = "stable",
            sequence = 1,
            contentBaseUrl = "https://original.example.test/content/",
            createdUtc = Fixture.Now.AddMinutes(-1).ToString("O"),
            expiresUtc = Fixture.Now.AddHours(1).ToString("O"),
            keyId = Fixture.KeyId,
            files = new[] { new { path = unsafePath, baseSha256 = (string?)null } },
            includeBundle = true,
            allowLoopbackHttp = false,
        });
        File.WriteAllText(planPath, plan, new UTF8Encoding(false));
        byte[] sourceBefore = File.ReadAllBytes(Path.Combine(f.Source, unsafePath));
        Assert.False(File.Exists(absentKey));
        var command = Assembly.Load("WolfConquer.Updater").GetType(
            "WolfConquer.Updater.PublishingCommand", throwOnError: true)!;
        var method = command.GetMethod("Run", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var run = method.CreateDelegate<Func<string, string[], TextWriter, CancellationToken, Task<int>>>();
        using var output = new StringWriter();

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => run("publish",
            ["--plan", planPath, "--key", absentKey], output, CancellationToken.None));

        Assert.DoesNotContain(unsafePath, error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(absentKey, error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(output.ToString());
        Assert.False(File.Exists(absentKey));
        Assert.Equal(plan, File.ReadAllText(planPath));
        Assert.Equal(sourceBefore, File.ReadAllBytes(Path.Combine(f.Source, unsafePath)));
        AssertQuarantinePublicationUnchanged(f, feed);
    }
}
