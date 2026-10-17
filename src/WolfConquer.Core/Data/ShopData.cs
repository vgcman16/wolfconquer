namespace WolfConquer.Core.Data;

public sealed record ShopData(IReadOnlyList<ShopDefinition> Shops, IReadOnlyList<ShopItem> Items);

public sealed record ShopDefinition(
    int Id,
    string? Name,
    int? Type,
    int? MoneyType,
    string SourceFile,
    string SourceDialect,
    IReadOnlyDictionary<string, string> Attributes);

public sealed record ShopItem(
    int ShopId,
    int ItemTypeId,
    int? Position,
    string? Category,
    string? Status,
    bool IsBound,
    int? Price,
    string? Description,
    IReadOnlyList<string> Flags,
    string SourceFile,
    string SourceDialect,
    string RawLine,
    IReadOnlyDictionary<string, string> Attributes);

public sealed record ExchangeShopRow(string SourceFile, int LineNumber, IReadOnlyList<string> Fields);

public sealed record HonorShopCatalog(
    IReadOnlyList<HonorShopItem> Items,
    IReadOnlyList<HonorShopRecommendation> Recommendations);

public sealed record HonorShopItem(
    int ItemTypeId,
    int Cost,
    int Unknown0,
    string Category,
    bool IsRecommended,
    string? Status,
    int Position,
    string SourceFile,
    string RawLine,
    IReadOnlyList<string> Fields);

public sealed record HonorShopRecommendation(
    int ItemTypeId,
    string Text,
    int Position,
    string SourceFile,
    string RawLine);

public sealed record PointShopCatalog(
    IReadOnlyList<PointShopItem> Items,
    IReadOnlyList<PointShopRecommendation> Recommendations,
    IReadOnlyDictionary<string, string> Config);

public sealed record PointShopItem(
    int ItemTypeId,
    int Cost,
    string ItemClass,
    string Category,
    string? Status,
    string Section,
    int Position,
    string SourceFile,
    string RawLine,
    IReadOnlyList<string> Fields);

public sealed record PointShopRecommendation(
    int ItemTypeId,
    string Text,
    string Section,
    int Position,
    string SourceFile,
    string RawLine);

public sealed record BoothCatalog(
    IReadOnlyList<BoothDefinition> Booths,
    IReadOnlyList<BoothItem> Items);

public sealed record BoothDefinition(
    int Id,
    string Name,
    int Mesh,
    int MapId,
    int? Type,
    int X,
    int Y,
    int? DeclaredItemAmount,
    string SourceSection,
    int SourceLine,
    int Position,
    string SourceFile,
    IReadOnlyDictionary<string, string> Attributes);

public sealed record BoothItem(
    int BoothId,
    int ItemTypeId,
    long Price,
    int? Unknown0,
    string? Tail,
    string? ExtraField,
    int ItemKey,
    int Position,
    string SourceFile,
    int SourceLine,
    string RawLine,
    IReadOnlyList<string> Fields);
