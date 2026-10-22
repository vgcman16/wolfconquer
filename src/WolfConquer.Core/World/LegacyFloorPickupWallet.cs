using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed record LegacyFloorPickupWalletChange(CharacterSnapshot UpdatedCharacter,
    long SilverTransferred, uint CpsTransferred);

internal static class LegacyFloorPickupWallet
{
    private const long MoneyCap = 500_000_000_000;
    private const uint ConquerPointsCap = 2_000_000_000;

    // FloorMap adds native uint Gold/CPS (MsgItem.cs:38..40). Player.cs:2494..2560
    // caps Money after signed addition, but CPS addition wraps as uint BEFORE its
    // setter caps it. BankCps projects native GameClient.ConquerPointsSave (ulong)
    // into the existing signed snapshot; unsupported negative/overflow states deny.
    internal static bool TryAdd(CharacterSnapshot character, FloorItemRuntimePickupKind kind, long amount,
        out LegacyFloorPickupWalletChange change, out string reason)
    {
        change = null!;
        reason = "floorPickupCurrencyInvalid";
        if (character is null || amount is <= 0 or > uint.MaxValue ||
            character.Money < 0 || character.ConquerPoints is < 0 or > uint.MaxValue ||
            character.BankConquerPoints < 0 || kind is not (FloorItemRuntimePickupKind.Money or FloorItemRuntimePickupKind.ConquerPoints))
            return false;
        var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in character.Attributes)
            if (value is null || !attrs.TryAdd(key, value))
            { reason = "floorPickupAttributesAmbiguous"; return false; }
        if (!TryWarehouse(attrs, out var warehouse))
        { reason = "floorPickupWarehouseInvalidOrAmbiguous"; return false; }
        try
        {
            if (kind == FloorItemRuntimePickupKind.Money)
            {
                var total = checked(character.Money + amount);
                var transferred = Math.Max(0, total - MoneyCap);
                var money = total - transferred;
                var nextWarehouse = checked(warehouse + transferred);
                attrs["Money"] = money.ToString(CultureInfo.InvariantCulture);
                attrs["WHMoney"] = nextWarehouse.ToString(CultureInfo.InvariantCulture);
                if (attrs.ContainsKey("WarehouseMoney")) attrs["WarehouseMoney"] = attrs["WHMoney"];
                change = new(character with { Money = money, Attributes = attrs }, transferred, 0);
            }
            else
            {
                var total = unchecked((uint)character.ConquerPoints + (uint)amount);
                var transferred = total > ConquerPointsCap ? total - ConquerPointsCap : 0u;
                var cps = total - transferred;
                var bank = checked(character.BankConquerPoints + transferred);
                attrs["ConquerPoints"] = cps.ToString(CultureInfo.InvariantCulture);
                attrs["BankCps"] = bank.ToString(CultureInfo.InvariantCulture);
                change = new(character with { ConquerPoints = cps, BankConquerPoints = bank, Attributes = attrs }, 0, transferred);
            }
            reason = string.Empty;
            return true;
        }
        catch (OverflowException)
        { reason = "floorPickupCurrencyOverflow"; return false; }
    }

    private static bool TryWarehouse(IReadOnlyDictionary<string, string> fields, out long warehouse)
    {
        warehouse = 0;
        var hasCanonical = fields.TryGetValue("WHMoney", out var canonical);
        var hasAlias = fields.TryGetValue("WarehouseMoney", out var alias);
        long first = 0, second = 0;
        if (hasCanonical && (!long.TryParse(canonical, NumberStyles.Integer, CultureInfo.InvariantCulture, out first) || first < 0) ||
            hasAlias && (!long.TryParse(alias, NumberStyles.Integer, CultureInfo.InvariantCulture, out second) || second < 0) ||
            hasCanonical && hasAlias && first != second) return false;
        warehouse = hasCanonical ? first : hasAlias ? second : 0;
        return true;
    }
}
