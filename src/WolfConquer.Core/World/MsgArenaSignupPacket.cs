using System.Buffers;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum MsgArenaSignupDialogType : uint
{
    ArenaIconOn = 0,
    ArenaIconOff = 1,
    StartCountDown = 2,
    ArenaGui = 3,
    OpponentGaveUp = 4,
    Match = 5,
    YouAreKicked = 6,
    StartTheFight = 7,
    Dialog = 8,
    Dialog2 = 9,
    Continue = 10
}

public enum MsgArenaSignupDialogButton : uint
{
    SignUp = 0,
    Win = 1,
    Accept = 1,
    DoGiveUp = 2,
    Lose = 3,
    MatchOff = 3,
    MatchOn = 5
}

public readonly record struct MsgArenaSignupPacket(
    MsgArenaSignupDialogType DialogType,
    MsgArenaSignupDialogButton DialogButton)
{
    public const ushort Opcode = WorldPacketOpcodes.MsgArenaSignup;
    public const int PayloadLength = sizeof(uint) + sizeof(uint);

    public static bool TryParse(PacketFrame frame, out MsgArenaSignupPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = new MsgArenaSignupPacket(
            (MsgArenaSignupDialogType)reader.ReadUInt32(0),
            (MsgArenaSignupDialogButton)reader.ReadUInt32(4));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(0, (uint)DialogType);
        writer.WriteUInt32(4, (uint)DialogButton);
        return writer.ToArray();
    }
}

public readonly record struct MsgArenaSignupResponsePacket(
    MsgArenaSignupDialogType DialogType,
    MsgArenaSignupDialogButton DialogButton,
    uint CharacterUid,
    string CharacterName,
    uint TodayRank,
    uint Class,
    uint ArenaPoints,
    uint Level)
{
    private const int NameLength = 32;
    private const int DialogTypeOffset = 0;
    private const int DialogButtonOffset = DialogTypeOffset + sizeof(uint);
    private const int CharacterUidOffset = DialogButtonOffset + sizeof(uint);
    private const int Reserved0Offset = CharacterUidOffset + sizeof(uint);
    private const int CharacterNameOffset = Reserved0Offset + sizeof(uint);
    private const int TodayRankOffset = CharacterNameOffset + NameLength;
    private const int ClassOffset = TodayRankOffset + sizeof(uint);
    private const int Reserved1Offset = ClassOffset + sizeof(uint);
    private const int ArenaPointsOffset = Reserved1Offset + sizeof(uint);
    private const int LevelOffset = ArenaPointsOffset + sizeof(uint);

    public const ushort Opcode = WorldPacketOpcodes.MsgArenaSignup;
    public const int PayloadLength = LevelOffset + sizeof(uint);

    public static MsgArenaSignupResponsePacket FromRequest(
        MsgArenaSignupPacket request,
        CharacterSnapshot character,
        ArenaStatistics arenaStatistics)
    {
        ArgumentNullException.ThrowIfNull(character);

        return new MsgArenaSignupResponsePacket(
            request.DialogType,
            request.DialogButton,
            checked((uint)character.Uid),
            character.Name,
            arenaStatistics.TodayRank,
            checked((uint)character.Class),
            arenaStatistics.ArenaPoints,
            checked((uint)character.Level));
    }

    public static bool TryParse(PacketFrame frame, out MsgArenaSignupResponsePacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = new MsgArenaSignupResponsePacket(
            (MsgArenaSignupDialogType)reader.ReadUInt32(DialogTypeOffset),
            (MsgArenaSignupDialogButton)reader.ReadUInt32(DialogButtonOffset),
            reader.ReadUInt32(CharacterUidOffset),
            reader.ReadCString(CharacterNameOffset, NameLength, Encoding.UTF8),
            reader.ReadUInt32(TodayRankOffset),
            reader.ReadUInt32(ClassOffset),
            reader.ReadUInt32(ArenaPointsOffset),
            reader.ReadUInt32(LevelOffset));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(DialogTypeOffset, (uint)DialogType);
        writer.WriteUInt32(DialogButtonOffset, (uint)DialogButton);
        writer.WriteUInt32(CharacterUidOffset, CharacterUid);
        writer.WriteUInt32(Reserved0Offset, 0);
        WriteFixedUtf8String(writer.Payload.Slice(CharacterNameOffset, NameLength), CharacterName);
        writer.WriteUInt32(TodayRankOffset, TodayRank);
        writer.WriteUInt32(ClassOffset, Class);
        writer.WriteUInt32(Reserved1Offset, 0);
        writer.WriteUInt32(ArenaPointsOffset, ArenaPoints);
        writer.WriteUInt32(LevelOffset, Level);
        return writer.ToArray();
    }

    private static void WriteFixedUtf8String(Span<byte> destination, string value)
    {
        destination.Clear();
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var encoder = Encoding.UTF8.GetEncoder();
        encoder.Convert(value.AsSpan(), destination, flush: true, out _, out _, out _);
    }
}
