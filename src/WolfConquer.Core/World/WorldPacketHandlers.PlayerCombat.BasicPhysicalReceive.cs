using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed class BasicPhysicalReceiveDecision { }
    internal enum BasicPhysicalReceiveKind { Observation, Nonlethal, NormalDeath }
    private sealed class BasicPhysicalReceiveData
    {
        internal readonly WorldServerFrameHandler.BasicPhysicalAttempt Attempt;
        internal readonly BasicPhysicalReceiveKind Kind;
        internal readonly uint Previous, Damage;
        internal readonly long ObservedHitpoints;
        internal readonly WorldSession Expected, Projection;
        private readonly LegacyNativeAttributeMapProof attributes, paid, projectionAttributes, projectionPaid;
        internal BasicPhysicalReceiveData(WorldServerFrameHandler.BasicPhysicalAttempt attempt, BasicPhysicalReceiveKind kind,
            uint previous, uint damage, WorldSession expected, WorldSession calculated)
        {
            Attempt = attempt; Kind = kind; Previous = previous; Damage = damage; Expected = expected;
            ObservedHitpoints = calculated.Character.MinHitPoints;
            attributes = LegacyNativeAttributeMapProof.CaptureRaw(calculated.Character.Attributes);
            paid = LegacyNativeAttributeMapProof.CaptureRaw(calculated.Character.PaidNobility);
            // Private BCL copies compose with the existing runtime input reader,
            // including empty paid maps. Their own witnesses prevent an escaped
            // projection edit from inheriting the source decision's authority.
            Projection = expected with { Character = calculated.Character with
            {
                Attributes = new Dictionary<string, string>(attributes.Fields, attributes.CapturedComparer),
                PaidNobility = new Dictionary<string, string>(paid.Fields, paid.CapturedComparer),
            } };
            projectionAttributes = LegacyNativeAttributeMapProof.CaptureRaw(Projection.Character.Attributes);
            projectionPaid = LegacyNativeAttributeMapProof.CaptureRaw(Projection.Character.PaidNobility);
            if (!IsCurrentPlain()) throw new InvalidOperationException("basicPhysicalCalculatedInputsChanged");
        }
        internal bool IsCurrentPlain() => attributes.IsCurrentPlain() && paid.IsCurrentPlain() &&
            projectionAttributes.IsCurrentPlain() && projectionPaid.IsCurrentPlain();
    }
    private static readonly ConditionalWeakTable<BasicPhysicalReceiveDecision, BasicPhysicalReceiveData> BasicPhysicalReceives = new();

    // Only this actual prepared receive path issues a projection. A token by
    // itself, a handled packet or an unsealed damage result cannot commit/arm.
    private static BasicPhysicalReceiveDecision? PrepareBasicPhysicalReceiveDecision(WorldPacketHandlerContext context,
        LegacyPlayerMagicReceivePreparation prepared)
    {
        if (context.BasicPhysicalAttempt is not { } attempt) return null;
        var result = prepared.Result;
        if (result.DamageApplied > result.PreviousHitpoints || result.UpdatedSession.Character.MinHitPoints < 0)
            throw new InvalidOperationException("basicPhysicalReceiveProjectionInvalid");
        var kind = !prepared.RequiresDamageCommit ? BasicPhysicalReceiveKind.Observation :
            result.Killed ? BasicPhysicalReceiveKind.NormalDeath : BasicPhysicalReceiveKind.Nonlethal;
        if (context.Sessions is not { } store || !store.TryGet(result.ActiveSession.Connection, out var actual) || actual is null ||
            actual.Character.Uid != result.UpdatedSession.Character.Uid || actual.AccountId != result.UpdatedSession.AccountId ||
            !ReferenceEquals(actual.NativeGameClient, result.UpdatedSession.NativeGameClient))
            throw new InvalidOperationException("basicPhysicalCalculatedOwnerLost");
        var decision = new BasicPhysicalReceiveDecision();
        BasicPhysicalReceives.Add(decision, new(attempt, kind, result.PreviousHitpoints,
            result.DamageApplied, actual, result.UpdatedSession));
        return decision;
    }

    internal static bool TryReadBasicPhysicalReceive(BasicPhysicalReceiveDecision decision,
        out WorldServerFrameHandler.BasicPhysicalAttempt attempt, out BasicPhysicalReceiveKind kind,
        out uint previous, out uint damage, out long observed)
    {
        attempt = null!; kind = default; previous = damage = 0; observed = 0;
        if (!BasicPhysicalReceives.TryGetValue(decision, out var issued) || !issued.IsCurrentPlain()) return false;
        attempt = issued.Attempt; kind = issued.Kind; previous = issued.Previous;
        damage = issued.Damage; observed = issued.ObservedHitpoints;
        return true;
    }

    internal static bool TryReadBasicPhysicalProjection(BasicPhysicalReceiveDecision decision,
        out WorldSession expected, out WorldSession projection)
    {
        expected = projection = null!;
        if (!BasicPhysicalReceives.TryGetValue(decision, out var data) || !data.IsCurrentPlain()) return false;
        expected = data.Expected; projection = data.Projection;
        return true;
    }
}

public sealed partial record WorldPacketHandlerContext
{
    [System.Text.Json.Serialization.JsonIgnore]
    internal WorldPacketHandlers.BasicPhysicalReceiveDecision? BasicPhysicalReceiveDecision { get; init; }
}
