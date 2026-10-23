using WolfConquer.Core.World;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.Data;

internal static partial class LegacyNativeJianHuTable
{
	public class Atribut
	{
		public byte Level;

		public byte Type;

		public ushort Power;
	}

	private static ushort[] MinutesOnTalent = new ushort[5] { 1001, 501, 385, 313, 193 };

	private static ushort[] FreeCourseInCastle = new ushort[5] { 294, 625, 833, 1000, 1562 };

	private static byte[] FreeCourse = new byte[5] { 10, 20, 26, 31, 52 };

	private static double[] MinutesInCastle = new double[5] { 31.25, 31.25, 32.0, 32.2, 30.0 };

	private static ushort[] GetPoints = new ushort[6] { 100, 120, 150, 200, 300, 500 };

	private static ushort[] PrestigePoints = new ushort[6] { 30, 36, 45, 60, 90, 150 };

	private static ushort[] GetAlignmentExtraPoints = new ushort[9] { 0, 10, 13, 15, 18, 21, 25, 30, 50 };

	public static Dictionary<byte, List<byte>> CultivateStatus = new Dictionary<byte, List<byte>>();

	public static Dictionary<ushort, Atribut> Atributes = new Dictionary<ushort, Atribut>();

	public static ushort AlignmentExtraPoints(byte amount)
	{
		return GetAlignmentExtraPoints[Math.Min(8, (int)amount)];
	}

	public static ushort GetMinutesOnTalent(byte Talent)
	{
		if (Talent == 0)
		{
			Talent = 1;
		}
		return MinutesOnTalent[Math.Min(4, Talent - 1)];
	}

	public static ushort GetFreeCourseInCastle(byte Talent)
	{
		if (Talent == 0)
		{
			Talent = 1;
		}
		return FreeCourseInCastle[Math.Min(4, Talent - 1)];
	}

	public static ushort GetFreeCourse(byte Talent)
	{
		if (Talent == 0)
		{
			Talent = 1;
		}
		return FreeCourse[Math.Min(4, Talent - 1)];
	}

	public static double GetMinutesInCastle(byte Talent)
	{
		if (Talent == 0)
		{
			Talent = 1;
		}
		return MinutesInCastle[Math.Min(4, Talent - 1)];
	}

	public static ushort GetStatusPoints(byte Level)
	{
		if (Level == 0)
		{
			Level = 1;
		}
		return GetPoints[Math.Min(5, Level - 1)];
	}

	public static ushort GetPrestigePoints(byte Level)
	{
		if (Level == 0)
		{
			Level = 1;
		}
		return PrestigePoints[Math.Min(5, Level - 1)];
	}

	public static ushort ValueToRoll(byte typ, byte level)
	{
		return (ushort)(typ + level * 256);
	}

	public static ushort GetPower(ushort UID)
	{
		return Atributes[UID].Power;
	}
}
