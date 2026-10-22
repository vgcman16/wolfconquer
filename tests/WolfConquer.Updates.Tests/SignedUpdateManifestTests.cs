using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WolfConquer.Updates.Manifest;
using Xunit;

namespace WolfConquer.Updates.Tests;

public sealed class SignedUpdateManifestTests : IDisposable
{
    private const string KeyId = "ephemeral-test-key";
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly string Hash = new('A', 64);
    private readonly ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    public void Dispose() => key.Dispose();

    [Fact]
    public void ExactSignedPayloadBytesAndPinnedIdentityProduceAnImmutableVerifiedInput()
    {
        var entries = new[] { new UpdateFileEntry("Conquer.exe", 17, Hash.ToLowerInvariant(), null) };
        var manifest = Model(entries);
        entries[0] = new("changed.dat", 99, Hash, Hash);
        byte[] canonical = Payload(manifest);
        byte[] exact = Encoding.UTF8.GetBytes(" \n" + Encoding.UTF8.GetString(canonical) + "\n");
        byte[] signedExact = SignRaw(exact);
        var verified = Verify(signedExact);
        Assert.Equal(KeyId, verified.KeyId);
        Assert.Equal(SignedUpdateManifest.GetPublicKeySha256(key.ExportSubjectPublicKeyInfoPem()), verified.SignerKeySha256);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(signedExact)), verified.EnvelopeSha256);
        Assert.Equal(signedExact.LongLength, verified.EnvelopeLength);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(exact)), verified.PayloadSha256);
        Assert.NotEqual(Convert.ToHexString(SHA256.HashData(canonical)), verified.PayloadSha256);
        Assert.Equal(7, verified.Manifest.Sequence);
        var file = Assert.Single(verified.Manifest.Files);
        Assert.Equal("Conquer.exe", file.Path);
        Assert.Null(file.BaseSha256);
        Assert.Equal(Hash.ToLowerInvariant(), file.Sha256);
        Assert.Null(verified.Manifest.Bundle);
        Assert.Empty(typeof(VerifiedUpdateManifest).GetConstructors());
        var list = Assert.IsAssignableFrom<IList<UpdateFileEntry>>(verified.Manifest.Files);
        Assert.Throws<NotSupportedException>(() => list[0] = entries[0]);
    }

    [Fact]
    public void OptionalBundleIsSignedImmutableTransportWithoutReplacingTheFilesList()
    {
        var bundle = new UpdateBundle(12986623874L, Hash.ToLowerInvariant());
        var manifest = Model(bundle: bundle);
        var verified = Verify(SignedUpdateManifest.CreateEnvelope(manifest, KeyId, key));
        Assert.Equal(bundle, verified.Manifest.Bundle);
        Assert.Equal("ini/catalog.dat", Assert.Single(verified.Manifest.Files).Path);
        Assert.Equal("https://updates.example.test/releases/blobs/" + Hash.ToLowerInvariant(),
            new Uri(verified.Manifest.ContentBaseUrl, "blobs/" + verified.Manifest.Bundle!.Sha256.ToLowerInvariant()).AbsoluteUri);
        Assert.Null(Verify(Mutate(root => root["bundle"] = null)).Manifest.Bundle);

        var envelope = JsonNode.Parse(SignedUpdateManifest.CreateEnvelope(manifest, KeyId, key))!.AsObject();
        var payload = JsonNode.Parse(Convert.FromBase64String(envelope["payload"]!.GetValue<string>()))!.AsObject();
        payload["bundle"]!["length"] = 1;
        envelope["payload"] = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(payload));
        var error = Assert.Throws<InvalidDataException>(() => Verify(JsonSerializer.SerializeToUtf8Bytes(envelope)));
        Assert.Contains("signature is invalid", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("negative-length")]
    [InlineData("bad-hash")]
    [InlineData("missing-hash")]
    [InlineData("unknown-url")]
    [InlineData("case-property")]
    [InlineData("wrong-kind")]
    public void AuthenticatedInvalidBundleIsRejected(string kind)
    {
        byte[] envelope = Mutate(root =>
        {
            var bundle = new JsonObject { ["length"] = 20L, ["sha256"] = Hash };
            root["bundle"] = bundle;
            switch (kind)
            {
                case "negative-length": bundle["length"] = -1; break;
                case "bad-hash": bundle["sha256"] = "not-a-hash"; break;
                case "missing-hash": bundle.Remove("sha256"); break;
                case "unknown-url": bundle["url"] = "https://untrusted.example.test/archive.zip"; break;
                case "case-property": bundle["Length"] = 20L; bundle.Remove("length"); break;
                case "wrong-kind": root["bundle"] = new JsonArray(); break;
            }
        });
        Assert.Throws<InvalidDataException>(() => Verify(envelope));
        Assert.Throws<InvalidDataException>(() => SignedUpdateManifest.CreateEnvelope(
            Model(bundle: new UpdateBundle(-1, Hash)), KeyId, key));
        Assert.Throws<InvalidDataException>(() => SignedUpdateManifest.CreateEnvelope(
            Model(bundle: new UpdateBundle(0, "bad")), KeyId, key));
    }

    [Fact]
    public void DuplicateBundleOrBundlePropertiesAreRejected()
    {
        string payload = Encoding.UTF8.GetString(Payload(Model(bundle: new UpdateBundle(20, Hash))));
        string duplicateRoot = payload.Replace("\"bundle\":", "\"bundle\":null,\"bundle\":", StringComparison.Ordinal);
        string duplicateLength = payload.Replace("\"length\":20", "\"length\":20,\"length\":20", StringComparison.Ordinal);
        Assert.NotEqual(payload, duplicateRoot);
        Assert.NotEqual(payload, duplicateLength);
        Assert.Throws<InvalidDataException>(() => Verify(SignRaw(Encoding.UTF8.GetBytes(duplicateRoot))));
        Assert.Throws<InvalidDataException>(() => Verify(SignRaw(Encoding.UTF8.GetBytes(duplicateLength))));
    }

    [Fact]
    public void SuppliedSigningKeyRemainsUsableAndSignatureIsFixedP1363()
    {
        byte[] envelope = SignedUpdateManifest.CreateEnvelope(Model(), KeyId, key);
        using var json = JsonDocument.Parse(envelope);
        byte[] payload = Convert.FromBase64String(json.RootElement.GetProperty("payload").GetString()!);
        byte[] signature = Convert.FromBase64String(json.RootElement.GetProperty("signature").GetString()!);
        Assert.Equal(64, signature.Length);
        Assert.True(key.VerifyData(payload, signature, HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        Assert.Equal(7, Verify(envelope, Context(minimum: 7)).Manifest.Sequence);
        Assert.NotEmpty(key.ExportSubjectPublicKeyInfo()); // Helper never disposes the caller's key.
    }

    [Fact]
    public void PayloadTamperFailsSignatureBeforeMalformedJsonIsInterpreted()
    {
        byte[] payload = Payload();
        byte[] envelope = SignRaw(payload);
        var document = JsonNode.Parse(envelope)!.AsObject();
        document["payload"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("not-json"));
        var error = Assert.Throws<InvalidDataException>(() => Verify(JsonSerializer.SerializeToUtf8Bytes(document)));
        Assert.Contains("signature is invalid", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WrongPinnedKeyKeyIdOrSignatureCannotAuthorizeThePayload()
    {
        byte[] envelope = SignRaw(Payload());
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Assert.Throws<InvalidDataException>(() => SignedUpdateManifest.VerifyEnvelope(envelope, KeyId,
            other.ExportSubjectPublicKeyInfoPem(), Context()));
        Assert.Throws<InvalidDataException>(() => SignedUpdateManifest.VerifyEnvelope(envelope, "wrong-key",
            key.ExportSubjectPublicKeyInfoPem(), Context()));
        var fields = JsonNode.Parse(envelope)!.AsObject();
        byte[] signature = Convert.FromBase64String(fields["signature"]!.GetValue<string>());
        signature[0] ^= 1;
        fields["signature"] = Convert.ToBase64String(signature);
        Assert.Throws<InvalidDataException>(() => Verify(JsonSerializer.SerializeToUtf8Bytes(fields)));
    }

    [Fact]
    public void DerSignaturesOtherCurvesPrivatePemAndExtraPemAreRejected()
    {
        byte[] payload = Payload();
        byte[] der = key.SignData(payload, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        Assert.Throws<InvalidDataException>(() => Verify(Envelope(payload, der)));
        using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        Assert.Throws<InvalidDataException>(() => SignedUpdateManifest.CreateEnvelope(Model(), KeyId, p384));
        byte[] valid = SignRaw(payload);
        foreach (string pem in new[] { p384.ExportSubjectPublicKeyInfoPem(), key.ExportECPrivateKeyPem(),
                     key.ExportSubjectPublicKeyInfoPem() + key.ExportSubjectPublicKeyInfoPem() })
            Assert.Throws<InvalidDataException>(() => SignedUpdateManifest.VerifyEnvelope(valid, KeyId, pem, Context()));
    }

    [Theory]
    [InlineData("wrong-product", "stable", 1)]
    [InlineData("wolf-test", "preview", 1)]
    [InlineData("WOLF-TEST", "stable", 1)]
    [InlineData("wolf-test", "STABLE", 1)]
    [InlineData("wolf-test", "stable", 8)]
    public void ProductChannelAndDowngradeChecksUseTrustedContext(string product, string channel, long minimum)
    {
        Assert.Throws<InvalidDataException>(() => Verify(SignRaw(Payload()), Context(product, channel, minimum)));
    }

    [Fact]
    public void ExpiryIsStrictAndOnlyFutureCreationAcceptsExplicitBoundedClockSkew()
    {
        byte[] expired = Mutate(root => root["expiresUtc"] = Now.ToString("O"));
        Assert.Throws<InvalidDataException>(() => Verify(expired));
        byte[] future = Mutate(root => root["createdUtc"] = Now.AddMinutes(1).ToString("O"));
        Assert.Throws<InvalidDataException>(() => Verify(future));
        Assert.Equal(Now.AddMinutes(1), Verify(future, Context(skew: TimeSpan.FromMinutes(1))).Manifest.CreatedUtc);
        Assert.Throws<InvalidDataException>(() => Verify(expired, Context(skew: TimeSpan.FromMinutes(5))));
        Assert.Throws<InvalidDataException>(() => Verify(SignRaw(Payload()), Context(skew: TimeSpan.FromMinutes(-1))));
        Assert.Throws<InvalidDataException>(() => Verify(SignRaw(Payload()), Context(skew: TimeSpan.FromMinutes(6))));
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("sequence")]
    [InlineData("expiry-order")]
    [InlineData("non-utc")]
    [InlineData("missing-zone")]
    [InlineData("negative-length")]
    [InlineData("bad-hash")]
    [InlineData("bad-base")]
    [InlineData("missing-base")]
    [InlineData("unknown-root")]
    [InlineData("unknown-file")]
    [InlineData("case-root")]
    public void AuthenticatedButInvalidSchemaIsRejected(string kind)
    {
        byte[] envelope = Mutate(root =>
        {
            var file = root["files"]!.AsArray()[0]!.AsObject();
            switch (kind)
            {
                case "schema": root["schemaVersion"] = 2; break;
                case "sequence": root["sequence"] = 0; break;
                case "expiry-order": root["expiresUtc"] = Now.AddDays(-1).ToString("O"); break;
                case "non-utc": root["createdUtc"] = Now.AddMinutes(-1).ToOffset(TimeSpan.FromHours(2)).ToString("O"); break;
                case "missing-zone": root["createdUtc"] = "2026-10-22T11:59:00"; break;
                case "negative-length": file["length"] = -1; break;
                case "bad-hash": file["sha256"] = new string('G', 64); break;
                case "bad-base": file["baseSha256"] = ""; break;
                case "missing-base": file.Remove("baseSha256"); break;
                case "unknown-root": root["algorithm"] = "none"; break;
                case "unknown-file": file["url"] = "https://untrusted.example.test/blob"; break;
                case "case-root": root["ProductId"] = root["productId"]!.DeepClone(); root.Remove("productId"); break;
            }
        });
        Assert.Throws<InvalidDataException>(() => Verify(envelope));
    }

    [Fact]
    public void DuplicatePropertiesMalformedJsonAndNoncanonicalBase64FailClosed()
    {
        string payload = Encoding.UTF8.GetString(Payload());
        string duplicateRoot = payload.Replace("\"productId\":\"wolf-test\"", "\"productId\":\"wolf-test\",\"productId\":\"wolf-test\"", StringComparison.Ordinal);
        string duplicateFile = payload.Replace("\"length\":17", "\"length\":17,\"length\":17", StringComparison.Ordinal);
        Assert.NotEqual(payload, duplicateRoot);
        Assert.NotEqual(payload, duplicateFile);
        foreach (string invalid in new[] { duplicateRoot, duplicateFile, "null", "[]", "{}", "{", payload + "garbage" })
            Assert.Throws<InvalidDataException>(() => Verify(SignRaw(Encoding.UTF8.GetBytes(invalid))));
        string envelope = Encoding.UTF8.GetString(SignRaw(Payload()));
        Assert.Throws<InvalidDataException>(() => Verify(Encoding.UTF8.GetBytes(envelope.Replace(
            "\"keyId\":", "\"unknown\":0,\"keyId\":", StringComparison.Ordinal))));
        Assert.Throws<InvalidDataException>(() => Verify(Encoding.UTF8.GetBytes(envelope.Replace(
            "\"keyId\":", "\"keyId\":\"ephemeral-test-key\",\"keyId\":", StringComparison.Ordinal))));
        var node = JsonNode.Parse(envelope)!.AsObject();
        node["signature"] = node["signature"]!.GetValue<string>() + "\n";
        Assert.Throws<InvalidDataException>(() => Verify(JsonSerializer.SerializeToUtf8Bytes(node)));
    }

    [Fact]
    public void UnsafeWindowsPathsAndReservedTrustPathsAreRejectedBeforeInstallerUse()
    {
        string[] paths = ["/x", "C:/x", "a\\b", "../x", "a/./x", "a//x", "a/", "a/name.", "a/name ",
            "a/CON.txt", "con .txt", "Lpt9.bin", "com\u00b9.dat", "a\u0001b", "file:stream", "file?name", ".GIT/config",
            "a/.wolfconquer-updates/journal", "wolfconquer-updates.json", "trusted-release-keys.json",
            "WolfConquer.Updater.exe", "wolfconquer.updater.dll", "WolfConquer.Updater.deps.json",
            "WolfConquer.Updater.runtimeconfig.json", "trusted-release-keys.json/child"];
        foreach (string path in paths)
        {
            Assert.Throws<InvalidDataException>(() => UpdatePathPolicy.ValidateRelativePath(path));
            Assert.Throws<InvalidDataException>(() => Verify(Mutate(root => root["files"]![0]!["path"] = path)));
        }
        UpdatePathPolicy.ValidateRelativePath("Conquer.exe");
        UpdatePathPolicy.ValidateRelativePath("ini/sub/catalog.dat");
    }

    [Theory]
    [InlineData("INI/catalog.dat", "ini/CATALOG.dat")]
    [InlineData("ini", "INI/catalog.dat")]
    public void FileCollisionsAndFileDirectoryConflictsAreRejected(string first, string second)
    {
        byte[] envelope = Mutate(root =>
        {
            var files = root["files"]!.AsArray();
            files[0]!["path"] = first;
            var duplicate = files[0]!.DeepClone();
            duplicate["path"] = second;
            files.Add(duplicate);
        });
        Assert.Throws<InvalidDataException>(() => Verify(envelope));
    }

    [Theory]
    [InlineData("http://updates.example.test/")]
    [InlineData("https://user:pass@updates.example.test/")]
    [InlineData("https://updates.example.test/?q=1")]
    [InlineData("https://updates.example.test/#fragment")]
    [InlineData("https://updates.example.test/not-a-directory")]
    [InlineData("file:///tmp/blobs/")]
    public void UnsafeContentBaseUrlsAreRejected(string uri)
    {
        Assert.Throws<InvalidDataException>(() => Verify(Mutate(root => root["contentBaseUrl"] = uri)));
    }

    [Fact]
    public void HttpLoopbackRequiresCallerDevelopmentOptInAtBothBuildAndVerify()
    {
        var model = Model(baseUrl: new Uri("http://127.0.0.1:8080/feed/"));
        Assert.Throws<InvalidDataException>(() => SignedUpdateManifest.CreateEnvelope(model, KeyId, key));
        byte[] envelope = SignedUpdateManifest.CreateEnvelope(model, KeyId, key, allowLoopbackHttp: true);
        Assert.Throws<InvalidDataException>(() => Verify(envelope));
        Assert.Equal(model.ContentBaseUrl, Verify(envelope, Context(http: true)).Manifest.ContentBaseUrl);
        Assert.Throws<InvalidDataException>(() => Verify(Mutate(root => root["contentBaseUrl"] = "http://updates.example.test/"), Context(http: true)));
    }

    [Fact]
    public void PayloadAndEnvelopeLimitsAreEnforced()
    {
        Assert.Equal(64 * 1024 * 1024, SignedUpdateManifest.MaximumPayloadBytes);
        Assert.Equal(96 * 1024 * 1024, SignedUpdateManifest.MaximumEnvelopeBytes);
        Assert.Throws<InvalidDataException>(() => Verify(new byte[SignedUpdateManifest.MaximumEnvelopeBytes + 1]));
        Assert.Throws<InvalidDataException>(() => Verify(SignRaw(new byte[SignedUpdateManifest.MaximumPayloadBytes + 1])));
    }

    [Fact]
    public void FullClientFileCountIsSupportedButStillBounded()
    {
        Assert.Equal(200000, SignedUpdateManifest.MaximumFiles);
        var files = Enumerable.Range(0, SignedUpdateManifest.MaximumFiles)
            .Select(i => new UpdateFileEntry($"data/{i}.bin", 0, Hash, null)).ToArray();
        Assert.Equal(200000, Verify(SignedUpdateManifest.CreateEnvelope(Model(files), KeyId, key)).Manifest.Files.Count);
        Assert.Throws<InvalidDataException>(() => SignedUpdateManifest.CreateEnvelope(Model(files.Append(files[0])), KeyId, key));
        Assert.Throws<InvalidDataException>(() => Verify(Mutate(root =>
        {
            // Count is checked before individual entries are interpreted; avoid a second huge valid model.
            var entries = new JsonArray();
            root["files"] = entries;
            for (int i = 0; i <= SignedUpdateManifest.MaximumFiles; i++) entries.Add((JsonNode?)null);
        })));
    }

    private UpdateManifest Model(IEnumerable<UpdateFileEntry>? files = null, Uri? baseUrl = null,
        UpdateBundle? bundle = null) => new(
        1, "wolf-test", "stable", 7, Now.AddMinutes(-1), Now.AddHours(1),
        baseUrl ?? new Uri("https://updates.example.test/releases/"),
        files ?? [new UpdateFileEntry("ini/catalog.dat", 17, Hash, new string('b', 64))], bundle);

    private static UpdateVerificationContext Context(string product = "wolf-test", string channel = "stable",
        long minimum = 1, TimeSpan? skew = null, bool http = false) => new(product, channel, minimum, Now, skew, http);

    private VerifiedUpdateManifest Verify(byte[] envelope, UpdateVerificationContext? context = null) =>
        SignedUpdateManifest.VerifyEnvelope(envelope, KeyId, key.ExportSubjectPublicKeyInfoPem(), context ?? Context());

    private byte[] Payload(UpdateManifest? model = null)
    {
        using var document = JsonDocument.Parse(SignedUpdateManifest.CreateEnvelope(model ?? Model(), KeyId, key));
        return Convert.FromBase64String(document.RootElement.GetProperty("payload").GetString()!);
    }

    private byte[] Mutate(Action<JsonObject> mutate)
    {
        var node = JsonNode.Parse(Payload())!.AsObject();
        mutate(node);
        return SignRaw(JsonSerializer.SerializeToUtf8Bytes(node));
    }

    private byte[] SignRaw(byte[] payload) => Envelope(payload, key.SignData(payload, HashAlgorithmName.SHA256,
        DSASignatureFormat.IeeeP1363FixedFieldConcatenation));

    private static byte[] Envelope(byte[] payload, byte[] signature) => JsonSerializer.SerializeToUtf8Bytes(
        new Dictionary<string, string> { ["keyId"] = KeyId, ["payload"] = Convert.ToBase64String(payload),
            ["signature"] = Convert.ToBase64String(signature) });
}
