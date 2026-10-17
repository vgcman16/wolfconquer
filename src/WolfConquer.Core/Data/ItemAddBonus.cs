namespace WolfConquer.Core.Data;

public sealed record ItemAddBonus(
    int ItemTypeId,
    int Level,
    IReadOnlyList<int> Values,
    int SourceLine,
    string RawLine);
