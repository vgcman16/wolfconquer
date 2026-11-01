using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private readonly LegacyNativeCombatGearRuntime? _nativeCombatGear;

    internal bool TryCaptureNativeCombatGearMetadata(WorldSessionStore store,
        out LegacyNativeCombatGearTable.MailboxLoadProof? proof)
    {
        proof = null;
        if (!ReferenceEquals(store, _sessions) || _nativeCombatGear is not { } metadata || _worldData is not { } catalog ||
            _protocolProfile != ConquerProtocolProfile.VisibleDx8D13 || _ownedAdmissionRuntime is not { } runtime ||
            !runtime.IsNativeCombatGearFrameCurrent(this) || !metadata.TryCapture(catalog, out var captured) ||
            !runtime.IsNativeCombatGearFrameCurrent(this) || captured is null || !captured.IsCurrent()) return false;
        proof = captured;
        return true;
    }
}
