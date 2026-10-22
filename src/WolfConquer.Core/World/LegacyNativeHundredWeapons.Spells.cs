#nullable disable
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeHundredWeapons
{
    public bool ValidateSpells()
    {
        bool result = false;
        if (Unlocked && Valid())
        {
            foreach (LegacyNativeHundredWeaponInfo value2 in Objects.Values)
            {
                if (value2.DBSpell != null && !Owner.MySpells.ClientSpells.ContainsKey(value2.DBSpell.ID))
                {
                    using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
                    LegacyNativePacket stream = recycledPacket.GetStream();
                    Owner.MySpells.Add(stream, value2.DBSpell.ID, value2.DBSpell.Level, 0, 0);
                    result = true;
                }
            }
        }
        LegacyNativeMsgSpell[] array = Owner.MySpells.ClientSpells.Values.ToArray();
        LegacyNativeMsgSpell[] array2 = array;
        foreach (LegacyNativeMsgSpell spell in array2)
        {
            if (!LegacyNativePoolMagic.Magic.TryGetValue(spell.ID, out var value) || value.Count <= spell.Level)
            {
                continue;
            }
            if (LegacyNativePoolMagic.Magic.ContainsKey(spell.ID) && LegacyNativePoolMagic.Magic[spell.ID].ContainsKey(spell.Level))
            {
                LegacyNativeMagicType.Magic magic = LegacyNativePoolMagic.Magic[spell.ID][spell.Level];
                if (magic == null || !magic.isTrojanArchiveSkill)
                {
                    continue;
                }
                LegacyNativeHundredWeaponsTable.HundredWeapon hundredWeapon = LegacyNativeHundredWeaponsTable.HundredWeaponsList.Values
                    .FirstOrDefault((LegacyNativeHundredWeaponsTable.HundredWeapon hundredWeapon2) => hundredWeapon2.MagicType == spell.ID);
                if (hundredWeapon != null && (!Unlocked || !Valid() || !ContainsWeapon(hundredWeapon.WeaponSubtype)))
                {
                    using LegacyNativeRecycledPacket recycledPacket2 = new LegacyNativeRecycledPacket();
                    LegacyNativePacket stream2 = recycledPacket2.GetStream();
                    Owner.MySpells.Remove(spell.ID, stream2);
                }
            }
            else
            {
                Console.WriteLine("ERORR [SPELL LOAD] Id: {0} Level {1}", spell.ID, spell.Level);
            }
        }
        return result;
    }
}
