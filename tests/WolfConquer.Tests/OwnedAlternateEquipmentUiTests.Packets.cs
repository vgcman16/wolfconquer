using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedAlternateEquipmentUiTests
{
    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
    private static List<LegacyMsgUpdatePacket> ParseMsgUpdates(IEnumerable<byte[]> frames) =>
        frames.Select(bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) ? packet : null)
            .Where(packet => packet is not null).Select(packet => packet!).ToList();
    private static void SourceAlternateUiAssertItemPrefix(IReadOnlyList<byte[]> frames)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(frames[0]), out var remove));
        Assert.Equal(92_602U, remove.Id);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, remove.Action);
        Assert.Equal(0UL, remove.DwParam);
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(frames[1]), out var slot));
        Assert.Equal(92_602U, slot.Id);
        Assert.Equal(MsgItemUsageAction.Unequip, slot.Action);
        Assert.Equal(51UL, slot.DwParam);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(frames[2]), out var added));
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(frames[3]), out var updated));
        Assert.Equal(92_602U, added.Uid);
        Assert.Equal(92_602U, updated.Uid);
        Assert.Equal(111_097U, added.ItemId);
        Assert.Equal(111_097U, updated.ItemId);
        Assert.Equal((byte)51, added.Position);
        Assert.Equal((byte)51, updated.Position);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, added.Mode);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, updated.Mode);
    }

    private static bool SourceAlternateUiIs5(byte[] bytes)
    {
        var frame = ReadFrame(bytes);
        return frame.Header.Opcode == WorldPacketOpcodes.MsgItemUsage &&
            frame.Payload.Length == LegacyShowEquipmentPacket.PayloadLength &&
            BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.ToArray().AsSpan(12)) == 5;
    }

}
