namespace WolfConquer.Tests;

public sealed partial class LegacyNativeLevelDependencyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Hi")]
    [InlineData("狼∞")]
    public Task BoxDialogUsesTheRealDialogOwnerAndSendsOnlyItsNativeMessageBoxFrame(string? text) =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Client.CreateBoxDialog(text!);
        Assert.Equal(Box(text), Assert.Single(scope.Queue));
        Assert.Equal(0, scope.Borrowed[0].Position);
        Assert.Equal(7, WolfConquer.Core.Networking.LegacyNativePacketRecycle.Count);
    });

    [Theory]
    [InlineData(0u, 0u)]
    [InlineData(1u, 1u)]
    [InlineData(5u, 5u)]
    [InlineData(6u, 5u)]
    [InlineData(99u, 5u)]
    [InlineData(100u, 100u)]
    [InlineData(105u, 105u)]
    [InlineData(109u, 105u)]
    [InlineData(16000u, 16000u)]
    [InlineData(16004u, 16004u)]
    [InlineData(16008u, 16005u)]
    [InlineData(uint.MaxValue, 4294967205u)]
    public Task LoweredClassUsesFullUInt32RemainderAndDoesNotPublishClass(uint source, uint expected) =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.PreexistingClass(source);
        Assert.Equal(expected, scope.Player.LoweredClass);
        Assert.Equal(source, scope.Player.Class);
        Assert.Empty(scope.Queue);
        Assert.Equal(8, WolfConquer.Core.Networking.LegacyNativePacketRecycle.Count);
    });
}
