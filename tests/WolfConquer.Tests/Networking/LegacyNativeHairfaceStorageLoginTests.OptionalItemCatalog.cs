using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualNullCatalogFrameAdmitsEmptyOrNonRuneItemsAndKeepsExactNullableProvenance(bool nonRune)
    {
        using var input = new LoginInput();
        PrepareAdmissionInput(input);
        var saved = PlayerItemSnapshot.Empty with
        {
            InventoryItems = nonRune ? new[] { new PlayerItemSnapshotItem { Uid = 900001,
                ItemId = 725016, Durability = 1, MaximumDurability = 1, StackSize = 1 } } :
                Array.Empty<PlayerItemSnapshotItem>(),
        };
        var itemRoot = Path.Combine(AdmissionRoot(input), "PlayersItems");
        new PlayerItemSnapshotLoader(itemRoot).SaveByCharacterUid(Uid, saved);
        byte[] itemBytes = File.ReadAllBytes(Path.Combine(itemRoot, Uid + ".bin"));
        using var uids = new AdmissionUidFixture(input); var state = uids.StateBytes();
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            Assert.Null(handler.RequireOwnedNativeItemContext().Catalog);
            Assert.Equal(ConquerProtocolProfile.VisibleDx8D13, handler.RequireOwnedNativeItemContext().Profile);
            var (wire, birth) = await ConnectBornAsync(fixture);
            var accepted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            var client = birth.Connection.NativeGameClient!;
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(client.OrdinarySpellBinding);
            var store = AdmissionOwner<WorldSessionStore>(handler, "_sessions");
            Assert.Null(binding.ItemCatalog); Assert.Null(binding.Items.Image.Catalog);
            Assert.Same(accepted.PlayerItems, binding.ItemSnapshot);
            Assert.Same(accepted.PlayerItems, binding.Items.Image.FinalSnapshot);
            Assert.True(store.IsBoundSpellOwnerCurrent(binding));
            Assert.NotNull(client.Inventory); Assert.NotNull(client.Equipment); Assert.NotNull(client.Warehouse);
            Assert.NotNull(client.MyWardrobe); Assert.NotNull(client.Rune);
            Assert.NotNull(client.RelicFuse); Assert.NotNull(client.GearMythsoulBag);
            Assert.Equal(nonRune ? 1 : 0, client.Inventory!.ClientItems.Count);
            if (nonRune) Assert.Equal(725016u, Assert.Single(client.Inventory.ClientItems).Value.ITEM_ID);
            var image = binding.Items.Image;

            store.Set(birth.Connection, accepted with { ActiveNpcId = 123 }, out var final);

            Assert.Same(image, binding.Items.Image); Assert.Same(final.PlayerItems, binding.ItemSnapshot);
            Assert.Same(final.PlayerItems, binding.Items.Image.FinalSnapshot);
            Assert.True(store.IsBoundSpellOwnerCurrent(binding)); Assert.Null(binding.ItemCatalog);
            Assert.Equal(itemBytes, File.ReadAllBytes(Path.Combine(itemRoot, Uid + ".bin")));
            Assert.Equal(state, uids.StateBytes()); AssertNoOrdinaryAllocation(uids, state);
            Assert.False(LegacyTradeJournal.HasPending(input.Users));
            Assert.False(AdmissionOwner<LegacyTradeRecoveryState>(handler, "_tradeRecovery").IsBlocked);
        }, admissionUids: uids.Owner);
    }

    [Fact]
    public async Task ActualNullCatalogFrameRefusesRequiredRuneBeforeAdmissionOrNativePublication()
    {
        using var input = new LoginInput();
        PrepareAdmissionInput(input);
        var saved = PlayerItemSnapshot.Empty with
        {
            RuneItems = new[] { new PlayerItemSnapshotItem { Uid = 900001, ItemId = 4020517,
                Position = 102, Durability = 1, MaximumDurability = 1, StackSize = 1 } },
        };
        new PlayerItemSnapshotLoader(Path.Combine(AdmissionRoot(input), "PlayersItems")).SaveByCharacterUid(Uid, saved);
        using var uids = new AdmissionUidFixture(input);
        var state = uids.StateBytes(); var files = CaptureAdmissionFiles(input);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            Assert.Null(handler.RequireOwnedNativeItemContext().Catalog);
            var (wire, birth) = await ConnectBornAsync(fixture);
            var refused = await SendLoginAsync(fixture, wire);
            var client = birth.Connection.NativeGameClient!;

            Assert.Null(refused.After); Assert.Null(client.OrdinarySpellBinding);
            Assert.Equal(0u, client.Player.UID);
            Assert.Null(client.Inventory); Assert.Null(client.Equipment); Assert.Null(client.Warehouse);
            Assert.Null(client.MyWardrobe); Assert.Null(client.Rune);
            Assert.Null(client.RelicFuse); Assert.Null(client.GearMythsoulBag);
            AssertAdmissionFiles(files); Assert.Equal(state, uids.StateBytes());
            AssertNoOrdinaryAllocation(uids, state); Assert.False(LegacyTradeJournal.HasPending(input.Users));
        }, admissionUids: uids.Owner);
    }
}
