namespace WolfConquer.Tests;

public sealed partial class LegacyNativeLevelDependencyTests
{
    [Theory]
    [InlineData(int.MinValue, true)]
    [InlineData(int.MaxValue, true)]
    [InlineData(-1, true)]
    [InlineData(int.MinValue, false)]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(42, false)]
    public Task BoundPointsRejectNegativeBalancesAndRetainTheValidNativeLoadingGate(int value,
        bool loaded) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        if (value < 0)
        {
            scope.Client.FullLoading = false;
            scope.Player.BoundConquerPoints = 41;
            scope.Client.ConquerPointsSave = 99;
            scope.Client.FullLoading = loaded;

            Assert.Throws<ArgumentOutOfRangeException>(() => scope.Player.BoundConquerPoints = value);

            Assert.Equal(41, scope.Player.BoundConquerPoints);
            Assert.Equal(99uL, scope.Client.ConquerPointsSave);
            Assert.Equal(loaded, scope.Client.FullLoading);
            Assert.Empty(scope.Queue);
            Assert.Equal(8, WolfConquer.Core.Networking.LegacyNativePacketRecycle.Count);
            Assert.All(scope.Borrowed, packet => { Assert.Equal(321, packet.Size); Assert.Equal(237, packet.Position); });
            return;
        }
        scope.Client.FullLoading = loaded;
        scope.Player.BoundConquerPoints = value;
        Assert.Equal(value, scope.Player.BoundConquerPoints);
        if (loaded) Assert.Equal(Update(45, checked((ulong)value)), Assert.Single(scope.Queue));
        else Assert.Empty(scope.Queue);
        Assert.Equal(loaded ? 7 : 8, WolfConquer.Core.Networking.LegacyNativePacketRecycle.Count);
    });

    [Theory]
    [InlineData(long.MinValue, true)]
    [InlineData(long.MaxValue, true)]
    [InlineData(-1L, true)]
    [InlineData(0L, true)]
    [InlineData(42L, false)]
    public Task DominoCoinsRetainFullSignedStateAndNativeConditionalUpdate(long value, bool loaded) =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Client.FullLoading = loaded;
        scope.Player.DominoCoins = value;
        Assert.Equal(value, scope.Player.DominoCoins);
        if (loaded) Assert.Equal(Update(91, unchecked((ulong)value)), Assert.Single(scope.Queue));
        else Assert.Empty(scope.Queue);
        Assert.Equal(loaded ? 7 : 8, WolfConquer.Core.Networking.LegacyNativePacketRecycle.Count);
    });

    [Theory]
    [InlineData(0uL)]
    [InlineData(9223372036854775808uL)]
    [InlineData(ulong.MaxValue)]
    public Task DominoCodeIsAnIndependentFullUInt64SlotWithoutPacketOrLoadingEffects(ulong value) =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Client.FullLoading = true;
        scope.Player.DominoCode = value;
        Assert.Equal(value, scope.Player.DominoCode);
        Assert.Equal(0L, scope.Player.DominoCoins);
        Assert.Empty(scope.Queue);
        Assert.Equal(8, WolfConquer.Core.Networking.LegacyNativePacketRecycle.Count);
    });

    [Theory]
    [InlineData(0u, false)]
    [InlineData(65536u, true)]
    [InlineData(uint.MaxValue, false)]
    public Task DonationPublishesTwoSeparateOneEntryNativeUpdatesWithoutALoadingGate(uint value,
        bool loaded) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Client.FullLoading = loaded;
        scope.Player.MyDontion = value;
        Assert.Equal(value, scope.Player.MyDontion);
        Assert.Equal(Update(125, value), scope.Queue.Dequeue());
        Assert.Equal(Update(126, value), scope.Queue.Dequeue());
        Assert.Empty(scope.Queue);
        Assert.Equal(0, scope.Borrowed[0].Position);
        Assert.Equal(7, WolfConquer.Core.Networking.LegacyNativePacketRecycle.Count);
    });

    [Fact]
    public Task BankAndWarehousePhysicalSlotsStartZeroAndDoNotImplicitlyMoveCurrency() =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Assert.Equal(0uL, scope.Client.ConquerPointsSave);
        Assert.Equal(0L, scope.Player.WHMoney);
        scope.Client.ConquerPointsSave = ulong.MaxValue;
        scope.Player.WHMoney = long.MinValue;
        Assert.Equal(ulong.MaxValue, scope.Client.ConquerPointsSave);
        Assert.Equal(long.MinValue, scope.Player.WHMoney);
        Assert.Equal(0L, scope.Player.Money);
        Assert.Equal(0, scope.Player.BoundConquerPoints);
        Assert.Empty(scope.Queue);
    });
}
