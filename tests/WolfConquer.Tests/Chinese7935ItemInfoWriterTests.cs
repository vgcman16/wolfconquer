using WolfConquer.Core.World;
using Scalar = WolfConquer.Core.World.Chinese7935ItemInfoScalar;

namespace WolfConquer.Tests;

public sealed partial class Chinese7935ItemInfoWriterTests
{
    private const string RequiredZero = "080010001800";
    private const string Golden = "086510CA01180020042805300638E00140084809500A58F5FFFFFFFFFFFFFFFF01600C680D700E780F800110880111900112980113A00114A80115B001EAFFFFFFFFFFFFFFFF01B80117C001E8FFFFFFFFFFFFFFFF01C801E7FFFFFFFFFFFFFFFF01D0011ADA01034100FFE20100E8011DF001E2FFFFFFFFFFFFFFFF01F80103F801FFFFFFFFFFFFFFFFFF01F801008002208802219002DEFFFFFFFFFFFFFFFF019802DDFFFFFFFFFFFFFFFF01A0028080808010A802DBFFFFFFFFFFFFFFFF01B002DAFFFFFFFFFFFFFFFF01B802D9FFFFFFFFFFFFFFFF01C002FEFFFFFFFFFFFFFFFF01C802FFFFFFFFFFFFFFFFFF01D002FFFFFFFF0FD802FEFFFFFFFFFFFFFFFF01";

    [Fact]
    public void TypedConstructionWritesIndependentAll43NativeOrderGolden()
    {
        var packet = CreateGolden();
        Assert.Equal(Convert.FromHexString(Golden), packet.ToPayloadBytes());
        Assert.True(packet.TryToPayloadBytes(out var second));
        Assert.Equal(Convert.FromHexString(Golden), second);
        Assert.True(Chinese7935ItemInfoView.TryParseKnownPayload(second!, out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal(packet.ToPayloadBytes(), parsed.ToPayloadBytes());
        Assert.True(parsed.TryGetEchoRelatedFields(out var fields));
        Assert.Equal(new Chinese7935EchoItemFields(101, 224, -2), fields);
    }

    [Fact]
    public void RequiredZeroAndOptionalAbsentPresentZeroEmptyAreWrittenDistinctly()
    {
        Assert.Equal(Convert.FromHexString(RequiredZero), Chinese7935ItemInfoView.Create(0, 0, 0).ToPayloadBytes());
        var present = Chinese7935ItemInfoView.Create(0, 0, 0,
            [Scalar.Int32(40, 0), Scalar.UInt64(36, 0), Scalar.UInt32(4, 0)],
            ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty);
        Assert.Equal(Convert.FromHexString(RequiredZero + "2000DA0100E20100A00200C00200"), present.ToPayloadBytes());
        Assert.True(present.HasField(4));
        Assert.True(present.HasField(36));
        Assert.True(present.HasField(40));
        Assert.Empty(present.CopyData27()!);
        Assert.Empty(present.CopyData28()!);
        Assert.False(present.HasField(31));
    }

    [Fact]
    public void PackedInputIsEmittedInNativeUnpacked31Order()
    {
        var input = Convert.FromHexString(RequiredZero + "FA010BFFFFFFFFFFFFFFFFFF0100F80102");
        Assert.True(Chinese7935ItemInfoView.TryParseKnownPayload(input, out var packet));
        Assert.NotNull(packet);
        Assert.Equal(new[] { -1, 0, 2 }, packet.Values31.ToArray());
        Assert.Equal(Convert.FromHexString(RequiredZero + "F801FFFFFFFFFFFFFFFFFF01F80100F80102"), packet.ToPayloadBytes());
    }

    [Fact]
    public void ParsedUnorderedDuplicateFieldsWriteOnceInNativeOrderWithLastValues()
    {
        var input = Convert.FromHexString(RequiredZero + "08010802C0020538E001C00206");
        Assert.True(Chinese7935ItemInfoView.TryParseKnownPayload(input, out var packet));
        Assert.NotNull(packet);
        Assert.Equal(Convert.FromHexString("08021000180038E001C00206"), packet.ToPayloadBytes());
    }

    [Theory]
    [InlineData(-1, "FFFFFFFFFFFFFFFFFF01")]
    [InlineData(int.MinValue, "80808080F8FFFFFFFF01")]
    [InlineData(int.MaxValue, "FFFFFFFF07")]
    public void Int32ConstructionUsesNativeSignExtension(int value, string expectedHex)
    {
        var packet = Chinese7935ItemInfoView.Create(0, 0, 0, [Scalar.Int32(40, value)]);
        Assert.Equal(Convert.FromHexString(RequiredZero + "C002" + expectedHex), packet.ToPayloadBytes());
    }

    [Theory]
    [InlineData("FFFFFFFF0F", "FFFFFFFFFFFFFFFFFF01")]
    [InlineData("8080808008", "80808080F8FFFFFFFF01")]
    [InlineData("80808080F8FFFFFFFF01", "80808080F8FFFFFFFF01")]
    public void ParsedSignedStorageFormsWriteCanonicalNativeInt32(string inputHex, string expectedHex)
    {
        Assert.True(Chinese7935ItemInfoView.TryParseKnownPayload(Convert.FromHexString(RequiredZero + "C002" + inputHex), out var packet));
        Assert.NotNull(packet);
        Assert.Equal(Convert.FromHexString(RequiredZero + "C002" + expectedHex), packet.ToPayloadBytes());
    }

    [Fact]
    public void RawStringsAreCopiedAndWrittenWithoutTextEncodingOrValidation()
    {
        var packet = Chinese7935ItemInfoView.Create(0, 0, 0, data27: new byte[] { 255, 254, 0 });
        Assert.Equal(Convert.FromHexString(RequiredZero + "DA0103FFFE00"), packet.ToPayloadBytes());
        Assert.Null(packet.CopyData28());
    }

    [Fact]
    public void ConstructedDataOwnsScalarsRawBytesRepeatedValuesAndEveryOutput()
    {
        Scalar[] scalars = [Scalar.Int32(40, -1)];
        byte[] data27 = [255, 0], data28 = [65];
        int[] values31 = [1, -2];
        var packet = Chinese7935ItemInfoView.Create(5, 6, 7, scalars, data27, data28, values31);
        var expected = packet.ToPayloadBytes();
        scalars[0] = Scalar.Int32(40, 0);
        data27[0] = 0;
        data28[0] = 0;
        values31[0] = 99;
        var first = packet.ToPayloadBytes();
        first[0] = 0;
        var copiedData = packet.CopyData27()!;
        copiedData[0] = 0;
        Assert.Equal(expected, packet.ToPayloadBytes());
        Assert.True(packet.TryGetInt32(40, out var position));
        Assert.Equal(-1, position);
        Assert.Equal(new byte[] { 255, 0 }, packet.CopyData27());
        Assert.Equal(new byte[] { 65 }, packet.CopyData28());
        Assert.Equal(new[] { 1, -2 }, packet.Values31.ToArray());
    }

    private static Chinese7935ItemInfoView CreateGolden()
    {
        var fields = new List<Scalar>();
        foreach (var field in new[] { 4, 5, 6, 7, 8, 9, 10, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 23, 26, 29, 32, 33, 42 })
            fields.Add(Scalar.UInt32(field, field switch { 7 => 224u, 42 => uint.MaxValue, _ => (uint)field }));
        foreach (var field in new[] { 11, 22, 24, 25, 30, 34, 35, 37, 38, 39, 40 })
            fields.Add(Scalar.Int32(field, field == 40 ? -2 : -field));
        fields.Add(Scalar.UInt64(36, 0x1_0000_0000UL));
        fields.Add(Scalar.UInt64(41, ulong.MaxValue));
        fields.Add(Scalar.UInt64(43, ulong.MaxValue - 1));
        Assert.Equal(Chinese7935ItemInfoView.MaximumOptionalScalarFields, fields.Count);
        fields.Reverse();
        return Chinese7935ItemInfoView.Create(101, 202, 0, fields.ToArray(),
            new byte[] { 65, 0, 255 }, ReadOnlyMemory<byte>.Empty, [3, -1, 0]);
    }
}
