using Shop = WolfConquer.Core.World.LegacyNativeExchangeShopRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeExchangeShopTests
{
    // This is an actual Dictionary dependency that fails while constructing staged
    // storage, not an accepting saver, result callback, forced RNG or fake queue.
    private sealed class RefreshThrowingComparer : IEqualityComparer<uint>
    {
        internal bool Enabled { get; set; }
        internal int ThrowOnCall { get; set; }
        internal int Calls { get; private set; }

        public bool Equals(uint left, uint right) => left == right;

        public int GetHashCode(uint value)
        {
            if (Enabled && ++Calls >= ThrowOnCall)
                throw new InvalidOperationException("Owned staged exchange dictionary materialization failure.");
            return value.GetHashCode();
        }
    }

    [Theory]
    [InlineData(false, 1)] [InlineData(false, 4)]
    [InlineData(true, 1)] [InlineData(true, 4)]
    public Task RefreshRealDictionaryMaterializationFailureCannotPublishOrClearAPrefix(bool reset, int throwOnCall)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope();
        ReadyRefreshPrefix(scope);
        var comparer = new RefreshThrowingComparer { ThrowOnCall = throwOnCall };
        scope.Shop.Items = new Dictionary<uint, Shop.Item>(scope.Shop.Items, comparer);
        ExCatalog(scope.State, 168, 1001, 8);
        var before = CaptureRefresh(scope);
        comparer.Enabled = true;
        try
        {
            bool accepted = reset ? scope.Shop.TryReset(out var reason) : scope.Shop.TryGetRandomEx(168, 8, out reason);
            Assert.False(accepted);
            Assert.StartsWith("exchangeShop", reason);
            Assert.True(comparer.Calls >= throwOnCall);
        }
        finally { comparer.Enabled = false; }
        AssertRefreshRejected(scope, before);
    });
}
