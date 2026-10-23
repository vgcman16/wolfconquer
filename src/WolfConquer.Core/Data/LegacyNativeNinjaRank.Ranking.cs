#nullable disable
using System.Collections.Generic;
using System.Linq;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeNinjaRank
{
    public class Rank
    {
        public Type _Type;
        public int MaxItems;
        public Dictionary<uint, Entry> RankingList;
        public object SynRoot;

        public Rank(Type typ)
        {
            _Type = typ;
            SynRoot = new object();
            RankingList = new Dictionary<uint, Entry>();
        }

        public void Remove(uint UID)
        {
            LegacyNativeCappedRankingMutations.Remove(SynRoot, ref RankingList, UID,
                ref MaxItems, static entry => entry.TotalPoints);
        }

        public void UpdateItem(Entry item)
        {
            LegacyNativeCappedRankingMutations.Update(SynRoot, ref RankingList, item,
                ref MaxItems, static entry => entry.UID, static entry => entry.TotalPoints);
        }
    }

    public static LegacyNativeDbSafeDictionary<Type, Rank> Ranks = new LegacyNativeDbSafeDictionary<Type, Rank>();

    public static void Create()
    {
        for (int i = 0; i < 6; i++)
        {
            Ranks.Add((Type)i, new Rank((Type)i)
            {
                MaxItems = 10
            });
        }
    }

    public static Entry GetInfo(Type typ, uint UID)
    {
        if (!Ranks.TryGetValue(typ, out var rank) || rank is null) return null;
        lock (rank.SynRoot)
        {
            var entries = rank.RankingList;
            return entries?.Values.FirstOrDefault(item => item is not null && item.UID == UID);
        }
    }

    public static uint GetMyRank(Type typ, uint UID)
    {
        if (!Ranks.TryGetValue(typ, out var rank) || rank is null) return 0u;
        lock (rank.SynRoot)
        {
            var entries = rank.RankingList;
            if (entries is null) return 0u;
            uint ordinal = 0;
            foreach (var item in entries.Values)
            {
                if (item is null) continue;
                ordinal++;
                if (item.UID == UID) return ordinal;
            }
            return 0u;
        }
    }

    public static void Remove(uint UID)
    {
        foreach (Rank value in Ranks.Values)
        {
            value.Remove(UID);
        }
    }

    public static Entry BestOf(Type type)
    {
        return Ranks[type].RankingList.Values.FirstOrDefault();
    }
}
