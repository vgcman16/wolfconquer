using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyPlayerBoothItemClosureTests
{
    [Fact]
    public Task ActualD13FrameBoothBuyOwnsExclusiveWithoutOuterSharedUpgradeAndReplayCannotResell() =>
        RunBoothSettlementAsync(async fixture =>
        {
            await fixture.EnterSettlementFrameAsync();
            await fixture.SendSettlementFrameAsync(fixture.SellerConnection, new MsgActionPacket
            {
                ObjId = D13VendorTestHarness.SellerCharacterUid,
                Type = MsgActionType.StartVendor,
            }.ToFrameBytes());
            var listedAt = fixture.Harness.Sent.Count;
            await fixture.SendSettlementFrameAsync(fixture.SellerConnection, BoothFixture.ListingRequest().ToFrameBytes());
            var listing = Assert.Single(fixture.Harness.Sent.Skip(listedAt));
            Assert.Equal("seller", listing.Recipient);
            Assert.Equal(BoothFixture.ListingRequest().ToFrameBytes(), listing.Bytes);
            var before = fixture.ReadFiles();
            var buyerBefore = fixture.ReadFrameCharacter(buyer: true);
            var sellerBefore = fixture.ReadFrameCharacter(buyer: false);
            Assert.Equal(1_000, buyerBefore.Money);
            Assert.Equal(300, sellerBefore.Money);
            var itemLoader = new PlayerItemSnapshotLoader(fixture.Harness.PlayerItemsRoot);
            var buyerItems = itemLoader.LoadByCharacterUid(D13VendorTestHarness.BuyerCharacterUid);
            var sellerItems = itemLoader.LoadByCharacterUid(D13VendorTestHarness.SellerCharacterUid);
            var original = Assert.Single(sellerItems.InventoryItems);
            var frameStart = fixture.Harness.Sent.Count;

            await fixture.SendSettlementFrameAsync(fixture.BuyerConnection, BoothFixture.BuyRequest().ToFrameBytes());

            Assert.Equal(875, fixture.ReadFrameCharacter(buyer: true).Money);
            Assert.Equal(425, fixture.ReadFrameCharacter(buyer: false).Money);
            Assert.Equal(200, fixture.ReadFrameCharacter(buyer: true).ConquerPoints);
            Assert.Equal(50, fixture.ReadFrameCharacter(buyer: false).ConquerPoints);
            var expectedBuyerItems = buyerItems with
            { InventoryItems = buyerItems.InventoryItems.Append(original).ToArray() };
            var expectedSellerItems = sellerItems with { InventoryItems = Array.Empty<PlayerItemSnapshotItem>() };
            var expected = new Dictionary<string, byte[]>(before, StringComparer.OrdinalIgnoreCase)
            {
                [fixture.FrameCharacterRelativePath(buyer: true)] = ProjectBoothWallet(
                    before[fixture.FrameCharacterRelativePath(buyer: true)], 875, 200),
                [fixture.FrameCharacterRelativePath(buyer: false)] = ProjectBoothWallet(
                    before[fixture.FrameCharacterRelativePath(buyer: false)], 425, 50),
                [fixture.FrameItemRelativePath(buyer: true)] = PlayerItemSnapshotLoader.ToLegacyBytes(expectedBuyerItems),
                [fixture.FrameItemRelativePath(buyer: false)] = PlayerItemSnapshotLoader.ToLegacyBytes(expectedSellerItems),
            };
            fixture.AssertFiles(expected);
            Assert.Equal(expected[fixture.FrameItemRelativePath(buyer: true)],
                PlayerItemSnapshotLoader.ToLegacyBytes(itemLoader.LoadByCharacterUid(D13VendorTestHarness.BuyerCharacterUid)));
            Assert.Equal(expected[fixture.FrameItemRelativePath(buyer: false)],
                PlayerItemSnapshotLoader.ToLegacyBytes(itemLoader.LoadByCharacterUid(D13VendorTestHarness.SellerCharacterUid)));
            var frames = fixture.Harness.Sent.Skip(frameStart).ToArray();
            Assert.Equal(6, frames.Length);
            Assert.Equal(new[] { "buyer", "buyer", "seller", "seller", "buyer", "seller" }, frames.Select(frame => frame.Recipient));
            var request = BoothFixture.BuyRequest();
            Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(original).ToFrameBytes(), frames[0].Bytes);
            Assert.Equal(request.ToFrameBytes(), frames[1].Bytes);
            Assert.Equal((request with { Action = MsgItemUsageAction.RemoveVendingItem }).ToFrameBytes(), frames[2].Bytes);
            Assert.Equal(new MsgItemUsagePacket(10, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes(), frames[3].Bytes);
            Assert.Equal(new LegacyMsgUpdatePacket(D13VendorTestHarness.BuyerCharacterUid,
                [new(LegacyMsgUpdateDataType.Money, 875)]).ToFrameBytes(), frames[4].Bytes);
            Assert.Equal(new LegacyMsgUpdatePacket(D13VendorTestHarness.SellerCharacterUid,
                [new(LegacyMsgUpdateDataType.Money, 425)]).ToFrameBytes(), frames[5].Bytes);
            Assert.False(LegacyTradeJournal.HasPending(fixture.Harness.UsersRoot));

            await fixture.SendSettlementFrameAsync(fixture.BuyerConnection, request.ToFrameBytes());

            fixture.AssertFiles(expected);
            Assert.Equal(frameStart + 6, fixture.Harness.Sent.Count);
            Assert.Contains("completed vendor purchase", fixture.Log.ToString(), StringComparison.Ordinal);
        });

    private sealed partial class BoothFixture
    {
        private WorldServerFrameHandler? settlementFrameOwner;

        internal async Task EnterSettlementFrameAsync()
        {
            // Real auth loader requires equal account/character IDs. This single
            // Frame case uses its genuine login contract, not the distinct-ID
            // business component fixture or private Store reflection.
            var seller = Harness.CreateSeller(300, 50, SevenStores(new[] { BoothItem(10) }, 40_000));
            var buyer = Harness.CreateBuyer(1_000, 200, SevenStores(new[] { BoothItem(50) }, 30_000));
            SeedOne(SellerConnection, seller with { AccountId = seller.Character.Uid });
            SeedOne(BuyerConnection, buyer with { AccountId = buyer.Character.Uid });
            var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
            var loader = new CharacterSnapshotLoader(Harness.UsersRoot, Harness.PlayerItemsRoot,
                playerSpellsRoot: null, playerProficienciesRoot: null, playersCombatGearRoot: null, playerQuestsRoot: null);
            settlementFrameOwner = new WorldServerFrameHandler(loader, handoff, worldData: null,
                runtimeUpdateLoop: LegacyRuntimeUpdateLoopOptions.Disabled, log: Log,
                protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            foreach (var (connection, uid, username) in new[]
            {
                (SellerConnection, D13VendorTestHarness.SellerCharacterUid, "seller"),
                (BuyerConnection, D13VendorTestHarness.BuyerCharacterUid, "buyer"),
            })
            {
                var ticket = handoff.Issue(uid, username, ConquerRuntimeDefaults.WorldServer);
                var before = Harness.Sent.Count;
                await SendSettlementFrameAsync(connection, new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes());
                Assert.True(Harness.Sent.Count > before);
                var loaded = ReadFrameCharacter(uid == D13VendorTestHarness.BuyerCharacterUid);
                Assert.Equal(WorldSnapshotResponsePacket.Build(WorldSnapshotWireStatus.Success, uid, loaded), Harness.Sent[before].Bytes);
                await SendSettlementFrameAsync(connection, new MsgActionPacket
                { ObjId = uid, Type = MsgActionType.SetLocation }.ToFrameBytes());
            }
        }

        internal async Task SendSettlementFrameAsync(ConquerTcpConnection connection, byte[] bytes)
        {
            // No lifetime-cancellation callback is fabricated. On an unknown
            // timeout retain the real owner/tasks; never call timeout a drain.
            var actual = settlementFrameOwner!.HandleFrameAsync(connection,
                D13VendorTestHarness.ReadFrame(bytes), CancellationToken.None).AsTask();
            Calls.Add(actual);
            await ObserveSettlementAsync(actual);
        }

        internal string FrameCharacterRelativePath(bool buyer) => Path.Combine("Users",
            (buyer ? D13VendorTestHarness.BuyerCharacterUid : D13VendorTestHarness.SellerCharacterUid) + ".ini");

        internal string FrameItemRelativePath(bool buyer) => Path.Combine("PlayerItems",
            (buyer ? D13VendorTestHarness.BuyerCharacterUid : D13VendorTestHarness.SellerCharacterUid) + ".bin");

        internal CharacterSnapshot ReadFrameCharacter(bool buyer)
        {
            var path = Path.Combine(Root, FrameCharacterRelativePath(buyer));
            return CharacterSnapshotParser.ParseText(File.ReadAllText(path), path);
        }
    }
}
