namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Theory]
    [InlineData("loading", "loading")]
    [InlineData("alternate", "loading")]
    [InlineData("loading", "alternate")]
    [InlineData("alternate", "alternate")]
    [InlineData("loading", "badge")]
    [InlineData("alternate", "badge")]
    public Task RealSelectedSpellComparerCannotReenterPublicNinjaWrappersWithBothGatesHeld(string outer, string nested) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            SeedSpellTransaction(scope);
            // Selected16310 is absent from the real original map. The actual
            // refresh membership comparer runs during spell preparation, while
            // the outer Ninja persistence and spell gates are already owned.
            var comparer = InstallSpellMutationComparer(scope, () => InvokeLoadingOperation(scope, nested), 16310);
            var before = new LoadingTransactionWitness(scope);
            comparer.Armed = true;

            var error = Record.Exception(() => InvokeLoadingOperation(scope, outer));

            before.RequireUnchanged();
            Assert.True(comparer.Triggered);
            Assert.IsType<InvalidOperationException>(error);
        });

    [Theory]
    [InlineData("loading")]
    [InlineData("alternate")]
    public Task SuccessfulNinjaCopyOnWriteRetainsTheActualOriginalComparer(string operation) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        SeedSpellTransaction(scope);
        var original = scope.Spells.ClientSpells;
        var retained = original[1000];
        var comparer = InstallSpellMutationComparer(scope, () => { }, 16310);
        var before = new LegacyNativeSpellMutationWitness(scope.Spells);
        comparer.Armed = true;

        InvokeLoadingOperation(scope, operation);

        Assert.True(comparer.Triggered);
        Assert.Same(comparer, scope.Spells.ClientSpells.Comparer);
        Assert.Same(retained, scope.Spells.ClientSpells[1000]);
        Assert.Equal((ushort)9, retained.Level);
        before.RequireEscapedMapUnchanged();
        Assert.Equal(new ushort[] { 1000, 2000, 16310, 16320 }, scope.Spells.ClientSpells.Keys.Order());
        Assert.Equal(2, scope.Queue.Count(frame => BitConverter.ToUInt16(frame, 2) == 2101));
    });
}
