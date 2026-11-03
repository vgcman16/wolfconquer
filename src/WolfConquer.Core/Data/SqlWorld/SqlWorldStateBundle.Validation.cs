using WolfConquer.Core.World;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldStateBundle
{
    private static bool TryValidateSidecar(SqlWorldStateSidecar sidecar, out SqlWorldStateBundleFailure failure)
    {
        failure = InvalidPayload(sidecar.Kind);
        if (sidecar.Presence == SqlWorldStateSidecarPresence.Missing)
        { failure = SqlWorldStateBundleFailure.None; return true; }
        try
        {
            switch (sidecar.Kind)
            {
                case SqlWorldStateSidecarKind.Spells:
                    var spells = PlayerSpellSnapshotLoader.Parse(sidecar.Bytes);
                    var spellIds = new HashSet<ushort>();
                    foreach (var row in spells.Spells)
                        if (row.Id == 0 || row.Experience < 0 || !spellIds.Add(row.Id)) return false;
                    break;
                case SqlWorldStateSidecarKind.Proficiencies:
                    var proficiencies = PlayerProficiencySnapshotLoader.Parse(sidecar.Bytes);
                    var proficiencyIds = new HashSet<uint>();
                    foreach (var row in proficiencies.Proficiencies)
                        if (!proficiencyIds.Add(row.Id)) return false;
                    break;
                case SqlWorldStateSidecarKind.Quests:
                    _ = PlayerQuestSnapshotLoader.Parse(sidecar.Bytes);
                    break;
                case SqlWorldStateSidecarKind.CombatGear:
                    if (!LegacyTradeJournal.TryReadCompleteMailboxGear(sidecar.CopyBytes(), out var gear) || gear is null)
                        return false;
                    break;
                default: return false;
            }
        }
        catch (Exception error) when (InvalidSidecar(error)) { return false; }
        failure = SqlWorldStateBundleFailure.None;
        return true;
    }

    private static SqlWorldStateBundleFailure InvalidPayload(SqlWorldStateSidecarKind kind) => kind switch
    {
        SqlWorldStateSidecarKind.Spells => SqlWorldStateBundleFailure.InvalidSpells,
        SqlWorldStateSidecarKind.Proficiencies => SqlWorldStateBundleFailure.InvalidProficiencies,
        SqlWorldStateSidecarKind.Quests => SqlWorldStateBundleFailure.InvalidQuests,
        SqlWorldStateSidecarKind.CombatGear => SqlWorldStateBundleFailure.InvalidCombatGear,
        _ => SqlWorldStateBundleFailure.InvalidKind,
    };

    private static bool InvalidSidecar(Exception error) => error is FormatException or InvalidDataException or
        ArgumentException or InvalidOperationException or OverflowException or KeyNotFoundException or IndexOutOfRangeException;
}
