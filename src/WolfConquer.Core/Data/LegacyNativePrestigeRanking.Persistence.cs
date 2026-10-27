using WolfConquer.Core.World;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativePrestigeRanking
{
	public static void Save()
	{
		lock (SynRoot)
		{
			using LegacyNativeDbWrite write = new LegacyNativeDbWrite("PrestigeRanking.txt");
			Rank[] values = Ranks.GetValues();
			foreach (Rank rank in values)
			{
				if (rank._Type == Type.World)
				{
					continue;
				}
				foreach (Entry value in rank.Items.Values)
				{
					LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('/');
					writeLine.Add(value.UID).Add(value.Name).Add(unchecked((byte)value.type))
						.Add(value.Class)
						.Add(value.Level)
						.Add(value.Mesh)
						.Add(value.TotalPoints)
						.Add(value.HairStyle)
						.Add(value.Head)
						.Add(value.Garment)
						.Add(value.LeftWeapon)
						.Add(value.LefttWeaponAccessory)
						.Add(value.RightWeapon)
						.Add(value.RightWeaponAccessory)
						.Add(value.MountArmor)
						.Add(value.Armor)
						.Add(value.Wing)
						.Add(value.WingPlus)
						.Add(value.Title)
						.Add(value.Flag)
						.Add(value.GuildName);
					if (value.Points == null)
					{
						writeLine.Add(0);
					}
					else
					{
						writeLine.Add(value.Points.Length);
						for (int j = 0; j < value.Points.Length; j++)
						{
							writeLine.Add(value.Points[j]);
						}
					}
					write.Add(writeLine.Close());
				}
			}
			write.Execute(LegacyNativeDbWriteMode.Open);
		}
	}
}
