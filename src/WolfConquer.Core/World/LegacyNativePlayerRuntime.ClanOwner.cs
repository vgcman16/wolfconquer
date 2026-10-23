namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Source Player897/899 and whole MyClanMember1405..1422, including its lazy identity cache.
    private LegacyNativeClanMember? clanmemb;

    internal LegacyNativeClanRuntime? MyClan;

    internal LegacyNativeClanMember? MyClanMember
    {
        get
        {
            if (clanmemb == null && MyClan != null)
            {
                MyClan.Members.TryGetValue(UID, out clanmemb);
            }
            return clanmemb;
        }
        set
        {
            clanmemb = value;
        }
    }
}
