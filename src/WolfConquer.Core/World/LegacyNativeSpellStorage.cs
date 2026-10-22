using System.Collections.Concurrent;
using System.Linq;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Native Role.Instance.Spell physical owner and methods. RuneSpells queries the
// genuine mutable Pool.Magic owner directly, never a snapshot or managed catalog.
internal class LegacyNativeSpellStorage
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
        foreach (LegacyNativeMsgSpell value in ClientSpells.Values)
        {
            if (!StaticSpells.Contains(value.ID))
            {
                Remove(value.ID, stream);
            }
            else
            {
                RebornSpell(stream, value.ID);
            }
        }
    }

    public void Remove(ushort ID, LegacyNativePacket stream)
    {
        if (ClientSpells.TryRemove(ID, out var _))
        {
            LegacyNativeActionQuery pQuery = new LegacyNativeActionQuery
            {
                Type = 109,
                ObjId = Owner.Player.UID,
                dwParam = ID
            };
            Owner.Send(stream.ActionCreate(pQuery));
        }
    }

    public void Add(LegacyNativePacket stream, ushort ID, ushort level = 0,
        byte levelsoul = 0, byte previouslevel = 0, int Experience = 0, bool ClearExp = false)
    {
        if (ID != 1105 && (ID < 4000 || ID > 4070))
        {
            LegacyNativeMsgSpell msgSpell = new LegacyNativeMsgSpell();
            msgSpell.ID = ID;
            msgSpell.Level = level;
            msgSpell.Experience = Experience;
            msgSpell.SoulLevel = levelsoul;
            msgSpell.PreviousLevel = previouslevel;
            msgSpell.Experience = Experience;
            if (ClearExp)
            {
                msgSpell.Experience = 0;
            }
            if (ClientSpells.ContainsKey(ID))
            {
                ClientSpells[ID] = msgSpell;
            }
            ClientSpells.TryAdd(msgSpell.ID, msgSpell);
            Owner.Send(stream.SpellCreate(msgSpell));
        }
    }

    public bool RebornSpell(LegacyNativePacket stream, ushort ID)
    {
        if (ClientSpells.TryRemove(ID, out var value))
        {
            Add(stream, ID, 0, value.SoulLevel, (byte)value.Level);
            return true;
        }
        return false;
    }

    public void Update(LegacyNativePacket stream, LegacyNativeMsgSpell spell)
    {
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
