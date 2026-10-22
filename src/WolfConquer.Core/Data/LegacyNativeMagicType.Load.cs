#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// Full native MagicType.Load220..411: incremental publication and native fault order are retained.
internal partial class LegacyNativeMagicType
{
    public void Load()
    {
        LegacyNativeItemCounterShellIni iniFile = new LegacyNativeItemCounterShellIni("MagicEffect.ini");
        if (File.Exists(LegacyNativeServerConfig.DbLocation + "magictype.txt"))
        {
            using StreamReader streamReader = File.OpenText(LegacyNativeServerConfig.DbLocation + "magictype.txt");
            string text;
            while ((text = streamReader.ReadLine()) != null)
            {
                string[] array = text.Split(new string[1] { "@@" }, StringSplitOptions.RemoveEmptyEntries);
                Magic magic = new Magic();
                magic.ID = ushort.Parse(array[1]);
                magic.isRuneSkill = iniFile.ReadByte(magic.ID + "00", "IsRuneSkill", 0) != 0;
                magic.AttackInFly = byte.Parse(array[5]) == 0;
                magic.isTrojanArchiveSkill = magic.ID >= 15860 && magic.ID <= 15940;
                if (!AttackInFlay(magic) && magic.AttackInFly)
                {
                    magic.AttackInFly = AttackInFlay(magic);
                }
                magic.Type = (MagicSort)byte.Parse(array[2]);
                magic.Name = array[3];
                magic.Level = byte.Parse(array[8]);
                magic.UseMana = ushort.Parse(array[9]);
                if (magic.ID == 13030 || magic.ID == 15560 || magic.ID == 14320 || magic.ID == 12200 || magic.ID == 15440 || magic.ID == 15470 || magic.ID == 14500)
                {
                    magic.Damage = int.Parse(array[10]);
                }
                else if (magic.ID == 6001)
                {
                    magic.Damage = (int)(byte)(double.Parse(array[10]) % 100.0);
                }
                else if (((magic.Type == MagicSort.AddMana || magic.Type == MagicSort.Recruit || magic.Type == MagicSort.Attack) && double.Parse(array[10]) < 10000.0) || magic.Type == MagicSort.Auras)
                {
                    magic.Damage = int.Parse(array[10]);
                }
                else
                {
                    magic.Damage = Math.Min((float)(double.Parse(array[10]) % 1000.0), 500f);
                }
                if (magic.ID == 12580 || magic.ID == 12590 || magic.ID == 12600)
                {
                    magic.Damage = 100f;
                }
                magic.CoolDown = ushort.Parse(array[33]);
                if (magic.ID == 10415)
                {
                    magic.CoolDown = 800;
                }
                if (magic.Damage == 0f)
                {
                    magic.Damage = 100f;
                }
                if (magic.ID == 11140)
                {
                    magic.Damage = 150f;
                }
                if (magic.ID == 10490)
                {
                    magic.Damage = 150f;
                }
                if (magic.ID == 3306)
                {
                    magic.Damage = 1f;
                }
                if (magic.ID == 12380)
                {
                    magic.Damage = 5000f;
                }
                magic.Rate = int.Parse(array[12]);
                if (magic.ID == 7030)
                {
                    magic.Rate += 30;
                }
                magic.MaxTargets = (byte)ushort.Parse(array[14]);
                magic.Experience = uint.Parse(array[18]);
                magic.NeedLevel = ushort.Parse(array[20]);
                magic.WeaponType = uint.Parse(array[22]);
                magic.UseStamina = byte.Parse(array[29]);
                if (magic.ID == 6001)
                {
                    magic.Duration = 20u;
                }
                else
                {
                    magic.Duration = uint.Parse(array[13]);
                }
                magic.CpsCost = ushort.Parse(array[48]);
                magic.AutoLearn = byte.Parse(array[30]);
                magic.DamageOnHuman = int.Parse(array[35]);
                magic.Damage2 = int.Parse(array[36]);
                magic.Damage3 = int.Parse(array[37]);
                magic.GDamage = int.Parse(array[10]);
                magic.width = int.Parse(array[43]);
                magic.DamageOnMonster = int.Parse(array[44]);
                magic.ColdTime = int.Parse(array[47]);
                magic.Range = byte.Parse(array[15]);
                magic.Status = int.Parse(array[16]);
                magic.Sector = (ushort)(magic.Range * 20);
                List<ushort> list = new List<ushort>();
                if (magic.WeaponType != 0 && magic.WeaponType != 50000)
                {
                    ushort num = (ushort)(magic.WeaponType % 1000);
                    if (num == 60000)
                    {
                        num = 614;
                    }
                    ushort num2 = (ushort)(magic.WeaponType / 1000 % 1000);
                    if (num > 0)
                    {
                        list.Add(num);
                    }
                    if (num2 > 0)
                    {
                        list.Add(num2);
                    }
                }
                if (magic.ID == 12070)
                {
                    magic.Damage = 104f;
                }
                magic.Passive = magic.isTrojanArchiveSkill || iniFile.ReadString(magic.ID + "00", "DescEx", "").Contains("Passive");
                if (ContainsKey(magic.ID))
                {
                    if (!base[magic.ID].ContainsKey(magic.Level))
                    {
                        base[magic.ID].Add(magic.Level, magic);
                    }
                }
                else
                {
                    Add(magic.ID, new Dictionary<ushort, Magic>());
                    base[magic.ID].Add(magic.Level, magic);
                }
                if (list.Count == 0)
                {
                    continue;
                }
                switch (magic.ID)
                {
                case 1250:
                case 1260:
                case 1290:
                case 1300:
                case 5010:
                case 5020:
                case 5030:
                case 5040:
                case 5050:
                case 7000:
                case 7010:
                case 7020:
                case 7030:
                case 7040:
                case 10490:
                case 11140:
                case 11990:
                case 12110:
                case 12210:
                case 12220:
                case 12230:
                case 12240:
                case 12580:
                case 12590:
                case 12600:
                case 12740:
                case 12750:
                case 12760:
                case 13040:
                case 13050:
                case 13060:
                case 14570:
                case 14580:
                case 15670:
                case 15680:
                case 15690:
                    foreach (ushort item in list)
                    {
                        if (!LegacyNativePoolMagic.WeaponSpells.ContainsKey(item))
                        {
                            LegacyNativePoolMagic.WeaponSpells.Add(item, new List<ushort>());
                        }
                        if (!LegacyNativePoolMagic.WeaponSpells[item].Contains(magic.ID))
                        {
                            LegacyNativePoolMagic.WeaponSpells[item].Add(magic.ID);
                        }
                    }
                    break;
                }
            }
        }
        Add(6009, base[6010]);
    }
}
