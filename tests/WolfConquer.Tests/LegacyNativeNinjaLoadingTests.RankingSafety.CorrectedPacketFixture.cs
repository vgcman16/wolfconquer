using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    // No test attribute: only the separate corrected-only class invokes this.
    internal static Task RunCorrectedOnlyNinjaPacketCapacity(int capacity) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        SeedRankingRows(scope);
        var old = scope.Item(101, 101, 1);
        var items = scope.Ninja.Items;
        scope.Meta(1, 101, 1); scope.Meta(7, 100, 1, score: 1000);
        var before = new RankingGraphWitness();
        using var packet = new LegacyNativePacket(capacity);
        FillCorrectedOnlyPacket(packet);
        packet.Size = 123;
        packet.Seek(0);
        scope.UseSingleOwnedPacketBin(packet);
        try
        {
            Assert.Throws<InvalidOperationException>(() => scope.Ninja.AddItem(100, 1, 0, 0, 0));
            Assert.Same(items, scope.Ninja.Items);
            Assert.Same(old, Assert.Single(items).Value);
            before.RequireUnchanged();
            Assert.Equal(123, packet.Size);
            Assert.Equal(0, packet.Position);
            Assert.All(ReadCorrectedOnlyPacket(packet), value => Assert.Equal((byte)0xa5, value));
        }
        finally { scope.RestoreOwnedBin(); }
        AssertNoRankingPackets(scope);
    });

    private static unsafe void FillCorrectedOnlyPacket(LegacyNativePacket packet) =>
        Marshal.Copy(Enumerable.Repeat((byte)0xa5, packet.Capacity).ToArray(), 0, (nint)packet.Memory, packet.Capacity);

    private static unsafe byte[] ReadCorrectedOnlyPacket(LegacyNativePacket packet)
    {
        var bytes = new byte[packet.Capacity];
        Marshal.Copy((nint)packet.Memory, bytes, 0, bytes.Length);
        return bytes;
    }
}
