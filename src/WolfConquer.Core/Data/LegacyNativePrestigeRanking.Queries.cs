using WolfConquer.Core.World;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativePrestigeRanking
{
	public static LegacyNativeDbSafeDictionary<Type, Rank> Ranks = new LegacyNativeDbSafeDictionary<Type, Rank>();

	public static object SynRoot = new object();

	public static Entry? BestOfTheWorld
	{
		get
		{
			try
			{
				return Ranks[Type.World].BestOfClass;
			}
			catch
			{
				LegacyNativeConsole.SaveException(new Exception("BestOfWorld Error Loading."));
			}
			return null;
		}
	}

	public static Type GetIndex(uint Class)
	{
		if (LegacyNativeAtributesStatus.IsTrojan(Class))
		{
			return Type.Trojan;
		}
		if (LegacyNativeAtributesStatus.IsWarrior(Class))
		{
			return Type.Warrior;
		}
		if (LegacyNativeAtributesStatus.IsArcher(Class))
		{
			return Type.Archer;
		}
		if (LegacyNativeAtributesStatus.IsNinja(Class))
		{
			return Type.Ninja;
		}
		if (LegacyNativeAtributesStatus.IsMonk(Class))
		{
			return Type.Monk;
		}
		if (LegacyNativeAtributesStatus.IsPirate(Class))
		{
			return Type.Pirate;
		}
		if (LegacyNativeAtributesStatus.IsLee(Class))
		{
			return Type.DragonWarrior;
		}
		if (LegacyNativeAtributesStatus.IsWater(Class))
		{
			return Type.WaterTao;
		}
		if (LegacyNativeAtributesStatus.IsFire(Class))
		{
			return Type.FireTao;
		}
		if (LegacyNativeAtributesStatus.IsWindWalker(Class))
		{
			return Type.WindWalker;
		}
		if (LegacyNativeAtributesStatus.IsThunderStriker(Class))
		{
			return Type.Thunderstriker;
		}
		return Type.WaterTao;
	}

	public static void Remove(uint UID)
	{
		foreach (Rank value in Ranks.Values)
		{
			value.Remove(UID);
		}
	}

	public static void Create()
	{
		for (int i = 0; i < 12; i++)
		{
			Ranks.Add((Type)i, new Rank((Type)i));
			if (i == 0)
			{
				Ranks[(Type)i].MaxItems = 100;
			}
			else
			{
				Ranks[(Type)i].MaxItems = 30;
			}
		}
	}

	public static void CheckReborn(LegacyNativeGameClientRuntime user)
	{
		lock (SynRoot)
		{
			if (user.Player.Reborn == 2)
			{
				if (Ranks[GetIndex(user.Player.SecoundeClass)].Items.ContainsKey(user.Player.UID))
				{
					Ranks[GetIndex(user.Player.SecoundeClass)].Items.Remove(user.Player.UID);
					Ranks[GetIndex(user.Player.SecoundeClass)].Items = Ranks[GetIndex(user.Player.SecoundeClass)].Items.OrderByDescending((KeyValuePair<uint, Entry> p) => p.Value.TotalPoints).Take(Ranks[GetIndex(user.Player.SecoundeClass)].MaxItems).ToDictionary((KeyValuePair<uint, Entry> pair) => pair.Key, (KeyValuePair<uint, Entry> pair) => pair.Value);
				}
			}
			else if (user.Player.Reborn == 1 && Ranks[GetIndex(user.Player.FirstClass)].Items.ContainsKey(user.Player.UID))
			{
				Ranks[GetIndex(user.Player.FirstClass)].Items.Remove(user.Player.UID);
				Ranks[GetIndex(user.Player.FirstClass)].Items = Ranks[GetIndex(user.Player.FirstClass)].Items.OrderByDescending((KeyValuePair<uint, Entry> p) => p.Value.TotalPoints).Take(Ranks[GetIndex(user.Player.FirstClass)].MaxItems).ToDictionary((KeyValuePair<uint, Entry> pair) => pair.Key, (KeyValuePair<uint, Entry> pair) => pair.Value);
			}
		}
	}

	public static uint GetMyRank(Type typ, uint UID)
	{
		lock (SynRoot)
		{
			if (!Ranks.TryGetValue(typ, out var rank) || rank is null || rank.Items is null) return 0u;
			uint ordinal = 0;
			foreach (var item in rank.Items.Values)
			{
				if (item is null) continue;
				ordinal++;
				if (item.UID == UID) return ordinal;
			}
			return 0u;
		}
	}

	public static Entry? GetInfo(uint UID)
	{
		lock (SynRoot)
		{
			foreach (Rank value in Ranks.Values)
			{
				foreach (Entry value2 in value.Items.Values)
				{
					if (value2.UID == UID)
					{
						return value2;
					}
				}
			}
		}
		return null;
	}
}
