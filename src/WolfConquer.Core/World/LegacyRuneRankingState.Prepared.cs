using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class LegacyRuneRankingState
{
    [ThreadStatic] private static int _rankEntryDepth;
    internal static bool IsCurrentThreadInsideLease => _rankEntryDepth != 0;

    private static List<LegacyRuneRankingEntry> PrepareCandidate(List<LegacyRuneRankingEntry> original, LegacyRuneRankingEntry entry)
    {
        var next = new List<LegacyRuneRankingEntry>(original);
        var index = next.FindIndex(candidate => candidate.CharacterUid == entry.CharacterUid);
        if (index < 0) next.Add(entry); else next[index] = entry;
        return Normalize(next);
    }

    // Must be acquired as a leaf after the actual Store/actor gates. The
    // candidate is complete before the durable writer; Commit is a plain store.
    internal PreparedRuneRankLease PrepareUpdate(CharacterSnapshot character, PlayerItemSnapshot items)
    {
        ArgumentNullException.ThrowIfNull(character); ArgumentNullException.ThrowIfNull(items);
        if (IsCurrentThreadInsideLease || Monitor.IsEntered(_gate)) throw new InvalidOperationException("Rune rank lease cannot reenter its gate.");
        _rankEntryDepth = 1;
        var held = false;
        try
        {
            var entry = new LegacyRuneRankingEntry(character.Uid, character.Name, LegacyRuneStoragePacket.ComputeStorageScore(items));
            Monitor.Enter(_gate, ref held);
            return new PreparedRuneRankLease(this, _entries, PrepareCandidate(_entries, entry));
        }
        catch { if (held) Monitor.Exit(_gate); _rankEntryDepth = 0; throw; }
    }

    internal sealed class PreparedRuneRankLease : IDisposable
    {
        private readonly LegacyRuneRankingState owner;
        private readonly List<LegacyRuneRankingEntry> original, candidate;
        private bool published, disposed;
        internal PreparedRuneRankLease(LegacyRuneRankingState owner, List<LegacyRuneRankingEntry> original, List<LegacyRuneRankingEntry> candidate)
        { this.owner = owner; this.original = original; this.candidate = candidate; }
        internal bool IsCurrentPlain() => !disposed && !published && Monitor.IsEntered(owner._gate) && ReferenceEquals(owner._entries, original);
        internal void Publish()
        {
            if (!IsCurrentPlain()) throw new InvalidOperationException("Rune rank lease is no longer current.");
            owner._entries = candidate; published = true;
        }
        public void Dispose()
        {
            if (disposed) return;
            if (!Monitor.IsEntered(owner._gate)) throw new InvalidOperationException("Rune rank lease released by the wrong thread.");
            disposed = true; Monitor.Exit(owner._gate); _rankEntryDepth = 0;
        }
    }
}
