using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private sealed record DragonhowlScalarQueryOrigin(LegacyOrdinarySpellBinding Binding, WorldSession Session,
        LegacyNativeMagicQueryClient.CompletedMaximums Query, LegacyNativeMagicQueryClient.RecordedReceiveValues Values);

    private DragonhowlScalarQueryOrigin CaptureDragonhowlScalarQuery(DragonhowlOwnerState owner,
        LegacyOrdinarySpellBinding binding)
    {
        if (owner.Plan is not null || owner.Started || !DragonhowlHeldCurrent(owner) ||
            !owner.Current.TryGetValue(binding.Connection, out var live) ||
            !LegacyNativeMagicQueryClient.TryCaptureRecordedReceive(binding, live, out var query, out var values) || query is null)
            throw new InvalidOperationException("ownedDragonhowlScalarQueryOriginUnavailable");
        if (owner.ScalarQueries.TryGetValue(binding.Connection, out var prior))
        {
            if (!ReferenceEquals(prior.Binding, binding) || !ReferenceEquals(prior.Session, live) ||
                !ReferenceEquals(prior.Query, query) || prior.Values != values)
                throw new InvalidOperationException("ownedDragonhowlScalarQueryOriginChanged");
            return prior;
        }
        var origin = new DragonhowlScalarQueryOrigin(binding, live, query, values);
        owner.ScalarQueries.Add(binding.Connection, origin);
        return origin;
    }

    private static bool ScalarQueryShapeUnchanged(WorldSession current, WorldSession original) =>
        current.Character.Class == original.Character.Class && current.Character.Level == original.Character.Level &&
        current.Character.Reborn == original.Character.Reborn &&
        LegacyNativeAttributeState.From(current.Character) == LegacyNativeAttributeState.From(original.Character) &&
        ReferenceEquals(current.PlayerItems, original.PlayerItems) && ReferenceEquals(current.PlayerSpells, original.PlayerSpells);
}
