using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackExecutorTests
{
    private sealed class DivineGuardFinalComparer(RefinementPool pool, int key, int finalReads) : IEqualityComparer<int>
    {
        internal bool Armed, Fired;
        internal int ReadsAfterRent;
        internal Action? OnFinalRead;
        public bool Equals(int x, int y) => x == y;
        public int GetHashCode(int value)
        {
            if (Armed && pool.Queue.IsEmpty && value == key)
            {
                ReadsAfterRent++;
                if (!Fired && ReadsAfterRent == finalReads)
                {
                    Fired = true;
                    OnFinalRead?.Invoke();
                }
            }
            return value;
        }
    }

    private static LegacyNativePreparedDivineGuardPublication PrepareGuard(LegacyNativeDivineGuardBindings bindings,
        out LegacyNativeStatusFlagsBigVector32.DivineGuardChange token)
    {
        lock (bindings.Vector.SyncRoot)
        {
            Assert.True(bindings.Vector.TryPrepareDivineGuard(bindings, out var result));
            token = result!;
            return LegacyNativePreparedDivineGuardPublication.Create(bindings, token);
        }
    }
}
