using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAnimaSkinTests
{
    [Fact]
    public void ExplicitConstructorSlotStoresActualClientAndReplacesOwnerWithoutMutatingEscapedStorage()
    {
        using var records = new Records();
        var client = new LegacyNativeGameClientRuntime();
        Assert.Null(client.MyAnimaSkin);
        LegacyNativeAnimaSkinMemberPhases.AssignAnimaSkinAtNativeConstructorSlot(client);
        var first = Assert.IsType<LegacyNativeAnimaSkinRuntime>(client.MyAnimaSkin);
        Assert.Same(client, first.user);
        Assert.False(first.Load);
        first.AddSkin(17, true);
        first.Load = true;
        var escaped = first.Items[17];
        LegacyNativeAnimaSkinMemberPhases.AssignAnimaSkinAtNativeConstructorSlot(client);
        var second = Assert.IsType<LegacyNativeAnimaSkinRuntime>(client.MyAnimaSkin);
        Assert.NotSame(first, second);
        Assert.NotSame(first.Items, second.Items);
        Assert.Same(client, second.user);
        Assert.False(second.Load);
        Assert.Empty(second.Items);
        Assert.Same(escaped, Assert.Single(first.Items).Value);
        RowIs(escaped, 17, true);
        Assert.True(first.Load);
        Assert.Throws<NullReferenceException>(() =>
            LegacyNativeAnimaSkinMemberPhases.AssignAnimaSkinAtNativeConstructorSlot(null!));
        Assert.Same(second, client.MyAnimaSkin);
    }
}
