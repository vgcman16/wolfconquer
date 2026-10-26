using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacySecondaryPasswordRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualD13WarehouseShowRequiresGenuinePinIssuerAndRejectsPokerWithoutWritingFiles(bool physicalSash)
    {
        var uid = physicalSash ? 1_299_742U : 1_299_741U;
        Seed(uid, "111,0,0", itemUid: 11);
        var setup = SashPinSetupItems(deposited: false);
        var sash = Assert.Single(setup.InventoryItems, row => row.Uid == 10);
        var child = Assert.Single(setup.InventoryItems, row => row.Uid == 11);
        var items = setup with
        {
            InventoryItems = physicalSash ? [sash.WithDepositedItems([child])] : [sash],
            WarehouseItems = physicalSash ? setup.WarehouseItems : setup.WarehouseItems.Append(child.WithWarehouseId(8)).ToArray(),
        };
        new PlayerItemSnapshotLoader(ItemsRoot).SaveByCharacterUid(uid, items); // Setup before real login.
        var owner = NewOwner();
        Action<byte[]>? witness = null;
        var player = await Login(owner, uid, beforeSend: frame => witness?.Invoke(frame));
        var current = Current(owner, player);
        var json = JsonSerializer.Serialize(current);
        var files = SashPinFiles();
        var calls = new List<Task>();
        var action = physicalSash ? LegacyWarehouseAction.ShashShow : LegacyWarehouseAction.Show;
        var target = physicalSash ? 10U : 8U;
        var request = new LegacyWarehousePacket(target, action, 0, 0, []).ToFrameBytes();
        var frameCount = player.Sent.Count;
        Assert.True(LegacySecondaryPasswordRuntime.TryGet(player.Connection, current, out var state, out var admitted));
        Assert.Equal(111U, state.Password);
        Assert.False(admitted);

        await SashPinFrameAsync(owner, player, request, calls);
        AssertShowPinUnchanged(owner, player, current, json, files, frameCount);

        // Public native scalar spoofing cannot replace the private login issuer.
        player.NativeClient.Player.IsCheckedPass = true;
        try
        {
            await SashPinFrameAsync(owner, player, request, calls);
            AssertShowPinUnchanged(owner, player, current, json, files, frameCount);
        }
        finally { player.NativeClient.Player.IsCheckedPass = false; }

        var verificationStart = player.Sent.Count;
        AssertReply(await Password(owner, player, 1, newPin: 111), 5, 257);
        var verification = player.Sent.Skip(verificationStart).ToArray();
        Assert.Equal(2, verification.Length);
        Assert.Equal(new LegacySecondaryPasswordPacket(5, 257, 0).ToFrameBytes(), verification[0]);
        Assert.Equal(LegacyMsgMessagePacket.SystemTalk("Secondary verified!").ToFrameBytes(), verification[1]);
        AssertSashPinFiles(files);
        Assert.True(LegacySecondaryPasswordRuntime.TryGet(player.Connection, current, out _, out admitted));
        Assert.True(admitted);
        frameCount = player.Sent.Count;
        var shown = physicalSash
            ? Assert.Single(Assert.Single(current.PlayerItems.InventoryItems).DepositedItems)
            : Assert.Single(current.PlayerItems.WarehouseItems, row => row.WarehouseId == 8);
        var expected = new LegacyWarehousePacket(target, action, 0, physicalSash ? (ushort)12 : (ushort)60, [shown]).ToFrameBytes();
        var observed = 0;
        witness = frame =>
        {
            Assert.Same(current, Current(owner, player));
            Assert.Equal(json, JsonSerializer.Serialize(Current(owner, player)));
            AssertSashPinFiles(files);
            Assert.Equal(expected, frame);
            Assert.Equal(1, ++observed);
        };

        await SashPinFrameAsync(owner, player, request, calls);

        Assert.Equal(1, observed);
        Assert.Equal(expected, Assert.Single(player.Sent.Skip(frameCount)));
        AssertShowPinUnchanged(owner, player, current, json, files, frameCount + 1);
        witness = null;
        frameCount = player.Sent.Count;
        // An actual nullable native PokerPlayer is a negative admission condition,
        // not an accepting stand-in or a claim that host poker entry is implemented.
        player.NativeClient.PokerPlayer = new LegacyNativePokerPlayer("ShowBlocked", uid);
        try
        {
            await SashPinFrameAsync(owner, player, request, calls);
            AssertShowPinUnchanged(owner, player, current, json, files, frameCount);
        }
        finally { player.NativeClient.PokerPlayer = null; }
        Assert.True(LegacySecondaryPasswordRuntime.TryGet(player.Connection, current, out _, out admitted));
        Assert.True(admitted);
        Assert.All(calls, call => Assert.True(call.IsCompletedSuccessfully));
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
    }

    private void AssertShowPinUnchanged(Owner owner, Player player, WorldSession expected, string json,
        Dictionary<string, byte[]> files, int frameCount)
    {
        Assert.Same(expected, Current(owner, player));
        Assert.Equal(json, JsonSerializer.Serialize(Current(owner, player)));
        AssertSashPinFiles(files);
        Assert.Equal(frameCount, player.Sent.Count);
    }
}
