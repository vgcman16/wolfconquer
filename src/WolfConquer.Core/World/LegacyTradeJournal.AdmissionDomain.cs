using System.Text.Json;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static JournalRoots GetAdmissionRoots(LegacyOrdinaryAdmissionRoots configured)
    {
        var users = NormalizeQuestRoot(configured.Users);
        if (!Directory.Exists(users)) throw new IOException("Admission Users root is missing.");
        var paths = configured.Kinds.Select(kind => configured.Root(kind)!).ToArray();
        foreach (var path in paths)
        {
            if (NormalizeQuestRoot(path) != path) throw new ArgumentException("Admission roots must be canonical absolute paths.");
            EnsureSafePath(path);
            if (File.Exists(path)) throw new IOException("An admission root is a file.");
        }
        for (var index = 0; index < paths.Length; index++)
            for (var other = index + 1; other < paths.Length; other++)
                if (QuestRootsOverlap(paths[index], paths[other])) throw new ArgumentException("Admission roots overlap.");
        RequireAdmissionPhysicalRoots(paths);
        var journal = Path.Combine(users, JournalDirectoryName);
        EnsureSafePath(journal);
        if (paths.Skip(1).Any(path => QuestRootsOverlap(path, journal)))
            throw new ArgumentException("Admission roots overlap the shared journal.");
        return new(users, configured.Items ?? "", journal) { Admission = configured };
    }

    private static void RequireAdmissionPhysicalRoots(IEnumerable<string> roots)
    {
        var existing = roots.Where(Directory.Exists).ToArray();
        for (var index = 0; index < existing.Length; index++)
            for (var other = index + 1; other < existing.Length; other++)
            {
                using var pins = WindowsJournalRootIdentity.CapturePair(existing[index], existing[other]);
                RequireDisjoint(pins.Inspect(WindowsJournalRootIdentity.RootRole.Users, WindowsJournalRootIdentity.RootRole.Items));
            }
    }

    private static LegacyOrdinaryAdmissionRoots ReadAdmissionDomain(JsonElement element)
    {
        var fields = GetProperties(element, "Users", "Items", "Spells", "Proficiencies", "Quests", "CombatGear");
        string? Read(string name)
        {
            var field = fields[name];
            return field.ValueKind == JsonValueKind.Null ? null : field.ValueKind == JsonValueKind.String
                ? field.GetString() : throw new JsonException("Invalid admission root.");
        }
        return new(Read("Users") ?? throw new JsonException("Missing Users root."), Read("Items"), Read("Spells"),
            Read("Proficiencies"), Read("Quests"), Read("CombatGear"));
    }

    private static JournalTarget[] GetAdmissionTargets(JournalRoots roots, JournalRecord record)
    {
        var domain = roots.Admission ?? throw new ArgumentException("No configured admission domain.");
        var retained = record.AdmissionDomain ?? throw new ArgumentException("No retained admission domain.");
        foreach (var kind in Enum.GetValues<LegacyTradeJournalFileKind>())
        {
            var expected = domain.Root(kind); var actual = retained.Root(kind);
            if (expected is null ? actual is not null : actual is null || !SameQuestRoot(expected, actual))
                throw new ArgumentException("Admission journal differs from explicit configuration.");
        }
        var inputs = record.AdmissionInputs ?? throw new ArgumentException("Missing admission input membership.");
        var all = record.Changes.Select(change => (change.Kind, change.FileId))
            .Concat(inputs.Select(input => (input.Kind, input.FileId))).ToArray();
        var id = record.Changes.Single(change => change.Kind == LegacyTradeJournalFileKind.Character).FileId;
        if (all.Any(row => row.FileId != id) || all.Select(row => row.Kind).Distinct().Count() != all.Length ||
            !all.Select(row => row.Kind).Order().SequenceEqual(domain.Kinds.Order()))
            throw new ArgumentException("Admission image/input membership is incomplete.");
        if (inputs.Any(input => input.Image?.Length > MaxTargetBytes)) throw new ArgumentException("Admission input is too large.");
        if (record.Version == 7) ValidatePreservedAdmission(record);
        foreach (var input in inputs)
        {
            if (input.Image is null) continue;
            if (input.Kind == LegacyTradeJournalFileKind.Quests) _ = PlayerQuestSnapshotLoader.Parse(input.Image);
            if (input.Kind == LegacyTradeJournalFileKind.CombatGear) ValidateMailboxGearImage(input.Image);
        }
        var targets = record.Changes.Select(change => new JournalTarget(domain.PathFor(change.Kind, id), change)).ToArray();
        foreach (var path in AdmissionLockPaths(roots, record, targets)) EnsureSafePath(path);
        return targets;
    }

    private static string[] AdmissionLockPaths(JournalRoots roots, JournalRecord record, JournalTarget[] targets) =>
        !IsOrdinaryAdmissionVersion(record.Version) ? targets.Select(target => target.Path).ToArray() : targets.Select(target => target.Path)
            .Concat(record.AdmissionInputs!.Select(input => roots.Admission!.PathFor(input.Kind, input.FileId))).ToArray();

    private static void EnsureAdmissionInputs(JournalRoots roots, JournalRecord record)
    {
        if (!IsOrdinaryAdmissionVersion(record.Version)) return;
        foreach (var input in record.AdmissionInputs!)
            if (!Matches(ReadTarget(roots.Admission!.PathFor(input.Kind, input.FileId)), input.Image))
                throw new IOException("Required admission input changed.");
    }

    private static void PrepareAdmissionDirectories(JournalRoots roots, JournalRecord record, JournalTarget[] targets)
    {
        if (!IsOrdinaryAdmissionVersion(record.Version)) return;
        foreach (var target in targets) Directory.CreateDirectory(Path.GetDirectoryName(target.Path)!);
        _ = GetAdmissionRoots(roots.Admission!); // Recheck actual new directory identities before staging bytes.
    }
}
