using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937EchoRuntimeCatalogTests
{
    private static byte[] ItemRows(IEnumerable<uint> ids, int fields = 67)
    {
        using var output = new MemoryStream();
        foreach (var id in ids)
        {
            for (var field = 0; field < fields; field++)
            {
                output.Write(field == 0 ? Encoding.ASCII.GetBytes(id.ToString(System.Globalization.CultureInfo.InvariantCulture)) :
                    field == 66 ? new byte[] { 0xff, 0x80 } : "0"u8);
                output.Write("@@"u8);
            }
            output.Write("\r\n"u8);
        }
        return output.ToArray();
    }

    private static ChineseFullClientCatalogSnapshot Capture(byte[]? encodedInfo = null,
        uint[]? main = null, uint[]? sub = null, int fields = 67, int version = 7937) =>
        ChineseFullClientCatalogIntake.ParseCaptured(version, Encoding.ASCII.GetBytes(version + "\r\n"),
            ClientItemCatalogCodec.Encode(ItemRows(main ?? [42, 43, 44], fields)),
            ClientItemCatalogCodec.Encode(ItemRows(sub ?? [7], fields)),
            [0xfe, 1, 2], encodedInfo ?? Chinese7937EchoRuntimeFixture.CompleteEncoded());

    private static Chinese7937EchoRuntimeCatalog Create(ChineseFullClientCatalogSnapshot? captured = null)
    {
        Assert.True(Chinese7937EchoRuntimeCatalog.TryCreate(captured ?? Capture(),
            Chinese7937EchoRuntimeFixture.CompleteEncoded(), new byte[12],
            SHA256.HashData(Chinese7937EchoRuntimeFixture.CompleteDecoded()), out var catalog, out var failure), failure.ToString());
        Assert.Equal(Chinese7937EchoRuntimeCatalogFailure.None, failure);
        return Assert.IsType<Chinese7937EchoRuntimeCatalog>(catalog);
    }

    [Fact]
    public void CompleteEncodedInfoBuildsRealTypedCatalogAndPreservesCapturedEvidence()
    {
        var captured = Capture();
        var catalog = Create(captured);
        Assert.Same(captured, catalog.Captured);
        Assert.Equal(3, catalog.MainItemCount);
        Assert.Equal(1, catalog.SubItemCount);
        Assert.Equal(59, catalog.Requirements.Source.Count);
        Assert.Equal(Chinese7937XuanbaoInfoVersion.Catalog7937, catalog.Requirements.Source.DeclaredVersion);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Chinese7937EchoRuntimeFixture.CompleteDecoded())).ToLowerInvariant(),
            catalog.Requirements.Source.ContentSha256);
        Assert.Equal(7, catalog.Requirements.CostRows.Count);
        Assert.Equal(6, catalog.Requirements.OpeningRows.Count);
        Assert.Equal(new Chinese7937EchoCostRow(1, 100, 42, 2), catalog.Requirements.CostRows[0]);
        Assert.Equal(new Chinese7937EchoOpeningRow(7, 70, 43, 77, 44, 84, 91), catalog.Requirements.OpeningRows[5]);
        Assert.Null(captured.DecodedEchoEvidence);
    }

    [Fact]
    public void ExactLookupKeepsMainSubAndBothWithoutChoosingPrecedence()
    {
        var catalog = Create(Capture(main: [42, 43, 44, 99], sub: [42, 7]));
        Assert.True(catalog.TryFindItem(42, out var both));
        Assert.Equal(42u, both.ItemType);
        Assert.NotNull(both.Main);
        Assert.NotNull(both.Sub);
        Assert.NotSame(both.Main, both.Sub);
        Assert.True(catalog.TryFindItem(99, out var main));
        Assert.NotNull(main.Main);
        Assert.Null(main.Sub);
        Assert.True(catalog.TryFindItem(7, out var sub));
        Assert.Null(sub.Main);
        Assert.NotNull(sub.Sub);
        Assert.False(catalog.TryFindItem(uint.MaxValue, out var missing));
        Assert.Equal(uint.MaxValue, missing.ItemType);
        Assert.False(missing.IsPresent);
        Assert.Null(missing.Main);
        Assert.Null(missing.Sub);
    }

    [Fact]
    public void RequiredReferencesCanResolveAcrossBothCatalogsWithoutInventedOwnership()
    {
        var catalog = Create(Capture(main: [43], sub: [42, 44]));
        Assert.True(catalog.TryPrepareActivationRequest(0, out var request));
        Assert.Null(request!.Item.Main);
        Assert.NotNull(request.Item.Sub);
        Assert.Equal(42u, request.Item.ItemType);
    }

    [Theory]
    [InlineData(42u)]
    [InlineData(43u)]
    [InlineData(44u)]
    public void EveryCostAndOpeningReferenceMustResolveBeforePublication(uint missing)
    {
        var main = new uint[] { 42, 43, 44 }.Where(id => id != missing).ToArray();
        Assert.False(Chinese7937EchoRuntimeCatalog.TryCreate(Capture(main: main),
            Chinese7937EchoRuntimeFixture.CompleteEncoded(), new byte[12],
            SHA256.HashData(Chinese7937EchoRuntimeFixture.CompleteDecoded()), out var catalog, out var failure));
        Assert.Null(catalog);
        Assert.Equal(Chinese7937EchoRuntimeCatalogFailure.MissingItemReference, failure);
    }

    [Fact]
    public void OpaqueFieldsAndReturnedBuffersDoNotBecomeMutableOrSemanticItemState()
    {
        var captured = Capture();
        var expected = captured.Findings[0].Chinese7937Items!.ToArray();
        var catalog = Create(captured);
        Assert.True(catalog.TryFindItem(42, out var item));
        Assert.Equal(new byte[] { 0xff, 0x80 }, item.Main!.GetField(66).ToArray());
        var exposed = item.Main.GetField(66).ToArray();
        Array.Clear(exposed);
        Assert.Equal(new byte[] { 0xff, 0x80 }, item.Main.GetField(66).ToArray());
        Assert.Equal(expected, captured.Findings[0].Chinese7937Items!.ToArray());
    }

    [Fact]
    public void CapturedEncodedInfoAndExplicitKeyAreBorrowedOnlyDuringPreparation()
    {
        var encoded = Chinese7937EchoRuntimeFixture.CompleteEncoded();
        var original = encoded.ToArray();
        var key = new byte[12];
        var pin = SHA256.HashData(Chinese7937EchoRuntimeFixture.CompleteDecoded());
        var captured = Capture(encoded);
        Assert.True(Chinese7937EchoRuntimeCatalog.TryCreate(captured, encoded, key, pin, out var result, out _));
        Assert.Equal(new byte[12], key);
        Assert.Equal(original, encoded);
        Array.Fill(encoded, (byte)0xff);
        Array.Fill(key, (byte)0xee);
        Array.Clear(pin);
        Assert.Equal(100u, result!.Requirements.CostRows[0].MoneyField);
        Assert.True(result.TryPrepareActivationRequest(0, out _));
    }
}
