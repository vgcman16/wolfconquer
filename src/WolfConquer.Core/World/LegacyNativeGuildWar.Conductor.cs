using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Entire native GuildConductor owner, including the source false-return location method.
internal partial class LegacyNativeGuildWar
{
	public class GuildConductor
	{
		public static List<uint> BlockMaps = new List<uint> { 1038u };

		public LegacyNativeNpcRuntime? Npc;

		public uint ToMap;

		public ushort ToX;

		public ushort ToY;

		public override string ToString()
		{
			LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine(',');
			writeLine.Add(Npc!.UID).Add(Npc!.X).Add(Npc!.Y)
				.Add(Npc!.Map)
				.Add(Npc!.Mesh)
				.Add((ushort)Npc!.NpcType)
				.Add(ToX)
				.Add(ToY)
				.Add(ToMap);
			return writeLine.Close();
		}

		internal void Load(string Line, LegacyNativeNpcID UID)
		{
			Npc = LegacyNativeNpcRuntime.Create();
			if (Line == "")
			{
				Npc!.UID = (uint)UID;
				return;
			}
			LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(Line, ',');
			Npc!.UID = readLine.Read(0u);
			Npc!.X = readLine.Read((ushort)0);
			Npc!.Y = readLine.Read((ushort)0);
			Npc!.Map = readLine.Read((ushort)0);
			Npc!.Mesh = readLine.Read((ushort)0);
			Npc!.NpcType = (LegacyNativeNpcType)readLine.Read((ushort)0);
			ToX = readLine.Read((ushort)0);
			ToY = readLine.Read((ushort)0);
			ToMap = readLine.Read((ushort)0);
		}

		internal static bool ChangeNpcLocation(LegacyNativeGameMap map, ref ushort X, ref ushort Y, ref uint Map)
		{
			return false;
		}

		internal void GetCoords(out ushort x, out ushort y, out uint map)
		{
			if (ToMap != 0 && ToX != 0 && ToY != 0)
			{
				x = ToX;
				y = ToY;
				map = ToMap;
			}
			else
			{
				x = 300;
				y = 278;
				map = 1002u;
			}
		}
	}
}
