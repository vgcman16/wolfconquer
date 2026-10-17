using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Scoped source-root resolution only. Other live frontdoors retain their explicit outstanding ownership.
    private static bool TryResolveLegacyNativeRootFirstFamilyCandidate(
        WorldSession session, WorldDataCatalog worldData, ushort spellId,
        out PlayerSpellSnapshotEntry learned, out LegacyMagicRuntimeSpell spell)
    {
        if (LegacyMagicRuntime.TryResolveNativeLearnedSpell(
                session.PlayerSpells, worldData, spellId, out learned, out spell, out _) &&
            IsLegacyRootFirstFamilyInvocation(session, spell))
            return true;
        learned = default!;
        spell = default!;
        return false;
    }
}
