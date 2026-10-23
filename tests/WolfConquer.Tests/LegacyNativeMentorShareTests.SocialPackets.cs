using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMentorShareTests
{
    private static void FixedNativeText(byte[] bytes, int offset, int length, string value)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(value); int copied = Math.Min(value.Length, length);
        utf8.AsSpan(0, copied).CopyTo(bytes.AsSpan(offset, copied));
    }

    [Theory]
    [InlineData(10)] [InlineData(11)] [InlineData(12)] [InlineData(13)]
    [InlineData(14)] [InlineData(15)] [InlineData(18)] [InlineData(19)] [InlineData(255)]
    public Task KnowPersonsCodecPreservesNativeFixed128ByteFieldsAndUnknownActions(int action) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Packet.KnowPersonsCreate((LegacyNativeKnowPersonsPacket.Action)action, 7, true, 9, 11, 13, 15, "éX", "Name");
        var expected = new byte[128]; BinaryPrimitives.WriteUInt16LittleEndian(expected, 128); BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(2), 2139);
        Put(expected, 4, 7); expected[8] = (byte)action; expected[9] = 1;
        Put(expected, 12, 9); Put(expected, 16, 11); Put(expected, 20, 13); Put(expected, 24, 15);
        FixedNativeText(expected, 28, 64, "éX"); Put(expected, 92, 1); FixedNativeText(expected, 96, 32, "Name");
        Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet)); Assert.Equal(4, scope.Packet.Position); Assert.Equal(128, scope.Packet.Size);
        Assert.Equal(new byte[] { 0xC3, 0xA9, 0 }, expected.AsSpan(28, 3).ToArray()); //Native char-count write drops X.
        scope.Packet.Seek(4); scope.Packet.GetKnowPersons(out uint uid, out var mode, out bool online);
        Assert.Equal(7u, uid); Assert.Equal((byte)action, (byte)mode); Assert.True(online); Assert.Equal(10, scope.Packet.Position);
    });

    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(65535)] [InlineData(65536)]
    [InlineData(-2147483648)] [InlineData(2147483647)]
    public Task TradePartnerWriterNarrowsHoursToUInt16ButRequestReaderStillConsumesSignedInt32(int hours) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Packet.TradePartnerCreate(7, LegacyNativeTradePartnerPacket.Action.AddPartner, true, hours, 0x12345678, 140, 12, "Signature", "éX");
        var expected = new byte[128]; BinaryPrimitives.WriteUInt16LittleEndian(expected, 128); BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(2), 2004);
        Put(expected, 4, 7); expected[8] = 5; expected[9] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(10), unchecked((ushort)hours));
        Put(expected, 12, 0x12345678); Put(expected, 16, 140); Put(expected, 20, 12); Put(expected, 24, 0);
        FixedNativeText(expected, 28, 64, "Signature"); Put(expected, 92, 1); FixedNativeText(expected, 96, 32, "éX");
        Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet)); Assert.Equal(4, scope.Packet.Position);
        scope.Packet.Seek(4); scope.Packet.GetTradePartner(out uint uid, out var action, out bool online, out int readHours);
        Assert.Equal(7u, uid); Assert.Equal(LegacyNativeTradePartnerPacket.Action.AddPartner, action); Assert.True(online);
        Assert.Equal(unchecked((int)((uint)(ushort)hours | 0x56780000u)), readHours); Assert.Equal(14, scope.Packet.Position);
    });

    [Theory]
    [InlineData(0, false)] [InlineData(1, true)] [InlineData(2, false)] [InlineData(255, false)]
    public Task SocialReadersUseOnlyRawOnlineByteOneAndCurrentCursorWithoutEnumValidation(int onlineByte, bool expected) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Packet.Seek(100); scope.Packet.Write(99u); scope.Packet.Write((byte)255); scope.Packet.Write((byte)onlineByte); scope.Packet.Write(int.MinValue);
        scope.Packet.Seek(100); scope.Packet.GetTradePartner(out uint uid, out var action, out bool online, out int hours);
        Assert.Equal(99u, uid); Assert.Equal((byte)255, (byte)action); Assert.Equal(expected, online); Assert.Equal(int.MinValue, hours); Assert.Equal(110, scope.Packet.Position);
        scope.Packet.Seek(100); scope.Packet.GetKnowPersons(out uid, out var knownAction, out online);
        Assert.Equal(99u, uid); Assert.Equal((byte)255, (byte)knownAction); Assert.Equal(expected, online); Assert.Equal(106, scope.Packet.Position);
    });

    [Fact]
    public Task SocialCodecFaultsRetainNativePrefixCursorAndOldSizeWithoutFinalizingOrNullRepair() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Assert.Throws<NullReferenceException>(() => scope.Packet.KnowPersonsCreate(LegacyNativeKnowPersonsPacket.Action.AddFriend, 7, true, 9, 0, 0, 0, null!, "N"));
        Assert.Equal(28, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        Assert.Throws<NullReferenceException>(() => scope.Packet.TradePartnerCreate(7, LegacyNativeTradePartnerPacket.Action.AddPartner, true, 1, 2, 3, 4, "B", null!));
        Assert.Equal(96, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        Assert.Empty(scope.Queue);
    });

    private static void Put(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
}
