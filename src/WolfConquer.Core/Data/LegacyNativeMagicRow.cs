using System.Globalization;

namespace WolfConquer.Core.Data;

// Only native mutable fields/identity are owned here. This does not replace the complete DB loader.
internal sealed class LegacyNativeMagicRow
{
    private float damage;
    private int coolDown;

    private LegacyNativeMagicRow(MagicType source, ushort id, byte level, int gDamage,
        float damage, ushort coolDown)
    {
        Source = source;
        Id = id;
        Level = level;
        GDamage = gDamage;
        this.damage = damage;
        this.coolDown = coolDown;
    }

    internal MagicType Source { get; }
    internal ushort Id { get; }
    internal byte Level { get; }
    internal int GDamage { get; } // Source ALWAYS parses field10 as signedInt32 after Damage assignment.
    internal float Damage => Volatile.Read(ref damage);
    internal ushort CoolDown => unchecked((ushort)Volatile.Read(ref coolDown));
    internal void SetDamage(float value) => Volatile.Write(ref damage, value);
    internal void SetCoolDown(ushort value) => Volatile.Write(ref coolDown, value);

    internal static LegacyNativeMagicRow ParseMutableFields(MagicType source, CultureInfo culture)
    {
        var f = source.Fields;
        var id = ushort.Parse(f[1], culture);
        var sort = byte.Parse(f[2], culture);
        var level = byte.Parse(f[8], culture);
        float power;
        if (id is 13_030 or 15_560 or 14_320 or 12_200 or 15_440 or 15_470 or 14_500)
            power = int.Parse(f[10], culture);
        else if (id == 6_001)
            power = (int)unchecked((byte)(double.Parse(f[10], culture) % 100.0));
        else if (((sort is 20 or 2 or 1) && double.Parse(f[10], culture) < 10_000.0) || sort == 51)
            power = int.Parse(f[10], culture);
        else
            power = Math.Min((float)(double.Parse(f[10], culture) % 1_000.0), 500f);
        if (id is 12_580 or 12_590 or 12_600) power = 100f;
        var cold = ushort.Parse(f[33], culture);
        if (id == 10_415) cold = 800;
        if (power == 0f) power = 100f;
        if (id is 11_140 or 10_490) power = 150f;
        if (id == 3_306) power = 1f;
        if (id == 12_380) power = 5_000f;
        var gDamage = int.Parse(f[10], culture);
        if (id == 12_070) power = 104f;
        return new(source, id, level, gDamage, power, cold);
    }
}
