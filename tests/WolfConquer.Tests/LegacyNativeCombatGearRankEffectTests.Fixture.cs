using WolfConquer.Core.Data;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using Ranks = WolfConquer.Core.Data.LegacyNativeCombatGearRankTable;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeCombatGearRankEffectTests
{
    private const uint UserId = 17;
    private static void AssertZero(Gear.VRANK? value)
    {
        Assert.NotNull(value);
        Assert.Equal((0d, 0d, 0d), (value.LukyStrike, value.Parry, value.CoreStrike));
        Assert.Equal((0, 0), ((int)value.Magic, (int)value.MagicLevel));
        Assert.Equal((0u, 0u, 0u, 0u), (value.PAttack, value.PDefense, value.MDefense, value.PrestigeScore));
    }

    private static Ranks.Type[] LookupOrder(uint classId) => classId switch
    {
        2001 => [Ranks.Type.Overall_Warrior, Ranks.Type.Dragonhowl, Ranks.Type.Bloodlust, Ranks.Type.Redcurse],
        4001 => [Ranks.Type.Overall_Archer, Ranks.Type.WEEK_Archer, Ranks.Type.StoneCracker,
            Ranks.Type.ColdMoon, Ranks.Type.TharnCutter],
        13001 or 14001 => [Ranks.Type.Overall_Taoist, Ranks.Type.WEEK_Taoist, Ranks.Type.Taoist1,
            Ranks.Type.Taoist2, Ranks.Type.Taoist3, Ranks.Type.Taoist4, Ranks.Type.Taoist5],
        7001 => [Ranks.Type.Overall_Pirate, Ranks.Type.WEEK_Pirate],
        _ => throw new ArgumentOutOfRangeException(nameof(classId))
    };

    private sealed class RankEffectScope : IDisposable
    {
        private readonly bool priorReady = LegacyNativeCombatGearTable.m_Load;
        private readonly Dictionary<Ranks.Type, Ranks.Rank> priorRanks = Ranks.Ranks;

        internal RankEffectScope()
        {
            LegacyNativeCombatGearTable.m_Load = true;
            Ranks.Ranks = new();
            Ranks.Create(); // Genuine standalone native table birth, not host publication.
        }

        internal void PlaceAll(int position)
        {
            foreach (var type in Ranks.Ranks.Keys) Place(type, position);
        }

        internal void Place(Ranks.Type type, int position)
        {
            var list = Ranks.Ranks[type].RankingList;
            list.Clear();
            // Genuine mutable row prestate, preserving current list order.
            // No sorting service, accepting result callback or host publication.
            for (var i = 1; i <= position; i++)
                list[(uint)(8000 + i)] = new Ranks.Entry
                {
                    UID = i == position ? UserId : (uint)(100 + i),
                    TotalPoints = i == position ? uint.MaxValue : 1
                };
        }

        public void Dispose()
        {
            Ranks.Ranks = priorRanks;
            LegacyNativeCombatGearTable.m_Load = priorReady;
        }
    }
}
