namespace WolfConquer.Core.Data;

public sealed record HundredWeapon(
    int Id,
    int WeaponType,
    int Level,
    IReadOnlyList<int> Values,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
