using WolfConquer.Core.World;
using Poker = WolfConquer.Core.World.LegacyNativePokerFlags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerMapTests
{
    [Theory]
    [InlineData(0, "2h")]
    [InlineData(7, "9h")]
    [InlineData(8, "th")]
    [InlineData(9, "jh")]
    [InlineData(10, "qh")]
    [InlineData(11, "kh")]
    [InlineData(12, "ah")]
    [InlineData(13, "15h")]
    public void CompletePokerCardFormattingUsesNativeValuePlusTwoAndLetterCases(byte value, string expected)
    {
        Assert.Equal(expected, new LegacyNativePokerCard { Value = value }.ToString());
        Assert.Equal("", new LegacyNativePokerCard { Type = (Poker.CardsType)255 }.ToString());
    }

    [Fact]
    public void PokerCardComparisonsUseSuitOnlyForEqualValuesAndPreserveNullFaults()
    {
        var low = new LegacyNativePokerCard { Value = 3, Type = Poker.CardsType.Diamond };
        var high = new LegacyNativePokerCard { Value = 4, Type = Poker.CardsType.Heart };
        Assert.True(low < high); Assert.True(high > low);
        high.Value = 3; Assert.True(low > high); Assert.True(high < low);
        Assert.Throws<NullReferenceException>(() => { _ = low > null!; });
    }

    [Fact]
    public void TableWholeBirthPreservesNativeEmptyReloadCardsAndUninitializedOnScreen()
    {
        var table = new LegacyNativePokerTableRuntime(55);
        Assert.Equal(55u, table.Id); Assert.Equal((ushort)1858, table.MapId);
        Assert.Empty(table.Players); Assert.Empty(table.Watchers); Assert.Empty(table.ToSend);
        Assert.Null(table.Kick); Assert.Null(table.PreviousPlayer); Assert.Null(table.Cards); Assert.Null(table.OnScreen);
        Assert.Equal(5, table.Board.Length); Assert.All(table.Board, Assert.Null);
        Assert.Equal(Poker.TableState.Unopened, table.PreviousState); Assert.NotNull(table.TableSyncRoot); Assert.NotNull(table.dr);
        Assert.Throws<NullReferenceException>(() => table.CanInterface(1));
        table.OnScreen = new(); Assert.False(table.CanInterface(1)); table.OnScreen[1] = 99; Assert.True(table.CanInterface(1));
        var kick = new LegacyNativePokerKick(); Assert.Null(kick.Accept); Assert.Null(kick.Refuse); Assert.Equal(default, kick.Time);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 5)]
    public void PokerPlayerWholeCreateResetsRoundFieldsAndUsesActualTableType(byte rawType, int expectedPocket)
    {
        var type = (Poker.TableType)rawType;
        var table = new LegacyNativePokerTableRuntime(77) { TableType = type };
        var player = new LegacyNativePokerPlayer("Poker", 5)
        { Fold = true, IsPlaying = true, IsPotAllin = true, PotinThisRound = true, TotalPot = 5, RoundPot = 6, Lose = 7, TempMoney = 8 };
        Assert.Null(player.Pocket); Assert.Null(player.Table);
        player.Create(Poker.PlayerType.Watcher, 3, table, 100);
        Assert.Equal("Poker", player.Name); Assert.Equal(5u, player.Uid); Assert.Equal(Poker.PlayerType.Watcher, player.PlayerType);
        Assert.Equal((byte)3, player.Seat); Assert.Same(table, player.Table); Assert.Equal(expectedPocket, player.Pocket.Length);
        Assert.False(player.Fold); Assert.False(player.IsPlaying); Assert.False(player.IsPotAllin); Assert.False(player.PotinThisRound);
        Assert.Equal(0ul, player.TotalPot); Assert.Equal(0ul, player.RoundPot); Assert.Equal(0, player.Lose); Assert.Equal(0ul, player.TempMoney);
        Assert.Equal(100, player.CurrentMoney); Assert.Equal("", player.ToString());
        player.Create(Poker.PlayerType.Player, 1, table, ulong.MaxValue); Assert.Equal(0, player.CurrentMoney);
    }

    [Fact]
    public void MoneyMethodsKeepNativeSignedClampingUnsignedPotWrapAndMutationBeforeNullTableFault()
    {
        var player = new LegacyNativePokerPlayer("Poker", 1) { CurrentMoney = 20 };
        player.Increment(5); Assert.Equal(25, player.CurrentMoney);
        Assert.Throws<NullReferenceException>(() => player.Decrement(30));
        Assert.Equal(0, player.CurrentMoney); Assert.Equal(30ul, player.RoundPot); Assert.Equal(30ul, player.TotalPot);
        player.Table = new LegacyNativePokerTableRuntime(1) { RoundPot = ulong.MaxValue, TotalPot = ulong.MaxValue };
        player.Decrement(2); Assert.Equal(1ul, player.Table.RoundPot); Assert.Equal(1ul, player.Table.TotalPot);
        player.Pocket = [new() { Value = 12 }, null!, new() { Value = 11, Type = Poker.CardsType.Diamond }];
        Assert.Equal("ah kd", player.ToString());
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    [InlineData(255, true)]
    public void TableQueriesUseNativeStateAndActualPlayers(byte rawState, bool expected)
    {
        var state = (Poker.TableState)rawState;
        var table = new LegacyNativePokerTableRuntime(1) { State = state };
        Assert.Equal(expected, table.InGame()); Assert.True(table.IsSeatAvailable(1));
        var player = new LegacyNativePokerPlayer("Player", 1) { Seat = 1, CurrentMoney = 50, IsPlaying = true };
        table.Players[999] = player; Assert.False(table.IsSeatAvailable(1)); Assert.Equal(50, table.LowestMoney);
        player.Fold = true; Assert.Equal(0, table.LowestMoney);
        table.Showhand = true; table.ShowhandTotalPot = -5; Assert.Equal(-5, table.LowestMoney);
    }
}
