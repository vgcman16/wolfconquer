using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Whole native Associate method family, reusing incoming genuine physical owners.
internal partial class LegacyNativeAssociateRuntime
{
	public static void Save()
	{
		try
		{
			using LegacyNativeDbWrite write = new LegacyNativeDbWrite("Associate.txt");
			MyAsociats[] array = Associates.Values.ToArray();
			foreach (MyAsociats myAsociats in array)
			{
				foreach (string item in myAsociats.ToStringMember())
				{
					write.Add(item);
				}
			}
			write.Execute(LegacyNativeDbWriteMode.Open);
		}
		catch (Exception ex)
		{
			LegacyNativeConsole.WriteLine(ex.ToString());
		}
	}

	public static void Load()
	{
		try
		{
			using (LegacyNativeDbRead read = new LegacyNativeDbRead("Associate.txt"))
			{
				if (read.Reader())
				{
					int count = read.Count;
					for (uint num = 0u; num < count; num++)
					{
						string[] array = read.ReadString("").Split('/');
						uint num2 = uint.Parse(array[0]);
						byte key = byte.Parse(array[1]);
						uint mentor_ExpBalls = uint.Parse(array[2]);
						uint mentor_Blessing = uint.Parse(array[3]);
						uint mentor_Stones = uint.Parse(array[4]);
						Member member = new Member();
						member.UID = uint.Parse(array[5]);
						member.Timer = ulong.Parse(array[6]);
						member.ExpBalls = uint.Parse(array[7]);
						member.Stone = uint.Parse(array[8]);
						member.Blessing = uint.Parse(array[9]);
						member.Name = array[10];
						member.KillsCount = ushort.Parse(array[11]);
						member.KillMap = uint.Parse(array[14]);
						member.Mesh = uint.Parse(array[15]);
						member.Level = uint.Parse(array[16]);
						member.NobilityRank = uint.Parse(array[17]);
						member.Signature = array[18];
						member.Class = uint.Parse(array[19]);
						if (Associates.ContainsKey(num2))
						{
							if (Associates[num2].Associat.ContainsKey(key))
							{
								Associates[num2].Associat[key].TryAdd(member.UID, member);
								continue;
							}
							Associates[num2].Associat.TryAdd(key, new ConcurrentDictionary<uint, Member>());
							Associates[num2].Associat[key].TryAdd(member.UID, member);
							continue;
						}
						MyAsociats myAsociats = new MyAsociats(num2);
						myAsociats.MyUID = num2;
						myAsociats.Mentor_ExpBalls = mentor_ExpBalls;
						myAsociats.Mentor_Blessing = mentor_Blessing;
						myAsociats.Mentor_Stones = mentor_Stones;
						myAsociats.Associat.TryAdd(key, new ConcurrentDictionary<uint, Member>());
						myAsociats.Associat[key].TryAdd(member.UID, member);
						Associates.TryAdd(num2, myAsociats);
					}
				}
			}
			GC.Collect();
		}
		catch (Exception ex)
		{
			LegacyNativeConsole.WriteLine(ex.ToString());
		}
	}
}
