using System.Collections.Concurrent;
using System.Linq;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Native Role.Instance.Spell physical owner and methods. RuneSpells queries the
// genuine mutable Pool.Magic owner directly, never a snapshot or managed catalog.
internal partial class LegacyNativeSpellStorage
{
    public ConcurrentDictionary<ushort, LegacyNativeMsgSpell> ClientSpells =
        new ConcurrentDictionary<ushort, LegacyNativeMsgSpell>();

    private LegacyNativeGameClientRuntime Owner;

    public LegacyNativeMsgSpell[] RuneSpells => ClientSpells.Values.Where(
        (LegacyNativeMsgSpell i) => LegacyNativePoolMagic.Magic.ContainsKey(i.ID) &&
            LegacyNativePoolMagic.Magic[i.ID][0].isRuneSkill).ToArray();

    public LegacyNativeSpellStorage(LegacyNativeGameClientRuntime _own)
    {
        Owner = _own;
    }

    public bool CheckSpell(ushort ID, byte Level)
    {
        if (ClientSpells.TryGetValue(ID, out var value))
        {
            return value.Level >= Level;
        }
        return false;
    }

    public void ClearSpells(List<ushort> StaticSpells, LegacyNativePacket stream)
    {
        ArgumentNullException.ThrowIfNull(StaticSpells);
        var retained = new HashSet<ushort>();
        foreach (var id in StaticSpells) retained.Add(id);
        ExecuteTypedMutation(stream, SpellMutationKind.Clear, 0, null, retained);
    }

    public void Remove(ushort ID, LegacyNativePacket stream)
    {
        ExecuteTypedMutation(stream, SpellMutationKind.Remove, ID, null, null);
    }

    public void Add(LegacyNativePacket stream, ushort ID, ushort level = 0,
        byte levelsoul = 0, byte previouslevel = 0, int Experience = 0, bool ClearExp = false)
    {
        if (!PermittedNinjaSkill(ID)) return;
        var row = CreateTypedSpell(ID, level, levelsoul, previouslevel, ClearExp ? 0 : Experience);
        ExecuteTypedMutation(stream, SpellMutationKind.Add, ID, row, null);
    }

    public bool RebornSpell(LegacyNativePacket stream, ushort ID)
    {
        return ExecuteTypedMutation(stream, SpellMutationKind.Reborn, ID, null, null);
    }

    public void Update(LegacyNativePacket stream, LegacyNativeMsgSpell spell)
    {
        ArgumentNullException.ThrowIfNull(spell);
        Add(stream, spell.ID, spell.Level, spell.SoulLevel, spell.PreviousLevel, spell.Experience);
    }

    public void SendAll(LegacyNativePacket stream)
    {
        foreach (LegacyNativeMsgSpell value in ClientSpells.Values)
        {
            Owner.Send(stream.SpellCreate(value));
        }
    }
}
