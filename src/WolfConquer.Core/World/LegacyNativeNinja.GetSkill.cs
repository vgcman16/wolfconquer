#nullable disable
using System.Collections.Generic;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    public ushort GetSkill()
    {
        List<ushort> list = new List<ushort>();
        if (user.MySpells.ClientSpells.ContainsKey(16320))
        {
            list.Add(16320);
        }
        if (user.MySpells.ClientSpells.ContainsKey(16340))
        {
            list.Add(16340);
        }
        if (user.MySpells.ClientSpells.ContainsKey(16410))
        {
            list.Add(16410);
        }
        if (user.MySpells.ClientSpells.ContainsKey(16430))
        {
            list.Add(16430);
        }
        if (user.MySpells.ClientSpells.ContainsKey(16450))
        {
            list.Add(16450);
        }
        if (user.MySpells.ClientSpells.ContainsKey(16470))
        {
            list.Add(16470);
        }
        if (user.MySpells.ClientSpells.ContainsKey(16490))
        {
            list.Add(16490);
        }
        if (user.MySpells.ClientSpells.ContainsKey(16510))
        {
            list.Add(16510);
        }
        if (user.MySpells.ClientSpells.ContainsKey(16530))
        {
            list.Add(16530);
        }
        if (user.MySpells.ClientSpells.ContainsKey(16550))
        {
            list.Add(16550);
        }
        LegacyNativeMsgSpell value = null;
        if (user.MySpells.ClientSpells.TryGetValue(16390, out value))
        {
            LegacyNativeMagicType.Magic magic = LegacyNativePoolMagic.Magic[value.ID][value.Level];
            if (LegacyNativeRoleCore.Rate(magic.Rate))
            {
                return 16390;
            }
        }
        if (list.Count != 0)
        {
            return list[LegacyNativePoolRandom.GetRandom.Next(0, list.Count)];
        }
        return 0;
    }
}
