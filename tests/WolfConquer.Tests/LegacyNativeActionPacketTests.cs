using System.Buffers.Binary;
using System.Reflection;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeActionPacketTests
{
    // Literal outputs from the pinned protobuf-net2.0.0.668 and complete source
    // contract, with the same object-typed Serialize expression as native Packet.
    [Theory]
    [InlineData("action-default", "080048006000680070007800")]
    [InlineData("action-remove", "081118A4A8014800606D680070007800")]
    [InlineData("action-max", "08FFFFFFFF0F10FFFFFFFF0F18FFFFFFFF0F20FFFFFFFF0728FFFFFFFFFFFFFFFF7F300138FFFFFFFF0F40FFFFFFFF0F48FFFFFFFF0F50FFFFFFFF0758FFFFFFFF0F60FFFF0368FFFF0370FFFFFFFF0F78FFFFFFFF0F8001FFFFFFFF0F8801FFFFFFFF0F9001FFFFFFFF0F9801FFFFFFFF07A001FFFFFFFF07A801FFFFFFFF0FB001FFFFFFFF07B80101")]
    [InlineData("action-negative", "080020FFFFFFFFFFFFFFFFFF01288080808080808080800148005080808080F8FFFFFFFF0160006800700078009801FFFFFFFFFFFFFFFFFF01A00180808080F8FFFFFFFF01B001FFFFFFFFFFFFFFFFFF01")]
    [InlineData("action-empty-strings", "080048006000680070007800")]
    [InlineData("action-strings", "080048006000680070007800C20100C2010141C20107E9BE99F09F9089")]
    [InlineData("action-null-strings", "080048006000680070007800C20100C2010142")]
    [InlineData("action-null-outer", "")]
    public Task ActionCreateMatchesPinnedFullContract(string name, string hex) => NativePacketTestThread.Run(() =>
    {
        using var seal = new SealScope();
        using var packet = new LegacyNativePacket(512);
        packet.Seek(31);

        Assert.Same(packet, packet.ActionCreate(Query(name)!));

        byte[] expected = Convert.FromHexString(hex);
        byte[] frame = Frame(packet);
        Assert.Equal(expected, frame[4..]);
        Assert.Equal(expected.Length + 4, packet.Size);
        Assert.Equal((ushort)frame.Length, BinaryPrimitives.ReadUInt16LittleEndian(frame));
        Assert.Equal((ushort)2046, BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2)));
        Assert.Equal(4, packet.Position);
    });

    [Fact]
    public Task QueryRemainsMutableAndUsesItsTimestampWithoutSamplingClock() => NativePacketTestThread.Run(() =>
    {
        using var seal = new SealScope();
        using var packet = new LegacyNativePacket(512);
        string[] strings = ["A"];
        var query = new LegacyNativeActionQuery { Timestamp = 7, Strings = strings };
        packet.ActionCreate(query);
        var first = Frame(packet);
        Assert.Equal("080048076000680070007800C2010141", Convert.ToHexString(first[4..]));

        query.Timestamp = 0;
        strings[0] = "B";
        Assert.Equal("080048076000680070007800C2010141", Convert.ToHexString(first[4..]));
        packet.ActionCreate(query);
        Assert.Equal("080048006000680070007800C2010142", Convert.ToHexString(Frame(packet)[4..]));
        Assert.Same(strings, query.Strings);
    });

    [Fact]
    public Task NativeSealIsOutsideHeaderLengthAndCursorReturnsToFour() => NativePacketTestThread.Run(() =>
    {
        using var seal = new SealScope();
        LegacyNativePacket.SealString = "TQServer";
        using var packet = new LegacyNativePacket(512);
        packet.ActionCreate(new LegacyNativeActionQuery());
        var frame = Frame(packet);
        Assert.Equal("1000FE070800480060006800700078005451536572766572", Convert.ToHexString(frame));
        Assert.Equal(24, packet.Size);
        Assert.Equal(16, BinaryPrimitives.ReadUInt16LittleEndian(frame));
        Assert.Equal(Encoding.ASCII.GetBytes("TQServer"), frame[^8..]);
        Assert.Equal(4, packet.Position);
    });

    [Fact]
    public void NullStreamFaultsEvenForNullQuery()
    {
        Assert.Throws<NullReferenceException>(() => LegacyNativeActionPacket.ActionCreate(null!, null!));
        Assert.Throws<NullReferenceException>(() => LegacyNativeActionPacket.ActionCreate(null!, new()));
    }

    private static LegacyNativeActionQuery? Query(string name) => name switch
    {
        "action-default" => new(),
        "action-remove" => new() { ObjId = 17, Type = 109, dwParam = 21540 },
        "action-max" => new()
        {
            ObjId = uint.MaxValue, TargetUID = uint.MaxValue, dwParam = uint.MaxValue,
            dwParam2 = int.MaxValue, dwParam3 = long.MaxValue, SucDone = true,
            TargetPositionX = uint.MaxValue, TargetPositionY = uint.MaxValue,
            Timestamp = uint.MaxValue, NpcID = int.MaxValue, CheatLogData1 = uint.MaxValue,
            Type = ushort.MaxValue, Fascing = ushort.MaxValue, PositionX = uint.MaxValue,
            PositionY = uint.MaxValue, CheatLogData2 = uint.MaxValue, MapID = uint.MaxValue,
            CheatLogData3 = uint.MaxValue, ScheduleTime = int.MaxValue, LinkIndex = int.MaxValue,
            CheatLogData4 = uint.MaxValue, RemainTime = int.MaxValue, MountCharge = true,
        },
        "action-negative" => new()
        {
            dwParam2 = -1, dwParam3 = long.MinValue, NpcID = int.MinValue,
            ScheduleTime = -1, LinkIndex = int.MinValue, RemainTime = -1,
        },
        "action-empty-strings" => new() { Strings = [] },
        "action-strings" => new() { Strings = ["", "A", "\u9f99\ud83d\udc09"] },
        "action-null-strings" => new() { Strings = [null!, "", "B", null!] },
        "action-null-outer" => null,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private static unsafe byte[] Frame(LegacyNativePacket packet)
    {
        Assert.InRange(packet.Size, 0, 512);
        return new ReadOnlySpan<byte>(packet.Memory, packet.Size).ToArray();
    }

    private sealed class SealScope : IDisposable
    {
        private static readonly FieldInfo Field = typeof(LegacyNativePacket).GetField("seal", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? previous = Field.GetValue(null);
        internal SealScope() => Field.SetValue(null, null);
        public void Dispose() => Field.SetValue(null, previous);
    }
}
