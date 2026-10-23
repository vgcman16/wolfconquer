using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHealthUpdateTests
{
    [Theory]
    [InlineData(100u, 50, "0C0080080811120408011064", "10008008081112040801106412021032")]
    [InlineData(0u, 0, "0A008008081112020801", "0C0080080811120208011200")]
    public Task HealthUpdatePublishesExactNativeCumulativeFrames(uint maximum, int current,
        string first, string second) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.SetPreexistingHealth(maximum, current);
        scope.Player.SendUpdateHP();
        Assert.Equal(Convert.FromHexString(first), scope.Queue.Dequeue());
        Assert.Equal(Convert.FromHexString(second), scope.Queue.Dequeue());
        Assert.Empty(scope.Queue);
        Assert.Equal(0, scope.Borrowed[0].Position);
        Assert.Equal(Convert.FromHexString(second), NativeSpellTestFixture.Bytes(scope.Borrowed[0]));
        Assert.Equal(current, scope.Player.HitPoints);
        Assert.Equal(maximum, scope.Client.Status.MaxHitpoints);
    });

    [Theory]
    [InlineData(0u, -1)]
    [InlineData(65536u, 65537)]
    [InlineData(uint.MaxValue, int.MaxValue)]
    [InlineData(1u, int.MinValue)]
    [InlineData(128u, 127)]
    public Task HealthUpdateRetainsFullWidthValuesAndNegativeSignedHpWithoutClamp(uint maximum,
        int current) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.SetPreexistingHealth(maximum, current);
        scope.Player.UID = uint.MaxValue;
        scope.Player.SendUpdateHP();
        Assert.Equal(Frame(uint.MaxValue, maximum, null), scope.Queue.Dequeue());
        Assert.Equal(Frame(uint.MaxValue, maximum, current), scope.Queue.Dequeue());
        Assert.Empty(scope.Queue);
        Assert.Equal(current, scope.Player.HitPoints);
        Assert.Equal(current > 0, scope.Player.Alive);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DirectHealthUpdateDoesNotApplyTheHeldSetterFullLoadingGate(bool loaded) =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Client.FullLoading = loaded;
        scope.Player.UID = 0;
        scope.SetPreexistingHealth(300, 200);
        scope.Player.SendUpdateHP();
        Assert.Equal(Frame(0, 300, null), scope.Queue.Dequeue());
        Assert.Equal(Frame(0, 300, 200), scope.Queue.Dequeue());
        Assert.Equal(loaded, scope.Client.FullLoading);
    });

    [Fact]
    public Task EachInvocationCreatesANewUpdateOwnerAndReadsCurrentPhysicalValues() =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(2);
        scope.SetPreexistingHealth(100, 50);
        scope.Player.SendUpdateHP();
        byte[] preservedFirst = scope.Queue.Dequeue();
        byte[] preservedSecond = scope.Queue.Dequeue();
        scope.SetPreexistingHealth(5, -9);
        scope.Player.SendUpdateHP();
        Assert.Equal(Frame(17, 100, null), preservedFirst);
        Assert.Equal(Frame(17, 100, 50), preservedSecond);
        Assert.Equal(Frame(17, 5, null), scope.Queue.Dequeue());
        Assert.Equal(Frame(17, 5, -9), scope.Queue.Dequeue());
        Assert.Empty(scope.Queue);
        Assert.All(scope.Borrowed, packet => Assert.Equal(0, packet.Position));
        // Native Reuse only seeks zero; it does not enqueue the borrowed packets.
        Assert.Equal(0, LegacyNativePacketRecycle.Count);
    });

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public Task SuppressedTransportStillBuildsBothFramesAndDisposesTheBorrowedStream(bool fake,
        bool alive) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Client.Fake = fake;
        scope.Client.Socket!.Alive = alive;
        scope.SetPreexistingHealth(12, 8);
        scope.Player.SendUpdateHP();
        Assert.Empty(scope.Queue);
        Assert.Equal(Frame(17, 12, 8), NativeSpellTestFixture.Bytes(scope.Borrowed[0]));
        Assert.Equal(0, scope.Borrowed[0].Position);
    });

    [Fact]
    public Task MissingStatusFaultsBeforeAnySerializationButAfterBorrowAndStillReturnsCursorToZero() =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Client.Status = null!; // Deliberate malformed source-owner fault fixture.
        Assert.Throws<NullReferenceException>(() => scope.Player.SendUpdateHP());
        Assert.Empty(scope.Queue);
        LegacyNativePacket borrowed = scope.Borrowed[0];
        Assert.Equal(0, borrowed.Position);
        Assert.Equal(321, borrowed.Size);
        Assert.All(NativeSpellTestFixture.Bytes(borrowed), value => Assert.Equal((byte)0xA5, value));
        Assert.Equal(0, LegacyNativePacketRecycle.Count);
    });

    [Fact]
    public Task TeamPkSlotHasItsOwnNativeDefaultAndDoesNotAlterHealthOrLoading() =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Assert.False(scope.Player.InTeamPk);
        scope.Player.InTeamPk = true;
        Assert.True(scope.Player.InTeamPk);
        Assert.False(scope.Client.FullLoading);
        Assert.Equal(0, scope.Player.HitPoints);
        Assert.Empty(scope.Queue);
        Assert.Equal(1, LegacyNativePacketRecycle.Count);
    });
}
