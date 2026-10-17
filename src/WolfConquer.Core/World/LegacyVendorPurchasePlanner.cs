using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public enum LegacyVendorPurchaseCurrencyMode
{
    Gold = 0,
    ConquerPoints = 1,
}

public enum LegacyVendorPurchaseFailureReason
{
    None = 0,
    BuyerIsSeller,
    SellerDoesNotOwnItem,
    SellerItemUidIsNotUnique,
    BuyerInventoryFull,
    BuyerAlreadyOwnsItemUid,
    NegativeCost,
    UnsupportedCurrencyMode,
    BuyerBalanceIsNegative,
    SellerBalanceIsNegative,
    InsufficientBuyerBalance,
    SellerBalanceOverflow,
}

public sealed record LegacyVendorPurchasePlan(
    LegacyVendorPurchaseFailureReason FailureReason,
    WorldSession UpdatedBuyerSession,
    WorldSession UpdatedSellerSession)
{
    public bool Succeeded => FailureReason == LegacyVendorPurchaseFailureReason.None;
}

public static class LegacyVendorPurchasePlanner
{
    private const int LegacyInventorySlots = 40;

    public static LegacyVendorPurchasePlan Plan(
        WorldSession buyer,
        WorldSession seller,
        PlayerItemSnapshotItem listedItem,
        LegacyVendorPurchaseCurrencyMode currencyMode,
        long cost)
    {
        ArgumentNullException.ThrowIfNull(buyer);
        ArgumentNullException.ThrowIfNull(seller);
        ArgumentNullException.ThrowIfNull(listedItem);

        if (ReferenceEquals(buyer, seller) || buyer.Character.Uid == seller.Character.Uid)
        {
            return Failed(LegacyVendorPurchaseFailureReason.BuyerIsSeller, buyer, seller);
        }

        var sellerItemIndex = -1;
        for (var index = 0; index < seller.PlayerItems.InventoryItems.Count; index++)
        {
            if (seller.PlayerItems.InventoryItems[index].Uid != listedItem.Uid)
            {
                continue;
            }

            if (sellerItemIndex >= 0)
            {
                return Failed(
                    LegacyVendorPurchaseFailureReason.SellerItemUidIsNotUnique,
                    buyer,
                    seller);
            }

            sellerItemIndex = index;
        }

        if (sellerItemIndex < 0)
        {
            return Failed(LegacyVendorPurchaseFailureReason.SellerDoesNotOwnItem, buyer, seller);
        }

        if (buyer.PlayerItems.InventoryItems.Count >= LegacyInventorySlots)
        {
            return Failed(LegacyVendorPurchaseFailureReason.BuyerInventoryFull, buyer, seller);
        }

        if (buyer.PlayerItems.InventoryItems.Any(item => item.Uid == listedItem.Uid))
        {
            return Failed(LegacyVendorPurchaseFailureReason.BuyerAlreadyOwnsItemUid, buyer, seller);
        }

        if (cost < 0)
        {
            return Failed(LegacyVendorPurchaseFailureReason.NegativeCost, buyer, seller);
        }

        long buyerBalance;
        long sellerBalance;
        switch (currencyMode)
        {
            case LegacyVendorPurchaseCurrencyMode.Gold:
                buyerBalance = buyer.Character.Money;
                sellerBalance = seller.Character.Money;
                break;
            case LegacyVendorPurchaseCurrencyMode.ConquerPoints:
                buyerBalance = buyer.Character.ConquerPoints;
                sellerBalance = seller.Character.ConquerPoints;
                break;
            default:
                return Failed(
                    LegacyVendorPurchaseFailureReason.UnsupportedCurrencyMode,
                    buyer,
                    seller);
        }

        if (buyerBalance < 0)
        {
            return Failed(LegacyVendorPurchaseFailureReason.BuyerBalanceIsNegative, buyer, seller);
        }

        if (sellerBalance < 0)
        {
            return Failed(LegacyVendorPurchaseFailureReason.SellerBalanceIsNegative, buyer, seller);
        }

        if (buyerBalance < cost)
        {
            return Failed(LegacyVendorPurchaseFailureReason.InsufficientBuyerBalance, buyer, seller);
        }

        if (sellerBalance > long.MaxValue - cost)
        {
            return Failed(LegacyVendorPurchaseFailureReason.SellerBalanceOverflow, buyer, seller);
        }

        var updatedBuyerBalance = checked(buyerBalance - cost);
        var updatedSellerBalance = checked(sellerBalance + cost);
        var updatedBuyerCharacter = currencyMode == LegacyVendorPurchaseCurrencyMode.Gold
            ? buyer.Character with { Money = updatedBuyerBalance }
            : buyer.Character with { ConquerPoints = updatedBuyerBalance };
        var updatedSellerCharacter = currencyMode == LegacyVendorPurchaseCurrencyMode.Gold
            ? seller.Character with { Money = updatedSellerBalance }
            : seller.Character with { ConquerPoints = updatedSellerBalance };

        var sellerItem = seller.PlayerItems.InventoryItems[sellerItemIndex];
        var updatedBuyerInventory = buyer.PlayerItems.InventoryItems
            .Append(sellerItem)
            .ToArray();
        var updatedSellerInventory = seller.PlayerItems.InventoryItems
            .Where((_, index) => index != sellerItemIndex)
            .ToArray();

        var updatedBuyer = buyer with
        {
            Character = updatedBuyerCharacter,
            PlayerItems = buyer.PlayerItems with { InventoryItems = updatedBuyerInventory },
        };
        var updatedSeller = seller with
        {
            Character = updatedSellerCharacter,
            PlayerItems = seller.PlayerItems with { InventoryItems = updatedSellerInventory },
        };

        return new LegacyVendorPurchasePlan(
            LegacyVendorPurchaseFailureReason.None,
            updatedBuyer,
            updatedSeller);
    }

    public static bool TryPlan(
        WorldSession buyer,
        WorldSession seller,
        PlayerItemSnapshotItem listedItem,
        LegacyVendorPurchaseCurrencyMode currencyMode,
        long cost,
        out LegacyVendorPurchasePlan plan)
    {
        plan = Plan(buyer, seller, listedItem, currencyMode, cost);
        return plan.Succeeded;
    }

    private static LegacyVendorPurchasePlan Failed(
        LegacyVendorPurchaseFailureReason reason,
        WorldSession buyer,
        WorldSession seller)
    {
        return new LegacyVendorPurchasePlan(reason, buyer, seller);
    }
}
