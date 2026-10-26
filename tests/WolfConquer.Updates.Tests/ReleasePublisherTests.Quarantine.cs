using WolfConquer.Updates.Publishing;

namespace WolfConquer.Updates.Tests;

public sealed partial class ReleasePublisherTests
{
    [Theory]
    [InlineData("AutoP.exe")]
    [InlineData("._cache_AutoP.exe")]
    [InlineData("nested/tools/AUTOP.EXE")]
    [InlineData("nested/tools/._cache_AutoP.exe")]
    [InlineData("data/signing.key")]
    [InlineData("data/client.PFX")]
    [InlineData("data/engine.dmp")]
    [InlineData("data/world.sql")]
    [InlineData("ini/catalog.dat.bak.20261022")]
    [InlineData("ini/catalog.old-2026-10-22")]
    [InlineData("ini/catalog.orig_copy")]
    [InlineData("ini/catalog.tmp.20261022")]
    [InlineData("ini/catalog.temp_20261022")]
    [InlineData("accounts/player.dat")]
    [InlineData("profile-current/selection.dat")]
    [InlineData("User_Data/selection.dat")]
    [InlineData("data/users.bin")]
    [InlineData("cache/pack.dat")]
    [InlineData("data/CACHES-fast/pack.dat")]
    [InlineData("bin/engine.dll")]
    [InlineData("obj/generated.dat")]
    [InlineData("Logs/session.dat")]
    [InlineData("data/log.engine.dat")]
    [InlineData("data/credentials.dat")]
    [InlineData("data/release-private.txt")]
    [InlineData("data/.env.local")]
    [InlineData("data/id_ed25519.pub")]
    [InlineData("ini/last-login.dat")]
    [InlineData("ini/recent_players.dat")]
    [InlineData("nested/trusted-release-keys.json")]
    [InlineData("ini/wolfconquer-updates.json")]
    [InlineData("tools/WolfConquer.Updater.dll")]
    [InlineData("tools/WolfConquer.Launcher.exe")]
    [InlineData(".wolfconquer-cache/blob.dat")]
    [InlineData("data/secret.token.txt")]
    [InlineData("data/customer.password.txt")]
    public void QuarantinedCuratedSourceCannotReplaceFeedOrCreateBlobs(string relative)
    {
        using var f = new Fixture();
        f.Put(relative, "synthetic quarantined payload; no real private material");
        string source = Path.Combine(f.Source, relative);
        byte[] before = File.ReadAllBytes(source);
        DateTime stamp = File.GetLastWriteTimeUtc(source);
        string feed = ExistingFeed(f);

        var error = Assert.Throws<InvalidDataException>(() => ReleasePublisher.Publish(f.Options(bundle: true),
            [new(relative, null)], f.Key, Fixture.KeyId));

        Assert.DoesNotContain(relative, error.Message, StringComparison.OrdinalIgnoreCase);
        AssertQuarantinePublicationUnchanged(f, feed);
        Assert.Equal(before, File.ReadAllBytes(source));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(source));
    }

    [WindowsFact]
    public void EntireQuarantinePlanIsRejectedBeforeOpeningAnEarlierExclusiveSource()
    {
        using var f = new Fixture();
        const string first = "ani/first.dat";
        const string unsafePath = "data/signing.key";
        f.Put(first, "synthetic ordinary asset");
        f.Put(unsafePath, "synthetic key-named bytes; not a signing key");
        string feed = ExistingFeed(f);
        byte[] firstBefore = File.ReadAllBytes(Path.Combine(f.Source, first));
        byte[] unsafeBefore = File.ReadAllBytes(Path.Combine(f.Source, unsafePath));
        InvalidDataException error;

        using (var held = new FileStream(Path.Combine(f.Source, first), FileMode.Open,
                   FileAccess.Read, FileShare.None))
            error = Assert.Throws<InvalidDataException>(() => ReleasePublisher.Publish(f.Options(),
                [new(first, null), new(unsafePath, null)], f.Key, Fixture.KeyId));

        Assert.DoesNotContain(unsafePath, error.Message, StringComparison.OrdinalIgnoreCase);
        AssertQuarantinePublicationUnchanged(f, feed);
        Assert.Equal(firstBefore, File.ReadAllBytes(Path.Combine(f.Source, first)));
        Assert.Equal(unsafeBefore, File.ReadAllBytes(Path.Combine(f.Source, unsafePath)));
    }

    private static void AssertQuarantinePublicationUnchanged(Fixture f, string feed)
    {
        Assert.Equal("previous signed feed", File.ReadAllText(feed));
        Assert.False(Directory.Exists(Path.Combine(f.Publish, "blobs")));
        Assert.Equal(new[] { feed }, Directory.GetFiles(f.Publish, "*", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(f.Publish, "*.tmp", SearchOption.AllDirectories));
    }
}
