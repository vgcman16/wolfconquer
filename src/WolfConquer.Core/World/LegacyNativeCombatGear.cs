using System;
using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

// Native CombatGear state, class storage and constructor940..946.
// Mutations/persistence live in other partials; native birth/Loading/host adoption remain absent.
internal partial class LegacyNativeCombatGear
{
    public uint MasteryPoints;

    public uint TaoistReiki;

    public uint PirateExp;

    public uint Lottery;

    public uint StarFlow;

    public uint Archshadow;

    public uint ArrowBlades;

    private uint Class;

    public static Random GetRandom = new Random();

    public static ushort[] GET_Passive = new ushort[9] { 21670, 21680, 21690, 21700, 22070, 22080, 22090, 22100, 22110 };

    public ConcurrentDictionary<TypeID, Weapon> Items;

    public ConcurrentDictionary<uint, BlackJade> BlackJadeBag;

    public ConcurrentDictionary<string, double[]> Info;

    public Powers AirPowers;

    public void SetClass(uint x)
    {
        Class = x;
    }

    public uint GetClass()
    {
        return Class;
    }

    public LegacyNativeCombatGear()
    {
        Items = new ConcurrentDictionary<TypeID, Weapon>();
        BlackJadeBag = new ConcurrentDictionary<uint, BlackJade>();
        Info = new ConcurrentDictionary<string, double[]>();
        AirPowers = new Powers();
    }
}
