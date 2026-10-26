using System.IO.Compression;
using WolfConquer.Updates.Publishing;

namespace WolfConquer.Updates.Tests;

public sealed partial class ReleasePublisherTests
{
    [Theory]
    [InlineData("dds")]
    [InlineData("BMP")]
    [InlineData("png")]
    [InlineData("tga")]
    [InlineData("msk")]
    public void ExactReviewedNativeMapAssetPublishesBoundBytesAndRetainsPriorInstallHash(string extension)
    {
        using var f = new Fixture();
        string path = NativeObjRoot + "/nested/more/sprite." + extension;
        f.Put(path, "synthetic reviewed bytes; no raster-format claim");
        var review = MakeNativeReview(f, path);
        review = review with
        {
            ExpectedSourceSha256 = review.ExpectedSourceSha256.ToLowerInvariant(),
            Evidence = new string('A', 128),
        };
        string priorInstalled = new('B', 64);

        var result = ReleasePublisher.Publish(f.Options(bundle: true), [new(path, priorInstalled)],
            f.Key, Fixture.KeyId, [review]);

        var full = f.Verify(result.FullEnvelope);
        var file = Assert.Single(full.Manifest.Files);
        Assert.Equal(1, full.Manifest.SchemaVersion);
        Assert.Equal(path, file.Path);
        Assert.Equal(review.Length, file.Length);
        Assert.Equal(review.ExpectedSourceSha256, file.Sha256, ignoreCase: true);
        Assert.Equal(priorInstalled, file.BaseSha256);
        Assert.Equal(File.ReadAllBytes(Path.Combine(f.Source, path)), File.ReadAllBytes(f.Blob(file.Sha256)));
        var bundle = Assert.IsType<WolfConquer.Updates.Manifest.UpdateBundle>(full.Manifest.Bundle);
        using var zip = new ZipArchive(File.OpenRead(f.Blob(bundle.Sha256)), ZipArchiveMode.Read);
        var entry = Assert.Single(zip.Entries);
        Assert.Equal(path, entry.FullName);
        using var input = entry.Open(); using var bytes = new MemoryStream(); input.CopyTo(bytes);
        Assert.Equal(File.ReadAllBytes(Path.Combine(f.Source, path)), bytes.ToArray());
        Assert.Equal(result.IndexEnvelope, File.ReadAllBytes(result.FeedPath));
    }

    [Fact]
    public void ExactLogsBitmapAndObjReviewCoexistWithOrdinaryPayloadAndRepoint()
    {
        using var f = new Fixture();
        f.Put(NativeObjLeaf, "synthetic map asset"); f.Put(NativeLogsBitmap, "synthetic bitmap asset");
        f.Put("ini/catalog.dat", "ordinary catalog");
        NativeAssetReview[] reviews = [MakeNativeReview(f, NativeObjLeaf), MakeNativeReview(f, NativeLogsBitmap)];
        var result = ReleasePublisher.Publish(f.Options(),
            [new(NativeObjLeaf, null), new(NativeLogsBitmap, null), new("ini/catalog.dat", null)],
            f.Key, Fixture.KeyId, reviews);
        var full = f.Verify(result.FullEnvelope);
        Assert.Equal(new[] { NativeObjLeaf, NativeLogsBitmap, "ini/catalog.dat" }, full.Manifest.Files.Select(file => file.Path));
        foreach (var file in full.Manifest.Files)
            Assert.Equal(File.ReadAllBytes(Path.Combine(f.Source, file.Path)), File.ReadAllBytes(f.Blob(file.Sha256)));

        byte[] repointed = ReleasePublisher.RepointIndex(result.FullEnvelope, Fixture.KeyId, f.PublicKey,
            f.Context(), new Uri("https://replacement.example.test/reviewed/"), f.Key);
        var index = f.Verify(repointed, index: true);
        Assert.Equal(result.FullEnvelopeSha256, Assert.Single(index.Manifest.Files).Sha256);
        Assert.Equal(result.IndexEnvelope, File.ReadAllBytes(result.FeedPath));
    }

    [Fact]
    public void NullReviewArgumentPreservesOrdinaryChangedBeforePublicationMeaning()
    {
        using var f = new Fixture(); f.Put("data/catalog.dat", "before");
        string oldInstalled = Fixture.Hash(File.ReadAllBytes(Path.Combine(f.Source, "data/catalog.dat")));
        f.Put("data/catalog.dat", "after!");

        var result = ReleasePublisher.Publish(f.Options(), [new("data/catalog.dat", oldInstalled)],
            f.Key, Fixture.KeyId, null);

        var entry = Assert.Single(f.Verify(result.FullEnvelope).Manifest.Files);
        Assert.Equal(oldInstalled, entry.BaseSha256);
        Assert.Equal(Fixture.Hash(File.ReadAllBytes(Path.Combine(f.Source, "data/catalog.dat"))), entry.Sha256);
        Assert.NotEqual(oldInstalled, entry.Sha256);
    }

    [Fact]
    public void ExistingFourArgumentPublisherDelegateRemainsCallable()
    {
        using var f = new Fixture(); f.Put("data/catalog.dat", "synthetic legacy publisher asset");
        Func<ReleasePublishOptions, IEnumerable<PublishEntry>, System.Security.Cryptography.ECDsa,
            string, PublishedRelease> publish = ReleasePublisher.Publish;

        var result = publish(f.Options(), [new("data/catalog.dat", null)], f.Key, Fixture.KeyId);

        var entry = Assert.Single(f.Verify(result.FullEnvelope).Manifest.Files);
        Assert.Equal("data/catalog.dat", entry.Path);
        Assert.Equal(File.ReadAllBytes(Path.Combine(f.Source, entry.Path)), File.ReadAllBytes(f.Blob(entry.Sha256)));
    }
}
