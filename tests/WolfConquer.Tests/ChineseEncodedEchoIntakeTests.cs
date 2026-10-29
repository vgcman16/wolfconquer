using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ChineseEncodedEchoIntakeTests
{
    // Independent synthetic zero-key fixture; generator checks the published Crypto++ zero-key known answer first.
    internal static byte[] Encoded() => Convert.FromHexString(
        "d48e4c409a325d5f7347a7e3d5e0d903b289f02b57882c95fc751ce067c70097cc68fe900100e54b" +
        "f19c588a8e845a9bd6b29db6538adebb1d370b8de78b7e56f2df9ce887c11b6cbe35c3f565d06aee" +
        "6f1548d370e2300c3d5d54f00e59e5bd03f09a98341d85049634b6175d96c1f11020cba6475c2bd2ef" +
        "85179b974214d5e7caa47fdfbacb657501287cfb25fa272021263a742631206f");
    internal static byte[] Decoded() => Encoding.ASCII.GetBytes(
        "local ret = {}; ret.cq_xuanbao_huying_extra_attr = { [1] = { attr_type = 17, " +
        "attr_value = 42, extra_attr_type = 6, extra_attr_value = 99 } }; return ret;");

    [Fact]
    public void CompleteExplicitTransformAndPinsProduceOwnedTypedRows()
    {
        var encoded = Encoded(); var decoded = Decoded(); var key = new byte[12];
        Assert.True(Chinese7935DecodedEchoCatalog.TryParseEncoded(encoded, key, Chinese7935EchoCatalogVersion.Catalog7931,
            SHA256.HashData(encoded), SHA256.HashData(decoded), out var snapshot, out var failure), failure.ToString());
        var result = Assert.IsType<Chinese7935EchoCatalogSnapshot>(snapshot);
        Assert.Equal(new Chinese7935EchoCatalogRow(1, 17, 42, 6, 99), result.Rows[0]);
        Assert.Equal(1, result.Count); Assert.Equal(Chinese7935EchoCatalogVersion.Catalog7931, result.DeclaredVersion);
        Array.Fill(encoded, (byte)0xff); Array.Fill(key, (byte)0xee); Array.Clear(decoded);
        Assert.Equal(99U, result.Rows[0].ExtraAttributeValue);
    }

    [Theory]
    [InlineData(0)] [InlineData(11)] [InlineData(13)]
    public void MissingOrWrongWidthKeyNeverUsesADefault(int length)
    {
        var bytes = Encoded();
        Assert.False(Chinese7935DecodedEchoCatalog.TryParseEncoded(bytes, new byte[length], Chinese7935EchoCatalogVersion.Catalog7931,
            SHA256.HashData(bytes), SHA256.HashData(Decoded()), out var snapshot, out var failure));
        Assert.Null(snapshot); Assert.Equal(Chinese7935EchoCatalogFailure.KeyRequired, failure);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void EncodedAndDecodedMismatchBothRefuseWithoutPublishing(bool mismatchEncoded)
    {
        var bytes = Encoded(); var rawPin = SHA256.HashData(bytes); var decodedPin = SHA256.HashData(Decoded());
        if (mismatchEncoded) rawPin[0] ^= 1; else decodedPin[0] ^= 1;
        Assert.False(Chinese7935DecodedEchoCatalog.TryParseEncoded(bytes, new byte[12], Chinese7935EchoCatalogVersion.Catalog7931,
            rawPin, decodedPin, out var snapshot, out var failure));
        Assert.Null(snapshot); Assert.Equal(Chinese7935EchoCatalogFailure.PinMismatch, failure);
    }

    [Fact]
    public void IncorrectExplicitKeyIsNeitherGuessedNorClearedInTheCallersArray()
    {
        var bytes = Encoded(); var key = new byte[12]; key[0] = 1;
        Assert.False(Chinese7935DecodedEchoCatalog.TryParseEncoded(bytes, key, Chinese7935EchoCatalogVersion.Catalog7931,
            SHA256.HashData(bytes), SHA256.HashData(Decoded()), out var snapshot, out var failure));
        Assert.Null(snapshot); Assert.Equal(Chinese7935EchoCatalogFailure.PinMismatch, failure); Assert.Equal((byte)1, key[0]);
    }

    [Fact]
    public void ClientVersionDoesNotInventANewLiteralSchemaAndPinsRemainRequired()
    {
        var bytes = Encoded();
        Assert.False(Chinese7935DecodedEchoCatalog.TryParseEncoded(bytes, new byte[12], (Chinese7935EchoCatalogVersion)7937,
            SHA256.HashData(bytes), SHA256.HashData(Decoded()), out var snapshot, out var failure));
        Assert.Null(snapshot); Assert.Equal(Chinese7935EchoCatalogFailure.UnsupportedVersion, failure);
        Assert.False(Chinese7935DecodedEchoCatalog.TryParseEncoded(bytes, new byte[12], Chinese7935EchoCatalogVersion.Catalog7931,
            new byte[31], SHA256.HashData(Decoded()), out snapshot, out failure));
        Assert.Null(snapshot); Assert.Equal(Chinese7935EchoCatalogFailure.PinRequired, failure);
    }

    [Fact]
    public void EmptyOversizedOrPinnedNonModuleDataNeverBecomesAnAcceptedCatalog()
    {
        foreach (var bytes in new[] { Array.Empty<byte>(), new byte[Chinese7935DecodedEchoCatalog.MaximumBytes + 1] })
        {
            Assert.False(Chinese7935DecodedEchoCatalog.TryParseEncoded(bytes, new byte[12], Chinese7935EchoCatalogVersion.Catalog7931,
                SHA256.HashData(bytes), new byte[32], out var rejected, out var failure));
            Assert.Null(rejected); Assert.Equal(Chinese7935EchoCatalogFailure.InputBounds, failure);
        }
        // Published zero-key block decodes to binary data, not a literal module, even with correct pins.
        var encoded = Convert.FromHexString("6EC75940C49DAE83F7EC21AD");
        var binary = Convert.FromHexString("010000000100000001000000");
        Assert.False(Chinese7935DecodedEchoCatalog.TryParseEncoded(encoded, new byte[12], Chinese7935EchoCatalogVersion.Catalog7931,
            SHA256.HashData(encoded), SHA256.HashData(binary), out var snapshot, out var reason));
        Assert.Null(snapshot); Assert.NotEqual(Chinese7935EchoCatalogFailure.None, reason);
    }
}
