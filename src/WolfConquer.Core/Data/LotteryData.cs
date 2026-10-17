namespace WolfConquer.Core.Data;

public sealed record LotteryDictionaryEntry(
    int Id,
    int Type,
    int SubType,
    int Value,
    int Unknown0,
    int Weight,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
