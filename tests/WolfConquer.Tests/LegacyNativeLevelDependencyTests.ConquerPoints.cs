namespace WolfConquer.Tests;

public sealed partial class LegacyNativeLevelDependencyTests
{
    [Theory]
    [InlineData(0u, false)]
    [InlineData(0u, true)]
    [InlineData(42u, false)]
    [InlineData(42u, true)]
    [InlineData(1999999999u, false)]
    [InlineData(1999999999u, true)]
    [InlineData(2000000000u, false)]
    [InlineData(2000000000u, true)]
    public Task UnboundConquerPointsRetainValidValuesAndTheExactNativeLoadingGate(uint value,
        bool loaded) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        // The unused real bank can hold its full native domain. An ordinary
        // assignment must not run spill arithmetic against that unrelated slot.
        scope.Client.FullLoading = false;
        scope.Client.ConquerPointsSave = ulong.MaxValue;
        scope.Player.BoundConquerPoints = 41;
        scope.Player.WHMoney = 77;
        scope.Client.FullLoading = loaded;

        scope.Player.ConquerPoints = value;

        Assert.Equal(value, scope.Player.ConquerPoints);
        Assert.Equal(ulong.MaxValue, scope.Client.ConquerPointsSave);
        Assert.Equal(41, scope.Player.BoundConquerPoints);
        Assert.Equal(0L, scope.Player.Money);
        Assert.Equal(77L, scope.Player.WHMoney);
        Assert.Equal(loaded, scope.Client.FullLoading);
        if (loaded)
        {
            Assert.Equal(Update(29, value), Assert.Single(scope.Queue));
            Assert.Equal(0, scope.Borrowed[0].Position);
        }
        else
        {
            Assert.Empty(scope.Queue);
            Assert.All(scope.Borrowed, packet => { Assert.Equal(321, packet.Size); Assert.Equal(237, packet.Position); });
        }
        Assert.Equal(loaded ? 7 : 8, WolfConquer.Core.Networking.LegacyNativePacketRecycle.Count);
    });

    [Theory]
    [InlineData(2000000001u, 0uL, false)]
    [InlineData(2000000001u, 0uL, true)]
    [InlineData(2000000001u, ulong.MaxValue, false)]
    [InlineData(2000000001u, ulong.MaxValue, true)]
    [InlineData(uint.MaxValue, 0uL, false)]
    [InlineData(uint.MaxValue, 0uL, true)]
    [InlineData(uint.MaxValue, ulong.MaxValue, false)]
    [InlineData(uint.MaxValue, ulong.MaxValue, true)]
    public Task UnboundConquerPointSpillRefusesItsMissingRealOwnerBeforeAllEffects(uint value,
        ulong bank, bool loaded) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Client.FullLoading = false;
        scope.Player.ConquerPoints = 123;
        scope.Player.BoundConquerPoints = 41;
        scope.Client.ConquerPointsSave = bank;
        scope.Player.WHMoney = 77;
        scope.Client.FullLoading = loaded;

        var error = Assert.Throws<InvalidOperationException>(() => scope.Player.ConquerPoints = value);

        Assert.Equal("nativeConquerPointsSpillOwnerUnavailable", error.Message);
        Assert.Equal(123u, scope.Player.ConquerPoints);
        Assert.Equal(41, scope.Player.BoundConquerPoints);
        Assert.Equal(bank, scope.Client.ConquerPointsSave);
        Assert.Equal(0L, scope.Player.Money);
        Assert.Equal(77L, scope.Player.WHMoney);
        Assert.Equal(loaded, scope.Client.FullLoading);
        Assert.Empty(scope.Queue); // No CP update or spill dialog was published.
        Assert.Equal(8, WolfConquer.Core.Networking.LegacyNativePacketRecycle.Count);
        Assert.All(scope.Borrowed, packet => { Assert.Equal(321, packet.Size); Assert.Equal(237, packet.Position); });
    });

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public Task SuppressedNativeTransportDoesNotSuppressTheValidCpSlotOrPacketConstruction(bool fake,
        bool alive) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Client.FullLoading = true;
        scope.Client.Fake = fake;
        scope.Client.Socket!.Alive = alive;

        scope.Player.ConquerPoints = 99;

        Assert.Equal(99u, scope.Player.ConquerPoints);
        Assert.Equal(0uL, scope.Client.ConquerPointsSave);
        Assert.Empty(scope.Queue);
        Assert.Equal(Update(29, 99), NativeSpellTestFixture.Bytes(scope.Borrowed[0]));
        Assert.Equal(0, scope.Borrowed[0].Position);
        Assert.Equal(7, WolfConquer.Core.Networking.LegacyNativePacketRecycle.Count);
    });
}
