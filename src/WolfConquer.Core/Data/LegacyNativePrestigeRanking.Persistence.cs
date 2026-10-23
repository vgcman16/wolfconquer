using WolfConquer.Core.World;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativePrestigeRanking
{
	public static void Load()
	{
		Create();
		lock (SynRoot)
		{
			using LegacyNativeDbRead read = new LegacyNativeDbRead("PrestigeRanking.txt");
			if (!read.Reader())
			{
				return;
			}
			int count = read.Count;
			for (int i = 0; i < count; i++)
			{
				LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(read.ReadString("/"), '/');
				Entry entry = new Entry();
				entry.UID = readLine.Read(0u);
				entry.Name = readLine.Read("")!;
				entry.type = (Type)readLine.Read((byte)0);
				entry.Class = readLine.Read(0u);
				entry.Level = readLine.Read((byte)0);
				entry.Mesh = readLine.Read(0u);
				entry.TotalPoints = readLine.Read(0u);
				entry.HairStyle = readLine.Read(0u);
				entry.Head = readLine.Read(0u);
				entry.Garment = readLine.Read(0u);
				entry.LeftWeapon = readLine.Read(0u);
				entry.LefttWeaponAccessory = readLine.Read(0u);
				entry.RightWeapon = readLine.Read(0u);
				entry.RightWeaponAccessory = readLine.Read(0u);
				entry.MountArmor = readLine.Read(0u);
				entry.Armor = readLine.Read(0u);
				entry.Wing = readLine.Read(0u);
				entry.WingPlus = readLine.Read(0u);
				entry.Title = readLine.Read(0u);
				entry.Flag = readLine.Read(0u);
				entry.GuildName = readLine.Read("")!;
				int num = readLine.Read(0);
				entry.Points = new uint[num];
				for (int j = 0; j < entry.Points.Length; j++)
				{
					entry.Points[j] = readLine.Read(0u);
				}
				Ranks[entry.type].AddItem(entry);
				Ranks[Type.World].AddItem(entry);
			}
		}
	}

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
