using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Defensive historical DATA. The actual prepared file writer separately proves
// physical before-images/current holders and privately issues its JournalOwner.
internal sealed record LegacyPhysicalDeathHistoryInputs(bool QueryAdmitted, bool Alternate,
    byte[] SourceItems, byte[] SourceSpells, IReadOnlyList<LegacyPhysicalDeathRankInput> RankingRows,
    IReadOnlyList<WorldPacketHandlers.OwnedPhysicalDeathDerivedSpellChange> DerivedSpells,
    IReadOnlyList<MagicType> GrantedRows);

public static partial class WorldPacketHandlers
{
    internal sealed partial class OwnedPhysicalDeathPreparation
    {
        internal bool TryCapturePhysicalDeathHistory(LegacyOrdinarySpellBinding binding,
            out LegacyPhysicalDeathHistoryInputs history)
        {
            history = null!;
            if (!IsCurrentPlain(binding)) return false;
            var ranks = query.Admitted && query.QuerySource.Character.Class / 1000 is 2 or 4 or 7 or 13 or 14
                ? (Context.WorldData ?? throw InvalidOwnedPhysicalDeath()).CombatGearRanks.Select(row =>
                    new LegacyPhysicalDeathRankInput(row.CharacterId, Array.AsReadOnly(row.Fields.ToArray()))).ToArray()
                : Array.Empty<LegacyPhysicalDeathRankInput>();
            var granted = metadata.CaptureDerivedSpellRows();
            var captured = new LegacyPhysicalDeathHistoryInputs(query.Admitted, query.QuerySource.AlternateEquipmentActive,
                PlayerItemSnapshotLoader.ToLegacyBytes(query.QuerySource.PlayerItems),
                PlayerSpellSnapshotLoader.ToLegacyBytes(query.QuerySource.PlayerSpells),
                Array.AsReadOnly(ranks), Array.AsReadOnly(derivedSpells.ToArray()), granted);
            if (!IsCurrentPlain(binding)) return false;
            history = captured;
            return true;
        }
    }
}
