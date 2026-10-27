using System.Collections.Concurrent;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Fact]
    public Task AddItemCopyOnWriteRetainsTheActualItemComparerAndEscapedRows() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        SeedLoadingTransaction(scope);
        var old = scope.Ninja.Items[100];
        var other = scope.Item(101, 101, 1);
        scope.Meta(2, 101, 1);
        var comparer = new ItemPreparationComparer("normal");
        InstallPreparationItemMap(scope, comparer);
        var original = scope.Ninja.Items;
        comparer.Armed = true;

        scope.Ninja.AddItem(100, 9, 1, 4, 5);

        Assert.True(comparer.TargetReads >= 3);
        Assert.NotSame(original, scope.Ninja.Items);
        Assert.Same(comparer, scope.Ninja.Items.Comparer);
        Assert.Same(old, original[100]);
        Assert.Same(other, original[101]);
        Assert.Same(other, scope.Ninja.Items[101]);
        Assert.NotSame(old, scope.Ninja.Items[100]);
        Assert.Equal(new uint[] { 100, 9, 1, 4, 5 }, new[] { old.ItemID, old.Level, old.Position, old.Exp, old.UK });
        scope.Sent(ItemFrame(LegacyNativeMsgNinja.MsgNinjaItem.TypeID.Add, 17, (100, 9, 1, 4, 0)));
    });

    [Theory]
    [InlineData("throw")]
    [InlineData("lookup")]
    [InlineData("insert")]
    public Task ItemComparerFailureCannotPublishAnInconsistentPrivateCandidate(string mode) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        SeedLoadingTransaction(scope);
        var comparer = new ItemPreparationComparer(mode);
        InstallPreparationItemMap(scope, comparer);
        var before = new LoadingTransactionWitness(scope);
        comparer.Armed = true;

        var error = Record.Exception(() => scope.Ninja.AddItem(100, 9, 1, 4, 5));

        before.RequireUnchanged();
        Assert.True(comparer.FaultUsed);
        Assert.Equal(mode == "insert" ? 3 : 1, comparer.TargetReads);
        Assert.IsType<InvalidOperationException>(error);
    });

    private static void InstallPreparationItemMap(Scope scope, IEqualityComparer<uint> comparer)
    {
        var map = new ConcurrentDictionary<uint, LegacyNativeNinja.Item>(comparer);
        foreach (var pair in scope.Ninja.Items) Assert.True(map.TryAdd(pair.Key, pair.Value));
        scope.Ninja.Items = map;
    }

    private sealed class ItemPreparationComparer(string mode) : IEqualityComparer<uint>
    {
        internal bool Armed;
        internal bool FaultUsed;
        internal int TargetReads;
        public bool Equals(uint first, uint second) => first == second;
        public int GetHashCode(uint value)
        {
            if (Armed && value == 100)
            {
                TargetReads++;
                if (mode == "throw" && TargetReads == 1)
                { FaultUsed = true; throw new InvalidOperationException("Owned item comparer refused."); }
                if (mode == "lookup" && TargetReads == 1 || mode == "insert" && TargetReads == 3)
                { FaultUsed = true; return 101; }
            }
            return value.GetHashCode();
        }
    }
}
