namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeEquipItemStorage
{
    // Whole native Equip1865..1882. Requires genuine equipment partial intake; not UpdateStats.
    public void AddInnerPowerAtributes(LegacyNativeInnerPowerRuntime Inner, uint percent = 100u)
    {
        Owner!.Status.CriticalStrike += Inner.CriticalStrike * percent / 100;
        Owner.Status.SkillCStrike += Inner.SkillCriticalStrike * percent / 100;
        Owner.Status.Immunity += Inner.Immunity * percent / 100;
        Owner.Status.Counteraction += Inner.Counteraction * percent / 100;
        Owner.Status.Breakthrough += Inner.Breakthrough * percent / 100;
        Owner.Status.MaxHitpoints += Inner.MaxLife * percent / 100;
        Owner.Status.MaxAttack += Inner.AddAttack * percent / 100;
        Owner.Status.MinAttack += Inner.AddAttack * percent / 100;
        Owner.Status.MagicAttack += Inner.AddMagicAttack * percent / 100;
        Owner.Status.MagicDefence += Inner.AddMagicDefense * percent / 100;
        Owner.Status.PhysicalDamageIncrease += Inner.FinalAttack * percent / 100;
        Owner.Status.PhysicalDamageDecrease += Inner.FinalDefense * percent / 100;
        Owner.Status.MagicDamageIncrease += Inner.FinalMagicAttack * percent / 100;
        Owner.Status.MagicDamageDecrease += Inner.FinalMagicDefense * percent / 100;
        Owner.Status.Defence += Inner.Defence * percent / 100;
    }
}
