using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyVendorPurchasePlannerTests
{
    [Fact]
    public void PlanTransfersGoldAndExactSellerItemAtTheLastFreeInventorySlot()
    {
        var listedItem = Item(90_001, 410_339);
        var buyerItems = Items(
            Enumerable.Range(1, 39)
                .Select(index => Item((uint)index, (uint)(700_000 + index)))
                .ToArray(),
            containerUidBase: 10_000);
        var sellerRemainder = Item(90_002, 108_8001);
        var sellerItems = Items([listedItem, sellerRemainder], containerUidBase: 20_000);
        var buyer = Session(1_001, money: 500, conquerPoints: 75, buyerItems);
        var seller = Session(2_002, money: 125, conquerPoints: 25, sellerItems);

        var result = LegacyVendorPurchasePlanner.Plan(
            buyer,
            seller,
            listedItem,
            LegacyVendorPurchaseCurrencyMode.Gold,
            cost: 300);

        Assert.True(result.Succeeded);
        Assert.Equal(LegacyVendorPurchaseFailureReason.None, result.FailureReason);
        Assert.Equal(200, result.UpdatedBuyerSession.Character.Money);
        Assert.Equal(425, result.UpdatedSellerSession.Character.Money);
        Assert.Equal(75, result.UpdatedBuyerSession.Character.ConquerPoints);
        Assert.Equal(25, result.UpdatedSellerSession.Character.ConquerPoints);
        Assert.Equal(40, result.UpdatedBuyerSession.PlayerItems.InventoryItems.Count);
        Assert.Same(listedItem, result.UpdatedBuyerSession.PlayerItems.InventoryItems[^1]);
        Assert.Same(
            sellerRemainder,
            Assert.Single(result.UpdatedSellerSession.PlayerItems.InventoryItems));

        Assert.Equal(39, buyer.PlayerItems.InventoryItems.Count);
        Assert.Equal(2, seller.PlayerItems.InventoryItems.Count);
        AssertNonInventoryContainersPreserved(buyer, result.UpdatedBuyerSession);
        AssertNonInventoryContainersPreserved(seller, result.UpdatedSellerSession);
    }

    [Fact]
    public void PlanTransfersConquerPointsAndMovesTheActiveSellerSnapshotWithTheListedUid()
    {
        var listedItem = Item(90_101, 410_339);
        var activeSellerItem = Item(90_101, 410_339);
        var buyer = Session(1_101, money: 1_000, conquerPoints: 700, Items([]));
        var seller = Session(2_102, money: 2_000, conquerPoints: 200, Items([activeSellerItem]));

        var result = LegacyVendorPurchasePlanner.Plan(
            buyer,
            seller,
            listedItem,
            LegacyVendorPurchaseCurrencyMode.ConquerPoints,
            cost: 450);

        Assert.True(result.Succeeded);
        Assert.Equal(250, result.UpdatedBuyerSession.Character.ConquerPoints);
        Assert.Equal(650, result.UpdatedSellerSession.Character.ConquerPoints);
        Assert.Equal(1_000, result.UpdatedBuyerSession.Character.Money);
        Assert.Equal(2_000, result.UpdatedSellerSession.Character.Money);
        Assert.Same(activeSellerItem, Assert.Single(result.UpdatedBuyerSession.PlayerItems.InventoryItems));
        Assert.Empty(result.UpdatedSellerSession.PlayerItems.InventoryItems);
    }

    [Fact]
    public void PlanRejectsTheSamePlayerWithoutMutation()
    {
        var listedItem = Item(91_001, 410_339);
        var buyer = Session(3_001, money: 100, conquerPoints: 100, Items([listedItem]));
        var seller = Session(3_001, money: 50, conquerPoints: 50, Items([listedItem]));

        var result = LegacyVendorPurchasePlanner.Plan(
            buyer,
            seller,
            listedItem,
            LegacyVendorPurchaseCurrencyMode.Gold,
            cost: 10);

        AssertFailure(result, LegacyVendorPurchaseFailureReason.BuyerIsSeller, buyer, seller);
    }

    [Fact]
    public void PlanRejectsAnItemTheSellerNoLongerOwnsWithoutMutation()
    {
        var listedItem = Item(91_101, 410_339);
        var buyer = Session(3_101, money: 100, conquerPoints: 100, Items([]));
        var seller = Session(3_102, money: 50, conquerPoints: 50, Items([Item(91_102, 410_339)]));

        var result = LegacyVendorPurchasePlanner.Plan(
            buyer,
            seller,
            listedItem,
            LegacyVendorPurchaseCurrencyMode.Gold,
            cost: 10);

        AssertFailure(result, LegacyVendorPurchaseFailureReason.SellerDoesNotOwnItem, buyer, seller);
    }

    [Fact]
    public void PlanRejectsAnAmbiguousSellerItemUidWithoutMutation()
    {
        var listedItem = Item(91_201, 410_339);
        var buyer = Session(3_201, money: 100, conquerPoints: 100, Items([]));
        var seller = Session(
            3_202,
            money: 50,
            conquerPoints: 50,
            Items([listedItem, Item(listedItem.Uid, 410_340)]));

        var result = LegacyVendorPurchasePlanner.Plan(
            buyer,
            seller,
            listedItem,
            LegacyVendorPurchaseCurrencyMode.Gold,
            cost: 10);

        AssertFailure(result, LegacyVendorPurchaseFailureReason.SellerItemUidIsNotUnique, buyer, seller);
    }

    [Fact]
    public void PlanRejectsAFullBuyerInventoryWithoutMutation()
    {
        var listedItem = Item(91_301, 410_339);
        var fullInventory = Enumerable.Range(1, 40)
            .Select(index => Item((uint)index, (uint)(700_000 + index)))
            .ToArray();
        var buyer = Session(3_301, money: 100, conquerPoints: 100, Items(fullInventory));
        var seller = Session(3_302, money: 50, conquerPoints: 50, Items([listedItem]));

        var result = LegacyVendorPurchasePlanner.Plan(
            buyer,
            seller,
            listedItem,
            LegacyVendorPurchaseCurrencyMode.Gold,
            cost: 10);

        AssertFailure(result, LegacyVendorPurchaseFailureReason.BuyerInventoryFull, buyer, seller);
    }

    [Fact]
    public void PlanRejectsAnItemUidAlreadyInTheBuyerInventoryWithoutMutation()
    {
        var listedItem = Item(91_401, 410_339);
        var buyer = Session(
            3_401,
            money: 100,
            conquerPoints: 100,
            Items([Item(listedItem.Uid, 410_340)]));
        var seller = Session(3_402, money: 50, conquerPoints: 50, Items([listedItem]));

        var result = LegacyVendorPurchasePlanner.Plan(
            buyer,
            seller,
            listedItem,
            LegacyVendorPurchaseCurrencyMode.Gold,
            cost: 10);

        AssertFailure(result, LegacyVendorPurchaseFailureReason.BuyerAlreadyOwnsItemUid, buyer, seller);
    }

    [Theory]
    [InlineData(LegacyVendorPurchaseCurrencyMode.Gold)]
    [InlineData(LegacyVendorPurchaseCurrencyMode.ConquerPoints)]
    public void PlanRejectsInsufficientBuyerBalanceWithoutMutation(
        LegacyVendorPurchaseCurrencyMode currencyMode)
    {
        var listedItem = Item(91_501, 410_339);
        var buyer = Session(3_501, money: 9, conquerPoints: 9, Items([]));
        var seller = Session(3_502, money: 50, conquerPoints: 50, Items([listedItem]));

        var result = LegacyVendorPurchasePlanner.Plan(
            buyer,
            seller,
            listedItem,
            currencyMode,
            cost: 10);

        AssertFailure(result, LegacyVendorPurchaseFailureReason.InsufficientBuyerBalance, buyer, seller);
    }

    [Fact]
    public void PlanRejectsANegativeCostWithoutMutation()
    {
        var listedItem = Item(91_601, 410_339);
        var buyer = Session(3_601, money: 100, conquerPoints: 100, Items([]));
        var seller = Session(3_602, money: 50, conquerPoints: 50, Items([listedItem]));

        var result = LegacyVendorPurchasePlanner.Plan(
            buyer,
            seller,
            listedItem,
            LegacyVendorPurchaseCurrencyMode.Gold,
            cost: -1);

        AssertFailure(result, LegacyVendorPurchaseFailureReason.NegativeCost, buyer, seller);
    }

    [Theory]
    [InlineData(true, LegacyVendorPurchaseFailureReason.BuyerBalanceIsNegative)]
    [InlineData(false, LegacyVendorPurchaseFailureReason.SellerBalanceIsNegative)]
    public void PlanRejectsNegativeWalletStateWithoutMutation(
        bool buyerIsNegative,
        LegacyVendorPurchaseFailureReason expectedFailure)
    {
        var listedItem = Item(91_701, 410_339);
        var buyer = Session(3_701, buyerIsNegative ? -1 : 100, conquerPoints: 100, Items([]));
        var seller = Session(3_702, buyerIsNegative ? 50 : -1, conquerPoints: 50, Items([listedItem]));

        var result = LegacyVendorPurchasePlanner.Plan(
            buyer,
            seller,
            listedItem,
            LegacyVendorPurchaseCurrencyMode.Gold,
            cost: 0);

        AssertFailure(result, expectedFailure, buyer, seller);
    }

    [Theory]
    [InlineData(LegacyVendorPurchaseCurrencyMode.Gold)]
    [InlineData(LegacyVendorPurchaseCurrencyMode.ConquerPoints)]
    public void PlanRejectsSellerWalletOverflowWithoutMutation(
        LegacyVendorPurchaseCurrencyMode currencyMode)
    {
        var listedItem = Item(91_801, 410_339);
        var buyer = Session(3_801, money: long.MaxValue, conquerPoints: long.MaxValue, Items([]));
        var seller = Session(3_802, money: long.MaxValue, conquerPoints: long.MaxValue, Items([listedItem]));

        var result = LegacyVendorPurchasePlanner.Plan(
            buyer,
            seller,
            listedItem,
            currencyMode,
            cost: 1);

        AssertFailure(result, LegacyVendorPurchaseFailureReason.SellerBalanceOverflow, buyer, seller);
    }

    [Fact]
    public void PlanRejectsAnUnsupportedCurrencyModeWithoutMutation()
    {
        var listedItem = Item(91_901, 410_339);
        var buyer = Session(3_901, money: 100, conquerPoints: 100, Items([]));
        var seller = Session(3_902, money: 50, conquerPoints: 50, Items([listedItem]));

        var result = LegacyVendorPurchasePlanner.Plan(
            buyer,
            seller,
            listedItem,
            (LegacyVendorPurchaseCurrencyMode)99,
            cost: 10);

        AssertFailure(result, LegacyVendorPurchaseFailureReason.UnsupportedCurrencyMode, buyer, seller);
    }

    private static void AssertFailure(
        LegacyVendorPurchasePlan result,
        LegacyVendorPurchaseFailureReason expectedFailure,
        WorldSession buyer,
        WorldSession seller)
    {
        Assert.False(result.Succeeded);
        Assert.Equal(expectedFailure, result.FailureReason);
        Assert.Same(buyer, result.UpdatedBuyerSession);
        Assert.Same(seller, result.UpdatedSellerSession);
    }

    private static void AssertNonInventoryContainersPreserved(
        WorldSession original,
        WorldSession updated)
    {
        Assert.Same(original.PlayerItems.RelicFuseItems, updated.PlayerItems.RelicFuseItems);
        Assert.Same(original.PlayerItems.MythsoulBagItems, updated.PlayerItems.MythsoulBagItems);
        Assert.Same(original.PlayerItems.EquipmentItems, updated.PlayerItems.EquipmentItems);
        Assert.Same(original.PlayerItems.RuneItems, updated.PlayerItems.RuneItems);
        Assert.Same(original.PlayerItems.WardrobeItems, updated.PlayerItems.WardrobeItems);
        Assert.Same(original.PlayerItems.WarehouseItems, updated.PlayerItems.WarehouseItems);
    }

    private static WorldSession Session(
        long uid,
        long money,
        long conquerPoints,
        PlayerItemSnapshot items)
    {
        return new WorldSession(
            AccountId: uid,
            Username: $"vendor-{uid}",
            Character: new CharacterSnapshot(
                Uid: uid,
                Name: $"Vendor{uid}",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 15,
                FirstClass: 15,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: 1_002,
                X: 430,
                Y: 378,
                PreviousMapId: 1_002,
                PreviousMapX: 430,
                PreviousMapY: 378,
                Strength: 0,
                Agility: 0,
                Vitality: 0,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: 1,
                MinMana: 0,
                Money: money,
                ConquerPoints: conquerPoints,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 1,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(),
                PaidNobility: new Dictionary<string, string>()),
            PlayerItems: items,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static PlayerItemSnapshot Items(
        IReadOnlyList<PlayerItemSnapshotItem> inventory,
        uint containerUidBase = 30_000)
    {
        return new PlayerItemSnapshot(
            InventoryItems: inventory,
            RelicFuseItems: [Item(containerUidBase + 1, 800_001, position: 213)],
            MythsoulBagItems: [Item(containerUidBase + 2, 800_002, position: 217)],
            EquipmentItems: [Item(containerUidBase + 3, 410_339, position: 4)],
            RuneItems: [Item(containerUidBase + 4, 4_030_109, position: 101)],
            WardrobeItems: [Item(containerUidBase + 5, 200_614, warehouseId: 100)])
        {
            WarehouseItems = [Item(containerUidBase + 6, 1_088_001, warehouseId: 6)],
        };
    }

    private static PlayerItemSnapshotItem Item(
        uint uid,
        uint itemId,
        ushort position = 0,
        uint warehouseId = 0)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Durability = 100,
            MaximumDurability = 100,
            Position = position,
            StackSize = 1,
            WarehouseId = warehouseId,
        };
    }
}
