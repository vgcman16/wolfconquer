using System.Collections.Generic;
using System.Linq;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// Complete native MrRayzo.Database/ActivityTask.cs; no host birth, startup, save caller or SQL route installed.
internal class LegacyNativeActivityTaskTable : LegacyNativeDbSafeDictionary<uint, LegacyNativeActivityTaskTable.Task>
{
	public class Task
	{
		public uint ID;

		public byte NeedProgress = 0;

		public byte ActiveValue = 0;

		public uint OpenLev;

		public bool CheckOpen(ushort level, byte reborn)
		{
			uint num = OpenLev % 1000;
			uint num2 = (OpenLev - num) / 1000;
			return level >= num && reborn >= num2;
		}
	}

	public void Load()
	{
		LegacyNativeItemCounterShellIni iniFile = new LegacyNativeItemCounterShellIni("ActivityTask.ini");
		int num = iniFile.ReadByte("ActivityTask", "Num", 0);
		for (uint num2 = 1u; num2 <= num; num2++)
		{
			Add(num2, new Task
			{
				ID = num2,
				NeedProgress = iniFile.ReadByte(num2.ToString(), "Progress", 0),
				ActiveValue = iniFile.ReadByte(num2.ToString(), "ActiveValue", 0),
				OpenLev = iniFile.ReadUInt32(num2.ToString(), "OpenLev")
			});
		}
	}
}
