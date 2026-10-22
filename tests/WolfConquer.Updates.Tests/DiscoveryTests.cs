using System.Net;
using System.Security.Cryptography;
using WolfConquer.Updates.Discovery;
using WolfConquer.Updates.Manifest;
using WolfConquer.Updates.Publishing;

namespace WolfConquer.Updates.Tests;

public sealed class DiscoveryTests
{
    [Fact]
    public void BoundIndexCanMoveTransportButCannotChangeReleaseIdentity()
    {
        using var fixture = new Fixture();
        var repointed = ReleasePublisher.RepointIndex(fixture.Full, "test", fixture.Key.ExportSubjectPublicKeyInfoPem(),
            fixture.Context("wolf-client"), new Uri("https://replacement.example/"), fixture.Key);
        var release = VerifiedUpdateRelease.Bind(fixture.Verify(repointed, "wolf-client.index"), fixture.Verify(fixture.Full, "wolf-client"));
        Assert.Equal("https://replacement.example/", release.ContentBaseUrl.AbsoluteUri);
        Assert.Equal(fixture.Full.LongLength, release.FullManifest.EnvelopeLength);
    }

    [Fact]
    public void SameKeyIdDoesNotMakeDifferentSignersEquivalent()
    {
        using var fixture = new Fixture();
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var index = fixture.IndexManifest();
        var foreignBytes = SignedUpdateManifest.CreateEnvelope(index, "test", other);
        var foreign = SignedUpdateManifest.VerifyEnvelope(foreignBytes, "test", other.ExportSubjectPublicKeyInfoPem(), fixture.Context("wolf-client.index"));
        Assert.Throws<InvalidDataException>(() => VerifiedUpdateRelease.Bind(foreign, fixture.Verify(fixture.Full, "wolf-client")));
    }

    [Theory]
    [InlineData("signer")]
    [InlineData("product")]
    [InlineData("sequence")]
    [InlineData("expiry")]
    public async Task ForeignOrStalePreverifiedIndexIsRejectedBeforeAnyNetworkRequest(string scenario)
    {
        using var fixture = new Fixture();
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var manifest = fixture.IndexManifest();
        manifest = new UpdateManifest(1, scenario == "product" ? "other.index" : manifest.ProductId, manifest.Channel,
            manifest.Sequence, scenario == "expiry" ? fixture.Now.AddDays(-2) : manifest.CreatedUtc,
            scenario == "expiry" ? fixture.Now.AddDays(-1) : manifest.ExpiresUtc, manifest.ContentBaseUrl, manifest.Files);
        var signer = scenario == "signer" ? other : fixture.Key;
        var bytes = SignedUpdateManifest.CreateEnvelope(manifest, "test", signer);
        var context = new UpdateVerificationContext(manifest.ProductId, "preview", 1,
            scenario == "expiry" ? fixture.Now.AddDays(-1).AddHours(-1) : fixture.Now);
        var verified = SignedUpdateManifest.VerifyEnvelope(bytes, "test", signer.ExportSubjectPublicKeyInfoPem(), context);
        using var handler = new ReplyHandler();
        using var http = new HttpClient(handler);
        await Assert.ThrowsAsync<InvalidDataException>(() => new UpdateDiscoveryClient(http).FetchReleaseAsync(verified,
            fixture.Trust, scenario == "sequence" ? 2 : 1));
        Assert.Equal(0, handler.Requests);
    }

    [Fact]
    public async Task ExactCachedEnvelopeAvoidsFullMetadataDownload()
    {
        using var fixture = new Fixture();
        using var handler = new ReplyHandler();
        using var http = new HttpClient(handler);
        var result = await new UpdateDiscoveryClient(http).FetchReleaseAsync(fixture.Verify(fixture.Index, "wolf-client.index"), fixture.Trust, 1, fixture.Full);
        Assert.Equal(0, handler.Requests);
        Assert.Equal(1, result.Release.FullManifest.Manifest.Sequence);
    }

    [Theory]
    [InlineData("redirect")]
    [InlineData("length")]
    [InlineData("tamper")]
    public async Task UnattributedOrInvalidFullResponsesAreRefused(string scenario)
    {
        using var fixture = new Fixture();
        using var handler = new ReplyHandler(request =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = scenario == "redirect" ? new HttpRequestMessage(HttpMethod.Get, "https://other.example/file") : request,
                Content = new ByteArrayContent(scenario == "tamper" ? new byte[fixture.Full.Length] : fixture.Full)
            };
            if (scenario == "length") response.Content.Headers.ContentLength = fixture.Full.Length + 1;
            return response;
        });
        using var http = new HttpClient(handler);
        await Assert.ThrowsAsync<InvalidDataException>(() => new UpdateDiscoveryClient(http).FetchReleaseAsync(
            fixture.Verify(fixture.Index, "wolf-client.index"), fixture.Trust, 1));
    }

    private sealed class ReplyHandler(Func<HttpRequestMessage, HttpResponseMessage>? reply = null) : HttpMessageHandler
    {
        internal int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Requests++; return Task.FromResult(reply?.Invoke(request) ?? throw new InvalidOperationException("Unexpected network request.")); }
    }

    private sealed class Fixture : IDisposable
    {
        internal ECDsa Key { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        internal DateTimeOffset Now { get; } = DateTimeOffset.UtcNow;
        internal byte[] Full { get; }
        internal byte[] Index { get; }
        internal UpdateTrust Trust => new("wolf-client", "preview", "test", Key.ExportSubjectPublicKeyInfoPem());
        internal Fixture()
        {
            Full = SignedUpdateManifest.CreateEnvelope(new UpdateManifest(1, "wolf-client", "preview", 1,
                Now.AddMinutes(-1), Now.AddDays(1), new Uri("https://original.example/"), []), "test", Key);
            Index = SignedUpdateManifest.CreateEnvelope(IndexManifest(), "test", Key);
        }
        internal UpdateManifest IndexManifest() => new(1, "wolf-client.index", "preview", 1,
            Now.AddMinutes(-1), Now.AddDays(1), new Uri("https://original.example/"),
            [new UpdateFileEntry("manifest.json", Full.LongLength, Convert.ToHexString(SHA256.HashData(Full)), null)]);
        internal UpdateVerificationContext Context(string product) => new(product, "preview", 1, Now);
        internal VerifiedUpdateManifest Verify(byte[] bytes, string product) =>
            SignedUpdateManifest.VerifyEnvelope(bytes, "test", Key.ExportSubjectPublicKeyInfoPem(), Context(product));
        public void Dispose() => Key.Dispose();
    }
}
