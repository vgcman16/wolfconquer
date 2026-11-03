using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    [ModernSqlAuthFixtureFact]
    public Task RealSqlNativeTradeSwapsExistingUidsAndCurrenciesBeforeEveryWireThenColdRelogs() => Run(async f =>
    {
        var before = SqlTradeBefore.Capture(f); var sql = await f.Database.SnapshotAsync();
        var party = await PrepareSqlTradeAsync(f); AssertSqlTradeEscrow(f, before, party);
        await f.TradeAsync(true, new(9));
        AssertSqlTradeEscrow(f, before, party);
        f.Sent.Clear(); f.OtherSent.Clear();
        var observed = new List<uint>();
        f.BeforeTradeWire = (uid, _) => { AssertSqlTradeState(f, before, exchanged: true); observed.Add(uid); };
        try { await f.TradeAsync(false, new(9)); } finally { f.BeforeTradeWire = null; }
        Assert.Equal(new[] { SqlWorldFixture.SecondUid, SqlWorldFixture.FirstUid, SqlWorldFixture.SecondUid,
            SqlWorldFixture.FirstUid, SqlWorldFixture.FirstUid, SqlWorldFixture.SecondUid,
            SqlWorldFixture.SecondUid, SqlWorldFixture.FirstUid }, observed);
        AssertSqlTradeFrames(f, before);
        AssertSqlTradeState(f, before, exchanged: true);
        Assert.Equal(sql, await f.Database.SnapshotAsync());
        await AssertSqlTradeReplayAsync(f, before, exchanged: true);
        var firstItems = File.ReadAllBytes(f.ItemsPath); var secondItems = File.ReadAllBytes(f.OtherItemsPath);
        var tickets = f.TicketCount;
        await f.CloseWorldAsync(); await f.OpenWorldAsync();
        Assert.Equal(tickets + 2, f.TicketCount); // Both cold admissions reauthenticate through the real SQL repository.
        AssertSqlTradeState(f, before, exchanged: true);
        Assert.Equal(firstItems, File.ReadAllBytes(f.ItemsPath)); Assert.Equal(secondItems, File.ReadAllBytes(f.OtherItemsPath));
        await AssertSqlTradeReplayAsync(f, before, exchanged: true);
        Assert.Equal(sql, await f.Database.SnapshotAsync());
    }, tradeItemState: "valid");

    private static void AssertSqlTradeFrames(SqlWorldFixture f, SqlTradeBefore before)
    {
        Assert.Equal(4, f.Sent.Count); Assert.Equal(4, f.OtherSent.Count);
        AssertTradeWallet(f.Sent[0], SqlWorldFixture.FirstUid, before.First.Character.Money + 10, before.First.Character.ConquerPoints + 1);
        AssertTradeWallet(f.OtherSent[0], SqlWorldFixture.SecondUid, before.Second.Character.Money - 10, before.Second.Character.ConquerPoints - 1);
        Assert.Equal(RemoveGolden(SqlWorldFixture.SoldUid), f.Sent[1]);
        Assert.Equal(RemoveGolden(SqlWorldFixture.PeerItemUid), f.OtherSent[2]);
        AssertTradeIncoming(f.Sent[2], SqlWorldFixture.PeerItemUid);
        AssertTradeIncoming(f.OtherSent[1], SqlWorldFixture.SoldUid);
        Assert.Equal(new LegacyTradePacket(4) { UID = SqlWorldFixture.FirstUid }.ToNativeFrameBytes(), f.Sent[3]);
        Assert.Equal(new LegacyTradePacket(4) { UID = SqlWorldFixture.SecondUid }.ToNativeFrameBytes(), f.OtherSent[3]);
    }

    private static void AssertTradeWallet(byte[] bytes, uint uid, long money, long cp)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update));
        Assert.Equal(uid, update.Uid);
        Assert.Equal(new[] { new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Money, checked((ulong)money)),
            new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.ConquerPoints, checked((ulong)cp)) }, update.Updates);
    }

    private static void AssertTradeIncoming(byte[] bytes, uint uid)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var item));
        Assert.Equal(uid, item.Uid); Assert.Equal(SqlWorldFixture.ItemId, item.ItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, item.Mode);
        Assert.Equal((byte)0, item.Bound); Assert.Equal((ushort)1, item.StackSize);
    }
}
