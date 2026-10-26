using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacySecondaryPasswordRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualD13WarehouseShowFreshNativeOwnerCannotInheritPreviousOwnersVerifiedPin(bool physicalSash)
    {
        var uid = physicalSash ? 1_299_752U : 1_299_751U;
        Seed(uid, "111,0,0", itemUid: 11);
        var setup = SashPinSetupItems(deposited: false);
        var sash = Assert.Single(setup.InventoryItems, item => item.Uid == 10);
        var child = Assert.Single(setup.InventoryItems, item => item.Uid == 11);
        var items = setup with
        {
            InventoryItems = physicalSash ? [sash.WithDepositedItems([child])] : [sash],
            WarehouseItems = physicalSash ? setup.WarehouseItems
                : setup.WarehouseItems.Append(child.WithWarehouseId(uid, 0)).ToArray(),
        };
        new PlayerItemSnapshotLoader(ItemsRoot).SaveByCharacterUid(uid, items); // Owned setup, before login.
        Assert.True(CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(UserPath(uid), uid,
            new Dictionary<string, string> { ["InventorySashCount"] = "2" },
            detailedIoFailures: true, out var reason), reason);
        var action = physicalSash ? LegacyWarehouseAction.ShashShow : LegacyWarehouseAction.ShowInventorySash;
        var target = physicalSash ? 10U : uid;
        var request = new LegacyWarehousePacket(target, action, 0, 0, []).ToFrameBytes();
        var calls = new List<Task>();
        var oldOwner = NewOwner();
        var oldPlayer = await Login(oldOwner, uid);
        AssertReply(await Password(oldOwner, oldPlayer, 1, newPin: 111), 5, 257);
        var oldCurrent = Current(oldOwner, oldPlayer);
        var oldJson = JsonSerializer.Serialize(oldCurrent);
        var oldFiles = SashPinFiles();
        var oldFrameCount = oldPlayer.Sent.Count;

        await SashPinFrameAsync(oldOwner, oldPlayer, request, calls);

        Assert.Equal(ExpectedFreshOwnerWarehouseShow(oldCurrent, physicalSash, target, action),
            Assert.Single(oldPlayer.Sent.Skip(oldFrameCount)));
        AssertFreshOwnerShowReadOnly(oldOwner, oldPlayer, oldCurrent, oldJson, oldFiles, oldFrameCount + 1);

        // Use the real issuer to change the credential. Native action4 preserves
        // an already checked owner; a blanket PIN-change revocation is not asserted.
        AssertReply(await Password(oldOwner, oldPlayer, 4, oldPin: 111, newPin: 222), 5, 257);
        Assert.Equal((222U, 0U, 0L), ReadCredential(uid));
        Assert.True(LegacySecondaryPasswordRuntime.TryGet(oldPlayer.Connection, Current(oldOwner, oldPlayer),
            out var oldState, out var oldAdmitted));
        Assert.Equal(222U, oldState.Password);
        Assert.True(oldAdmitted);

        var freshOwner = NewOwner();
        var fresh = await Login(freshOwner, uid);
        var current = Current(freshOwner, fresh);
        Assert.NotSame(oldPlayer.NativeClient, fresh.NativeClient);
        Assert.NotSame(oldPlayer.NativeClient.Player, fresh.NativeClient.Player);
        Assert.True(LegacySecondaryPasswordRuntime.TryGet(fresh.Connection, current, out var state, out var admitted));
        Assert.Equal(222U, state.Password);
        Assert.False(admitted);
        Assert.False(fresh.NativeClient.Player.IsCheckedPass);
        Assert.Equal("2", current.Character.Attributes["InventorySashCount"]);
        var json = JsonSerializer.Serialize(current);
        var files = SashPinFiles(); // After legitimate native-login scalar normalization.
        var frameCount = fresh.Sent.Count;

        await SashPinFrameAsync(freshOwner, fresh, request, calls);

        AssertFreshOwnerShowReadOnly(freshOwner, fresh, current, json, files, frameCount);
        AssertReply(await Password(freshOwner, fresh, 1, newPin: 111), 6, 0);
        AssertFreshOwnerShowReadOnly(freshOwner, fresh, current, json, files, frameCount + 1);
        Assert.True(LegacySecondaryPasswordRuntime.TryGet(fresh.Connection, current, out _, out admitted));
        Assert.False(admitted);
        frameCount = fresh.Sent.Count;

        await SashPinFrameAsync(freshOwner, fresh, request, calls);

        AssertFreshOwnerShowReadOnly(freshOwner, fresh, current, json, files, frameCount);
        var verificationStart = fresh.Sent.Count;
        AssertReply(await Password(freshOwner, fresh, 1, newPin: 222), 5, 257);
        var verification = fresh.Sent.Skip(verificationStart).ToArray();
        Assert.Equal(2, verification.Length);
        Assert.Equal(new LegacySecondaryPasswordPacket(5, 257, 0).ToFrameBytes(), verification[0]);
        Assert.Equal(LegacyMsgMessagePacket.SystemTalk("Secondary verified!").ToFrameBytes(), verification[1]);
        AssertFreshOwnerShowReadOnly(freshOwner, fresh, current, json, files, verificationStart + 2);
        Assert.True(LegacySecondaryPasswordRuntime.TryGet(fresh.Connection, current, out state, out admitted));
        Assert.Equal(222U, state.Password);
        Assert.True(admitted);
        frameCount = fresh.Sent.Count;

        await SashPinFrameAsync(freshOwner, fresh, request, calls);

        Assert.Equal(ExpectedFreshOwnerWarehouseShow(current, physicalSash, target, action),
            Assert.Single(fresh.Sent.Skip(frameCount)));
        AssertFreshOwnerShowReadOnly(freshOwner, fresh, current, json, files, frameCount + 1);
        Assert.Equal((222U, 0U, 0L), ReadCredential(uid));
        Assert.All(calls, call => Assert.True(call.IsCompletedSuccessfully));
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
    }

    private static byte[] ExpectedFreshOwnerWarehouseShow(WorldSession current, bool physicalSash,
        uint target, LegacyWarehouseAction action)
    {
        var shown = physicalSash
            ? Assert.Single(Assert.Single(current.PlayerItems.InventoryItems).DepositedItems)
            : Assert.Single(current.PlayerItems.WarehouseItems, item => item.WarehouseId == target);
        return new LegacyWarehousePacket(target, action, 0, physicalSash ? (ushort)12 : (ushort)2, [shown]).ToFrameBytes();
    }

    private void AssertFreshOwnerShowReadOnly(Owner owner, Player player, WorldSession expected, string json,
        Dictionary<string, byte[]> files, int frameCount)
    {
        Assert.Same(expected, Current(owner, player));
        Assert.Equal(json, JsonSerializer.Serialize(Current(owner, player)));
        AssertSashPinFiles(files);
        Assert.Equal(frameCount, player.Sent.Count);
    }
}
