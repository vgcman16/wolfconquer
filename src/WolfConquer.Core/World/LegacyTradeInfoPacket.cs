using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

// Source-derived MsgTrade.TradeInfo output contract, not a new-client capture.
public sealed record LegacyTradeInfoPacket(
    uint Sender,
    uint Receiver,
    string Name,
    uint Level,
    uint BattlePower)
{
    public const ushort Opcode = 2396;

    public bool Un1 { get; init; } = true;
    public uint Un2 { get; init; }
    public uint Un8 { get; init; }
    public uint Un9 { get; init; }
    public bool Spouse { get; init; }
    public bool Friend { get; init; }
    public bool TradePartner { get; init; }
    public bool Mentor { get; init; }
    public bool Apprentice { get; init; }
    public bool Teammate { get; init; }
    public bool GuildMember { get; init; }
    public bool Enemy { get; init; }
    public uint Un18 { get; init; }
    public uint Un19 { get; init; }
    public long Un20 { get; init; }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Name);
        var payload = new List<byte>();
        LegacyProtoWire.WriteBoolField(payload, 1, Un1);
        LegacyProtoWire.WriteVarintField(payload, 2, Un2);
        LegacyProtoWire.WriteVarintField(payload, 3, Sender);
        LegacyProtoWire.WriteStringField(payload, 4, Name);
        LegacyProtoWire.WriteVarintField(payload, 5, Receiver);
        LegacyProtoWire.WriteVarintField(payload, 6, Level);
        LegacyProtoWire.WriteVarintField(payload, 7, BattlePower);
        LegacyProtoWire.WriteVarintField(payload, 8, Un8);
        LegacyProtoWire.WriteVarintField(payload, 9, Un9);
        LegacyProtoWire.WriteBoolField(payload, 10, Spouse);
        LegacyProtoWire.WriteBoolField(payload, 11, Friend);
        LegacyProtoWire.WriteBoolField(payload, 12, TradePartner);
        LegacyProtoWire.WriteBoolField(payload, 13, Mentor);
        LegacyProtoWire.WriteBoolField(payload, 14, Apprentice);
        LegacyProtoWire.WriteBoolField(payload, 15, Teammate);
        LegacyProtoWire.WriteBoolField(payload, 16, GuildMember);
        LegacyProtoWire.WriteBoolField(payload, 17, Enemy);
        LegacyProtoWire.WriteVarintField(payload, 18, Un18);
        LegacyProtoWire.WriteVarintField(payload, 19, Un19);
        LegacyProtoWire.WriteVarintField(payload, 20, unchecked((ulong)Un20));

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.ToArray().CopyTo(writer.Payload);
        return writer.ToArray();
    }
}
