using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using ItemQuery = WolfConquer.Core.World.LegacyNativeMsgNinja.MsgNinjaItem;

namespace WolfConquer.Tests;

// NEVER select this class in the old-source baseline: its old writer would
// copy beyond these genuine tiny allocations. Corrected implementation only.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class CorrectedOnlyNinjaItemWriterBoundsTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(4, false)]
    [InlineData(8, false)]
    [InlineData(1, true)]
    [InlineData(4, true)]
    [InlineData(8, true)]
    public Task TinyRealWriterStorageRefusesBeforeAnyHeaderPayloadSealOrCursorWrite(int capacity, bool seal) =>
        NativePacketTestThread.Run(() =>
        {
            using var fixture = new NativeSpellTestFixture();
            if (seal) LegacyNativePacket.SealString = "TQServer";
            using var packet = new LegacyNativePacket(capacity);
            Fill(packet);
            packet.Size = 123;
            packet.Seek(0);
            Assert.Throws<InvalidOperationException>(() => packet.CreateNinjaItem(Message()));
            Assert.Equal(123, packet.Size);
            Assert.Equal(0, packet.Position);
            Assert.All(Bytes(packet), value => Assert.Equal((byte)0xa5, value));
        });

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(8)]
    public Task TinyRealAddItemRentalRefusesWithoutChangingItemsRanksOrQueue(int capacity) =>
        LegacyNativeNinjaLoadingTests.RunCorrectedOnlyNinjaPacketCapacity(capacity);

    [Fact]
    public Task ExactRealStorageRetainsIndependentLiteralFrameHeaderAndCapturedSeal() => NativePacketTestThread.Run(() =>
    {
        using var fixture = new NativeSpellTestFixture();
        LegacyNativePacket.SealString = "TQServer";
        var expected = Convert.FromHexString("14005909080210111A0A086410011800200028005451536572766572");
        using var packet = new LegacyNativePacket(expected.Length);
        Assert.Same(packet, packet.CreateNinjaItem(Message()));
        Assert.Equal(expected, NativeSpellTestFixture.Bytes(packet));
        Assert.Equal(4, packet.Position);
    });

    [Fact]
    public Task MinimumSchemaSizeRejectsTooManyNonnullRowsButRetainsNullEntryOmission() => NativePacketTestThread.Run(() =>
    {
        using var fixture = new NativeSpellTestFixture();
        var zero = new ItemQuery.Item();
        using var packet = new LegacyNativePacket(65_540);
        Fill(packet);
        packet.Size = 123;
        packet.Seek(0);
        var tooMany = new ItemQuery { Items = Enumerable.Repeat(zero, 5461).ToArray() };
        Assert.Throws<InvalidOperationException>(() => packet.CreateNinjaItem(tooMany));
        Assert.Equal(123, packet.Size);
        Assert.Equal(0, packet.Position);
        Assert.All(Bytes(packet), value => Assert.Equal((byte)0xa5, value));

        var boundary = new ItemQuery { Items = Enumerable.Repeat(zero, 5460).ToArray() };
        packet.CreateNinjaItem(boundary);
        Assert.Equal(65_528, packet.Size); // 4 header +4 required outer fields +5460*12 minimum row.
        Assert.Equal((ushort)65_528, BitConverter.ToUInt16(NativeSpellTestFixture.Bytes(packet), 0));

        packet.CreateNinjaItem(new ItemQuery { Items = new ItemQuery.Item[5461] });
        Assert.Equal(Convert.FromHexString("0800590908001000"), NativeSpellTestFixture.Bytes(packet));
    });

    private static ItemQuery Message() => new()
    {
        Type = ItemQuery.TypeID.Add, UID = 17,
        Items = [new() { ID = 100, Level = 1, Position = 0, Points = 0, UK = 0 }],
    };

    private static unsafe void Fill(LegacyNativePacket packet) =>
        Marshal.Copy(Enumerable.Repeat((byte)0xa5, packet.Capacity).ToArray(), 0, (nint)packet.Memory, packet.Capacity);

    private static unsafe byte[] Bytes(LegacyNativePacket packet)
    {
        var bytes = new byte[packet.Capacity];
        Marshal.Copy((nint)packet.Memory, bytes, 0, bytes.Length);
        return bytes;
    }
}
