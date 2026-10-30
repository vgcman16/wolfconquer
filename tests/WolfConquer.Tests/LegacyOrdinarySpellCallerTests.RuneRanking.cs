using System.Reflection;
using WolfConquer.Core.World;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinarySpellCallerTests
{
    [Fact]
    public async Task AdmittedRankUpdateRejectsStaleRecordAfterARealStorePublication()
    {
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(true, async f =>
        {
            await f.Login(); var old = f.Current;
            var ranking = Assert.IsType<LegacyRuneRankingState>(typeof(WorldServerFrameHandler)
                .GetField("_runeRankings", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
            Assert.Equal(10600U, LegacyRuneStoragePacket.ComputeStorageScore(old.PlayerItems));
            Assert.True(f.Store.TryUpdateRuneRanking(f.Connection, old, ranking));
            Assert.Equal(old.Character.Name, Assert.Single(ranking.Snapshot(), x => x.CharacterUid == old.Character.Uid).CharacterName);
            Assert.True(f.Store.TryCompareAndSet(f.Connection, old, old with { Character = old.Character with { Name = "NewerName" } }));
            var newer = f.Current; Assert.NotSame(old, newer);
            Assert.False(f.Store.TryUpdateRuneRanking(f.Connection, old, ranking));
            Assert.True(f.Store.TryUpdateRuneRanking(f.Connection, newer, ranking));
            Assert.Equal("NewerName", Assert.Single(ranking.Snapshot(), x => x.CharacterUid == newer.Character.Uid).CharacterName);
            Assert.False(f.Store.TryUpdateRuneRanking(f.Connection, old, ranking));
            Assert.Equal("NewerName", Assert.Single(ranking.Snapshot(), x => x.CharacterUid == newer.Character.Uid).CharacterName);
        });
    }

    [Fact]
    public async Task RankLeaseCannotReenterAStoreWriter()
    {
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(true, async f =>
        {
            await f.Login(); var current = f.Current;
            var ranking = Assert.IsType<LegacyRuneRankingState>(typeof(WorldServerFrameHandler)
                .GetField("_runeRankings", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
            using (var lease = ranking.PrepareUpdate(current.Character, current.PlayerItems))
            {
                Assert.Throws<InvalidOperationException>(() => f.Store.Set(f.Connection, current));
                Assert.True(lease.IsCurrentPlain()); Assert.Same(current, f.Current);
            }
            Assert.True(f.Store.TryUpdateRuneRanking(f.Connection, current, ranking));
        });
    }
    [Fact]
    public async Task ActualAdmittedRuneUnequipNeedsNoInventedRankingOwnerWhenContextHasNone()
    {
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(true, async f =>
        {
            await f.Login(); await f.EstablishOwnedRuneOriginAsync();
            var before = f.Current; int first = f.Sent.Count;
            var context = new WorldPacketHandlerContext(f.Connection, f.Log, Session: before, WorldData: f.Catalog,
                Sessions: f.Store, PlayerSpellsRoot: f.Spells, PlayerItemsRoot: f.Items,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13)
                { Trades = f.Trades, TradeOperations = f.Operations, TradeRecovery = f.Recovery, RuneRankings = null };
            var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
            using var operation = await f.Operations.EnterSharedAsync(CancellationToken.None);
            await registry.HandleAsync(context, Frame(RuneRequest()), CancellationToken.None);
            Assert.NotSame(before, f.Current);
            Assert.Equal((ushort)212, Assert.Single(f.Current.PlayerItems.RuneItems, row => row.Uid == WorldServerFrameHandlerTests.NativeItemSpellCallerFixture.ItemUid).Position);
            Assert.DoesNotContain(f.Current.PlayerSpells.Spells, row => row.Id == 14160);
            Assert.Equal("", f.Current.Character.Attributes["ManagedRuneSpellOrigins"]);
            Assert.False(LegacyTradeJournal.HasPending(f.Users)); Assert.True(f.Sent.Count > first);
            Assert.Equal(f.Current.PlayerSpells.Spells, new PlayerSpellSnapshotLoader(f.Spells).LoadByCharacterUid(WorldServerFrameHandlerTests.NativeItemSpellCallerFixture.Uid).Spells);
        }, ownedRuneOrigin: true);
    }
}
