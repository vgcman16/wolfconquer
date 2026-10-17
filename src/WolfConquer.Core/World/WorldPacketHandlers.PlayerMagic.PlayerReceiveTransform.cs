using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal static bool TryPrepareLegacyPlayerTransformFinish(
        WorldSession session,
        WorldDataCatalog? worldData,
        ILegacySignedMillisecondClock clock,
        out LegacyPlayerTransformFinishPreparation prepared)
    {
        prepared = new(session);
        _ = clock; // Admission belongs to the actual async query AFTER metadata commit/CAS.
        // Preserve source Status.MaxHP before clear even if the first owned
        // equipment query has not yet occurred during this transformation.
        var previousMaximum = GetLegacyEquipmentRuntimeMaxHitpoints(session, worldData);
        if (!LegacyTransformState.TryClear(session.Character, out var cleared))
        {
            return false;
        }

        var staged = session with
        {
            Character = cleared, // No normalization/clamp before query admission.
            EquipmentHitpointsStatus = new LegacyEquipmentHitpointsStatus(session.Character.Uid, previousMaximum),
        };
        prepared = new(staged);
        return true;
    }
}
