using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerLifecycleTests
{
    public static IEnumerable<object[]> DurationPriorityCases => Enumerable.Range(0, 14).Select(index => new object[] { index });

    [Theory]
    [MemberData(nameof(DurationPriorityCases))]
    public Task DurationSelectsFirstActualXpFlagAcrossAllFourteenPriorities(int first)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var expected = new List<(LegacyNativeStatusFlagsBigVector32.Flag Row, int Seconds)>();
        for (int index = first; index < DurationOrder.Length; index++)
        {
            scope.Flag(DurationOrder[index], 17 + index);
            expected.Add((scope.Player.BitVector.ArrayFlags[(int)DurationOrder[index]], 17 + index));
        }
        scope.Flag(Flag.Normal, 11);
        var normal = scope.Player.BitVector.ArrayFlags[(int)Flag.Normal];
        var selected = expected[0].Row; var timer = selected.Timer;
        var registry = scope.Player.BitVector.ArrayFlags; var words = scope.Player.BitVector.bits;
        Assert.True(scope.Player.TryIncreaseXPDuration(5));
        for (int index = 0; index < expected.Count; index++)
            Assert.Equal(expected[index].Seconds + (index == 0 ? 5 : 0), expected[index].Row.Secounds);
        Assert.Same(selected, registry[(int)DurationOrder[first]]); Assert.Equal(timer, selected.Timer);
        Assert.Equal(11, normal.Secounds); Assert.Same(registry, scope.Player.BitVector.ArrayFlags);
        Assert.Same(words, scope.Player.BitVector.bits); Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(17, 5, 22)] [InlineData(17, -20, -3)] [InlineData(-5, 3, -2)]
    [InlineData(int.MaxValue, 0, int.MaxValue)] [InlineData(int.MinValue, 0, int.MinValue)]
    public Task DurationKeepsRepresentableSignedShorteningAndExactPhysicalRow(int before, short delta, int expected)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Flag(Flag.Cyclone, before);
        var row = scope.Player.BitVector.ArrayFlags[(int)Flag.Cyclone];
        row.Timer = new DateTime(2000, 1, 1); var timer = row.Timer;
        Assert.True(scope.Player.TryIncreaseXPDuration(delta));
        Assert.Equal(expected, row.Secounds); Assert.Equal(timer, row.Timer);
        Assert.Same(row, scope.Player.BitVector.ArrayFlags[(int)Flag.Cyclone]); Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(int.MaxValue, 1)] [InlineData(int.MinValue, -1)]
    public Task DurationOverflowRefusesAndLegacyVoidCallCannotContinueAsSuccess(int before, short delta)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Flag(Flag.Cyclone, before);
        scope.Player.BitVector.ArrayFlags[(int)Flag.Cyclone].Timer = new DateTime(2000, 1, 1);
        var snapshot = DurationState.Capture(scope);
        Assert.False(scope.Player.TryIncreaseXPDuration(delta)); snapshot.Unchanged(scope);
        Assert.Throws<InvalidDataException>(() => scope.Player.IncreaseXPDuration(delta)); snapshot.Unchanged(scope);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task DurationUnrepresentableDateRefusesWithoutChangingTheOldValidDeadline(bool minimum)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Flag(Flag.Cyclone, minimum ? -1 : 1);
        var row = scope.Player.BitVector.ArrayFlags[(int)Flag.Cyclone];
        row.Timer = minimum ? DateTime.MinValue.AddSeconds(2) : DateTime.MaxValue.AddSeconds(-2);
        var snapshot = DurationState.Capture(scope);
        Assert.False(scope.Player.TryIncreaseXPDuration(minimum ? (short)-2 : (short)2)); snapshot.Unchanged(scope);
    });

    [Theory]
    [InlineData("vector")] [InlineData("words")] [InlineData("registry")] [InlineData("bitmap")]
    [InlineData("null-row")] [InlineData("key")] [InlineData("alias")] [InlineData("deadline")]
    [InlineData("invoke")] [InlineData("tail")]
    public Task DurationMalformedWholeGraphRefusesBeforeAnyStateOrOutputChange(string corruption)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Flag(Flag.Cyclone);
        CorruptDuration(scope.Player, corruption); var snapshot = DurationState.Capture(scope);
        Assert.False(scope.Player.TryIncreaseXPDuration(1)); snapshot.Unchanged(scope);
        Assert.Throws<InvalidDataException>(() => scope.Player.IncreaseXPDuration(1)); snapshot.Unchanged(scope);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task DurationAbsenceAndPhysicalNormalFallbackRemainValidWithoutOptionalOwners(bool normal)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Client.Rune = null; scope.Client.Equipment = null;
        scope.Client.HeroRewards = null; scope.Player.UID = 0;
        if (normal) scope.Flag(Flag.Normal, 8);
        if (normal)
        {
            Assert.True(scope.Player.TryIncreaseXPDuration(4));
            Assert.Equal(12, scope.Player.BitVector.ArrayFlags[(int)Flag.Normal].Secounds);
        }
        else
        {
            var snapshot = DurationState.Capture(scope);
            Assert.True(scope.Player.TryIncreaseXPDuration(4)); snapshot.Unchanged(scope);
        }
        Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task DurationRejectsDetachedOrForeignCurrentPlayerWithoutMutatingCapturedOwner(bool foreign)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var captured = scope.Player; scope.Flag(Flag.Cyclone);
        var replacement = foreign ? new LegacyNativeGameClientRuntime().Player : new LegacyNativePlayerRuntime(scope.Client);
        scope.Client.Player = replacement;
        var snapshot = DurationState.Capture(scope, captured);
        Assert.False(captured.TryIncreaseXPDuration(1)); snapshot.Unchanged(scope, captured);
        Assert.Same(replacement, scope.Client.Player);
    });
}
