using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed class OwnedMovementProjection
    {
        internal WorldSession Expected => ReadMovementProjection(this).Expected;
        internal WorldSession Proposed => ReadMovementProjection(this).Proposed;
        internal uint Action => ReadMovementProjection(this).Action;
        internal bool IsCurrentPlain() => OwnedMovementProjections.TryGetValue(this, out var actual) && actual.IsCurrentPlain();
    }

    private sealed class OwnedMovementProjectionData
    {
        internal WorldSession Expected { get; }
        internal WorldSession Proposed { get; }
        internal uint Action { get; }
        private readonly LegacyNativeAttributeMapProof beforeAttributes, beforePaid, afterAttributes, afterPaid;

        internal OwnedMovementProjectionData(WorldSession expected, CharacterSnapshot character, uint action)
        {
            Expected = expected; Action = action;
            beforeAttributes = LegacyNativeAttributeMapProof.CaptureRaw(expected.Character.Attributes);
            beforePaid = LegacyNativeAttributeMapProof.CaptureRaw(expected.Character.PaidNobility);
            var canonical = new BoundHitpointsInputs(expected.Character, character).After;
            Proposed = expected with { Character = canonical };
            afterAttributes = LegacyNativeAttributeMapProof.CaptureRaw(canonical.Attributes);
            afterPaid = LegacyNativeAttributeMapProof.CaptureRaw(canonical.PaidNobility);
            if (!BoundHitpointsInputs.ScalarsEqual(expected.Character with { X = canonical.X, Y = canonical.Y }, canonical) ||
                CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(expected.Character with { X = canonical.X, Y = canonical.Y }, canonical) ||
                !BoundHitpointsInputs.PaidEqual(expected.Character, canonical) || LegacyActionState.RequireCurrent(canonical) != action ||
                !IsCurrentPlain()) throw new InvalidDataException("ordinaryMovementProjectionInvalid");
        }

        internal bool IsCurrentPlain() => beforeAttributes.IsCurrentPlain() && beforePaid.IsCurrentPlain() &&
            afterAttributes.IsCurrentPlain() && afterPaid.IsCurrentPlain();
    }

    private static readonly ConditionalWeakTable<OwnedMovementProjection, OwnedMovementProjectionData> OwnedMovementProjections = new();
    private static OwnedMovementProjectionData ReadMovementProjection(OwnedMovementProjection token) =>
        OwnedMovementProjections.TryGetValue(token, out var actual) ? actual : throw new InvalidOperationException("ordinaryMovementPurposeNotIssued");
    private static OwnedMovementProjection IssueMovementProjection(WorldSession before, CharacterSnapshot after, uint action)
    {
        var data = new OwnedMovementProjectionData(before, after, action);
        var token = new OwnedMovementProjection();
        OwnedMovementProjections.Add(token, data);
        return token;
    }

    // These factories remain private to the actual already-validated handler
    // branches. Neither an Action scalar nor a coordinate DTO grants purpose.
    private static OwnedMovementProjection PrepareOwnedWalk(WorldSession before, MsgMovementPacket request, CharacterSnapshot after)
    {
        if (!TryCalculateMovementPosition(before.Character, request, out var x, out var y, out _) || after.X != x || after.Y != y)
            throw new InvalidDataException("ordinaryWalkProjectionInvalid");
        return IssueMovementProjection(before, after, 0);
    }

    private static OwnedMovementProjection PrepareOwnedJump(WorldSession before, CharacterSnapshot after)
    {
        if (after.X is < 0 or > ushort.MaxValue || after.Y is < 0 or > ushort.MaxValue || after.X == 0 && after.Y == 0 ||
            Math.Abs(after.X - before.Character.X) >= 17 || Math.Abs(after.Y - before.Character.Y) >= 17)
            throw new InvalidDataException("ordinaryJumpProjectionInvalid");
        return IssueMovementProjection(before, after, 100);
    }
}
