using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNpcServer
{
	internal static void NpcTime()
	{
		uint num = 0u;
		string[] array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "NpcTime.txt");
		string[] array2 = array;
		foreach (string line in array2)
		{
			LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(line, ',');
			LegacyNativeSobNpcRuntime sobNpc = new LegacyNativeSobNpcRuntime();
			sobNpc.ObjType = LegacyNativeMapObjectType.SobNpc;
			sobNpc.UID = readLine.Read(0u);
			sobNpc.Name = readLine.Read("");
			sobNpc.Type = (LegacyNativeNpcType)readLine.Read((ushort)0);
			sobNpc.Mesh = (LegacyNativeSobNpcRuntime.StaticMesh)readLine.Read((ushort)0);
			sobNpc.Map = readLine.Read((ushort)0);
			sobNpc.X = readLine.Read((ushort)0);
			sobNpc.Y = readLine.Read((ushort)0);
			sobNpc.HitPoints = readLine.Read(0);
			sobNpc.MaxHitPoints = readLine.Read(0);
			sobNpc.Sort = readLine.Read((ushort)0);
			if (sobNpc.Map == 1039)
			{
				int maxHitPoints = (sobNpc.HitPoints = 10000);
				sobNpc.MaxHitPoints = maxHitPoints;
			}
			if (readLine.Read((byte)0) == 0)
			{
				sobNpc.Name = null;
			}
			if (LegacyNativePoolMaps.ServerMaps!.ContainsKey(sobNpc.Map))
			{
				LegacyNativePoolMaps.ServerMaps![sobNpc.Map].View!.EnterMap((ILegacyNativeMapObj)sobNpc);
				num++;
				if (LegacyNativeGameMap.IsGate(sobNpc.UID))
				{
					LegacyNativePoolMaps.ServerMaps![sobNpc.Map].SetGateFlagNpc(sobNpc.X, sobNpc.Y);
				}
				else
				{
					LegacyNativePoolMaps.ServerMaps![sobNpc.Map].SetFlagNpc(sobNpc.X, sobNpc.Y);
				}
			}
		}
		LegacyNativeConsole.WriteLine("Loaded " + num + " NpcTime");
	}

internal static void LoadSobNpcs(uint id = 0u)
	{
		string[] array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "SobNpcs.txt");
		string[] array2 = array;
		foreach (string line in array2)
		{
			LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(line, ',');
			LegacyNativeSobNpcRuntime sobNpc = new LegacyNativeSobNpcRuntime();
			sobNpc.ObjType = LegacyNativeMapObjectType.SobNpc;
			sobNpc.UID = readLine.Read(0u);
			sobNpc.Name = readLine.Read("");
			sobNpc.Type = (LegacyNativeNpcType)readLine.Read((ushort)0);
			sobNpc.Mesh = (LegacyNativeSobNpcRuntime.StaticMesh)readLine.Read((ushort)0);
			sobNpc.Map = readLine.Read((ushort)0);
			if (sobNpc.Map != id && id != 0)
			{
				continue;
			}
			sobNpc.X = readLine.Read((ushort)0);
			sobNpc.Y = readLine.Read((ushort)0);
			sobNpc.HitPoints = readLine.Read(0);
			sobNpc.MaxHitPoints = readLine.Read(0);
			sobNpc.Sort = readLine.Read((ushort)0);
			sobNpc.Name2 = readLine.Read("");
			sobNpc.Name2 = readLine.Read("");
			if (sobNpc.Map == 1039)
			{
				int maxHitPoints = (sobNpc.HitPoints = 10000);
				sobNpc.MaxHitPoints = maxHitPoints;
			}
			if (LegacyNativePoolMaps.ServerMaps!.Base.ContainsKey(sobNpc.Map))
			{
				LegacyNativePoolMaps.ServerMaps![sobNpc.Map].View!.EnterMap((ILegacyNativeMapObj)sobNpc);
				if (LegacyNativeGameMap.IsGate(sobNpc.UID))
				{
					LegacyNativePoolMaps.ServerMaps![sobNpc.Map].SetGateFlagNpc(sobNpc.X, sobNpc.Y);
				}
				else
				{
					LegacyNativePoolMaps.ServerMaps![sobNpc.Map].SetFlagNpc(sobNpc.X, sobNpc.Y);
				}
			}
		}
	}
}
