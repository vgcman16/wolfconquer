using System.Collections;
using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeJiangHu : IEnumerable<LegacyNativeJiangHu.Stage>, IEnumerable
{
	public IEnumerator<Stage> GetEnumerator()
	{
		Stage[] arrayStages = ArrayStages;
		for (int i = 0; i < arrayStages.Length; i++)
		{
			yield return arrayStages[i];
		}
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}

	public override string ToString()
	{
		if (StartCountDwon.Ticks > CountDownEnd.Ticks)
		{
			CreateTime();
		}
		uint data = (uint)((CountDownEnd.Ticks - StartCountDwon.Ticks) / 10000000);
		if (Name.Contains('/'))
		{
			Name = Name.Replace("/", "");
		}
		if (CustomizedName.Contains('/'))
		{
			CustomizedName = CustomizedName.Replace("/", "");
		}
		LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('/');
		writeLine.Add(UID).Add(Name).Add(CustomizedName)
			.Add(Level)
			.Add(Talent)
			.Add(FreeTimeToday)
			.Add((byte)(OnJiangMode ? 1u : 0u))
			.Add(FreeCourse)
			.Add(data)
			.Add(RoundBuyPoints);
		Stage[] arrayStages = ArrayStages;
		foreach (Stage stage in arrayStages)
		{
			writeLine.Add((byte)(stage.Activate ? 1u : 0u));
			Stage.Star[] arrayStars = stage.ArrayStars;
			foreach (Stage.Star star in arrayStars)
			{
				writeLine.Add((byte)(star.Activate ? 1u : 0u)).Add(star.UID);
			}
		}
		return writeLine.Close();
	}
}
