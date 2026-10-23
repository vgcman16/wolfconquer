using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Action = WolfConquer.Core.World.LegacyNativeClientAchievement.TypeAchievement;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAchievementTests
{
    [Fact]
    public void ContractKeepsNativeEnumPhysicalOwnersAndNoUidCacheOrPartialReceiver()
    {
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(Action)));
        Assert.Equal(new[] { "BigHash", "CheakView", "SendQueue" }, Enum.GetNames<Action>());
        Assert.Equal(new uint[] { 0, 1, 2 }, Enum.GetValues<Action>().Select(x => (uint)x));
        Assert.Equal(14, LegacyNativeClientAchievement.File_Size);
        var fields = typeof(LegacyNativeClientAchievement).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var field = Assert.Single(fields); Assert.Equal("BitVector32", field.Name); Assert.True(field.IsPrivate);
        Assert.Equal(typeof(LegacyNativeBitVector32), field.FieldType);
        // The whole receiver requires the genuine Player IMapObj/GetArray graph.
        // Keep it absent rather than compile a reduced SendQueue-only handler.
        Assert.Null(typeof(LegacyNativeClientAchievement).GetMethod("AchievementHandler"));
        Assert.Equal(typeof(LegacyNativeAchievementCollection), typeof(LegacyNativeGameClientRuntime)
            .GetField("Achievement", BindingFlags.Instance | BindingFlags.NonPublic)!.FieldType);
        Assert.Equal(typeof(LegacyNativeClientAchievement), typeof(LegacyNativePlayerRuntime)
            .GetField("Achievement", BindingFlags.Instance | BindingFlags.NonPublic)!.FieldType);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(13)]
    [InlineData(14)]
    public void ConstructorCopiesEveryInputWordAndMyFlagsAlwaysReturnsIndependentFourteenWords(int length)
    {
        uint[] input = Enumerable.Range(0, length).Select(i => (uint)(i + 1)).ToArray();
        var achievement = new LegacyNativeClientAchievement(input, uint.MaxValue);
        var first = achievement.MyFlags(); var second = achievement.MyFlags();
        Assert.Equal(14, first.Length); Assert.NotSame(first, second); Assert.NotSame(input, first);
        Assert.Equal(input, first[..length]); Assert.All(first[length..], x => Assert.Equal(0u, x));
        if (length != 0) input[0] = 0;
        first[13] = uint.MaxValue;
        Assert.Equal(second, achievement.MyFlags());
    }

    [Fact]
    public void ConstructorPreservesNativeNullAndOverlengthFaults()
    {
        Assert.Throws<NullReferenceException>(() => new LegacyNativeClientAchievement(null!, 1));
        Assert.Throws<IndexOutOfRangeException>(() => new LegacyNativeClientAchievement(new uint[15], 1));
    }

    [Theory]
    [InlineData(0u, 0)]
    [InlineData(1u, 14)]
    [InlineData(0x80000000u, 0)]
    [InlineData(0xFFFFFFFFu, 434)]
    public void ScoreRetainsNativeSignedBit31Comparison(uint word, int score)
    {
        var achievement = new LegacyNativeClientAchievement(Enumerable.Repeat(word, 14).ToArray(), 9);
        Assert.Equal(score, achievement.Score());
    }

    [Fact]
    public void SaveCopiesExactlyFourteenWordsKeepsKeyAndLeavesLongSuffix()
    {
        var words = Enumerable.Range(1, 14).Select(i => (uint)i * 11).ToArray();
        var achievement = new LegacyNativeClientAchievement(words, 99);
        var record = new LegacyNativeAchievementCollection { Key = -77, Value = Enumerable.Repeat(123u, 16).ToArray() };
        var alias = record.Value;
        achievement.Save(record);
        Assert.Same(alias, record.Value); Assert.Equal(-77, record.Key);
        Assert.Equal(words, record.Value[..14]); Assert.Equal(new uint[] { 123, 123 }, record.Value[14..]);
        words[0] = 0; record.Value[0] = 0;
        Assert.Equal(11u, achievement.MyFlags()[0]);
    }

    [Fact]
    public void SaveShortDestinationCommitsPrefixBeforeFaultAndNullDoesNotMutateOwner()
    {
        var words = Enumerable.Range(1, 14).Select(i => (uint)i).ToArray();
        var achievement = new LegacyNativeClientAchievement(words, 0);
        var record = new LegacyNativeAchievementCollection { Key = 73, Value = new uint[3] };
        Assert.Throws<IndexOutOfRangeException>(() => achievement.Save(record));
        Assert.Equal(new uint[] { 1, 2, 3 }, record.Value); Assert.Equal(73, record.Key);
        Assert.Throws<NullReferenceException>(() => achievement.Save(null!));
        record.Value = null!;
        Assert.Throws<NullReferenceException>(() => achievement.Save(record));
        Assert.Equal(words, achievement.MyFlags());
    }

    [Fact]
    public void RecordDefaultAndVariableLengthRoundTripsUseGenuineSlashReaderWriter()
    {
        var record = new LegacyNativeAchievementCollection();
        Assert.Equal(0, record.Key); Assert.Equal(new uint[14], record.Value);
        Assert.Equal("0/" + string.Concat(Enumerable.Repeat("0/", 14)), record.ToString());
        record.Key = -17; record.Value = [1, uint.MaxValue, 99];
        Assert.Equal("-17/1/4294967295/99/", record.ToString());
        var loaded = new LegacyNativeAchievementCollection { Value = new uint[3] };
        loaded.Load(record.ToString()); Assert.Equal(-17, loaded.Key); Assert.Equal(record.Value, loaded.Value);
        record.Value = []; Assert.Equal("-17/", record.ToString());
        loaded.Value = []; loaded.Load("77/123/"); Assert.Equal(77, loaded.Key); Assert.Empty(loaded.Value);
    }

    [Theory]
    [InlineData(null, 0, 0u, 0u)]
    [InlineData("", 0, 0u, 0u)]
    [InlineData("17", 0, 0u, 0u)]
    [InlineData("-17/1/4294967295/ignored/", -17, 1u, 4294967295u)]
    [InlineData("2147483648/-1/4294967296/", 0, 0u, 0u)]
    [InlineData("bad/7/bad/", 0, 7u, 0u)]
    [InlineData("73/", 73, 0u, 0u)]
    public void RecordLoadUsesDefaultPerMalformedMissingOrOverflowToken(string? data, int key, uint first, uint second)
    {
        var record = new LegacyNativeAchievementCollection { Key = -1, Value = [9, 9] };
        record.Load(data!); Assert.Equal(key, record.Key); Assert.Equal(new[] { first, second }, record.Value);
    }

    [Fact]
    public void RecordNullValueLoadAssignsKeyBeforeFault()
    {
        var record = new LegacyNativeAchievementCollection { Key = -1, Value = null! };
        Assert.Throws<NullReferenceException>(() => record.Load("73/1/"));
        Assert.Equal(73, record.Key); Assert.Null(record.Value);
        Assert.Throws<NullReferenceException>(() => record.ToString());
    }

    [Fact]
    public Task NativeSlotsStayNullUntilExplicitUnwiredPhaseThenRecreateFromSameRecord() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var client = scope.Client;
        Assert.Null(client.Achievement); Assert.Null(client.Player.Achievement);
        LegacyNativeAchievementMemberPhases.AssignAchievementAtNativeLoadClientSlot(client.Player);
        var record = client.Achievement!; var first = client.Player.Achievement!;
        Assert.Equal(new uint[14], first.MyFlags()); Assert.Equal(0, record.Key);
        record.Key = -9; record.Value[13] = 37;
        LegacyNativeAchievementMemberPhases.AssignAchievementAtNativeLoadClientSlot(client.Player);
        Assert.Same(record, client.Achievement); Assert.NotSame(first, client.Player.Achievement);
        Assert.Equal(-9, record.Key); Assert.Equal(0u, first.MyFlags()[13]);
        Assert.Equal(37u, client.Player.Achievement!.MyFlags()[13]);
        record.Value[13] = 0; Assert.Equal(37u, client.Player.Achievement.MyFlags()[13]); Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ExplicitPhaseConstructorFaultRetainsExistingOwnerAndRecord(bool nullValue) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var client = scope.Client;
        var original = Attach(client, 7); var record = new LegacyNativeAchievementCollection { Key = 77, Value = nullValue ? null! : new uint[15] };
        client.Achievement = record;
        if (nullValue) Assert.Throws<NullReferenceException>(() => LegacyNativeAchievementMemberPhases.AssignAchievementAtNativeLoadClientSlot(client.Player));
        else Assert.Throws<IndexOutOfRangeException>(() => LegacyNativeAchievementMemberPhases.AssignAchievementAtNativeLoadClientSlot(client.Player));
        Assert.Same(original, client.Player.Achievement); Assert.Same(record, client.Achievement); Assert.Equal(77, record.Key);
        Assert.Empty(scope.Queue);
    });
}
