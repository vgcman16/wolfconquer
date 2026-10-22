using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using ProtoBuf;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Message = WolfConquer.Core.World.LegacyNativeMsgRankMemberShow;
using Model = WolfConquer.Core.World.LegacyNativeMsgRankMemberShow.MsgRankMemberShowProto;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaRankTests
{
    [Fact]
    public void MessageUsesNativeEnumAliasesRequiredTagsAndPhysicalDefaults()
    {
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(Message.Mode)));
        Assert.NotNull(typeof(Message.Mode).GetCustomAttribute<FlagsAttribute>());
        Assert.Equal(new uint[] { 0, 1 }, Enum.GetValues<Message.Mode>().Select(value => (uint)value));
        var message = new Model();
        Assert.Equal(0u, message.Type);
        Assert.Null(message.Items);
        var row = new Model.Entry();
        Assert.Equal("", row.Name);
        Assert.Equal("", row.GuildName);
        foreach (Type type in new[] { typeof(Model), typeof(Model.Entry) })
        {
            Assert.NotNull(type.GetCustomAttribute<ProtoContractAttribute>());
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .OrderBy(field => field.MetadataToken).ToArray();
            Assert.Equal(type == typeof(Model) ? 2 : 21, fields.Length);
            for (int index = 0; index < fields.Length; index++)
            {
                var attribute = Assert.IsType<ProtoMemberAttribute>(fields[index].GetCustomAttribute<ProtoMemberAttribute>());
                Assert.True(attribute.IsRequired);
                Assert.Equal(index + 1, attribute.Tag);
            }
        }
    }

    [Fact]
    public Task ActualEntryConversionAndPrimitiveEncoderPreserveAllFieldsAndCursor() => NativePacketTestThread.Run(() =>
    {
        using var state = new State();
        using var native = new NativeSpellTestFixture();
        Fill(native.Packet);
        Model.Entry converted = Entry();
        var message = new Model { Type = 1, Items = [converted] };
        Assert.Same(native.Packet, native.Packet.CreateMsgRankMemberShow(message));
        byte[] expected = RankFrame(1, EntryWire());
        Assert.Equal(expected, NativeSpellTestFixture.Bytes(native.Packet));
        Assert.Equal(4, native.Packet.Position);
        Assert.Equal(expected.Length, native.Packet.Size);
        Assert.Empty(native.Queue); // Primitive codec does not send.
        Assert.Equal((byte)0xA5, ByteAt(native.Packet, expected.Length));
        Assert.Same(converted, message.Items[0]);
    });

    [Fact]
    public Task NativeDecoderIgnoresCallerCursorSizeAndOpcodeAndCreatesFreshObjects() => NativePacketTestThread.Run(() =>
    {
        using var state = new State();
        using var native = new NativeSpellTestFixture();
        byte[] bytes = RankFrame(1, EntryWire());
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 65535);
        Fill(native.Packet);
        PlaceBytes(native.Packet, bytes);
        Model query = new() { Type = 99, Items = [new() { EntityUID = 777 }] };
        Model old = query;
        native.Packet.GetRankMemberShow(out query);
        Assert.NotSame(old, query);
        Assert.Equal(1u, query.Type);
        var row = Assert.Single(query.Items);
        Assert.Equal(new uint[] { 2, 1, 41, 707, 808, 909, 1010, 1111, 1212, 1313, 1414, 1515,
            1616, 1717, 1818, 1919, 2020, 0, 0 },
            new[] { row.Type, row.Rank, row.EntityUID, row.Mesh, row.HairStyle, row.Head, row.Garment,
                row.LeftWeapon, row.LefttWeaponAccessory, row.RightWeapon, row.RightWeaponAccessory,
                row.MountArmor, row.Armor, row.Wing, row.WingPlus, row.Title, row.Flag, row.Profession, row.WeaponWar });
        Assert.Equal("Hero", row.Name);
        Assert.Equal("Guild", row.GuildName);
        Assert.Equal(bytes.Length, native.Packet.Position);
        Assert.Equal(321, native.Packet.Size);
        Assert.Equal(99u, old.Type);
        Assert.Empty(native.Queue);
    });

    [Fact]
    public Task NullOuterSerializesEmptyPayloadAndDecoderReturnsNewDefaultModel() => NativePacketTestThread.Run(() =>
    {
        using var state = new State();
        using var native = new NativeSpellTestFixture();
        Fill(native.Packet);
        native.Packet.CreateMsgRankMemberShow(null!);
        Assert.Equal(new byte[] { 4, 0, 6, 8 }, NativeSpellTestFixture.Bytes(native.Packet));
        native.Packet.GetRankMemberShow(out var query);
        Assert.NotNull(query);
        Assert.Equal(0u, query.Type);
        Assert.Null(query.Items);
        Assert.Equal(4, native.Packet.Position);
        Assert.Empty(native.Queue);
    });

    [Fact]
    public Task NullArrayElementIsSkippedWhileOuterRequiredTypeStillSerializes() => NativePacketTestThread.Run(() =>
    {
        using var state = new State();
        using var native = new NativeSpellTestFixture();
        Fill(native.Packet);
        var message = new Model { Type = 1, Items = [null!] };
        native.Packet.CreateMsgRankMemberShow(message);
        Assert.Equal(RankFrame(1), NativeSpellTestFixture.Bytes(native.Packet));
        Assert.Null(message.Items[0]);
        native.Packet.GetRankMemberShow(out var query);
        Assert.Equal(1u, query.Type);
        Assert.Null(query.Items);
        Assert.Empty(native.Queue);
    });

    [Fact]
    public Task ManagedNegativeLengthFaultStillPublishesFirstNewOutModelAndAdvancesFourBytes() => NativePacketTestThread.Run(() =>
    {
        using var state = new State();
        using var native = new NativeSpellTestFixture();
        Fill(native.Packet);
        PlaceBytes(native.Packet, [3, 0, 6, 8]);
        Model query = new() { Type = 77 };
        Model old = query;
        Assert.Throws<OverflowException>(() => native.Packet.GetRankMemberShow(out query));
        Assert.NotSame(old, query);
        Assert.Equal(0u, query.Type);
        Assert.Null(query.Items);
        Assert.Equal(4, native.Packet.Position);
        Assert.Equal(321, native.Packet.Size);
        Assert.Empty(native.Queue);
    });

    private static unsafe void Fill(LegacyNativePacket packet)
    {
        Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(),
            0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
        packet.Size = 321;
        packet.Seek(237);
    }

    private static unsafe void PlaceBytes(LegacyNativePacket packet, byte[] bytes)
    {
        Assert.InRange(bytes.Length, 4, 1024);
        Marshal.Copy(bytes, 0, (IntPtr)packet.Memory, bytes.Length);
    }

    private static unsafe byte ByteAt(LegacyNativePacket packet, int position) => packet.Memory[position];

    private static byte[] RankFrame(uint type, params byte[][] entries)
    {
        var payload = new List<byte>();
        Scalar(payload, 1, type);
        foreach (byte[] entry in entries) Delimited(payload, 2, entry);
        var frame = new byte[4 + payload.Count];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, checked((ushort)frame.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 2054);
        payload.CopyTo(frame, 4);
        return frame;
    }

    private static byte[] EntryWire()
    {
        uint[] values = [2, 1, 41, 0, 0, 707, 808, 909, 1010, 1111, 1212, 1313, 1414, 1515,
            1616, 1717, 1818, 1919, 2020, 0, 0];
        var bytes = new List<byte>();
        for (int tag = 1; tag <= 21; tag++)
            if (tag is 4 or 5) Delimited(bytes, tag, Encoding.UTF8.GetBytes(tag == 4 ? "Hero" : "Guild"));
            else Scalar(bytes, tag, values[tag - 1]);
        return bytes.ToArray();
    }

    private static void Scalar(List<byte> bytes, int tag, uint value)
    {
        Varint(bytes, (uint)tag << 3);
        Varint(bytes, value);
    }

    private static void Delimited(List<byte> bytes, int tag, byte[] value)
    {
        Varint(bytes, ((uint)tag << 3) | 2);
        Varint(bytes, (uint)value.Length);
        bytes.AddRange(value);
    }

    private static void Varint(List<byte> bytes, uint value)
    {
        while (value > 127) { bytes.Add((byte)((value & 127) | 128)); value >>= 7; }
        bytes.Add((byte)value);
    }
}
