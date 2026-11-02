using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937XuanbaoRuleCatalogsTests
{
    [Theory]
    [InlineData("1@@0@@1@@400@@\n")]
    [InlineData("1@@0@@1@@400@@\r")]
    [InlineData("1@@0@@1@@400@@")]
    [InlineData("1@@0@@1@@400@@\r\n\r\n")]
    [InlineData("\r\n1@@0@@1@@400@@\r\n")]
    [InlineData("1@@0@@1@@400\r\n")]
    [InlineData("1@@0@@1@@400@@5@@\r\n")]
    [InlineData("1@@0@@1@@\r\n")]
    [InlineData("1@@0@@1@@-1@@\r\n")]
    [InlineData("1@@0@@1@@+1@@\r\n")]
    [InlineData("1@@0@@1@@ 1@@\r\n")]
    [InlineData("1@@0@@1@@1.0@@\r\n")]
    [InlineData("1@@0@@1@@0x10@@\r\n")]
    [InlineData("@@0@@1@@400@@\r\n")]
    [InlineData("header\r\n1@@0@@1@@400@@\r\n")]
    public void UnsupportedCompleteTableFormatsCannotBePartiallyAccepted(string malformed)
    {
        var packed = Chinese7937XuanbaoRulesFixture.Pack(malformed);
        Refuses(packed, Chinese7937XuanbaoRulesFixture.RandomPacked(), Chinese7937XuanbaoRulesFailure.InvalidTable);
        Refuses(Chinese7937XuanbaoRulesFixture.ComposePacked(), packed, Chinese7937XuanbaoRulesFailure.InvalidTable);
    }

    [Fact]
    public void DuplicateNumericIdentitiesRefuseEvenWhenRawSpellingsDiffer()
    {
        Refuses(Chinese7937XuanbaoRulesFixture.Pack("1@@0@@1@@400@@\r\n0001@@0@@2@@300@@\r\n"),
            Chinese7937XuanbaoRulesFixture.RandomPacked(), Chinese7937XuanbaoRulesFailure.DuplicateComposeId);
        Refuses(Chinese7937XuanbaoRulesFixture.ComposePacked(),
            Chinese7937XuanbaoRulesFixture.Pack("0042@@003@@100@@201@@\r\n42@@3@@1@@101@@\r\n"),
            Chinese7937XuanbaoRulesFailure.DuplicateRandomKey);
    }

    [Fact]
    public void EveryNumericColumnRefusesAnInt32Overflow()
    {
        for (var column = 0; column < 4; column++)
        {
            var fields = new[] { "1", "0", "1", "400" }; fields[column] = "2147483648";
            var packed = Chinese7937XuanbaoRulesFixture.Pack(string.Join("@@", fields) + "@@\r\n");
            Refuses(packed, Chinese7937XuanbaoRulesFixture.RandomPacked(), Chinese7937XuanbaoRulesFailure.NumericWidth);
            Refuses(Chinese7937XuanbaoRulesFixture.ComposePacked(), packed, Chinese7937XuanbaoRulesFailure.NumericWidth);
        }
    }

    [Fact]
    public void NulHighBytesAndBomHaveNoTextRepairOrFallback()
    {
        var row = Encoding.ASCII.GetBytes("1@@0@@1@@400@@\r\n");
        foreach (var bytes in new byte[][] { [0xef, 0xbb, 0xbf, .. row], [0xff, 0xfe, .. row], [.. row, 0], [.. row, 0x81] })
            Refuses(ClientItemCatalogCodec.Encode(bytes), Chinese7937XuanbaoRulesFixture.RandomPacked(), Chinese7937XuanbaoRulesFailure.InvalidTable);
    }

    [Theory]
    [InlineData(7899)]
    [InlineData(7931)]
    [InlineData(7938)]
    public void OtherDeclaredVersionsCannotBorrowThisSemanticTableContract(int version)
    {
        Assert.False(Chinese7937XuanbaoRuleCatalogs.TryParsePacked(version,
            Chinese7937XuanbaoRulesFixture.ComposePacked(), Chinese7937XuanbaoRulesFixture.RandomPacked(),
            new byte[32], new byte[32], out var catalogs, out var failure));
        Assert.Null(catalogs); Assert.Equal(Chinese7937XuanbaoRulesFailure.UnsupportedVersion, failure);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(33)]
    public void BothPackedPinsMustBeExplicitAndFullWidth(int length)
    {
        var compose = Chinese7937XuanbaoRulesFixture.ComposePacked(); var random = Chinese7937XuanbaoRulesFixture.RandomPacked();
        foreach (var badCompose in new[] { true, false })
        {
            Assert.False(Chinese7937XuanbaoRuleCatalogs.TryParsePacked(7937, compose, random,
                badCompose ? new byte[length] : SHA256.HashData(compose),
                badCompose ? SHA256.HashData(random) : new byte[length], out var catalogs, out var failure));
            Assert.Null(catalogs); Assert.Equal(Chinese7937XuanbaoRulesFailure.PinRequired, failure);
        }
    }

    [Fact]
    public void EitherWrongPinRefusesBeforePublishingOneTable()
    {
        var compose = Chinese7937XuanbaoRulesFixture.ComposePacked(); var random = Chinese7937XuanbaoRulesFixture.RandomPacked();
        foreach (var badCompose in new[] { true, false })
        {
            var composePin = SHA256.HashData(compose); var randomPin = SHA256.HashData(random);
            (badCompose ? composePin : randomPin)[0] ^= 1;
            Assert.False(Chinese7937XuanbaoRuleCatalogs.TryParsePacked(7937, compose, random, composePin, randomPin, out var catalogs, out var failure));
            Assert.Null(catalogs); Assert.Equal(Chinese7937XuanbaoRulesFailure.PinMismatch, failure);
        }
    }

    [Fact]
    public void NonemptyAndFiniteInputBoundsApplyToEachPackedCatalog()
    {
        foreach (var bad in new[] { Array.Empty<byte>(), new byte[Chinese7937XuanbaoRuleCatalogs.MaximumPackedBytes + 1] })
        {
            Refuses(bad, Chinese7937XuanbaoRulesFixture.RandomPacked(), Chinese7937XuanbaoRulesFailure.InputBounds);
            Refuses(Chinese7937XuanbaoRulesFixture.ComposePacked(), bad, Chinese7937XuanbaoRulesFailure.InputBounds);
        }
    }

    private static void Refuses(byte[] compose, byte[] random, Chinese7937XuanbaoRulesFailure expected)
    {
        Assert.False(Chinese7937XuanbaoRuleCatalogs.TryParsePacked(7937, compose, random,
            SHA256.HashData(compose), SHA256.HashData(random), out var catalogs, out var failure));
        Assert.Null(catalogs); Assert.Equal(expected, failure);
    }
}
