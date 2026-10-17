using System.Text;
using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

public sealed class LegacyGameServerCipherTests
{
    private static readonly byte[] LoginKey = Encoding.ASCII.GetBytes("AS19RayF54ZO1D6P");

    [Fact]
    public void LoginKeyEncryptsLegacyVector()
    {
        var plain = Enumerable.Range(0, 20).Select(value => (byte)value).ToArray();
        var encrypted = new byte[plain.Length];
        var cipher = new LegacyGameServerCipher(LoginKey);

        cipher.Encrypt(plain, encrypted);

        Assert.Equal(Convert.FromHexString("52DBE08A74D35020FBB6BDBD713B30A9E7552028"), encrypted);
    }

    [Fact]
    public void LoginKeyDecryptsLegacyVector()
    {
        var encrypted = Convert.FromHexString("A9F7191697997A9251B22F97027E0E40525B367C");
        var decrypted = new byte[encrypted.Length];
        var cipher = new LegacyGameServerCipher(LoginKey);

        cipher.Decrypt(encrypted, decrypted);

        Assert.Equal(Convert.FromHexString("FB2DFB9FE74F2CB5918D0C9B8F47F224C5A2486D"), decrypted);
    }

    [Fact]
    public void StreamingEncryptMatchesLegacyChunkedVector()
    {
        var plain = Enumerable.Range(0, 20).Select(value => (byte)value).ToArray();
        var encrypted = new byte[plain.Length];
        var cipher = new LegacyGameServerCipher(LoginKey);

        cipher.Encrypt(plain.AsSpan(0, 5), encrypted.AsSpan(0, 5));
        cipher.Encrypt(plain.AsSpan(5), encrypted.AsSpan(5));

        Assert.Equal(Convert.FromHexString("52DBE08A74D35020FBB6BDBD713B30A9E7552028"), encrypted);
    }

    [Fact]
    public void ResetRestoresCipherFeedbackState()
    {
        var plain = Enumerable.Range(0, 20).Select(value => (byte)value).ToArray();
        var first = new byte[plain.Length];
        var second = new byte[plain.Length];
        var cipher = new LegacyGameServerCipher(LoginKey);

        cipher.Encrypt(plain, first);
        cipher.Reset();
        cipher.Encrypt(plain, second);

        Assert.Equal(first, second);
    }

    [Fact]
    public void InPlaceEncryptAndDecryptRoundTripAcrossMatchingDirections()
    {
        var plain = Convert.FromHexString("00112233445566778899AABBCCDDEEFF");
        var buffer = plain.ToArray();
        var encryptCipher = new LegacyGameServerCipher(LoginKey);
        var decryptCipher = new LegacyGameServerCipher(LoginKey);

        encryptCipher.EncryptInPlace(buffer);
        decryptCipher.DecryptInPlace(buffer);

        Assert.Equal(plain, buffer);
    }

    [Fact]
    public void DerivedKeyEncryptsLegacyVector()
    {
        var plain = Convert.FromHexString("FFEEDDCCBBAA99887766554433221100");
        var encrypted = new byte[plain.Length];
        var cipher = new LegacyGameServerCipher(Encoding.ASCII.GetBytes("0123456789abcdef0123456789abcdef"));

        cipher.Encrypt(plain, encrypted);

        Assert.Equal(Convert.FromHexString("5C052EC51A7767BF542A07ACA08F9B07"), encrypted);
    }

    [Fact]
    public void DiffieHellmanPostProcessingMatchesLegacyHashPipeline()
    {
        var sharedSecret = new byte[] { 1, 2, 3, 0, 4, 5 };
        Span<byte> processed = stackalloc byte[LegacyGameServerCipher.DiffieHellmanProcessedKeySize];
        Span<byte> keyMaterial = stackalloc byte[LegacyGameServerCipher.KeyMaterialSize];

        var processedLength = LegacyGameServerCipher.PostProcessDiffieHellmanKey(sharedSecret, processed);
        var keyLength = LegacyGameServerCipher.DeriveKeyMaterialFromDiffieHellmanSharedSecret(sharedSecret, keyMaterial);

        Assert.Equal(LegacyGameServerCipher.DiffieHellmanProcessedKeySize, processedLength);
        Assert.Equal(
            "5289df737df57326fcdd22597afb1fac282e3823be63e182a4fbbc74483a41d0",
            Encoding.ASCII.GetString(processed));
        Assert.Equal(LegacyGameServerCipher.KeyMaterialSize, keyLength);
        Assert.Equal("5289df737df57326", Encoding.ASCII.GetString(keyMaterial));
    }

    [Fact]
    public void TransformRequiresKeyAndValidBuffers()
    {
        var cipher = new LegacyGameServerCipher();
        var plain = Convert.FromHexString("00112233");
        var tooSmall = new byte[plain.Length - 1];

        Assert.Throws<InvalidOperationException>(() => cipher.Encrypt(plain, new byte[plain.Length]));
        Assert.Throws<ArgumentException>(() => new LegacyGameServerCipher(LoginKey[..15]));
        Assert.Throws<ArgumentException>(() => new LegacyGameServerCipher(LoginKey).Encrypt(plain, tooSmall));
    }
}
