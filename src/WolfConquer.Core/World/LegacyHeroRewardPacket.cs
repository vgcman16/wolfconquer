using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyHeroRewardAction : byte
{
    Show = 0,
    ClaimPrize = 1,
    ClaimSubPrize = 2,
}

public readonly record struct LegacyHeroRewardPacket(LegacyHeroRewardAction Action, uint Prize)
{
    private const int ActionOffset = 0;
    private const int PrizeOffset = ActionOffset + sizeof(byte);

    public const ushort Opcode = WorldPacketOpcodes.LegacyHeroReward;
    public const int PayloadLength = PrizeOffset + sizeof(uint);

    public static bool TryParse(PacketFrame frame, out LegacyHeroRewardPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = new LegacyHeroRewardPacket(
            (LegacyHeroRewardAction)reader.Payload[ActionOffset],
            reader.ReadUInt32(PrizeOffset));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.Payload[ActionOffset] = (byte)Action;
        writer.WriteUInt32(PrizeOffset, Prize);
        return writer.ToArray();
    }
}

public readonly record struct LegacyHeroRewardListItem(uint Uid, uint Unknown, byte Claim);

public sealed record LegacyHeroRewardListPacket(
    uint Stage,
    byte AllDone,
    IReadOnlyList<LegacyHeroRewardListItem> Items)
{
    private const int StageOffset = 0;
    private const int AllDoneOffset = StageOffset + sizeof(uint);
    private const int CountOffset = AllDoneOffset + sizeof(byte);
    private const int ItemsOffset = CountOffset + sizeof(ushort);
    private const int ItemLength = sizeof(uint) + sizeof(uint) + sizeof(byte);

    public const ushort Opcode = WorldPacketOpcodes.LegacyHeroRewardsInfo;

    public static bool TryParse(PacketFrame frame, out LegacyHeroRewardListPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < ItemsOffset ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        var count = reader.ReadUInt16(CountOffset);
        if (frame.Payload.Length != ItemsOffset + (count * ItemLength))
        {
            return false;
        }

        var items = new List<LegacyHeroRewardListItem>(count);
        for (var index = 0; index < count; index++)
        {
            var offset = ItemsOffset + (index * ItemLength);
            items.Add(new LegacyHeroRewardListItem(
                reader.ReadUInt32(offset),
                reader.ReadUInt32(offset + sizeof(uint)),
                reader.Payload[offset + sizeof(uint) + sizeof(uint)]));
        }

        packet = new LegacyHeroRewardListPacket(
            reader.ReadUInt32(StageOffset),
            reader.Payload[AllDoneOffset],
            items);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Items);

        if (Items.Count > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(Items), "Legacy hero reward item count must fit UInt16.");
        }

        var writer = new PacketWriter(Opcode, ItemsOffset + (Items.Count * ItemLength));
        writer.WriteUInt32(StageOffset, Stage);
        writer.Payload[AllDoneOffset] = AllDone;
        writer.WriteUInt16(CountOffset, (ushort)Items.Count);
        for (var index = 0; index < Items.Count; index++)
        {
            var item = Items[index];
            var offset = ItemsOffset + (index * ItemLength);
            writer.WriteUInt32(offset, item.Uid);
            writer.WriteUInt32(offset + sizeof(uint), item.Unknown);
            writer.Payload[offset + sizeof(uint) + sizeof(uint)] = item.Claim;
        }

        return writer.ToArray();
    }
}
