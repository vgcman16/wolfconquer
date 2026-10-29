using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedEquipmentWeaponBehaviorTests
{
    // Original method name, item, class, request, right item/slot, left item/slot, expected slot.
    public static IEnumerable<object[]> AcceptedWeaponCases =>
    [
        ["HandlerItemUsageEquipMovesInventoryWeaponToAlternateRightHandSlot", 410003u, 7051L, (ushort)54, 0u, (ushort)0, 0u, (ushort)0, (ushort)54],
        ["HandlerItemUsageEquipMovesAlternateLeftArrowWhenAlternateRightBowIsEquipped", 1050000u, 7051L, (ushort)55, 500109u, (ushort)54, 0u, (ushort)0, (ushort)55],
        ["HandlerItemUsageEquipMovesArcherKnifeToAlternateLeftHandWhenAlternateRightHandIsEquipped", 613000u, 4005L, (ushort)55, 410003u, (ushort)54, 0u, (ushort)0, (ushort)55],
        ["HandlerItemUsageEquipMovesAlternatePistolWhenNormalRapierAndAlternateRightHandAreEquipped", 612000u, 7051L, (ushort)55, 611000u, (ushort)4, 410003u, (ushort)54, (ushort)55],
        ["HandlerItemUsageEquipMovesRightHandWeaponInventoryItemToRequestedWeaponSlot", 500109u, 7051L, (ushort)4, 0u, (ushort)0, 0u, (ushort)0, (ushort)4],
        ["HandlerItemUsageEquipMovesKnifeToRightHandForArcherClass", 613000u, 4005L, (ushort)4, 0u, (ushort)0, 0u, (ushort)0, (ushort)4],
        ["HandlerItemUsageEquipMovesHookScytheToRightHandForNinjaClass", 511000u, 5005L, (ushort)4, 0u, (ushort)0, 0u, (ushort)0, (ushort)4],
        ["HandlerItemUsageEquipMovesRightHandWeaponInventoryItemToRequestedLeftHandWeaponSlotForDualWieldClass", 410003u, 7051L, (ushort)5, 410003u, (ushort)4, 0u, (ushort)0, (ushort)5],
        ["HandlerItemUsageEquipMovesShieldToLeftHandWhenRightHandIsEquipped", 900000u, 2057L, (ushort)5, 410003u, (ushort)4, 0u, (ushort)0, (ushort)5],
        ["HandlerItemUsageEquipMovesShieldToLeftHandWhenRightHandBackswordIsEquipped", 900000u, 2005L, (ushort)5, 421003u, (ushort)4, 0u, (ushort)0, (ushort)5],
        ["HandlerItemUsageEquipMovesHossuToLeftHandForTaoistWhenRightHandIsEquipped", 619000u, 13002L, (ushort)5, 410003u, (ushort)4, 0u, (ushort)0, (ushort)5],
        ["HandlerItemUsageEquipMovesPistolToLeftHandWhenRapierRightHandIsEquipped", 612000u, 7051L, (ushort)5, 611000u, (ushort)4, 0u, (ushort)0, (ushort)5],
        ["HandlerItemUsageEquipMovesArrowToLeftHandWhenBowIsEquipped", 1050000u, 7051L, (ushort)5, 500109u, (ushort)4, 0u, (ushort)0, (ushort)5],
    ];

    public static IEnumerable<object[]> RefusedWeaponCases =>
    [
        ["HandlerItemUsageEquipSkipsAlternateLeftArrowWhenAlternateRightHandIsEmpty", 1050000u, 7051L, (ushort)55, 0u, (ushort)0, 0u, (ushort)0],
        ["HandlerItemUsageEquipSkipsWindWalkerRightHandWeaponInAlternateLeftHand", 410003u, 16000L, (ushort)55, 410003u, (ushort)54, 0u, (ushort)0],
        ["HandlerItemUsageEquipSkipsAlternatePistolWhenNormalRapierIsMissing", 612000u, 7051L, (ushort)55, 611000u, (ushort)54, 0u, (ushort)0],
        ["HandlerItemUsageEquipSkipsHookScytheForNonNinjaWithoutPerseveranceSpell", 511000u, 7051L, (ushort)4, 0u, (ushort)0, 0u, (ushort)0],
        ["HandlerItemUsageEquipSkipsKnifeForNonArcherClass", 613000u, 7051L, (ushort)4, 0u, (ushort)0, 0u, (ushort)0],
        ["HandlerItemUsageEquipSkipsRightHandWeaponWhenLeftHandKnifeRequiresKnife", 410003u, 4005L, (ushort)4, 0u, (ushort)0, 613000u, (ushort)5],
        ["HandlerItemUsageEquipSkipsRightHandWeaponWhenLeftHandDaggerRequiresDagger", 410003u, 7051L, (ushort)4, 0u, (ushort)0, 490003u, (ushort)5],
        ["HandlerItemUsageEquipSkipsRightHandKnifeWhenLeftHandShieldIsEquipped", 613000u, 4005L, (ushort)4, 0u, (ushort)0, 900000u, (ushort)5],
        ["HandlerItemUsageEquipSkipsShieldWhenRightHandSlotIsFree", 900000u, 2005L, (ushort)5, 0u, (ushort)0, 0u, (ushort)0],
        ["HandlerItemUsageEquipSkipsShieldForNonWarriorNonMonkClass", 900000u, 7051L, (ushort)5, 410003u, (ushort)4, 0u, (ushort)0],
        ["HandlerItemUsageEquipSkipsShieldForTaoistWhenRightHandIsEquipped", 900000u, 14057L, (ushort)5, 410003u, (ushort)4, 0u, (ushort)0],
        ["HandlerItemUsageEquipSkipsHossuForNonTaoistWhenRightHandIsEquipped", 619000u, 7051L, (ushort)5, 410003u, (ushort)4, 0u, (ushort)0],
        ["HandlerItemUsageEquipSkipsHossuForTaoistWhenRightHandSlotIsFree", 619000u, 13002L, (ushort)5, 0u, (ushort)0, 0u, (ushort)0],
        ["HandlerItemUsageEquipSkipsNaturalLeftHandNonShieldWhenRightHandBackswordIsEquipped", 619000u, 13002L, (ushort)5, 421003u, (ushort)4, 0u, (ushort)0],
        ["HandlerItemUsageEquipSkipsNaturalLeftHandNonKnifeWhenRightHandKnifeIsEquipped", 619000u, 13002L, (ushort)5, 613000u, (ushort)4, 0u, (ushort)0],
        ["HandlerItemUsageEquipSkipsNaturalLeftHandNonDaggerWhenRightHandDaggerIsEquipped", 619000u, 13002L, (ushort)5, 490003u, (ushort)4, 0u, (ushort)0],
        ["HandlerItemUsageEquipSkipsNaturalLeftHandNonShieldWhenRightHandIsTwoHandWeapon", 619000u, 13002L, (ushort)5, 500109u, (ushort)4, 0u, (ushort)0],
        ["HandlerItemUsageEquipSkipsPistolWhenRightHandSlotIsFree", 612000u, 7051L, (ushort)5, 0u, (ushort)0, 0u, (ushort)0],
        ["HandlerItemUsageEquipSkipsPistolWhenRightHandIsNotRapier", 612000u, 7051L, (ushort)5, 410003u, (ushort)4, 0u, (ushort)0],
        ["HandlerItemUsageEquipSkipsPistolForTaoistEvenWithRapierRightHand", 612000u, 13002L, (ushort)5, 611000u, (ushort)4, 0u, (ushort)0],
        ["HandlerItemUsageEquipSkipsTwoHandWeaponRequestedInLeftHandWeaponSlot", 500109u, 7051L, (ushort)5, 0u, (ushort)0, 0u, (ushort)0],
        ["HandlerItemUsageEquipSkipsArrowWhenRightHandIsNotBow", 1050000u, 7051L, (ushort)5, 410003u, (ushort)4, 0u, (ushort)0],
    ];
}
