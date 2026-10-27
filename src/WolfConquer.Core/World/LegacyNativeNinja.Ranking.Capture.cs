using System.Collections.Concurrent;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    private sealed record RankingItem(uint Key, Item Row, uint Id, uint Level, uint Position, uint Exp, uint UK);
    private sealed record RankingMetadata(uint Key, LegacyNativeNinjaFile.NinjaSprintItem Row,
        uint Id, uint Level, uint Score);

    private RankingChange CaptureRankingChange(LegacyNativeGameClientRuntime client,
        LegacyNativePlayerRuntime player, Item? added)
    {
        var uid = player.UID;
        var job = player.Class;
        if (!IsCombatOwnedBy(client) || !ReferenceEquals(client.Player, player) ||
            !ReferenceEquals(player.Owner, client) || uid == 0 || !KnownRankingClass(job))
            throw UnavailableRanking();
        var original = ReadPersistenceState();
        var ninjaClass = LegacyNativeAtributesStatus.IsNinja(job);
        var needsItems = ninjaClass || added is not null;
        var originalRows = needsItems ? CaptureRankingItems(original.Items) : [];
        var candidate = original;
        if (added is not null)
        {
            var items = PrepareRankingItemMap(original.Items, originalRows, added);
            candidate = original with { Items = items };
        }
        var rows = added is null ? originalRows : CaptureRankingItems(candidate.Items);
        var metadata = CaptureRankingMetadata(rows, added);
        var scores = ninjaClass ? PrepareRankingScores(candidate, rows, metadata.Rows) : new uint[6];
        LegacyNativeNinjaRank.Entry? appearance = ninjaClass && scores[0] >= 1000
            ? CaptureRankingAppearance(client, player) : null;
        var replacements = PrepareRankingEntries(appearance, scores);
        var change = new RankingChange(this, client, player, uid, job, original, candidate, needsItems,
            originalRows, rows, metadata.Table, metadata.Rows, appearance, replacements);
        change.RequireCurrent();
        return change;
    }

    private static bool KnownRankingClass(uint job) =>
        LegacyNativeAtributesStatus.IsTrojan(job) || LegacyNativeAtributesStatus.IsWarrior(job) ||
        LegacyNativeAtributesStatus.IsArcher(job) || LegacyNativeAtributesStatus.IsNinja(job) ||
        LegacyNativeAtributesStatus.IsMonk(job) || LegacyNativeAtributesStatus.IsPirate(job) ||
        LegacyNativeAtributesStatus.IsLee(job) || LegacyNativeAtributesStatus.IsThunderStriker(job) ||
        LegacyNativeAtributesStatus.IsWater(job) || LegacyNativeAtributesStatus.IsFire(job) ||
        LegacyNativeAtributesStatus.IsWindWalker(job) || job is 10000 or 10001;

    private static RankingItem[] CaptureRankingItems(ConcurrentDictionary<uint, Item>? items)
    {
        if (items is null || items.Count > MaximumEquippedItemRows) throw UnavailableRanking();
        var rows = new List<RankingItem>();
        var ids = new HashSet<uint>();
        var owners = new HashSet<Item>(ReferenceEqualityComparer.Instance);
        foreach (var pair in items)
        {
            if (pair.Value is not { } row || rows.Count >= MaximumEquippedItemRows || pair.Key == 0 ||
                pair.Key != row.ItemID || !ids.Add(row.ItemID) || !owners.Add(row)) throw UnavailableRanking();
            rows.Add(new(pair.Key, row, row.ItemID, row.Level, row.Position, row.Exp, row.UK));
        }
        return rows.ToArray();
    }

    private static (Dictionary<uint, LegacyNativeNinjaFile.NinjaSprintItem>? Table, RankingMetadata[] Rows)
        CaptureRankingMetadata(RankingItem[] items, Item? added)
    {
        if (added is null && !items.Any(row => RankingScoreGroup(row.Id) >= 0)) return (null, []);
        var table = LegacyNativeNinjaFile.gouyu_type ?? throw UnavailableRanking();
        if (table.Count > MaximumEquippedMetadataRows) throw UnavailableRanking();
        var rows = new List<RankingMetadata>();
        foreach (var pair in table)
        {
            if (pair.Value is not { } row || rows.Count >= MaximumEquippedMetadataRows) throw UnavailableRanking();
            rows.Add(new(pair.Key, row, row.ItemID, row.Level, row.Score));
        }
        var snapshot = rows.ToArray();
        foreach (var item in items)
            if (RankingScoreGroup(item.Id) >= 0 || ReferenceEquals(item.Row, added))
                _ = MatchingRankingMetadata(snapshot, item.Id, item.Level);
        return (table, snapshot);
    }
}
