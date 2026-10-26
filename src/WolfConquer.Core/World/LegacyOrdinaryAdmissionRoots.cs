using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Explicit configured storage routes, never a source of gameplay/UID authority.
internal sealed record LegacyOrdinaryAdmissionRoots(string Users, string? Items, string? Spells,
    string? Proficiencies, string? Quests, string? CombatGear)
{
    internal static LegacyOrdinaryAdmissionRoots FromLoader(CharacterSnapshotLoader loader) => new(
        Normalize(loader.UsersRoot)!, Normalize(loader.PlayerItemsRoot), Normalize(loader.PlayerSpellsRoot),
        Normalize(loader.PlayerProficienciesRoot), Normalize(loader.PlayerQuestsRoot), Normalize(loader.PlayersCombatGearRoot));

    private static string? Normalize(string? path) => string.IsNullOrWhiteSpace(path) ? null :
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    internal string? Root(LegacyTradeJournalFileKind kind) => kind switch
    {
        LegacyTradeJournalFileKind.Character => Users,
        LegacyTradeJournalFileKind.Items => Items,
        LegacyTradeJournalFileKind.Spells => Spells,
        LegacyTradeJournalFileKind.Proficiencies => Proficiencies,
        LegacyTradeJournalFileKind.Quests => Quests,
        LegacyTradeJournalFileKind.CombatGear => CombatGear,
        _ => null,
    };

    internal IEnumerable<LegacyTradeJournalFileKind> Kinds => Enum.GetValues<LegacyTradeJournalFileKind>()
        .Where(kind => Root(kind) is not null);

    internal string PathFor(LegacyTradeJournalFileKind kind, long uid) => Path.Combine(
        Root(kind) ?? throw new InvalidDataException("Required admission storage is not configured."),
        uid.ToString(System.Globalization.CultureInfo.InvariantCulture) +
        (kind is LegacyTradeJournalFileKind.Character or LegacyTradeJournalFileKind.CombatGear ? ".ini" : ".bin"));
}

internal sealed record LegacyOrdinaryAdmissionInput(LegacyTradeJournalFileKind Kind, long FileId, byte[]? Image);
