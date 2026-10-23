#nullable disable

namespace WolfConquer.Core.World;

// Complete native Union.Member storage/enums, implicit construction, IsOnline and
// pure GetRank and Guild.Member factory. The complete game-client factory now
// lives in its own partial and reads the genuine physical Player owners.
// This standalone nested-member equivalent does not invent a cold Union birth.
internal partial class LegacyNativeUnionMember
{
    public enum Ranks : uint
    {
        Member = 0u,
        Emperror = 1u,
        LeftPremier = 2u,
        RightPremier = 4u,
        LeftMarshal = 8u,
        RightMarshal = 0x10u,
        SecGeneral = 0x20u,
        DefGeneral = 0x40u,
        ParGeneral = 0x80u,
        CoGeneral = 0x100u,
        ImprialGuard = 0x200u,
        Empress = 0x400u,
        SeniorConc = 0x800u,
        Conc = 0x1000u
    }

    public enum MilitaryRanks : uint
    {
        Member = 0u,
        Emperor = 1000u,
        LeftPre = 2000u,
        RightPre = 2010u,
        LeftMar = 2020u,
        RightMar = 2030u,
        SEC_Gen = 2040u,
        DEF_Gen = 2050u,
        PAC_Gen = 2060u,
        CO_Gen = 2070u,
        Imprl_Guard = 3000u,
        Empress = 4000u,
        SeniorConc = 401u,
        Conc = 402u
    }

    public byte ReceiveKick = 0;

    public MilitaryRanks Rank = MilitaryRanks.Member;

    public uint Exploits = 0u;

    public uint MyTreasury;

    public uint UID;

    public uint Class;

    public uint Mesh;

    public ushort Level;

    public LegacyNativeNobilityRank NobilityRank = LegacyNativeNobilityRank.Serf;

    public string Name = "";

    public LegacyNativeGameClientRuntime Owner;

    public bool IsOnline => Owner != null;

    public static LegacyNativeUnionMember CreateMember(LegacyNativeGuildRuntime.Member user,
        MilitaryRanks Rank = MilitaryRanks.Member)
    {
        LegacyNativeUnionMember member = new();
        member.Name = user.Name;
        member.Level = (ushort)user.Level;
        member.UID = user.UID;
        member.Rank = Rank;
        member.Mesh = user.Mesh;
        member.Class = user.Class;
        member.NobilityRank = (LegacyNativeNobilityRank)user.NobilityRank;
        return member;
    }

    public static Ranks GetRank(MilitaryRanks _rank)
    {
        return _rank switch
        {
            MilitaryRanks.CO_Gen => Ranks.CoGeneral,
            MilitaryRanks.Conc => Ranks.Conc,
            MilitaryRanks.DEF_Gen => Ranks.DefGeneral,
            MilitaryRanks.Emperor => Ranks.Emperror,
            MilitaryRanks.Empress => Ranks.Empress,
            MilitaryRanks.Imprl_Guard => Ranks.ImprialGuard,
            MilitaryRanks.LeftMar => Ranks.LeftMarshal,
            MilitaryRanks.LeftPre => Ranks.LeftPremier,
            MilitaryRanks.Member => Ranks.Member,
            MilitaryRanks.PAC_Gen => Ranks.ParGeneral,
            MilitaryRanks.RightMar => Ranks.RightMarshal,
            MilitaryRanks.RightPre => Ranks.RightPremier,
            MilitaryRanks.SEC_Gen => Ranks.SecGeneral,
            MilitaryRanks.SeniorConc => Ranks.SeniorConc,
            _ => Ranks.Member,
        };
    }
}
