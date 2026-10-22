#nullable disable

using System.Collections.Generic;

namespace WolfConquer.Core.Data;

// Genuine complete native MagicType row/query owner, distinct from the managed catalog adapter.
// No startup producer or host publication invokes Load here.
internal partial class LegacyNativeMagicType : Dictionary<ushort, Dictionary<ushort, LegacyNativeMagicType.Magic>>
{
    public class Magic
    {
        public ushort ID;

        public string Name;

        public MagicSort Type;

        public byte Level;

        public ushort UseMana;

        public float Damage;

        public int Rate = 0;

        public uint Experience;

        public byte Range;

        public ushort Sector;

        public uint Duration;

        public uint WeaponType;

        public byte UseStamina;

        public uint GiveHitPoints;

        public bool AttackInFly;

        public bool isTrojanArchiveSkill;

        public int Status;

        public ushort NeedLevel = 0;

        public uint CpsCost = 0u;

        public byte MaxTargets = 0;

        public byte IncreaseStamin = 0;

        public ushort CoolDown = 100;

        public byte AutoLearn = 0;

        public int ColdTime = 0;

        public int Damage2;

        public int Damage3;

        public int DamageOnHuman;

        public int DamageOnMonster;

        public bool Passive;

        public bool isRuneSkill;

        public int width;

        public int GDamage;

        public bool IsSpellWithColdTime => ColdTime > 0;
    }

    public static List<ushort> Weaponextra = new List<ushort>();

    public bool AttackInFlay(Magic DBSpell)
    {
        if (DBSpell.ID == 1045 || DBSpell.ID == 1046 || DBSpell.ID == 1250 || DBSpell.ID == 1260 || DBSpell.ID == 1290 || (DBSpell.ID >= 5010 && DBSpell.ID <= 5050) || DBSpell.ID == 6000 || DBSpell.ID == 6001 || (DBSpell.ID >= 7000 && DBSpell.ID <= 7040) || DBSpell.ID == 10315 || DBSpell.ID == 10381 || DBSpell.ID == 10415 || DBSpell.ID == 10490 || DBSpell.ID == 11000 || DBSpell.ID == 11005 || DBSpell.ID == 11040 || DBSpell.ID == 11070 || DBSpell.ID == 11110 || DBSpell.ID == 11140 || DBSpell.ID == 11170 || DBSpell.ID == 11180 || DBSpell.ID == 11190 || DBSpell.ID == 11230 || DBSpell.ID == 6010)
        {
            return false;
        }
        return true;
    }

    public uint GetSpell(string name)
    {
        uint result = 0u;
        foreach (Dictionary<ushort, Magic> value in base.Values)
        {
            foreach (Magic value2 in value.Values)
            {
                if (value2.Name != null && value2.Name.Contains(name))
                {
                    result = value2.ID;
                }
            }
        }
        return result;
    }
}
