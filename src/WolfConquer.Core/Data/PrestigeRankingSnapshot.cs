using System.Diagnostics.CodeAnalysis;

namespace WolfConquer.Core.Data;

/// <summary>
/// A complete validated projection of saved prestige query data. It is not a live
/// progression, reward, inventory, or persistence authority.
/// </summary>
public sealed class PrestigeRankingSnapshot
{
    public const uint MinimumRankedPoints = 20_000;
    public const int WorldRankLimit = 100;
    public const int ClassRankLimit = 30;
    public const int PointCount = 43;

    private const int CategoryCount = 12;
    private readonly IReadOnlyList<PrestigeRankingEntry>[] ranks;
    private readonly Dictionary<uint, PrestigeRankingEntry> retainedEntriesByUid = [];

    private PrestigeRankingSnapshot(PrestigeRankingEntry[] entries)
    {
        Entries = Array.AsReadOnly(entries);
        ranks = new IReadOnlyList<PrestigeRankingEntry>[CategoryCount];

        // The source orders by descending total points. This managed projection
        // uses stable OrderByDescending to deterministically keep saved order
        // within ties, without assuming legacy Dictionary enumeration behavior.
        var eligible = entries.Where(entry => entry.TotalPoints >= MinimumRankedPoints)
            .OrderByDescending(entry => entry.TotalPoints)
            .ToArray();
        for (var index = 0; index < CategoryCount; index++)
        {
            var category = (PrestigeRankCategory)index;
            var candidates = category == PrestigeRankCategory.World
                ? eligible
                : eligible.Where(entry => entry.Category == category);
            var limit = category == PrestigeRankCategory.World ? WorldRankLimit : ClassRankLimit;
            var retained = candidates.Take(limit).ToArray();
            ranks[index] = Array.AsReadOnly(retained);
            foreach (var entry in retained)
            {
                retainedEntriesByUid.TryAdd(entry.UID, entry);
            }
        }
    }

    /// <summary>All validated rows, in saved order, including unranked rows.</summary>
    public IReadOnlyList<PrestigeRankingEntry> Entries { get; }

    public IReadOnlyList<PrestigeRankingEntry> WorldRanks => ranks[(int)PrestigeRankCategory.World];

    public PrestigeRankingEntry? BestOfTheWorld => WorldRanks.Count == 0 ? null : WorldRanks[0];

    public static PrestigeRankingSnapshot Create(IReadOnlyList<PrestigeRankEntry> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        // Parse every row, including ineligible and capped tails, before any
        // champion, rank list, or lookup can be published to a caller.
        return new PrestigeRankingSnapshot(PrestigeRankingRowProjection.ParseRows(rows));
    }

    public static bool TryCreate(
        IReadOnlyList<PrestigeRankEntry>? rows,
        [NotNullWhen(true)] out PrestigeRankingSnapshot? snapshot,
        out string? rejection)
    {
        snapshot = null;
        if (rows is null)
        {
            rejection = "The saved prestige rows are unavailable.";
            return false;
        }

        try
        {
            snapshot = Create(rows);
            rejection = null;
            return true;
        }
        catch (FormatException error)
        {
            rejection = error.Message;
            return false;
        }
    }

    public IReadOnlyList<PrestigeRankingEntry> GetRanks(PrestigeRankCategory category)
    {
        if ((uint)category >= CategoryCount)
        {
            throw new ArgumentOutOfRangeException(nameof(category));
        }

        return ranks[(int)category];
    }

    public uint GetMyRank(PrestigeRankCategory category, uint uid)
    {
        var entries = GetRanks(category);
        for (var index = 0; index < entries.Count; index++)
        {
            if (entries[index].UID == uid)
            {
                return (uint)(index + 1);
            }
        }

        return 0;
    }

    /// <summary>Matches the saved source's scan across retained ranking buckets.</summary>
    public PrestigeRankingEntry? GetInfo(uint uid)
        => retainedEntriesByUid.GetValueOrDefault(uid);
}
