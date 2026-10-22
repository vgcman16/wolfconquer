#nullable disable

using System.Collections.Generic;

namespace WolfConquer.Core.Data;

// Native CQ_COMBAT_GEAR row/cache and lookup only. Load/registration is not ported.
internal class LegacyNativeCombatGearTable
{
    public class CQ
    {
        public uint ID;

        public uint Type;

        public uint Level;

        public uint Exp;

        public uint MaxHP;

        public uint PAttack;

        public uint PDefense;

        public uint MDefense;

        public uint[] Skills;

        public uint Score;
    }

    public static Dictionary<uint, Dictionary<uint, CQ>> Info;

    internal static bool m_Load = false;

    public static string Location = "";

    public static bool TryGetValue(uint id, uint level, out CQ type)
    {
        if (Info.ContainsKey(id) && Info[id].TryGetValue(level, out type))
        {
            return true;
        }
        type = null;
        return false;
    }
}
