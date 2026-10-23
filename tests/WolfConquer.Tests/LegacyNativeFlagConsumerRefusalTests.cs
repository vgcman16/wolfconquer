using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;
using Vector = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMonsterAttackTests
{
    [Theory]
    [InlineData("words")] [InlineData("dictionary")] [InlineData("divergent")]
    public Task MonsterWrappersHonorActualVectorRefusalWithoutPublishingOrReportingSuccess(string malformed)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var role = scope.Monster();
        var saved = FlagConsumerCorruption.Prepare(role.BitVector, malformed);
        Assert.False(role.AddFlag(Flag.Cyclone, 30, true));
        Assert.False(role.RemoveFlag(Flag.TeamLeader, null));
        role.AddSpellFlag(Flag.TeamLeader, 99, false); role.ClearFlags(true);
        saved.Unchanged(role.BitVector); Assert.Empty(scope.Queue);
    });
}

public sealed partial class LegacyNativeMapPopulationTests
{
    [Theory]
    [InlineData("words")] [InlineData("dictionary")] [InlineData("divergent")]
    public Task SobWrappersHonorActualVectorRefusalWithoutPublishingOrReportingSuccess(string malformed)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Map();
        var role = new LegacyNativeSobNpcRuntime { Map = 700, UID = 77 };
        var saved = FlagConsumerCorruption.Prepare(role.BitVector, malformed);
        Assert.False(role.AddFlag(Flag.Cyclone, 30, true)); Assert.False(role.RemoveFlag(Flag.TeamLeader));
        saved.Unchanged(role.BitVector); Assert.Empty(scope.Queue);
    });
}

public sealed partial class LegacyNativeMapSpawnTests
{
    [Theory]
    [InlineData("words")] [InlineData("dictionary")] [InlineData("divergent")]
    public Task StaticWrappersHonorActualVectorRefusalWithoutPublishingOrReportingSuccess(string malformed)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var map = scope.Map();
        var role = new LegacyNativeStaticRoleRuntime(12, 13) { Map = 700 };
        var saved = FlagConsumerCorruption.Prepare(role.BitVector, malformed);
        Assert.False(role.AddFlag(Flag.Cyclone, 30, true)); Assert.False(role.RemoveFlag(Flag.TeamLeader, map));
        saved.Unchanged(role.BitVector); Assert.Empty(scope.Queue);
    });
}

internal sealed record FlagConsumerCorruption(object? Dictionary, KeyValuePair<int, Vector.Flag>[]? Rows,
    uint[]? Words, uint[]? Bytes, Vector.Flag Row, DateTime Timer, DateTime InvokeTimer)
{
    internal static FlagConsumerCorruption Prepare(Vector vector, string malformed)
    {
        Assert.True(vector.TryAdd((int)Flag.TeamLeader, 19, true, 3));
        var row = vector.ArrayFlags[(int)Flag.TeamLeader];
        if (malformed == "words") vector.bits = null!;
        else if (malformed == "dictionary") vector.ArrayFlags = null!;
        else vector.Add(7);
        return new(vector.ArrayFlags, vector.ArrayFlags?.ToArray(), vector.bits, vector.bits?.ToArray(),
            row, row.Timer, row.TimerInvoker);
    }

    internal void Unchanged(Vector vector)
    {
        Assert.Same(Dictionary, vector.ArrayFlags); Assert.Equal(Rows, vector.ArrayFlags?.ToArray());
        Assert.Same(Words, vector.bits); Assert.Equal(Bytes, vector.bits);
        Assert.Equal(6, Row.Key); Assert.Equal(19, Row.Secounds); Assert.Equal(3, Row.InvokerSecouds);
        Assert.True(Row.RemoveOnDead); Assert.Equal(Timer, Row.Timer); Assert.Equal(InvokeTimer, Row.TimerInvoker);
    }
}
