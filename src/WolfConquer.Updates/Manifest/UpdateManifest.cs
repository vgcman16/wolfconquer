namespace WolfConquer.Updates.Manifest;

public sealed record UpdateFileEntry(string Path, long Length, string Sha256, string? BaseSha256);

// Optional ZIP transport only. The Files list remains the authority for every extracted byte.
public sealed record UpdateBundle(long Length, string Sha256);

public sealed record UpdateManifest
{
    public int SchemaVersion { get; }
    public string ProductId { get; }
    public string Channel { get; }
    public long Sequence { get; }
    public DateTimeOffset CreatedUtc { get; }
    public DateTimeOffset ExpiresUtc { get; }
    public Uri ContentBaseUrl { get; }
    public IReadOnlyList<UpdateFileEntry> Files { get; }
    public UpdateBundle? Bundle { get; }

    public UpdateManifest(int schemaVersion, string productId, string channel, long sequence,
        DateTimeOffset createdUtc, DateTimeOffset expiresUtc, Uri contentBaseUrl,
        IEnumerable<UpdateFileEntry> files, UpdateBundle? bundle = null)
    {
        SchemaVersion = schemaVersion;
        ProductId = productId;
        Channel = channel;
        Sequence = sequence;
        CreatedUtc = createdUtc;
        ExpiresUtc = expiresUtc;
        ContentBaseUrl = contentBaseUrl;
        Files = Array.AsReadOnly((files ?? throw new ArgumentNullException(nameof(files))).ToArray());
        Bundle = bundle;
    }
}

public sealed record UpdateVerificationContext
{
    public string ExpectedProductId { get; }
    public string ExpectedChannel { get; }
    // Inclusive lower bound; persist the last accepted sequence outside the untrusted feed.
    public long MinimumSequence { get; }
    public DateTimeOffset NowUtc { get; }
    // Skew permits a slightly future creation time, never extends the expiry deadline.
    public TimeSpan AllowedClockSkew { get; }
    public bool AllowLoopbackHttp { get; }

    public UpdateVerificationContext(string expectedProductId, string expectedChannel,
        long minimumSequence, DateTimeOffset nowUtc, TimeSpan? allowedClockSkew = null,
        bool allowLoopbackHttp = false)
    {
        ExpectedProductId = expectedProductId;
        ExpectedChannel = expectedChannel;
        MinimumSequence = minimumSequence;
        NowUtc = nowUtc;
        AllowedClockSkew = allowedClockSkew ?? TimeSpan.Zero;
        AllowLoopbackHttp = allowLoopbackHttp;
    }
}

// Only verification can construct this externally consumable installer input.
public sealed record VerifiedUpdateManifest
{
    public UpdateManifest Manifest { get; }
    public string PayloadSha256 { get; }
    public string KeyId { get; }
    public string EnvelopeSha256 { get; }
    public long EnvelopeLength { get; }
    public string SignerKeySha256 { get; }

    internal VerifiedUpdateManifest(UpdateManifest manifest, string payloadSha256, string keyId,
        string envelopeSha256, long envelopeLength, string signerKeySha256)
    {
        Manifest = manifest;
        PayloadSha256 = payloadSha256;
        KeyId = keyId;
        EnvelopeSha256 = envelopeSha256;
        EnvelopeLength = envelopeLength;
        SignerKeySha256 = signerKeySha256;
    }
}
