using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaMutationTests
{
    // Attribute-free: only the explicitly corrected-only class invokes this.
    internal static Task RunCorrectedOnlyPointStorage(int capacity, bool disposed, uint amount) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); SeedPointCredit(scope);
            using var rental = new LegacyNativePacket(capacity);
            unsafe
            {
                Marshal.Copy(Enumerable.Repeat((byte)0xA5, capacity).ToArray(),
                    0, (nint)rental.Memory, capacity);
            }
            rental.Size = capacity; rental.Seek(0);
            if (disposed) rental.Dispose();
            var packet = disposed ? null : new LegacyNativePacketMutationWitness(rental);
            var priorPool = PointsBinField.GetValue(null);
            PointsBinField.SetValue(null, new ConcurrentQueue<LegacyNativePacket>([rental]));
            try
            {
                var before = new PointCreditImage(scope);
                var error = Record.Exception(() => scope.Ninja.AddPoints(amount));
                if (!disposed && capacity == 52)
                {
                    Assert.Null(error);
                    before.RequireNonPointState(71 + amount);
                    Assert.Equal(2, scope.Queue.Count);
                    var expected = Frame(2, 0, 0, 71 + amount);
                    Assert.Equal(expected, scope.Queue.Last());
                    Assert.Equal(expected, NativeSpellTestFixture.Bytes(rental));
                    Assert.Equal(52, rental.Size); Assert.Equal(0, rental.Position);
                }
                else
                {
                    before.RequireUnchanged(requireAvailable: false);
                    if (packet is not null) packet.RequireUnchanged();
                    else unsafe
                    {
                        Assert.Equal(nint.Zero, (nint)rental.Memory);
                        Assert.Equal(capacity, rental.Capacity);
                        Assert.Equal(capacity, rental.Size);
                    }
                    Assert.IsType<InvalidOperationException>(error);
                }
            }
            finally { PointsBinField.SetValue(null, priorPool); }
        });

    // The old public storage has no real mutationGate; this gate control needs
    // the composed reviewed SpellStorage source, not a fabricated replacement.
    internal static Task RunCorrectedOnlyPointSpellGate(uint amount) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); SeedPointCredit(scope);
            var field = typeof(LegacyNativeSpellStorage).GetField("mutationGate",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            var gate = field.GetValue(scope.Native.Storage)!;
            var before = new PointCreditImage(scope);
            Exception? error;
            lock (gate) error = Record.Exception(() => scope.Ninja.AddPoints(amount));
            before.RequireUnchanged();
            Assert.IsType<InvalidOperationException>(error);
        });
}
