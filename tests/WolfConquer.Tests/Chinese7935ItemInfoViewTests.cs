using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class Chinese7935ItemInfoViewTests
{
    // Independent literal protobuf vector: all 43 researched fields, raw bytes, signed negatives and 64-bit limits.
    private const string Golden = "086510CA01180020042805300638E00140084809500A58F5FFFFFFFFFFFFFFFF01600C680D700E780F800110880111900112980113A00114A80115B001EAFFFFFFFFFFFFFFFF01B80117C001E8FFFFFFFFFFFFFFFF01C801E7FFFFFFFFFFFFFFFF01D0011ADA01034100FFE20100E8011DF001E2FFFFFFFFFFFFFFFF01F80103F801FFFFFFFFFFFFFFFFFF01F801008002208802219002DEFFFFFFFFFFFFFFFF019802DDFFFFFFFFFFFFFFFF01A0028080808010A802DBFFFFFFFFFFFFFFFF01B002DAFFFFFFFFFFFFFFFF01B802D9FFFFFFFFFFFFFFFF01C002FEFFFFFFFFFFFFFFFF01C802FFFFFFFFFFFFFFFFFF01D002FFFFFFFF0FD802FEFFFFFFFFFFFFFFFF01";
    private const string RequiredZero = "080010001800";

    [Fact]
    public void IndependentAll43FieldGoldenPreservesActualKindsAndValues()
    {
        Assert.True(Chinese7935ItemInfoView.TryParseKnownPayload(Convert.FromHexString(Golden), out var view));
        Assert.NotNull(view);
        Assert.Equal(101u, view.ItemUid);
        Assert.Equal(202u, view.Field2);
        Assert.Equal(0u, view.Field3);
        int[] unsignedFields = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 23, 26, 29, 32, 33, 42];
        foreach (var field in unsignedFields)
        {
            var expected = field switch { 1 => 101u, 2 => 202u, 3 => 0u, 7 => 224u, 42 => uint.MaxValue, _ => (uint)field };
            Assert.True(view.TryGetUInt32(field, out var value));
            Assert.Equal(expected, value);
        }
        foreach (var field in new[] { 11, 22, 24, 25, 30, 34, 35, 37, 38, 39, 40 })
        {
            Assert.True(view.TryGetInt32(field, out var value));
            Assert.Equal(field == 40 ? -2 : -field, value);
        }
        Assert.True(view.TryGetUInt64(36, out var first64));
        Assert.Equal(0x1_0000_0000UL, first64);
        Assert.True(view.TryGetUInt64(41, out var second64));
        Assert.Equal(ulong.MaxValue, second64);
        Assert.True(view.TryGetUInt64(43, out var third64));
        Assert.Equal(ulong.MaxValue - 1, third64);
        Assert.Equal(new byte[] { 65, 0, 255 }, view.CopyData27());
        Assert.Empty(view.CopyData28()!);
        Assert.Equal(new[] { 3, -1, 0 }, view.Values31.ToArray());
        for (var field = 1; field <= 43; field++) Assert.True(view.HasField(field));
        Assert.True(view.TryGetEchoRelatedFields(out var echo));
        Assert.Equal(new Chinese7935EchoItemFields(101, 224, -2), echo);
    }

    [Fact]
    public void RequiredPresentZeroOptionalAbsenceAndEmptyBytesRemainDistinct()
    {
        Assert.True(Chinese7935ItemInfoView.TryParseKnownPayload(Convert.FromHexString(RequiredZero), out var absent));
        Assert.NotNull(absent);
        Assert.True(absent.HasField(1));
        Assert.False(absent.HasField(7));
        Assert.False(absent.HasField(40));
        Assert.Null(absent.CopyData27());
        Assert.False(absent.TryGetEchoRelatedFields(out var fields));
        Assert.Equal(default, fields);
        Assert.True(Chinese7935ItemInfoView.TryParseKnownPayload(Convert.FromHexString(RequiredZero + "3800DA0100E20100FA0100C00200"), out var present));
        Assert.NotNull(present);
        Assert.True(present.HasField(7));
        Assert.True(present.HasField(40));
        Assert.Empty(present.CopyData27()!);
        Assert.Empty(present.CopyData28()!);
        Assert.False(present.HasField(31));
        Assert.True(present.TryGetEchoRelatedFields(out fields));
        Assert.Equal(new Chinese7935EchoItemFields(0, 0, 0), fields);
    }

    [Fact]
    public void DuplicateScalarsAndBytesTakeLastWhilePackedAndUnpacked31Append()
    {
        var bytes = Convert.FromHexString(RequiredZero + "0801080238013802DA010141DA010142F80103FA010BFFFFFFFFFFFFFFFFFF0100F80102C00205C00206");
        Assert.True(Chinese7935ItemInfoView.TryParseKnownPayload(bytes, out var view));
        Assert.NotNull(view);
        Assert.Equal(2u, view.ItemUid);
        Assert.True(view.TryGetUInt32(7, out var category));
        Assert.Equal(2u, category);
        Assert.True(view.TryGetInt32(40, out var position));
        Assert.Equal(6, position);
        Assert.Equal(new byte[] { 66 }, view.CopyData27());
        Assert.Equal(new[] { 3, -1, 0, 2 }, view.Values31.ToArray());
    }

    [Theory]
    [InlineData("FFFFFFFF0F", -1)]
    [InlineData("FFFFFFFFFFFFFFFFFF01", -1)]
    [InlineData("8080808008", int.MinValue)]
    [InlineData("80808080F8FFFFFFFF01", int.MinValue)]
    [InlineData("FFFFFFFF07", int.MaxValue)]
    public void SignedField40RetainsDwordAndSignExtendedForms(string valueHex, int expected)
    {
        Assert.True(Chinese7935ItemInfoView.TryParseKnownPayload(Convert.FromHexString(RequiredZero + "C002" + valueHex), out var view));
        Assert.NotNull(view);
        Assert.True(view.TryGetInt32(40, out var value));
        Assert.Equal(expected, value);
    }

    [Fact]
    public void ParsedViewOwnsBytesAndRepeatedValues()
    {
        var bytes = Convert.FromHexString(Golden);
        Assert.True(Chinese7935ItemInfoView.TryParseKnownPayload(bytes, out var view));
        Assert.NotNull(view);
        Array.Fill(bytes, (byte)0);
        var copy = view.CopyData27()!;
        copy[0] = 0;
        var repeated = view.Values31.ToArray();
        repeated[0] = 99;
        Assert.Equal(new byte[] { 65, 0, 255 }, view.CopyData27());
        Assert.Equal(new[] { 3, -1, 0 }, view.Values31.ToArray());
        Assert.Equal(101u, view.ItemUid);
    }

    [Fact]
    public void TypedGettersDoNotReinterpretWrongOrUnsupportedFields()
    {
        Assert.True(Chinese7935ItemInfoView.TryParseKnownPayload(Convert.FromHexString(Golden), out var view));
        Assert.NotNull(view);
        Assert.False(view.TryGetUInt32(40, out _));
        Assert.False(view.TryGetInt32(1, out _));
        Assert.False(view.TryGetUInt64(31, out _));
        Assert.False(view.TryGetUInt32(-1, out _));
        Assert.False(view.HasField(int.MaxValue));
        Assert.False(view.HasField(0));
        Assert.Equal(Chinese7935ItemInfoFieldKind.Unsupported, Chinese7935ItemInfoView.FieldKind(44));
    }

    [Fact]
    public void Explicit2350FrameReaderDoesNotRegisterOrAcceptD13ItemInfo()
    {
        var bytes = Convert.FromHexString(Golden);
        Assert.True(Chinese7935ItemInfoView.TryParseKnownFrame(Frame(2350, bytes), out var view));
        Assert.NotNull(view);
        Assert.False(Chinese7935ItemInfoView.TryParseKnownFrame(Frame(1008, bytes), out view));
        Assert.Null(view);
        Assert.False(Chinese7935ItemInfoView.TryParseKnownFrame(Frame(2132, bytes), out view));
        Assert.Null(view);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        Assert.False(registry.TryGetHandler(2350, out _));
    }

    private static PacketFrame Frame(ushort opcode, byte[] payload)
    {
        var writer = new PacketWriter(opcode, payload.Length);
        payload.CopyTo(writer.Payload);
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(writer.ToArray()), out var frame, out _, out _));
        return frame;
    }
}
