using System.Collections.Concurrent;
using WolfConquer.Core.World;
using ItemQuery = WolfConquer.Core.World.LegacyNativeMsgNinja.MsgNinjaItem;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)] // Update includes the View bit, but the receiver uses equality.
    [InlineData(4)]
    [InlineData(int.MaxValue)]
    public Task ItemReceiverNonViewModesDecodeWithoutReadingNullPoolRequesterOrRecycleBin(int type) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            using var poll = new NinjaItemPoll();
            LegacyNativePoolClients.GamePoll = null;
            byte[] input = NinjaViewInput(scope, type);
            scope.UseNullBin();
            NinjaItemReceiver()(null!, scope.Packet);
            scope.RestoreOwnedBin();
            NinjaViewInputUnchanged(scope, input);
            Assert.Null(LegacyNativePoolClients.GamePoll);
            Assert.Empty(scope.Queue); Assert.Equal(12, scope.Available); scope.Untaken(0);
        });

    [Fact]
    public Task ItemReceiverMissingViewKeyDoesNotReadNullRequesterOrInventAClient() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            using var poll = new NinjaItemPoll();
            byte[] input = NinjaViewInput(scope, uid: uint.MaxValue);
            scope.UseNullBin();
            NinjaItemReceiver()(null!, scope.Packet);
            scope.RestoreOwnedBin();
            NinjaViewInputUnchanged(scope, input);
            Assert.Empty(poll.Rows); Assert.Same(poll.Rows, LegacyNativePoolClients.GamePoll);
            Assert.Empty(scope.Queue); Assert.Equal(12, scope.Available); scope.Untaken(0);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ItemReceiverUsesLivePoolAndPeerNinjaPhysicalUidAndMetadataThenQueuesItemsBeforeUser(bool replacePool) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            using var poll = new NinjaItemPoll();
            using var peer = new NativeSpellTestFixture();
            peer.Owner.Player.UID = 99;
            peer.Owner.Map = new LegacyNativeGameMap(2, 2, 7000);
            scope.Client.Map = new LegacyNativeGameMap(2, 2, 1002);
            scope.Client.MyNinja = null!;
            scope.SetClass(0);
            peer.Owner.MyNinja = new LegacyNativeNinja(); // Replaced current owner, not a cached birth reference.
            var ninja = new LegacyNativeNinja(scope.Client)
            {
                Flag = 1UL << 55, Exp = 99, Fire = 3, Water = 4, Earth = 5, Wind = 6, Lightning = 7,
                tmp_Fire = 8, tmp_Water = 9, tmp_Earth = 10, tmp_Wind = 11, tmp_Lightning = 12,
                FireMastery = 13, WaterMastery = 14, EarthMastery = 15, WindMastery = 16, LightningMastery = 17,
                UnlockMastery = 18,
            };
            peer.Owner.MyNinja = ninja;
            var row = new LegacyNativeNinja.Item { ItemID = 100, Level = 2, Position = 3, Exp = 4, UK = 5 };
            Assert.True(ninja.Items.TryAdd(999, row));
            var metadata = scope.Meta(12345, 100, 2, score: 19);
            poll.Rows[77] = replacePool ? null! : peer.Owner;
            if (replacePool)
                LegacyNativePoolClients.GamePoll = new ConcurrentDictionary<uint, LegacyNativeGameClientRuntime> { [77] = peer.Owner };
            byte[] input = NinjaViewInput(scope, 1, 77, (300, uint.MaxValue, 9, 8, 7));
            input[2] = 7; input[3] = 0; // The body decoder ignores the transport opcode, as native does.
            CopyInto(scope, input);
            scope.Packet.Seek(237);

            NinjaItemReceiver()(scope.Client, scope.Packet);

            byte[] items = ItemFrame(ItemQuery.TypeID.View, 99, (100, 2, 3, 4, 5));
            // 19 metadata + 3*4 Fire + all five masteries(13..17) = 106.
            byte[] user = UserFrame(0, 99, 1UL << 55, 99, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 106, 13, 14, 15, 16, 17, 18);
            scope.Sent(items, user); scope.Serialized(0, items); scope.Serialized(1, user);
            NinjaViewInputUnchanged(scope, input);
            Assert.Empty(peer.Queue); Assert.Equal(10, scope.Available); scope.Untaken(2);
            Assert.Same(peer.Owner, LegacyNativePoolClients.GamePoll![77]);
            Assert.False(LegacyNativePoolClients.GamePoll.ContainsKey(99));
            if (replacePool) { Assert.NotSame(poll.Rows, LegacyNativePoolClients.GamePoll); Assert.Null(poll.Rows[77]); }
            Assert.Same(ninja, peer.Owner.MyNinja); Assert.Same(scope.Client, ninja.user);
            Assert.Same(row, Assert.Single(ninja.Items).Value); Assert.Equal(999u, Assert.Single(ninja.Items).Key);
            Assert.Equal(new uint[] { 100, 2, 3, 4, 5 }, new[] { row.ItemID, row.Level, row.Position, row.Exp, row.UK });
            Assert.Same(metadata, scope.Metadata[12345]); Assert.Equal(19u, metadata.Score);
            Assert.Null(scope.Client.MyNinja); Assert.Equal(17u, scope.Client.Player.UID);
            Assert.Equal(1002u, scope.Client.Map.ID); Assert.Equal(7000u, peer.Owner.Map.ID);
            Assert.Empty(scope.Log);
        });

    [Fact]
    public Task ItemReceiverAllowsZeroPhysicalPeerUidAndEmptyItemsWithoutMetadataOrBacklink() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            using var poll = new NinjaItemPoll();
            using var peer = new NativeSpellTestFixture();
            peer.Owner.Player.UID = 0;
            peer.Owner.MyNinja = new LegacyNativeNinja();
            LegacyNativeNinjaFile.gouyu_type = null!;
            poll.Rows[77] = peer.Owner;
            byte[] input = NinjaViewInput(scope);

            NinjaItemReceiver()(scope.Client, scope.Packet);

            byte[] items = ItemFrame(ItemQuery.TypeID.View, 0), user = UserFrame();
            scope.Sent(items, user); scope.Serialized(0, items); scope.Serialized(1, user);
            NinjaViewInputUnchanged(scope, input);
            Assert.Empty(peer.Queue); Assert.Empty(peer.Owner.MyNinja.Items); Assert.Null(peer.Owner.MyNinja.user);
            Assert.Null(LegacyNativeNinjaFile.gouyu_type); Assert.Equal(10, scope.Available); scope.Untaken(2);
        });
}
