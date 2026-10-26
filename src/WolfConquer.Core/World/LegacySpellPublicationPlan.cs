using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Immutable learned-set/serialization capture; preparation has no save, grant,
// Store, transport or client effects. Selection changes only which rows are sent.
internal sealed class LegacySpellPublicationPlan
{
    internal PlayerSpellSnapshot Source { get; }
    internal PlayerSpellSnapshotEntry[] Entries { get; }
    internal byte[][] Frames { get; }
    private LegacySpellPublicationPlan(PlayerSpellSnapshot source, PlayerSpellSnapshotEntry[] entries, byte[][] frames)
    { Source = source; Entries = entries; Frames = frames; }

    internal static bool TryPrepare(PlayerSpellSnapshot source, IReadOnlySet<ushort>? selection,
        out LegacySpellPublicationPlan? plan)
    {
        plan = null;
        if (source?.Spells is not { } rows || rows.Count is < 0 or > ushort.MaxValue) return false;
        try
        {
            var captured = rows.ToArray();
            if (captured.Length != rows.Count || captured.Any(row => row is null || row.Id == 0 || row.Experience < 0) ||
                captured.Select(row => row.Id).Distinct().Count() != captured.Length) return false;
            if (selection is not null && selection.Any(id => !captured.Any(row => row.Id == id))) return false;
            var frames = captured.Where(row => selection is null || selection.Contains(row.Id))
                .Select(row => LegacySpellPacket.FromSnapshot(row).ToFrameBytes()).ToArray();
            plan = new(source, captured, frames);
            return true;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        { return false; }
    }

    internal bool Matches(PlayerSpellSnapshot current)
    {
        if (!ReferenceEquals(current, Source)) return false;
        try { return current.Spells.Count == Entries.Length && current.Spells.SequenceEqual(Entries); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException) { return false; }
    }
}
