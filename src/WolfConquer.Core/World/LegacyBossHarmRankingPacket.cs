using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyBossHarmRankingAction : uint
{
    Show = 0,
    ShowRespondForFirstThree = 1,
    ShowRespondForTheRest = 2,
}

public sealed record LegacyBossHarmRankingPacket(
    LegacyBossHarmRankingAction Type,
    uint MonsterId,
    IReadOnlyList<LegacyBossHarmRankingPacketEntry> Hunters)
{
    private const int TypeField = 1;
    private const int MonsterIdField = 2;
    private const int HuntersField = 3;
    private const int RankField = 1;
    private const int ServerIdField = 2;
    private const int HunterUidField = 3;
    private const int HunterNameField = 4;
    private const int HunterScoreField = 5;

    public const ushort Opcode = WorldPacketOpcodes.LegacyBossHarmRanking;

    public static LegacyBossHarmRankingPacket CreateShowForTheRest(
        uint monsterId,
        IReadOnlyList<LegacyBossHarmRankingEntry> ranking,
        uint serverId)
    {
        ArgumentNullException.ThrowIfNull(ranking);

        return new LegacyBossHarmRankingPacket(
            LegacyBossHarmRankingAction.ShowRespondForTheRest,
            monsterId,
            ranking
                .Select(entry => new LegacyBossHarmRankingPacketEntry(
                    CheckedUInt32(entry.Rank, nameof(entry.Rank)),
                    serverId,
                    entry.PlayerUid,
                    entry.PlayerName,
                    CheckedUInt32(entry.Damage, nameof(entry.Damage))))
                .ToArray());
    }

    public static bool TryParse(PacketFrame frame, out LegacyBossHarmRankingPacket packet)
    {
        packet = default!;
        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new LegacyProtoWire.Reader(frame.Payload.ToArray());
        LegacyBossHarmRankingAction type = default;
        uint monsterId = 0;
        var hunters = new List<LegacyBossHarmRankingPacketEntry>();
        var hasType = false;
        var hasMonsterId = false;

        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case TypeField:
                    if (wireType != LegacyProtoWire.VarintWireType ||
                        !reader.TryReadUInt32(out var typeValue) ||
                        !Enum.IsDefined(typeof(LegacyBossHarmRankingAction), typeValue))
                    {
                        return false;
                    }

                    type = (LegacyBossHarmRankingAction)typeValue;
                    hasType = true;
                    break;
                case MonsterIdField:
                    if (wireType != LegacyProtoWire.VarintWireType ||
                        !reader.TryReadUInt32(out monsterId))
                    {
                        return false;
                    }

                    hasMonsterId = true;
                    break;
                case HuntersField:
                    if (wireType != LegacyProtoWire.LengthDelimitedWireType ||
                        !reader.TryReadLengthDelimited(out var hunterPayload) ||
                        !TryParseHunter(hunterPayload, out var hunter))
                    {
                        return false;
                    }

                    hunters.Add(hunter);
                    break;
                default:
                    if (!reader.TrySkipField(wireType))
                    {
                        return false;
                    }

                    break;
            }
        }

        if (!hasType || !hasMonsterId)
        {
            return false;
        }

        packet = new LegacyBossHarmRankingPacket(type, monsterId, hunters);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        LegacyProtoWire.WriteVarintField(payload, TypeField, (uint)Type);
        LegacyProtoWire.WriteVarintField(payload, MonsterIdField, MonsterId);
        foreach (var hunter in Hunters)
        {
            LegacyProtoWire.WriteBytesField(payload, HuntersField, CreateHunterPayload(hunter));
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static byte[] CreateHunterPayload(LegacyBossHarmRankingPacketEntry hunter)
    {
        var payload = new List<byte>();
        LegacyProtoWire.WriteVarintField(payload, RankField, hunter.Rank);
        LegacyProtoWire.WriteVarintField(payload, ServerIdField, hunter.ServerId);
        LegacyProtoWire.WriteVarintField(payload, HunterUidField, hunter.HunterUid);
        LegacyProtoWire.WriteStringField(payload, HunterNameField, hunter.HunterName);
        LegacyProtoWire.WriteVarintField(payload, HunterScoreField, hunter.HunterScore);
        return payload.ToArray();
    }

    private static bool TryParseHunter(
        ReadOnlySpan<byte> payload,
        out LegacyBossHarmRankingPacketEntry hunter)
    {
        hunter = default!;

        var reader = new LegacyProtoWire.Reader(payload);
        uint rank = 0;
        uint serverId = 0;
        uint hunterUid = 0;
        var hunterName = string.Empty;
        uint hunterScore = 0;
        var hasRank = false;
        var hasServerId = false;
        var hasHunterUid = false;
        var hasHunterName = false;
        var hasHunterScore = false;

        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case RankField:
                    if (wireType != LegacyProtoWire.VarintWireType ||
                        !reader.TryReadUInt32(out rank))
                    {
                        return false;
                    }

                    hasRank = true;
                    break;
                case ServerIdField:
                    if (wireType != LegacyProtoWire.VarintWireType ||
                        !reader.TryReadUInt32(out serverId))
                    {
                        return false;
                    }

                    hasServerId = true;
                    break;
                case HunterUidField:
                    if (wireType != LegacyProtoWire.VarintWireType ||
                        !reader.TryReadUInt32(out hunterUid))
                    {
                        return false;
                    }

                    hasHunterUid = true;
                    break;
                case HunterNameField:
                    if (wireType != LegacyProtoWire.LengthDelimitedWireType ||
                        !reader.TryReadString(out hunterName))
                    {
                        return false;
                    }

                    hasHunterName = true;
                    break;
                case HunterScoreField:
                    if (wireType != LegacyProtoWire.VarintWireType ||
                        !reader.TryReadUInt32(out hunterScore))
                    {
                        return false;
                    }

                    hasHunterScore = true;
                    break;
                default:
                    if (!reader.TrySkipField(wireType))
                    {
                        return false;
                    }

                    break;
            }
        }

        if (!hasRank || !hasServerId || !hasHunterUid || !hasHunterName || !hasHunterScore)
        {
            return false;
        }

        hunter = new LegacyBossHarmRankingPacketEntry(
            rank,
            serverId,
            hunterUid,
            hunterName,
            hunterScore);
        return true;
    }

    private static uint CheckedUInt32(int value, string name)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(name);
        }

        return (uint)value;
    }

    private static uint CheckedUInt32(ulong value, string name)
    {
        if (value > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name);
        }

        return (uint)value;
    }
}

public sealed record LegacyBossHarmRankingPacketEntry(
    uint Rank,
    uint ServerId,
    uint HunterUid,
    string HunterName,
    uint HunterScore);
