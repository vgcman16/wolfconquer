using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackDispatchTests
{
    [Fact]
    public Task MatchingConsumedMetadataRetainsDoubleProficiencyOrderAndLiteralSpellFrame() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var comparer = SeedTrainingMetadata(scope);
            var before = new TrainingMetadataImage(scope);
            comparer.Tracking = true;

            LegacyNativeUpdateSpell.CheckUpdate(scope.Packet, scope.Client, scope.Attack, 600, scope.Rows);

            before.RequireProficiencies(200, 200);
            before.RequirePriorFrames(5);
            Assert.Equal(new uint[] { 410, 420, 410, 420 }, comparer.Lookups);
            Assert.Same(scope.ClientSpell, scope.Native.Storage.ClientSpells[1195]);
            Assert.Equal(600, scope.ClientSpell.Experience);
            Assert.Equal((ushort)0, scope.ClientSpell.Level);
            var frames = scope.Queue.Skip(before.PriorFrameCount).ToArray();
            Assert.Equal(new ushort[] { 2199, 2199, 2101, 2199, 2199 }, frames.Select(Opcode));
            // Existing fixed payload: experience600, ID1195, other fields zero.
            Assert.Equal(Convert.FromHexString("1900350858020000AB04000000000000000000000000000000"), frames[2]);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CappedTrainingKeepsUnusedMetadataIdentityOutsideTheConsumedRowContract(bool wrongLevel) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var comparer = SeedTrainingMetadata(scope);
            scope.Rows.Remove(1); // Current level0 is capped by the retained Count-1 expression.
            if (wrongLevel) scope.Row.Level = 9;
            else scope.Row.ID = 1196;
            var before = new TrainingMetadataImage(scope);
            comparer.Tracking = true;

            var error = Record.Exception(() =>
                LegacyNativeUpdateSpell.CheckUpdate(scope.Packet, scope.Client, scope.Attack, 600, scope.Rows));

            before.RequireSpellStateUnchanged();
            before.RequireProficiencies(200, 200);
            before.RequirePriorFrames(4);
            Assert.Equal(new uint[] { 410, 420, 410, 420 }, comparer.Lookups);
            Assert.All(scope.Queue.Skip(before.PriorFrameCount), frame => Assert.Equal((ushort)2199, Opcode(frame)));
            Assert.Null(error);
        });
}
