using System.Collections.Concurrent;
using System.Linq;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Native Role.Instance.Spell physical owner and methods. RuneSpells queries the
// genuine mutable Pool.Magic owner directly, never a snapshot or managed catalog.
internal partial class LegacyNativeSpellStorage
{
    public ConcurrentDictionary<ushort, LegacyNativeMsgSpell> ClientSpells = new();
    private LegacyNativeGameClientRuntime Owner;
    private long mutationGeneration;
    internal long MutationGenerationPlain => Volatile.Read(ref mutationGeneration);

    internal void PublishPreparedMapPlain(ConcurrentDictionary<ushort, LegacyNativeMsgSpell> candidate)
    {
        ClientSpells = candidate;
        Interlocked.Increment(ref mutationGeneration);
    }

    public LegacyNativeMsgSpell[] RuneSpells => ClientSpells.Values.Where(
        (LegacyNativeMsgSpell i) => LegacyNativePoolMagic.Magic.ContainsKey(i.ID) &&
            LegacyNativePoolMagic.Magic[i.ID][0].isRuneSkill).ToArray();

    public LegacyNativeSpellStorage(LegacyNativeGameClientRuntime _own)
    { ArgumentNullException.ThrowIfNull(_own); Owner = _own; }

    public bool CheckSpell(ushort ID, byte Level) =>
        ClientSpells.TryGetValue(ID, out var value) && value.Level >= Level;

    public void ClearSpells(List<ushort> StaticSpells, LegacyNativePacket stream)
    {
        ArgumentNullException.ThrowIfNull(StaticSpells);
        ExecuteTypedMutation(stream, new LegacyNativeSpellMutationIntent.Clear(StaticSpells));
    }

    public void Remove(ushort ID, LegacyNativePacket stream) =>
        ExecuteTypedMutation(stream, new LegacyNativeSpellMutationIntent.Remove(ID));

    public void Add(LegacyNativePacket stream, ushort ID, ushort level = 0,
        byte levelsoul = 0, byte previouslevel = 0, int Experience = 0, bool ClearExp = false)
    {
        if (!PermittedNinjaSkill(ID)) return;
        ExecuteTypedMutation(stream, new LegacyNativeSpellMutationIntent.Add(
            ID, level, levelsoul, previouslevel, Experience, ClearExp));
    }

    public bool RebornSpell(LegacyNativePacket stream, ushort ID) =>
        ExecuteTypedMutation(stream, new LegacyNativeSpellMutationIntent.Reborn(ID));

    public void Update(LegacyNativePacket stream, LegacyNativeMsgSpell spell)
    {
        ArgumentNullException.ThrowIfNull(spell);
        Add(stream, spell.ID, spell.Level, spell.SoulLevel, spell.PreviousLevel, spell.Experience);
    }

    public void SendAll(LegacyNativePacket stream)
    {
        var binding = GetJoinedBindingForEntry();
        if (binding is not null) { SendAllJoined(binding, stream); return; }
        foreach (LegacyNativeMsgSpell value in ClientSpells.Values) Owner.Send(stream.SpellCreate(value));
    }
}
