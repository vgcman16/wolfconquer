using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // An empty token confers nothing. Only the actual held-attempt preparation
    // below registers calculated data in this private table.
    internal sealed class OwnedBasicPhysicalReceivePreparation
    {
        private OwnedBasicPhysicalReceiveData Data => ReadOwnedBasicPhysicalReceive(this);
        internal WorldPacketHandlerContext Context => Data.Context;
        internal MsgAttackPacket Packet => Data.Packet;
        internal LegacyOrdinarySpellBinding ActorBinding => Data.ActorBinding;
        internal LegacyOrdinarySpellBinding TargetBinding => Data.TargetBinding;
        internal WorldSession ExpectedActor => Data.ExpectedActor;
        internal WorldSession ExpectedTarget => Data.ExpectedTarget;
        internal WorldSession Actor => Data.Actor;
        internal WorldSession Target => Data.Target;
        internal BasicPhysicalReceiveDecision Decision => Data.Decision;
        internal MsgAttackPacket Response => Data.Response;
        internal ReadOnlyMemory<byte> InteractionFrame => Data.InteractionFrame;
        internal OwnedPhysicalDeathPreparation? Death => Data.Death;
        internal uint ProficiencyDamage => Data.ProficiencyDamage;
        internal IReadOnlyList<OwnedBasicPhysicalReceivePhase> Phases => Data.Phases;
        internal bool IsCurrentPlain() => OwnedBasicPhysicalReceives.TryGetValue(this, out var data) && data.IsCurrentPlain();
        internal bool CanonicalOutcomesMatch(WorldSession actor, WorldSession target) =>
            OwnedBasicPhysicalReceives.TryGetValue(this, out var data) && data.CanonicalOutcomesMatch(actor, target);
        internal bool CanonicalProgressionOutcomesMatch(WorldSession actor, WorldSession target,
            OwnedBasicPhysicalProficiencyPlan progression) => ReferenceEquals(progression.Hit, this) &&
            ReferenceEquals(progression.Before, ExpectedActor.PlayerProficiencies) &&
            ReferenceEquals(actor.PlayerProficiencies, progression.After) && progression.HasChanges &&
            OwnedBasicPhysicalReceives.TryGetValue(this, out var data) &&
            data.CanonicalOutcomesMatch(actor, target, progression.After);
    }

    internal enum OwnedBasicPhysicalReceiveAudience { Target, TargetScreen }
    internal sealed record OwnedBasicPhysicalReceivePhase(OwnedBasicPhysicalReceiveAudience Audience, ReadOnlyMemory<byte> Frame);

    private static readonly ConditionalWeakTable<OwnedBasicPhysicalReceivePreparation, OwnedBasicPhysicalReceiveData>
        OwnedBasicPhysicalReceives = new();
    private static OwnedBasicPhysicalReceiveData ReadOwnedBasicPhysicalReceive(OwnedBasicPhysicalReceivePreparation token) =>
        OwnedBasicPhysicalReceives.TryGetValue(token, out var data) ? data :
            throw new InvalidOperationException("ordinaryBasicPhysicalPreparationNotIssued");

    internal static bool TryPrepareOwnedBasicPhysicalReceive(WorldPacketHandlerContext context, MsgAttackPacket packet,
        WorldActiveSession selected, TeamSessionStateStore.OwnedBasicPhysicalPermission permission,
        out OwnedBasicPhysicalReceivePreparation? prepared, out string reason)
    {
        prepared = null; reason = "ordinaryBasicPhysicalPreparationOwnerChanged";
        if (!permission.IsPreparedFor(context, packet, selected) || context.Sessions is not { } store ||
            !store.TryReadOwnedBasicPhysicalPreparationOwners(context, packet, selected,
                out var actor, out var target, out var expectedActor, out var expectedTarget)) return false;
        try
        {
            var data = new OwnedBasicPhysicalReceiveData(context, packet, actor, target, expectedActor, expectedTarget, permission);
            if (!data.TryCalculate(out reason) || !data.IsCurrentPlain()) return false;
            var token = new OwnedBasicPhysicalReceivePreparation();
            OwnedBasicPhysicalReceives.Add(token, data);
            prepared = token; reason = string.Empty; return true;
        }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or OverflowException)
        {
            reason = "ordinaryBasicPhysicalPreparation:" + error.GetType().Name;
            return false;
        }
    }

    private sealed partial class OwnedBasicPhysicalReceiveData
    {
        internal readonly WorldPacketHandlerContext Context;
        internal readonly MsgAttackPacket Packet;
        internal readonly LegacyOrdinarySpellBinding ActorBinding, TargetBinding;
        internal readonly WorldSession ExpectedActor, ExpectedTarget;
        internal WorldSession Actor { get; private set; }
        internal WorldSession Target { get; private set; }
        internal BasicPhysicalReceiveDecision Decision { get; private set; } = null!;
        internal MsgAttackPacket Response { get; private set; }
        internal ReadOnlyMemory<byte> InteractionFrame { get; private set; }
        internal OwnedPhysicalDeathPreparation? Death { get; private set; }
        internal uint ProficiencyDamage { get; private set; }
        internal IReadOnlyList<OwnedBasicPhysicalReceivePhase> Phases { get; private set; } = Array.Empty<OwnedBasicPhysicalReceivePhase>();
        private readonly LegacyNativeAttributeMapProof actorAttributes, actorPaid, targetAttributes, targetPaid;
        private readonly LegacyPerformerSpellSource actorSpells, targetSpells;
        private readonly LegacyItemSaveGraph actorItems, targetItems;
        private readonly LegacyOwnedPhysicalMetadataWitness actorMetadata, targetMetadata;
        private readonly TeamSessionStateStore.OwnedBasicPhysicalPermission teamPermission;
        private LegacyNativeAttributeMapProof? actorAfterAttributes, actorAfterPaid, targetAfterAttributes, targetAfterPaid;

        internal OwnedBasicPhysicalReceiveData(WorldPacketHandlerContext context, MsgAttackPacket packet,
            LegacyOrdinarySpellBinding actor, LegacyOrdinarySpellBinding target, WorldSession expectedActor, WorldSession expectedTarget,
            TeamSessionStateStore.OwnedBasicPhysicalPermission permission)
        {
            Context = context; Packet = packet; ActorBinding = actor; TargetBinding = target;
            teamPermission = permission;
            modifierGateUtc = (context.TimeProvider ?? throw new InvalidOperationException("ordinaryBasicPhysicalClockOwnerMissing")).GetUtcNow();
            ExpectedActor = Actor = expectedActor; ExpectedTarget = Target = expectedTarget;
            actorAttributes = LegacyNativeAttributeMapProof.CaptureRaw(expectedActor.Character.Attributes);
            actorPaid = LegacyNativeAttributeMapProof.CaptureRaw(expectedActor.Character.PaidNobility);
            targetAttributes = LegacyNativeAttributeMapProof.CaptureRaw(expectedTarget.Character.Attributes);
            targetPaid = LegacyNativeAttributeMapProof.CaptureRaw(expectedTarget.Character.PaidNobility);
            actorSpells = new(expectedActor.PlayerSpells.Spells); targetSpells = new(expectedTarget.PlayerSpells.Spells);
            if (!LegacyItemSaveGraph.TryCapture(expectedActor.PlayerItems, out var own) || own is null ||
                !LegacyItemSaveGraph.TryCapture(expectedTarget.PlayerItems, out var peer) || peer is null)
                throw new InvalidDataException("ordinaryBasicPhysicalItemInputInvalid");
            actorItems = own; targetItems = peer;
            actorMetadata = LegacyOwnedPhysicalMetadataWitness.CaptureBasicPhysical(actor, context.WorldData, expectedActor);
            targetMetadata = LegacyOwnedPhysicalMetadataWitness.CaptureBasicPhysical(target, context.WorldData, expectedTarget);
        }

        internal bool TryCalculate(out string reason)
        {
            if (!CanOwnedBasicPhysicalDamageTarget(Context, ExpectedActor, ExpectedTarget, modifierGateUtc, out reason)) return false;
            var original = Packet.Query;
            var calculated = CalculateLegacyDirectPlayerPlayerPhysicalDamageResult(ExpectedActor, ExpectedTarget, Context.WorldData);
            ProficiencyDamage = calculated.Damage;
            if (!TryRequireApplicableOwnedPhysicalBranches(calculated.Damage, out reason)) return false;
            var displayed = CalculateLegacyDirectPlayerPhysicalDisplayedDamage(ExpectedActor, Context.WorldData, calculated.Damage);
            Response = new(original with { Damage = CheckedInt32(displayed, nameof(displayed)), KilledMonster = 0,
                ResponseDamage = original.ResponseDamage, Effect = calculated.Effect });
            InteractionFrame = Response.ToFrameBytes();

            var actor = WithCharacterAttribute(ExpectedActor.Character, RuntimeLegacyThundercloudSightAttribute,
                CheckedUInt32(ExpectedTarget.Character.Uid, nameof(ExpectedTarget.Character.Uid)));
            actor = WithCharacterAttribute(actor, "Action", 0U);
            actor = WithCharacterAttribute(actor, RuntimeLegacyAttackHitAttribute, "true");
            var target = WithCharacterAttribute(ExpectedTarget.Character, RuntimeLegacyThundercloudSightAttribute,
                CheckedUInt32(ExpectedActor.Character.Uid, nameof(ExpectedActor.Character.Uid)));
            uint damage = calculated.Damage;
            bool hawks = HasLegacyStatusFlag(target, LegacyHawksAmbitionFlag);
            if (hawks)
            {
                uint reduction = MulDivToUInt32(damage, hawksPower?.Power ??
                    throw new InvalidOperationException("ordinaryBasicPhysicalHawksCasterOwnerMissing"), 100);
                damage = reduction >= damage ? 0 : damage - reduction;
                target = WithoutLegacyStatusFlag(target, LegacyHawksAmbitionFlag);
            }
            uint previous = CheckedUInt32(ExpectedTarget.Character.MinHitPoints, nameof(ExpectedTarget.Character.MinHitPoints));
            uint applied = Math.Min(damage, previous);
            bool killed = previous != 0 && applied >= previous;
            Actor = ExpectedActor with { Character = new BoundHitpointsInputs(ExpectedActor.Character, actor).After };
            var receiveTarget = ExpectedTarget with { Character = new BoundHitpointsInputs(ExpectedTarget.Character, target).After };
            var result = new LegacyPlayerMagicDamageResult(new(TargetBinding.Connection, ExpectedTarget), receiveTarget,
                previous, applied, killed, HawksAmbitionConsumed: hawks, UpdatedAttackerSession: Actor, ReceiveDamage: damage);
            var receive = new LegacyPlayerMagicReceivePreparation(result, Actor.Character, RequiresDamageCommit: applied != 0,
                DeathX: killed ? CheckedUInt16(target.X, nameof(target.X)) : (ushort)0,
                DeathY: killed ? CheckedUInt16(target.Y, nameof(target.Y)) : (ushort)0);
            Decision = PrepareBasicPhysicalReceiveDecision(Context, receive)
                ?? throw new InvalidOperationException("ordinaryBasicPhysicalDecisionOwnerChanged");
            if (killed)
            {
                Death = PrepareOwnedPhysicalDeath(Context, Decision, ActorBinding, TargetBinding, ExpectedActor, ExpectedTarget);
                Target = Death.Proposed;
            }
            else Target = ProjectAssignedLegacyHitpoints(Context, receiveTarget, TargetBinding.Uid, checked((int)(previous - applied)));
            actorAfterAttributes = LegacyNativeAttributeMapProof.CaptureRaw(Actor.Character.Attributes);
            actorAfterPaid = LegacyNativeAttributeMapProof.CaptureRaw(Actor.Character.PaidNobility);
            targetAfterAttributes = LegacyNativeAttributeMapProof.CaptureRaw(Target.Character.Attributes);
            targetAfterPaid = LegacyNativeAttributeMapProof.CaptureRaw(Target.Character.PaidNobility);
            Phases = PrepareOwnedPhysicalReceivePhases(receiveTarget, Target, hawks, killed);
            reason = string.Empty; return true;
        }

        internal bool IsCurrentPlain() => Context.Sessions is { } store &&
            teamPermission.IsCurrentPlain() &&
            store.IsOwnedBasicPhysicalPreparationCurrent(Context, Packet, ActorBinding, TargetBinding, ExpectedActor, ExpectedTarget) &&
            actorAttributes.IsCurrentPlain() && actorPaid.IsCurrentPlain() && targetAttributes.IsCurrentPlain() && targetPaid.IsCurrentPlain() &&
            actorSpells.IsCurrent() && targetSpells.IsCurrent() && actorItems.IsCurrent(ExpectedActor.PlayerItems) &&
            targetItems.IsCurrent(ExpectedTarget.PlayerItems) && actorMetadata.IsCurrentPlain() && targetMetadata.IsCurrentPlain() &&
            (actorAfterAttributes is null || actorAfterAttributes.IsCurrentPlain()) && (actorAfterPaid is null || actorAfterPaid.IsCurrentPlain()) &&
            (targetAfterAttributes is null || targetAfterAttributes.IsCurrentPlain()) && (targetAfterPaid is null || targetAfterPaid.IsCurrentPlain()) &&
            (Death is null || Death.IsCurrentPlain(TargetBinding)) && CanonicalTargetsAreCurrentPlain() && ApplicableInputsAreCurrentPlain();
    }
}
