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
            lock (SynRoot)
            {
                if (RankingList.ContainsKey(UID))
                {
                    RankingList.Remove(UID);
                    RankingList = RankingList.OrderByDescending((KeyValuePair<uint, Entry> p) => p.Value.TotalPoints)
                        .Take(MaxItems).ToDictionary((KeyValuePair<uint, Entry> pair) => pair.Key,
                            (KeyValuePair<uint, Entry> pair) => pair.Value);
                }
            }
        }

        public void UpdateItem(Entry item)
        {
            lock (SynRoot)
            {
                if (!RankingList.ContainsKey(item.UID))
                {
                    RankingList.Add(item.UID, item);
                }
                else if (RankingList.ContainsKey(item.UID))
                {
                    RankingList[item.UID] = item;
                }
                RankingList = RankingList.OrderByDescending((KeyValuePair<uint, Entry> p) => p.Value.TotalPoints)
                    .Take(MaxItems).ToDictionary((KeyValuePair<uint, Entry> pair) => pair.Key,
                        (KeyValuePair<uint, Entry> pair) => pair.Value);
            }
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
        if (Ranks[typ].RankingList.Values.Count((Entry i) => i.UID == UID) > 0)
        {
            return Ranks[typ].RankingList.Values.FirstOrDefault((Entry i) => i.UID == UID);
        }
        return null;
    }

    public static uint GetMyRank(Type typ, uint UID)
    {
        if (Ranks[typ].RankingList.Values.Count((Entry i) => i.UID == UID) > 0)
        {
            Entry item = Ranks[typ].RankingList.Values.FirstOrDefault((Entry i) => i.UID == UID);
            return (uint)(Ranks[typ].RankingList.Values.ToList().IndexOf(item) + 1);
        }
        return 0u;
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
