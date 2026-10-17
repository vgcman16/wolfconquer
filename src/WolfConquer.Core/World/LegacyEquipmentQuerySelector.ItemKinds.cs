namespace WolfConquer.Core.World;

internal static partial class LegacyEquipmentQuerySelector
{
    // Exact matching ItemType predicates; IsTwoHand and MsgGameItem.IsTwoHander
    // are intentionally DIFFERENT source predicates.
    private static bool IsPistol(uint id) => id is >= 612_000 and <= 612_439;
    private static bool IsRapier(uint id) => id is >= 611_000 and <= 611_439;
    private static bool IsKnife(uint id) => id is >= 613_000 and <= 613_429;
    private static bool IsDagger(uint id) => id is >= 490_003 and <= 490_149;
    private static bool IsShield(uint id) => id is >= 900_000 and <= 900_309;
    private static bool IsHossu(uint id) => id is >= 619_000 and <= 619_439;
    private static bool IsTaoistEpic(uint id) => id is >= 620_003 and <= 620_439;
    private static bool IsBacksword(uint id) => id is >= 421_003 and <= 421_439;
    private static bool IsArrow(uint id) => id is >= 1_050_000 and <= 1_051_000;
    private static bool IsBow(uint id) => id is >= 500_003 and <= 500_429;
    private static bool IsTwoHand(uint id) => id.ToString(System.Globalization.CultureInfo.InvariantCulture)[0] == '5' ||
        IsBacksword(id);
    private static bool IsTwoHander(uint id) => unchecked((ushort)(id / 1_000)) is >= 500 and <= 580;
}
