namespace WolfConquer.Core.World;

// Reference owners only, NOT an implementation of native GameClient/Player in full.
// MyRandom is a physical-Player field initializer, not a client/UID/Store or first-Rate cache.
public sealed partial class LegacyNativeGameClientRuntime
{
    // Explicit native field slot29. Current partial birth does not initialize it.
    internal LegacyNativeItemBagRuntime? GearMythsoulBag;

    public LegacyNativeGameClientRuntime()
    {
        Player = new LegacyNativePlayerRuntime(this);
    }

    // Deliberately direct native-reference assignment, not UID, Store.Set, CAS or map inference.
    // A future real GetHeroInfo producer must construct its new Player BEFORE parsing imported UID
    // and assign that same object here at native adoption. Do not call this on ordinary with-copies.
    public LegacyNativePlayerRuntime Player { get; set; }

    // Native GameClient141: nullable field; accepted-existing-character prefix owns conditional birth.
    internal LegacyNativeBeastsRuntime? Beasts;

    // Native GameClient declaration211; assigned ONLY by its explicit constructor-slot phase.
    internal LegacyNativeDemonExterminatorRuntime? DemonExterminator;

    // Native member declarations263/267/269; owning slots remain explicit and unwired.
    internal LegacyNativeWardrobeRuntime? MyWardrobe;
    internal LegacyNativeWarehouseRuntime? Warehouse;
    internal LegacyNativeItemBagRuntime? RelicFuse;

    // GENUINE same-item Equip constructor/storage. Not the historical raw observation.
    internal LegacyNativeEquipItemStorage? Equipment;

    // Explicit configured raw-item load owns this slice; birth/UID/Store.Set never allocate it.
    // Raw-load observation only. Public mutation writers and live Mercy admission remain held.
    internal LegacyNativeEquipmentRuntime? RawEquipmentObservation { get; set; }

    // Native Inventory member reference; client birth/Store.Set do NOT allocate it.
    // Only the explicit, currently unwired inventory member phase assigns this owner.
    internal LegacyNativeInventoryRuntime? Inventory;

    // Native Rune declaration283; assigned only by the explicit DataCore item-owner prefix.
    internal LegacyNativeRuneItemStorage? Rune;

    // Native HairfaceStorage285: accepted-existing-character prefix owns conditional birth.
    internal LegacyNativeHairfaceStorageRuntime? HairfaceStorage;

    // Native GameClient1562..1572: lazy enumeration, no sorting/dedup/null-owner repair.
    internal System.Collections.Generic.IEnumerable<LegacyNativeGameItemRuntime> GetAllMainItems()
    {
        foreach (LegacyNativeGameItemRuntime value in Inventory!.ClientItems.Values)
            yield return value;
        foreach (LegacyNativeGameItemRuntime value2 in Equipment!.ClientItems.Values)
            yield return value2;
    }

    // Native GameClient1574..1608. Bags use Values, NOT their cached GetValues().
    internal System.Collections.Generic.IEnumerable<LegacyNativeGameItemRuntime> AllMyItems()
    {
        foreach (LegacyNativeGameItemRuntime value in Inventory!.ClientItems.Values)
            yield return value;
        foreach (LegacyNativeGameItemRuntime value2 in Equipment!.ClientItems.Values)
            yield return value2;
        foreach (System.Collections.Concurrent.ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> Wh in Warehouse!.ClientItems.Values)
        {
            foreach (LegacyNativeGameItemRuntime value3 in Wh.Values)
                yield return value3;
        }
        foreach (LegacyNativeGameItemRuntime allItem in MyWardrobe!.GetAllItems())
            yield return allItem;
        LegacyNativeGameItemRuntime[] objects = Rune!.Objects;
        for (int i = 0; i < objects.Length; i++)
            yield return objects[i];
        foreach (LegacyNativeGameItemRuntime value4 in RelicFuse!.Values)
            yield return value4;
        foreach (LegacyNativeGameItemRuntime value5 in GearMythsoulBag!.Values)
            yield return value5;
    }

    // Native GameClient1610..1621 deliberately does NOT count the two bags.
    internal int GetItemsCount()
    {
        int num = 0;
        num += Inventory!.ClientItems.Count;
        num += Equipment!.ClientItems.Count;
        foreach (System.Collections.Concurrent.ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> value in Warehouse!.ClientItems.Values)
            num += value.Count;
        num += MyWardrobe!.GetCountItems();
        return num + Rune!.Objects.Length;
    }
}

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player333/335: direct UInt32 fields, before RelicSpirit447.
    public uint DeityLandLuckyPoints;

    public uint HeroPoints;

    // Native Player443/445: direct byte state immediately before RelicSpirit447.
    public byte XPBooster = 0;

    public byte XPKiller = 0;

    // Native Player447: direct field before MyRandom677; LoadCharacter759 owns its future read.
    public bool RelicSpirit;

    // Native field initializer: ONE shared Pool seed draw BEFORE Owner/BitVector ctor body.
    public Random MyRandom = new Random(LegacyNativePoolRandom.GetRandom.Next());

    // Native Player713/715: plain bool fields, after MyRandom677 and before Kills1053.
    public bool SpawinEffect = false;

    public bool SpawinEffectSkills = false;

    // Native Player843: direct field; LoadCharacter758 reads before760 increments.
    public uint Online;

    // Native Player1031: the single mutable physical status-vector owner.
    internal LegacyNativeStatusFlagsBigVector32 BitVector;

    // Native Player.Kills is a direct UInt32 field, incremented before family8418 RNG.
    public uint Kills = 0u;

    public LegacyNativePlayerRuntime(LegacyNativeGameClientRuntime owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        AllowDynamic = false;
        Owner = owner;
        ObjType = LegacyNativeMapObjectType.Player;
        View = new LegacyNativeRoleView(Owner);
        BitVector = new LegacyNativeStatusFlagsBigVector32(576);
    }

    public LegacyNativeGameClientRuntime Owner { get; }

    // Native Player915 direct field and1590 plain auto-property. No Class setter stand-in.
    public uint GuildID;
    public uint UID { get; set; }

    // Diagnostic only: completed real Next calls, never a seed/roll/result injection seam.
    private long _completedRateDraws;
    internal long CompletedRateDraws => System.Threading.Interlocked.Read(ref _completedRateDraws);

    public bool Rate(int value)
    {
        var raw = MyRandom.Next();
        System.Threading.Interlocked.Increment(ref _completedRateDraws);
        return value > raw % 100;
    }
}
