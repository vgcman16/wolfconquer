using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    private sealed partial class SqlWorldFixture
    {
        internal const uint FirstBoundKept = 1_000_094, SecondBoundKept = 1_000_095;
        private readonly string? tradeItemState;
        internal Action<uint, byte[]>? BeforeTradeWire;
        internal LegacyTradeStateStore Trades => Assert.IsType<LegacyTradeStateStore>(typeof(WorldServerFrameHandler)
            .GetField("_tradeStates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Handler));

        private void ConfigureTradeInputs()
        {
            if (tradeItemState is null) return; // Original SQL5 fixtures remain unchanged.
            Assert.Contains(tradeItemState, new[] { "valid", "locked", "bound", "suspicious" });
            var loader = new PlayerItemSnapshotLoader(Data.Items);
            var first = loader.LoadByCharacterUid(FirstUid); var second = loader.LoadByCharacterUid(SecondUid);
            var offered = new PlayerItemSnapshotItem
            {
                Uid = SoldUid, ItemId = ItemId, StackSize = 1, Color = 5, Durability = 10, MaximumDurability = 10,
                Locked = tradeItemState == "locked" ? (byte)1 : (byte)0,
                Bound = tradeItemState == "bound" ? (byte)1 : (byte)0,
                Suspicious = tradeItemState == "suspicious" ? (byte)1 : (byte)0,
            };
            static PlayerItemSnapshotItem Retained(uint uid) => new()
            { Uid = uid, ItemId = ItemId, StackSize = 1, Color = 3, Durability = 9, MaximumDurability = 10, Bound = 1, Locked = 1 };
            loader.SaveByCharacterUid(FirstUid, first with { InventoryItems = first.InventoryItems
                .Select(item => item.Uid == SoldUid ? offered : item).Append(Retained(FirstBoundKept)).ToArray() });
            loader.SaveByCharacterUid(SecondUid, second with { InventoryItems = second.InventoryItems.Append(Retained(SecondBoundKept)).ToArray() });
        }

        private void InstallTradeWireObserver(RootLoopbackFramePeer peer, uint uid) => peer.OrdinaryWriteRequested = bytes =>
        {
            try { BeforeTradeWire?.Invoke(uid, bytes.ToArray()); }
            catch (Exception error) { observerFailure ??= error; throw; }
        };

        internal async Task TradeAsync(bool first, LegacyTradePacket packet)
        {
            var peer = Assert.IsType<RootLoopbackFramePeer>(first ? Peer : OtherPeer);
            var bytes = packet.ToNativeFrameBytes();
            Assert.True(LegacyTradePacket.TryParse(ReadFrame(bytes), out var parsed));
            Assert.Equal(packet.Action, parsed.Action); Assert.Equal(packet.UID, parsed.UID);
            Assert.Equal(packet.ItemUID, parsed.ItemUID); Assert.Equal(packet.Money, parsed.Money); Assert.Equal(packet.CPS, parsed.CPS);
            await Handler.HandleFrameAsync(peer.Connection, ReadFrame(bytes));
            await DrainTradeAsync();
        }

        internal async Task DrainTradeAsync()
        {
            if (Peer is { } first) await first.DrainObservations();
            if (OtherPeer is { } second) await second.DrainObservations();
            if (observerFailure is { } error) ExceptionDispatchInfo.Capture(error).Throw();
        }

        internal async Task<LegacyTradeParty> OpenTradeAsync()
        {
            await TradeAsync(true, new(0) { UID = SecondUid });
            await TradeAsync(false, new(1) { UID = FirstUid });
            Assert.True(Current.LegacyTradeActive, Diagnostics); Assert.True(Other.LegacyTradeActive, Diagnostics);
            lock (Trades.SyncRoot)
            {
                Assert.True(Trades.TryGet(Peer!.Connection, out var first));
                Assert.True(Trades.TryGet(OtherPeer!.Connection, out var second));
                Assert.Same(first.Other, second); Assert.Same(second.Other, first);
                Assert.True(first.IsRequester); Assert.False(second.IsRequester);
                Assert.Equal(FirstUid, first.Uid); Assert.Equal(SecondUid, second.Uid);
                Assert.Equal(first.TradeId, second.TradeId); Assert.NotEqual(Guid.Empty, first.TradeId);
                Assert.True(first.WindowOpen); Assert.True(second.WindowOpen);
                return first;
            }
        }

        internal async Task DisconnectOtherTradeActorAsync()
        {
            var other = Assert.IsType<RootLoopbackFramePeer>(OtherPeer);
            await other.Connection.DisconnectAsync();
            await other.Connection.CompleteTransportCloseAsync();
            await other.DisposeAsync(); OtherPeer = null;
            await DrainTradeAsync();
            Assert.False(Store.TryGet(other.Connection, out _));
        }
    }
}
