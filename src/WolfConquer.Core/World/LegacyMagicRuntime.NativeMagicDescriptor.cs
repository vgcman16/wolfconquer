using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class LegacyMagicRuntime
{
    // Explicit native descriptor: opt-in callers must read Damage from the shared float row.
    // The old public uint DTO is a transport adapter, never the mutable scalar authority.
    internal static bool TryResolveNativeLoadedSpell(
        WorldDataCatalog data, ushort id, ushort level,
        out LegacyMagicRuntimeSpell spell, out LegacyNativeMagicRow native)
    {
        spell = default!;
        native = data.FindNativeMagicRow(id, level)!;
        if (native is null) return false;
        var f = native.Source.Fields;
        var culture = data.NativeMagicParseCulture;
        var sort = byte.Parse(f[2], culture);
        var attackInFly = byte.Parse(f[5], culture);
        var useMana = ushort.Parse(f[9], culture);
        _ = int.Parse(f[12], culture); // Rate, NOT GDamage.
        var maxTargets = unchecked((byte)ushort.Parse(f[14], culture));
        var experience = uint.Parse(f[18], culture);
        var needLevel = ushort.Parse(f[20], culture);
        var weapon = uint.Parse(f[22], culture);
        var stamina = byte.Parse(f[29], culture);
        var duration = native.Id == 6_001 ? 20U : uint.Parse(f[13], culture);
        _ = ushort.Parse(f[48], culture); // CpsCost and AutoLearn are source load validation.
        _ = byte.Parse(f[30], culture);
        var human = int.Parse(f[35], culture);
        var damage2 = int.Parse(f[36], culture);
        var damage3 = int.Parse(f[37], culture);
        _ = int.Parse(f[43], culture);
        var monster = int.Parse(f[44], culture);
        var coldTime = int.Parse(f[47], culture);
        var range = byte.Parse(f[15], culture);
        _ = int.Parse(f[16], culture);
        spell = new LegacyMagicRuntimeSpell(
            native.Id, (LegacyMagicSort)sort, f[3], native.Level, useMana,
            unchecked((uint)(int)native.Damage), experience, maxTargets, range,
            needLevel, weapon, stamina, native.CoolDown, coldTime, duration,
            unchecked((uint)human), unchecked((uint)damage2), unchecked((uint)damage3),
            unchecked((uint)monster), IsLegacyAttackInFly(native.Id, attackInFly),
            unchecked((uint)native.GDamage), unchecked((uint)native.GDamage));
        spell = WithRuneClassification(data, spell);
        return true;
    }

    internal static bool TryResolveNativeLearnedSpell(
        PlayerSpellSnapshot book, WorldDataCatalog data, ushort id,
        out PlayerSpellSnapshotEntry learned, out LegacyMagicRuntimeSpell spell,
        out LegacyNativeMagicRow native)
    {
        foreach (var entry in book.Spells)
        {
            if (entry.Id != id) continue;
            if (TryResolveNativeLoadedSpell(data, id, entry.Level, out spell, out native))
            {
                learned = entry;
                return true;
            }
            break;
        }
        learned = default!;
        spell = default!;
        native = default!;
        return false;
    }
}
