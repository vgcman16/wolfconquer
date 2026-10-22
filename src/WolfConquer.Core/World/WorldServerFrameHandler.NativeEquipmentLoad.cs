using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private LegacyNativeEquipmentRuntime? BeginLegacyNativeEquipmentRawLoad(ConquerTcpConnection connection)
    {
        // An absent configured loader is NOT a genuinely empty native Equip.
        if (_snapshotLoader?.PlayerItemsRoot is null || connection.NativeGameClient is not { } client)
            return null; // Preserve existing unbound/manual behavior, no native owner fallback.
        var equipment = new LegacyNativeEquipmentRuntime(client);
        client.RawEquipmentObservation = equipment; // Observation only, BEFORE raw file open; NOT genuine DataCore Equip.
        return equipment;
    }
}
