using System.Reflection;
using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Theory]
    [InlineData("loading", false)]
    [InlineData("alternate", false)]
    [InlineData("loading", true)]
    [InlineData("alternate", true)]
    public Task SelectedSkillNarrowingRefusesTheEntirePreparedOperation(string operation, bool level) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            SeedLoadingTransaction(scope);
            if (level) { scope.Ninja.Items[100].Level = 258; scope.Metadata[1].Level = 258; }
            else scope.Metadata[1].magic2 = 70000;
            var before = new LoadingTransactionWitness(scope);
            var error = Record.Exception(() => InvokeLoadingOperation(scope, operation));
            before.RequireUnchanged();
            Assert.IsType<InvalidOperationException>(error);
        });

    [Theory]
    [InlineData("loading")]
    [InlineData("badge")]
    public Task SelectedLevelSumOverflowCannotWrapIntoABadgeOrLeaveOtherOutputs(string operation) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            SeedLoadingTransaction(scope);
            scope.Ninja.Items[100].Level = uint.MaxValue;
            scope.Metadata[1].Level = uint.MaxValue;
            scope.Item(200, 200, 1, 2);
            scope.Meta(2, 200, 1, magic1: 16310, magic2: 16320);
            scope.Spell(16310); scope.Spell(16320); // Existing rows require no byte-level cast.
            var before = new LoadingTransactionWitness(scope);
            var error = Record.Exception(() => InvokeLoadingOperation(scope, operation));
            before.RequireUnchanged();
            Assert.IsType<InvalidOperationException>(error);
        });

    [Theory]
    [InlineData("loading")]
    [InlineData("alternate")]
    [InlineData("badge")]
    public Task InvalidCapturedSealRefusesBeforeEveryPreparedNinjaStore(string operation) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            SeedLoadingTransaction(scope);
            typeof(LegacyNativePacket).GetField("seal", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, "short");
            var before = new LoadingTransactionWitness(scope);
            var error = Record.Exception(() => InvokeLoadingOperation(scope, operation));
            before.RequireUnchanged();
            Assert.IsType<InvalidDataException>(error);
        });
}
