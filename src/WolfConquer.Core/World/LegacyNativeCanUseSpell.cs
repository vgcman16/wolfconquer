#nullable disable
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Complete native CanUseSpell.Verified. The native attack dispatcher/host caller remains absent.
internal class LegacyNativeCanUseSpell
{
    public static bool Verified(LegacyNativeInteractQuery Attack, LegacyNativeGameClientRuntime client,
        Dictionary<ushort, LegacyNativeMagicType.Magic> DBSpells, out LegacyNativeMsgSpell ClientSpell,
        out LegacyNativeMagicType.Magic Spell)
    {
        try
        {
            foreach (KeyValuePair<ushort, List<ushort>> weaponSpell in LegacyNativePoolMagic.WeaponSpells)
            {
                if (weaponSpell.Value.Contains(Attack.SpellID))
                {
                    if (client.Player.RandomSpell != Attack.SpellID)
                    {
                        ClientSpell = null;
                        Spell = null;
                        return false;
                    }
                    client.Player.RandomSpell = 0;
                    break;
                }
            }
            if (client.MySpells.ClientSpells.TryGetValue(Attack.SpellID, out ClientSpell) &&
                DBSpells.TryGetValue(ClientSpell.Level, out Spell))
            {
                if ((Spell.Type == LegacyNativeMagicType.MagicSort.DirectAttack || Spell.Type == LegacyNativeMagicType.MagicSort.Attack) &&
                    !client.IsInSpellRange(Attack.OpponentUID, Spell.Range))
                {
                    ClientSpell = null;
                    Spell = null;
                    return false;
                }
                uint num = 0u;
                if (client.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.ScurvyBomb))
                {
                    num += 6;
                }
                if (ClientSpell.UseSpellSoul > 0)
                {
                    client.OnSoulSpell = ClientSpell.ID;
                }
                else
                {
                    client.OnSoulSpell = 0;
                }
                if (client.Player.Map != 1039)
                {
                    int num2 = 0;
                    if ((uint)(Spell.UseStamina + num - num2) > client.Player.Stamina)
                    {
                        return false;
                    }
                    if ((ushort)(Spell.UseStamina + num) > 0)
                    {
                        client.Player.Stamina -= (ushort)(Spell.UseStamina + num - num2);
                        using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
                        LegacyNativePacket stream = recycledPacket.GetStream();
                        client.Player.SendUpdate(stream, client.Player.Stamina, LegacyNativeMsgUpdate.DataType.Stamina);
                    }
                    int num3 = Spell.UseMana;
                    byte itemLevel = 0;
                    if (client.Rune.IsEquipped("MPMaster", ref itemLevel))
                    {
                        double num4 = itemLevel * 10;
                        switch (itemLevel)
                        {
                            case 2: num4 = 15.0; break;
                            case 3: num4 = 25.0; break;
                            case 7: num4 = 75.0; break;
                            case 8: num4 = 85.0; break;
                            case 9: num4 = 90.0; break;
                        }
                        num3 -= (int)((double)num3 * num4 / 100.0);
                    }
                    if (num3 > client.Player.Mana)
                    {
                        return false;
                    }
                    if (num3 > 0)
                    {
                        client.Player.Mana -= (ushort)num3;
                    }
                }
                if (Spell.IsSpellWithColdTime && Spell.ID != 15710 && Spell.ID != 20280 &&
                    !client.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.CannonBarrage) &&
                    !client.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.BlackbeardsRage))
                {
                    DateTime now = DateTime.Now;
                    if (ClientSpell.ColdTime > now)
                    {
                        return false;
                    }
                    int num5 = 0;
                    if (client.MyNinja.TryGetValueEquip(LegacyNativeNinja.ItemType.WildSigilRapid, out var obj) && ClientSpell.ID == 16520)
                    {
                        num5 = obj.DBItem.Power;
                    }
                    if (client.MyNinja.TryGetValueEquip(LegacyNativeNinja.ItemType.FlameSigilScorch, out obj) && ClientSpell.ID == 16540)
                    {
                        num5 = obj.DBItem.Power;
                    }
                    if (client.MyNinja.TryGetValueEquip(LegacyNativeNinja.ItemType.DustSigilExtinction, out obj) && ClientSpell.ID == 16460)
                    {
                        num5 = obj.DBItem.Power;
                    }
                    if (client.MyNinja.TryGetValueEquip(LegacyNativeNinja.ItemType.SickleSigilGallop, out obj) && ClientSpell.ID == 16500)
                    {
                        num5 = obj.DBItem.Power;
                    }
                    ClientSpell.IsSpellWithColdTime = true;
                    ClientSpell.ColdTime = now.AddMilliseconds(Spell.ColdTime - num5);
                }
                return true;
            }
            ClientSpell = null;
            Spell = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            ClientSpell = null;
            Spell = null;
            return false;
        }
        return false;
    }
}
