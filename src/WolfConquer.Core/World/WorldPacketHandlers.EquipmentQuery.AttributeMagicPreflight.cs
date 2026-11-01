using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static void PreflightAttributeMagic(WorldPacketHandlerContext context, WorldSession source,
        IReadOnlyList<PlayerItemSnapshotItem> equipment)
    {
        var data = context.WorldData;
        var fields = LegacyEquipmentMagicArithmetic.Start(source.Character.Spirit);
        uint mana = 0, prestige = 0;
        foreach (var item in equipment)
        {
            prestige = checked(prestige + item.PerfectionLevel); // Actual query includes broken captured items.
            if (item.Durability == 0) continue;
            fields = (item.Position % 50) switch
            {
                4 => fields with { RightWeaponItemId = item.ItemId },
                5 => fields with { LeftWeaponItemId = item.ItemId },
                _ => fields,
            };
            fields = LegacyPlayerStatusCalculator.NativeMagicItemGems(fields, item);
            fields = LegacyEquipmentMagicArithmetic.Add(fields, LegacyPlayerStatusCalculator.NativeMagicItemAfterCircle(item, data));
            mana = checked(mana + LegacyEquipmentNativeMana.Item(item, data));
        }
        if (data is not null && TryGetLegacyEligibleEquippedRuneLevel(source, data, "witchery", out var witchery))
            fields = LegacyEquipmentMagicArithmetic.Witchery(fields, witchery);
        if (data is not null && TryGetLegacyEligibleEquippedRuneLevel(source, data, "Nature`sChant", out var chant))
            fields = LegacyEquipmentMagicArithmetic.NatureChant(fields, chant);
        fields = EquipmentQueryMagicBeforeGear(fields, source);
        fields = LegacyEquipmentMagicArithmetic.Add(fields, LegacyPlayerStatusCalculator.NativeMagicBeforeGear(source, data, prestige));
        fields = LegacyEquipmentMagicArithmetic.Add(fields, NativeMagicInnerPower(source, data));
        mana = checked(mana + LegacyEquipmentNativeMana.CombatGear(source, data));
        fields = LegacyEquipmentMagicArithmetic.Add(fields, LegacyPlayerStatusCalculator.NativeMagicGear(source, data));
        fields = LegacyEquipmentMagicArithmetic.Add(fields, LegacyPlayerStatusCalculator.NativeMagicMythsoul(source, data));
        mana = checked(mana + LegacyEquipmentNativeMana.ClassAndSpirit(source.Character));
        fields = LegacyEquipmentMagicArithmetic.Add(fields, NativeMagicSubClass(source, data));
        bool interserver = HasLegacyPlayerMagicInterserverAttribute(source);
        var jiangHu = interserver
            ? LegacyNativeMagicQueryClient.ReadJiangHu(context.Sessions, context.Connection, source.Character.Uid,
                source.NativeGameClient?.OrdinarySpellBinding?.NativeIdentity)
            : NativeMagicJiangHu(source, data);
        fields = LegacyEquipmentMagicArithmetic.Add(fields, jiangHu);
        mana = checked(mana + (interserver ? LegacyPhysicalStatusOverlay.ForOwner(source).JiangHuMaxMana ?? 0
            : LegacyEquipmentNativeMana.JiangHu(source.Character, data)));
        fields = LegacyEquipmentMagicArithmetic.Add(fields, LegacyPlayerStatusCalculator.NativeMagicRuneStorage(source, data));
        if (HasLegacyStatusFlag(source.Character, LegacyRiseOfTaoismFlag))
            fields = LegacyEquipmentMagicArithmetic.Add(fields, new(13_000, 0, 0, 0));
        // These are arithmetic checks only. The later private query still owns
        // native publication, metadata currentness and every successful output.
        _ = fields; _ = mana;
    }
}
