using WolfConquer.Updates.Manifest;

namespace WolfConquer.Updates.Publishing;

public sealed record PublishEntry(string Path, string? BaseSha256);

public sealed record ReleasePublishOptions
{
    public string SourceRoot { get; }
    public string PublishRoot { get; }
    public string ProductId { get; }
    public string Channel { get; }
    public long Sequence { get; }
    public Uri ContentBaseUrl { get; }
    public DateTimeOffset CreatedUtc { get; }
    public DateTimeOffset ExpiresUtc { get; }
    public bool IncludeBundle { get; }
    public bool AllowLoopbackHttp { get; }

    public ReleasePublishOptions(string sourceRoot, string publishRoot, string productId, string channel,
        long sequence, Uri contentBaseUrl, DateTimeOffset createdUtc, DateTimeOffset expiresUtc,
        bool includeBundle = false, bool allowLoopbackHttp = false)
    {
        SourceRoot = sourceRoot; PublishRoot = publishRoot; ProductId = productId; Channel = channel;
        Sequence = sequence; ContentBaseUrl = contentBaseUrl; CreatedUtc = createdUtc; ExpiresUtc = expiresUtc;
        IncludeBundle = includeBundle; AllowLoopbackHttp = allowLoopbackHttp;
    }
}

public sealed class PublishedRelease
{
    private readonly byte[] fullEnvelope;
    private readonly byte[] indexEnvelope;
    public byte[] FullEnvelope => (byte[])fullEnvelope.Clone();
    public byte[] IndexEnvelope => (byte[])indexEnvelope.Clone();
    public string FullEnvelopeSha256 { get; }
    public UpdateManifest Manifest { get; }
    public string FeedPath { get; }

    internal PublishedRelease(byte[] full, byte[] index, string hash, UpdateManifest manifest, string feedPath)
    {
        fullEnvelope = (byte[])full.Clone(); indexEnvelope = (byte[])index.Clone();
        FullEnvelopeSha256 = hash; Manifest = manifest; FeedPath = feedPath;
    }
}
