using System.Globalization;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.OwnedEquipmentBehaviorFixture;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class OwnedVendorListingIncarnationTests
{
    private const uint Listed = 3101, Child = 3102, Extra = 3103, Sash = 1_100_009, Plain = 1_088_001;

    private static Task Run(bool childInSash, bool damaged, Func<Fixture, Task> body)
    {
        var child = new PlayerItemSnapshotItem { Uid = Child, ItemId = Plain, StackSize = 1, Durability = 1, MaximumDurability = 1 };
        var sash = new PlayerItemSnapshotItem { Uid = Listed, ItemId = Sash, StackSize = 1,
            Durability = damaged ? (ushort)5 : (ushort)10, MaximumDurability = 10,
            DepositedItems = childInSash ? [child] : [] };
        var inventory = new List<PlayerItemSnapshotItem> { sash,
            new() { Uid = Extra, ItemId = Plain, StackSize = 1, Durability = 1, MaximumDurability = 1 } };
        if (!childInSash) inventory.Add(child);
        var seller = Fixture.Character(minHitPoints: 1000, reborn: 0) with { Money = 1000, ConquerPoints = 100 };
        var buyer = Fixture.Character(Fixture.Peer, "VendorBuyer", x: 831, minHitPoints: 1000, reborn: 0)
            with { Money = 2000, ConquerPoints = 200 };
        var catalog = Fixture.Catalog(Fixture.Item(Sash, fields: new Dictionary<int, string>
            { [ItemType.GoldWorthFieldIndex] = "100", [ItemType.DurabilityFieldIndex] = "10" }), Fixture.Item(Plain));
        return Fixture.RunAsync(new(seller, new(inventory, [], [], [], [])), catalog, body,
            new OwnedEquipmentBehaviorInput(buyer, PlayerItemSnapshot.Empty));
    }

    private static MsgItemUsagePacket Listing() => new(Listed, 125, MsgItemUsageAction.AddVendingItemGold, 1234, 0, 0, 0);
    private static MsgItemUsagePacket Remove() => new(Listed, 0, MsgItemUsageAction.RemoveVendingItem, 1234, 0, 0, 0);
    private static MsgItemUsagePacket Buy(uint booth) => new(Listed, booth, MsgItemUsageAction.BuyVendingItem, 1234, 0, 0, 0);
    private static MsgItemUsagePacket Show(uint booth) => new(booth, 0, MsgItemUsageAction.ShowVendingList, 1234, 0, 0, 0);
    private static Task Move(Fixture f, uint uid, LegacyWarehouseAction action) => f.SendAsync(Fixture.Actor,
        new LegacyWarehousePacket(8, action, uid, 0, []).ToFrameBytes());

    private static async Task<uint> StartAndList(Fixture f)
    {
        await f.SendAsync(Fixture.Actor, new MsgActionPacket { ObjId = Fixture.Actor, Type = MsgActionType.StartVendor }.ToFrameBytes());
        Assert.True(uint.TryParse(f.Current().Character.Attributes["RuntimeVendorUid"], NumberStyles.None, CultureInfo.InvariantCulture, out var booth));
        Assert.NotEqual(0U, booth);
        var frames = f.ActorFrames.Count;
        await f.SendAsync(Fixture.Actor, Listing().ToFrameBytes());
        Assert.Equal(Listing().ToFrameBytes(), Assert.Single(f.ActorFrames.Skip(frames)));
        Assert.True(f.Current().LegacyVendorListings!.Contains(Listed));
        return booth;
    }

    private static async Task AssertShown(Fixture f, uint booth)
    {
        var start = f.Frames(Fixture.Peer).Count;
        await f.SendAsync(Fixture.Peer, Show(booth).ToFrameBytes());
        var frame = Assert.Single(f.Frames(Fixture.Peer).Skip(start));
        Assert.True(LegacyItemViewPacket.TryParse(Fixture.ReadFrame(frame), out var view));
        Assert.Equal(Listed, view.Uid); Assert.Equal(booth, view.PlayerUid);
        Assert.Equal(125L, view.Cost); Assert.Equal((ushort)LegacyItemViewActionMode.Gold, view.Mode);
    }

    private sealed record Before(WorldSession Seller, WorldSession Buyer, string SellerState, string BuyerState,
        byte[] SellerItems, byte[] BuyerItems, Dictionary<string, byte[]> Files, int SellerFrames, int BuyerFrames)
    {
        internal static Before Capture(Fixture f) => new(f.Current(), f.Current(Fixture.Peer),
            JsonSerializer.Serialize(f.Current().Character), JsonSerializer.Serialize(f.Current(Fixture.Peer).Character),
            PlayerItemSnapshotLoader.ToLegacyBytes(f.Current().PlayerItems),
            PlayerItemSnapshotLoader.ToLegacyBytes(f.Current(Fixture.Peer).PlayerItems),
            f.Data.Files(), f.ActorFrames.Count, f.Frames(Fixture.Peer).Count);
        internal void Unchanged(Fixture f)
        {
            Assert.Same(Seller, f.Current()); Assert.Same(Buyer, f.Current(Fixture.Peer));
            Assert.Equal(SellerState, JsonSerializer.Serialize(f.Current().Character));
            Assert.Equal(BuyerState, JsonSerializer.Serialize(f.Current(Fixture.Peer).Character));
            Assert.Equal(SellerItems, PlayerItemSnapshotLoader.ToLegacyBytes(f.Current().PlayerItems));
            Assert.Equal(BuyerItems, PlayerItemSnapshotLoader.ToLegacyBytes(f.Current(Fixture.Peer).PlayerItems));
            Assert.Equal(SellerFrames, f.ActorFrames.Count); Assert.Equal(BuyerFrames, f.Frames(Fixture.Peer).Count);
            f.Data.AssertFiles(Files); AssertActors(f);
        }
    }

    private static void AssertActors(Fixture f)
    {
        foreach (var uid in new[] { Fixture.Actor, Fixture.Peer })
        {
            var session = f.Current(uid); var native = session.NativeGameClient!;
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(native.OrdinarySpellBinding);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.True(binding.Items.PhysicalOwnerIsCurrentPlain()); Assert.True(binding.Items.Image.IsCurrentPlain());
            Assert.Same(session.PlayerItems, binding.Items.Image.FinalSnapshot);
            Assert.Equal(session.PlayerItems.InventoryItems.Select(row => row.Uid).Order(), native.Inventory!.ClientItems.Keys.Order());
            Assert.Equal(session.Character.Money, native.Player!.Money);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(session.PlayerItems), File.ReadAllBytes(f.ItemPath(uid)));
        }
        Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
    }
}
