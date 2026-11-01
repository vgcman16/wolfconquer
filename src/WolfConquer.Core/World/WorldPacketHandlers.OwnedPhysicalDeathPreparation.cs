using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Calculated data only. Native publication consumes it only through the
    // actual decision-bound preparation, never from this record alone.
    internal sealed record OwnedPhysicalDeathQueryValues(bool Admitted, WorldSession QuerySource,
        LegacyNativeMagicQueryFields Fields, LegacyNativeMagicQueryFields JiangHu,
        uint PerfectionPrestige, LegacyNativePerfectionSnapshot Perfection);

    internal sealed partial class OwnedPhysicalDeathPreparation
    {
        internal WorldPacketHandlerContext Context { get; }
        internal BasicPhysicalReceiveDecision? Decision { get; }
        internal WorldServerFrameHandler.BasicPhysicalAttempt? Attempt { get; }
        internal WorldSessionStore.OwnedDragonhowlDeath? Dragonhowl { get; }
        internal LegacyOrdinarySpellBinding ActorBinding { get; }
        internal LegacyOrdinarySpellBinding TargetBinding { get; }
        private readonly WorldSession expectedActor, expectedTarget;
        internal WorldSession ExpectedActor => Dragonhowl?.ExpectedActor ?? expectedActor;
        internal WorldSession ExpectedTarget => Dragonhowl?.ExpectedTarget ?? expectedTarget;
        internal WorldSession Proposed { get; }
        private readonly OwnedPhysicalDeathQueryValues query;
        private readonly LegacyOwnedPhysicalMetadataWitness metadata;
        private readonly LegacyNativeAttributeMapProof actorAttributes, actorPaid, targetAttributes, targetPaid;
        private readonly LegacyNativeAttributeMapProof proposedAttributes, proposedPaid, queryAttributes, queryPaid;
        private readonly LegacyPerformerSpellSource beforeSpells, proposedSpells;
        private readonly LegacyItemSaveGraph beforeItems;
        private readonly List<CanonicalDeathTarget> canonicalTargets = [];

        // Only the real normal-death builder in this containing type creates
        // this plan after complete phase calculation. No public recipe factory.
        private OwnedPhysicalDeathPreparation(WorldPacketHandlerContext context, BasicPhysicalReceiveDecision? decision,
            WorldServerFrameHandler.BasicPhysicalAttempt? attempt, LegacyOrdinarySpellBinding actorBinding,
            LegacyOrdinarySpellBinding targetBinding, WorldSession actor, WorldSession target,
            WorldSession proposed, OwnedPhysicalDeathQueryValues query, OwnedDeathNativeStatus nativeStatus,
            LegacyOwnedPhysicalMetadataWitness metadata, WorldSessionStore.OwnedDragonhowlDeath? dragonhowl = null)
        {
            if (dragonhowl is null ? decision is null || attempt is null : decision is not null || attempt is not null)
                throw InvalidOwnedPhysicalDeath();
            Context = context; Decision = decision; Attempt = attempt;
            Dragonhowl = dragonhowl;
            ActorBinding = actorBinding; TargetBinding = targetBinding;
            expectedActor = actor; expectedTarget = target;
            Proposed = proposed with { Character = new BoundHitpointsInputs(target.Character, proposed.Character).After };
            this.query = query; this.nativeStatus = nativeStatus; this.metadata = metadata;
            actorAttributes = LegacyNativeAttributeMapProof.CaptureRaw(actor.Character.Attributes);
            actorPaid = LegacyNativeAttributeMapProof.CaptureRaw(actor.Character.PaidNobility);
            targetAttributes = LegacyNativeAttributeMapProof.CaptureRaw(target.Character.Attributes);
            targetPaid = LegacyNativeAttributeMapProof.CaptureRaw(target.Character.PaidNobility);
            proposedAttributes = LegacyNativeAttributeMapProof.CaptureRaw(Proposed.Character.Attributes);
            proposedPaid = LegacyNativeAttributeMapProof.CaptureRaw(Proposed.Character.PaidNobility);
            queryAttributes = LegacyNativeAttributeMapProof.CaptureRaw(query.QuerySource.Character.Attributes);
            queryPaid = LegacyNativeAttributeMapProof.CaptureRaw(query.QuerySource.Character.PaidNobility);
            beforeSpells = new(target.PlayerSpells.Spells); proposedSpells = new(Proposed.PlayerSpells.Spells);
            if (!LegacyItemSaveGraph.TryCapture(target.PlayerItems, out var graph) || graph is null)
                throw InvalidOwnedPhysicalDeath();
            beforeItems = graph;
            if (!IsCurrentPlain(targetBinding)) throw InvalidOwnedPhysicalDeath();
        }

        // This before-owner proof remains valid after file commitment while
        // both holders/native participants are still unpublished. It does no
        // file I/O and intentionally stops being valid after live publication.
        internal bool IsCurrentPlain(LegacyOrdinarySpellBinding binding)
        {
            if (!ReferenceEquals(binding, TargetBinding) ||
                !binding.Store.IsOwnedPhysicalDeathPlanCurrent(this) ||
                !actorAttributes.IsCurrentPlain() || !actorPaid.IsCurrentPlain() ||
                !targetAttributes.IsCurrentPlain() || !targetPaid.IsCurrentPlain() ||
                !proposedAttributes.IsCurrentPlain() || !proposedPaid.IsCurrentPlain() ||
                !queryAttributes.IsCurrentPlain() || !queryPaid.IsCurrentPlain() ||
                !beforeSpells.IsCurrent() || !proposedSpells.IsCurrent() || !beforeItems.IsCurrent(ExpectedTarget.PlayerItems) ||
                !nativeStatus.IsCurrentPlain() || !metadata.IsCurrentPlain())
                return false;
            for (int index = 0; index < canonicalTargets.Count; index++)
                if (!canonicalTargets[index].IsCurrentPlain()) return false;
            return true;
        }

        internal bool TryReadNativeQueryProposal(LegacyOrdinarySpellBinding binding, WorldSession canonicalTarget,
            out OwnedPhysicalDeathQueryValues values)
        {
            values = null!;
            if (!IsCurrentPlain(binding) || !CanonicalTargetMatches(canonicalTarget)) return false;
            if (!canonicalTargets.Any(proof => ReferenceEquals(proof.Target, canonicalTarget)))
                canonicalTargets.Add(new(canonicalTarget));
            if (!IsCurrentPlain(binding)) return false;
            values = query;
            return true;
        }

        private static InvalidOperationException InvalidOwnedPhysicalDeath() =>
            new("ordinaryPhysicalDeathPreparationChanged");
    }
}
