using WolfConquer.Core.World;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.Data;

internal static partial class LegacyNativeJianHuTable
{
	public static void LoadJiangHu()
	{
		using LegacyNativeDbRead read = new LegacyNativeDbRead("JiangHu.txt");
		if (!read.Reader())
		{
			return;
		}
		int count = read.Count;
		for (uint num = 0u; num < count; num++)
		{
			LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(read.ReadString(""), '/');
			LegacyNativeJiangHu jiangHu = new LegacyNativeJiangHu(readLine.Read(0u));
			jiangHu.Name = readLine.Read("")!;
			jiangHu.CustomizedName = readLine.Read("")!;
			jiangHu.Level = readLine.Read((byte)0);
			jiangHu.Talent = readLine.Read((byte)0);
			jiangHu.FreeTimeToday = readLine.Read(0u);
			jiangHu.OnJiangMode = readLine.Read((byte)0) == 1;
			jiangHu.FreeCourse = readLine.Read(0u);
			jiangHu.StartCountDwon = DateTime.Now;
			jiangHu.CountDownEnd = DateTime.Now.AddSeconds(readLine.Read(0u));
			jiangHu.RoundBuyPoints = readLine.Read(0u);
			uint num2 = 0u;
			LegacyNativeJiangHu.Stage[] arrayStages = jiangHu.ArrayStages;
			foreach (LegacyNativeJiangHu.Stage stage in arrayStages)
			{
				num2++;
				stage.Activate = readLine.Read((byte)0) == 1;
				LegacyNativeJiangHu.Stage.Star[] arrayStars = stage.ArrayStars;
				foreach (LegacyNativeJiangHu.Stage.Star star in arrayStars)
				{
					star.Activate = readLine.Read((byte)0) == 1;
					star.UID = readLine.Read((ushort)0);
					if (star.Activate)
					{
						star.Typ = jiangHu.GetValueType(star.UID);
						star.Level = jiangHu.GetValueLevel(star.UID);
						if (num2 == 9 && star.Typ == LegacyNativeJiangHu.Stage.AtributesType.PAttack)
						{
							star.Typ = LegacyNativeJiangHu.Stage.AtributesType.MaxLife;
							star.UID = jiangHu.ValueToRoll(star.Typ, star.Level);
						}
					}
				}
			}
			jiangHu.CreateStatusAtributes(null);
		}
	}

	public static void SaveJiangHu()
	{
		using LegacyNativeDbWrite write = new LegacyNativeDbWrite("JiangHu.txt");
		LegacyNativeJiangHu[] array = LegacyNativeJiangHu.Poll.Values.Where((LegacyNativeJiangHu p) => p.UID < 3999900001u).ToArray();
		LegacyNativeJiangHu[] array2 = array;
		foreach (LegacyNativeJiangHu jiangHu in array2)
		{
			write.Add(jiangHu.ToString());
		}
		write.Execute(LegacyNativeDbWriteMode.Open);
	}
}
