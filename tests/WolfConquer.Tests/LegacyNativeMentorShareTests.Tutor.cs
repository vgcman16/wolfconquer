using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMentorShareTests
{
    [Theory]
    [InlineData(0, 80)] [InlineData(100, 80)] [InlineData(200, 50)]
    [InlineData(250, 25)] [InlineData(299, 0)] [InlineData(300, 0)] [InlineData(400, 0)]
    public Task ShareBattleUsesRealMentorBpIntegerTruncationAndActualStudentLimit(int studentBp, int expected) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); Assert.Equal(300, scope.Player.RealBattlePower);
        Assert.Equal(expected, LegacyNativeTutorInfo.ShareBattle(scope.Client, studentBp));
        Assert.Equal(unchecked((uint)expected), scope.Player.GetShareBattlePowers((uint)studentBp));
        scope.Player.ExtraBattlePower = 999;
        Assert.Equal(expected, LegacyNativeTutorInfo.ShareBattle(scope.Client, studentBp)); //Not BattlePower/extra BP.
    });

    [Theory]
    [InlineData(-50, -50)] [InlineData(0, 0)] [InlineData(1, 1)]
    [InlineData(50, 50)] [InlineData(1000, 80)] [InlineData(2147483647, -1)]
    public Task ShareBattlePreservesNegativePercentAndUncheckedSignedProductBeforeUIntWrapper(int percent, int expected) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); LegacyNativeTutorInfo.TutorTypes[0].BattleLevShare = percent;
        Assert.Equal(expected, LegacyNativeTutorInfo.ShareBattle(scope.Client, 200));
        Assert.Equal(unchecked((uint)expected), scope.Player.GetShareBattlePowers(200));
    });

    [Fact]
    public Task ShareBattleUsesLastArraySlotForLargeStudentBpButRetainsNativeNegativeAndEmptyIndexFaults() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); LegacyNativeTutorInfo.BattleLimit = [5, 13, 17];
        Assert.Equal(17, LegacyNativeTutorInfo.ShareBattle(scope.Client, 200));
        Assert.Throws<IndexOutOfRangeException>(() => LegacyNativeTutorInfo.ShareBattle(scope.Client, -1));
        Assert.Throws<IndexOutOfRangeException>(() => scope.Player.GetShareBattlePowers(uint.MaxValue));
        LegacyNativeTutorInfo.BattleLimit = [];
        Assert.Throws<IndexOutOfRangeException>(() => LegacyNativeTutorInfo.ShareBattle(scope.Client, 0));
        LegacyNativeTutorInfo.BattleLimit = null!;
        Assert.Equal(0, LegacyNativeTutorInfo.ShareBattle(scope.Client, 300));
        LegacyNativeTutorInfo.TutorTypes = null!;
        Assert.Equal(0, LegacyNativeTutorInfo.ShareBattle(scope.Client, 300));
        Assert.Throws<NullReferenceException>(() => LegacyNativeTutorInfo.ShareBattle(scope.Client, 200));
    });

    [Fact]
    public Task GetTutorInfoUsesFirstPhysicalMatchAndClampsOnlyUpperLevelComparisonTo140() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var first = Tutor(100, 140, -1, 7, 50); var second = Tutor(1, 140, 99, 80, 1);
        LegacyNativeTutorInfo.TutorTypes = [first, second];
        Assert.Same(first, LegacyNativeTutorInfo.GetTutorInfo(scope.Client)); Assert.Equal(-1, LegacyNativeTutorInfo.AddAppCount(scope.Client));
        Set(scope.Player, "_level", (ushort)99); Assert.Same(second, LegacyNativeTutorInfo.GetTutorInfo(scope.Client));
        Set(scope.Player, "_level", (ushort)200);
        first.MinLevel = 150; first.MaxLevel = 140;
        Assert.Same(first, LegacyNativeTutorInfo.GetTutorInfo(scope.Client)); //Native Min can exceed Max.
        Assert.Equal(50, first.Index); Assert.Equal(1, second.Index);
        LegacyNativeTutorInfo.TutorTypes = [];
        Assert.Null(LegacyNativeTutorInfo.GetTutorInfo(null!)); Assert.Equal(0, LegacyNativeTutorInfo.AddAppCount(null!));
        LegacyNativeTutorInfo.TutorTypes = [null!];
        Assert.Throws<NullReferenceException>(() => LegacyNativeTutorInfo.GetTutorInfo(scope.Client));
    });

    [Fact]
    public Task SourceEmptyMentorSetterDoesNotChangePrivateContributionExtraBpOrPublishAnything() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Player.ExtraBattlePower = 37;
        Set(scope.Player, "_mentorBp", 11u);
        scope.Player.SetMentorBattlePowers(uint.MaxValue, uint.MaxValue);
        Assert.Equal(37u, scope.Player.ExtraBattlePower);
        Assert.Equal(11u, typeof(LegacyNativePlayerRuntime).GetField("_mentorBp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(scope.Player));
        Assert.Empty(scope.Queue);
        scope.Client.Equipment = null;
        scope.Player.SetMentorBattlePowers(10, 100); //The complete native body really has no owner reads.
        Assert.Equal(37u, scope.Player.ExtraBattlePower);
    });
}
