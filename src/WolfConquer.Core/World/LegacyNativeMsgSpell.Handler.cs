using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgSpell
{
    // Native HandlerSpell127..136. Entry body only: no host packet-dispatch adoption.
    public static void HandlerSpell(LegacyNativeGameClientRuntime client, LegacyNativePacket stream)
    {
        stream.GetSpell(out var spell);
        if (client.MySpells!.ClientSpells.TryGetValue(spell.ID, out var value) && value.SoulLevel >= spell.UseSpellSoul)
        {
            value.UseSoul = spell.UseSoul;
            client.MySpells!.ClientSpells[spell.ID] = value;
            spell.Type = 3;
            client.Send(stream.SpellCreate(spell));
        }
    }
}
