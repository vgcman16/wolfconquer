using WolfConquer.Core.Data;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Core.World;

internal sealed partial class SqlOrdinaryAdmissionImagePlan
{
    // Parse private bytes on every read: mutable parsed graphs never become plan
    // state, and the exact original sidecar presence and bytes stay in Source.
    internal SqlOrdinaryAdmissionData ReadPreparedData()
    {
        var character = CharacterSnapshotParser.ParseText(
            CharacterSnapshotFileEncoding.Decode(After.Core.CopyCharacterBytes(), out _));
        var items = PlayerItemSnapshotLoader.Parse(After.Core.CopyItemBytes());
        var spells = new PlayerSpellSnapshot(new List<PlayerSpellSnapshotEntry>());
        var proficiencies = new PlayerProficiencySnapshot(new List<PlayerProficiencySnapshotEntry>());
        var quests = new PlayerQuestSnapshot(new List<PlayerQuestSnapshotEntry>());
        var gear = new CombatGearSnapshot(0, 0, 0, 0, 0,
            new CombatGearAirPowersSnapshot(0, 0, 0, 0, 0, 0, 0),
            new List<CombatGearSnapshotItem>(), new CombatGearBlackJadeBagSnapshot(0, new List<uint>()));

        foreach (var sidecar in After.Sidecars)
        {
            if (sidecar.Presence == SqlWorldStateSidecarPresence.Missing) continue;
            switch (sidecar.Kind)
            {
                case SqlWorldStateSidecarKind.Spells:
                    spells = PlayerSpellSnapshotLoader.Parse(sidecar.CopyBytes()); break;
                case SqlWorldStateSidecarKind.Proficiencies:
                    proficiencies = PlayerProficiencySnapshotLoader.Parse(sidecar.CopyBytes()); break;
                case SqlWorldStateSidecarKind.Quests:
                    quests = PlayerQuestSnapshotLoader.Parse(sidecar.CopyBytes()); break;
                case SqlWorldStateSidecarKind.CombatGear:
                    if (!LegacyTradeJournal.TryReadCompleteMailboxGear(sidecar.CopyBytes(), out var parsed) || parsed is null)
                        throw new InvalidDataException("sqlAdmissionCombatGearInvalid");
                    gear = parsed; break;
                default: throw new InvalidDataException("sqlAdmissionSidecarKindInvalid");
            }
        }
        return new(After, character with { PlayerQuests = quests }, items, spells, proficiencies, gear);
    }
}
