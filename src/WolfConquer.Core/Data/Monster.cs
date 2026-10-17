namespace WolfConquer.Core.Data;

public sealed record Monster(int Id, string Name, IReadOnlyDictionary<string, string> Attributes, IReadOnlyList<SpecialDrop> SpecialDrops);

public sealed record SpecialDrop(int ItemTypeId, int ChanceOrCount);
