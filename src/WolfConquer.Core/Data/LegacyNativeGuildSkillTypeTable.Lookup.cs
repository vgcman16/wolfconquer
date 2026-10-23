namespace WolfConquer.Core.Data;

internal partial class LegacyNativeGuildSkillTypeTable
{
    // Whole native cq_syn_skill_type.TryGetValue; `id` actually indexes row.Type.
    public static bool TryGetValue(uint id, uint level, out SkillType? skill_type)
    {
        if (_syn_skill_type.ContainsKey(id) && _syn_skill_type[id].TryGetValue(level, out skill_type))
        {
            return true;
        }
        skill_type = null;
        return false;
    }
}
