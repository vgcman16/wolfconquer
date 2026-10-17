using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

public sealed class LegacyAccServerCipherTests
{
    [Fact]
    public void BaseCipherEncryptsLegacyVector()
    {
        var plain = Convert.FromHexString("040096070102030405060708");
        var encrypted = plain.ToArray();
        var cipher = new LegacyAccServerCipher();

        cipher.Encrypt(encrypted);

        Assert.Equal(Convert.FromHexString("0548322219740F6EAD9043DA"), encrypted);
        Assert.Equal((ushort)plain.Length, cipher.EncryptCounter);

    }

    [Fact]
    public void BaseCipherDecryptUsesLegacyServerReceiveTransform()
    {
        var buffer = Convert.FromHexString("040096070102030405060708");
        var cipher = new LegacyAccServerCipher();

        cipher.Decrypt(buffer);

        Assert.Equal(Convert.FromHexString("0548322219740F6EAD9043DA"), buffer);
        Assert.Equal((ushort)buffer.Length, cipher.DecryptCounter);
    }

    [Fact]
    public void ServerOutboundCipherRoundTripsWithClientInverse()
    {
        var plain = Convert.FromHexString("040096070102030405060708");
        var buffer = plain.ToArray();
        var serverCipher = new LegacyAccServerCipher();
        var clientCipher = new LegacyAccServerCipher();

        serverCipher.Encrypt(buffer);
        clientCipher.DecryptFromServer(buffer);

        Assert.Equal(plain, buffer);
        Assert.Equal((ushort)plain.Length, clientCipher.EncryptCounter);
    }

    [Fact]
    public void ClientOutboundCipherRoundTripsWithServerReceiveTransform()
    {
        var plain = Convert.FromHexString("040096070102030405060708");
        var buffer = plain.ToArray();
        var clientCipher = new LegacyAccServerCipher();
        var serverCipher = new LegacyAccServerCipher();

        clientCipher.EncryptForServer(buffer);
        serverCipher.Decrypt(buffer);

        Assert.Equal(plain, buffer);
        Assert.Equal((ushort)plain.Length, clientCipher.DecryptCounter);
    }

    [Fact]
    public void GeneratedCryptoKeyDecryptsLegacyVector()
    {
        var buffer = Convert.FromHexString("AABBCCDD10203040");
        var cipher = new LegacyAccServerCipher();
        cipher.GenerateDecryptKeys(0x12345678u, 0x0013BEEDu);

        cipher.Decrypt(buffer);

        Assert.Equal(Convert.FromHexString("C3E3FB9F2446503A"), buffer);
    }

    [Fact]
    public void GeneratedByteKeysDecryptLegacyVector()
    {
        var buffer = Convert.FromHexString("AABBCCDD10203040");
        var cipher = new LegacyAccServerCipher();
        cipher.GenerateDecryptKeys(
            Convert.FromHexString("01020304"),
            Convert.FromHexString("A0B1C2D3"));

        cipher.Decrypt(buffer);

        Assert.Equal(Convert.FromHexString("D9B2E2EB3E17494E"), buffer);
    }

    [Fact]
    public void PasswordDecryptMatchesLegacyVector()
    {
        var buffer = Convert.FromHexString("414243444546");

        LegacyAccServerCipher.DecryptPasswordInPlace(buffer);

        Assert.Equal(Convert.FromHexString("1959D1DBECE1"), buffer);
    }

    [Fact]
    public void RivestCipher5GeneratesLegacySeedBytes()
    {
        Span<byte> seed = stackalloc byte[16];

        LegacyAccServerRivestCipher5.GenerateSeed(123456789, seed);

        Assert.Equal(Convert.FromHexString("CB5EEF9215F50ADD9B6FBE9C0E45F02F"), seed.ToArray());
    }

    [Fact]
    public void RivestCipher5EncryptsAndDecryptsDefaultLegacyVector()
    {
        var plain = Convert.FromHexString("000102030405060708090A0B0C0D0E0F");
        var buffer = plain.ToArray();
        var cipher = new LegacyAccServerRivestCipher5();

        Assert.True(cipher.Encrypt(buffer));

        Assert.Equal(Convert.FromHexString("D976DE208EB7895C0F1D4AEDB547547F"), buffer);

        Assert.True(cipher.Decrypt(buffer));
        Assert.Equal(plain, buffer);
    }

    [Fact]
    public void RivestCipher5EncryptsAndDecryptsSeededLegacyVector()
    {
        var plain = Convert.FromHexString("F0EFEEEDECEBEAE9E8E7E6E5E4E3E2E1");
        var buffer = plain.ToArray();
        var cipher = new LegacyAccServerRivestCipher5(123456789);

        Assert.True(cipher.Encrypt(buffer));

        Assert.Equal(Convert.FromHexString("B4F8DF30631CB13CDC9EE21FEB7A6A44"), buffer);

        Assert.True(cipher.Decrypt(buffer));
        Assert.Equal(plain, buffer);
    }

    [Fact]
    public void RivestCipher5RejectsNonBlockSizedBuffers()
    {
        var cipher = new LegacyAccServerRivestCipher5();
        Span<byte> buffer = stackalloc byte[7];

        Assert.False(cipher.Encrypt(buffer));
        Assert.False(cipher.Decrypt(buffer));
    }
}
