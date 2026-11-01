using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// One actual admitted pair, not a general two-file saver or an operation DTO
// accepting permission. File capture and journal use retain these same owners.
internal sealed partial class LegacyPreparedPairMovementWrite
{
    internal enum Outcome { Rejected, Committed, RecoveryRequired }
    internal WorldServerFrameHandler Frame { get; }
    internal WorldSessionStore Store { get; }
    internal WorldPacketHandlerContext Context { get; }
    internal LegacyPairMovementOperation Operation { get; }
    internal LegacyOrdinarySpellBinding ActorBinding { get; }
    internal LegacyOrdinarySpellBinding PartnerBinding { get; }
    internal WorldSession ActorBefore { get; }
    internal WorldSession PartnerBefore { get; }
    internal WorldSession ActorProposed { get; }
    internal WorldSession PartnerProposed { get; }
    internal string UsersRoot => ActorBinding.UsersRoot;
    private readonly Member actor, partner;
    private readonly JournalOwner journalOwner;
    private LegacyPairMovementOperation? committed;

    private sealed class Member
    {
        internal readonly LegacyOrdinarySpellBinding Binding;
        internal readonly WorldSession Before, Proposed;
        internal readonly LegacyNativeAttributeMapProof beforeAttributes, beforePaid, afterAttributes, afterPaid;
        internal string Path = "";
        internal byte[] BeforeImage = [], AfterImage = [];
        internal Member(LegacyOrdinarySpellBinding binding, WorldSession before, WorldSession proposed)
        {
            Binding = binding; Before = before;
            beforeAttributes = new(before.Character.Attributes); beforePaid = new(before.Character.PaidNobility);
            var canonical = new WorldPacketHandlers.BoundHitpointsInputs(before.Character, proposed.Character).After;
            Proposed = proposed with { Character = canonical };
            afterAttributes = new(canonical.Attributes); afterPaid = new(canonical.PaidNobility);
        }
        internal bool IsCurrentPlain() => beforeAttributes.IsCurrentPlain() && beforePaid.IsCurrentPlain() &&
            afterAttributes.IsCurrentPlain() && afterPaid.IsCurrentPlain();
    }

    private LegacyPreparedPairMovementWrite(WorldServerFrameHandler frame, WorldSessionStore store,
        WorldPacketHandlerContext context, LegacyPairMovementOperation operation,
        LegacyOrdinarySpellBinding actorBinding, WorldSession actorBefore,
        LegacyOrdinarySpellBinding partnerBinding, WorldSession partnerBefore)
    {
        Frame = frame; Store = store; Context = context; Operation = operation;
        ActorBinding = actorBinding; PartnerBinding = partnerBinding;
        ActorBefore = actorBefore; PartnerBefore = partnerBefore;
        actor = new(actorBinding, actorBefore, WorldPacketHandlers.ProjectPairMovementActor(actorBefore, operation));
        partner = new(partnerBinding, partnerBefore, WorldPacketHandlers.ProjectPairMovementPartner(partnerBefore, operation));
        ActorProposed = actor.Proposed; PartnerProposed = partner.Proposed;
        journalOwner = new(this);
        operation.Validate();
        ValidateProjection();
        if (!IsCurrent()) throw new InvalidOperationException("pairMovementOwnerUnavailable");
        CaptureImages();
        if (!IsCurrent()) throw new InvalidOperationException("pairMovementOwnerChangedDuringCapture");
    }

    internal static LegacyPreparedPairMovementWrite Prepare(WorldServerFrameHandler frame, WorldSessionStore store,
        WorldPacketHandlerContext context, LegacyPairMovementOperation operation,
        LegacyOrdinarySpellBinding actorBinding, WorldSession actorBefore,
        LegacyOrdinarySpellBinding partnerBinding, WorldSession partnerBefore) =>
        new(frame, store, context, operation, actorBinding, actorBefore, partnerBinding, partnerBefore);

    internal bool IsCurrent() => actor.IsCurrentPlain() && partner.IsCurrentPlain() && Store.IsPairMovementWriteOwnerCurrent(this);

    private void ValidateProjection()
    {
        var op = Operation;
        if (ActorBefore.AccountId != op.ActorAccountId || PartnerBefore.AccountId != op.PartnerAccountId ||
            ActorBefore.Character.Uid != op.ActorUid || PartnerBefore.Character.Uid != op.PartnerUid ||
            ActorBefore.Character.X != op.FromX || ActorBefore.Character.Y != op.FromY ||
            PartnerBefore.Character.X != op.FromX || PartnerBefore.Character.Y != op.FromY ||
            ActorBefore.Character.MapId != op.MapId || PartnerBefore.Character.MapId != op.MapId ||
            !MovementScalarsEqual(ActorBefore.Character, ActorProposed.Character, op) ||
            !MovementScalarsEqual(PartnerBefore.Character, PartnerProposed.Character, op))
            throw new InvalidDataException("pairMovementProjectionInvalid");
        uint actorAction = op.Kind == LegacyPairMovementKind.Jump ? 100U : 0U;
        uint partnerAction = op.Kind == LegacyPairMovementKind.Jump ? 100U : LegacyActionState.RequireCurrent(PartnerBefore.Character);
        if (LegacyActionState.RequireCurrent(ActorProposed.Character) != actorAction ||
            LegacyActionState.RequireCurrent(PartnerProposed.Character) != partnerAction)
            throw new InvalidDataException("pairMovementActionProjectionInvalid");
    }

    private static bool MovementScalarsEqual(CharacterSnapshot before, CharacterSnapshot after, LegacyPairMovementOperation op) =>
        after.X == op.ToX && after.Y == op.ToY &&
        WorldPacketHandlers.BoundHitpointsInputs.ScalarsEqual(before with { X = op.ToX, Y = op.ToY }, after) &&
        PersistedMovementFieldsEqual(before with { X = op.ToX, Y = op.ToY }, after) &&
        WorldPacketHandlers.BoundHitpointsInputs.PaidEqual(before, after);

    private static bool PersistedMovementFieldsEqual(CharacterSnapshot first, CharacterSnapshot second)
    {
        // The proposed session uses the canonical persisted/runtime projection.
        // Compare both sides in that same form: an absent EpicTrojan composite
        // and its projected defaults represent the same unchanged state.
        var canonicalFirst = new WorldPacketHandlers.BoundHitpointsInputs(first, first).After;
        var canonicalSecond = new WorldPacketHandlers.BoundHitpointsInputs(second, second).After;
        return !CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(canonicalFirst, canonicalSecond);
    }
}
