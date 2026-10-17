namespace WolfConquer.Core.Data;

public sealed record MagicType(int Id, int TypeId, string Name, IReadOnlyList<string> Fields);
