using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ClientTextFileClassifierTests
{
    [Fact]
    public void SimpleIniBytesClassifyAsText()
    {
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        var bytes = encoding.GetBytes("[NpcType1]\r\nName=\u6742\u8d27\u5e97\u8001\u677f\r\nLookColor=0xffffffff\r\n");

        var result = ClientTextFileClassifier.Classify(bytes);

        Assert.True(result.IsLikelyText);
        Assert.False(result.IsLikelyPackedOrBinary);
        Assert.Equal(0, result.NullByteCount);
        Assert.Equal(3, result.NewlineCount);
        Assert.True(result.PrintableRatio > 0.95);
    }

    [Fact]
    public void Utf8LuaWithChineseClassifiesAsText()
    {
        var bytes = Encoding.UTF8.GetBytes(
            "local className = \"\u6b66\u50e7\"\n" +
            "function describe()\n" +
            "    return \"\u6b22\u8fce\u6765\u5230\u53cc\u9f99\u57ce\"\n" +
            "end\n");

        var result = ClientTextFileClassifier.Classify(bytes);

        Assert.True(result.IsLikelyText);
        Assert.False(result.IsLikelyPackedOrBinary);
        Assert.Equal(0, result.NullByteCount);
        Assert.Equal(4, result.NewlineCount);
        Assert.True(result.PrintableRatio > 0.95);
    }

    [Fact]
    public void PseudoPackedBytesWithNullsAndControlsClassifyAsPackedOrBinary()
    {
        var bytes = new byte[]
        {
            0x1F, 0x8A, 0x00, 0x03, 0x7F, 0xC1, 0x00, 0x14,
            0x92, 0xA7, 0x10, 0x00, 0xE4, 0x05, 0x8B, 0x00,
        };

        var result = ClientTextFileClassifier.Classify(bytes);

        Assert.False(result.IsLikelyText);
        Assert.True(result.IsLikelyPackedOrBinary);
        Assert.Equal(4, result.NullByteCount);
        Assert.True(result.ControlByteRatio > 0.25);
        Assert.Contains("NUL", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void LargeBufferWithAlmostNoLineBreaksClassifiesAsPackedOrBinary()
    {
        var bytes = Enumerable.Repeat((byte)0x8A, 12_000).ToArray();
        bytes[6_000] = (byte)'\n';

        var result = ClientTextFileClassifier.Classify(bytes);

        Assert.False(result.IsLikelyText);
        Assert.True(result.IsLikelyPackedOrBinary);
        Assert.Equal(0, result.NullByteCount);
        Assert.Equal(1, result.NewlineCount);
        Assert.True(result.PrintableRatio > 0.99);
        Assert.Contains("newline", result.Reason, StringComparison.OrdinalIgnoreCase);
    }
}
