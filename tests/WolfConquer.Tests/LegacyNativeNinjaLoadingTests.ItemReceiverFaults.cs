using WolfConquer.Core.World;
using ItemQuery = WolfConquer.Core.World.LegacyNativeMsgNinja.MsgNinjaItem;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Theory]
    [InlineData("pool")]
    [InlineData("target")]
    [InlineData("player")]
    public Task ItemReceiverLookupAndIdentityFaultsPropagateAfterDecodeBeforeTakingAnyPacket(string missing) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            using var poll = new NinjaItemPoll();
            using var peer = new NativeSpellTestFixture();
            peer.Owner.MyNinja = null!;
            if (missing == "pool") LegacyNativePoolClients.GamePoll = null;
            else if (missing == "target") poll.Rows[77] = null!;
            else { peer.Owner.Player = null!; poll.Rows[77] = peer.Owner; }
            byte[] input = NinjaViewInput(scope);

            Assert.Throws<NullReferenceException>(() => NinjaItemReceiver()(scope.Client, scope.Packet));

            NinjaViewInputUnchanged(scope, input);
            Assert.Empty(scope.Queue); Assert.Empty(peer.Queue); Assert.Equal(12, scope.Available); scope.Untaken(0);
            if (missing == "pool") Assert.Null(LegacyNativePoolClients.GamePoll);
            else Assert.Same(poll.Rows, LegacyNativePoolClients.GamePoll);
        });

    [Theory]
    [InlineData("ninja")]
    [InlineData("items")]
    [InlineData("row")]
    public Task ItemReceiverBulkSourceFaultsDisposeFirstPacketWithoutUserResponseOrRepair(string missing) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            using var poll = new NinjaItemPoll();
            using var peer = new NativeSpellTestFixture();
            peer.Owner.MyNinja = new LegacyNativeNinja(peer.Owner);
            if (missing == "ninja") peer.Owner.MyNinja = null!;
            else if (missing == "items") peer.Owner.MyNinja.Items = null!;
            else Assert.True(peer.Owner.MyNinja.Items.TryAdd(100, null!));
            poll.Rows[77] = peer.Owner;
            byte[] input = NinjaViewInput(scope);

            Assert.Throws<NullReferenceException>(() => NinjaItemReceiver()(scope.Client, scope.Packet));

            NinjaViewInputUnchanged(scope, input);
            scope.UnserializedDisposed(0); scope.Untaken(1);
            Assert.Empty(scope.Queue); Assert.Empty(peer.Queue); Assert.Equal(11, scope.Available);
            if (missing == "ninja") Assert.Null(peer.Owner.MyNinja);
            else if (missing == "items") Assert.Null(peer.Owner.MyNinja.Items);
            else Assert.Null(peer.Owner.MyNinja.Items[100]);
        });

    [Fact]
    public Task ItemReceiverMetadataFaultKeepsFirstItemFrameAndDisposesUnserializedUserPacket() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            using var poll = new NinjaItemPoll();
            using var peer = new NativeSpellTestFixture();
            peer.Owner.Player.UID = 29;
            peer.Owner.MyNinja = new LegacyNativeNinja();
            var row = new LegacyNativeNinja.Item { ItemID = 100, Level = 2, Position = 3, Exp = 4, UK = 5 };
            Assert.True(peer.Owner.MyNinja.Items.TryAdd(456, row));
            LegacyNativeNinjaFile.gouyu_type = null!;
            poll.Rows[77] = peer.Owner;
            byte[] input = NinjaViewInput(scope);

            Assert.Throws<NullReferenceException>(() => NinjaItemReceiver()(scope.Client, scope.Packet));

            byte[] items = ItemFrame(ItemQuery.TypeID.View, 29, (100, 2, 3, 4, 5));
            scope.Sent(items); scope.Serialized(0, items); scope.UnserializedDisposed(1); scope.Untaken(2);
            NinjaViewInputUnchanged(scope, input);
            Assert.Equal(10, scope.Available); Assert.Empty(peer.Queue);
            Assert.Same(row, peer.Owner.MyNinja.Items[456]); Assert.Null(LegacyNativeNinjaFile.gouyu_type);
        });

    [Fact]
    public Task ItemReceiverNullRequesterFaultsAfterWholeItemEncodingButBeforeUserPacketTake() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            using var poll = new NinjaItemPoll();
            using var peer = new NativeSpellTestFixture();
            peer.Owner.Player.UID = 29;
            peer.Owner.MyNinja = new LegacyNativeNinja();
            poll.Rows[77] = peer.Owner;
            byte[] input = NinjaViewInput(scope);

            Assert.Throws<NullReferenceException>(() => NinjaItemReceiver()(null!, scope.Packet));

            scope.Serialized(0, ItemFrame(ItemQuery.TypeID.View, 29)); scope.Untaken(1);
            NinjaViewInputUnchanged(scope, input);
            Assert.Empty(scope.Queue); Assert.Empty(peer.Queue); Assert.Equal(11, scope.Available);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ItemReceiverDecodingFaultsPrecedeNullPoolRequesterAndAllResponseTakes(bool negativeLength) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            using var poll = new NinjaItemPoll();
            LegacyNativePoolClients.GamePoll = null;
            byte[] input = negativeLength ? [3, 0, 89, 9] : [5, 0, 89, 9, 8];
            CopyInto(scope, input);
            scope.Packet.Seek(237);

            if (negativeLength) Assert.Throws<OverflowException>(() => NinjaItemReceiver()(null!, scope.Packet));
            else Assert.Throws<EndOfStreamException>(() => NinjaItemReceiver()(null!, scope.Packet));

            Assert.Equal(input, NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(negativeLength ? 4 : 5, scope.Packet.Position);
            Assert.Null(LegacyNativePoolClients.GamePoll);
            Assert.Empty(scope.Queue); Assert.Equal(12, scope.Available); scope.Untaken(0);
        });

    [Fact]
    public Task ItemReceiverNullStreamCannotSkipDecodeEvenWithNullPoolAndRequester() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            using var poll = new NinjaItemPoll();
            LegacyNativePoolClients.GamePoll = null;
            Assert.Throws<NullReferenceException>(() => NinjaItemReceiver()(null!, null!));
            Assert.Null(LegacyNativePoolClients.GamePoll);
            Assert.Empty(scope.Queue); Assert.Equal(12, scope.Available); scope.Untaken(0);
        });
}
