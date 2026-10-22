using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Native MsgFloorItem.MsgItemPacket.FloorMap gates InTrade/OnMyOwnServer
// before parsing; it does not check the independent secondary-password flag.
// Actual managed D13 admission only, not durable pickup/ADD/quest/client proof.
public sealed partial class LegacyNpcSaleTransactionTests
{
    private const uint AdmissionMoneyFloorUid = 9_201_001;
    private const uint AdmissionItemFloorUid = 9_201_002;
    private const uint AdmissionPickedItemUid = 601_029_001;

    [Theory]
    [InlineData(0U, false)]
    [InlineData(42U, true)]
    public async Task ConfiguredOwnServerAllowsMoneyAndKnownItemPickupEvenWithUnverifiedSecondaryPin(uint serverId, bool protectedPin)
    {
        const uint uid = 1_299_801;
        Seed(uid);
        if (protectedPin) ReplaceCharacterField(uid, "SecurityPass", "111,0,0,");
        var identity = new LegacyLocalServerIdentity(serverId);
        var owner = NewOwner(identity: identity);
        var player = protectedPin ? await LoginPinDrop(owner, uid) : await Login(owner, uid);
        var before = Current(owner, player);
        Assert.True(LegacyServerIdentityRuntime.IsOnOwnServer(player.Connection, before, identity));
        Assert.Equal(!protectedPin, player.Connection.NativeGameClient!.Player.IsCheckedPass);
        Assert.Equal(!protectedPin, LegacySecondaryPasswordRuntime.IsAdmitted(player.Connection, before));
        var item = SeedAdmissionFloor(owner, player);
        var first = player.Sent.Count;

        await PickupAdmissionFloor(owner, player, AdmissionMoneyFloorUid);

        var afterMoney = Current(owner, player);
        Assert.Equal(before.Character.Money + 125, afterMoney.Character.Money);
        Assert.Equal(before.Character.ConquerPoints, afterMoney.Character.ConquerPoints);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems),
            PlayerItemSnapshotLoader.ToLegacyBytes(afterMoney.PlayerItems));
        Assert.Contains(player.Sent.Skip(first), bytes => IsAdmissionFloorRemoval(bytes, AdmissionMoneyFloorUid));
        Assert.DoesNotContain(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow), entry => entry.Packet.Uid == AdmissionMoneyFloorUid);
        var beforeItem = Current(owner, player);
        first = player.Sent.Count;

        await PickupAdmissionFloor(owner, player, AdmissionItemFloorUid);

        var afterItem = Current(owner, player);
        // Native MsgGameItem.Send clears Plus on this ordinary non-equipment
        // item. The independent DB-byte oracle retains every other item field.
        var expectedItems = ExpectedPickupInventoryBytes(beforeItem.PlayerItems, item.WithPosition(0),
            merge: false, stack: item.StackSize);
        Assert.Equal(expectedItems, PlayerItemSnapshotLoader.ToLegacyBytes(afterItem.PlayerItems));
        Assert.Equal(expectedItems, File.ReadAllBytes(ItemPath(uid)));
        Assert.Equal(beforeItem.Character.Money, afterItem.Character.Money);
        Assert.Equal(beforeItem.Character.ConquerPoints, afterItem.Character.ConquerPoints);
        Assert.Contains(player.Sent.Skip(first), bytes => LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == item.Uid);
        Assert.Contains(player.Sent.Skip(first), bytes => IsAdmissionFloorRemoval(bytes, AdmissionItemFloorUid));
        Assert.Empty(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.Equal(!protectedPin, player.Connection.NativeGameClient.Player.IsCheckedPass);
        Assert.True(LegacyServerIdentityRuntime.IsOnOwnServer(player.Connection, afterItem, identity));
    }

    [Theory]
    [InlineData("missing-identity")]
    [InlineData("wrapped-identity")]
    [InlineData("forged-field")]
    [InlineData("replacement-player")]
    public async Task MissingWrappedForgedOrReplacementOwnServerCannotConsumeFloorEntries(string scenario)
    {
        const uint uid = 1_299_811;
        Seed(uid);
        var identity = scenario switch
        {
            "wrapped-identity" => new LegacyLocalServerIdentity(65_536),
            "replacement-player" => new LegacyLocalServerIdentity(42),
            _ => null,
        };
        var owner = NewOwner(identity: identity);
        var player = await Login(owner, uid);
        var client = player.Connection.NativeGameClient!;
        if (scenario == "forged-field")
        {
            client.Player.ServerID = 42;
            var current = Current(owner, player);
            var attributes = new Dictionary<string, string>(current.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            { ["ServerID"] = "42", ["MyServerID"] = "42", ["OnMyOwnServer"] = "1" };
            owner.Sessions.Set(player.Connection, current with { Character = current.Character with { Attributes = attributes } });
        }
        else if (scenario == "replacement-player")
        {
            Assert.True(LegacyServerIdentityRuntime.IsOnOwnServer(player.Connection, Current(owner, player), identity));
            var previous = client.Player;
            client.Player = new LegacyNativePlayerRuntime(client)
            { UID = uid, ServerID = 42, IsCheckedPass = true };
            Assert.NotSame(previous, client.Player);
        }
        Assert.False(LegacyServerIdentityRuntime.IsOnOwnServer(player.Connection, Current(owner, player), identity));
        SeedAdmissionFloor(owner, player);

        await AssertAdmissionFloorDenied(owner, player, [AdmissionMoneyFloorUid, AdmissionItemFloorUid], beforeParse: true);
    }

    [Fact]
    public async Task CurrentTradeActiveProjectionBlocksFloorPickupWithoutConsumingAnything()
    {
        const uint uid = 1_299_821;
        Seed(uid);
        var owner = NewOwner(identity: new LegacyLocalServerIdentity(42));
        var player = await Login(owner, uid);
        var current = Current(owner, player);
        owner.Sessions.Set(player.Connection, current with { LegacyTradeActive = true });
        SeedAdmissionFloor(owner, player);

        await AssertAdmissionFloorDenied(owner, player, [AdmissionMoneyFloorUid, AdmissionItemFloorUid], beforeParse: true);
    }

    [Fact]
    public async Task RealOpenedTradeHolderBlocksFloorPickupEvenIfProjectionIsCleared()
    {
        const uint uid = 1_299_831;
        const uint partnerUid = 1_299_832;
        Seed(uid);
        Seed(partnerUid);
        var owner = NewOwner(identity: new LegacyLocalServerIdentity(42));
        var player = await Login(owner, uid);
        var partner = await Login(owner, partnerUid);
        await SendFloorAdmissionTrade(owner, player, new LegacyTradePacket(0) { UID = partnerUid });
        await SendFloorAdmissionTrade(owner, partner, new LegacyTradePacket(1) { UID = uid });
        Assert.True(Current(owner, player).LegacyTradeActive);
        Assert.True(Current(owner, partner).LegacyTradeActive);
        var field = typeof(WorldServerFrameHandler).GetField("_tradeStates", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var trades = Assert.IsType<LegacyTradeStateStore>(field.GetValue(owner.Handler));
        Assert.True(trades.Contains(player.Connection));
        Assert.True(trades.Contains(partner.Connection));
        // Deliberately clear only the compatibility projection. The actual
        // request/accept pair remains the independently owned live Trade gate.
        var current = Current(owner, player);
        owner.Sessions.Set(player.Connection, current with { LegacyTradeActive = false });
        var partnerSession = Current(owner, partner);
        var partnerFirst = partner.Sent.Count;
        SeedAdmissionFloor(owner, player);

        await AssertAdmissionFloorDenied(owner, player, [AdmissionMoneyFloorUid, AdmissionItemFloorUid], beforeParse: true);

        Assert.Same(partnerSession, Current(owner, partner));
        Assert.Equal(partnerFirst, partner.Sent.Count);
        Assert.True(trades.Contains(player.Connection));
    }

    [Fact]
    public async Task RequestOnlyTradePairDoesNotBlockOwnServerFloorPickupBeforeWindowOpens()
    {
        const uint uid = 1_299_851;
        const uint partnerUid = 1_299_852;
        Seed(uid);
        Seed(partnerUid);
        var owner = NewOwner(identity: new LegacyLocalServerIdentity(42));
        var player = await Login(owner, uid);
        var partner = await Login(owner, partnerUid);
        await SendFloorAdmissionTrade(owner, player, new LegacyTradePacket(0) { UID = partnerUid });
        var field = typeof(WorldServerFrameHandler).GetField("_tradeStates", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var trades = Assert.IsType<LegacyTradeStateStore>(field.GetValue(owner.Handler));
        Assert.True(trades.TryGet(player.Connection, out var pending));
        Assert.False(pending.WindowOpen);
        Assert.False(pending.Other.WindowOpen);
        Assert.False(Current(owner, player).LegacyTradeActive);
        Assert.False(Current(owner, partner).LegacyTradeActive);
        var before = Current(owner, player);
        SeedAdmissionFloor(owner, player);
        var first = player.Sent.Count;

        await PickupAdmissionFloor(owner, player, AdmissionMoneyFloorUid);

        Assert.Equal(before.Character.Money + 125, Current(owner, player).Character.Money);
        Assert.Contains(player.Sent.Skip(first), bytes => IsAdmissionFloorRemoval(bytes, AdmissionMoneyFloorUid));
        Assert.DoesNotContain(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow), entry => entry.Packet.Uid == AdmissionMoneyFloorUid);
        Assert.True(trades.TryGet(player.Connection, out var stillPending));
        Assert.Same(pending, stillPending);
        Assert.False(stillPending.WindowOpen);
        Assert.False(Current(owner, player).LegacyTradeActive);
    }

    [Fact]
    public async Task PopulatedFloorItemWithUnknownCatalogTypeIsRetainedWithoutPickupMutation()
    {
        const uint uid = 1_299_841;
        Seed(uid);
        var owner = NewOwner(identity: new LegacyLocalServerIdentity(42));
        var player = await Login(owner, uid);
        var unknown = AdmissionFloorItem().WithItemId(ItemId + 1);
        var packet = LegacyFloorItemPacket.FromDroppedItem(unknown, Current(owner, player).Character, AdmissionItemFloorUid);
        DropFloor(owner).AddVisible(FloorItemRuntimeEntry.ForItem(Current(owner, player).Character.MapId, packet, unknown));

        await AssertAdmissionFloorDenied(owner, player, [AdmissionItemFloorUid], beforeParse: false);
    }

    private PlayerItemSnapshotItem SeedAdmissionFloor(Owner owner, Player player)
    {
        var character = Current(owner, player).Character;
        var item = AdmissionFloorItem();
        var money = LegacyFloorItemPacket.FromDroppedItem(item.WithItemId(1_090_000), character, AdmissionMoneyFloorUid);
        DropFloor(owner).AddVisible(FloorItemRuntimeEntry.ForMoney(character.MapId, money, 125));
        var packet = LegacyFloorItemPacket.FromDroppedItem(item, character, AdmissionItemFloorUid);
        DropFloor(owner).AddVisible(FloorItemRuntimeEntry.ForItem(character.MapId, packet, item));
        return item;
    }

    private static PlayerItemSnapshotItem AdmissionFloorItem() => new()
    {
        Uid = AdmissionPickedItemUid, ItemId = ItemId, Durability = 7, MaximumDurability = 10,
        StackSize = 3, Bound = 0, Color = 5, Plus = 2, Bless = 1, Enchant = 17,
        SocketOne = 13, SocketTwo = 23, PlusProgress = 123, AnimaItemId = 0,
    };

    private async Task PickupAdmissionFloor(Owner owner, Player player, uint floorUid)
    {
        var writer = new PacketWriter(LegacyFloorItemPacket.Opcode, sizeof(uint));
        writer.WriteUInt32(0, floorUid);
        await owner.Handler.HandleFrameAsync(player.Connection, ReadFrame(writer.ToArray()), player.Lifetime.Token);
    }

    private async Task AssertAdmissionFloorDenied(Owner owner, Player player, IReadOnlyList<uint> floorUids, bool beforeParse)
    {
        var before = Current(owner, player);
        var characterBytes = File.ReadAllBytes(UserPath(player.Uid));
        var itemBytes = File.ReadAllBytes(ItemPath(player.Uid));
        var floorBefore = DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow).ToArray();
        var first = player.Sent.Count;
        foreach (var floorUid in floorUids) await PickupAdmissionFloor(owner, player, floorUid);
        if (beforeParse)
        {
            var logStart = owner.Log.GetStringBuilder().Length;
            await owner.Handler.HandleFrameAsync(player.Connection,
                ReadFrame(new PacketWriter(LegacyFloorItemPacket.Opcode, 0).ToArray()), player.Lifetime.Token);
            Assert.DoesNotContain("malformed LegacyFloorItem", owner.Log.ToString()[logStart..], StringComparison.Ordinal);
        }
        Assert.Same(before, Current(owner, player));
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(player.Uid)));
        Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(player.Uid)));
        Assert.Equal(floorBefore, DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow).ToArray());
        Assert.Equal(first, player.Sent.Count);
    }

    private async Task SendFloorAdmissionTrade(Owner owner, Player player, LegacyTradePacket packet) =>
        await owner.Handler.HandleFrameAsync(player.Connection, ReadFrame(packet.ToNativeFrameBytes()), player.Lifetime.Token);

    private static bool IsAdmissionFloorRemoval(byte[] bytes, uint floorUid) =>
        LegacyFloorItemPacket.TryParse(ReadFrame(bytes), out var packet) &&
        packet.Uid == floorUid && packet.DropType == LegacyFloorItemDropType.Remove;
}
