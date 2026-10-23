using System.Collections.Generic;
using System.Linq;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Core.World;

// Complete native MrRayzo.Role.Instance/HeroRewards.cs; no host birth, startup, save caller or SQL route installed.
internal class LegacyNativeHeroRewardsRuntime
{
	public class Item
	{
		public uint UID;

		public uint UnKnow;

		public byte Claim;
	}

	private LegacyNativeGameClientRuntime Owner;

	public LegacyNativeDbSafeDictionary<uint, Item> ArrayGoals = new LegacyNativeDbSafeDictionary<uint, Item>();

	public LegacyNativeHeroRewardsRuntime(LegacyNativeGameClientRuntime _owner)
	{
		Owner = _owner;
	}

	public void AddGoal(uint ID)
	{
		if (!ArrayGoals.ContainsKey(ID))
		{
			ArrayGoals.Add(ID, new Item
			{
				UID = ID
			});
		}
	}

	public bool ContainGoal(uint ID)
	{
		return ArrayGoals.ContainsKey(ID);
	}

	public bool FinishStage(uint ID)
	{
		int num = ArrayGoals.Where((KeyValuePair<uint, Item> p) => p.Key / 100 == ID).Count();
		if (LegacyNativePoolSaveRecords.TableHeroRewards.ContainsKey(ID))
		{
			return num == LegacyNativePoolSaveRecords.TableHeroRewards[ID].CountGoals;
		}
		return false;
	}

	public bool ComplateFullStage(uint ID)
	{
		int num = ArrayGoals.Where((KeyValuePair<uint, Item> p) => (p.Key / 100 == ID && p.Value.Claim > 0) || (p.Key == ID && p.Value.Claim > 0)).Count();
		if (LegacyNativePoolSaveRecords.TableHeroRewards.ContainsKey(ID))
		{
			return num == LegacyNativePoolSaveRecords.TableHeroRewards[ID].CountGoals + 1;
		}
		return false;
	}

	public override string ToString()
	{
		LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('/');
		writeLine.Add(ArrayGoals.Count);
		foreach (Item value in ArrayGoals.Values)
		{
			writeLine.Add(value.UID).Add(value.Claim);
		}
		return writeLine.Close();
	}

	public void Load(string line)
	{
		LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(line, '/');
		int num = readLine.Read(0);
		for (int i = 0; i < num; i++)
		{
			Item item = new Item();
			item.UID = readLine.Read(0u);
			item.Claim = readLine.Read((byte)0);
			if (!ArrayGoals.ContainsKey(item.UID))
			{
				ArrayGoals.Add(item.UID, item);
			}
		}
	}
}
