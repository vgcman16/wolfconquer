namespace WolfConquer.Core.Data;

public sealed record LotteryPrize(
    int Type,
    int Level,
    string ItemName,
    string ItemTypeRaw,
    int? ItemTypeId,
    int? SocketOne,
    int? SocketTwo,
    int? PlusLevel,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
