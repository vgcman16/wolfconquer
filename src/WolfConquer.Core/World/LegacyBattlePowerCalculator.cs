using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static class LegacyBattlePowerCalculator
{
    private const byte NoSocket = 0;

    public static uint CalculateEquipmentBattlePower(
        PlayerItemSnapshot playerItems,
        bool alternateEquipmentActive = false)
    {
        ArgumentNullException.ThrowIfNull(playerItems);

        var leftWeaponFree = LegacyEquipmentSelector.IsPositionFree(playerItems, position: 5);
        var alternateLeftWeaponFree = LegacyEquipmentSelector.IsPositionFree(playerItems, position: 55);
        var total = 0u;
        foreach (var item in LegacyEquipmentSelector.SelectCurrentEquipment(playerItems, alternateEquipmentActive))
        {
            total = checked(total + CalculateItemBattlePower(item, leftWeaponFree, alternateLeftWeaponFree));
        }

        return total;
    }

    public static uint CalculateItemBattlePower(
        PlayerItemSnapshotItem item,
        bool leftWeaponSlotFree = true,
        bool alternateLeftWeaponSlotFree = true)
    {
        ArgumentNullException.ThrowIfNull(item);

        var equipmentPosition = ItemType.GetLegacyEquipmentPosition(item.ItemId);
        if (IsExcludedEquipmentPosition(equipmentPosition) ||
            ItemType.IsLegacyHossu(item.ItemId))
        {
            return 0;
        }

        var points = GetQualityBattlePower(item.ItemId);
        points += Math.Min(item.Plus, (byte)12);

        var socketOne = IsSocketClearedPosition(equipmentPosition) ? NoSocket : item.SocketOne;
        var socketTwo = IsSocketClearedPosition(equipmentPosition) ? NoSocket : item.SocketTwo;
        points += GetSocketBattlePower(socketOne);
        points += GetSocketBattlePower(socketTwo);
        points += GetAnimaBattlePower(item.AnimaItemId);

        if (ItemType.IsLegacyBacksword(item.ItemId) ||
            IsTaoistEpicWeapon(item.ItemId) ||
            (ItemType.IsLegacyTwoHandWeapon(item.ItemId) && leftWeaponSlotFree && alternateLeftWeaponSlotFree))
        {
            points *= 2;
        }

        return points;
    }

    private static bool IsExcludedEquipmentPosition(ushort equipmentPosition)
    {
        return equipmentPosition is 7 or 9 or 13 or 15 or 16 or 17;
    }

    private static bool IsSocketClearedPosition(ushort equipmentPosition)
    {
        return equipmentPosition is 7 or 12 or 13 or 15 or 16 or 17 or 18;
    }

    private static uint GetQualityBattlePower(uint itemId)
    {
        var quality = itemId - ((itemId / 10u) * 10u);
        return quality switch
        {
            9 => 4u,
            8 => 3u,
            7 => 2u,
            6 => 1u,
            _ => 0u,
        };
    }

    private static uint GetSocketBattlePower(byte socket)
    {
        if (socket == NoSocket)
        {
            return 0;
        }

        return (uint)(socket % 10 == 3 ? 2 : 1);
    }

    private static uint GetAnimaBattlePower(uint animaItemId)
    {
        return (animaItemId % 100u) switch
        {
            0 => 0,
            >= 1 and <= 3 => 1,
            19 => 16,
            var animaLevel => (uint)(byte)(animaLevel - 2),
        };
    }

    private static bool IsTaoistEpicWeapon(uint itemId)
    {
        return itemId >= 620003 && itemId <= 620439;
    }
}
