using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Theory]
    [InlineData("loading", "player")]
    [InlineData("alternate", "player")]
    [InlineData("badge", "player")]
    [InlineData("loading", "equipment")]
    [InlineData("alternate", "equipment")]
    [InlineData("badge", "equipment")]
    [InlineData("loading", "ninja")]
    [InlineData("alternate", "ninja")]
    [InlineData("badge", "ninja")]
    [InlineData("loading", "uid")]
    [InlineData("alternate", "uid")]
    [InlineData("badge", "uid")]
    public Task PreparedNinjaConsumersRequireTheirActualCurrentPhysicalOwners(string operation, string invalid) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            using var other = new NativeSpellTestFixture();
            SeedLoadingTransaction(scope);
            // Both genuine players have UID17: equality cannot confer authority.
            if (invalid == "player") scope.Client.Player = other.Owner.Player;
            if (invalid == "equipment")
            {
                other.Owner.Equipment = new LegacyNativeEquipItemStorage(other.Owner);
                scope.Client.Equipment = other.Owner.Equipment;
            }
            if (invalid == "ninja") scope.Client.MyNinja = new LegacyNativeNinja(scope.Client);
            if (invalid == "uid") scope.Client.Player.UID = 0;
            var before = new LoadingTransactionWitness(scope);
            var foreignSpells = new LegacyNativeSpellMutationWitness(other.Storage);

            var error = Record.Exception(() => InvokeLoadingOperation(scope, operation));

            before.RequireUnchanged();
            foreignSpells.RequireUnchanged();
            Assert.Empty(other.Queue);
            Assert.IsType<InvalidOperationException>(error);
        });

    [Theory]
    [InlineData("loading", false)]
    [InlineData("alternate", false)]
    [InlineData("loading", true)]
    [InlineData("alternate", true)]
    public Task SkillConsumersRefuseMissingOrForeignRealSpellStorageBeforeAnyOutput(string operation, bool foreign) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            using var other = new NativeSpellTestFixture();
            SeedLoadingTransaction(scope);
            scope.Client.MySpells = foreign ? other.Storage : null;
            var before = new LoadingTransactionWitness(scope);
            var foreignSpells = new LegacyNativeSpellMutationWitness(other.Storage);

            var error = Record.Exception(() => InvokeLoadingOperation(scope, operation));

            before.RequireUnchanged();
            foreignSpells.RequireUnchanged();
            Assert.Empty(other.Queue);
            Assert.IsType<InvalidOperationException>(error);
        });
}
