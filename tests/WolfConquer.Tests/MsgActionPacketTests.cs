using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class MsgActionPacketTests
{
    [Theory]
    [InlineData(74, MsgActionType.SetLocation)]
    [InlineData(75, MsgActionType.Hotkeys)]
    [InlineData(76, MsgActionType.ConfirmAssociates)]
    [InlineData(77, MsgActionType.ConfirmProficiencies)]
    [InlineData(78, MsgActionType.ConfirmSpells)]
    [InlineData(79, MsgActionType.ChangeDirection)]
    [InlineData(81, MsgActionType.ChangeStance)]
    [InlineData(85, MsgActionType.ChangeMap)]
    [InlineData(86, MsgActionType.Teleport)]
    [InlineData(94, MsgActionType.Revive)]
    [InlineData(96, MsgActionType.SetPkMode)]
    [InlineData(102, MsgActionType.RequestEntity)]
    [InlineData(103, MsgActionType.CancelFocusedSpell)]
    [InlineData(104, MsgActionType.LegacyColor)]
    [InlineData(106, MsgActionType.TeamSearchForMember)]
    [InlineData(111, MsgActionType.StartVendor)]
    [InlineData(114, MsgActionType.StopVending)]
    [InlineData(117, MsgActionType.ViewEquipment)]
    [InlineData(120, MsgActionType.EndFly)]
    [InlineData(126, MsgActionType.OpenClientWindow)]
    [InlineData(132, MsgActionType.CompleteLogin)]
    [InlineData(134, MsgActionType.MonsterRespawnEffect)]
    [InlineData(135, MsgActionType.RemoveEntity)]
    [InlineData(137, MsgActionType.Jump)]
    [InlineData(145, MsgActionType.Ghost)]
    [InlineData(157, MsgActionType.LegacyMerchantStartup)]
    [InlineData(160, MsgActionType.OpenGuiNpc)]
    [InlineData(161, MsgActionType.Away)]
    [InlineData(164, MsgActionType.ActionPick)]
    [InlineData(256, MsgActionType.UpdateInventorySash)]
    [InlineData(310, MsgActionType.QuerySpawn)]
    [InlineData(408, MsgActionType.QueryEquipment)]
    [InlineData(151, MsgActionType.ChangeLookface)]
    [InlineData(476, MsgActionType.LegacyUnregistered476)]
    public void MsgActionTypeDefinesLegacyNumericValues(int expected, MsgActionType type)
    {
        Assert.Equal((ushort)expected, (ushort)type);
    }

    [Fact]
    public void MsgActionOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2046, WorldPacketOpcodes.MsgAction);
        Assert.Equal(WorldPacketOpcodes.MsgAction, MsgActionPacket.Opcode);
    }

    [Fact]
    public void MsgActionPacketWritesLegacyActionQueryProtobufShape()
    {
        var packet = new MsgActionPacket
        {
            ObjId = 1,
            DwParam = 1002,
            Timestamp = 2,
            Type = MsgActionType.SetLocation,
            Facing = 6,
            PositionX = 830,
            PositionY = 584,
        };
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(MsgActionPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(
        [
            0x08, 0x01,
            0x18, 0xEA, 0x07,
            0x48, 0x02,
            0x60, 0x4A,
            0x68, 0x06,
            0x70, 0xBE, 0x06,
            0x78, 0xC8, 0x04,
        ], frame.Payload.ToArray());
    }

    [Fact]
    public void MsgActionPacketWritesSyntheticLegacyMerchantStartupFrame()
    {
        var packet = MsgActionPacket.CreateLegacyMerchantStartup(73);

        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.Equal(CreateSyntheticProtoFrame((1, 73), (3, 2), (9, 0), (12, 157), (13, 0), (14, 0), (15, 0)), bytes);
        Assert.True(MsgActionPacket.TryParse(frame, out var parsed));
        Assert.Equal(73U, parsed.ObjId);
        Assert.Equal(2U, parsed.DwParam);
        Assert.Equal(0U, parsed.Timestamp);
        Assert.Equal(MsgActionType.LegacyMerchantStartup, parsed.Type);
        Assert.Equal(0, parsed.Facing);
        Assert.Equal(0U, parsed.PositionX);
        Assert.Equal(0U, parsed.PositionY);
    }

    [Fact]
    public void MsgActionPacketRoundTripsLegacyActionPickRemovalShape()
    {
        var packet = new MsgActionPacket
        {
            ObjId = 41_001,
            DwParam = 220,
            Timestamp = 123_456,
            Type = MsgActionType.ActionPick,
            Facing = 2,
            PositionX = 0,
            PositionY = 0,
            RemainTime = 0,
            Strings = ["Interrupted"],
        };
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(MsgActionPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet.ObjId, parsed.ObjId);
        Assert.Equal(packet.DwParam, parsed.DwParam);
        Assert.Equal(packet.Timestamp, parsed.Timestamp);
        Assert.Equal(packet.Type, parsed.Type);
        Assert.Equal(packet.Facing, parsed.Facing);
        Assert.Equal(packet.PositionX, parsed.PositionX);
        Assert.Equal(packet.PositionY, parsed.PositionY);
        Assert.Equal(packet.RemainTime, parsed.RemainTime);
        Assert.Equal(packet.Strings, parsed.Strings);
    }

    [Fact]
    public void MsgActionPacketRoundTripsSetLocationFields()
    {
        var packet = new MsgActionPacket
        {
            ObjId = 41_001,
            TargetUid = 400_001,
            DwParam = 1002,
            DwParam2 = -7,
            DwParam3 = 3935,
            SucDone = true,
            TargetPositionX = 111,
            TargetPositionY = 222,
            Timestamp = 1234,
            NpcId = -12,
            Type = MsgActionType.SetLocation,
            Facing = 5,
            PositionX = 830,
            PositionY = 584,
            MapId = 1015,
            ScheduleTime = 10,
            LinkIndex = 2,
            RemainTime = 9,
            MountCharge = true,
            Strings = ["alpha", "beta"],
        };
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(MsgActionPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet.ObjId, parsed.ObjId);
        Assert.Equal(packet.TargetUid, parsed.TargetUid);
        Assert.Equal(packet.DwParam, parsed.DwParam);
        Assert.Equal(packet.DwParam2, parsed.DwParam2);
        Assert.Equal(packet.DwParam3, parsed.DwParam3);
        Assert.Equal(packet.SucDone, parsed.SucDone);
        Assert.Equal(packet.TargetPositionX, parsed.TargetPositionX);
        Assert.Equal(packet.TargetPositionY, parsed.TargetPositionY);
        Assert.Equal(packet.Timestamp, parsed.Timestamp);
        Assert.Equal(packet.NpcId, parsed.NpcId);
        Assert.Equal(packet.Type, parsed.Type);
        Assert.Equal(packet.Facing, parsed.Facing);
        Assert.Equal(packet.PositionX, parsed.PositionX);
        Assert.Equal(packet.PositionY, parsed.PositionY);
        Assert.Equal(packet.MapId, parsed.MapId);
        Assert.Equal(packet.ScheduleTime, parsed.ScheduleTime);
        Assert.Equal(packet.LinkIndex, parsed.LinkIndex);
        Assert.Equal(packet.RemainTime, parsed.RemainTime);
        Assert.Equal(packet.MountCharge, parsed.MountCharge);
        Assert.Equal(packet.Strings, parsed.Strings);
    }

    [Theory]
    [InlineData(7U, (ushort)408)]
    [InlineData(11U, (ushort)476)]
    public void MsgActionPacketParsesSyntheticShortClientActionFrames(uint expectedDwParam, ushort expectedType)
    {
        var frame = ReadFrame(CreateSyntheticProtoFrame((3, expectedDwParam), (12, expectedType)));
        Assert.True(MsgActionPacket.TryParse(frame, out var parsed));
        Assert.Equal(expectedDwParam, parsed.DwParam);
        Assert.Equal((MsgActionType)expectedType, parsed.Type);
        Assert.Equal(0U, parsed.ObjId);
        Assert.Equal(0U, parsed.Timestamp);
        Assert.Equal(0, parsed.Facing);
        Assert.Equal(0U, parsed.PositionX);
        Assert.Equal(0U, parsed.PositionY);
    }

    [Fact]
    public void MsgActionPacketRejectsPacketWithoutActionType()
    {
        var writer = new PacketWriter(MsgActionPacket.Opcode, payloadLength: 2);
        writer.Payload[0] = 0x08;
        writer.Payload[1] = 0x01;

        Assert.False(MsgActionPacket.TryParse(ReadFrame(writer.ToArray()), out _));
    }

    [Fact]
    public void MsgActionPacketRejectsWrongOpcode()
    {
        var writer = new PacketWriter(MsgActionPacket.Opcode + 1, payloadLength: 0);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(MsgActionPacket.TryParse(frame, out _));
    }

    private static byte[] CreateSyntheticProtoFrame(params (int Field, ulong Value)[] fields)
    {
        var payload = new List<byte>();
        foreach (var (field, value) in fields)
        {
            WriteVarint((ulong)(field << 3));
            WriteVarint(value);
        }
        var bytes = new byte[4 + payload.Count];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)bytes.Length);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2046);
        payload.CopyTo(bytes, 4);
        return bytes;

        void WriteVarint(ulong value)
        {
            while (value >= 128)
            {
                payload.Add((byte)((value & 127) | 128));
                value >>= 7;
            }
            payload.Add((byte)value);
        }
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
