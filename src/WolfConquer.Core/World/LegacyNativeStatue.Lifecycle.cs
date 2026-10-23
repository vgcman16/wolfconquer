using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Whole genuine statue map and persistence lifecycle. Tests must bind owned paths only.
internal partial class LegacyNativeStatue
{
	public static void RemoveStatue(LegacyNativePacket stream, LegacyNativeGameClientRuntime killer, uint UID, ILegacyNativeMapObj obj)
	{
		LegacyNativeGameMap gameMap = ((killer.Map == null) ? LegacyNativePoolMaps.ServerMaps![1002u] : killer.Map);
		gameMap.View!.LeaveMap(obj);
		LegacyNativeActionQuery pQuery = new LegacyNativeActionQuery
		{
			ObjId = UID,
			Type = 135
		};
		killer.Player.View.SendView(stream.ActionCreate(pQuery), me: true);
	}

public static void ElitePkStatue(LegacyNativeGameClientRuntime user)
	{
		if (StaticStatue == null && StaticSobNpc == null)
		{
			CreateStatue(user, 301, 141, 0, 0, Static: true);
			return;
		}
		using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
		LegacyNativePacket stream = recycledPacket.GetStream();
		RemoveStatue(stream, user, StaticSobNpc!.UID, StaticSobNpc);
		CreateStatue(user, 301, 141, 0, 0, Static: true);
	}

public static void Save()
	{
		using LegacyNativeDbWrite write = new LegacyNativeDbWrite("StaticStatue.txt");
		if (StaticStatue != null && StaticStatue.StatuePacket != null && StaticSobNpc != null)
		{
			int num = StaticStatue.StatuePacket.Length;
			LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('/');
			writeLine.Add(num);
			for (int i = 0; i < num; i++)
			{
				writeLine.Add(StaticStatue.StatuePacket[i]);
			}
			writeLine.Add(StaticSobNpc!.UID).Add(StaticSobNpc.X).Add(StaticSobNpc.Y)
				.Add(StaticSobNpc!.Map)
				.Add(StaticSobNpc.MaxHitPoints)
				.Add(StaticSobNpc.HitPoints);
			write.Add(writeLine.Close());
			write.Execute(LegacyNativeDbWriteMode.Open);
		}
	}

public static void Load()
	{
		using LegacyNativeDbRead read = new LegacyNativeDbRead("StaticStatue.txt");
		if (!read.Reader())
		{
			return;
		}
		int count = read.Count;
		for (uint num = 0u; num < count; num++)
		{
			LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(read.ReadString(""), '/');
			int num2 = readLine.Read(0);
			if (num2 != 0)
			{
				StaticStatue = new LegacyNativeStatue();
				StaticStatue.Static = true;
				StaticStatue.StatuePacket = new byte[num2];
				for (int i = 0; i < StaticStatue.StatuePacket.Length; i++)
				{
					StaticStatue.StatuePacket[i] = readLine.Read((byte)0);
				}
				StaticSobNpc = new LegacyNativeSobNpcRuntime(StaticStatue);
				StaticSobNpc.ObjType = LegacyNativeMapObjectType.SobNpc;
				StaticSobNpc!.UID = readLine.Read(0u);
				StaticSobNpc.X = readLine.Read((ushort)0);
				StaticSobNpc.Y = readLine.Read((ushort)0);
				StaticSobNpc!.Map = readLine.Read((ushort)0);
				StaticSobNpc.MaxHitPoints = readLine.Read(0);
				StaticSobNpc.HitPoints = readLine.Read(0);
				LegacyNativePoolMaps.ServerMaps![StaticSobNpc!.Map].View!.EnterMap((ILegacyNativeMapObj)StaticSobNpc);
			}
		}
	}
}
