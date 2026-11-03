using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task RealSqlTradeSecondCharacterAndItemLeasesRetainEscrowThenSamePairRetriesOnce() => Run(async f =>
    {
        var before = SqlTradeBefore.Capture(f); var sql = await f.Database.SnapshotAsync();
        var party = await PrepareSqlTradeAsync(f);
        foreach (var path in new[] { f.OtherUserPath, f.OtherItemsPath })
        {
            await f.TradeAsync(true, new(9));
            var files = f.Data.Files(); var first = f.Current; var second = f.Other;
            var firstImage = f.Native.OrdinarySpellBinding!.Items.Image;
            var secondImage = f.Other.NativeGameClient!.OrdinarySpellBinding!.Items.Image;
            f.Sent.Clear(); f.OtherSent.Clear();
            using (var unavailable = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                await f.TradeAsync(false, new(9));
                Assert.Same(first, f.Current); Assert.Same(second, f.Other);
                Assert.Same(firstImage, f.Native.OrdinarySpellBinding.Items.Image);
                Assert.Same(secondImage, f.Other.NativeGameClient.OrdinarySpellBinding.Items.Image);
                AssertNoTradeSuccess(f.Sent); AssertNoTradeSuccess(f.OtherSent);
            }
            f.Data.AssertFiles(files); AssertSqlTradeEscrow(f, before, party);
            Assert.False(party.Confirmed); Assert.False(party.Other.Confirmed);
            Assert.False(f.Runtime!.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        }
        await f.TradeAsync(true, new(9)); await f.TradeAsync(false, new(9));
        AssertSqlTradeState(f, before, exchanged: true);
        await f.CloseWorldAsync(); await f.OpenWorldAsync();
        AssertSqlTradeState(f, before, exchanged: true); await AssertSqlTradeReplayAsync(f, before, exchanged: true);
        Assert.Equal(sql, await f.Database.SnapshotAsync());
    }, tradeItemState: "valid");

    [ModernSqlAuthFixtureFact]
    public Task RealSqlTradeNegativeAndUnavailableFundsCannotDebitOrPublishSuccess() => Run(async f =>
    {
        var party = await f.OpenTradeAsync(); var files = f.Data.Files(); var first = f.Current; var second = f.Other;
        var sql = await f.Database.SnapshotAsync();
        foreach (var packet in new[] { new LegacyTradePacket(7) { Money = -1 },
            new LegacyTradePacket(7) { Money = first.Character.Money + 1 }, new LegacyTradePacket(12) { CPS = uint.MaxValue } })
        {
            f.Sent.Clear(); f.OtherSent.Clear(); await f.TradeAsync(true, packet);
            Assert.Empty(f.Sent); Assert.Empty(f.OtherSent); Assert.Same(first, f.Current); Assert.Same(second, f.Other);
            f.Data.AssertFiles(files); AssertSqlTradeActor(f, true); AssertSqlTradeActor(f, false);
            Assert.Equal(0L, party.Money); Assert.Equal(0U, party.ConquerPoints);
        }
        await f.TradeAsync(true, new(2)); Assert.Equal(sql, await f.Database.SnapshotAsync());
    }, tradeItemState: "valid");

    [ModernSqlAuthFixtureFact]
    public Task RealSqlTradeUndefinedHighActionCannotAliasTheMoneyOffer() => Run(async f =>
    {
        var party = await f.OpenTradeAsync(); var files = f.Data.Files(); var first = f.Current; var second = f.Other;
        var sql = await f.Database.SnapshotAsync();
        f.Sent.Clear(); f.OtherSent.Clear();
        await f.TradeAsync(true, new LegacyTradePacket(7) { Action = (1UL << 32) | 7, Money = 10 });
        Assert.Empty(f.Sent); Assert.Empty(f.OtherSent); Assert.Same(first, f.Current); Assert.Same(second, f.Other);
        f.Data.AssertFiles(files); AssertSqlTradeActor(f, true); AssertSqlTradeActor(f, false);
        Assert.Equal(0L, party.Money); Assert.Equal(0U, party.ConquerPoints);
        await f.TradeAsync(true, new(2)); Assert.Equal(sql, await f.Database.SnapshotAsync());
    }, tradeItemState: "valid");

    [ModernSqlAuthFixtureFact]
    public Task RealSqlNativeTradeLockedInventoryItemIsRefused() => AssertSqlIneligibleTradeAsync("locked");
    [ModernSqlAuthFixtureFact]
    public Task RealSqlNativeTradeBoundInventoryItemIsRefused() => AssertSqlIneligibleTradeAsync("bound");
    [ModernSqlAuthFixtureFact]
    public Task RealSqlNativeTradeSuspiciousInventoryItemIsRefused() => AssertSqlIneligibleTradeAsync("suspicious");

    private static Task AssertSqlIneligibleTradeAsync(string kind) => Run(async f =>
    {
        var party = await f.OpenTradeAsync(); var files = f.Data.Files(); var first = f.Current; var second = f.Other;
        var sql = await f.Database.SnapshotAsync(); f.Sent.Clear(); f.OtherSent.Clear();
        await f.TradeAsync(true, new(6) { ItemUID = SqlWorldFixture.SoldUid });
        Assert.Empty(party.Items); Assert.Empty(party.Other.Items);
        Assert.Same(first, f.Current); Assert.Same(second, f.Other); f.Data.AssertFiles(files);
        Assert.Empty(f.OtherSent); AssertNoTradeSuccess(f.Sent);
        Assert.Contains(f.Sent, bytes => LegacyTradePacket.TryParse(ReadFrame(bytes), out var denied) && denied.Action == 10);
        AssertSqlTradeActor(f, true); AssertSqlTradeActor(f, false);
        await f.TradeAsync(true, new(2)); Assert.Equal(sql, await f.Database.SnapshotAsync());
    }, tradeItemState: kind);

    private static void AssertNoTradeSuccess(IEnumerable<byte[]> frames)
    {
        foreach (var bytes in frames)
        {
            Assert.False(LegacyTradePacket.TryParse(ReadFrame(bytes), out var trade) && trade.Action == 4);
            Assert.False(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out _));
            Assert.False(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var usage) && usage.Action == MsgItemUsageAction.RemoveInventory);
            Assert.False(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out _));
        }
    }
}
