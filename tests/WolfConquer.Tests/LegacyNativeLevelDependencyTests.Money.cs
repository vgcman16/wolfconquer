using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeLevelDependencyTests
{
    [Theory]
    [InlineData(0L)]
    [InlineData(499999999999L)]
    [InlineData(500000000000L)]
    public Task MoneyRetainsNonnegativeValuesAtOrBelowCapAndPublishesTheNativeUpdate(long value) =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Player.WHMoney = 77;
        scope.Player.Money = value;
        Assert.Equal(value, scope.Player.Money);
        Assert.Equal(77L, scope.Player.WHMoney);
        Assert.Equal(Update(4, unchecked((ulong)value)), Assert.Single(scope.Queue));
        Assert.Equal(0, scope.Borrowed[0].Position);
        Assert.Equal(7, WolfConquer.Core.Networking.LegacyNativePacketRecycle.Count);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task MoneyDoesNotInventAFullLoadingGate(bool loaded) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Client.FullLoading = loaded;
        scope.Player.Money = 99;
        Assert.Equal(Update(4, 99), Assert.Single(scope.Queue));
        Assert.Equal(loaded, scope.Client.FullLoading);
    });

    [Theory]
    [InlineData(500000000001L, 0L, 1L, false)]
    [InlineData(500000000100L, 7L, 107L, true)]
    public Task OverflowGoldUsesActualWarehousePokerAndMessagePathsBeforeMoneyUpdate(long value,
        long beforeWarehouse, long expectedWarehouse, bool poker) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Player.WHMoney = beforeWarehouse;
        if (poker) scope.Client.PokerPlayer = new LegacyNativePokerPlayer("Fixture", 17) { CurrentMoney = 42 };
        scope.Player.Money = value;
        Assert.Equal(500000000000L, scope.Player.Money);
        Assert.Equal(expectedWarehouse, scope.Player.WHMoney);
        if (poker) Assert.Equal(500000000000L, scope.Client.PokerPlayer!.CurrentMoney);
        string[] message = OverflowGoldMessage(scope.Queue.Dequeue());
        Assert.Equal(new[] { "SYSTEM", "ALL", "", (value - 500000000000L) +
            " Gold has transfare to your warehouse you can't hold more than 500,000,000,000 into your inventory.",
            "", "", "" }, message);
        Assert.Equal(Update(4, 500000000000uL), scope.Queue.Dequeue());
        Assert.Empty(scope.Queue);
        Assert.Equal(0, scope.Borrowed[0].Position);
        Assert.Equal(0, scope.Borrowed[1].Position);
        Assert.Equal(6, WolfConquer.Core.Networking.LegacyNativePacketRecycle.Count);
    });

    [Theory]
    [InlineData(long.MinValue)]
    [InlineData(-1L)]
    public Task NegativeMoneyRefusesBeforeAnySlotPokerPacketOrPoolChange(long value) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Player.WHMoney = 77;
        scope.Client.PokerPlayer = new LegacyNativePokerPlayer("Fixture", 17) { CurrentMoney = 42 };
        Assert.Throws<ArgumentOutOfRangeException>(() => scope.Player.Money = value);
        Assert.Equal(0L, scope.Player.Money); Assert.Equal(77L, scope.Player.WHMoney);
        Assert.Equal(42L, scope.Client.PokerPlayer.CurrentMoney);
        Assert.Empty(scope.Queue); Assert.Equal(8, WolfConquer.Core.Networking.LegacyNativePacketRecycle.Count);
        Assert.All(scope.Borrowed, packet => { Assert.Equal(321, packet.Size); Assert.Equal(237, packet.Position); });
    });

    [Fact]
    public Task OverflowingWarehouseSpillRefusesBeforeAnySlotPokerPacketOrPoolChange() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Player.WHMoney = long.MaxValue;
        scope.Client.PokerPlayer = new LegacyNativePokerPlayer("Fixture", 17) { CurrentMoney = 42 };
        Assert.Throws<OverflowException>(() => scope.Player.Money = long.MaxValue);
        Assert.Equal(0L, scope.Player.Money); Assert.Equal(long.MaxValue, scope.Player.WHMoney);
        Assert.Equal(42L, scope.Client.PokerPlayer.CurrentMoney);
        Assert.Empty(scope.Queue); Assert.Equal(8, WolfConquer.Core.Networking.LegacyNativePacketRecycle.Count);
        Assert.All(scope.Borrowed, packet => { Assert.Equal(321, packet.Size); Assert.Equal(237, packet.Position); });
    });

    [Fact]
    public Task OrdinaryUnboundMoneyDoesNotRequireAnUnusedWarehouseBalance() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Player.WHMoney = long.MinValue;
        scope.Player.Money = 99;
        Assert.Equal(99L, scope.Player.Money); Assert.Equal(long.MinValue, scope.Player.WHMoney);
        Assert.Equal(Update(4, 99), Assert.Single(scope.Queue));
    });

    [Fact]
    public Task SpillRequiresAValidActualWarehouseBalanceBeforeAnyChange() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Player.WHMoney = -1;
        Assert.Throws<InvalidOperationException>(() => scope.Player.Money = 500000000001L);
        Assert.Equal(0L, scope.Player.Money); Assert.Equal(-1L, scope.Player.WHMoney);
        Assert.Empty(scope.Queue); Assert.Equal(8, WolfConquer.Core.Networking.LegacyNativePacketRecycle.Count);
    });

    [Fact]
    public Task OrdinaryMoneyWritesDoNotSynchronizePokerOrMutateTheWarehouse() =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Client.PokerPlayer = new LegacyNativePokerPlayer("Fixture", 17) { CurrentMoney = 42 };
        scope.Player.WHMoney = 5;
        scope.Player.Money = 500000000000L;
        Assert.Equal(42L, scope.Client.PokerPlayer.CurrentMoney);
        Assert.Equal(5L, scope.Player.WHMoney);
        Assert.Equal(Update(4, 500000000000uL), Assert.Single(scope.Queue));
    });

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public Task SuppressedTransportDoesNotSuppressOverflowGoldStateOrRealPacketConstruction(bool fake,
        bool alive) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Client.Fake = fake;
        scope.Client.Socket!.Alive = alive;
        scope.Client.PokerPlayer = new LegacyNativePokerPlayer("Fixture", 17) { CurrentMoney = 42 };
        scope.Player.Money = 500000000001L;
        Assert.Equal(500000000000L, scope.Player.Money);
        Assert.Equal(1L, scope.Player.WHMoney);
        Assert.Equal(500000000000L, scope.Client.PokerPlayer.CurrentMoney);
        Assert.Empty(scope.Queue);
        Assert.Equal(Update(4, 500000000000uL), NativeSpellTestFixture.Bytes(scope.Borrowed[1]));
        Assert.Equal(0, scope.Borrowed[0].Position);
        Assert.Equal(0, scope.Borrowed[1].Position);
    });
}
