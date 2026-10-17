namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed partial class LegacyEquipmentQueryExecution
    {
        private bool AddManaMaximum(uint addition) => Commit(session => session with
        {
            PhysicalStatusOverlay = LegacyPhysicalStatusOverlay.ForOwner(session) with
            {
                MaxMana = unchecked((LegacyPhysicalStatusOverlay.ForOwner(session).MaxMana ?? 0) + addition),
            },
        });

        private bool AddJiangHuManaMaximum()
        {
            if (!Refresh()) return false;
            // The target's existing explicit client-interserver boundary retains the independent native cache.
            // Global interserver-host startup remains a separately unsupported target environment.
            var contribution = HasLegacyPlayerMagicInterserverAttribute(Session)
                ? LegacyPhysicalStatusOverlay.ForOwner(Session).JiangHuMaxMana ?? 0
                : LegacyEquipmentNativeMana.JiangHu(Session.Character, Context.WorldData);
            return Commit(session => session with
            {
                PhysicalStatusOverlay = LegacyPhysicalStatusOverlay.ForOwner(session) with
                {
                    JiangHuMaxMana = contribution,
                    MaxMana = unchecked((LegacyPhysicalStatusOverlay.ForOwner(session).MaxMana ?? 0) + contribution),
                },
            });
        }
    }
}
