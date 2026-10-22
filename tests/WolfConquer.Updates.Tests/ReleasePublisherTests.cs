using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using WolfConquer.Updates.Manifest;
using WolfConquer.Updates.Publishing;
using Xunit;

namespace WolfConquer.Updates.Tests;

public sealed partial class ReleasePublisherTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CuratedReleaseAndSignedIndexReferenceOnlyVerifiedImmutableBlobs(bool bundle)
    {
        using var f = new Fixture();
        f.Put("Conquer.exe", "synthetic client"); f.Put("ini/catalog.dat", "catalog");
        f.Put("private-unlisted.dat", "never package this");
        string previous = new('B', 64);
        var result = ReleasePublisher.Publish(f.Options(bundle: bundle),
            [new("Conquer.exe", null), new("ini/catalog.dat", previous)], f.Key, Fixture.KeyId);
        var full = f.Verify(result.FullEnvelope);
        var index = f.Verify(result.IndexEnvelope, index: true);
        Assert.Equal(result.FullEnvelopeSha256, full.EnvelopeSha256);
        Assert.Equal(result.FullEnvelope.LongLength, full.EnvelopeLength);
        Assert.Equal(result.FullEnvelope, File.ReadAllBytes(f.Blob(full.EnvelopeSha256)));
        Assert.Equal(result.IndexEnvelope, File.ReadAllBytes(result.FeedPath));
        Assert.Equal("game-stable.json", Path.GetFileName(result.FeedPath));
        Assert.Equal(new[] { "Conquer.exe", "ini/catalog.dat" }, full.Manifest.Files.Select(file => file.Path));
        Assert.Null(full.Manifest.Files[0].BaseSha256);
        Assert.Equal(previous, full.Manifest.Files[1].BaseSha256);
        foreach (var file in full.Manifest.Files)
        {
            byte[] bytes = File.ReadAllBytes(f.Blob(file.Sha256));
            Assert.Equal(file.Length, bytes.LongLength);
            Assert.Equal(file.Sha256, Fixture.Hash(bytes));
            Assert.Equal(File.ReadAllBytes(Path.Combine(f.Source, file.Path)), bytes);
        }
        Assert.False(File.Exists(f.Blob(Fixture.Hash(Encoding.UTF8.GetBytes("never package this")))));
        var pointer = Assert.Single(index.Manifest.Files);
        Assert.Equal("manifest.json", pointer.Path); Assert.Null(pointer.BaseSha256);
        Assert.Equal(full.EnvelopeSha256, pointer.Sha256); Assert.Equal(full.EnvelopeLength, pointer.Length);
        Assert.Equal(full.Manifest.Sequence, index.Manifest.Sequence);
        Assert.Null(index.Manifest.Bundle);
        Assert.Equal(bundle, full.Manifest.Bundle is not null);
        if (bundle)
        {
            var transport = full.Manifest.Bundle!;
            byte[] bytes = File.ReadAllBytes(f.Blob(transport.Sha256));
            Assert.Equal(transport.Length, bytes.LongLength); Assert.Equal(transport.Sha256, Fixture.Hash(bytes));
            using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            Assert.Equal(full.Manifest.Files.Select(file => file.Path), zip.Entries.Select(entry => entry.FullName));
            foreach (var entry in zip.Entries)
            {
                Assert.False(entry.FullName.EndsWith('/'));
                using var output = new MemoryStream(); using var input = entry.Open(); input.CopyTo(output);
                Assert.Equal(File.ReadAllBytes(Path.Combine(f.Source, entry.FullName)), output.ToArray());
            }
        }
        byte[] mutable = result.FullEnvelope; mutable[0] ^= 1;
        Assert.Equal(full.EnvelopeSha256, Fixture.Hash(result.FullEnvelope));
        Assert.Empty(Directory.GetFiles(f.Publish, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void IdenticalContentIsDeduplicatedAndExistingValidBlobsAreNotRewritten()
    {
        using var f = new Fixture(); f.Put("one.dat", "same"); f.Put("two.dat", "same");
        string hash = Fixture.Hash(Encoding.UTF8.GetBytes("same"));
        Directory.CreateDirectory(Path.Combine(f.Publish, "blobs"));
        File.WriteAllText(f.Blob(hash), "same", new UTF8Encoding(false));
        DateTime stamp = new(2020, 1, 2, 3, 4, 6, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(f.Blob(hash), stamp);
        var result = ReleasePublisher.Publish(f.Options(), [new("one.dat", null), new("two.dat", null)], f.Key, Fixture.KeyId);
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(f.Blob(hash)));
        Assert.Equal(2, Directory.GetFiles(Path.Combine(f.Publish, "blobs")).Length); // Content + full envelope.
        Assert.All(result.Manifest.Files, file => Assert.Equal(hash, file.Sha256));
    }

    [Fact]
    public void RepointChangesOnlyTheTinyIndexAndRequiresTheSamePinnedSigningKey()
    {
        using var f = new Fixture(); f.Put("one.dat", "one");
        var result = ReleasePublisher.Publish(f.Options(bundle: true), [new("one.dat", null)], f.Key, Fixture.KeyId);
        byte[] before = result.FullEnvelope;
        var newBase = new Uri("https://replacement.example.test/downloads/");
        byte[] repointed = ReleasePublisher.RepointIndex(before, Fixture.KeyId, f.PublicKey, f.Context(), newBase, f.Key);
        var index = f.Verify(repointed, index: true);
        Assert.Equal(newBase, index.Manifest.ContentBaseUrl);
        var pointer = Assert.Single(index.Manifest.Files);
        Assert.Equal(Fixture.Hash(before), pointer.Sha256); Assert.Equal(before.LongLength, pointer.Length);
        Assert.Equal(before, File.ReadAllBytes(f.Blob(pointer.Sha256)));
        Assert.Equal(result.IndexEnvelope, File.ReadAllBytes(result.FeedPath)); // Pure builder; no implicit feed mutation.
        Assert.Equal(new Uri("https://original.example.test/content/"), f.Verify(before).Manifest.ContentBaseUrl);
        using var wrong = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Assert.Throws<InvalidDataException>(() => ReleasePublisher.RepointIndex(before, Fixture.KeyId,
            f.PublicKey, f.Context(), newBase, wrong));
        Assert.Throws<InvalidDataException>(() => ReleasePublisher.RepointIndex(before, Fixture.KeyId,
            f.PublicKey, f.Context(minimum: 2), newBase, f.Key));
        Assert.Throws<InvalidDataException>(() => ReleasePublisher.RepointIndex(before, Fixture.KeyId,
            f.PublicKey, f.Context(), new Uri("http://untrusted.example.test/"), f.Key));
    }

    private sealed class Fixture : IDisposable
    {
        internal const string KeyId = "ephemeral-publisher-key";
        internal static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-publish-tests-" + Guid.NewGuid().ToString("N"));
        internal string Source => Path.Combine(Root, "source");
        internal string Publish => Path.Combine(Root, "published");
        internal ECDsa Key { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        internal string PublicKey => Key.ExportSubjectPublicKeyInfoPem();
        internal Fixture() => Directory.CreateDirectory(Source);
        internal void Put(string relative, string contents)
        {
            string path = Path.Combine(Source, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, contents, new UTF8Encoding(false));
        }
        internal string Blob(string hash) => Path.Combine(Publish, "blobs", hash.ToLowerInvariant());
        internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
        internal ReleasePublishOptions Options(bool bundle = false, string? source = null, string? publish = null) =>
            new(source ?? Source, publish ?? Publish, "game", "stable", 1,
                new Uri("https://original.example.test/content/"), Now.AddMinutes(-1), Now.AddHours(1), bundle);
        internal UpdateVerificationContext Context(bool index = false, long minimum = 1) =>
            new(index ? "game.index" : "game", "stable", minimum, Now);
        internal VerifiedUpdateManifest Verify(byte[] envelope, bool index = false) =>
            SignedUpdateManifest.VerifyEnvelope(envelope, KeyId, PublicKey, Context(index));
        public void Dispose()
        {
            Key.Dispose();
            string path = Path.GetFullPath(Root);
            if (!path.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(path).StartsWith("wolf-publish-tests-", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing unsafe fixture cleanup.");
            Directory.Delete(path, recursive: true);
        }
    }
}
