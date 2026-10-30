using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.WorldServerFrameHandlerTests.NativeItemSpellCallerFixture;

namespace WolfConquer.Tests;

public sealed partial class OwnedNativeItemJoinTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealBookAndRunePersistedHandlersRefreshNativeItemsBeforeTheirFirstOutput(bool rune)
    {
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(rune, async f =>
        {
            await f.Login(); var before = f.Current; var binding = RequireGraph(f);
            var inventory = f.Native.Inventory; var runes = f.Native.Rune;
            bool observed = false;
            f.OnSend = _ =>
            {
                if (observed) return;
                observed = true;
                Assert.NotSame(before, f.Current); RequireGraph(f);
                Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(f.Current.PlayerItems), File.ReadAllBytes(f.ItemsPath));
                Assert.False(LegacyTradeJournal.HasPending(f.Users));
            };
            var request = rune
                ? new MsgItemUsagePacket(Fixture.ItemUid, 102, MsgItemUsageAction.UnequipRune, 511043495, 0, 0, 0)
                : new MsgItemUsagePacket(Fixture.ItemUid, 0, MsgItemUsageAction.Equip, 1234, 0, 0, 0);

            await f.Send(request.ToFrameBytes()); f.OnSend = null;

            Assert.True(observed, f.Log.ToString());
            Assert.Same(binding, f.Native.OrdinarySpellBinding);
            Assert.Same(inventory, f.Native.Inventory); Assert.Same(runes, f.Native.Rune);
            if (rune) Assert.Equal((ushort)212, Assert.Single(runes!.Objects, row => row.Uid == Fixture.ItemUid).Position);
            else Assert.Empty(inventory!.ClientItems);
            RequireGraph(f);
        });
    }

    [Fact]
    public async Task RealItemWriterRefusalLeavesHolderAndPreparedNativeGraphUnpublished()
    {
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(false, async f =>
        {
            await f.Login(); var before = f.Current; var binding = RequireGraph(f); var image = binding.Items.Image;
            var actual = Assert.Single(f.Native.Inventory!.ClientItems).Value;
            var proposed = before with { PlayerItems = before.PlayerItems with { InventoryItems = Array.Empty<PlayerItemSnapshotItem>() } };
            string held = f.ItemsPath + ".owned-held";
            File.Move(f.ItemsPath, held);
            try
            {
                var files = f.Files(); bool called = false; var reason = string.Empty;
                bool Writer()
                {
                    called = true;
                    return LegacyNpcSalePersistence.TryPersistItemRemoval(before, proposed, f.Items, out reason);
                }

                Assert.False(f.Store.TryCommitSession(f.Connection, before, proposed, Writer, out var final));

                Assert.True(called); Assert.Null(final); Assert.NotEmpty(reason);
                Assert.Same(before, f.Current); Assert.Same(image, binding.Items.Image);
                Assert.Same(actual, Assert.Single(f.Native.Inventory.ClientItems).Value);
                f.CheckFiles(files); RequireGraph(f);
            }
            finally { File.Move(held, f.ItemsPath); }
        });
    }

    [Fact]
    public async Task ActualDurableItemCommitThenPhysicalOwnerLossRetiresAndBlocksWithoutRefundingTheFile()
    {
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(false, async f =>
        {
            await f.Login(); var before = f.Current; var binding = RequireGraph(f);
            var proposed = before with { PlayerItems = before.PlayerItems with { InventoryItems = Array.Empty<PlayerItemSnapshotItem>() } };
            bool durable = false; var reason = string.Empty;
            bool Writer()
            {
                durable = LegacyNpcSalePersistence.TryPersistItemRemoval(before, proposed, f.Items, out reason);
                if (durable) f.Native.Player = new LegacyNativePlayerRuntime(f.Native);
                return durable;
            }

            Assert.Throws<LegacyCommittedSpellPublicationException>(() =>
                f.Store.TryCommitSession(f.Connection, before, proposed, Writer));

            Assert.True(durable, reason); Assert.True(f.Recovery.IsBlocked);
            Assert.True(binding.IsRetired); Assert.False(binding.Items.PhysicalOwnerIsCurrentPlain());
            Assert.False(f.Store.TryGet(f.Connection, out _));
            Assert.Empty(PlayerItemSnapshotLoader.Parse(File.ReadAllBytes(f.ItemsPath)).InventoryItems);
            Assert.False(LegacyTradeJournal.HasPending(f.Users));
        });
    }

    [Fact]
    public async Task ActualCombinedMailboxPackagePopulatesNativeInventoryWithoutAnotherLogin()
    {
        await WorldServerFrameHandlerTests.WithNativeSpellJoinAsync(async f =>
        {
            await f.LoginAsync(); var before = f.Current;
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
            var inventory = f.Native.Inventory; var image = binding.Items.Image;
            Assert.Empty(inventory!.ClientItems); int first = f.Sent.Count;

            await f.FrameAsync(new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimRecordType, 21, []).ToFrameBytes());

            Assert.NotSame(before, f.Current); Assert.True(f.Sent.Count > first, f.Log.ToString());
            Assert.Same(inventory, f.Native.Inventory); Assert.Same(binding, f.Native.OrdinarySpellBinding);
            Assert.NotSame(image, binding.Items.Image);
            Assert.NotEmpty(inventory.ClientItems);
            Assert.Equal(f.Current.PlayerItems.InventoryItems.Select(row => row.Uid).Order(),
                inventory.ClientItems.Values.Select(row => row.Uid).Order());
            Assert.Same(f.Current.PlayerItems, binding.ItemSnapshot);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            var itemsPath = Path.Combine(f.Root, "PlayersItems", WorldServerFrameHandlerTests.NativeSpellJoinFixture.Uid + ".bin");
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(f.Current.PlayerItems), File.ReadAllBytes(itemsPath));
            var mail = Assert.Single(new LegacyMailboxStore(f.Root).Load(WorldServerFrameHandlerTests.NativeSpellJoinFixture.Uid), row => row.Id == 21);
            Assert.Equal(0UL, mail.Flag); Assert.False(LegacyTradeJournal.HasPending(f.Users));
        }, combined: true, lineage: false);
    }
}
