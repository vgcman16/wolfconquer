using System.Buffers;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedNativeItemJoinTests
{
    [Fact]
    public async Task RegisteredJoinedVendorBuyPublishesBothCanonicalOwnersBeforeItsFirstResponseAndCannotReplay()
    {
        const uint itemUid = 910001;
        const long price = 125;
        var item = new PlayerItemSnapshotItem
        {
            Uid = itemUid,
            ItemId = 132009,
            Durability = 1,
            MaximumDurability = 1,
            StackSize = 1,
        };
        var saved = PlayerItemSnapshot.Empty with { InventoryItems = new[] { item } };
        await WorldServerFrameHandlerTests.WithNativeSpellJoinAsync(async f =>
        {
            await f.LoginAsync();
            var buyer = await f.AdmitSecondAsync();
            WorldSession CurrentBuyer()
            {
                Assert.True(f.Store.TryGet(buyer.Connection, out var current));
                return Assert.IsType<WorldSession>(current);
            }
            var sellerBinding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
            var buyerBinding = Assert.IsType<LegacyOrdinarySpellBinding>(buyer.Native.OrdinarySpellBinding);
            var sellerInventory = Assert.IsType<LegacyNativeInventoryRuntime>(f.Native.Inventory);
            var buyerInventory = Assert.IsType<LegacyNativeInventoryRuntime>(buyer.Native.Inventory);
            Assert.Equal(itemUid, Assert.Single(sellerInventory.ClientItems).Key);
            Assert.Empty(buyerInventory.ClientItems);
            Assert.False(f.Current.Character.Attributes.ContainsKey("RuntimeVendorUid"));

            int startAt = f.Sent.Count;
            await f.FrameAsync(new MsgActionPacket
            {
                ObjId = checked((uint)f.Current.Character.Uid),
                Type = MsgActionType.StartVendor,
            }.ToFrameBytes());
            var startReply = Assert.Single(f.Sent.Skip(startAt), bytes =>
                MsgActionPacket.TryParse(JoinedVendorFrame(bytes), out var action) &&
                action.Type == MsgActionType.StartVendor);
            Assert.True(MsgActionPacket.TryParse(JoinedVendorFrame(startReply), out var started));
            Assert.NotEqual(0u, started.DwParam);
            Assert.Equal(started.DwParam.ToString(CultureInfo.InvariantCulture),
                f.Current.Character.Attributes["RuntimeVendorUid"]);
            Assert.Empty(Assert.IsType<LegacyVendorListingBook>(f.Current.LegacyVendorListings).Listings);

            var listingRequest = new MsgItemUsagePacket(itemUid, checked((ulong)price), MsgItemUsageAction.AddVendingItemGold,
                Timestamp: 3210, DwParam2: 0, DwParam3: 0, DwParam4: 0);
            int listingAt = f.Sent.Count;
            await f.FrameAsync(listingRequest.ToFrameBytes());
            Assert.Equal(listingRequest.ToFrameBytes(), Assert.Single(f.Sent.Skip(listingAt)));
            Assert.True(f.Current.LegacyVendorListings!.TryGetListing(itemUid, out var listing));
            Assert.Equal(LegacyVendorListingCostMode.Gold, listing.CostMode);
            Assert.Equal(price, listing.Amount);

            var sellerBefore = f.Current;
            var buyerBefore = CurrentBuyer();
            Assert.Equal(1000, sellerBefore.Character.Money);
            Assert.Equal(1000, buyerBefore.Character.Money);
            var incoming = Assert.Single(sellerBefore.PlayerItems.InventoryItems);
            Assert.Equal(itemUid, incoming.Uid);
            var buyerItems = buyerBefore.PlayerItems with
            {
                InventoryItems = buyerBefore.PlayerItems.InventoryItems.Append(incoming).ToArray(),
            };
            var sellerItems = sellerBefore.PlayerItems with
            {
                InventoryItems = Array.Empty<PlayerItemSnapshotItem>(),
            };
            string buyerCharacterPath = Path.Combine(f.Users,
                buyerBefore.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
            string sellerCharacterPath = Path.Combine(f.Users,
                sellerBefore.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
            string buyerItemsPath = Path.Combine(f.Root, "PlayersItems",
                buyerBefore.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
            string sellerItemsPath = Path.Combine(f.Root, "PlayersItems",
                sellerBefore.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
            var expectedFiles = f.Files();
            expectedFiles[buyerCharacterPath] = JoinedVendorWalletImage(expectedFiles[buyerCharacterPath], 875, 200);
            expectedFiles[sellerCharacterPath] = JoinedVendorWalletImage(expectedFiles[sellerCharacterPath], 1125, 200);
            expectedFiles[buyerItemsPath] = PlayerItemSnapshotLoader.ToLegacyBytes(buyerItems);
            expectedFiles[sellerItemsPath] = PlayerItemSnapshotLoader.ToLegacyBytes(sellerItems);
            var request = new MsgItemUsagePacket(itemUid, started.DwParam, MsgItemUsageAction.BuyVendingItem,
                Timestamp: 3456, DwParam2: 2, DwParam3: 3, DwParam4: 4);
            byte[][] expectedFrames =
            [
                LegacyGameItemPacket.FromSnapshotItem(incoming, mode: LegacyGameItemPacket.AddItemMode).ToFrameBytes(),
                request.ToFrameBytes(),
                (request with { Action = MsgItemUsageAction.RemoveVendingItem }).ToFrameBytes(),
                new MsgItemUsagePacket(itemUid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes(),
                new LegacyMsgUpdatePacket(checked((uint)buyerBefore.Character.Uid),
                    [new(LegacyMsgUpdateDataType.Money, 875)]).ToFrameBytes(),
                new LegacyMsgUpdatePacket(checked((uint)sellerBefore.Character.Uid),
                    [new(LegacyMsgUpdateDataType.Money, 1125)]).ToFrameBytes(),
            ];

            void RequireCommittedPair()
            {
                var sellerNow = f.Current;
                var buyerNow = CurrentBuyer();
                Assert.NotSame(sellerBefore, sellerNow);
                Assert.NotSame(buyerBefore, buyerNow);
                Assert.Equal((875L, 200L), (buyerNow.Character.Money, buyerNow.Character.ConquerPoints));
                Assert.Equal((1125L, 200L), (sellerNow.Character.Money, sellerNow.Character.ConquerPoints));
                Assert.Same(sellerBinding, f.Native.OrdinarySpellBinding);
                Assert.Same(buyerBinding, buyer.Native.OrdinarySpellBinding);
                Assert.Same(sellerInventory, f.Native.Inventory);
                Assert.Same(buyerInventory, buyer.Native.Inventory);
                RequireJoinedVendorOwner(f.Store, f.Connection, f.Native, sellerNow, sellerBinding);
                RequireJoinedVendorOwner(f.Store, buyer.Connection, buyer.Native, buyerNow, buyerBinding);
                Assert.Empty(sellerInventory.ClientItems);
                var nativeIncoming = Assert.Single(buyerInventory.ClientItems);
                Assert.Equal(itemUid, nativeIncoming.Key);
                Assert.Equal(itemUid, nativeIncoming.Value.Uid);
                Assert.Equal(incoming.ItemId, nativeIncoming.Value.ITEM_ID);
                Assert.Empty(sellerNow.PlayerItems.InventoryItems);
                Assert.Equal(itemUid, Assert.Single(buyerNow.PlayerItems.InventoryItems).Uid);
                Assert.Empty(sellerNow.LegacyVendorListings!.Listings);
                Assert.Equal(expectedFiles[buyerItemsPath], PlayerItemSnapshotLoader.ToLegacyBytes(buyerNow.PlayerItems));
                Assert.Equal(expectedFiles[sellerItemsPath], PlayerItemSnapshotLoader.ToLegacyBytes(sellerNow.PlayerItems));
                f.RequireFiles(expectedFiles);
                Assert.False(LegacyTradeJournal.HasPending(f.Users));
                Assert.False(f.Recovery.IsBlocked);
            }

            int first = f.Sent.Count;
            int observed = 0;
            Exception? observationFailure = null;
            f.ObserveSend = async (bytes, token) =>
            {
                int index = observed++;
                try
                {
                    Assert.True(index < expectedFrames.Length, "Unexpected registered vendor response.");
                    Assert.Equal(expectedFrames[index], bytes);
                    if (index == 0)
                    {
                        Assert.False(Monitor.IsEntered(f.Trades.SyncRoot));
                        Assert.False(Monitor.IsEntered(f.Native.SpellAdmissionSyncRoot));
                        Assert.False(Monitor.IsEntered(f.Native.Player.SpellAdmissionSyncRoot));
                        Assert.False(Monitor.IsEntered(buyer.Native.SpellAdmissionSyncRoot));
                        Assert.False(Monitor.IsEntered(buyer.Native.Player.SpellAdmissionSyncRoot));
                        using var bound = CancellationTokenSource.CreateLinkedTokenSource(token);
                        bound.CancelAfter(TimeSpan.FromSeconds(5));
                        using var shared = await f.Operations.EnterSharedAsync(bound.Token);
                        RequireCommittedPair();
                    }
                }
                catch (Exception error)
                {
                    observationFailure = observationFailure is null ? error :
                        new AggregateException("Joined vendor response observations failed.", observationFailure, error);
                }
                f.Sent.Add(bytes);
            };
            try
            {
                await f.Handler.HandleFrameAsync(buyer.Connection, JoinedVendorFrame(request.ToFrameBytes()));

                Assert.Null(observationFailure);
                Assert.Equal(expectedFrames.Length, observed);
                Assert.Equal(first + expectedFrames.Length, f.Sent.Count);
                RequireCommittedPair();
                var committedSeller = f.Current;
                var committedBuyer = CurrentBuyer();
                var committedFiles = f.Files();

                await f.Handler.HandleFrameAsync(buyer.Connection, JoinedVendorFrame(request.ToFrameBytes()));

                Assert.Null(observationFailure);
                Assert.Equal(expectedFrames.Length, observed);
                Assert.Equal(first + expectedFrames.Length, f.Sent.Count);
                Assert.Same(committedSeller, f.Current);
                Assert.Same(committedBuyer, CurrentBuyer());
                f.RequireFiles(committedFiles);
                Assert.False(LegacyTradeJournal.HasPending(f.Users));
                Assert.Contains("completed vendor purchase", f.Log.ToString(), StringComparison.Ordinal);
            }
            finally
            {
                f.ObserveSend = null;
            }
        }, lineage: false, playerItems: saved);
    }

    private static void RequireJoinedVendorOwner(
        WorldSessionStore store,
        ConquerTcpConnection connection,
        LegacyNativeGameClientRuntime client,
        WorldSession session,
        LegacyOrdinarySpellBinding binding)
    {
        Assert.True(store.TryGet(connection, out var current));
        Assert.Same(session, current);
        Assert.Same(store, binding.Store);
        Assert.Same(connection, binding.Connection);
        Assert.Same(client, binding.Client);
        Assert.Same(client.Player, binding.Player);
        Assert.Same(session.PlayerItems, binding.ItemSnapshot);
        Assert.Same(session.PlayerItems, binding.Items.Image.FinalSnapshot);
        Assert.True(binding.Items.PhysicalOwnerIsCurrentPlain());
        Assert.True(store.IsBoundSpellOwnerCurrent(binding));
    }

    private static PacketFrame JoinedVendorFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static byte[] JoinedVendorWalletImage(byte[] before, long money, long cps)
    {
        var text = CharacterSnapshotFileEncoding.Decode(before, out var encoding);
        using var reader = new StringReader(text);
        var lines = new List<string>();
        int moneyFields = 0;
        int cpFields = 0;
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith("Money=", StringComparison.Ordinal))
            {
                moneyFields++;
                line = "Money=" + money.ToString(CultureInfo.InvariantCulture);
            }
            else if (line.StartsWith("ConquerPoints=", StringComparison.Ordinal))
            {
                cpFields++;
                line = "ConquerPoints=" + cps.ToString(CultureInfo.InvariantCulture);
            }
            lines.Add(line);
        }
        Assert.Equal(1, moneyFields);
        Assert.Equal(1, cpFields);
        return encoding.GetPreamble().Concat(encoding.GetBytes(
            string.Join(Environment.NewLine, lines) + Environment.NewLine)).ToArray();
    }
}
