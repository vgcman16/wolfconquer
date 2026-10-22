using System.Security.Cryptography;
using System.Text.Json;

namespace WolfConquer.Updates.Manifest;

public static class SignedUpdateManifest
{
    public const int MaximumPayloadBytes = 64 * 1024 * 1024;
    public const int MaximumFiles = 200000;
    public const int MaximumEnvelopeBytes = 96 * 1024 * 1024;
    private const DSASignatureFormat SignatureFormat = DSASignatureFormat.IeeeP1363FixedFieldConcatenation;

    // The caller supplies an in-memory signing key. This helper creates/stores no keys.
    public static byte[] CreateEnvelope(UpdateManifest manifest, string keyId, ECDsa signingKey,
        bool allowLoopbackHttp = false)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(signingKey);
        ManifestValidation.Identifier(keyId);
        ManifestValidation.Validate(manifest, allowLoopbackHttp);
        RequireP256(signingKey);
        byte[] payload = ManifestJson.WritePayload(manifest);
        if (payload.Length > MaximumPayloadBytes) throw new InvalidDataException("Manifest payload exceeds limit.");
        byte[] signature = signingKey.SignData(payload, HashAlgorithmName.SHA256, SignatureFormat);
        if (signature.Length != 64) throw new InvalidDataException("Unexpected signature size.");
        using var output = new MemoryStream();
        using (var json = new Utf8JsonWriter(output))
        {
            json.WriteStartObject();
            json.WriteString("keyId", keyId);
            json.WriteBase64String("payload", payload);
            json.WriteBase64String("signature", signature);
            json.WriteEndObject();
        }
        return output.ToArray();
    }

    public static VerifiedUpdateManifest VerifyEnvelope(ReadOnlySpan<byte> envelopeUtf8,
        string expectedKeyId, string pinnedPublicKeySpkiPem, UpdateVerificationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            if (envelopeUtf8.Length is 0 or > MaximumEnvelopeBytes)
                throw new InvalidDataException("Signed envelope size is invalid.");
            ManifestValidation.Identifier(expectedKeyId);
            using var envelope = ManifestJson.Read(envelopeUtf8);
            var fields = ManifestJson.Fields(envelope.RootElement, "keyId", "payload", "signature");
            string keyId = ManifestJson.Text(fields["keyId"]);
            if (!string.Equals(keyId, expectedKeyId, StringComparison.Ordinal))
                throw new InvalidDataException("The manifest key ID is not pinned.");
            byte[] payload = DecodeBase64(ManifestJson.Text(fields["payload"]), MaximumPayloadBytes);
            byte[] signature = DecodeBase64(ManifestJson.Text(fields["signature"]), 64);
            if (payload.Length == 0 || signature.Length != 64)
                throw new InvalidDataException("Payload or P1363 signature size is invalid.");
            using var key = ReadPinnedKey(pinnedPublicKeySpkiPem);
            if (!key.VerifyData(payload, signature, HashAlgorithmName.SHA256, SignatureFormat))
                throw new InvalidDataException("Manifest signature is invalid.");
            // Only exact, authenticated payload bytes reach schema/path/time interpretation.
            var manifest = ManifestJson.ReadPayload(payload);
            ManifestValidation.Validate(manifest, context.AllowLoopbackHttp);
            ManifestValidation.ValidateContext(manifest, context);
            return new VerifiedUpdateManifest(manifest, Convert.ToHexString(SHA256.HashData(payload)), keyId,
                Convert.ToHexString(SHA256.HashData(envelopeUtf8)), envelopeUtf8.Length,
                Convert.ToHexString(SHA256.HashData(key.ExportSubjectPublicKeyInfo())));
        }
        catch (Exception error) when (error is JsonException or FormatException or CryptographicException or
                                      ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new InvalidDataException("Signed manifest verification failed.", error);
        }
    }

    public static string GetPublicKeySha256(string pinnedPublicKeySpkiPem)
    {
        using var key = ReadPinnedKey(pinnedPublicKeySpkiPem);
        return Convert.ToHexString(SHA256.HashData(key.ExportSubjectPublicKeyInfo()));
    }

    private static byte[] DecodeBase64(string value, int maximum)
    {
        if (value.Length > ((maximum + 2) / 3) * 4) throw new InvalidDataException("Base64 field exceeds limit.");
        byte[] decoded = Convert.FromBase64String(value);
        if (decoded.Length > maximum || Convert.ToBase64String(decoded) != value)
            throw new InvalidDataException("Base64 field is not canonical or exceeds limit.");
        return decoded;
    }

    private static ECDsa ReadPinnedKey(string pem)
    {
        if (pem is null || pem.Length > 16384 || !PemEncoding.TryFind(pem, out var fields) ||
            !pem.AsSpan()[fields.Label].SequenceEqual("PUBLIC KEY"))
            throw new InvalidDataException("Pinned key must be a single SPKI PUBLIC KEY PEM.");
        var (start, length) = fields.Location.GetOffsetAndLength(pem.Length);
        if (!pem.AsSpan(0, start).Trim().IsEmpty || !pem.AsSpan(start + length).Trim().IsEmpty)
            throw new InvalidDataException("Pinned PEM contains additional data.");
        byte[] der = Convert.FromBase64String(pem.AsSpan()[fields.Base64Data].ToString());
        var key = ECDsa.Create();
        try
        {
            key.ImportSubjectPublicKeyInfo(der, out int bytesRead);
            if (bytesRead != der.Length) throw new InvalidDataException("Pinned SPKI has trailing data.");
            RequireP256(key);
            return key;
        }
        catch { key.Dispose(); throw; }
    }

    private static void RequireP256(ECDsa key)
    {
        if (key.KeySize != 256 || key.ExportParameters(false).Curve.Oid.Value != "1.2.840.10045.3.1.7")
            throw new InvalidDataException("Only ECDSA NIST P-256 is supported.");
    }
}
