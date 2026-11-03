using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task RealSqlNativeTradeTransportEofRefundsBothEscrowsWithoutMovingAnItemOrReissuing() => Run(async f =>
    {
        var before = SqlTradeBefore.Capture(f); var sql = await f.Database.SnapshotAsync();
        var party = await PrepareSqlTradeAsync(f); AssertSqlTradeEscrow(f, before, party);
        f.Sent.Clear(); f.OtherSent.Clear();
        await f.DisconnectOtherTradeActorAsync();
        Assert.Single(f.Store.Snapshot()); Assert.False(f.Current.LegacyTradeActive);
        AssertSqlTradeActor(f, true);
        Assert.Equal(before.First.Character.Money, f.Current.Character.Money);
        Assert.Equal(before.First.Character.ConquerPoints, f.Current.Character.ConquerPoints);
        var second = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.OtherUserPath), out _));
        Assert.Equal(before.Second.Character.Money, second.Money); Assert.Equal(before.Second.Character.ConquerPoints, second.ConquerPoints);
        Assert.True(LegacyTradeEscrowClaims.TryRead(second.Attributes, out var claim, out var reason), reason); Assert.Null(claim);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(before.First.PlayerItems), File.ReadAllBytes(f.ItemsPath));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(before.Second.PlayerItems), File.ReadAllBytes(f.OtherItemsPath));
        lock (f.Trades.SyncRoot) Assert.False(f.Trades.Contains(f.Peer!.Connection));
        Assert.DoesNotContain(f.Sent, bytes => LegacyTradePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Action == 4);
        Assert.Equal(before.Registry, File.ReadAllBytes(f.Data.State));
        Assert.False(f.Runtime!.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        await f.CloseWorldAsync(); await f.OpenWorldAsync();
        AssertSqlTradeState(f, before, exchanged: false); await AssertSqlTradeReplayAsync(f, before, exchanged: false);
        Assert.Equal(sql, await f.Database.SnapshotAsync());
    }, tradeItemState: "valid");

    [ModernSqlAuthFixtureFact]
    public Task RealSqlNativeTradeInterruptedFourFilePairConvergesBeforeBothColdSqlAdmissions() => Run(async f =>
    {
        var before = SqlTradeBefore.Capture(f); var sql = await f.Database.SnapshotAsync();
        await PrepareSqlTradeAsync(f); await f.TradeAsync(true, new(9));
        f.Sent.Clear(); f.OtherSent.Clear();
        using (var interruption = LegacyJournalInterruption.StopAfter(3))
        {
            f.Handler.TradeJournalInterruption = interruption;
            try { await f.TradeAsync(false, new(9)); }
            finally { f.Handler.TradeJournalInterruption = null; }
            Assert.True(interruption.PendingObserved, f.Diagnostics);
            Assert.Equal(new[] { 0, 1, 2, 3 }, interruption.Visited);
        }
        Assert.True(f.Runtime!.Recovery.IsBlocked); Assert.True(LegacyTradeJournal.HasPending(f.Data.Users));
        AssertNoTradeSuccess(f.Sent); AssertNoTradeSuccess(f.OtherSent);
        Assert.Equal(before.Registry, File.ReadAllBytes(f.Data.State));
        var tickets = f.TicketCount;
        await f.CloseWorldAsync(); await f.OpenWorldAsync();
        Assert.Equal(tickets + 2, f.TicketCount);
        AssertSqlTradeState(f, before, exchanged: true);
        await AssertSqlTradeReplayAsync(f, before, exchanged: true);
        var recovered = f.Data.Files();
        await f.CloseWorldAsync(); await f.OpenWorldAsync();
        AssertSqlTradeState(f, before, exchanged: true); f.Data.AssertFiles(recovered);
        Assert.Equal(sql, await f.Database.SnapshotAsync());
    }, tradeItemState: "valid");
}
