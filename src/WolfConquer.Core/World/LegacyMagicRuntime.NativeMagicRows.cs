using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class LegacyMagicRuntime
{
    internal static float ReadNativeDamage(WorldDataCatalog data, LegacyMagicRuntimeSpell spell) =>
        data.FindNativeMagicRow(spell.Id, spell.Level)?.Damage ?? spell.Damage;

    internal static ushort ReadNativeCoolDown(WorldDataCatalog data, LegacyMagicRuntimeSpell spell) =>
        data.FindNativeMagicRow(spell.Id, spell.Level)?.CoolDown ?? spell.CoolDown;

    internal static void NormalizeNativeRootCoolDown(WorldDataCatalog data, LegacyMagicRuntimeSpell spell, ushort value) =>
        data.FindNativeMagicRow(spell.Id, spell.Level)?.SetCoolDown(value);

    internal static void ClampNativePhysicalMonsterDamage(WorldDataCatalog data, LegacyMagicRuntimeSpell spell)
    {
        if (data.FindNativeMagicRow(spell.Id, spell.Level) is { } row && row.Damage < 10f) row.SetDamage(10f);
    }
}
