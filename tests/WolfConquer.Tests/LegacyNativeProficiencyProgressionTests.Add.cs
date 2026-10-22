using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeProficiencyProgressionTests
{
    [Fact]
    public Task AddNewInsertsRealMessageThenQueuesProficiencyBeforeExperienceWithoutChat() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Storage.Add(scope.Packet, 410, 6, 123, 4);
            AssertRecord(Assert.Single(scope.Storage.ClientProf).Value, 410, 6, 123, 4000000, 4);
            AssertFrames(scope, Proficiency(410, 6, 123, 4000000), Experience(123, 4000000, 410));
            Assert.Equal(Experience(123, 4000000, 410), NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(4, scope.Packet.Position);
        });

    [Fact]
    public Task AddExistingMutatesSameStoredReferenceAndUsesItsIdRatherThanDictionaryKey() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var stored = scope.Record(level: 5, experience: 77, previous: 2, id: 420);
            scope.Storage.Add(scope.Packet, 410, 6, 123, 4);
            Assert.Same(stored, scope.Storage.ClientProf[410]);
            AssertRecord(stored, 420, 6, 123, 4000000, 4);
            Assert.False(scope.Storage.ClientProf.ContainsKey(420));
            AssertFrames(scope, Proficiency(420, 6, 123, 4000000), Experience(123, 4000000, 420));
        });

    [Theory]
    [InlineData(20u, false, 0u, 3200000000u)]
    [InlineData(21u, false, 123u, 3200000000u)]
    [InlineData(21u, true, 0u, 3200000000u)]
    [InlineData(256u, false, 123u, 0u)]
    public Task AddClearsOnlyExactLevelTwentyOrExplicitFlagAndNarrowsThresholdLookup(
        uint level, bool clear, uint experience, uint needed) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Storage.Add(scope.Packet, 410, level, 123, 9, clear);
        AssertRecord(scope.Storage.ClientProf[410], 410, level, experience, needed, 9);
        AssertFrames(scope, Proficiency(410, level, experience, needed), Experience(experience, needed, 410));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task AddNullStreamRetainsPriorInsertOrMutationButCannotQueueFirstPacket(bool existing) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            LegacyNativeMsgProficiency? old = existing ? scope.Record() : null;
            Assert.Throws<NullReferenceException>(() => scope.Storage.Add(null!, 410, 6, 123, 4));
            var actual = scope.Storage.ClientProf[410];
            AssertRecord(actual, 410, 6, 123, 4000000, 4);
            if (existing) Assert.Same(old, actual);
            AssertUntouched(scope);
        });

    [Fact]
    public Task AddThresholdLookupFaultLeavesExistingValueAndDictionaryUntouched() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var original = scope.Record(level: 5, experience: 77, needed: 88, previous: 9);
            Scope.Table.SetValue(null, null);
            Assert.Throws<NullReferenceException>(() => scope.Storage.Add(scope.Packet, 410, 6, 123, 4));
            Assert.Same(original, scope.Storage.ClientProf[410]);
            AssertRecord(original, 410, 5, 77, 88, 9);
            AssertUntouched(scope);
        });
}
