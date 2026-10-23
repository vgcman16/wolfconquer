using System.Linq;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeUnionRuntime
{
    // Whole native Union174..200 queries. Array copies retain actual member/guild references.
    public LegacyNativeGuildRuntime[] GetOrderGuilds => Guilds.Values.OrderBy((LegacyNativeGuildRuntime p) => p.Members.Count).ToArray();
    public static LegacyNativeUnionRuntime[] GetOrderyUnions => UnionPoll.Values.OrderByDescending((LegacyNativeUnionRuntime p) => p.GoldBrick).ToArray();
    public LegacyNativeUnionMember[] GetOnlineMembers => Members.Values.OrderByDescending((LegacyNativeUnionMember p) => p.IsOnline).ToArray();
    public LegacyNativeUnionMember[] UnionOfficials => UnionRanks.Values.OrderBy((LegacyNativeUnionMember p) => p.Rank).Take(9).ToArray();
    public LegacyNativeUnionMember[] GetImperialHarem => ImperialHarem.Values.ToArray();
    public static LegacyNativeUnionRuntime[] Top3Unions => UnionPoll.Values.OrderByDescending((LegacyNativeUnionRuntime p) => p.GoldBrick).Take(3).ToArray();

    public int GetImperialHaremCount(LegacyNativeUnionMember.MilitaryRanks rank)
    {
        return ImperialHarem.Values.Where((LegacyNativeUnionMember p) => p.Rank == rank).Count();
    }

    public bool IsImperialHaremRank(LegacyNativeUnionMember.MilitaryRanks rank)
    {
        return rank == LegacyNativeUnionMember.MilitaryRanks.Empress || rank == LegacyNativeUnionMember.MilitaryRanks.Conc || rank == LegacyNativeUnionMember.MilitaryRanks.SeniorConc;
    }

    public LegacyNativeUnionMember? GetOfficialMemberByUID(uint UID)
    {
        return UnionRanks.Values.Where((LegacyNativeUnionMember p) => p.UID == UID).FirstOrDefault();
    }

    public LegacyNativeUnionMember? GetImperialHaremMemberByUID(uint UID)
    {
        return ImperialHarem.Values.Where((LegacyNativeUnionMember p) => p.UID == UID).FirstOrDefault();
    }
}
