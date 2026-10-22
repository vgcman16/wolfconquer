namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Actual Player635/933/935 physical slots; no outer Union/Nobility birth.
    internal LegacyNativeUnionMember? UnionMemeber = null;

    internal LegacyNativeNobility? Nobility = null;

    private LegacyNativeNobilityRank _NobilityRank;

    // Internal only because the complete native enum is an internal owner.
    internal LegacyNativeNobilityRank NobilityRank
    {
        get
        {
            return _NobilityRank;
        }
        set
        {
            _NobilityRank = value;
            if (MyGuild != null && MyGuildMember != null)
            {
                MyGuildMember.NobilityRank = (uint)_NobilityRank;
            }
            if (UnionMemeber != null)
            {
                UnionMemeber.NobilityRank = _NobilityRank;
            }
        }
    }
}
