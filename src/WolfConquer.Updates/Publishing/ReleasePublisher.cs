using System.Security.Cryptography;
using WolfConquer.Updates.Manifest;

namespace WolfConquer.Updates.Publishing;

public static partial class ReleasePublisher
{
    private sealed record SourceFile(PublishEntry Entry, string FullPath, long Length, DateTime LastWriteUtc,
        string SourceRoot, NativeAssetReview? Review);

    public static PublishedRelease Publish(ReleasePublishOptions options, IEnumerable<PublishEntry> plan,
        ECDsa signingKey, string keyId) => Publish(options, plan, signingKey, keyId, null);

    public static PublishedRelease Publish(ReleasePublishOptions options, IEnumerable<PublishEntry> plan,
        ECDsa signingKey, string keyId, IEnumerable<NativeAssetReview>? nativeAssetReviews)
    {
        ArgumentNullException.ThrowIfNull(options); ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(signingKey);
        NativeAssetReviewSet admission = NativeAssetReviewSet.Create(plan, nativeAssetReviews);
        PublishEntry[] entries = admission.Entries;
        string source = PublishFileSystem.Root(options.SourceRoot, mustExist: true);
        string publish = PublishFileSystem.Root(options.PublishRoot, mustExist: false);
        if (PublishFileSystem.Contains(source, publish) || PublishFileSystem.Contains(publish, source))
            throw new InvalidDataException("Source and publication roots must not overlap.");
        ManifestValidation.Identifier(options.ProductId + ".index");
        // Validate all plan paths/collisions/base hashes and the signer before any output writes.
        var preview = MakeManifest(options, entries.Select(entry => new UpdateFileEntry(entry.Path, 0,
            new string('0', 64), entry.BaseSha256)));
        ManifestValidation.Validate(preview, options.AllowLoopbackHttp);
        _ = SignedUpdateManifest.CreateEnvelope(MakeManifest(options, []), keyId, signingKey, options.AllowLoopbackHttp);
        NativeAssetSource.ValidateMembership(source, admission.Reviews);
        SourceFile[] sources = entries.Select(entry => Inspect(source, entry, admission.For(entry.Path))).ToArray();
        var sizedPreview = MakeManifest(options, sources.Select(item => new UpdateFileEntry(item.Entry.Path,
            item.Length, new string('0', 64), item.Entry.BaseSha256)),
            options.IncludeBundle ? new UpdateBundle(long.MaxValue, new string('0', 64)) : null);
        if (ManifestJson.WritePayload(sizedPreview).Length > SignedUpdateManifest.MaximumPayloadBytes)
            throw new InvalidDataException("Curated manifest exceeds the signed payload bound.");
        string blobs = Path.Combine(publish, "blobs");
        string feeds = Path.Combine(publish, "feeds");
        PublishFileSystem.Check(blobs); PublishFileSystem.Check(feeds);
        string feed = Path.Combine(feeds, options.ProductId + "-" + options.Channel + ".json");
        PublishFileSystem.Check(feed);
        if (Directory.Exists(feed)) throw new IOException("Feed path names a directory.");
        PublishFileSystem.EnsureDirectory(blobs);
        var files = sources.Select(item => CopySource(item, blobs)).ToArray();
        NativeAssetSource.ValidateMembership(source, admission.Reviews);
        UpdateBundle? bundle = options.IncludeBundle ? BuildBundle(blobs, files) : null;
        var manifest = MakeManifest(options, files, bundle);
        byte[] full = SignedUpdateManifest.CreateEnvelope(manifest, keyId, signingKey, options.AllowLoopbackHttp);
        string hash = StoreBytes(blobs, full);
        byte[] index = CreateIndex(manifest, full.LongLength, hash, options.ContentBaseUrl,
            signingKey, keyId, options.AllowLoopbackHttp);
        PublishFileSystem.EnsureDirectory(feeds);
        WriteFeed(feed, index); // The only mutable publication: all referenced content is ready first.
        return new PublishedRelease(full, index, hash, manifest, feed);
    }

    public static byte[] RepointIndex(ReadOnlySpan<byte> existingFullEnvelope, string keyId,
        string pinnedPublicKeySpkiPem, UpdateVerificationContext context, Uri newContentBaseUrl, ECDsa signingKey)
    {
        var verified = SignedUpdateManifest.VerifyEnvelope(existingFullEnvelope, keyId, pinnedPublicKeySpkiPem, context);
        byte[] index = CreateIndex(verified.Manifest, verified.EnvelopeLength, verified.EnvelopeSha256,
            newContentBaseUrl, signingKey, keyId, context.AllowLoopbackHttp);
        // A different signing key may not repoint a release authenticated by the pinned key.
        _ = SignedUpdateManifest.VerifyEnvelope(index, keyId, pinnedPublicKeySpkiPem,
            new UpdateVerificationContext(context.ExpectedProductId + ".index", context.ExpectedChannel,
                context.MinimumSequence, context.NowUtc, context.AllowedClockSkew, context.AllowLoopbackHttp));
        return index;
    }

    private static UpdateManifest MakeManifest(ReleasePublishOptions options, IEnumerable<UpdateFileEntry> files,
        UpdateBundle? bundle = null) => new(1, options.ProductId, options.Channel, options.Sequence,
            options.CreatedUtc, options.ExpiresUtc, options.ContentBaseUrl, files, bundle);

    private static byte[] CreateIndex(UpdateManifest release, long length, string hash, Uri baseUrl,
        ECDsa key, string keyId, bool allowLoopbackHttp) => SignedUpdateManifest.CreateEnvelope(new UpdateManifest(
            1, release.ProductId + ".index", release.Channel, release.Sequence, release.CreatedUtc, release.ExpiresUtc,
            baseUrl, [new UpdateFileEntry("manifest.json", length, hash, null)]), keyId, key, allowLoopbackHttp);

    private static SourceFile Inspect(string root, PublishEntry entry, NativeAssetReview? review)
    {
        string path = review is null ? PublishFileSystem.Contained(root, entry.Path) :
            NativeAssetSource.ExactFile(root, entry.Path);
        using var stream = PublishFileSystem.Read(path);
        long length = stream.Length;
        DateTime lastWrite = File.GetLastWriteTimeUtc(path);
        if (review is not null)
        {
            NativeAssetSource.VerifyDigest(stream, review);
            _ = NativeAssetSource.ExactFile(root, entry.Path);
            if (File.GetLastWriteTimeUtc(path) != lastWrite)
                throw new InvalidDataException("Reviewed source changed during preflight.");
        }
        return new SourceFile(entry, path, length, lastWrite, root, review);
    }

    private static void WriteFeed(string destination, byte[] bytes)
    {
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = PublishFileSystem.New(temporary)) { output.Write(bytes); output.Flush(true); }
            PublishFileSystem.Check(destination);
            if (File.Exists(destination)) File.Replace(temporary, destination, null);
            else File.Move(temporary, destination);
            PublishFileSystem.Check(destination);
        }
        finally { PublishFileSystem.DeleteOwnedTemporary(temporary); }
    }
}
