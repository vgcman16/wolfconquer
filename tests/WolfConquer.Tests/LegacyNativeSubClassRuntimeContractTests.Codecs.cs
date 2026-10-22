using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Sort = WolfConquer.Core.Data.LegacyNativeDbLevExp.Sort;
using Action = WolfConquer.Core.World.LegacyNativeMsgSubClass.Action;
using Row = WolfConquer.Core.World.LegacyNativeMsgSubClass.SubClases;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeSubClassRuntimeContractTests
{
    [Fact]
    public void NativeDeclarationsKeepMarkerLayoutEnumWidthsAndMutablePromotionArrays()
    {
        Assert.Equal(1, Marshal.SizeOf<LegacyNativeMsgSubClass>());
        Assert.Equal(LayoutKind.Sequential, typeof(LegacyNativeMsgSubClass).StructLayoutAttribute!.Value);
        Assert.Equal(typeof(ushort), Enum.GetUnderlyingType(typeof(Action)));
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(Sort)));
        Assert.Equal(Enumerable.Range(0, 11).Select(value => (Action)value), Enum.GetValues<Action>());
        Assert.Equal(new uint[] { 0, 721259, 721261, 711188, 723087, 1088001, 711679, 0, 0, 723903 },
            LegacyNativeMsgSubClass.ItemsPromote);
        Assert.Equal(new byte[] { 0, 5, 10, 1, 20, 10, 1, 0, 0, 40 }, LegacyNativeMsgSubClass.ItemsCount);
        Assert.False(typeof(LegacyNativeMsgSubClass).GetField("ItemsPromote")!.IsInitOnly);
        Assert.False(typeof(LegacyNativeMsgSubClass).GetField("ItemsCount")!.IsInitOnly);
        var player = new LegacyNativeGameClientRuntime().Player;
        Assert.Null(player.SubClass);
        Assert.Equal(0u, player.SubClassHasPoints);
        Assert.Equal(Sort.User, player.ActiveSublass);
        Assert.DoesNotContain(typeof(LegacyNativeMsgSubClass).GetMethods(BindingFlags.Static | BindingFlags.Public),
            method => method.Name is "Process" or "Handler");
    }

    [Fact]
    public Task SingleBuilderWritesExactReservedBytesAndDoesNotSend() => NativePacketTestThread.Run(() =>
    {
        using var fixture = new NativeSpellTestFixture();
        Fill(fixture.Packet);
        Assert.Same(fixture.Packet, fixture.Packet.SubClassCreate((Action)0xABCD, (Sort)255, 254, 65535));
        Assert.Equal(Convert.FromHexString("1A00F107CDABFFFE000000000000FFFF00000000000000000000"),
            NativeSpellTestFixture.Bytes(fixture.Packet));
        Finished(fixture.Packet, 26);
        Assert.Empty(fixture.Queue);
    });

    [Fact]
    public Task ListBuilderKeepsPhysicalIdPhraseLevelOrderAndStudyReceive() => NativePacketTestThread.Run(() =>
    {
        using var fixture = new NativeSpellTestFixture();
        Fill(fixture.Packet);
        Row[] rows = [new() { ID = Sort.Wrangler, Phrase = 7, Level = 3 },
            new() { ID = (Sort)255, Phrase = 254, Level = 253 }];
        Assert.Same(fixture.Packet, fixture.Packet.SubClassCreate(Action.ShowGUI, (ushort)65535, 0x1234, rows));
        Assert.Equal(Convert.FromHexString("2000F1070700FFFF000000000000341200000000000002000000090703FFFEFD"),
            NativeSpellTestFixture.Bytes(fixture.Packet));
        Finished(fixture.Packet, 32);
        Assert.Empty(fixture.Queue);
    });

    [Fact]
    public Task NullAndEmptyListsBothWriteAZeroCountWithoutSending() => NativePacketTestThread.Run(() =>
    {
        using var fixture = new NativeSpellTestFixture();
        foreach (Row[]? rows in new Row[]?[] { null, [] })
        {
            Fill(fixture.Packet);
            fixture.Packet.SubClassCreate((Action)65535, (ushort)17, 23, rows!);
            Assert.Equal(Convert.FromHexString("1A00F107FFFF1100000000000000170000000000000000000000"),
                NativeSpellTestFixture.Bytes(fixture.Packet));
            Finished(fixture.Packet, 26);
        }
        Assert.Empty(fixture.Queue);
    });

    [Fact]
    public Task SingleReaderStartsAtCurrentCursorAndReinterpretsSignedByte() => NativePacketTestThread.Run(() =>
    {
        byte[] bytes = Enumerable.Repeat((byte)0xA5, 20).ToArray();
        bytes[9] = 0xCD; bytes[10] = 0xAB; bytes[11] = 0xFF;
        using var packet = new LegacyNativePacket(bytes);
        int size = packet.Size;
        packet.Seek(9);
        packet.GetSubClass(out Action action, out Sort type);
        Assert.Equal((Action)0xABCD, action);
        Assert.Equal((Sort)255, type);
        Assert.Equal(12, packet.Position);
        Assert.Equal(size, packet.Size);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ListReaderSkipsSixteenBytesAndDoesNotValidateHeader(bool empty) => NativePacketTestThread.Run(() =>
    {
        byte[] bytes = Convert.FromHexString("2000AA7ACDAB112233445566778899AABBCCDDEEFF0002000000090703FFFEFDCCDD");
        if (empty) BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(22), 0);
        using var packet = new LegacyNativePacket(bytes);
        int size = packet.Size;
        packet.Seek(4);
        packet.GetSubClass(out Action action, out Row[] rows);
        Assert.Equal((Action)0xABCD, action);
        Assert.Equal(empty ? 0 : 2, rows.Length);
        if (!empty)
        {
            Assert.Equal(Sort.Wrangler, rows[0].ID);
            Assert.Equal((byte)7, rows[0].Phrase); Assert.Equal((byte)3, rows[0].Level);
            Assert.Equal((Sort)255, rows[1].ID);
            Assert.Equal((byte)254, rows[1].Phrase); Assert.Equal((byte)253, rows[1].Level);
        }
        Assert.Equal(empty ? 26 : 32, packet.Position);
        Assert.Equal(size, packet.Size);
    });

    [Fact]
    public Task NegativeListCountFaultsAfterReadingCountRatherThanClamping() => NativePacketTestThread.Run(() =>
    {
        byte[] bytes = new byte[26];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(22), -1);
        using var packet = new LegacyNativePacket(bytes);
        packet.Seek(4);
        Assert.Throws<OverflowException>(() => packet.GetSubClass(out Action _, out Row[] _));
        Assert.Equal(26, packet.Position);
    });

    [Fact]
    public Task NullRowFaultKeepsWrittenPrefixAndDoesNotFinalizeOrSend() => NativePacketTestThread.Run(() =>
    {
        using var fixture = new NativeSpellTestFixture();
        Fill(fixture.Packet);
        Row[] rows = [new() { ID = Sort.Warlock, Phrase = 7, Level = 3 }, null!];
        Assert.Throws<NullReferenceException>(() => fixture.Packet.SubClassCreate(Action.ShowGUI, (ushort)17, 23, rows));
        Assert.Equal(29, fixture.Packet.Position);
        Assert.Equal(321, fixture.Packet.Size);
        Assert.Equal(new byte[] { 2, 7, 3 }, Read(fixture.Packet, 26, 3));
        Assert.Equal(new byte[] { 2, 0, 0, 0 }, Read(fixture.Packet, 22, 4));
        Assert.Equal(new byte[] { 0xA5, 0xA5, 0xA5, 0xA5 }, Read(fixture.Packet, 0, 4));
        Assert.Empty(fixture.Queue);
    });

    private static unsafe void Fill(LegacyNativePacket packet)
    {
        Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(),
            0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
        packet.Size = 321; packet.Seek(237);
    }

    private static unsafe byte[] Read(LegacyNativePacket packet, int offset, int length)
    {
        byte[] bytes = new byte[length];
        Marshal.Copy((IntPtr)(packet.Memory + offset), bytes, 0, length);
        return bytes;
    }

    private static void Finished(LegacyNativePacket packet, int length)
    {
        Assert.Equal(length, packet.Size); Assert.Equal(4, packet.Position);
        Assert.Equal(new byte[] { 0xA5 }, Read(packet, length, 1));
    }
}
