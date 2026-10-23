using WolfConquer.Core.World;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.Data;

internal static partial class LegacyNativeJianHuTable
{
	public static void LoadStatus()
	{
		try
		{
			using (LegacyNativeDbRead read = new LegacyNativeDbRead("JianghuAttributes.txt"))
			{
				if (read.Reader())
				{
					uint count = (uint)read.Count;
					for (uint num = 0u; num < count; num++)
					{
						LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(read.ReadString(""), ' ');
						Atribut atribut = new Atribut();
						int num2 = readLine.Read(0);
						atribut.Type = readLine.Read((byte)0);
						atribut.Level = readLine.Read((byte)0);
						atribut.Power = readLine.Read((ushort)0);
						if (atribut.Type == 10 || atribut.Type == 11 || atribut.Type == 12)
						{
							atribut.Power *= 10;
						}
						ushort key = ValueToRoll(atribut.Type, atribut.Level);
						Atributes.Add(key, atribut);
					}
				}
			}
			using LegacyNativeDbRead read2 = new LegacyNativeDbRead("JingHuCultivateStatus.txt");
			if (!read2.Reader())
			{
				return;
			}
			uint count2 = (uint)read2.Count;
			for (uint num3 = 0u; num3 < count2; num3++)
			{
				LegacyNativeDbReadLine readLine2 = new LegacyNativeDbReadLine(read2.ReadString(""), ' ');
				byte key2 = readLine2.Read((byte)0);
				byte b = readLine2.Read((byte)0);
				List<byte> list = new List<byte>();
				for (byte b2 = 0; b2 < b; b2++)
				{
					list.Add(readLine2.Read((byte)0));
				}
				CultivateStatus.Add(key2, list);
			}
		}
		catch (Exception ex)
		{
			LegacyNativeConsole.WriteLine(ex.ToString());
		}
	}
}
