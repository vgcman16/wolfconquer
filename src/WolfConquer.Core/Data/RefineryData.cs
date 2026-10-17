namespace WolfConquer.Core.Data;

public sealed record RefineryBox(
    int ItemType,
    IReadOnlyList<int> Values,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);

public sealed record RefineryMaterial(
    int ItemType,
    string Name,
    int PrimaryValue,
    int EffectType,
    IReadOnlyList<int> Parameters,
    string Target,
    string Description,
    IReadOnlyList<int> TailValues,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);

public sealed record RefinerySoul(
    int SoulLevel,
    string EquipmentName,
    int ItemType,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
