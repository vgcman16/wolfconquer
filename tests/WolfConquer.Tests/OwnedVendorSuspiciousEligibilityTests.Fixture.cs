using System.Globalization;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.OwnedEquipmentBehaviorFixture;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class OwnedVendorSuspiciousEligibilityTests
{
    private const uint ListedUid = 2101, ChildUid = 2102, SellerKeptUid = 2103, BuyerKeptUid = 2201;
    private const uint SashType = 1_100_009, OrdinaryType = 1_088_001;

    private static PlayerItemSnapshotItem Item(uint uid, uint type = OrdinaryType, byte suspicious = 0,
        params PlayerItemSnapshotItem[] children) => new()
    { Uid = uid, ItemId = type, Suspicious = suspicious, StackSize = 1, Durability = 1, MaximumDurability = 1,
      DepositedItems = children };

    private static Task Run(PlayerItemSnapshotItem[] seller, PlayerItemSnapshotItem[] buyer, Func<Fixture, Task> body)
    {
        var first = Fixture.Character(minHitPoints: 1000, reborn: 0) with { Money = 1000, ConquerPoints = 100 };
        var second = Fixture.Character(Fixture.Peer, "VendorBuyer", x: 831, minHitPoints: 1000, reborn: 0)
            with { Money = 2000, ConquerPoints = 200 };
        return Fixture.RunAsync(new(first, new(seller, [], [], [], [])),
            Fixture.Catalog(Fixture.Item(SashType), Fixture.Item(OrdinaryType)), body,
            new OwnedEquipmentBehaviorInput(second, new(buyer, [], [], [], [])));
    }

    private static async Task<uint> Start(Fixture f)
    {
        await f.SendAsync(Fixture.Actor, new MsgActionPacket { ObjId = Fixture.Actor, Type = MsgActionType.StartVendor }.ToFrameBytes());
        Assert.True(uint.TryParse(f.Current().Character.Attributes["RuntimeVendorUid"],
            NumberStyles.None, CultureInfo.InvariantCulture, out var booth));
        Assert.NotEqual(0U, booth); Assert.Empty(f.Current().LegacyVendorListings!.Listings);
        AssertActors(f);
        return booth;
    }

    private static MsgItemUsagePacket Listing(bool cp) => new(ListedUid, cp ? 25UL : 125UL,
        cp ? MsgItemUsageAction.AddVendingItemConquerPoints : MsgItemUsageAction.AddVendingItemGold, 3210, 0, 0, 0);
    private static MsgItemUsagePacket Purchase(uint booth) => new(ListedUid, booth, MsgItemUsageAction.BuyVendingItem, 3456, 2, 3, 4);

    private static async Task List(Fixture f, bool cp)
    {
        var request = Listing(cp); var first = f.ActorFrames.Count;
        await f.SendAsync(Fixture.Actor, request.ToFrameBytes());
        Assert.Equal(request.ToFrameBytes(), Assert.Single(f.ActorFrames.Skip(first)));
        Assert.True(f.Current().LegacyVendorListings!.TryGetListing(ListedUid, out var listing));
        Assert.Equal(cp ? LegacyVendorListingCostMode.CPs : LegacyVendorListingCostMode.Gold, listing.CostMode);
        Assert.Equal(cp ? 25L : 125L, listing.Amount);
    }

    private static void AssertActors(Fixture f)
    {
        foreach (var uid in new[] { Fixture.Actor, Fixture.Peer })
        {
            var owner = f.Current(uid); var native = owner.NativeGameClient!;
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(native.OrdinarySpellBinding);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.True(binding.Items.PhysicalOwnerIsCurrentPlain());
            Assert.True(binding.Items.Image.IsCurrentPlain()); Assert.Same(owner.PlayerItems, binding.Items.Image.FinalSnapshot);
            Assert.Equal(owner.PlayerItems.InventoryItems.Select(row => row.Uid).Order(), native.Inventory!.ClientItems.Keys.Order());
            Assert.Equal(owner.Character.Money, native.Player!.Money);
            Assert.Equal(checked((uint)owner.Character.ConquerPoints), native.Player.ConquerPoints);
            Assert.Equal(checked((int)owner.Character.BoundConquerPoints), native.Player.BoundConquerPoints);
            Assert.Equal(checked((ulong)owner.Character.BankConquerPoints), native.ConquerPointsSave);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(owner.PlayerItems), File.ReadAllBytes(f.ItemPath(uid)));
            var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath(uid)), out _));
            Assert.Equal(owner.Character.Money, saved.Money); Assert.Equal(owner.Character.ConquerPoints, saved.ConquerPoints);
        }
        Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
    }

    private sealed record Before(WorldSession Seller, WorldSession Buyer, string SellerCharacter, string BuyerCharacter,
        byte[] SellerItems, byte[] BuyerItems,
        object SellerNative, object BuyerNative, Dictionary<string, byte[]> Files, byte[] Registry, int SellerFrames, int BuyerFrames)
    {
        internal static Before Capture(Fixture f) => new(f.Current(), f.Current(Fixture.Peer),
            JsonSerializer.Serialize(f.Current().Character), JsonSerializer.Serialize(f.Current(Fixture.Peer).Character),
            PlayerItemSnapshotLoader.ToLegacyBytes(f.Current().PlayerItems), PlayerItemSnapshotLoader.ToLegacyBytes(f.Current(Fixture.Peer).PlayerItems),
            f.Current().NativeGameClient!.OrdinarySpellBinding!.Items.Image, f.Current(Fixture.Peer).NativeGameClient!.OrdinarySpellBinding!.Items.Image,
            f.Data.Files(), File.ReadAllBytes(f.Data.State), f.ActorFrames.Count, f.Frames(Fixture.Peer).Count);

        internal void AssertUnchanged(Fixture f)
        {
            Assert.Same(Seller, f.Current()); Assert.Same(Buyer, f.Current(Fixture.Peer));
            Assert.Equal(SellerCharacter, JsonSerializer.Serialize(f.Current().Character));
            Assert.Equal(BuyerCharacter, JsonSerializer.Serialize(f.Current(Fixture.Peer).Character));
            Assert.Equal(SellerItems, PlayerItemSnapshotLoader.ToLegacyBytes(f.Current().PlayerItems));
            Assert.Equal(BuyerItems, PlayerItemSnapshotLoader.ToLegacyBytes(f.Current(Fixture.Peer).PlayerItems));
            Assert.Same(SellerNative, f.Current().NativeGameClient!.OrdinarySpellBinding!.Items.Image);
            Assert.Same(BuyerNative, f.Current(Fixture.Peer).NativeGameClient!.OrdinarySpellBinding!.Items.Image);
            Assert.Equal(SellerFrames, f.ActorFrames.Count); Assert.Equal(BuyerFrames, f.Frames(Fixture.Peer).Count);
            f.Data.AssertFiles(Files); Assert.Equal(Registry, File.ReadAllBytes(f.Data.State)); AssertActors(f);
        }
    }
}
