#nullable disable
using System.Collections.Generic;
using System.Linq;

namespace WolfConquer.Core.Data;

// Native CQ_COMBAT_GEAR_RANK family; direct file persistence is in the Persistence partial.
internal partial class LegacyNativeCombatGearRankTable
{
    public enum Type : uint
    {
        Overall_Warrior = 19u,
        Dragonhowl = 20u,
        Bloodlust = 21u,
        Redcurse = 22u,
        Overall_Archer = 27u,
        StoneCracker = 28u,
        ColdMoon = 29u,
        TharnCutter = 30u,
        Overall_Taoist = 33u,
        Taoist1 = 34u,
        Taoist2 = 35u,
        Taoist3 = 36u,
        Taoist4 = 37u,
        Taoist5 = 38u,
        WEEK_Archer = 26u,
        WEEK_Taoist = 39u,
        WEEK_Pirate = 40u,
        Overall_Pirate = 42u
    }

    public class Entry
    {
        public Type Type;
        public uint UID;
        public string Name;
        public uint Class;
        public byte Level;
        public uint Mesh;
        public uint TotalPoints;
        public uint HairStyle;
        public uint Head;
        public uint Garment;
        public uint LeftWeapon;
        public uint LefttWeaponAccessory;
        public uint RightWeapon;
        public uint RightWeaponAccessory;
        public uint MountArmor;
        public uint Armor;
        public uint Wing;
        public uint WingPlus;
        public uint Title;
        public uint Flag;
        public string GuildName = "";
        public uint WeaponWar;

        public Entry ShallowCopy()
        {
            return (Entry)MemberwiseClone();
        }
    }

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
                if (RankingList.Remove(UID))
                {
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

    public static Dictionary<Type, Rank> Ranks
    {
        get => LegacyNativeCombatGearTable.MailboxRanks;
        set => LegacyNativeCombatGearTable.MailboxRanks = value;
    }

    public static void Create()
    {
        if (LegacyNativeCombatGearTable.m_Load)
        {
            Ranks.Add(Type.Overall_Warrior, new Rank(Type.Overall_Warrior) { MaxItems = 10 });
            Ranks.Add(Type.Dragonhowl, new Rank(Type.Dragonhowl) { MaxItems = 10 });
            Ranks.Add(Type.Bloodlust, new Rank(Type.Bloodlust) { MaxItems = 10 });
            Ranks.Add(Type.Redcurse, new Rank(Type.Redcurse) { MaxItems = 10 });
            Ranks.Add(Type.WEEK_Archer, new Rank(Type.WEEK_Archer) { MaxItems = 10 });
            Ranks.Add(Type.Overall_Archer, new Rank(Type.Overall_Archer) { MaxItems = 10 });
            Ranks.Add(Type.StoneCracker, new Rank(Type.StoneCracker) { MaxItems = 10 });
            Ranks.Add(Type.ColdMoon, new Rank(Type.ColdMoon) { MaxItems = 10 });
            Ranks.Add(Type.TharnCutter, new Rank(Type.TharnCutter) { MaxItems = 10 });
            Ranks.Add(Type.Overall_Taoist, new Rank(Type.Overall_Taoist) { MaxItems = 10 });
            Ranks.Add(Type.Taoist1, new Rank(Type.Taoist1) { MaxItems = 10 });
            Ranks.Add(Type.Taoist2, new Rank(Type.Taoist2) { MaxItems = 10 });
            Ranks.Add(Type.Taoist3, new Rank(Type.Taoist3) { MaxItems = 10 });
            Ranks.Add(Type.Taoist4, new Rank(Type.Taoist4) { MaxItems = 10 });
            Ranks.Add(Type.Taoist5, new Rank(Type.Taoist5) { MaxItems = 10 });
            Ranks.Add(Type.WEEK_Taoist, new Rank(Type.WEEK_Taoist) { MaxItems = 10 });
            Ranks.Add(Type.WEEK_Pirate, new Rank(Type.WEEK_Pirate) { MaxItems = 10 });
            Ranks.Add(Type.Overall_Pirate, new Rank(Type.Overall_Pirate) { MaxItems = 10 });
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

    public static void RemoveWarrior(uint UID)
    {
        foreach (Rank value in Ranks.Values)
        {
            if (value._Type >= Type.Overall_Warrior && value._Type <= Type.Redcurse)
                value.Remove(UID);
        }
    }

    public static void RemoveArcher(uint UID)
    {
        foreach (Rank value in Ranks.Values)
        {
            if (value._Type >= Type.WEEK_Archer && value._Type <= Type.TharnCutter)
                value.Remove(UID);
        }
    }

    public static void RemoveTaoist(uint UID)
    {
        foreach (Rank value in Ranks.Values)
        {
            if (value._Type >= Type.Overall_Taoist && value._Type <= Type.WEEK_Taoist)
                value.Remove(UID);
        }
    }

    public static void RemovePirate(uint UID)
    {
        foreach (Rank value in Ranks.Values)
        {
            if (value._Type == Type.WEEK_Pirate || value._Type == Type.Overall_Pirate)
                value.Remove(UID);
        }
    }

    public static Entry BestOf(Type type)
    {
        return Ranks[type].RankingList.Values.FirstOrDefault();
    }
}
