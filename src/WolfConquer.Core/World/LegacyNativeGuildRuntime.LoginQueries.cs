namespace WolfConquer.Core.World;

internal partial class LegacyNativeGuildRuntime
{
    // Whole native Guild246..257: no fallback union or membership projection.
    public LegacyNativeUnionRuntime? GetUnion
    {
        get
        {
            if (LegacyNativeUnionRuntime.UnionPoll.TryGetValue(UnionID, out var value))
            {
                return value;
            }
            return null;
        }
    }

    // Whole native Guild502..509; RankMember really is unused and the clamp mutates Info.
    public uint ShareMemberPotency(LegacyNativeGuildMemberRank RankMember)
    {
        if (Info.ArsenalBP > 15)
        {
            Info.ArsenalBP = 15u;
        }
        return Info.ArsenalBP;
    }
}
