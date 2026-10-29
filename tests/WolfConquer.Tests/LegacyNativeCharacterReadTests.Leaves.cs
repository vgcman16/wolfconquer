using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCharacterReadTests
{
    [Theory]
    [InlineData(649u, 10u, true, 659u, 30u, 0)]
    [InlineData(650u, 1u, true, 651u, 21u, 0)]
    [InlineData(651u, 1u, true, 651u, 20u, 1)]
    [InlineData(651u, 1u, false, 652u, 21u, 0)]
    [InlineData(651u, 0u, true, 651u, 20u, 1)]
    public Task WholeChampionAwardKeepsStrictCapAndFalseOptionStillIncrementsToday(uint today, uint value, bool checkCap, uint finalToday, uint finalPoints, int frames) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Player.TodayChampionPoints = today; scope.Player.ChampionPoints = 20;
        scope.Player.AddChampionPoints(value, checkCap);
        Assert.Equal(finalToday, scope.Player.TodayChampionPoints); Assert.Equal(finalPoints, scope.Player.ChampionPoints);
        Assert.Equal(Math.Max(20u, finalPoints), scope.Player.HistoryChampionPoints); Assert.Equal(frames, scope.Queue.Count);
        if (frames > 0) Assert.Equal((ushort)2179, Opcode(scope.Queue.Peek()));
    });

    [Fact]
    public Task ChampionOverflowKeepsUncheckedWrapAndHistoricalMaximum() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Player.ChampionPoints = uint.MaxValue; scope.Player.TodayChampionPoints = uint.MaxValue;
        scope.Player.AddChampionPoints(2, false);
        Assert.Equal(1u, scope.Player.ChampionPoints); Assert.Equal(1u, scope.Player.TodayChampionPoints);
        Assert.Equal(uint.MaxValue, scope.Player.HistoryChampionPoints); Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(0u, 0u)] [InlineData(15u, 15u)] [InlineData(16u, 15u)] [InlineData(4294967295u, 15u)]
    public Task GuildShareWholeMethodMutatesActualInfoAndDoesNotInventRankPolicy(uint before, uint after) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var guild = LegacyNativeGuildRuntime.CreateLoadedGuild("Owned"); guild.Info.ArsenalBP = before;
        Assert.Equal(after, guild.ShareMemberPotency((LegacyNativeGuildMemberRank)65535)); Assert.Equal(after, guild.Info.ArsenalBP);
        Assert.Equal(after, guild.ShareMemberPotency(LegacyNativeGuildMemberRank.None));
        guild.Info = null!; Assert.Throws<NullReferenceException>(() => guild.ShareMemberPotency(LegacyNativeGuildMemberRank.GuildLeader));
    });

    [Fact]
    public Task GuildUnionLookupReadsActualCatalogEveryTimeWithoutCreatingOrCachingOwner() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var guild = LegacyNativeGuildRuntime.CreateLoadedGuild("Owned"); guild.UnionID = 9;
        Assert.Null(guild.GetUnion); var first = new LegacyNativeUnionRuntime { UID = 9 };
        LegacyNativeUnionRuntime.UnionPoll[9] = first; Assert.Same(first, guild.GetUnion);
        var second = new LegacyNativeUnionRuntime { UID = 9 }; LegacyNativeUnionRuntime.UnionPoll[9] = second;
        Assert.Same(second, guild.GetUnion); LegacyNativeUnionRuntime.UnionPoll.TryRemove(9, out _); Assert.Null(guild.GetUnion);
    });

    [Theory]
    [InlineData(1002u, false, false)] [InlineData(1008u, true, false)] [InlineData(2007u, false, true)]
    [InlineData(2008u, false, false)] [InlineData(11008u, false, false)]
    public void SourceGenderChecksAreExactIdsNotGeneralMeshClassification(uint mesh, bool boy, bool girl)
    { Assert.Equal(boy, LegacyNativeRoleCore.IsBoy(mesh)); Assert.Equal(girl, LegacyNativeRoleCore.IsGirl(mesh)); }

    [Theory]
    [InlineData("", "")] [InlineData("1010", "0101")] [InlineData("ab中", "中ba")]
    public void ReverseStringRetainsSourceUtf16CodeUnitOrder(string input, string expected) => Assert.Equal(expected, LegacyNativeBaseFunc.ReverseString(input));

    [Fact]
    public void ReverseStringReversesSurrogateCodeUnitsRatherThanUnicodeScalars()
    {
        string input = new(new[] { '\uD83D', '\uDE00' });
        string expected = new(new[] { '\uDE00', '\uD83D' });
        Assert.Equal(expected, LegacyNativeBaseFunc.ReverseString(input));
    }

    [Fact]
    public void ReverseNullKeepsNaturalNullReferenceFault() => Assert.Throws<NullReferenceException>(() => LegacyNativeBaseFunc.ReverseString(null!));

    [Fact]
    public void ReverseStringRetainsLiteralSourcePublicSignature()
    {
        var method = typeof(LegacyNativeBaseFunc).GetMethod("ReverseString")!;
        Assert.True(method.IsPublic && method.IsStatic); Assert.Equal(typeof(string), method.ReturnType);
        var parameter = Assert.Single(method.GetParameters()); Assert.Equal(typeof(string), parameter.ParameterType);
        Assert.Equal("text", parameter.Name); Assert.False(parameter.IsOptional);
    }

    [Theory]
    [InlineData(89, 0, 0, 0)] [InlineData(90, 0, 0, 200)] [InlineData(90, 1, 3, 300)]
    [InlineData(90, 3, 3, 300)] [InlineData(90, 5, 4, 500)] [InlineData(90, 7, 5, 500)]
    [InlineData(90, 9, 6, 700)] [InlineData(90, 12, 6, 800)]
    public Task FullEnlightenCalculationUsesActualLevelRankAndVipThresholds(int level, int rank, int vip, int expected) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        typeof(LegacyNativePlayerRuntime).GetField("_level", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(scope.Player, (ushort)level);
        scope.Player.NobilityRank = (LegacyNativeNobilityRank)rank; scope.Player.VipLevel = (byte)vip;
        Assert.Equal((ushort)expected, LegacyNativeServerDatabasePersistence.CalculateEnlighten(scope.Player));
    });

    [Fact]
    public void InitialOnlineSqlShapeIsRealUnfilteredUpdateButThisControlDoesNotExecuteIt()
    {
        using var command = new LegacyNativeMySqlCommand(LegacyNativeMySqlCommandType.UPDATE);
        Assert.Same(command, command.Update("configuration").Set("PlayersOnline", 41u));
        using var sql = command.CreateProviderCommand();
        Assert.Equal("UPDATE `configuration` SET `PlayersOnline` = @p0", sql.CommandText);
        Assert.Equal(41u, Convert.ToUInt32(sql.Parameters[0].Value));
    }
}
