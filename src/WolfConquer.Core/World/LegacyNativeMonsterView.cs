using WolfConquer.Core.Networking;
using System.Collections.Generic;
using System.Linq;

namespace WolfConquer.Core.World;

internal class LegacyNativeMonsterView
{
	public const int ViewThreshold = 18;

	private LegacyNativeMonsterRole role;

	public LegacyNativeMonsterView(LegacyNativeMonsterRole _role)
	{
		role = _role;
	}

	public IEnumerable<ILegacyNativeMapObj> Roles(LegacyNativeGameMap map, LegacyNativeMapObjectType typ)
	{
		return map.View!.Roles(typ, role.X, role.Y, (ILegacyNativeMapObj p) => CanSee(p));
	}

	public ILegacyNativeMapObj? GetTarget(LegacyNativeGameMap map, LegacyNativeMapObjectType typ)
	{
		if (map?.View is not { } view || role is null) return null;
		return view.Roles(typ, role.X, role.Y,
			(ILegacyNativeMapObj p) => CanSee(p) && p.ObjType == LegacyNativeMapObjectType.Player)
			.OrderByDescending((ILegacyNativeMapObj p) => p.IndexInScreen).FirstOrDefault();
	}

	public void SendScreen(LegacyNativePacket msg, LegacyNativeGameMap map)
	{
		foreach (ILegacyNativeMapObj item in Roles(map, LegacyNativeMapObjectType.Player))
		{
			item.Send(msg);
		}
	}

	public bool CanSee(ILegacyNativeMapObj obj)
	{
		if (obj.Map != role.Map)
		{
			return false;
		}
		if (obj.DynamicID != role.DynamicID)
		{
			return false;
		}
		if (obj.UID == role.UID)
		{
			return false;
		}
		return GetDistance(obj.X, obj.Y, role.X, role.Y) <= 18;
	}

	public static short GetDistance(ushort X, ushort Y, ushort X2, ushort Y2)
	{
		short num = 0;
		short num2 = 0;
		if (X >= X2)
		{
			num = (short)(X - X2);
		}
		else if (X2 >= X)
		{
			num = (short)(X2 - X);
		}
		if (Y >= Y2)
		{
			num2 = (short)(Y - Y2);
		}
		else if (Y2 >= Y)
		{
			num2 = (short)(Y2 - Y);
		}
		if (num > num2)
		{
			return num;
		}
		return num2;
	}

	internal void CheckScren()
	{
	}
}
