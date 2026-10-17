using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.LegacyFullAccountItemMutationFixture;

namespace WolfConquer.Tests;

public sealed class LegacyFullAccountP18AnimaMutationTests : IDisposable
{
    private const byte OptionId = 17;
    private const uint RewardItemId = 4_200_018;
    private const ushort CatalogDurability = 321;

    private readonly Fixture _fixture = new("p18-anima", accountId: 1_293_840);

    [Fact]
    public async Task GrantPersistsBeforeExposureAndSendsOnlyCurrentGameItemAdd()
    {
        var originalItems = Fixture.Items(Fixture.InventoryItem(41, 400_041));
        var result = await _fixture.InvokeAsync(
            OptionId,
            originalItems,
            LegacyItemWorldDataFixture.Create(ItemType()),
            observerCount: 2);

        Assert.True(result.Handled);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        var reward = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == RewardItemId);
        Assert.Equal(42U, reward.Uid);
        AssertRewardItem(reward);

        Assert.NotNull(result.PersistedAtCommit);
        var committedReward = Assert.Single(
            result.PersistedAtCommit!.Items.InventoryItems,
            item => item.ItemId == RewardItemId);
        AssertRewardItem(committedReward);
        Assert.Equal("keep-me", result.PersistedAtCommit.Character.Attributes["Sentinel"]);
        Fixture.AssertPlayerItemsEqual(result.UpdatedSession.PlayerItems, result.PersistedItems);
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
        Assert.Equal(["commit", "claimant:2116"], result.ExposureOrder);

        var frame = Fixture.ReadFrame(Assert.Single(result.ClaimantSent));
        Assert.Equal((ushort)2_116, frame.Header.Opcode);
        Assert.Equal(LegacyGameItemPacket.Opcode, frame.Header.Opcode);
        Assert.True(LegacyGameItemPacket.TryParse(frame, out var packet));
        Assert.Equal(reward.Uid, packet.Uid);
        Assert.Equal(RewardItemId, packet.ItemId);
        Assert.Equal(CatalogDurability, packet.Durability);
        Assert.Equal(CatalogDurability, packet.MaximumDurability);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, packet.Mode);
        Assert.Equal((byte)0, packet.Position);
        Assert.Equal((ushort)1, packet.StackSize);
        Assert.Equal((byte)0, packet.Bound);
        Assert.Equal((byte)0, packet.Plus);
        Assert.Equal((byte)0, packet.Bless);
        Assert.Equal((byte)0, packet.Enchant);
        Assert.Equal((byte)0, packet.SocketOne);
        Assert.Equal((byte)0, packet.SocketTwo);
        Assert.Equal(0U, packet.SocketProgress);
        Assert.Equal((byte)0, packet.Effect);
        Assert.Equal(0U, packet.SpellId);
        Assert.Equal(0U, packet.PerfectionLevel);
        Assert.Equal(0U, packet.PerfectionProgress);
        Assert.All(result.ObserverSent, Assert.Empty);
        Assert.DoesNotContain(
            result.ClaimantSent,
            bytes => LegacyNpcReplyPacket.TryParse(Fixture.ReadFrame(bytes), out _));
        Assert.DoesNotContain(
            result.ClaimantSent,
            bytes => LegacyMsgUpdatePacket.TryParse(Fixture.ReadFrame(bytes), out _));
        Assert.Contains("packetOrder=GameItemAdd", result.Log, StringComparison.Ordinal);
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FullBagWinsBeforeAnySuccessInfrastructure()
    {
        var fullInventory = Fixture.Items(
            Enumerable.Range(1, 40)
                .Select(index => Fixture.InventoryItem(
                    checked((uint)index),
                    checked((uint)(400_000 + index))))
                .ToArray());
        var result = await _fixture.InvokeAsync(
            OptionId,
            fullInventory,
            worldData: null,
            includeSessions: false,
            includeUsersRoot: false,
            includePlayerItemsRoot: false,
            observerCount: 1);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Null(result.PersistedAtCommit);
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
        Assert.Equal(result.OriginalItemBytes, File.ReadAllBytes(result.PlayerItemsPath));
        Assert.Equal(["claimant:2255"], result.ExposureOrder);

        var frame = Fixture.ReadFrame(Assert.Single(result.ClaimantSent));
        Assert.Equal((ushort)2_255, frame.Header.Opcode);
        Assert.True(LegacyNpcReplyPacket.TryParse(frame, out var reply));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, reply.InteractType);
        Assert.Equal("Please make 1 more space in your inventory.", reply.Text);
        Assert.All(result.ObserverSent, Assert.Empty);
        Assert.Contains("reason=inventory-full", result.Log, StringComparison.Ordinal);
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("sessions", "session-store-missing")]
    [InlineData("world-data", "world-data-missing")]
    [InlineData("metadata", "itemtype-missing")]
    [InlineData("users-root", "usersRootMissing")]
    [InlineData("player-items-root", "playerItemsRootMissing")]
    public async Task MissingSuccessInfrastructureFailsClosed(string missing, string expectedReason)
    {
        var worldData = missing switch
        {
            "world-data" => null,
            "metadata" => LegacyItemWorldDataFixture.Create(),
            _ => LegacyItemWorldDataFixture.Create(ItemType()),
        };
        var result = await _fixture.InvokeAsync(
            OptionId,
            Fixture.Items(Fixture.InventoryItem(41, 400_041)),
            worldData,
            includeSessions: missing != "sessions",
            includeUsersRoot: missing != "users-root",
            includePlayerItemsRoot: missing != "player-items-root",
            observerCount: 1);

        AssertUnchanged(result, expectedReason);
    }

    [Fact]
    public async Task InvalidCatalogDurabilityFailsClosed()
    {
        var result = await _fixture.InvokeAsync(
            OptionId,
            Fixture.Items(Fixture.InventoryItem(41, 400_041)),
            LegacyItemWorldDataFixture.Create(ItemType(ushort.MaxValue + 1U)));

        AssertUnchanged(result, "itemtype-invalid");
    }

    [Fact]
    public async Task ItemUidExhaustionFailsClosed()
    {
        var result = await _fixture.InvokeAsync(
            OptionId,
            Fixture.Items(Fixture.InventoryItem(uint.MaxValue, 400_041)),
            LegacyItemWorldDataFixture.Create(ItemType()));

        AssertUnchanged(result, "item-uid-exhausted");
    }

    [Theory]
    [InlineData(Fixture.PersistenceLock.Character)]
    [InlineData(Fixture.PersistenceLock.Items)]
    public async Task ForcedCommitFailureRollsBackCharacterAndItems(Fixture.PersistenceLock lockedFile)
    {
        var originalItems = Fixture.Items(Fixture.InventoryItem(41, 400_041));
        var result = await _fixture.InvokeAsync(
            OptionId,
            originalItems,
            LegacyItemWorldDataFixture.Create(ItemType()),
            persistenceLock: lockedFile,
            observerCount: 1);

        AssertUnchanged(result, "transactionWriteFailed");
        Fixture.AssertPlayerItemsEqual(originalItems, result.PersistedItems);
        Assert.Empty(Directory.EnumerateFiles(result.UsersRoot, "*.reward-points-*"));
        Assert.Empty(Directory.EnumerateFiles(result.PlayerItemsRoot, "*.reward-points-*"));
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }

    private static ItemType ItemType(uint durability = CatalogDurability)
    {
        return Fixture.ItemType(RewardItemId, "P18Anima", durability);
    }

    private static void AssertRewardItem(PlayerItemSnapshotItem item)
    {
        Assert.Equal(RewardItemId, item.ItemId);
        Assert.Equal(CatalogDurability, item.Durability);
        Assert.Equal(CatalogDurability, item.MaximumDurability);
        Assert.Equal((ushort)0, item.Position);
        Assert.Equal((ushort)1, item.StackSize);
        Assert.Equal((byte)0, item.Bound);
        Assert.Equal((byte)0, item.Plus);
        Assert.Equal((byte)0, item.Bless);
        Assert.Equal((byte)0, item.Enchant);
        Assert.Equal((byte)0, item.SocketOne);
        Assert.Equal((byte)0, item.SocketTwo);
        Assert.Equal(0U, item.SocketProgress);
        Assert.Equal(0U, item.Effect);
        Assert.Equal(0U, item.PerfectionLevel);
        Assert.Equal(0U, item.PerfectionProgress);
        Assert.InRange(item.Color, 3U, 8U);
    }

    private static void AssertUnchanged(Fixture.InvokeResult result, string expectedReason)
    {
        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Null(result.PersistedAtCommit);
        Assert.Empty(result.ClaimantSent);
        Assert.All(result.ObserverSent, Assert.Empty);
        Assert.Empty(result.ExposureOrder);
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
        Assert.Equal(result.OriginalItemBytes, File.ReadAllBytes(result.PlayerItemsPath));
        Assert.Contains("reason=" + expectedReason, result.Log, StringComparison.Ordinal);
        Assert.Contains(
            "no item, session state, or packet mutation was committed",
            result.Log,
            StringComparison.Ordinal);
    }
}
