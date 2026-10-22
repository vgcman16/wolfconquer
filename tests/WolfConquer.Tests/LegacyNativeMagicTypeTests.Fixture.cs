using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using MagicType = WolfConquer.Core.Data.LegacyNativeMagicType;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeMagicTypeTests
{
    private static string Row(ushort id, params (int Index, string Value)[] changes) =>
        NativeMagicTypeTestFixture.Row(id, changes);
}

// Owned file/static MODEL only. No maintained DB, native startup, or host caller is installed.
internal sealed class NativeMagicTypeTestFixture : IDisposable
{
    private readonly MagicType priorMagic = LegacyNativePoolMagic.Magic;
    private readonly Dictionary<ushort, List<ushort>> priorWeapons = LegacyNativePoolMagic.WeaponSpells;
    private readonly List<ushort> priorExtra = MagicType.Weaponextra;
    private readonly string priorLocation = LegacyNativeServerConfig.DbLocation;
    private readonly CultureInfo priorCulture = CultureInfo.CurrentCulture;
    private readonly CultureInfo priorUiCulture = CultureInfo.CurrentUICulture;

    internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-native-magic-" + Guid.NewGuid().ToString("N"));
    internal MagicType Magic { get; } = new();
    internal Dictionary<ushort, List<ushort>> Weapons { get; } = new();
    internal List<ushort> Extra { get; } = new() { 99 };
    internal string DataPath => Path.Combine(Root, "magictype.txt");

    internal NativeMagicTypeTestFixture()
    {
        Directory.CreateDirectory(Root);
        LegacyNativeServerConfig.DbLocation = Root + Path.DirectorySeparatorChar;
        LegacyNativePoolMagic.Magic = Magic;
        LegacyNativePoolMagic.WeaponSpells = Weapons;
        MagicType.Weaponextra = Extra;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
    }

    internal void WriteRows(params string[] rows) => File.WriteAllLines(DataPath, rows, new UTF8Encoding(false));

    internal void Effect(ushort id, string key, string value)
    {
        var ini = new LegacyNativeItemCounterShellIni("MagicEffect.ini");
        Assert.Equal(Path.Combine(Root, "MagicEffect.ini"), ini.FileName);
        ini.WriteString(id.ToString(CultureInfo.InvariantCulture) + "00", key, value);
    }

    internal static string Row(ushort id, params (int Index, string Value)[] changes)
    {
        var fields = Enumerable.Repeat("0", 49).ToArray();
        fields[0] = "row";
        fields[1] = id.ToString(CultureInfo.InvariantCulture);
        fields[2] = "4";
        fields[3] = "Spell" + id.ToString(CultureInfo.InvariantCulture);
        fields[10] = "101";
        fields[33] = "100";
        foreach (var (index, value) in changes) fields[index] = value;
        return string.Join("@@", fields);
    }

    public void Dispose()
    {
        LegacyNativePoolMagic.Magic = priorMagic;
        LegacyNativePoolMagic.WeaponSpells = priorWeapons;
        MagicType.Weaponextra = priorExtra;
        LegacyNativeServerConfig.DbLocation = priorLocation;
        CultureInfo.CurrentCulture = priorCulture;
        CultureInfo.CurrentUICulture = priorUiCulture;
        var full = Path.GetFullPath(Root);
        var expectedParent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(full), expectedParent) ||
            !System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(full), "^wolf-native-magic-[0-9a-f]{32}$"))
            throw new InvalidOperationException("Owned magic fixture cleanup containment failed.");
        if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
    }
}
