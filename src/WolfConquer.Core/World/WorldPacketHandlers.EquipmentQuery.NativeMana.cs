namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed partial class LegacyEquipmentQueryExecution
    {
        private uint capturedManaAggregate;

        private bool AddManaMaximum(uint addition, uint? jiangHu = null)
        {
            uint before = capturedManaAggregate;
            uint after = checked(before + addition);
            if (!Commit(session =>
            {
                var overlay = LegacyPhysicalStatusOverlay.ForOwner(session);
                if (overlay.MaxMana != before) throw new InvalidDataException("equipmentManaQueryAggregateChanged");
                return session with { PhysicalStatusOverlay = overlay with
                { MaxMana = after, JiangHuMaxMana = jiangHu ?? overlay.JiangHuMaxMana } };
            })) return false;
            capturedManaAggregate = after;
            return true;
        }

        private bool AddJiangHuManaMaximum()
        {
            if (!Refresh()) return false;
            // The target's existing explicit client-interserver boundary retains the independent native cache.
            // Global interserver-host startup remains a separately unsupported target environment.
            var contribution = HasLegacyPlayerMagicInterserverAttribute(Session)
                ? LegacyPhysicalStatusOverlay.ForOwner(Session).JiangHuMaxMana ?? 0
                : LegacyEquipmentNativeMana.JiangHu(Session.Character, Context.WorldData);
            return AddManaMaximum(contribution, contribution);
        }
    }
}
