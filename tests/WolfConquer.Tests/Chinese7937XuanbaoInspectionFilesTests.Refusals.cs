using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937XuanbaoInspectionFilesTests
{
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void CompleteMalformedPinnedTailReportsTheActualParserWithoutPartialPublication(bool info) => WithFixture(f =>
    {
        var bad = info ? f.Info.Concat(Encoding.ASCII.GetBytes("XuanBaoResolve.GoldNum = 1\n")).ToArray()
            : ClientItemCatalogCodec.Encode(Encoding.ASCII.GetBytes("1@@2@@3@@4@@5@@\r\n2@@2@@3@@4@@bad@@\r\n"));
        File.WriteAllBytes(info ? f.InfoPath : f.AdditionPath, bad);
        Assert.False(Chinese7937DecodedXuanbaoInfo.TryLoadResolveAndAdditionFiles(f.InfoPath, f.AdditionPath, 7937,
            info ? SHA256.HashData(bad) : f.InfoPin, info ? f.AdditionPin : SHA256.HashData(bad),
            out var resolve, out var addition, out var failure));
        Assert.Null(resolve); Assert.Null(addition);
        Assert.StartsWith(info ? "ResolveParser:" : "AdditionParser:", failure, StringComparison.Ordinal);
        Assert.DoesNotContain(f.Root, failure, StringComparison.OrdinalIgnoreCase); f.AssertReleased();
    });

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void RequiredVersionPinsAndAbsoluteFilesRefuseBeforeOpeningAnyInput(int kind) => WithFixture(f =>
    {
        int version = kind == 0 ? 7931 : 7937;
        var infoPath = kind == 2 ? "relative-info.lua" : kind == 3 ? f.Root : f.InfoPath;
        var infoPin = kind == 1 ? new byte[31] : f.InfoPin;
        Assert.False(Chinese7937DecodedXuanbaoInfo.TryLoadResolveAndAdditionFiles(infoPath, f.AdditionPath, version,
            infoPin, f.AdditionPin, out var resolve, out var addition, out var failure));
        Assert.Null(resolve); Assert.Null(addition);
        Assert.Equal(new[] { "UnsupportedVersion", "PinRequired", "PathRequired", "UnsafePath" }[kind], failure);
        f.AssertReleased();
    });

    [Theory]
    [InlineData(true, false)] [InlineData(true, true)] [InlineData(false, false)] [InlineData(false, true)]
    public void BothFiniteCaptureBoundsRejectEmptyOrOversizedFilesWithoutPartialData(bool info, bool oversized) => WithFixture(f =>
    {
        byte[] bytes = oversized ? new byte[(info ? Chinese7937DecodedXuanbaoInfo.MaximumBytes : Chinese7937XuanbaoAdditionRawCatalog.MaximumBytes) + 1] : [];
        File.WriteAllBytes(info ? f.InfoPath : f.AdditionPath, bytes);
        Assert.False(Chinese7937DecodedXuanbaoInfo.TryLoadResolveAndAdditionFiles(f.InfoPath, f.AdditionPath, 7937,
            info ? SHA256.HashData(bytes) : f.InfoPin, info ? f.AdditionPin : SHA256.HashData(bytes),
            out var resolve, out var addition, out var failure));
        Assert.Null(resolve); Assert.Null(addition); Assert.Equal("InputBounds", failure); f.AssertReleased();
    });

    [Fact]
    public void MissingFileAndUncDeviceInputsNeverResolveToADefault() => WithFixture(f =>
    {
        foreach (var path in new[] { Path.Combine(f.Root, "missing-info"), "\\\\localhost\\not-a-source\\info", "\\\\?\\C:\\not-a-source" })
        {
            Assert.False(Chinese7937DecodedXuanbaoInfo.TryLoadResolveAndAdditionFiles(path, f.AdditionPath, 7937,
                f.InfoPin, f.AdditionPin, out var resolve, out var addition, out var failure));
            Assert.Null(resolve); Assert.Null(addition); Assert.Equal("UnsafePath", failure);
        }
        f.AssertReleased();
    });
}
