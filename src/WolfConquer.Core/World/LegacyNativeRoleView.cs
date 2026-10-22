#nullable disable
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Native RoleView birth plus the complete Packet broadcast/visibility path used by Mesh updates.
// Spawn serialization, monster actions, other view mutations and byte[] broadcasts remain absent.
internal partial class LegacyNativeRoleView
{
    public const int ViewThreshold = 18;

    public LegacyNativeMonsterActionHandler MobActions = new LegacyNativeMonsterActionHandler();

    public LegacyNativeDbSafeDictionary<uint, ILegacyNativeMapObj>[] src;

    public LegacyNativeGameClientRuntime Owner;

    public bool InColorScrenn = false;

    public LegacyNativePlayerRuntime GetPlayer()
    {
        return Owner.Player;
    }

    public LegacyNativeRoleView(LegacyNativeGameClientRuntime _client)
    {
        Owner = _client;
        src = new LegacyNativeDbSafeDictionary<uint, ILegacyNativeMapObj>[7];
        for (byte b = 0; b < 7; b++)
        {
            src[b] = new LegacyNativeDbSafeDictionary<uint, ILegacyNativeMapObj>();
        }
    }

    public void SendView(LegacyNativePacket msg, bool me)
    {
        if (me)
        {
            Owner.Send(msg);
        }
        foreach (ILegacyNativeMapObj item in Roles(LegacyNativeMapObjectType.Player))
        {
            item.Send(msg);
        }
    }

    public IEnumerable<ILegacyNativeMapObj> Roles(LegacyNativeMapObjectType typ, Predicate<bool> P = null)
    {
        if (Owner.Map != null)
        {
            if (P != null)
            {
                return Owner.Map.View.Roles(typ, Owner.Player.X, Owner.Player.Y,
                    (ILegacyNativeMapObj p) => CanSee(p) && P(p.Alive));
            }
            return Owner.Map.View.Roles(typ, Owner.Player.X, Owner.Player.Y,
                (ILegacyNativeMapObj p) => CanSee(p));
        }
        return new ILegacyNativeMapObj[0];
    }

    public bool CanSee(ILegacyNativeMapObj obj)
    {
        try
        {
            if (obj == null)
            {
                return false;
            }
            if (obj.Map != Owner.Player.Map)
            {
                return false;
            }
            if (!obj.AllowDynamic && obj.DynamicID != Owner.Player.DynamicID)
            {
                return false;
            }
            if (obj.UID == Owner.Player.UID)
            {
                return false;
            }
            if (obj.ObjType == LegacyNativeMapObjectType.Player &&
                (obj.Map == 10428 || Owner.Player.Map == 10428))
            {
                return false;
            }
            return LegacyNativeRoleCore.GetDistance(obj.X, obj.Y, Owner.Player.X, Owner.Player.Y) <= 18;
        }
        catch (Exception e)
        {
            LegacyNativeConsole.WriteException(e);
            return false;
        }
    }
}
