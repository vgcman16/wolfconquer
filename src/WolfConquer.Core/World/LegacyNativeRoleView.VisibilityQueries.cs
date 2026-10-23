namespace WolfConquer.Core.World;

// Whole independent native visibility/membership/byte-broadcast families.
// This is not the still-held CanAdd or full Role population lifecycle.
internal partial class LegacyNativeRoleView
{
public bool SameLocation(LegacyNativeMapObjectType typ, out ILegacyNativeMapObj? obj)
	{
		foreach (ILegacyNativeMapObj item in Roles(typ))
		{
			if (item.X == GetPlayer().X && item.Y == GetPlayer().Y)
			{
				obj = item;
				return true;
			}
		}
		obj = null;
		return false;
	}

public bool Contains(ILegacyNativeMapObj obj)
	{
		if (obj.UID == Owner.Player.UID)
		{
			return true;
		}
		if (Owner.Map != null)
		{
			return Owner.Map!.View.Contain(obj.UID, Owner.Player.X, Owner.Player.Y);
		}
		return false;
	}

public bool ContainMobInScreen(string name)
	{
		bool result = false;
		foreach (ILegacyNativeMapObj item in Roles(LegacyNativeMapObjectType.Monster))
		{
			LegacyNativeMonsterRole monsterRole = (item as LegacyNativeMonsterRole)!;
			if (monsterRole.Name == name)
			{
				result = true;
				break;
			}
		}
		return result;
	}

public IEnumerable<ILegacyNativeMapObj> AttackableRoles()
	{
		foreach (ILegacyNativeMapObj item in Owner.Map!.View.Roles(LegacyNativeMapObjectType.Monster, Owner.Player.X, Owner.Player.Y, (ILegacyNativeMapObj x) => CanSee(x)))
		{
			yield return item;
		}
		foreach (ILegacyNativeMapObj item2 in Owner.Map!.View.Roles(LegacyNativeMapObjectType.Player, Owner.Player.X, Owner.Player.Y, (ILegacyNativeMapObj x) => CanSee(x)))
		{
			yield return item2;
		}
		foreach (ILegacyNativeMapObj item3 in Owner.Map!.View.Roles(LegacyNativeMapObjectType.SobNpc, Owner.Player.X, Owner.Player.Y, (ILegacyNativeMapObj x) => CanSee(x)))
		{
			yield return item3;
		}
	}

public void SendView(byte[] msg, bool me)
	{
		if (me)
		{
			Owner.Send(msg);
		}
		foreach (ILegacyNativeMapObj item in Roles(LegacyNativeMapObjectType.Player))
		{
			(item as LegacyNativePlayerRuntime)!.Owner.Send(msg);
		}
	}
}
