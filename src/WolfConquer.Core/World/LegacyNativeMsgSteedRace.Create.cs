using WolfConquer.Core.Networking;
namespace WolfConquer.Core.World;
internal partial class LegacyNativeMsgSteedRace
{
public void Create()
	{
		int num;
		do
		{
			num = LegacyNativePoolRandom.GetRandom.Next(RaceSettings.Length);
		}
		while (!LegacyNativePoolMaps.ServerMaps!.ContainsKey((ushort)RaceSettings[num][0]));
		Create(RaceSettings[num][0]);
	}

public void Create(uint mapId)
	{
		int num = -1;
		for (int i = 0; i < RaceSettings.Length; i++)
		{
			if (RaceSettings[i][0] == mapId)
			{
				num = i;
				break;
			}
		}
		if (num == -1)
		{
			return;
		}
		Settings = RaceSettings[num];
		MAPID = (ushort)Settings![0];
		Map = LegacyNativePoolMaps.ServerMaps![MAPID];
		IEnumerable<ILegacyNativeMapObj> allMapRoles = Map!.View!.GetAllMapRoles(LegacyNativeMapObjectType.StaticRole);
		foreach (ILegacyNativeMapObj item in allMapRoles)
		{
			Map!.RemoveStaticRole(item);
		}
		Map!.View!.ClearMap(LegacyNativeMapObjectType.SobNpc);
		Gate = new LegacyNativeSobNpcRuntime();
		Gate!.UID = 19501u;
		Gate!.ObjType = LegacyNativeMapObjectType.SobNpc;
		Gate!.Map = MAPID;
		Gate!.X = (ushort)Settings![6];
		Gate!.Y = (ushort)Settings![7];
		Gate!.Mesh = (LegacyNativeSobNpcRuntime.StaticMesh)Settings![8];
		Gate!.Name = " ";
		Gate!.Type = LegacyNativeNpcType.Furniture;
		Gate!.Sort = 1;
		GateSetX = Gate!.X;
		GateSetY = Gate!.Y;
		Map!.View!.EnterMap((ILegacyNativeMapObj)Gate);
		Map!.SetFlagNpc(Gate!.X, Gate!.Y);
		Init();
	}

private void Init()
	{
		isOn = (InvitationsOut = (InvitationsExpired = (GateOpen = (FiveSecondsLeft = false))));
		GeneratePotions();
		Records = 0;
	}

private void GeneratePotions()
	{
		uint num = 100u;
		Tuple<ushort, ushort, int>[] array = new Tuple<ushort, ushort, int>[3]
		{
			new Tuple<ushort, ushort, int>((ushort)Settings![9], (ushort)Settings![10], (int)Settings![11]),
			new Tuple<ushort, ushort, int>((ushort)Settings![12], (ushort)Settings![13], (int)Settings![14]),
			new Tuple<ushort, ushort, int>((ushort)Settings![15], (ushort)Settings![16], (int)Settings![17])
		};
		while (num != 0)
		{
			ushort num2 = (ushort)LegacyNativePoolRandom.GetRandom.Next(0, Map!.bounds.Width);
			ushort num3 = (ushort)LegacyNativePoolRandom.GetRandom.Next(0, Map!.bounds.Height);
			bool flag = false;
			Tuple<ushort, ushort, int>[] array2 = array;
			foreach (Tuple<ushort, ushort, int> tuple in array2)
			{
				flag |= LegacyNativeRoleCore.GetDistance(num2, num3, tuple.Item1, tuple.Item2) < tuple.Item3;
			}
			if (!flag || !Map!.IsFlagPresent(num2, num3, LegacyNativeMapFlagType.Valid))
			{
				continue;
			}
			bool flag2 = true;
			for (int j = 0; j < LegacyNativeGameMap.XDir.Length; j++)
			{
				if (!Map!.IsFlagPresent(num2 + LegacyNativeGameMap.XDir[j], num3 + LegacyNativeGameMap.YDir[j], LegacyNativeMapFlagType.Valid))
				{
					flag2 = false;
				}
			}
			if (flag2)
			{
				LegacyNativeStaticRoleRuntime staticRole = new LegacyNativeStaticRoleRuntime(num2, num3, "");
				staticRole.Map = MAPID;
				staticRole.Pick();
				Map!.AddStaticRole(staticRole);
				num--;
			}
		}
	}
}
