namespace WolfConquer.Core.Data;

public sealed record MonsterSpawn(
    int GeneratorId,
    int MapId,
    int MonsterTypeId,
    int BoundX,
    int BoundY,
    int BoundWidth,
    int BoundHeight,
    int MaxNpc,
    int RestSeconds,
    int MaxPerGenerator,
    int TimerBegin,
    int TimerEnd,
    int BornX,
    int BornY,
    int? SourceMapFolderId,
    string? SourceMonsterName,
    string SourceRelativePath,
    IReadOnlyDictionary<string, string> Attributes);
