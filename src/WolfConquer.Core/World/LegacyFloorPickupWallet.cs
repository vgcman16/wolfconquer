using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed record LegacyFloorPickupWalletChange(CharacterSnapshot UpdatedCharacter,
    long SilverTransferred, long CpsTransferred);

internal static class LegacyFloorPickupWallet
{
    private const long MoneyCap = 500_000_000_000;
    private const uint ConquerPointsCap = 2_000_000_000;

    // FloorMap inputs are native uint Gold/CPS (MsgItem.cs:38..40). Sanitize
    // the emulator's unchecked CP addition: preserve earned CP in a wide total
    // before the existing cap/bank transfer instead of silently wrapping it.
    // BankCps projects the actual UInt64 bank into the existing signed snapshot;
    // unsupported negative/overflow states deny before any claim or mutation.
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
                var total = checked(character.ConquerPoints + amount);
                var transferred = Math.Max(0L, total - ConquerPointsCap);
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
        return LegacyWarehouseMoneyState.TryRead(fields, out warehouse);
    }
}
