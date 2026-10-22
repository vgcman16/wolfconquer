#nullable disable

namespace WolfConquer.Core.World;

internal partial class LegacyNativeRoleView
{
    // Complete native RoleView125..133; this is raw map lookup, not CanSee filtering.
    public bool TryGetValue(uint UID, out ILegacyNativeMapObj obj, LegacyNativeMapObjectType typ)
    {
        if (Owner.Map != null)
        {
            return Owner.Map.View.TryGetObject<ILegacyNativeMapObj>(UID, typ, Owner.Player.X, Owner.Player.Y, out obj);
        }
        obj = null;
        return false;
    }
}
