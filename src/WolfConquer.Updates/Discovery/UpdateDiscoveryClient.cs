using System.Security.Cryptography;
using WolfConquer.Updates.Manifest;

namespace WolfConquer.Updates.Discovery;

public sealed record UpdateTrust(string ProductId, string Channel, string KeyId, string PublicKeySpkiPem,
    bool AllowLoopbackHttp = false);

public sealed record DiscoveredRelease(VerifiedUpdateRelease Release, byte[] FullEnvelope);

/// <summary>Downloads only authenticated metadata, never code instructions or credentials.</summary>
public sealed class UpdateDiscoveryClient(HttpClient client)
{
    private readonly HttpClient _client = client ?? throw new ArgumentNullException(nameof(client));

    public async Task<VerifiedUpdateManifest> FetchIndexAsync(Uri feed, UpdateTrust trust, long minimumSequence,
        CancellationToken cancellationToken = default)
    {
        ValidateFeed(feed, trust.AllowLoopbackHttp);
        var bytes = await Read(feed, 32768, null, cancellationToken).ConfigureAwait(false);
        var index = SignedUpdateManifest.VerifyEnvelope(bytes, trust.KeyId, trust.PublicKeySpkiPem,
            Context(trust, trust.ProductId + ".index", minimumSequence));
        VerifiedUpdateRelease.ValidateIndex(index);
        return index;
    }

    public async Task<DiscoveredRelease> FetchReleaseAsync(VerifiedUpdateManifest index, UpdateTrust trust,
        long minimumSequence, byte[]? cachedEnvelope = null, CancellationToken cancellationToken = default)
    {
        VerifiedUpdateRelease.ValidateIndex(index);
        ManifestValidation.Validate(index.Manifest, trust.AllowLoopbackHttp);
        ManifestValidation.ValidateContext(index.Manifest, Context(trust, trust.ProductId + ".index", minimumSequence));
        if (index.KeyId != trust.KeyId || index.SignerKeySha256 != SignedUpdateManifest.GetPublicKeySha256(trust.PublicKeySpkiPem))
            throw new InvalidDataException("Discovery signer is not the configured pinned key.");
        var entry = index.Manifest.Files[0];
        byte[] envelope;
        if (cachedEnvelope is not null && cachedEnvelope.LongLength == entry.Length &&
            Convert.ToHexString(SHA256.HashData(cachedEnvelope)).Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase))
            envelope = cachedEnvelope;
        else
            envelope = await Read(new Uri(index.Manifest.ContentBaseUrl, "blobs/" + entry.Sha256.ToLowerInvariant()),
                SignedUpdateManifest.MaximumEnvelopeBytes, entry.Length, cancellationToken).ConfigureAwait(false);
        var full = SignedUpdateManifest.VerifyEnvelope(envelope, trust.KeyId, trust.PublicKeySpkiPem,
            Context(trust, trust.ProductId, minimumSequence));
        return new DiscoveredRelease(VerifiedUpdateRelease.Bind(index, full), envelope);
    }

    private static UpdateVerificationContext Context(UpdateTrust trust, string product, long minimum) =>
        new(product, trust.Channel, minimum, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2), trust.AllowLoopbackHttp);

    private static void ValidateFeed(Uri feed, bool loopback)
    {
        if (!feed.IsAbsoluteUri || !(feed.Scheme == Uri.UriSchemeHttps || loopback && feed.Scheme == Uri.UriSchemeHttp && feed.IsLoopback) ||
            feed.UserInfo.Length != 0 || feed.Fragment.Length != 0 || feed.Query.Length != 0 ||
            feed.OriginalString.Any(character => char.IsControl(character) || char.IsWhiteSpace(character) || character == '\\'))
            throw new InvalidDataException("Discovery must use a credential-free HTTPS URL.");
    }

    private async Task<byte[]> Read(Uri uri, int maximum, long? exactLength, CancellationToken cancellation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromMinutes(3));
        using var response = await _client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
        if (response.StatusCode != System.Net.HttpStatusCode.OK || response.RequestMessage?.RequestUri != uri ||
            response.Content.Headers.ContentLength is > 0 && response.Content.Headers.ContentLength > maximum ||
            exactLength is not null && response.Content.Headers.ContentLength is { } advertised && advertised != exactLength)
            throw new InvalidDataException("Metadata response status, redirect or length is not permitted.");
        await using var input = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int count;
        while ((count = await input.ReadAsync(buffer, deadline.Token).ConfigureAwait(false)) != 0)
        {
            if (output.Length + count > maximum || exactLength is not null && output.Length + count > exactLength)
                throw new InvalidDataException("Metadata body exceeds its signed or configured bound.");
            output.Write(buffer, 0, count);
        }
        if (exactLength is not null && output.Length != exactLength)
            throw new InvalidDataException("Metadata body is truncated.");
        return output.ToArray();
    }
}
