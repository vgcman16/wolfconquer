using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed class OwnedTeleportDestinationProjection
    {
        internal WorldSession Expected => ReadTeleportDestination(this).Expected;
        internal WorldSession Proposed => ReadTeleportDestination(this).Proposed;
        internal LegacyTeleportOwner? OriginalOwner => ReadTeleportDestination(this).Owner;
        internal bool IsCurrentPlain() => TeleportDestinations.TryGetValue(this, out var actual) && actual.IsCurrentPlain();
    }
    private sealed class TeleportDestinationData
    {
        internal readonly WorldSession Expected, Proposed;
        internal readonly LegacyTeleportOwner? Owner;
        private readonly LegacyNativeAttributeMapProof before, paidBefore, after, paidAfter;
        internal TeleportDestinationData(WorldSession expected, WorldSession proposed, LegacyTeleportOwner? owner)
        {
            Expected = expected; Owner = owner;
            before = new(expected.Character.Attributes); paidBefore = new(expected.Character.PaidNobility);
            var canonical = new BoundHitpointsInputs(expected.Character, proposed.Character).After;
            Proposed = proposed with { Character = canonical };
            after = new(canonical.Attributes); paidAfter = new(canonical.PaidNobility);
            var location = expected.Character with { MapId = canonical.MapId, X = canonical.X, Y = canonical.Y,
                PreviousMapId = canonical.PreviousMapId, PreviousMapX = canonical.PreviousMapX, PreviousMapY = canonical.PreviousMapY };
            if ((proposed with { Character = expected.Character }) != expected ||
                !BoundHitpointsInputs.ScalarsEqual(location, canonical) ||
                CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(location, canonical) ||
                !BoundHitpointsInputs.PaidEqual(expected.Character, canonical)) throw InvalidTeleportDestination();
            _ = LegacyNativeActorScalarState.RequireCurrent(canonical);
        }
        internal bool IsCurrentPlain() => before.IsCurrentPlain() && paidBefore.IsCurrentPlain() &&
            after.IsCurrentPlain() && paidAfter.IsCurrentPlain();
    }
    private static readonly ConditionalWeakTable<OwnedTeleportDestinationProjection, TeleportDestinationData> TeleportDestinations = new();
    private static TeleportDestinationData ReadTeleportDestination(OwnedTeleportDestinationProjection purpose) =>
        TeleportDestinations.TryGetValue(purpose, out var actual) ? actual : throw InvalidTeleportDestination();
    private static OwnedTeleportDestinationProjection PrepareOwnedTeleportDestination(WorldPacketHandlerContext context,
        WorldSession expected, WorldSession proposed)
    {
        var data = new TeleportDestinationData(expected, proposed, context.TeleportOwner);
        var token = new OwnedTeleportDestinationProjection(); TeleportDestinations.Add(token, data); return token;
    }
    private static InvalidOperationException InvalidTeleportDestination() => new("ownedTeleportDestinationPurposeInvalid");
}
