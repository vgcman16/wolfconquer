using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// The existing disabled-parallel collection protects read-only observations of
// the real shared floor UID counter. It is never reset/replaced by these tests.
// Actual D13/login/file/Drop dispatch proof, not SQL or live-client proof.
[Collection("DeferredMonsterMoneyFloorUid")]
public sealed partial class LegacyNpcSaleTransactionTests
{
    [Theory]
    [InlineData(77u)]
    [InlineData(uint.MaxValue)]
    public async Task ActualDropCapturesCurrentUInt32ScopeAndBroadcastsOnlyMatchingPeersWithinSeventeenTiles(uint dynamicId)
    {
        const uint uid = 1_299_970;
        const uint nearUid = 1_299_971;
        const uint edgeUid = 1_299_972;
        const uint otherScopeUid = 1_299_973;
        SeedPersistenceDrop(uid, disposition: 2);
        File.AppendAllText(UserPath(uid), "\nDynamicID=" + dynamicId.ToString(CultureInfo.InvariantCulture) + "\n");
        foreach (var peerUid in new[] { nearUid, edgeUid, otherScopeUid }) Seed(peerUid);
        File.AppendAllText(UserPath(nearUid), "\nDynamicID=" + dynamicId.ToString(CultureInfo.InvariantCulture) + "\n");
        File.AppendAllText(UserPath(edgeUid), "\nDynamicID=" + dynamicId.ToString(CultureInfo.InvariantCulture) + "\n");
        uint otherScope = dynamicId == 77 ? 88u : 77u;
        File.AppendAllText(UserPath(otherScopeUid), "\nDynamicID=" + otherScope.ToString(CultureInfo.InvariantCulture) + "\n");
        var owner = NewOwner();
        Player? player = null;
        WorldSession? before = null;
        byte[]? characterBytes = null;
        bool armed = false;
        int visibleCallbacks = 0;
        Exception? callbackFailure = null;
        player = await Login(owner, uid, bytes =>
        {
            if (!armed || !LegacyFloorItemPacket.TryParse(ReadFrame(bytes), out var packet) ||
                packet.DropType != LegacyFloorItemDropType.Visible) return;
            visibleCallbacks++;
            callbackFailure ??= Record.Exception(() =>
            {
                Assert.NotNull(before);
                Assert.NotNull(characterBytes);
                AssertPersistenceDropCommitted(owner, player!, before, characterBytes, groundCount: 1);
                var published = Assert.Single(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow));
                Assert.Equal((uint?)dynamicId, published.DynamicId);
                Assert.Equal(published.Packet.ToFrameBytes(), bytes);
            });
        });
        var near = await Login(owner, nearUid);
        var edge = await Login(owner, edgeUid);
        var other = await Login(owner, otherScopeUid);
        before = Current(owner, player);
        SetDropVisibilityPeerPosition(owner, near, before.Character.X + 17, before.Character.Y + 17);
        SetDropVisibilityPeerPosition(owner, edge, before.Character.X + 18, before.Character.Y + 18);
        SetDropVisibilityPeerPosition(owner, other, before.Character.X + 17, before.Character.Y + 17);
        characterBytes = File.ReadAllBytes(UserPath(uid));
        var first = player.Sent.Count;
        var nearFirst = near.Sent.Count;
        var edgeFirst = edge.Sent.Count;
        var otherFirst = other.Sent.Count;
        armed = true;

        await DropPinItem(owner, player, SoldUid);

        Assert.Equal(1, visibleCallbacks);
        Assert.Null(callbackFailure);
        AssertPersistenceDropCommitted(owner, player, before, characterBytes, groundCount: 1);
        AssertPersistenceDropFrames(player.Sent.Skip(first).ToArray(), ground: true);
        var entry = Assert.Single(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.Equal((uint?)dynamicId, entry.DynamicId);
        Assert.Equal(before.Character.MapId, entry.MapId);
        Assert.Equal(before.Character.Uid, entry.Packet.ItemOwnerUid);
        Assert.Equal(entry.Packet.ToFrameBytes(), Assert.Single(near.Sent.Skip(nearFirst)));
        Assert.Empty(edge.Sent.Skip(edgeFirst));
        Assert.Empty(other.Sent.Skip(otherFirst));
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("negative")]
    [InlineData("overflow")]
    [InlineData("conflicting-aliases")]
    public async Task ActualDropInvalidCurrentScopePreservesExactItemSessionFilesAndUndrawnFloorUid(string scenario)
    {
        const uint uid = 1_299_980;
        SeedPersistenceDrop(uid, disposition: 2);
        var owner = NewOwner();
        var player = await LoginPinDrop(owner, uid);
        var current = Current(owner, player);
        var attributes = new Dictionary<string, string>(current.Character.Attributes, StringComparer.Ordinal)
        {
            ["DynamicID"] = scenario switch
            {
                "malformed" => "bad",
                "negative" => "-1",
                "overflow" => "4294967296",
                _ => "77",
            },
        };
        if (scenario == "conflicting-aliases") attributes.Add("dynamicid", "88");
        owner.Sessions.Set(player.Connection, current with
        {
            Character = current.Character with { Attributes = attributes },
        });
        Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(player.Connection, Current(owner, player)));
        var before = Current(owner, player);
        var droppedItem = Assert.Single(before.PlayerItems.InventoryItems, item => item.Uid == SoldUid);
        uint counterBefore = ReadDropVisibilityFloorUidCounter();

        await AssertPinDropDenied(owner, player, expectedReason: "invalidDynamicId");

        Assert.Equal(counterBefore, ReadDropVisibilityFloorUidCounter());
        Assert.Same(before, Current(owner, player));
        Assert.Same(droppedItem, Assert.Single(Current(owner, player).PlayerItems.InventoryItems, item => item.Uid == SoldUid));
        Assert.Same(attributes, Current(owner, player).Character.Attributes);
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
    }

    [Fact]
    public async Task ActualDropMissingCurrentScopeCapturesExplicitNativeDefaultZeroAndCommitsOrdinaryDrop()
    {
        const uint uid = 1_299_990;
        SeedPersistenceDrop(uid, disposition: 2);
        var owner = NewOwner();
        var player = await Login(owner, uid);
        var before = Current(owner, player);
        Assert.DoesNotContain(before.Character.Attributes.Keys,
            key => string.Equals(key, "DynamicID", StringComparison.OrdinalIgnoreCase));
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var first = player.Sent.Count;

        await DropPinItem(owner, player, SoldUid);

        AssertPersistenceDropCommitted(owner, player, before, characterBytes, groundCount: 1);
        AssertPersistenceDropFrames(player.Sent.Skip(first).ToArray(), ground: true);
        var entry = Assert.Single(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.Equal((uint?)0, entry.DynamicId);
        Assert.Equal(before.Character.MapId, entry.MapId);
        Assert.Equal(before.Character.Uid, entry.Packet.ItemOwnerUid);
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
    }

    private static void SetDropVisibilityPeerPosition(Owner owner, Player player, long x, long y)
    {
        var current = Current(owner, player);
        owner.Sessions.Set(player.Connection, current with
        {
            Character = current.Character with { X = x, Y = y },
        });
    }

    private static uint ReadDropVisibilityFloorUidCounter()
    {
        var field = typeof(WorldPacketHandlers).GetField("s_legacyFloorItemUid", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return unchecked((uint)Assert.IsType<int>(field.GetValue(null)));
    }
}
