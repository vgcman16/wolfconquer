using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Feasibility DATA retained by the real detached admission plan. This has
    // no binding, epoch, mutation permit, completion or output capability.
    internal sealed class InitialWorldEntryQueryData
    {
        private readonly WorldSession candidate;
        private readonly WorldDataCatalog? catalog;
        private readonly ILegacySignedMillisecondClock clock;
        private readonly LegacyOwnedPhysicalMetadataWitness.AdmissionQueryData metadata;
        private readonly PlayerSpellSnapshot projectedSpells;
        internal bool RequiresDerivedSpellWrite { get; }

        private InitialWorldEntryQueryData(WorldSession candidate, WorldDataCatalog? catalog, ILegacySignedMillisecondClock clock)
        {
            this.candidate = candidate; this.catalog = catalog; this.clock = clock;
            if (HasLegacyPlayerMagicInterserverAttribute(candidate))
                throw new InvalidOperationException("initialQueryDataRequiresInterserverHistoryOwner");
            metadata = LegacyOwnedPhysicalMetadataWitness.AdmissionQueryData.Capture(candidate, catalog);
            PreflightEquipmentQueryDataCore(candidate, catalog, clock, null, metadata, null, LegacyEquipmentRebuildReason.InitialWorldEntry);
            var derived = ProjectAttributeDerivedSpells(candidate, catalog);
            projectedSpells = derived.Spells;
            RequiresDerivedSpellWrite = derived.Changes.Count != 0;
            PreflightDerivedSpellFrames(checked((uint)candidate.Character.Uid), derived, null, metadata);
            _ = LegacyShowEquipmentPacket.CreateEquipmentRefresh(candidate.PlayerItems, candidate.AlternateEquipmentActive).ToFrameBytes();
            if (!IsCurrentFor(candidate, catalog)) throw new InvalidOperationException("initialQueryDataChanged");
        }

        internal static bool TryPrepare(WorldSession candidate, WorldDataCatalog? catalog, ILegacySignedMillisecondClock clock,
            out InitialWorldEntryQueryData? proof)
        {
            proof = null;
            try { proof = new(candidate, catalog, clock); return true; }
            catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or
                OverflowException or FormatException or NotSupportedException) { return false; }
        }

        internal bool IsCurrentFor(WorldSession actual, WorldDataCatalog? actualCatalog) =>
            ReferenceEquals(candidate, actual) && ReferenceEquals(catalog, actualCatalog) && metadata.IsCurrentFor(actual, actualCatalog) &&
            LegacyEquipmentRebuildAdmission.ApplyCurrent(candidate, LegacyEquipmentRebuildReason.InitialWorldEntry, clock).Admitted &&
            metadata.IsCurrentFor(actual, actualCatalog);

        internal bool PreservesLoginPublication(LegacySpellPublicationPlan plan) => IsCurrentFor(candidate, catalog) &&
            plan.TryPrepareQuerySuccessor(candidate.PlayerSpells, projectedSpells, out _) && IsCurrentFor(candidate, catalog);
    }
}
