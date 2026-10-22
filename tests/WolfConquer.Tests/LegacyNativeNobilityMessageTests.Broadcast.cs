using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNobilityMessageTests
{
    [Fact]
    public Task BroadcastVisitsActualPoolAliasesAndCopiesPacketForEachNonFakeRecipientWithoutMutatingSource() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var otherQueue = new Queue<byte[]>(); var fakeQueue = new Queue<byte[]>();
            var other = NativeSpellTestFixture.CreateOwner(otherQueue); var fake = NativeSpellTestFixture.CreateOwner(fakeQueue);
            fake.Fake = true; fake.Socket = null;
            scope.Pool[91] = scope.Native.Owner; scope.Pool[92] = scope.Native.Owner;
            scope.Pool[93] = other; scope.Pool[94] = fake;
            scope.Packet.NobilityIconCreate(new LegacyNativeNobility(17, "Wolf", 99, 0, 0) { Position = 2 });
            byte[] expected = Icon(17, "17 99 12 2");

            LegacyNativeServerBroadcast.SendGlobalPacket(scope.Packet);

            Assert.Equal(2, scope.Native.Queue.Count); Assert.Equal(expected, scope.Native.Queue.ElementAt(0));
            Assert.Equal(expected, scope.Native.Queue.ElementAt(1)); Assert.Equal(expected, Assert.Single(otherQueue)); Assert.Empty(fakeQueue);
            Assert.NotSame(scope.Native.Queue.ElementAt(0), scope.Native.Queue.ElementAt(1));
            Assert.NotSame(scope.Native.Queue.ElementAt(0), Assert.Single(otherQueue));
            Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet)); Assert.Equal(4, scope.Packet.Position);
            scope.Packet.Seek(4); scope.Packet.Write(123u);
            Assert.Equal(expected, scope.Native.Queue.Peek()); Assert.Equal(expected, otherQueue.Peek());
            Assert.Same(scope.Pool, LegacyNativePoolClients.GamePoll); Assert.Equal(4, scope.Pool.Count);
        });

    [Fact]
    public Task EmptyPoolDoesNotTouchNullPacketWhileUnpublishedPoolFaultsBeforeAnySend() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); LegacyNativeServerBroadcast.SendGlobalPacket(null!);
            Assert.Empty(scope.Native.Queue); Assert.Equal(321, scope.Packet.Size); Assert.Equal(237, scope.Packet.Position);
            LegacyNativePoolClients.GamePoll = null;
            Assert.Throws<NullReferenceException>(() => LegacyNativeServerBroadcast.SendGlobalPacket(scope.Packet));
            Assert.Null(LegacyNativePoolClients.GamePoll); Assert.Empty(scope.Native.Queue);
        });

    [Fact]
    public Task ActualNullPoolRowPropagatesUncaughtNullFaultWithoutRemovingOrRepairingTheRow() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); scope.Pool[17] = null!;
            Assert.Throws<NullReferenceException>(() => LegacyNativeServerBroadcast.SendGlobalPacket(scope.Packet));
            Assert.Null(scope.Pool[17]); Assert.Single(scope.Pool); Assert.Empty(scope.Native.Queue);
            Assert.Equal(321, scope.Packet.Size); Assert.Equal(237, scope.Packet.Position);
        });
}
