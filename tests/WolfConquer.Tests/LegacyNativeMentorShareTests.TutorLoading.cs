using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMentorShareTests
{
    [Fact]
    public Task TutorLoadParsesSignedRowsSkipsAnySemicolonAndUsesFullSecondFileLineCountForNewArray() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new Files();
        var old = Tutor(index: 99); LegacyNativeTutorInfo.TutorTypes = [old];
        files.Write("cq_tutor_type.txt", "50,150,140,-1,2147483647\r\n1,1,140,5,50\r\n7,1,140,1,0;whole row skipped");
        files.Write("cq_tutor_battle_limit_type.txt", ";comment\r\n0,-5\r\n2,17\r\n2,19");
        LegacyNativeTutorInfo.Load();
        Assert.Same(old, LegacyNativeTutorInfo.TutorTypes[0]); Assert.Equal(3, LegacyNativeTutorInfo.TutorTypes.Count);
        var row = LegacyNativeTutorInfo.TutorTypes[1];
        Assert.Equal(50, row.Index); Assert.Equal(150, row.MinLevel); Assert.Equal(140, row.MaxLevel);
        Assert.Equal(-1, row.StudentNum); Assert.Equal(int.MaxValue, row.BattleLevShare);
        Assert.Equal(new[] { -5, 0, 19, 0, 0 }, LegacyNativeTutorInfo.BattleLimit);
        Assert.Equal(files.Location, LegacyNativeServerConfig.DbLocation);
    });

    [Fact]
    public Task MissingTutorFileThrowsBeforeOwnerReplacementAndSecondMissingFileRetainsFirstFilePublication() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new Files();
        var oldTutors = LegacyNativeTutorInfo.TutorTypes; var oldLimits = LegacyNativeTutorInfo.BattleLimit;
        Assert.Throws<FileNotFoundException>(() => LegacyNativeTutorInfo.Load());
        Assert.Same(oldTutors, LegacyNativeTutorInfo.TutorTypes); Assert.Same(oldLimits, LegacyNativeTutorInfo.BattleLimit);
        files.Write("cq_tutor_type.txt", "7,1,140,2,50");
        Assert.Throws<FileNotFoundException>(() => LegacyNativeTutorInfo.Load());
        Assert.Equal(2, oldTutors.Count); Assert.Equal(7, oldTutors[1].Index); Assert.Same(oldLimits, LegacyNativeTutorInfo.BattleLimit);
        LegacyNativeServerConfig.DbLocation = files.Root;
        Assert.Throws<FileNotFoundException>(() => LegacyNativeTutorInfo.Load()); //No inserted separator/search.
        Assert.Equal(2, oldTutors.Count);
    });

    [Fact]
    public Task TutorLoadKeepsPrefixRowsOnParseFaultAndAcceptsDuplicateTypeIndexesAcrossReloads() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new Files(); LegacyNativeTutorInfo.TutorTypes = [];
        files.Write("cq_tutor_type.txt", "7,1,140,2,50\r\n7,1,bad,2,40\r\n3,1,140,3,30");
        files.Write("cq_tutor_battle_limit_type.txt", "0,9");
        var oldLimits = LegacyNativeTutorInfo.BattleLimit;
        Assert.Throws<FormatException>(() => LegacyNativeTutorInfo.Load());
        Assert.Single(LegacyNativeTutorInfo.TutorTypes); Assert.Same(oldLimits, LegacyNativeTutorInfo.BattleLimit);
        files.Write("cq_tutor_type.txt", "7,1,140,2,50"); LegacyNativeTutorInfo.Load(); LegacyNativeTutorInfo.Load();
        Assert.Equal(new[] { 7, 7, 7 }, LegacyNativeTutorInfo.TutorTypes.Select(row => row.Index));
    });

    [Fact]
    public Task TutorLimitReplacementAndPrefixWritesSurviveLaterOutOfRangeRow() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new Files();
        files.Write("cq_tutor_type.txt", ";comment"); files.Write("cq_tutor_battle_limit_type.txt", "0,7\r\n9,99\r\n1,8");
        var old = LegacyNativeTutorInfo.BattleLimit;
        Assert.Throws<IndexOutOfRangeException>(() => LegacyNativeTutorInfo.Load());
        Assert.NotSame(old, LegacyNativeTutorInfo.BattleLimit); Assert.Equal(new[] { 7, 0, 0, 0 }, LegacyNativeTutorInfo.BattleLimit);
    });

    [Theory]
    [InlineData("", typeof(FormatException))]
    [InlineData("1,2", typeof(IndexOutOfRangeException))]
    [InlineData("2147483648,1,140,1,50", typeof(OverflowException))]
    public Task TutorLoadDoesNotIgnoreBlankRowsOrRelaxSignedWidths(string text, Type exception) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new Files(); LegacyNativeTutorInfo.TutorTypes = [];
        files.Write("cq_tutor_type.txt", text + "\r\n"); files.Write("cq_tutor_battle_limit_type.txt", "0,1");
        Assert.Throws(exception, () => LegacyNativeTutorInfo.Load()); Assert.Empty(LegacyNativeTutorInfo.TutorTypes);
    });
}
