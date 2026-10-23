using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Whole native Player3462..3468. Deliberately additive, not idempotent.
    internal void CheckAura()
    {
        if (UseAura != LegacyNativeMsgUpdate.Flags.Normal)
        {
            IncreaseStatusAura(UseAura, Aura!);
        }
    }

    // Whole native Player3529..3555, reading the current physical Aura only in matching cases.
    private void DecreaseStatusAura(LegacyNativeMsgUpdate.Flags flag)
    {
        switch (flag)
        {
            case LegacyNativeMsgUpdate.Flags.FeandAura:
                Owner.Status.Immunity -= NativeAuraUInt32((double)Aura!.Damage * 100.0);
                break;
            case LegacyNativeMsgUpdate.Flags.TyrantAura:
                Owner.Status.CriticalStrike -= NativeAuraUInt32((double)Aura!.Damage * 100.0);
                break;
            case LegacyNativeMsgUpdate.Flags.MetalAura:
                Owner.Status.MetalResistance -= NativeAuraUInt32(Aura!.Damage);
                break;
            case LegacyNativeMsgUpdate.Flags.WoodAura:
                Owner.Status.WoodResistance -= NativeAuraUInt32(Aura!.Damage);
                break;
            case LegacyNativeMsgUpdate.Flags.WaterAura:
                Owner.Status.WaterResistance -= NativeAuraUInt32(Aura!.Damage);
                break;
            case LegacyNativeMsgUpdate.Flags.FireAura:
                Owner.Status.FireResistance -= NativeAuraUInt32(Aura!.Damage);
                break;
            case LegacyNativeMsgUpdate.Flags.EartAura:
                Owner.Status.EarthResistance -= NativeAuraUInt32(Aura!.Damage);
                break;
        }
    }

    // Whole native Player3557..3583. Native cast results and unchecked uint add remain literal.
    private void IncreaseStatusAura(LegacyNativeMsgUpdate.Flags flag, LegacyNativeMagicType.Magic new_aura)
    {
        switch (flag)
        {
            case LegacyNativeMsgUpdate.Flags.FeandAura:
                Owner.Status.Immunity += NativeAuraUInt32((double)new_aura.Damage * 100.0);
                break;
            case LegacyNativeMsgUpdate.Flags.TyrantAura:
                Owner.Status.CriticalStrike += NativeAuraUInt32((double)new_aura.Damage * 100.0);
                break;
            case LegacyNativeMsgUpdate.Flags.MetalAura:
                Owner.Status.MetalResistance += NativeAuraUInt32(new_aura.Damage);
                break;
            case LegacyNativeMsgUpdate.Flags.WoodAura:
                Owner.Status.WoodResistance += NativeAuraUInt32(new_aura.Damage);
                break;
            case LegacyNativeMsgUpdate.Flags.WaterAura:
                Owner.Status.WaterResistance += NativeAuraUInt32(new_aura.Damage);
                break;
            case LegacyNativeMsgUpdate.Flags.FireAura:
                Owner.Status.FireResistance += NativeAuraUInt32(new_aura.Damage);
                break;
            case LegacyNativeMsgUpdate.Flags.EartAura:
                Owner.Status.EarthResistance += NativeAuraUInt32(new_aura.Damage);
                break;
        }
    }
}
