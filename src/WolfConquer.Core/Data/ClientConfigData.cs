namespace WolfConquer.Core.Data;

public sealed record ClientConfigRecord(
    int ServerId,
    string ServerName,
    int ClientBuild,
    string LoginXRaw,
    int LoginX,
    string LoginYRaw,
    int LoginY,
    string MapXRaw,
    int MapX,
    string MapYRaw,
    int MapY,
    int Region,
    int Status,
    string Host,
    int Port,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
