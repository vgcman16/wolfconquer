namespace WolfConquer.Core.Data;

// Entire native ElTabakh.cq_syn_skill_type row/enum/cache declarations.
internal partial class LegacyNativeGuildSkillTypeTable
{
    public class SkillType
    {
        public uint ID;
        public uint Type;
        public uint Level;
        public uint data1;
        public uint data2;
        public uint Silver;
        public uint DonationPoints;
    }

    public enum Type : uint
    {
        MaxHP = 1u, PAttack, PDefense, MAttack, MDefense, FinalPAttack, FinalPDamage,
        FinalMAttack, FinalMDamage, PStrike, MStrike, Immunity, Break, AntiBreak,
    }

    public static Dictionary<uint, Dictionary<uint, SkillType>> _syn_skill_type = null!;
}
