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
				lock (SynRoot)
				{
					return Ranks[Type.World].BestOfClass;
				}
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
		if (TryGetIndex(Class, out var category)) return category;
		throw new ArgumentOutOfRangeException(nameof(Class), Class,
			"The character class has no supported prestige class category.");
	}

	public static void Remove(uint UID)
	{
		lock (SynRoot)
		{
			if (UID == 0)
				throw new InvalidOperationException("Prestige removal requires a positive character UID.");
			var sources = CapturePrestigeRefreshRegistry();
			var prepared = new List<PreparedPrestigeRank>();
			foreach (var source in sources)
			{
				var change = PreparePrestigeRankChange(source, UID, replacement: null);
				if (change is not null) prepared.Add(change);
			}
			foreach (var change in prepared) PublishPrestigeRank(change);
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
		ArgumentNullException.ThrowIfNull(user);
		var input = CapturePrestigeRebornInput(user);
		if (input is null) return;

		var gate = SynRoot;
		lock (gate)
		{
			RequireCurrentPrestigeRebornInput(input, gate);
			var ranks = Ranks ?? throw new InvalidOperationException("Prestige reborn cleanup requires a ranking graph.");
			var source = CapturePrestigeRebornRank(ranks, input.PreviousCategory);
			var prepared = PreparePrestigeRankChange(source, input.Uid, replacement: null);
			RequireCurrentPrestigeRebornInput(input, gate);
			RequireCurrentPrestigeRebornRank(ranks, source);
			if (prepared is not null) PublishPrestigeRank(prepared);
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
