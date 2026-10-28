using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackDispatchTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ConsumedTrainingMetadataIdentityRefusesBeforeEitherProficiencyPass(bool wrongLevel) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var comparer = SeedTrainingMetadata(scope);
            if (wrongLevel) scope.Row.Level = 1;
            else scope.Row.ID = 1196;
            var before = new TrainingMetadataImage(scope);
            comparer.Tracking = true;

            var error = Record.Exception(() =>
                LegacyNativeUpdateSpell.CheckUpdate(scope.Packet, scope.Client, scope.Attack, 600, scope.Rows));

            before.RequireUnchanged();
            Assert.Empty(comparer.Lookups);
            Assert.Equal(wrongLevel ? (ushort)1195 : (ushort)1196, scope.Row.ID);
            Assert.Equal(wrongLevel ? (byte)1 : (byte)0, scope.Row.Level);
            Assert.Null(error); // Same fail-soft boundary as a missing/null consumed row.
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RealProficiencyLookupChangingMetadataIdentityStopsRemainingTrainingAndXp(bool changeLevel) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var comparer = SeedTrainingMetadata(scope, () =>
            {
                if (changeLevel) scope.Row.Level = 1;
                else scope.Row.ID = 1196;
            });
            var before = new TrainingMetadataImage(scope);
            comparer.Tracking = comparer.Armed = true;

            var error = Record.Exception(() =>
                LegacyNativeUpdateSpell.CheckUpdate(scope.Packet, scope.Client, scope.Attack, 600, scope.Rows));

            before.RequireUnchanged();
            Assert.True(comparer.Triggered);
            Assert.Contains(410u, comparer.Lookups);
            Assert.Equal(changeLevel ? (ushort)1195 : (ushort)1196, scope.Row.ID);
            Assert.Equal(changeLevel ? (byte)1 : (byte)0, scope.Row.Level);
            Assert.Null(error);
            // No earlier proficiency pass is published before the final
            // consumed-metadata fence of the compound training operation.
        });
}
