#nullable disable

namespace WolfConquer.Core.World;

// Complete native MsgGuildInformation state/Create; no packet or membership substitute.
internal class LegacyNativeMsgGuildInformation
{
    public uint GuildID;

    public long SilverFund;

    public uint ConquerPointFund;

    public uint MyRank;

    public uint Level;

    public uint CreateTime;

    public string LeaderName;

    public uint Leaderid;

    public uint ArsenalBP;

    public ulong Material;

    public ulong Prestige;

    public LegacyNativeGuildRuntime.ClassFlag Recruitment_Flag;

    public uint Recruitment_Battle_Power;

    public bool RecruitmentON;

    public bool RecruitmentOFF;

    public static LegacyNativeMsgGuildInformation Create()
    {
        return new LegacyNativeMsgGuildInformation();
    }
}
