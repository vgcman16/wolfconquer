using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacySecondaryPasswordRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualD13SashTransferRequiresRealSecondaryPasswordIssuerThenCommitsBeforePhysicalSends(bool withdraw)
    {
        var uid = withdraw ? 1_299_732U : 1_299_731U;
        Seed(uid, "111,0,0", itemUid: 11);
        new PlayerItemSnapshotLoader(ItemsRoot).SaveByCharacterUid(uid, SashPinSetupItems(withdraw));
        var owner = NewOwner();
        Action<byte[]>? witness = null;
        var player = await Login(owner, uid, beforeSend: frame => witness?.Invoke(frame));
        var rejected = Current(owner, player);
        var rejectedJson = JsonSerializer.Serialize(rejected);
        var rejectedFiles = SashPinFiles();
        var rejectedFrameCount = player.Sent.Count;
        Assert.False(player.NativeClient.Player.IsCheckedPass);
        Assert.True(LegacySecondaryPasswordRuntime.TryGet(player.Connection, rejected, out var lockedState, out var checkedBefore));
        Assert.Equal(111U, lockedState.Password);
        Assert.False(checkedBefore);
        var calls = new List<Task>();

        await SashPinFrameAsync(owner, player, SashPinRequest(withdraw), calls);

        Assert.Same(rejected, Current(owner, player));
        Assert.Equal(rejectedJson, JsonSerializer.Serialize(Current(owner, player)));
        AssertSashPinFiles(rejectedFiles);
        Assert.Equal(rejectedFrameCount, player.Sent.Count);

        var verificationFrameCount = player.Sent.Count;
        AssertReply(await Password(owner, player, 1, newPin: 111), 5, 257);
        var verificationFrames = player.Sent.Skip(verificationFrameCount).ToArray();
        Assert.Equal(2, verificationFrames.Length);
        Assert.Equal(new LegacySecondaryPasswordPacket(5, 257, 0).ToFrameBytes(), verificationFrames[0]);
        Assert.Equal(LegacyMsgMessagePacket.SystemTalk("Secondary verified!").ToFrameBytes(), verificationFrames[1]);
        AssertSashPinFiles(rejectedFiles);
        var admitted = Current(owner, player);
        Assert.Same(rejected, admitted);
        Assert.True(LegacySecondaryPasswordRuntime.TryGet(player.Connection, admitted, out var verifiedState, out var admittedByIssuer));
        Assert.Equal(111U, verifiedState.Password);
        Assert.True(admittedByIssuer);

        var nativeOwners = CaptureSashPinNativeOwners(player);
        var otherNativeRows = CaptureSashPinOtherNativeRows(player);
        Assert.Equal(6, otherNativeRows.Count);
        var expectedItems = SashPinExpectedItems(admitted.PlayerItems, withdraw);
        var expectedFiles = new Dictionary<string, byte[]>(rejectedFiles, StringComparer.OrdinalIgnoreCase)
        {
            [Path.GetRelativePath(root, ItemPath(uid))] = PlayerItemSnapshotLoader.ToLegacyBytes(expectedItems),
        };
        var successFrameCount = player.Sent.Count;
        var observed = 0;
        witness = _ =>
        {
            AssertSashPinFiles(expectedFiles);
            var current = Current(owner, player);
            Assert.Equal(JsonSerializer.Serialize(expectedItems), JsonSerializer.Serialize(current.PlayerItems));
            Assert.Equal(admitted with { PlayerItems = current.PlayerItems }, current);
            AssertSashPinNativeOwnersCurrent(owner, player, current, nativeOwners, otherNativeRows);
            observed++;
        };

        await SashPinFrameAsync(owner, player, SashPinRequest(withdraw), calls);

        Assert.Equal(2, observed);
        AssertSashPinFiles(expectedFiles);
        var committed = Current(owner, player);
        Assert.Equal(JsonSerializer.Serialize(expectedItems), JsonSerializer.Serialize(committed.PlayerItems));
        Assert.Equal(rejectedJson, JsonSerializer.Serialize(rejected));
        Assert.Equal(admitted with { PlayerItems = committed.PlayerItems }, committed);
        AssertSashPinOtherStores(admitted.PlayerItems, committed.PlayerItems);
        AssertSashPinNativeOwnersCurrent(owner, player, committed, nativeOwners, otherNativeRows);
        var frames = player.Sent.Skip(successFrameCount).ToArray();
        var expectedFrames = SashPinSuccessFrames(expectedItems, withdraw);
        Assert.Equal(expectedFrames.Length, frames.Length);
        for (var index = 0; index < expectedFrames.Length; index++) Assert.Equal(expectedFrames[index], frames[index]);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expectedItems),
            PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshotLoader(ItemsRoot).LoadByCharacterUid(uid)));
        Assert.True(LegacySecondaryPasswordRuntime.TryGet(player.Connection, committed, out _, out var stillAdmitted));
        Assert.True(stillAdmitted);
        witness = null;
        var committedJson = JsonSerializer.Serialize(committed);
        var frameCount = player.Sent.Count;

        await SashPinFrameAsync(owner, player, SashPinRequest(withdraw), calls);

        Assert.Same(committed, Current(owner, player));
        Assert.Equal(committedJson, JsonSerializer.Serialize(Current(owner, player)));
        AssertSashPinFiles(expectedFiles);
        Assert.Equal(frameCount, player.Sent.Count);
        Assert.All(calls, task => Assert.True(task.IsCompletedSuccessfully));
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
    }

    private static PlayerItemSnapshot SashPinSetupItems(bool deposited)
    {
        static PlayerItemSnapshotItem Row(uint itemUid) => new()
        {
            Uid = itemUid, ItemId = 1_088_001, Durability = 5, MaximumDurability = 10,
            StackSize = 3, Bound = 1, Locked = 1, Inscribed = 1,
            OwnerUid = 77, OwnerName = "SashPinOwner", Signature = "preserved item row",
        };
        var child = Row(11);
        var sash = new PlayerItemSnapshotItem
        { Uid = 10, ItemId = 1_100_009, Durability = 1, MaximumDurability = 1, StackSize = 1 };
        return new(deposited ? [sash.WithDepositedItems([child])] : [sash, child],
            [Row(40_001).WithPosition(213)], [Row(40_002).WithPosition(217)],
            [Row(40_003).WithPosition(4)], [Row(40_004).WithPosition(211)],
            [Row(40_005).WithItemId(181305).WithWarehouseId(100)])
        { WarehouseItems = [Row(40_006).WithWarehouseId(1000)] };
    }

    private static PlayerItemSnapshot SashPinExpectedItems(PlayerItemSnapshot before, bool withdraw)
    {
        var sash = Assert.Single(before.InventoryItems, item => item.Uid == 10);
        var child = withdraw ? Assert.Single(sash.DepositedItems, item => item.Uid == 11)
            : Assert.Single(before.InventoryItems, item => item.Uid == 11);
        var moved = child.WithWarehouseId(0, position: 0);
        var changedSash = sash.WithDepositedItems(withdraw ? [] : [moved]);
        return before with { InventoryItems = withdraw ? [changedSash, moved] : [changedSash] };
    }

    private static byte[] SashPinRequest(bool withdraw) => new LegacyWarehousePacket(10,
        withdraw ? LegacyWarehouseAction.ShashWithdrawItem : LegacyWarehouseAction.ShashDepositItem,
        11, 0, []).ToFrameBytes();

    private static byte[][] SashPinSuccessFrames(PlayerItemSnapshot expected, bool withdraw)
    {
        var sash = Assert.Single(expected.InventoryItems, item => item.Uid == 10);
        var child = withdraw ? Assert.Single(expected.InventoryItems, item => item.Uid == 11)
            : Assert.Single(sash.DepositedItems, item => item.Uid == 11);
        return withdraw
            ? [LegacyGameItemPacket.FromSnapshotItem(child).ToFrameBytes(),
                new LegacyWarehousePacket(10, LegacyWarehouseAction.ShashWithdrawItem, 11, 0, [])
                    { DeclaredItemCount = 1 }.ToFrameBytes()]
            : [new MsgItemUsagePacket(11, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes(),
                new LegacyWarehousePacket(10, LegacyWarehouseAction.ShashDepositItem, 0, 0, [child]).ToFrameBytes()];
    }

    private static void AssertSashPinOtherStores(PlayerItemSnapshot before, PlayerItemSnapshot after)
    {
        // Canonical publication owns detached snapshot lists. Preserve every
        // unchanged saved field/order rather than requiring proposal aliases.
        Assert.Equal(
            PlayerItemSnapshotLoader.ToLegacyBytes(before with { InventoryItems = Array.Empty<PlayerItemSnapshotItem>() }),
            PlayerItemSnapshotLoader.ToLegacyBytes(after with { InventoryItems = Array.Empty<PlayerItemSnapshotItem>() }));
    }

    private static object[] CaptureSashPinNativeOwners(Player player)
    {
        var client = player.NativeClient;
        return
        [
            Assert.IsType<LegacyOrdinarySpellBinding>(client.OrdinarySpellBinding),
            Assert.IsType<LegacyNativeInventoryRuntime>(client.Inventory),
            Assert.IsType<LegacyNativeEquipItemStorage>(client.Equipment),
            Assert.IsType<LegacyNativeWarehouseRuntime>(client.Warehouse),
            Assert.IsType<LegacyNativeWardrobeRuntime>(client.MyWardrobe),
            Assert.IsType<LegacyNativeRuneItemStorage>(client.Rune),
            Assert.IsType<LegacyNativeItemBagRuntime>(client.RelicFuse),
            Assert.IsType<LegacyNativeItemBagRuntime>(client.GearMythsoulBag),
        ];
    }

    private static Dictionary<uint, LegacyNativeGameItemRuntime> CaptureSashPinOtherNativeRows(Player player)
    {
        var client = player.NativeClient;
        return client.Equipment!.ClientItems
            .Concat(client.Warehouse!.ClientItems.Values.SelectMany(bank => bank))
            .Concat(client.MyWardrobe!.Items.SelectMany(bucket => bucket))
            .Concat(client.Rune!.Objects.Select(item => new KeyValuePair<uint, LegacyNativeGameItemRuntime>(item.Uid, item)))
            .Concat(client.RelicFuse!.ToArray())
            .Concat(client.GearMythsoulBag!.ToArray())
            .ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    private static void AssertSashPinNativeOwnersCurrent(
        Owner owner,
        Player player,
        WorldSession current,
        object[] beforeOwners,
        Dictionary<uint, LegacyNativeGameItemRuntime> beforeRows)
    {
        var afterOwners = CaptureSashPinNativeOwners(player);
        Assert.Equal(beforeOwners.Length, afterOwners.Length);
        for (int index = 0; index < beforeOwners.Length; index++)
            Assert.Same(beforeOwners[index], afterOwners[index]);
        var afterRows = CaptureSashPinOtherNativeRows(player);
        Assert.Equal(beforeRows.Keys.Order(), afterRows.Keys.Order());
        foreach (var pair in beforeRows)
            Assert.Same(pair.Value, afterRows[pair.Key]);
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(player.NativeClient.OrdinarySpellBinding);
        Assert.Same(owner.Sessions, binding.Store);
        Assert.Same(player.Connection, binding.Connection);
        Assert.Same(player.NativeClient, binding.Client);
        Assert.Same(player.NativeClient.Player, binding.Player);
        Assert.Same(current.PlayerItems, binding.ItemSnapshot);
        Assert.Same(current.PlayerItems, binding.Items.Image.FinalSnapshot);
        Assert.True(binding.Items.PhysicalOwnerIsCurrentPlain());
        Assert.True(owner.Sessions.IsBoundSpellOwnerCurrent(binding));
    }

    private Dictionary<string, byte[]> SashPinFiles() => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
        .ToDictionary(path => Path.GetRelativePath(root, path), File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);

    private void AssertSashPinFiles(Dictionary<string, byte[]> expected)
    {
        var actual = SashPinFiles();
        Assert.Equal(expected.Keys.Order(StringComparer.OrdinalIgnoreCase), actual.Keys.Order(StringComparer.OrdinalIgnoreCase));
        foreach (var (path, bytes) in expected) Assert.Equal(bytes, actual[path]);
    }

    private static async Task SashPinFrameAsync(Owner owner, Player player, byte[] bytes, List<Task> calls)
    {
        var actual = owner.Handler.HandleFrameAsync(player.Connection, ReadFrame(bytes), player.Lifetime.Token).AsTask();
        calls.Add(actual);
        // Await the actual producer. The inherited synchronous fixture Dispose may
        // not delete its files while a timeout-observed task is still executing.
        await actual;
    }
}
