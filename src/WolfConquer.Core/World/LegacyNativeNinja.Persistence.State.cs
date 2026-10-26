using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    private sealed record PersistenceState
    {
        internal ulong Flag { get; init; }
        internal uint Exp { get; init; }
        internal uint Fire { get; init; }
        internal uint Water { get; init; }
        internal uint Earth { get; init; }
        internal uint Wind { get; init; }
        internal uint Lightning { get; init; }
        internal uint tmp_Fire { get; init; }
        internal uint tmp_Water { get; init; }
        internal uint tmp_Earth { get; init; }
        internal uint tmp_Wind { get; init; }
        internal uint tmp_Lightning { get; init; }
        internal uint FireMastery { get; init; }
        internal uint WaterMastery { get; init; }
        internal uint EarthMastery { get; init; }
        internal uint WindMastery { get; init; }
        internal uint LightningMastery { get; init; }
        internal uint UnlockMastery { get; init; }
        internal ConcurrentDictionary<uint, Item> Items { get; init; } = new();
    }

    private readonly object persistenceLoadGate = new();
    private PersistenceState persistenceState = new();
    private PersistenceState ReadPersistenceState() => Volatile.Read(ref persistenceState);
    private void ChangePersistenceState(Func<PersistenceState, PersistenceState> change)
    {
        lock (persistenceLoadGate)
            Volatile.Write(ref persistenceState, change(ReadPersistenceState()));
    }

    // Preserve the public source names/types. Actual complete Load replaces one
    // aggregate; legacy gameplay readers still require their operation scopes.
    public ulong Flag
    {
        get => ReadPersistenceState().Flag;
        set => ChangePersistenceState(state => state with { Flag = value });
    }
    public uint Exp
    {
        get => ReadPersistenceState().Exp;
        set => ChangePersistenceState(state => state with { Exp = value });
    }
    public uint Fire
    {
        get => ReadPersistenceState().Fire;
        set => ChangePersistenceState(state => state with { Fire = value });
    }
    public uint Water
    {
        get => ReadPersistenceState().Water;
        set => ChangePersistenceState(state => state with { Water = value });
    }
    public uint Earth
    {
        get => ReadPersistenceState().Earth;
        set => ChangePersistenceState(state => state with { Earth = value });
    }
    public uint Wind
    {
        get => ReadPersistenceState().Wind;
        set => ChangePersistenceState(state => state with { Wind = value });
    }
    public uint Lightning
    {
        get => ReadPersistenceState().Lightning;
        set => ChangePersistenceState(state => state with { Lightning = value });
    }
    public uint tmp_Fire
    {
        get => ReadPersistenceState().tmp_Fire;
        set => ChangePersistenceState(state => state with { tmp_Fire = value });
    }
    public uint tmp_Water
    {
        get => ReadPersistenceState().tmp_Water;
        set => ChangePersistenceState(state => state with { tmp_Water = value });
    }
    public uint tmp_Earth
    {
        get => ReadPersistenceState().tmp_Earth;
        set => ChangePersistenceState(state => state with { tmp_Earth = value });
    }
    public uint tmp_Wind
    {
        get => ReadPersistenceState().tmp_Wind;
        set => ChangePersistenceState(state => state with { tmp_Wind = value });
    }
    public uint tmp_Lightning
    {
        get => ReadPersistenceState().tmp_Lightning;
        set => ChangePersistenceState(state => state with { tmp_Lightning = value });
    }
    public uint FireMastery
    {
        get => ReadPersistenceState().FireMastery;
        set => ChangePersistenceState(state => state with { FireMastery = value });
    }
    public uint WaterMastery
    {
        get => ReadPersistenceState().WaterMastery;
        set => ChangePersistenceState(state => state with { WaterMastery = value });
    }
    public uint EarthMastery
    {
        get => ReadPersistenceState().EarthMastery;
        set => ChangePersistenceState(state => state with { EarthMastery = value });
    }
    public uint WindMastery
    {
        get => ReadPersistenceState().WindMastery;
        set => ChangePersistenceState(state => state with { WindMastery = value });
    }
    public uint LightningMastery
    {
        get => ReadPersistenceState().LightningMastery;
        set => ChangePersistenceState(state => state with { LightningMastery = value });
    }
    public uint UnlockMastery
    {
        get => ReadPersistenceState().UnlockMastery;
        set => ChangePersistenceState(state => state with { UnlockMastery = value });
    }
    public ConcurrentDictionary<uint, Item> Items
    {
        get => ReadPersistenceState().Items;
        set => ChangePersistenceState(state => state with { Items = value });
    }
}
