using WolfConquer.Core.World;
using Gem = WolfConquer.Core.World.LegacyNativeGameItemFlags.Gem;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeProficiencyProgressionTests
{
    [Theory]
    [InlineData(410u, 0u)]
    [InlineData(999u, 1u)]
    public Task ZeroExperienceAndUndefinedWeaponReturnBeforeDictionaryOwnerOrStreamUse(uint id, uint gained) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var storage = scope.Storage; storage.ClientProf = null!;
            storage.CheckUpdate(id, gained, null!);
            Assert.Null(storage.ClientProf);
            AssertUntouched(scope);
        });

    [Theory]
    [InlineData(20u)]
    [InlineData(uint.MaxValue)]
    public Task ExistingLevelAtOrAboveTwentyReturnsBeforeGemOrStreamAccess(uint level) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var stored = scope.Record(level: level, experience: 123, needed: 456, previous: 7);
            scope.Client.Gems = null!;
            scope.Storage.CheckUpdate(410, uint.MaxValue, null!);
            Assert.Same(stored, scope.Storage.ClientProf[410]);
            AssertRecord(stored, 410, level, 123, 456, 7);
            AssertUntouched(scope);
        });

    [Theory]
    [InlineData(410u)]
    [InlineData(65946u)] // ushort65946 == Blade410 for Enum.IsDefined; map key remains full uint.
    public Task MissingProficiencyUsesNativeAddZeroAndDiscardsTheRequestedGain(uint id) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Storage.CheckUpdate(id, uint.MaxValue, scope.Packet);
            AssertRecord(Assert.Single(scope.Storage.ClientProf).Value, id, 0, 0, 0);
            Assert.True(scope.Storage.ClientProf.ContainsKey(id));
            AssertFrames(scope, Proficiency(id, 0, 0, 0), Experience(0, 0, id));
        });

    [Theory]
    [InlineData(1u, 1u, 150u, 1200u)]
    [InlineData(5u, 600u, 200u, 1600000u)]
    [InlineData(13u, 1400u, 200u, 213750000u)]
    [InlineData(14u, 150000u, 200u, 320625000u)]
    public Task NativeGainScalingAtLowNormalAndHighLevelEdgesUsesIntegerDivisionAndRetainsNeeded(
        uint level, uint gained, uint experience, uint needed) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var stored = scope.Record(level: level, experience: 100, needed: 777, previous: 3);
        scope.Storage.CheckUpdate(410, gained, scope.Packet);
        Assert.NotSame(stored, scope.Storage.ClientProf[410]);
        AssertRecord(stored, 410, level, 100, 777, 3);
        AssertRecord(scope.Storage.ClientProf[410], 410, level, experience, 777, 3);
        AssertFrames(scope, Experience(experience, needed, 410));
    });

    [Fact]
    public Task LowLevelMultiplyUsesWidenedArithmeticBeforeThresholds() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var before = scope.Record(level: 4, experience: 123);
        scope.Storage.CheckUpdate(410, 42949673, scope.Packet);
        Assert.NotSame(before, scope.Storage.ClientProf[410]);
        AssertRecord(before, 410, 4, 123, 991);
        AssertRecord(scope.Storage.ClientProf[410], 410, 14, 239503583, 991);
        AssertFrames(scope, Enumerable.Repeat(LevelMessage, 10).Concat(new[] {
            Proficiency(410, 14, 239503583, 320625000), Experience(239503583, 320625000, 410) }).ToArray());
    });

    [Fact]
    public Task AddingGainUsesWidenedArithmeticBeforeThresholds() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var before = scope.Record(level: 5, experience: uint.MaxValue - 3);
        scope.Storage.CheckUpdate(410, 60, scope.Packet);
        Assert.NotSame(before, scope.Storage.ClientProf[410]);
        AssertRecord(before, 410, 5, uint.MaxValue - 3, 991);
        AssertRecord(scope.Storage.ClientProf[410], 410, 18, 1071039177, 991);
        AssertFrames(scope, Enumerable.Repeat(LevelMessage, 13).Concat(new[] {
            Proficiency(410, 18, 1071039177, 1623164062), Experience(1071039177, 1623164062, 410) }).ToArray());
    });

    [Fact]
    public Task RealMutableVioletGemCategoryAddsBonusToScaledGainOnly() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var before = scope.Record(level: 5);
        scope.Client.AddGem(Gem.NormalVioletGem, 25);
        scope.Client.AddGem(Gem.SuperVioletGem, 12);
        scope.Client.Gems[1] = 999;
        Assert.Equal(37u, scope.Client.GemValues(Gem.RefinedVioletGem));
        scope.Storage.CheckUpdate(410, 600, scope.Packet);
        Assert.NotSame(before, scope.Storage.ClientProf[410]);
        AssertRecord(before, 410, 5, 0, 991);
        AssertRecord(scope.Storage.ClientProf[410], 410, 5, 137, 991);
        AssertFrames(scope, Experience(137, 1600000, 410));
        Assert.Equal((ushort)37, scope.Client.Gems[5]);
        Assert.Equal((ushort)999, scope.Client.Gems[1]);
    });
    [Fact]
    public Task VioletBonusUsesWidenedArithmeticBeforeThresholds() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var before = scope.Record(level: 5, experience: 4261412963);
        scope.Client.Gems[5] = 128;
        scope.Storage.CheckUpdate(410, 201326598, scope.Packet);
        Assert.NotSame(before, scope.Storage.ClientProf[410]);
        AssertRecord(before, 410, 5, 4261412963, 991);
        AssertRecord(scope.Storage.ClientProf[410], 410, 18, 1113988945, 991);
        AssertFrames(scope, Enumerable.Repeat(LevelMessage, 13).Concat(new[] {
            Proficiency(410, 18, 1113988945, 1623164062), Experience(1113988945, 1623164062, 410) }).ToArray());
    });
    [Fact]
    public Task GemLookupFaultRefusesBeforeMutationOrAnySend() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var stored = scope.Record(level: 5, experience: 20, needed: 777, previous: 9);
        scope.Client.Gems = null!;
        Assert.Throws<InvalidOperationException>(() => scope.Storage.CheckUpdate(410, 60, scope.Packet));
        AssertRecord(stored, 410, 5, 20, 777, 9);
        Assert.Same(stored, scope.Storage.ClientProf[410]);
        AssertUntouched(scope);
    });

    [Fact]
    public Task MultipleThresholdsSendEachRealSystemMessageBeforeSingleFinalProficiencyAndExperience() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var stored = scope.Record(level: 1, experience: 70000, needed: 777);
            scope.Storage.CheckUpdate(410, 2, scope.Packet);
            Assert.NotSame(stored, scope.Storage.ClientProf[410]);
            AssertRecord(stored, 410, 1, 70000, 777);
            AssertRecord(scope.Storage.ClientProf[410], 410, 3, 900, 777);
            AssertFrames(scope, LevelMessage, LevelMessage,
                Proficiency(410, 3, 900, 250000), Experience(900, 250000, 410));
            Assert.Equal(Experience(900, 250000, 410), NativeSpellTestFixture.Bytes(scope.Packet));
        });

    [Fact]
    public Task NaturalLevelTwentyBranchClearsExperienceAndSendsProficiencyThenMessageThenExperience() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var stored = scope.Record(level: 19, experience: 2099999995, needed: 777, previous: 21);
            scope.Storage.CheckUpdate(410, 10000, scope.Packet);
            Assert.NotSame(stored, scope.Storage.ClientProf[410]);
            AssertRecord(stored, 410, 19, 2099999995, 777, 21);
            AssertRecord(scope.Storage.ClientProf[410], 410, 20, 0, 777, 21);
            // Source supplies Player.UID17 to the first codec's Needed argument,
            // but that genuine codec recomputes threshold20 before serialization.
            AssertFrames(scope, Proficiency(410, 20, 0, 3200000000), LevelMessage,
                Experience(0, 3200000000, 410));
        });

    [Theory]
    [InlineData(2u, 67999u, 3u, (byte)7, 7u, 10000000u)]

    public Task RebornUsesIntegerHalfOfValidPreviousLevel(
        uint level, uint experience, uint gained, byte previous, uint next, uint needed) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var stored = scope.Record(level: level, experience: experience, needed: 777, previous: previous);
            scope.Storage.CheckUpdate(410, gained, scope.Packet);
            Assert.NotSame(stored, scope.Storage.ClientProf[410]);
            AssertRecord(stored, 410, level, experience, 777, previous);
            AssertRecord(scope.Storage.ClientProf[410], 410, next, 0, 777, previous);
            AssertFrames(scope, LevelMessage, Proficiency(410, next, 0, needed), Experience(0, needed, 410));
        });

    [Fact]
    public Task NativeZeroThresholdPromotesLevelZeroWithoutInventingLoopCapsOrConsumingExperience() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var stored = scope.Record(level: 0, experience: 0, needed: 777);
            scope.Storage.CheckUpdate(410, 1, scope.Packet);
            Assert.NotSame(stored, scope.Storage.ClientProf[410]);
            AssertRecord(stored, 410, 0, 0, 777);
            AssertRecord(scope.Storage.ClientProf[410], 410, 1, 100, 777);
            AssertFrames(scope, LevelMessage, Proficiency(410, 1, 100, 1200), Experience(100, 1200, 410));
        });

    [Fact]
    public Task MismatchedKeyAndRowIdRefusesWithoutMutationOrOutput() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var stored = scope.Record(level: 5, experience: 10, id: 999);
        Assert.Throws<InvalidOperationException>(() => scope.Storage.CheckUpdate(410, 60, scope.Packet));
        Assert.Same(stored, scope.Storage.ClientProf[410]);
        AssertRecord(stored, 999, 5, 10, 991);
        Assert.False(scope.Storage.ClientProf.ContainsKey(999));
        AssertUntouched(scope);
    });

    [Fact]
    public Task PreviousLevelAboveTwentyRefusesBeforeMutationOrOutput() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var stored = scope.Record(level: 9, experience: 39999999, needed: 777, previous: 21);
        Assert.Throws<InvalidOperationException>(() => scope.Storage.CheckUpdate(410, 10, scope.Packet));
        Assert.Same(stored, scope.Storage.ClientProf[410]);
        AssertRecord(stored, 410, 9, 39999999, 777, 21);
        AssertUntouched(scope);
    });
}
