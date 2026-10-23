using System.Buffers.Binary;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// All tests stay typed-held with the numeric-dependent whole getter until root
// approves Framework/x86 source-expression results and modern-target comparison.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeItemRemainingTimeTests
{
    private sealed class Scope : IDisposable
    {
        private readonly LegacyNativeSafeRandom previousRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime previousReset = LegacyNativePoolRandom.ResetRandom;
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativePacket Packet => Native.Packet;

        internal Scope()
        {
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(59);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            Native = new NativeSpellTestFixture();
        }

        public void Dispose()
        {
            Native.Dispose();
            LegacyNativePoolRandom.GetRandom = previousRandom; LegacyNativePoolRandom.ResetRandom = previousReset;
        }
    }

    private static DateTime EndAfter(long seconds) => DateTime.Now.AddTicks(seconds * TimeSpan.TicksPerSecond + 5000000L);

    private static void AssertClockWindow(uint actual, DateTime end, DateTime before, DateTime after)
    {
        // Independent integer tick-division bounds, not the candidate's double
        // cast or a production converter. The 2048-tick envelope covers double
        // tick/seconds rounding over the entire legal DateTime difference range
        // (<2^62 ticks, <2^39 seconds), not a one-second arbitrary allowance.
        // Exact tick-edge numeric outcomes are separately gated by the probe.
        const long roundingTicks = 2048;
        long lower = (end.Ticks - after.Ticks - roundingTicks) / TimeSpan.TicksPerSecond;
        long upper = (end.Ticks - before.Ticks + roundingTicks) / TimeSpan.TicksPerSecond;
        Assert.True(lower <= upper);
        ulong width = (ulong)(upper - lower);
        Assert.True(width < uint.MaxValue);
        uint offset = unchecked(actual - unchecked((uint)lower));
        Assert.True((ulong)offset <= width,
            "Timer is outside the independently bounded signed-seconds/low32 clock window.");
    }
}
