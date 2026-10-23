namespace WolfConquer.Core.World;

internal partial class LegacyNativeUnionMember
{
    // Complete native Union76..88 factory, now its genuine Player inputs exist.
    public static LegacyNativeUnionMember CreateMember(LegacyNativeGameClientRuntime user,
        MilitaryRanks Rank = MilitaryRanks.Member)
    {
        LegacyNativeUnionMember member = new LegacyNativeUnionMember();
        member.Name = user.Player.Name;
        member.Level = user.Player.Level;
        member.UID = user.Player.UID;
        member.Owner = user;
        member.Rank = Rank;
        member.Mesh = user.Player.Mesh;
        member.Class = user.Player.Class;
        member.NobilityRank = user.Player.NobilityRank;
        return member;
    }
}
