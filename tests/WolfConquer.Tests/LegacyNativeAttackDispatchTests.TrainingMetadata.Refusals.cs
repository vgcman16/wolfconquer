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

            before.RequireSpellStateUnchanged();
            before.RequireProficiencies(rightGain: 100);
            before.RequirePriorFrames(1);
            Assert.True(comparer.Triggered);
            Assert.Equal(new uint[] { 410 }, comparer.Lookups);
            // The first genuine proficiency credit remains: 1000 + 600/(5+1).
            // Existing opcode2199: experience1100, level5 threshold1600000, weapon410, eight zero bytes.
            var frame = scope.Queue.Last();
            Assert.Equal(Convert.FromHexString("180097084C040000006A18009A0100000000000000000000"), frame);
            Assert.Equal(frame, NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(4, scope.Packet.Position);
            Assert.Equal(changeLevel ? (ushort)1195 : (ushort)1196, scope.Row.ID);
            Assert.Equal(changeLevel ? (byte)1 : (byte)0, scope.Row.Level);
            Assert.Null(error);
            // This fence does not roll back the earlier proficiency operation.
        });
}
