namespace WolfConquer.Core.Data;

public sealed record ArenaRecord(
    int PlayerId,
    string PlayerName,
    IReadOnlyList<int> Values,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields)
{
    public int Level => Values[0];
    public int Mesh => Values[1];
    public int Lookface => Values[2];
    public int Points => Values[3];
    public int Prize => Values[10];
    public int Rank => Values[13];
}

public sealed record TeamArenaRecord(
    int PlayerId,
    string PlayerName,
    IReadOnlyList<int> Values,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields)
{
    public int Level => Values[0];
    public int Mesh => Values[1];
    public int Lookface => Values[2];
    public int Points => Values[3];
    public int Prize => Values[10];
    public int Rank => Values[13];
}

public sealed record PkTournamentRecord(
    int Group,
    int Slot,
    int Level,
    string PlayerName,
    int Lookface,
    int Status,
    int PlayerId,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);

public sealed record ClassPkWarRecord(
    int ClassId,
    int Slot,
    int PlayerId,
    int Points,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
