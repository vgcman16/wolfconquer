using System.Buffers;
using WolfConquer.Core.Packets;
using DataPokerTable = WolfConquer.Core.Data.PokerTable;

namespace WolfConquer.Core.World;

public readonly record struct LegacyPokerTablePlayer(uint Uid, byte Seat, byte Status);

public sealed record LegacyPokerTablePacket(
    uint Uid,
    ushort X,
    ushort Y,
    uint Mesh,
    uint TableNumber,
    bool Unlimited,
    bool IsCps,
    ulong MinBet,
    byte State,
    ulong TotalPot,
    IReadOnlyList<LegacyPokerTablePlayer> Players)
{
    private const int UidOffset = 0;
    private const int Reserved1Offset = UidOffset + sizeof(uint);
    private const int XOffset = Reserved1Offset + sizeof(ulong);
    private const int YOffset = XOffset + sizeof(ushort);
    private const int MeshOffset = YOffset + sizeof(ushort);
    private const int Reserved2Offset = MeshOffset + sizeof(uint);
    private const int TableNumberOffset = Reserved2Offset + sizeof(ushort);
    private const int UnlimitedOffset = TableNumberOffset + sizeof(uint);
    private const int IsCpsOffset = UnlimitedOffset + sizeof(int);
    private const int MinBetOffset = IsCpsOffset + sizeof(int);
    private const int Reserved3Offset = MinBetOffset + sizeof(ulong);
    private const int StateOffset = Reserved3Offset + sizeof(int);
    private const int TotalPotOffset = StateOffset + sizeof(byte);
    private const int PlayerCountOffset = TotalPotOffset + sizeof(ulong);
    private const int PlayersOffset = PlayerCountOffset + sizeof(byte);
    private const int PlayerLength = sizeof(uint) + sizeof(byte) + sizeof(byte);

    public const ushort Opcode = WorldPacketOpcodes.LegacyPokerTable;
    public const int BasePayloadLength = PlayersOffset;
    public const int ShowHandMapId = 1858;
    public const int CpsMapId = 1860;

    public int MapId => IsCps ? CpsMapId : ShowHandMapId;

    public static LegacyPokerTablePacket FromPokerTable(DataPokerTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (table.Values.Count < 7)
        {
            throw new ArgumentOutOfRangeException(nameof(table), "Poker table rows require at least seven value fields.");
        }

        return new LegacyPokerTablePacket(
            CheckedUInt32(table.Id, nameof(table.Id)),
            CheckedUInt16(table.Values[0], "poker table x"),
            CheckedUInt16(table.Values[1], "poker table y"),
            CheckedUInt32(table.Values[2], "poker table mesh"),
            CheckedUInt32(table.Values[3], "poker table number"),
            table.Values[4] == 1,
            table.Values[5] == 1,
            CheckedUInt32(table.Values[6], "poker table minimum bet"),
            State: 0,
            TotalPot: 0,
            Players: []);
    }

    public static int PayloadLengthForPlayerCount(int playerCount)
    {
        if (playerCount < 0 || playerCount > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(playerCount), "Legacy poker table player count must fit in one byte.");
        }

        return checked(BasePayloadLength + (playerCount * PlayerLength));
    }

    public static bool TryParse(PacketFrame frame, out LegacyPokerTablePacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader) ||
            frame.Payload.Length < BasePayloadLength)
        {
            return false;
        }

        if (reader.ReadUInt64(Reserved1Offset) != 0 ||
            reader.ReadUInt16(Reserved2Offset) != 0 ||
            reader.ReadInt32(Reserved3Offset) != 0)
        {
            return false;
        }

        var unlimited = reader.ReadInt32(UnlimitedOffset);
        var isCps = reader.ReadInt32(IsCpsOffset);
        if (unlimited is not 0 and not 1 ||
            isCps is not 0 and not 1)
        {
            return false;
        }

        var playerCount = reader.Payload[PlayerCountOffset];
        if (reader.Payload.Length != PayloadLengthForPlayerCount(playerCount))
        {
            return false;
        }

        var players = new LegacyPokerTablePlayer[playerCount];
        for (var index = 0; index < players.Length; index++)
        {
            var offset = PlayersOffset + (index * PlayerLength);
            players[index] = new LegacyPokerTablePlayer(
                reader.ReadUInt32(offset),
                reader.Payload[offset + sizeof(uint)],
                reader.Payload[offset + sizeof(uint) + sizeof(byte)]);
        }

        packet = new LegacyPokerTablePacket(
            reader.ReadUInt32(UidOffset),
            reader.ReadUInt16(XOffset),
            reader.ReadUInt16(YOffset),
            reader.ReadUInt32(MeshOffset),
            reader.ReadUInt32(TableNumberOffset),
            unlimited == 1,
            isCps == 1,
            reader.ReadUInt64(MinBetOffset),
            reader.Payload[StateOffset],
            reader.ReadUInt64(TotalPotOffset),
            players);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Players);

        var writer = new PacketWriter(Opcode, PayloadLengthForPlayerCount(Players.Count));
        writer.WriteUInt32(UidOffset, Uid);
        writer.WriteUInt64(Reserved1Offset, 0);
        writer.WriteUInt16(XOffset, X);
        writer.WriteUInt16(YOffset, Y);
        writer.WriteUInt32(MeshOffset, Mesh);
        writer.WriteUInt16(Reserved2Offset, 0);
        writer.WriteUInt32(TableNumberOffset, TableNumber);
        writer.WriteInt32(UnlimitedOffset, Unlimited ? 1 : 0);
        writer.WriteInt32(IsCpsOffset, IsCps ? 1 : 0);
        writer.WriteUInt64(MinBetOffset, MinBet);
        writer.WriteInt32(Reserved3Offset, 0);
        writer.Payload[StateOffset] = State;
        writer.WriteUInt64(TotalPotOffset, TotalPot);
        writer.Payload[PlayerCountOffset] = checked((byte)Players.Count);

        for (var index = 0; index < Players.Count; index++)
        {
            var player = Players[index];
            var offset = PlayersOffset + (index * PlayerLength);
            writer.WriteUInt32(offset, player.Uid);
            writer.Payload[offset + sizeof(uint)] = player.Seat;
            writer.Payload[offset + sizeof(uint) + sizeof(byte)] = player.Status;
        }

        return writer.ToArray();
    }

    private static ushort CheckedUInt16(int value, string name)
    {
        if (value is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy poker table value cannot be outside ushort range.");
        }

        return (ushort)value;
    }

    private static uint CheckedUInt32(int value, string name)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy poker table value cannot be outside uint range.");
        }

        return (uint)value;
    }
}
