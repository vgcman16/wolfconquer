using System.Collections.Concurrent;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class RootLoopbackFramePeerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecursiveSendWaitsForActualReceiptWithoutWaitingForItsOwnObserver(bool observeOrdinaryBeforeReturn)
    {
        var first = new MsgActionPacket { ObjId = 17, DwParam = 1, Type = MsgActionType.AllowAnimation }.ToFrameBytes();
        var second = new MsgActionPacket { ObjId = 17, DwParam = 2, Type = MsgActionType.AllowAnimation }.ToFrameBytes();
        var received = new ConcurrentQueue<byte[]>();
        var callbacks = new ConcurrentQueue<int>();
        RootLoopbackFramePeer? peer = null;
        peer = new RootLoopbackFramePeer(bytes => received.Enqueue(bytes), async bytes =>
        {
            if (bytes.AsSpan().SequenceEqual(first))
            {
                callbacks.Enqueue(1);
                await peer!.Connection.SendAsync(second).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Contains(received, frame => frame.AsSpan().SequenceEqual(second));
                await peer.DrainObservations().WaitAsync(TimeSpan.FromSeconds(5)); // Receipt only while inside the observer.
                callbacks.Enqueue(2);
            }
            else if (bytes.AsSpan().SequenceEqual(second)) callbacks.Enqueue(3);
        }, observeOrdinaryBeforeReturn: observeOrdinaryBeforeReturn);
        await using (peer)
        {
            await peer.Connection.SendAsync(first).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            await peer.DrainObservations().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(new[] { 1, 2, 3 }, callbacks.ToArray());
            Assert.Equal(2, received.Count);
        }
    }
}
