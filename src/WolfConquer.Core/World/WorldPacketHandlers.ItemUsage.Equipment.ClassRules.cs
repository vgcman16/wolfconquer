using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool CanEquipLegacyRightHandItem(
            WorldSession session,
            PlayerItemSnapshotItem item,
            bool alternate = false)
    {
        var leftHandPosition = alternate ? 55 : 5;
        var leftHand = session.PlayerItems.EquipmentItems.FirstOrDefault(equipped => equipped.Position == leftHandPosition);
        if (leftHand is null)
        {
            return true;
        }

        if (ItemType.IsLegacyDagger(leftHand.ItemId) && !ItemType.IsLegacyDagger(item.ItemId))
        {
            return false;
        }

        if (ItemType.IsLegacyKnife(leftHand.ItemId) && !ItemType.IsLegacyKnife(item.ItemId))
        {
            return false;
        }

        return !ItemType.IsLegacyShield(leftHand.ItemId) || !ItemType.IsLegacyKnife(item.ItemId);
    }

    private static bool IsAlternateEquipmentToggleBlocked(WorldSession session, out string reason)
    {
        if (HasLegacyStatusFlag(session.Character, LegacyFineRain1Flag))
        {
            reason = "fine rain";
            return true;
        }

        var alternateLeftHand = session.PlayerItems.EquipmentItems
            .FirstOrDefault(equipped => equipped.Position == 55);
        if (alternateLeftHand is not null && ItemType.IsLegacyKnife(alternateLeftHand.ItemId))
        {
            reason = "alternate left-hand knife";
            return true;
        }

        if (alternateLeftHand is not null && ItemType.IsLegacyDagger(alternateLeftHand.ItemId))
        {
            reason = "alternate left-hand dagger";
            return true;
        }

        var alternateRightHand = session.PlayerItems.EquipmentItems
            .FirstOrDefault(equipped => equipped.Position == 54);
        if (alternateRightHand is not null && ItemType.IsLegacyKnife(alternateRightHand.ItemId))
        {
            reason = "alternate right-hand knife";
            return true;
        }

        if (HasLegacyStatusFlag(session.Character, LegacyPathOfShadowFlag))
        {
            reason = "path of shadow";
            return true;
        }

        if (HasLegacyStatusFlag(session.Character, LegacyFlyFlag))
        {
            reason = "fly";
            return true;
        }

        if (ReadAttributeUInt64(session.Character.Attributes, "SpecialGarment") != 0)
        {
            reason = "special garment";
            return true;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            reason = "inventory full";
            return true;
        }

        reason = "";
        return false;
    }

    private static bool CanEquipRightHandWeaponInLeftHand(
            WorldSession session,
            PlayerItemSnapshotItem item,
            bool alternate = false)
    {
        if (ItemType.IsLegacyTwoHandWeapon(item.ItemId))
        {
            return false;
        }

        var canDualWield = alternate
            ? HasLegacyAlternateDualWieldClass(session.Character.Class, item.ItemId)
            : HasLegacyDualWieldClass(session.Character.Class);
        if (!canDualWield)
        {
            return false;
        }

        var rightHandPosition = alternate ? 54 : 4;
        var rightHand = session.PlayerItems.EquipmentItems.FirstOrDefault(equipped => equipped.Position == rightHandPosition);
        return rightHand is not null && !ItemType.IsLegacyTwoHandWeapon(rightHand.ItemId);
    }

    private static bool CanEquipLegacyPistol(WorldSession session, bool alternate = false)
    {
        if (alternate)
        {
            return CanEquipLegacyAlternatePistol(session);
        }

        var rightHandPosition = alternate ? 54 : 4;
        var rightHand = session.PlayerItems.EquipmentItems.FirstOrDefault(equipped => equipped.Position == rightHandPosition);
        return rightHand is not null &&
            ItemType.IsLegacyRapier(rightHand.ItemId) &&
            HasLegacyNaturalLeftHandItemClass(session.Character.Class) &&
            !IsLegacyTaoistClass(session.Character.Class);
    }

    private static bool CanEquipLegacyAlternatePistol(WorldSession session)
    {
        var normalRightHand = session.PlayerItems.EquipmentItems.FirstOrDefault(equipped => equipped.Position == 4);
        var alternateRightHand = session.PlayerItems.EquipmentItems.FirstOrDefault(equipped => equipped.Position == 54);
        return normalRightHand is not null &&
            ItemType.IsLegacyRapier(normalRightHand.ItemId) &&
            alternateRightHand is not null &&
            HasLegacyAlternateNaturalLeftHandItemClass(session.Character.Class);
    }

    private static bool CanEquipLegacyLeftHandItem(
            WorldSession session,
            PlayerItemSnapshotItem item,
            bool alternate = false)
    {
        var rightHandPosition = alternate ? 54 : 4;
        var rightHand = session.PlayerItems.EquipmentItems.FirstOrDefault(equipped => equipped.Position == rightHandPosition);
        if (ItemType.IsLegacyArrow(item.ItemId))
        {
            return rightHand is not null && ItemType.IsLegacyBow(rightHand.ItemId);
        }

        if (rightHand is not null &&
            ((ItemType.IsLegacyKnife(rightHand.ItemId) && !ItemType.IsLegacyKnife(item.ItemId)) ||
             (ItemType.IsLegacyDagger(rightHand.ItemId) && !ItemType.IsLegacyDagger(item.ItemId))))
        {
            return false;
        }

        if (rightHand is not null &&
            ItemType.IsLegacyTwoHandWeapon(rightHand.ItemId) &&
            !ItemType.IsLegacyShield(item.ItemId))
        {
            return false;
        }

        if (ItemType.IsLegacyHossu(item.ItemId))
        {
            return rightHand is not null && IsLegacyTaoistClass(session.Character.Class);
        }

        if (!ItemType.IsLegacyShield(item.ItemId) ||
            !CanLegacyClassEquipShield(session.Character.Class) ||
            IsLegacyShieldBlockedClass(session.Character.Class))
        {
            return false;
        }

        return rightHand is not null;
    }

    private static bool HasLegacyDualWieldClass(long classCode)
    {
        var primary = classCode is >= 1001 and <= 8057 || IsLegacyWindWalkerClass(classCode);
        var secondary = IsLegacyTrojanClass(classCode) ||
            IsLegacyWarriorClass(classCode) ||
            classCode is >= 5001 and <= 14057 ||
            IsLegacyWindWalkerClass(classCode);

        return primary && secondary;
    }

    private static bool HasLegacyAlternateDualWieldClass(long classCode, uint itemId)
    {
        var primary = classCode is >= 1001 and <= 8057;
        var secondary = IsLegacyTrojanClass(classCode) ||
            IsLegacyWarriorClass(classCode) ||
            classCode is >= 5001 and <= 8057;
        if (IsLegacyArcherClass(classCode) && ItemType.IsLegacyKnife(itemId))
        {
            secondary = true;
        }

        return primary && secondary;
    }

    private static bool HasLegacyNaturalLeftHandItemClass(long classCode)
    {
        return classCode is >= 1001 and <= 8057 ||
            IsLegacyTrojanClass(classCode) ||
            IsLegacyWarriorClass(classCode) ||
            classCode is >= 5001 and <= 14057 ||
            IsLegacyWindWalkerClass(classCode);
    }

    private static bool HasLegacyAlternateNaturalLeftHandItemClass(long classCode)
    {
        return classCode is >= 1001 and <= 8057 ||
            IsLegacyTrojanClass(classCode) ||
            IsLegacyWarriorClass(classCode) ||
            classCode is >= 5001 and <= 8057 ||
            IsLegacyArcherClass(classCode);
    }

    private static bool CanLegacyClassEquipShield(long classCode)
    {
        return IsLegacyWarriorClass(classCode) || IsLegacyMonkClass(classCode);
    }

    private static bool CanLegacyClassEquipHookScythe(WorldSession session)
    {
        return IsLegacyNinjaClass(session.Character.Class) ||
            session.PlayerSpells.ContainsSpell(LegacyHookScytheExceptionSpellId);
    }

    private static bool IsLegacyShieldBlockedClass(long classCode)
    {
        return IsLegacyTaoistClass(classCode);
    }

    private static bool IsLegacyTaoistClass(long classCode)
    {
        return classCode is >= 10000 and <= 14057;
    }

    private static bool IsLegacyTrojanClass(long classCode)
    {
        return classCode is >= 1000 and <= 1057;
    }

    private static bool IsLegacyWarriorClass(long classCode)
    {
        return classCode is >= 2000 and <= 2057;
    }

    private static bool IsLegacyMonkClass(long classCode)
    {
        return classCode is >= 6000 and <= 6057;
    }

    private static bool IsLegacyArcherClass(long classCode)
    {
        return classCode is >= 4000 and <= 4057;
    }

    private static bool IsLegacyNinjaClass(long classCode)
    {
        return classCode is >= 5000 and <= 5057;
    }

    private static bool IsLegacyWindWalkerClass(long classCode)
    {
        return classCode is >= 16000 and <= 16057;
    }
}
