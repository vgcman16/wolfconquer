using System.Globalization;
using System.Text.Json;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private sealed record HitProgressionJournalDomain(string Users, string Items, string Spells, string Proficiencies);

    private static JournalRoots GetHitProgressionRoots(string users, string items, string spells, string proficiencies)
    {
        var roots = GetCharacterItemsSpellsRoots(users, items, spells);
        if (string.IsNullOrWhiteSpace(proficiencies) || proficiencies != NormalizeQuestRoot(proficiencies))
            throw new ArgumentException("Hit progression Proficiencies root must be canonical.");
        EnsureSafePath(proficiencies);
        if (File.Exists(proficiencies) || new[] { roots.Users, roots.Items, roots.Spells!, roots.Journal }
                .Any(root => QuestRootsOverlap(root, proficiencies)))
            throw new ArgumentException("Hit progression roots overlap or are not directories.");
        RequireAdmissionPhysicalRoots([roots.Users, roots.Items, roots.Spells!, proficiencies]);
        return roots with { Proficiencies = proficiencies };
    }

    private static HitProgressionJournalDomain ReadHitProgressionDomain(JsonElement element)
    {
        var fields = GetProperties(element, "Users", "Items", "Spells", "Proficiencies");
        if (fields.Values.Any(value => value.ValueKind != JsonValueKind.String)) throw new JsonException("Invalid hit progression roots.");
        var values = new[] { "Users", "Items", "Spells", "Proficiencies" }.Select(name => fields[name].GetString()!).ToArray();
        if (values.Any(value => value.Length > 32768 || value != NormalizeQuestRoot(value)))
            throw new JsonException("Noncanonical hit progression root.");
        return new(values[0], values[1], values[2], values[3]);
    }

    private static JournalTarget[] GetHitProgressionTargets(JournalRoots roots, JournalRecord record)
    {
        ValidateRecordedHitProgressionImages(record);
        var domain = record.HitProgressionDomain;
        if (domain is null || roots.Spells is null || roots.Proficiencies is null ||
            !SameQuestRoot(roots.Users, domain.Users) || !SameQuestRoot(roots.Items, domain.Items) ||
            !SameQuestRoot(roots.Spells, domain.Spells) || !SameQuestRoot(roots.Proficiencies, domain.Proficiencies))
            throw new InvalidDataException("Hit progression differs from explicit configured roots.");
        var current = GetHitProgressionRoots(roots.Users, roots.Items, roots.Spells, roots.Proficiencies);
        var targets = record.Changes.Select(change => new JournalTarget(Path.Combine(change.Kind switch
        {
            LegacyTradeJournalFileKind.Character => current.Users,
            LegacyTradeJournalFileKind.Spells => current.Spells!,
            LegacyTradeJournalFileKind.Proficiencies => current.Proficiencies!,
            _ => throw new InvalidDataException("Unexpected hit progression target."),
        }, change.FileId.ToString(CultureInfo.InvariantCulture) +
            (change.Kind == LegacyTradeJournalFileKind.Character ? ".ini" : ".bin")), change)).ToArray();
        if (targets.Select(target => target.Path).Distinct(JournalDomainComparer).Count() != targets.Length)
            throw new InvalidDataException("Duplicate hit progression target.");
        foreach (var target in targets) EnsureSafePath(target.Path);
        return targets;
    }

    private static string[] HitProgressionReferencePaths(JournalRoots roots, JournalRecord record)
    {
        var operation = record.PhysicalHitProgression!;
        var actor = Path.Combine(roots.Users, operation.ActorAccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        var items = Path.Combine(roots.Items, operation.ActorUid.ToString(CultureInfo.InvariantCulture) + ".bin");
        return operation.Death is null ? [actor, items] :
            [actor, items, Path.Combine(roots.Items, operation.TargetUid.ToString(CultureInfo.InvariantCulture) + ".bin")];
    }

    private static void PrepareHitProgressionDirectories(JournalRoots roots, JournalRecord record, JournalTarget[] targets)
    {
        RequireHitProgressionTargetsCurrent(roots, record, targets);
        foreach (var target in targets)
        {
            var directory = Path.GetDirectoryName(target.Path)!;
            EnsureSafePath(directory); Directory.CreateDirectory(directory);
        }
        RequireHitProgressionTargetsCurrent(roots, record, targets);
    }

    private static void RequireHitProgressionTargetsCurrent(JournalRoots roots, JournalRecord record, JournalTarget[] targets)
    {
        var current = GetHitProgressionTargets(roots, record);
        if (targets.Length != current.Length || targets.Where((target, index) =>
                !JournalDomainComparer.Equals(target.Path, current[index].Path) || !ReferenceEquals(target.Change, current[index].Change)).Any())
            throw new InvalidDataException("Hit progression configured targets changed.");
        RequireHitProgressionReferencesCurrent(roots, record);
        EnsureTargetsMatch(current, allowAfter: true);
    }
}
