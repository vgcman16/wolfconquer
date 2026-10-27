using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Theory]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("reborn")]
    [InlineData("clear")]
    [InlineData("update")]
    public Task EveryTypedSpellMutationRefusesMissingFrameStorageWithoutDirtyOutputs(string operation) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            SeedSpellTransaction(scope);
            var before = new LoadingTransactionWitness(scope);
            var error = Record.Exception(() => InvokeSpellTransaction(scope, operation, null!));
            before.RequireUnchanged();
            Assert.IsType<InvalidOperationException>(error);
        });

    [Theory]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("reborn")]
    [InlineData("clear")]
    [InlineData("update")]
    public Task EveryTypedSpellMutationRequiresItsCurrentStorageSlotAndPositiveActorUid(string operation) =>
        NativePacketTestThread.Run(() =>
        {
            foreach (var zeroUid in new[] { false, true })
            {
                using var scope = new Scope();
                SeedSpellTransaction(scope);
                if (zeroUid) scope.Client.Player.UID = 0;
                else scope.Client.MySpells = new LegacyNativeSpellStorage(scope.Client);
                var before = new LoadingTransactionWitness(scope);
                var error = Record.Exception(() => InvokeSpellTransaction(scope, operation, scope.Packet));
                before.RequireUnchanged();
                Assert.IsType<InvalidOperationException>(error);
            }
        });

    [Theory]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("reborn")]
    [InlineData("clear")]
    [InlineData("update")]
    public Task CompleteMalformedSpellTailsRefuseBeforeAnyTypedMutation(string operation) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            SeedSpellTransaction(scope);
            scope.Spells.ClientSpells[60000] = new() { ID = 60001, Level = 4 };
            var before = new LoadingTransactionWitness(scope);
            var error = Record.Exception(() => InvokeSpellTransaction(scope, operation, scope.Packet));
            before.RequireUnchanged();
            Assert.IsType<InvalidOperationException>(error);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RebornAndClearCannotNarrowAnActualRetainedPreviousLevel(bool clear) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        SeedSpellTransaction(scope);
        scope.Spells.ClientSpells[1000].Level = 300;
        var before = new LoadingTransactionWitness(scope);
        var error = Record.Exception(() =>
        {
            if (clear) scope.Spells.ClearSpells([1000, 2000], scope.Packet);
            else scope.Spells.RebornSpell(scope.Packet, 1000);
        });
        before.RequireUnchanged();
        Assert.IsType<InvalidOperationException>(error);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RequiredSpellMutationObjectsAreRejectedWithoutAnyPrefix(bool update) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        SeedSpellTransaction(scope);
        var before = new LoadingTransactionWitness(scope);
        var error = Record.Exception(() =>
        {
            if (update) scope.Spells.Update(scope.Packet, null!);
            else scope.Spells.ClearSpells(null!, scope.Packet);
        });
        before.RequireUnchanged();
        Assert.IsType<ArgumentNullException>(error);
    });
}
