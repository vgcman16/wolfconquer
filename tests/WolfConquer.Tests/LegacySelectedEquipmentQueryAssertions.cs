using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Only neutral, non-team, non-transform queries in the selected item fixtures.
// Exact ten-frame source suffix: no filtering, replay, or synthetic self spawn.
internal static class LegacySelectedEquipmentQueryAssertions
{
    internal static (LegacyShowEquipmentPacket Equipment, LegacyPlayerStatusPacket Status,
        LegacyUserTotalRefineLevPacket Refinement) NeutralQuery(
        IReadOnlyList<byte[]> frames, int start, uint ownerUid, bool alternate = false)
    {
        Assert.True(LegacyShowEquipmentPacket.TryParse(Read(frames[start]), out var equipment));
        Assert.Equal(LegacyShowEquipmentPacket.ShowAction, equipment.Action);
        Assert.Equal(alternate ? 1U : 0U, equipment.Alternante);
        Assert.True(LegacyServerInfoPacket.TryParse(Read(frames[start + 1]), out _));
        var beforeClamp = Pair(frames, start + 2, ownerUid);
        Assert.True(LegacyPlayerStatusPacket.TryParse(Read(frames[start + 4]), out var status));
        Assert.Equal(ownerUid, status.Uid);
        Assert.True(status.Fields[1] > 0);
        Assert.Equal((ulong)status.MaxHP, beforeClamp.Maximum);
        var clamp = Pair(frames, start + 5, ownerUid);
        Assert.Equal(beforeClamp.Maximum, clamp.Maximum);
        Assert.Equal(Math.Min(beforeClamp.Hitpoints, beforeClamp.Maximum), clamp.Hitpoints);
        var explicitPair = Pair(frames, start + 7, ownerUid);
        Assert.Equal(clamp, explicitPair);
        Assert.True(LegacyUserTotalRefineLevPacket.TryParse(Read(frames[start + 9]), out var refinement));
        Assert.Equal(ownerUid, refinement.Uid);
        // Every frame has an exact source kind; an extra local spawn cannot hide.
        for (var index = start; index < start + 10; index++)
            Assert.False(LegacyPlayerSpawnPacket.TryParse(Read(frames[index]), out _));
        return (equipment, status, refinement);
    }

    // Loaded seed UID1293600 is weekly Pirate rank1: every admitted query adds21930level10.
    // Exact eleven-frame sequence, including the interleaved spell; no frame filtering.
    internal static (LegacyShowEquipmentPacket Equipment, LegacyPlayerStatusPacket Status,
        LegacyUserTotalRefineLevPacket Refinement) PirateRankQuery(
        IReadOnlyList<byte[]> frames, int start, uint ownerUid)
    {
        Assert.InRange(start, 0, frames.Count - 11);
        Assert.True(LegacyShowEquipmentPacket.TryParse(Read(frames[start]), out var equipment));
        Assert.Equal(LegacyShowEquipmentPacket.ShowAction, equipment.Action);
        Assert.Equal(0U, equipment.Alternante);
        Assert.True(LegacySpellPacket.TryParse(Read(frames[start + 1]), out var rankSpell));
        Assert.Equal(new LegacySpellPacket(Experience: 0, Id: 21_930, Unknown: 0, Level: 10,
            Type: 0, UseSoul: 0, Soul: 0, HashLevelSoul: 0), rankSpell);
        Assert.True(LegacyServerInfoPacket.TryParse(Read(frames[start + 2]), out _));
        var beforeClamp = Pair(frames, start + 3, ownerUid);
        Assert.True(LegacyPlayerStatusPacket.TryParse(Read(frames[start + 5]), out var status));
        Assert.Equal(ownerUid, status.Uid);
        Assert.True(status.MaxHP > 0);
        Assert.Equal((ulong)status.MaxHP, beforeClamp.Maximum);
        var clamp = Pair(frames, start + 6, ownerUid);
        Assert.Equal(beforeClamp.Maximum, clamp.Maximum);
        Assert.Equal(Math.Min(beforeClamp.Hitpoints, beforeClamp.Maximum), clamp.Hitpoints);
        var explicitPair = Pair(frames, start + 8, ownerUid);
        Assert.Equal(clamp, explicitPair);
        Assert.True(LegacyUserTotalRefineLevPacket.TryParse(Read(frames[start + 10]), out var refinement));
        Assert.Equal(ownerUid, refinement.Uid);
        for (var index = start; index < start + 11; index++)
            Assert.False(LegacyPlayerSpawnPacket.TryParse(Read(frames[index]), out _));
        return (equipment, status, refinement);
    }

    internal static void AlternateNotification(byte[] bytes, ushort action, uint itemUid, uint position)
    {
        var frame = Read(bytes);
        Assert.Equal(WorldPacketOpcodes.MsgItemUsage, frame.Header.Opcode);
        Assert.Equal(LegacyShowEquipmentPacket.PayloadLength, frame.Payload.Length);
        var payload = frame.Payload.ToArray();
        Assert.Equal(itemUid, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0)));
        Assert.Equal(position, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(4)));
        Assert.Equal(0U, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(8)));
        Assert.Equal(action, BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(12)));
        Assert.All(payload.Skip(14), value => Assert.Equal((byte)0, value));
    }

    internal static void InactiveAlternate(
        IReadOnlyList<byte[]> frames, int start, uint itemUid, uint position)
    {
        AlternateNotification(frames[start], action: 5, itemUid, position);
        AlternateNotification(frames[start + 1], LegacyShowEquipmentPacket.AllowAlternanteAction,
            itemUid: 0, position: 0);
    }

    private static (ulong Maximum, ulong Hitpoints) Pair(
        IReadOnlyList<byte[]> frames, int start, uint ownerUid)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(Read(frames[start]), out var maximumOnly));
        Assert.Equal(ownerUid, maximumOnly.Uid);
        var maximum = Assert.Single(maximumOnly.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, maximum.Type);
        Assert.True(LegacyMsgUpdatePacket.TryParse(Read(frames[start + 1]), out var pair));
        Assert.Equal(ownerUid, pair.Uid);
        Assert.Collection(pair.Updates,
            entry =>
            {
                Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, entry.Type);
                Assert.Equal(maximum.Value, entry.Value);
            },
            entry => Assert.Equal(LegacyMsgUpdateDataType.Hitpoints, entry.Type));
        return (maximum.Value, pair.Updates[1].Value);
    }

    private static PacketFrame Read(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
