using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyBlessingInfoAction : uint
{
    Show = 0,
    ClaimOnlineTraining = 1,
    ClaimHuntBlessing = 2,
}

public readonly record struct LegacyBlessingInfoPacket(
    LegacyBlessingInfoAction Action,
    uint OnlineBlessingTraining,
    uint HuntBlessing)
{
    private const int ActionOffset = 0;
    private const int OnlineBlessingTrainingOffset = ActionOffset + sizeof(uint);
    private const int HuntBlessingOffset = OnlineBlessingTrainingOffset + sizeof(uint);

    public const ushort Opcode = WorldPacketOpcodes.LegacyBlessingInfo;
    public const int PayloadLength = HuntBlessingOffset + sizeof(uint);

    public static bool TryParse(PacketFrame frame, out LegacyBlessingInfoPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = new LegacyBlessingInfoPacket(
            (LegacyBlessingInfoAction)reader.ReadUInt32(ActionOffset),
            reader.ReadUInt32(OnlineBlessingTrainingOffset),
            reader.ReadUInt32(HuntBlessingOffset));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(ActionOffset, (uint)Action);
        writer.WriteUInt32(OnlineBlessingTrainingOffset, OnlineBlessingTraining);
        writer.WriteUInt32(HuntBlessingOffset, HuntBlessing);
        return writer.ToArray();
    }
}

public enum LegacyClaimAction : uint
{
    ClaimExperience = 1,
    ClaimAmountExpBalls = 2,
    ClaimHeavenBlessing = 3,
    ClaimPlus = 4,
    Show = 5,
}

public readonly record struct LegacyClaimPacket(
    uint Mode,
    uint MentorUid,
    uint OnlineBlessingTraining,
    uint HuntBlessing,
    ulong MentorExperience,
    ushort MentorHeavensBlessing,
    ushort PlusStone)
{
    private const int ModeOffset = 0;
    private const int MentorUidOffset = ModeOffset + sizeof(uint);
    private const int OnlineBlessingTrainingOffset = MentorUidOffset + sizeof(uint);
    private const int HuntBlessingOffset = OnlineBlessingTrainingOffset + sizeof(uint);
    private const int ReservedOffset = HuntBlessingOffset + sizeof(uint);
    private const int MentorExperienceOffset = ReservedOffset + sizeof(uint);
    private const int MentorHeavensBlessingOffset = MentorExperienceOffset + sizeof(ulong);
    private const int PlusStoneOffset = MentorHeavensBlessingOffset + sizeof(ushort);
    private const int TailOffset = PlusStoneOffset + sizeof(ushort);

    public const ushort Opcode = WorldPacketOpcodes.LegacyClaim;
    public const int RequestPayloadLength = TailOffset;
    public const int ResponsePayloadLength = TailOffset + sizeof(uint);

    public LegacyClaimAction? Action => Enum.IsDefined(typeof(LegacyClaimAction), Mode)
        ? (LegacyClaimAction)Mode
        : null;

    public static LegacyClaimPacket Show(
        uint mentorUid,
        uint onlineBlessingTraining,
        uint huntBlessing,
        ulong mentorExperience,
        ushort mentorHeavensBlessing,
        ushort plusStone)
    {
        return new LegacyClaimPacket(
            (uint)LegacyClaimAction.Show,
            mentorUid,
            onlineBlessingTraining,
            huntBlessing,
            mentorExperience,
            mentorHeavensBlessing,
            plusStone);
    }

    public static bool TryParse(PacketFrame frame, out LegacyClaimPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < RequestPayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = new LegacyClaimPacket(
            reader.ReadUInt32(ModeOffset),
            reader.ReadUInt32(MentorUidOffset),
            reader.ReadUInt32(OnlineBlessingTrainingOffset),
            reader.ReadUInt32(HuntBlessingOffset),
            reader.ReadUInt64(MentorExperienceOffset),
            reader.ReadUInt16(MentorHeavensBlessingOffset),
            reader.ReadUInt16(PlusStoneOffset));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, ResponsePayloadLength);
        writer.WriteUInt32(ModeOffset, Mode);
        writer.WriteUInt32(MentorUidOffset, MentorUid);
        writer.WriteUInt32(OnlineBlessingTrainingOffset, OnlineBlessingTraining);
        writer.WriteUInt32(HuntBlessingOffset, HuntBlessing);
        writer.WriteUInt64(MentorExperienceOffset, MentorExperience);
        writer.WriteUInt16(MentorHeavensBlessingOffset, MentorHeavensBlessing);
        writer.WriteUInt16(PlusStoneOffset, PlusStone);
        return writer.ToArray();
    }
}
