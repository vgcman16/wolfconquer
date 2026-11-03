using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    private sealed record SqlTradeBefore(WorldSession First, WorldSession Second, byte[] Registry,
        Dictionary<string, byte[]> Files)
    {
        internal static SqlTradeBefore Capture(SqlWorldFixture f) =>
            new(f.Current, f.Other, File.ReadAllBytes(f.Data.State), f.Data.Files());
    }

    private static void AssertSqlTradeActor(SqlWorldFixture f, bool first)
    {
        var session = first ? f.Current : f.Other;
        var path = first ? f.UserPath : f.OtherUserPath;
        var itemPath = first ? f.ItemsPath : f.OtherItemsPath;
        var expectedUid = first ? SqlWorldFixture.FirstUid : SqlWorldFixture.SecondUid;
        Assert.Equal(expectedUid, session.AccountId); Assert.Equal(expectedUid, session.Character.Uid);
        var native = Assert.IsType<LegacyNativeGameClientRuntime>(session.NativeGameClient);
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(native.OrdinarySpellBinding);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.True(binding.Items.PhysicalOwnerIsCurrentPlain());
        Assert.True(binding.Items.Image.IsCurrentPlain()); Assert.Same(session.PlayerItems, binding.Items.Image.FinalSnapshot);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(session.PlayerItems), File.ReadAllBytes(itemPath));
        Assert.Equal(session.PlayerItems.InventoryItems.Select(row => row.Uid).Order(), native.Inventory!.ClientItems.Keys.Order());
        Assert.Equal(session.Character.Money, native.Player!.Money);
        Assert.Equal(checked((uint)session.Character.ConquerPoints), native.Player.ConquerPoints);
        Assert.Equal(checked((int)session.Character.BoundConquerPoints), native.Player.BoundConquerPoints);
        Assert.Equal(checked((ulong)session.Character.BankConquerPoints), native.ConquerPointsSave);
        var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(path), out _));
        Assert.Equal(session.Character.Money, saved.Money); Assert.Equal(session.Character.ConquerPoints, saved.ConquerPoints);
        Assert.True(LegacyTradeEscrowClaims.TryRead(saved.Attributes, out var savedClaim, out var reason), reason);
        Assert.True(LegacyTradeEscrowClaims.TryRead(session.Character.Attributes, out var liveClaim, out reason), reason);
        Assert.Equal(savedClaim, liveClaim);
        Assert.Contains("KeepBytes=startup-owned-value", File.ReadAllText(path));
    }

    private static void AssertSqlTradeState(SqlWorldFixture f, SqlTradeBefore before, bool exchanged)
    {
        AssertSqlTradeActor(f, true); AssertSqlTradeActor(f, false);
        Assert.Equal(before.First.Character.Money + (exchanged ? 10 : 0), f.Current.Character.Money);
        Assert.Equal(before.Second.Character.Money - (exchanged ? 10 : 0), f.Other.Character.Money);
        Assert.Equal(before.First.Character.ConquerPoints + (exchanged ? 1 : 0), f.Current.Character.ConquerPoints);
        Assert.Equal(before.Second.Character.ConquerPoints - (exchanged ? 1 : 0), f.Other.Character.ConquerPoints);
        foreach (var pair in new[] { (before.First, f.Current), (before.Second, f.Other) })
        {
            Assert.Equal(pair.Item1.Character.BoundConquerPoints, pair.Item2.Character.BoundConquerPoints);
            Assert.Equal(pair.Item1.Character.BankConquerPoints, pair.Item2.Character.BankConquerPoints);
            Assert.False(pair.Item2.LegacyTradeActive);
            Assert.True(LegacyTradeEscrowClaims.TryRead(pair.Item2.Character.Attributes, out var claim, out var reason), reason);
            Assert.Null(claim);
        }
        var firstRows = before.First.PlayerItems.InventoryItems;
        var secondRows = before.Second.PlayerItems.InventoryItems;
        var firstOffered = Assert.Single(firstRows, row => row.Uid == SqlWorldFixture.SoldUid);
        var secondOffered = Assert.Single(secondRows, row => row.Uid == SqlWorldFixture.PeerItemUid);
        var expectedFirst = exchanged ? before.First.PlayerItems with { InventoryItems = firstRows
            .Where(row => row.Uid != SqlWorldFixture.SoldUid).Append(secondOffered).ToArray() } : before.First.PlayerItems;
        var expectedSecond = exchanged ? before.Second.PlayerItems with { InventoryItems = secondRows
            .Where(row => row.Uid != SqlWorldFixture.PeerItemUid).Append(firstOffered).ToArray() } : before.Second.PlayerItems;
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expectedFirst), PlayerItemSnapshotLoader.ToLegacyBytes(f.Current.PlayerItems));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expectedSecond), PlayerItemSnapshotLoader.ToLegacyBytes(f.Other.PlayerItems));
        var all = f.Current.PlayerItems.InventoryItems.Concat(f.Other.PlayerItems.InventoryItems).ToArray();
        Assert.Equal(all.Length, all.Select(row => row.Uid).Distinct().Count());
        Assert.Equal(firstRows.Concat(secondRows).Select(row => row.Uid).Order(), all.Select(row => row.Uid).Order());
        Assert.Equal((byte)1, Assert.Single(all, row => row.Uid == SqlWorldFixture.FirstBoundKept).Bound);
        Assert.Equal((byte)1, Assert.Single(all, row => row.Uid == SqlWorldFixture.SecondBoundKept).Locked);
        Assert.Equal(before.Registry, File.ReadAllBytes(f.Data.State));
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users)); Assert.False(f.Runtime!.Recovery.IsBlocked);
        lock (f.Trades.SyncRoot)
        { Assert.False(f.Trades.Contains(f.Peer!.Connection)); Assert.False(f.Trades.Contains(f.OtherPeer!.Connection)); }
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { f.UserPath, f.OtherUserPath, f.ItemsPath, f.OtherItemsPath };
        foreach (var file in before.Files)
        {
            var full = Path.Combine(f.Data.Root, file.Key);
            if (!targets.Contains(full)) Assert.Equal(file.Value, File.ReadAllBytes(full));
        }
    }

    private static async Task<LegacyTradeParty> PrepareSqlTradeAsync(SqlWorldFixture f)
    {
        var party = await f.OpenTradeAsync();
        var firstNative = f.Native.Inventory!.ClientItems[SqlWorldFixture.SoldUid];
        var secondNative = f.Other.NativeGameClient!.Inventory!.ClientItems[SqlWorldFixture.PeerItemUid];
        await f.TradeAsync(true, new(6) { ItemUID = SqlWorldFixture.SoldUid });
        await f.TradeAsync(false, new(6) { ItemUID = SqlWorldFixture.PeerItemUid });
        lock (f.Trades.SyncRoot)
        {
            Assert.Single(party.Items); Assert.Single(party.Other.Items);
            Assert.IsType<WorldSessionStore.TradeItemLineage>(party.Items[SqlWorldFixture.SoldUid].Lineage);
            Assert.IsType<WorldSessionStore.TradeItemLineage>(party.Other.Items[SqlWorldFixture.PeerItemUid].Lineage);
        }
        await OfferSqlCurrencyAsync(f, true, new(7) { Money = 10 });
        await OfferSqlCurrencyAsync(f, true, new(12) { CPS = 1 });
        await OfferSqlCurrencyAsync(f, false, new(7) { Money = 20 });
        await OfferSqlCurrencyAsync(f, false, new(12) { CPS = 2 });
        AssertSqlTradeActor(f, true); AssertSqlTradeActor(f, false);
        Assert.Same(firstNative, f.Native.Inventory!.ClientItems[SqlWorldFixture.SoldUid]);
        Assert.Same(secondNative, f.Other.NativeGameClient!.Inventory!.ClientItems[SqlWorldFixture.PeerItemUid]);
        lock (f.Trades.SyncRoot)
        {
            Assert.Equal(10L, party.Money); Assert.Equal(1U, party.ConquerPoints);
            Assert.Equal(20L, party.Other.Money); Assert.Equal(2U, party.Other.ConquerPoints);
            var first = Assert.Single(f.Current.PlayerItems.InventoryItems, row => row.Uid == SqlWorldFixture.SoldUid);
            var second = Assert.Single(f.Other.PlayerItems.InventoryItems, row => row.Uid == SqlWorldFixture.PeerItemUid);
            Assert.True(party.Items[SqlWorldFixture.SoldUid].Lineage!.MatchesCurrent(f.Store, f.Peer!.Connection, f.Current, first));
            Assert.True(party.Other.Items[SqlWorldFixture.PeerItemUid].Lineage!.MatchesCurrent(f.Store, f.OtherPeer!.Connection, f.Other, second));
        }
        return party;
    }

    private static async Task OfferSqlCurrencyAsync(SqlWorldFixture f, bool first, LegacyTradePacket packet)
    {
        Assert.Null(f.BeforeTradeWire);
        var before = first ? f.Current : f.Other;
        long expectedMoney = before.Character.Money - (packet.Type == 7 ? packet.Money : 0);
        long expectedCp = before.Character.ConquerPoints - (packet.Type == 12 ? packet.CPS : 0);
        int observed = 0;
        f.BeforeTradeWire = (_, _) =>
        {
            AssertSqlTradeActor(f, true); AssertSqlTradeActor(f, false);
            var actual = first ? f.Current : f.Other;
            Assert.Equal(expectedMoney, actual.Character.Money); Assert.Equal(expectedCp, actual.Character.ConquerPoints);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems), PlayerItemSnapshotLoader.ToLegacyBytes(actual.PlayerItems));
            observed++;
        };
        try { await f.TradeAsync(first, packet); } finally { f.BeforeTradeWire = null; }
        Assert.Equal(2, observed); // One actual wallet update and one partner notification.
    }

    private static void AssertSqlTradeEscrow(SqlWorldFixture f, SqlTradeBefore before, LegacyTradeParty party)
    {
        AssertSqlTradeActor(f, true); AssertSqlTradeActor(f, false);
        Assert.True(f.Current.LegacyTradeActive); Assert.True(f.Other.LegacyTradeActive);
        Assert.Equal(before.First.Character.Money - 10, f.Current.Character.Money);
        Assert.Equal(before.Second.Character.Money - 20, f.Other.Character.Money);
        Assert.Equal(before.First.Character.ConquerPoints - 1, f.Current.Character.ConquerPoints);
        Assert.Equal(before.Second.Character.ConquerPoints - 2, f.Other.Character.ConquerPoints);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(before.First.PlayerItems), PlayerItemSnapshotLoader.ToLegacyBytes(f.Current.PlayerItems));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(before.Second.PlayerItems), PlayerItemSnapshotLoader.ToLegacyBytes(f.Other.PlayerItems));
        foreach (var row in new[] { (f.Current, 10L, 1U), (f.Other, 20L, 2U) })
        {
            Assert.True(LegacyTradeEscrowClaims.TryRead(row.Item1.Character.Attributes, out var claim, out var reason), reason);
            Assert.NotNull(claim); Assert.Equal(party.TradeId, claim.TradeId);
            Assert.Equal(row.Item2, claim.Money); Assert.Equal(row.Item3, claim.ConquerPoints);
        }
        lock (f.Trades.SyncRoot)
        {
            Assert.True(f.Trades.TryGet(f.Peer!.Connection, out var actual)); Assert.Same(party, actual);
            Assert.True(f.Trades.TryGet(f.OtherPeer!.Connection, out var other)); Assert.Same(party.Other, other);
            Assert.True(actual.WindowOpen); Assert.True(other.WindowOpen);
            Assert.Single(actual.Items); Assert.Single(other.Items);
        }
        Assert.Equal(before.Registry, File.ReadAllBytes(f.Data.State));
    }

    private static async Task AssertSqlTradeReplayAsync(SqlWorldFixture f, SqlTradeBefore before, bool exchanged)
    {
        var files = f.Data.Files(); var first = f.Current; var second = f.Other;
        // Native action9 without a pair may send a close notification; it must not transfer or pay again.
        await f.TradeAsync(true, new(9)); await f.TradeAsync(false, new(9));
        Assert.Same(first, f.Current); Assert.Same(second, f.Other); f.Data.AssertFiles(files);
        AssertSqlTradeState(f, before, exchanged);
    }
}
