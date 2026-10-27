using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    private sealed class RankingChange
    {
        private readonly LegacyNativeNinja ninja;
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyNativePlayerRuntime player;
        private readonly uint job;
        private readonly PersistenceState original;
        private readonly PersistenceState candidate;
        private readonly bool needsItems;
        private readonly RankingItem[] originalRows;
        private readonly RankingItem[] candidateRows;
        private readonly Dictionary<uint, LegacyNativeNinjaFile.NinjaSprintItem>? table;
        private readonly RankingMetadata[] metadata;
        private readonly LegacyNativeNinjaRank.Entry? appearance;
        private readonly LegacyNativeEquipItemStorage? equipment;
        private readonly object? guild;
        private readonly object? vector;
        private readonly object? socket;
        private readonly bool fake;

        internal uint Uid { get; }
        internal LegacyNativeNinjaRank.Entry?[] Replacements { get; }
        internal LegacyNativePlayerRuntime Player => player;
        internal object StateGate => ninja.persistenceLoadGate;

        internal RankingChange(LegacyNativeNinja ninja, LegacyNativeGameClientRuntime client,
            LegacyNativePlayerRuntime player, uint uid, uint job, PersistenceState original, PersistenceState candidate,
            bool needsItems, RankingItem[] originalRows, RankingItem[] candidateRows,
            Dictionary<uint, LegacyNativeNinjaFile.NinjaSprintItem>? table, RankingMetadata[] metadata,
            LegacyNativeNinjaRank.Entry? appearance, LegacyNativeNinjaRank.Entry?[] replacements)
        {
            this.ninja = ninja; this.client = client; this.player = player;
            this.original = original; this.candidate = candidate; this.needsItems = needsItems;
            this.originalRows = originalRows; this.candidateRows = candidateRows;
            this.table = table; this.metadata = metadata; this.appearance = appearance;
            equipment = appearance is null ? null : client.Equipment;
            guild = appearance is null ? null : player.MyGuild;
            vector = appearance is null ? null : player.BitVector;
            socket = client.Socket; fake = client.Fake;
            Uid = uid; this.job = job; Replacements = replacements;
        }

        internal void RequireCurrent()
        {
            RequireCurrentPlain();
            if (appearance is not null &&
                !SameRankingAppearance(appearance, CaptureRankingAppearance(client, player))) throw UnavailableRanking();
            // Appearance flag membership can invoke a real dictionary comparer.
            // No such read follows this final plain owner/state/row fence.
            RequireCurrentPlain();
        }

        internal void RequireCurrentPlain()
        {
            if (!ninja.IsCombatOwnedBy(client) || !ReferenceEquals(client.Player, player) ||
                !ReferenceEquals(player.Owner, client) || player.UID != Uid || player.Class != job ||
                !ReferenceEquals(ninja.ReadPersistenceState(), original)) throw UnavailableRanking();
            if (needsItems)
            {
                RequireCurrentRankingItems(original.Items, originalRows);
                if (!ReferenceEquals(candidate, original)) RequireCurrentRankingItems(candidate.Items, candidateRows);
            }
            if (table is not null) RequireCurrentRankingMetadata(table, metadata);
            if (appearance is null) return;
            if (!ReferenceEquals(client.Equipment, equipment) || !ReferenceEquals(player.MyGuild, guild) ||
                !ReferenceEquals(player.BitVector, vector) || equipment is not null && !equipment.IsOwnedBy(client))
                throw UnavailableRanking();
        }

        // Called only after owner and rank final checks, while all gates remain held.
        internal void PublishCandidateState() => Volatile.Write(ref ninja.persistenceState, candidate);

        internal bool HasCurrentCommittedOwner()
        {
            if (!ninja.IsCombatOwnedBy(client) || !ReferenceEquals(client.Player, player) ||
                !ReferenceEquals(player.Owner, client) || player.UID != Uid || player.Class != job ||
                !ReferenceEquals(ninja.ReadPersistenceState(), candidate) ||
                !ReferenceEquals(client.Socket, socket) || client.Fake != fake) return false;
            try { RequireCurrentRankingItems(candidate.Items, candidateRows); return true; }
            catch (InvalidOperationException) { return false; }
        }
    }

    private static void RequireCurrentRankingItems(
        System.Collections.Concurrent.ConcurrentDictionary<uint, Item> items, RankingItem[] rows)
    {
        if (items is null || items.Count != rows.Length) throw UnavailableRanking();
        var ordinal = 0;
        foreach (var pair in items)
        {
            if (ordinal >= rows.Length) throw UnavailableRanking();
            var row = rows[ordinal++];
            if (pair.Key != row.Key || !ReferenceEquals(pair.Value, row.Row) || pair.Value.ItemID != row.Id ||
                pair.Value.Level != row.Level || pair.Value.Position != row.Position ||
                pair.Value.Exp != row.Exp || pair.Value.UK != row.UK) throw UnavailableRanking();
        }
        if (ordinal != rows.Length) throw UnavailableRanking();
    }

    private static void RequireCurrentRankingMetadata(
        Dictionary<uint, LegacyNativeNinjaFile.NinjaSprintItem> table, RankingMetadata[] rows)
    {
        if (!ReferenceEquals(LegacyNativeNinjaFile.gouyu_type, table) || table.Count != rows.Length)
            throw UnavailableRanking();
        var ordinal = 0;
        foreach (var pair in table)
        {
            if (ordinal >= rows.Length) throw UnavailableRanking();
            var row = rows[ordinal++];
            if (pair.Key != row.Key || !ReferenceEquals(pair.Value, row.Row) || pair.Value.ItemID != row.Id ||
                pair.Value.Level != row.Level || pair.Value.Score != row.Score) throw UnavailableRanking();
        }
        if (ordinal != rows.Length) throw UnavailableRanking();
    }
}
