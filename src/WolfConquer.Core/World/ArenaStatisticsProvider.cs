using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public readonly record struct ArenaStatistics(
    uint TodayRank,
    uint ArenaPoints,
    uint TotalWin = 0,
    uint TotalLose = 0,
    uint TodayWin = 0,
    uint TodayBattles = 0,
    uint HistoryHonor = 0,
    uint CurrentHonor = 0);

public readonly record struct TeamArenaStatistics(
    uint TodayRank,
    uint ArenaPoints,
    uint TotalWin = 0,
    uint TotalLose = 0,
    uint TodayWin = 0,
    uint TodayBattles = 0,
    uint HistoryHonor = 0,
    uint CurrentHonor = 0,
    uint Unknown2 = 0,
    uint Unknown3 = 0);

public sealed class ArenaStatisticsProvider
{
    public const uint DefaultArenaPoints = 4000;

    public static ArenaStatisticsProvider Empty { get; } = new([]);

    private readonly IReadOnlyDictionary<int, ArenaRecord> _recordsByPlayerId;
    private readonly IReadOnlyDictionary<int, uint> _todayRanksByPlayerId;

    public ArenaStatisticsProvider(IReadOnlyList<ArenaRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        Records = records;
        _recordsByPlayerId = records
            .GroupBy(record => record.PlayerId)
            .ToDictionary(group => group.Key, group => group.First());
        _todayRanksByPlayerId = BuildTodayRanks(records);
    }

    public IReadOnlyList<ArenaRecord> Records { get; }

    public static ArenaStatisticsProvider FromCatalog(WorldDataCatalog? catalog)
    {
        return catalog is null ? Empty : new ArenaStatisticsProvider(catalog.ArenaRecords);
    }

    public ArenaStatistics GetForCharacter(long characterUid)
    {
        if (characterUid < int.MinValue || characterUid > int.MaxValue)
        {
            return new ArenaStatistics(TodayRank: 0, DefaultArenaPoints);
        }

        var playerId = (int)characterUid;
        if (!_recordsByPlayerId.TryGetValue(playerId, out var record))
        {
            return new ArenaStatistics(TodayRank: 0, DefaultArenaPoints);
        }

        _todayRanksByPlayerId.TryGetValue(playerId, out var todayRank);
        return new ArenaStatistics(
            todayRank,
            checked((uint)record.Points),
            TotalWin: checked((uint)record.Values[9]),
            TotalLose: checked((uint)record.Values[8]),
            TodayWin: checked((uint)record.Values[7]),
            TodayBattles: checked((uint)record.Values[6]),
            HistoryHonor: checked((uint)record.Values[5]),
            CurrentHonor: checked((uint)record.Values[4]));
    }

    private static IReadOnlyDictionary<int, uint> BuildTodayRanks(IReadOnlyList<ArenaRecord> records)
    {
        return records
            .OrderByDescending(record => record.Points)
            .Take(1000)
            .Select((record, index) => (record.PlayerId, Rank: checked((uint)(index + 1))))
            .GroupBy(entry => entry.PlayerId)
            .ToDictionary(group => group.Key, group => group.First().Rank);
    }
}

public sealed class TeamArenaStatisticsProvider
{
    public static TeamArenaStatisticsProvider Empty { get; } = new([]);

    private readonly IReadOnlyDictionary<int, TeamArenaRecord> _recordsByPlayerId;
    private readonly IReadOnlyDictionary<int, uint> _todayRanksByPlayerId;

    public TeamArenaStatisticsProvider(IReadOnlyList<TeamArenaRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        Records = records;
        _recordsByPlayerId = records
            .GroupBy(record => record.PlayerId)
            .ToDictionary(group => group.Key, group => group.First());
        _todayRanksByPlayerId = BuildTodayRanks(records);
    }

    public IReadOnlyList<TeamArenaRecord> Records { get; }

    public static TeamArenaStatisticsProvider FromCatalog(WorldDataCatalog? catalog)
    {
        return catalog is null ? Empty : new TeamArenaStatisticsProvider(catalog.TeamArenaRecords);
    }

    public TeamArenaStatistics GetForCharacter(long characterUid)
    {
        if (characterUid < int.MinValue || characterUid > int.MaxValue)
        {
            return new TeamArenaStatistics(TodayRank: 0, ArenaStatisticsProvider.DefaultArenaPoints);
        }

        var playerId = (int)characterUid;
        if (!_recordsByPlayerId.TryGetValue(playerId, out var record))
        {
            return new TeamArenaStatistics(TodayRank: 0, ArenaStatisticsProvider.DefaultArenaPoints);
        }

        _todayRanksByPlayerId.TryGetValue(playerId, out var todayRank);
        return new TeamArenaStatistics(
            todayRank,
            checked((uint)record.Points),
            TotalWin: ReadValue(record, 9),
            TotalLose: ReadValue(record, 8),
            TodayWin: ReadValue(record, 7),
            TodayBattles: ReadValue(record, 6),
            HistoryHonor: ReadValue(record, 5),
            CurrentHonor: ReadValue(record, 4));
    }

    private static IReadOnlyDictionary<int, uint> BuildTodayRanks(IReadOnlyList<TeamArenaRecord> records)
    {
        return records
            .OrderByDescending(record => record.Points)
            .Take(1000)
            .Select((record, index) => (record.PlayerId, Rank: checked((uint)(index + 1))))
            .GroupBy(entry => entry.PlayerId)
            .ToDictionary(group => group.Key, group => group.First().Rank);
    }

    private static uint ReadValue(TeamArenaRecord record, int index)
    {
        return record.Values.Count > index ? checked((uint)record.Values[index]) : 0;
    }
}
