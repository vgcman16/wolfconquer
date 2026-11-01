using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedDerivedSpellQueryWrite
{
    internal sealed class JournalOwner
    {
        private readonly LegacyPreparedDerivedSpellQueryWrite writer;
        private JournalOwner(LegacyPreparedDerivedSpellQueryWrite writer) => this.writer = writer;
        internal static JournalOwner Prepare(LegacyPreparedDerivedSpellQueryWrite writer)
        { if (!writer.BeforeOwnerIsCurrent()) throw Invalid(); return new(writer); }
        internal LegacyDerivedSpellQueryOperation Operation => writer.Operation;
        internal bool IsCurrent() => ReferenceEquals(writer.journalOwner, this) && writer.committing &&
            writer.committed is null && writer.BeforeOwnerIsCurrent();
        internal void RequireJournalTargets(string users, string items, string spells, IReadOnlyList<LegacyTradeJournalChange> changes)
        {
            if (!IsCurrent() || !SamePath(users, writer.binding.UsersRoot) || !SamePath(items, writer.itemsRoot) ||
                !SamePath(spells, writer.binding.SpellsRoot) || changes.Count != writer.changes.Length) throw Invalid();
            for (int index = 0; index < changes.Count; index++)
            {
                var expected = writer.changes[index]; var actual = changes[index];
                if (actual is null || actual.Kind != expected.Kind || actual.FileId != expected.FileId ||
                    !Same(actual.Before, expected.Before) || !Same(actual.After, expected.After)) throw Invalid();
            }
            if (!IsCurrent()) throw Invalid();
        }
        private static bool SamePath(string first, string second) => string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    internal sealed class Publication
    {
        private readonly LegacyPreparedDerivedSpellQueryWrite writer;
        private Publication(LegacyPreparedDerivedSpellQueryWrite writer) => this.writer = writer;
        internal static Publication Prepare(LegacyPreparedDerivedSpellQueryWrite writer)
        { if (!writer.BeforeOwnerIsCurrent()) throw Invalid(); return new(writer); }
        internal void RequireCurrent(WorldSessionStore store, ConquerTcpConnection connection, WorldSession expected, WorldSession proposed)
        {
            if (!ReferenceEquals(writer.store, store) || !ReferenceEquals(writer.binding.Connection, connection) ||
                !ReferenceEquals(writer.preparation.Expected, expected) || !writer.BeforeOwnerIsCurrent() ||
                !writer.ProposalMatches(proposed)) throw Invalid();
        }
        internal bool RequireCommittedPair() => writer.BeforeOwnerIsCurrent() && writer.ExactAfterFilesAreCurrent();
        internal bool IsCurrentPlain() => writer.BeforeOwnerIsCurrent();
    }

    internal Publication PreparePublication() => Publication.Prepare(this);
    private bool ProposalMatches(WorldSession proposed) =>
        (Proposed with { Character = proposed.Character, PlayerItems = proposed.PlayerItems,
            PlayerSpells = proposed.PlayerSpells, PlayerProficiencies = proposed.PlayerProficiencies }) == proposed &&
        ReferenceEquals(Proposed.Character.PlayerQuests, proposed.Character.PlayerQuests) && SameCharacter(Proposed.Character, proposed.Character) &&
        ReferenceEquals(preparation.Expected.PlayerItems, proposed.PlayerItems) && SameBook(Proposed.PlayerSpells, proposed.PlayerSpells) &&
        PlayerProficiencySnapshotLoader.ToLegacyBytes(Proposed.PlayerProficiencies).AsSpan()
            .SequenceEqual(PlayerProficiencySnapshotLoader.ToLegacyBytes(proposed.PlayerProficiencies));
    private static bool SameCharacter(CharacterSnapshot first, CharacterSnapshot second) =>
        (first with { Attributes = second.Attributes, PaidNobility = second.PaidNobility }) == second &&
        SameFields(first.Attributes, second.Attributes) && SameFields(first.PaidNobility, second.PaidNobility);
    private static bool SameFields(IReadOnlyDictionary<string, string> first, IReadOnlyDictionary<string, string> second)
    {
        var a = new LegacyNativeAttributeMapProof(first); var b = new LegacyNativeAttributeMapProof(second);
        return a.Fields.Count == b.Fields.Count && a.Fields.All(pair => b.Fields.TryGetValue(pair.Key, out var value) && value == pair.Value) &&
            a.IsCurrentPlain() && b.IsCurrentPlain();
    }
}
