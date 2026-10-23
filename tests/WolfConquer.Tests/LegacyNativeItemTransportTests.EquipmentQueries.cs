using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeItemTransportTests
{
    [Fact]
    public Task EmptyEquipmentQueriesKeepNativeNullPredicateAndNonpositiveCountSemantics() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Assert.False(scope.Equipment.Exist(null!)); scope.Equipment.Have(null!, out int count); Assert.Equal(0, count);
        Assert.True(scope.Equipment.Exist(null!, 0)); Assert.True(scope.Equipment.Exist(null!, -1));
        Assert.False(scope.Equipment.Exist(null!, 1)); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task SingleExistShortCircuitsWhileCountedExistAndHaveTraverseEveryActualAliasAndNullRow() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var a = new LegacyNativeGameItemRuntime(); var b = new LegacyNativeGameItemRuntime();
        scope.Equipment.CurentEquip = new[] { a, null!, b, a };
        var calls = new List<LegacyNativeGameItemRuntime?>();
        Assert.True(scope.Equipment.Exist(item => { calls.Add(item); return ReferenceEquals(item, a); }));
        Assert.Equal(new[] { a }, calls);
        calls.Clear();
        Assert.True(scope.Equipment.Exist(item => { calls.Add(item); return item != null; }, 1));
        Assert.Equal(new LegacyNativeGameItemRuntime?[] { a, null, b, a }, calls);
        calls.Clear(); scope.Equipment.Have(item => { calls.Add(item); return ReferenceEquals(item, a); }, out int count);
        Assert.Equal(2, count); Assert.Equal(new LegacyNativeGameItemRuntime?[] { a, null, b, a }, calls);
    });

    [Fact]
    public Task QueriesCaptureTheOriginalArrayButReadCurrentElementsDuringSourceCallbackMutation() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var a = new LegacyNativeGameItemRuntime(); var b = new LegacyNativeGameItemRuntime();
        var replacement = new LegacyNativeGameItemRuntime(); var original = new[] { a, b };
        scope.Equipment.CurentEquip = original; var seen = new List<LegacyNativeGameItemRuntime>();
        scope.Equipment.Have(item =>
        {
            seen.Add(item);
            if (ReferenceEquals(item, a)) { scope.Equipment.CurentEquip = new[] { replacement }; original[1] = replacement; }
            return true;
        }, out int count);
        Assert.Equal(2, count); Assert.Equal(new[] { a, replacement }, seen);
        Assert.Equal(new[] { replacement }, scope.Equipment.CurentEquip);
    });

    [Fact]
    public Task HaveKeepsItsRealOutCountPrefixWhenTheNativePredicateThrows() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var a = new LegacyNativeGameItemRuntime(); var b = new LegacyNativeGameItemRuntime();
        scope.Equipment.CurentEquip = new[] { a, a, b }; int count = 999;
        var fault = new InvalidOperationException("owned predicate fault");
        var thrown = Assert.Throws<InvalidOperationException>(() => scope.Equipment.Have(item =>
        {
            if (ReferenceEquals(item, b)) throw fault; return true;
        }, out count));
        Assert.Same(fault, thrown); Assert.Equal(2, count);
    });

    [Fact]
    public Task NullQueriesAreNotGuardedWhenTheActualEquipmentArrayHasElementsOrIsNull() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Equipment.CurentEquip = new[] { new LegacyNativeGameItemRuntime() };
        Assert.Throws<NullReferenceException>(() => scope.Equipment.Exist(null!));
        Assert.Throws<NullReferenceException>(() => scope.Equipment.Exist(null!, 0));
        Assert.Throws<NullReferenceException>(() => scope.Equipment.Have(null!, out _));
        scope.Equipment.CurentEquip = null!;
        Assert.Throws<NullReferenceException>(() => scope.Equipment.Exist(_ => true));
        Assert.Throws<NullReferenceException>(() => scope.Equipment.Have(_ => true, out _));
        Assert.Null(scope.Equipment.CurentEquip);
    });

    [Fact]
    public Task EquipmentContainsUsesTheCurrentDictionaryKeyRatherThanTheRetainedItemUid() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = new LegacyNativeGameItemRuntime { Uid = 999 };
        scope.Equipment.ClientItems.TryAdd(7, item);
        Assert.True(scope.Equipment.Contains(7)); Assert.False(scope.Equipment.Contains(999));
        item.Uid = 123; Assert.True(scope.Equipment.Contains(7)); Assert.False(scope.Equipment.Contains(123));
        scope.Equipment.ClientItems = new(); Assert.False(scope.Equipment.Contains(7));
    });
}
