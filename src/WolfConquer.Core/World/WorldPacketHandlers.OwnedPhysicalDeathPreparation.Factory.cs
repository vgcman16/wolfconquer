namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal static OwnedPhysicalDeathPreparation PrepareOwnedDragonhowlDeath(WorldSessionStore.OwnedDragonhowlDeath purpose) =>
        OwnedPhysicalDeathPreparation.PrepareActualDragonhowl(purpose);
    // The actual paired Store preparation holds both connection/actor gates.
    // The decision's private registry, not caller-supplied after-state, owns
    // the calculation passed to the pure death/query builder.
    internal static OwnedPhysicalDeathPreparation PrepareOwnedPhysicalDeath(WorldPacketHandlerContext context,
        BasicPhysicalReceiveDecision decision, LegacyOrdinarySpellBinding actorBinding,
        LegacyOrdinarySpellBinding targetBinding, WorldSession expectedActor, WorldSession expectedTarget) =>
        OwnedPhysicalDeathPreparation.PrepareActual(context, decision, actorBinding, targetBinding, expectedActor, expectedTarget);

    internal sealed partial class OwnedPhysicalDeathPreparation
    {
        internal bool MatchesDragonhowlCanonical(WorldSession target) => Dragonhowl is not null &&
            IsCurrentPlain(TargetBinding) && CanonicalTargetMatches(target);

        internal static OwnedPhysicalDeathPreparation PrepareActualDragonhowl(WorldSessionStore.OwnedDragonhowlDeath purpose)
        {
            if (!purpose.IsCurrentPlain()) throw InvalidOwnedPhysicalDeath();
            var context = purpose.ActorBinding.Store.RequireOwnedDragonhowlDeathContext(purpose);
            return new OwnedPhysicalDeathBuilder(context, null, null, purpose.ActorBinding, purpose.TargetBinding,
                purpose.ExpectedActor, purpose.ExpectedTarget, purpose.Calculated, purpose).Build();
        }
        // Construction stays in this actual nested type. Its private builder can
        // use the private constructor without widening arbitrary recipe authority.
        internal static OwnedPhysicalDeathPreparation PrepareActual(WorldPacketHandlerContext context,
            BasicPhysicalReceiveDecision decision, LegacyOrdinarySpellBinding actorBinding,
            LegacyOrdinarySpellBinding targetBinding, WorldSession expectedActor, WorldSession expectedTarget)
        {
            if (context.Sessions is not { } store || !ReferenceEquals(store, actorBinding.Store) ||
                !ReferenceEquals(store, targetBinding.Store) ||
                !TryReadBasicPhysicalReceive(decision, out var attempt, out var kind, out _, out _, out _) ||
                kind != BasicPhysicalReceiveKind.NormalDeath || !ReferenceEquals(context.BasicPhysicalAttempt, attempt) ||
                !TryReadBasicPhysicalProjection(decision, out var captured, out var calculated) ||
                !ReferenceEquals(captured, expectedTarget) || calculated.Character.Uid != targetBinding.Uid ||
                calculated.AccountId != targetBinding.AccountId || !ReferenceEquals(calculated.NativeGameClient, targetBinding.Client))
                throw new InvalidOperationException("ordinaryPhysicalDeathRequiresActualDecision");
            var actual = store.RequireOwnedPhysicalDeathContext(context, decision, attempt, actorBinding,
                targetBinding, expectedActor, expectedTarget);
            return new OwnedPhysicalDeathBuilder(actual, decision, attempt, actorBinding, targetBinding,
                expectedActor, expectedTarget, calculated).Build();
        }
    }
}
