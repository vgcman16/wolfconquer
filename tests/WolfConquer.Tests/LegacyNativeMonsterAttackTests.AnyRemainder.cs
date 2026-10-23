using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMonsterAttackTests
{
    [Fact]
    public Task AnyRemainderTargetPreservesRealIndexPriorityAndStablePhysicalTieOrder() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var monster = scope.Monster();
        var firstQueue = new Queue<byte[]>(); var secondQueue = new Queue<byte[]>();
        var first = scope.Peer(91, 27, 25, firstQueue);
        var second = scope.Peer(92, 28, 25, secondQueue);
        ((ILegacyNativeMapObj)scope.Player).IndexInScreen = 1;
        ((ILegacyNativeMapObj)first.Player).IndexInScreen = 50;
        ((ILegacyNativeMapObj)second.Player).IndexInScreen = 50;
        Assert.Same(first.Player, monster.View.GetTarget(scope.Map, LegacyNativeMapObjectType.Player));
        ((ILegacyNativeMapObj)second.Player).IndexInScreen = 51;
        Assert.Same(second.Player, monster.View.GetTarget(scope.Map, LegacyNativeMapObjectType.Player));
        second.Player.DynamicID = 9;
        Assert.Same(first.Player, monster.View.GetTarget(scope.Map, LegacyNativeMapObjectType.Player));
        first.Player.DynamicID = 9;
        Assert.Same(scope.Player, monster.View.GetTarget(scope.Map, LegacyNativeMapObjectType.Player));
        scope.Map.View!.ClearMap(LegacyNativeMapObjectType.Player);
        Assert.Null(monster.View.GetTarget(scope.Map, LegacyNativeMapObjectType.Player));
        Assert.Empty(scope.Queue); Assert.Empty(firstQueue); Assert.Empty(secondQueue);
    });

    [Fact]
    public Task AnyRemainderTargetMissingMapOrViewDoesNotCreateAReplacement() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var monster = scope.Monster();
        Assert.Null(monster.View.GetTarget(null!, LegacyNativeMapObjectType.Player));
        var original = scope.Map.View;
        scope.Map.View = null;
        Assert.Null(monster.View.GetTarget(scope.Map, LegacyNativeMapObjectType.Player));
        Assert.Null(scope.Map.View); Assert.NotNull(original); Assert.Empty(scope.Queue);
    });
}
