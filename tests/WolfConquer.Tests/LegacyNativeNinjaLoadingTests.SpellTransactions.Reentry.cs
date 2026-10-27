using System.Collections.Concurrent;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Theory]
    [InlineData("add", "add")]
    [InlineData("remove", "add")]
    [InlineData("reborn", "add")]
    [InlineData("clear", "add")]
    [InlineData("update", "add")]
    [InlineData("add", "remove")]
    [InlineData("remove", "remove")]
    [InlineData("reborn", "remove")]
    [InlineData("clear", "remove")]
    [InlineData("update", "remove")]
    public Task GenuineOriginalSpellComparerCannotRecursivelyMutateEitherMapOrOutputs(string outer, string nested) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            SeedSpellTransaction(scope);
            var comparer = InstallSpellMutationComparer(scope, () => InvokeSpellTransaction(scope, nested, scope.Packet));
            var before = new LoadingTransactionWitness(scope);
            comparer.Armed = true;
            var error = Record.Exception(() => InvokeSpellTransaction(scope, outer, scope.Packet));
            before.RequireUnchanged();
            Assert.True(comparer.Triggered);
            Assert.IsType<InvalidOperationException>(error);
        });

    [Theory]
    [InlineData("add", "loading")]
    [InlineData("remove", "loading")]
    [InlineData("add", "alternate")]
    [InlineData("remove", "alternate")]
    [InlineData("add", "badge")]
    [InlineData("remove", "badge")]
    public Task OriginalSpellComparerCannotEnterNinjaOperationsWithInvertedGateOrder(string outer, string nested) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            SeedSpellTransaction(scope);
            // Public Storage mutation owns its real spell gate, without the
            // Ninja persistence gate. The actual dictionary comparer reenters.
            var comparer = InstallSpellMutationComparer(scope, () => InvokeLoadingOperation(scope, nested));
            var before = new LoadingTransactionWitness(scope);
            comparer.Armed = true;
            var error = Record.Exception(() => InvokeSpellTransaction(scope, outer, scope.Packet));
            before.RequireUnchanged();
            Assert.True(comparer.Triggered);
            Assert.IsType<InvalidOperationException>(error);
        });

    [Theory]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("reborn")]
    [InlineData("clear")]
    [InlineData("update")]
    public Task ThrowingOriginalSpellComparerIsARealUnchangedRefusal(string operation) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        SeedSpellTransaction(scope);
        var comparer = InstallSpellMutationComparer(scope, () => throw new InvalidOperationException("Owned comparer refused."));
        var before = new LoadingTransactionWitness(scope);
        comparer.Armed = true;
        var error = Record.Exception(() => InvokeSpellTransaction(scope, operation, scope.Packet));
        before.RequireUnchanged();
        Assert.True(comparer.Triggered);
        Assert.IsType<InvalidOperationException>(error);
    });

    [Fact]
    public Task NonmutatingOriginalSpellComparerRemainsAValidAdmission() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Spell(1000, 9);
        var comparer = InstallSpellMutationComparer(scope, () => { });
        var escaped = new LegacyNativeSpellMutationWitness(scope.Spells);
        comparer.Armed = true;
        scope.Spells.Add(scope.Packet, 1000, 8);
        Assert.True(comparer.Triggered);
        Assert.Same(comparer, scope.Spells.ClientSpells.Comparer);
        escaped.RequireEscapedMapUnchanged();
        Assert.Equal((ushort)8, scope.Spells.ClientSpells[1000].Level);
        scope.Sent(SpellFrame(1000, 8));
    });

    private static SpellMutationComparer InstallSpellMutationComparer(Scope scope, Action reenter, ushort target = 1000)
    {
        var comparer = new SpellMutationComparer(reenter, target);
        var map = new ConcurrentDictionary<ushort, LegacyNativeMsgSpell>(comparer);
        foreach (var row in scope.Spells.ClientSpells) Assert.True(map.TryAdd(row.Key, row.Value));
        scope.Spells.ClientSpells = map;
        return comparer;
    }

    private sealed class SpellMutationComparer(Action reenter, ushort target) : IEqualityComparer<ushort>
    {
        internal bool Armed;
        internal bool Triggered;
        public bool Equals(ushort first, ushort second) => first == second;
        public int GetHashCode(ushort value)
        {
            if (Armed && value == target)
            {
                Armed = false;
                Triggered = true;
                reenter();
            }
            return value.GetHashCode();
        }
    }
}
