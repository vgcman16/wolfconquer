using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.LegacyFullAccountItemMutationFixture;

namespace WolfConquer.Tests;

public sealed class LegacyFullAccountRandomRelicMutationTests : IDisposable
{
    private const byte OptionId = 15;

    private readonly Fixture _fixture = new("max-random-relic", accountId: 1_293_860);

    [Fact]
    public async Task MaxRelicPersistsBeforeExposureAndSendsOnlyCurrentGameItemAdd()
    {
        var originalItems = Fixture.Items(Fixture.InventoryItem(41, 400_041));
        var worldData = LegacyMaxRandomRelicTestData.CreateWorldData();
        var result = await _fixture.InvokeAsync(
            OptionId,
            originalItems,
            worldData,
            observerCount: 2);

        Assert.True(result.Handled);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        var reward = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => LegacyMaxRandomRelicTestData.ItemIds.Contains(item.ItemId));
        Assert.Equal(42U, reward.Uid);
        AssertMaxRelic(reward);

        Assert.NotNull(result.PersistedAtCommit);
        var committedReward = Assert.Single(
            result.PersistedAtCommit!.Items.InventoryItems,
            item => LegacyMaxRandomRelicTestData.ItemIds.Contains(item.ItemId));
        AssertMaxRelic(committedReward);
        Fixture.AssertPlayerItemsEqual(result.UpdatedSession.PlayerItems, result.PersistedItems);
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
        Assert.Equal(["commit", "claimant:2116"], result.ExposureOrder);

        var frame = Fixture.ReadFrame(Assert.Single(result.ClaimantSent));
        Assert.Equal((ushort)2_116, frame.Header.Opcode);
        Assert.True(LegacyGameItemPacket.TryParse(frame, out var packet));
        Assert.Equal(reward.Uid, packet.Uid);
        Assert.Equal(reward.ItemId, packet.ItemId);
        Assert.Equal((ushort)10_099, packet.Durability);
        Assert.Equal((ushort)10_099, packet.MaximumDurability);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, packet.Mode);
        Assert.Equal((ushort)1, packet.StackSize);
        Assert.Equal((byte)0, packet.Bound);
        Assert.Equal(reward.Attribute1, packet.Attribute1);
        Assert.Equal(reward.Attribute2, packet.Attribute2);
        Assert.Equal(reward.Attribute3, packet.Attribute3);
        Assert.Equal(reward.Attribute4, packet.Attribute4);
        Assert.Equal(reward.Attribute5, packet.Attribute5);
        Assert.All(result.ObserverSent, Assert.Empty);
        Assert.DoesNotContain(
            result.ClaimantSent,
            bytes => LegacyNpcReplyPacket.TryParse(Fixture.ReadFrame(bytes), out _));
        Assert.DoesNotContain(
            result.ClaimantSent,
            bytes => LegacyMsgUpdatePacket.TryParse(Fixture.ReadFrame(bytes), out _));
        Assert.Contains("Max:true", result.Log, StringComparison.Ordinal);
        Assert.Contains("packetOrder=GameItemAdd", result.Log, StringComparison.Ordinal);
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FullBagIsSilentBeforeAnySuccessInfrastructure()
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
        Assert.Empty(result.ClaimantSent);
        Assert.All(result.ObserverSent, Assert.Empty);
        Assert.Empty(result.ExposureOrder);
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
        Assert.Equal(result.OriginalItemBytes, File.ReadAllBytes(result.PlayerItemsPath));
        Assert.Contains("reason=inventory-full", result.Log, StringComparison.Ordinal);
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("sessions", "session-store-missing")]
    [InlineData("world-data", "world-data-missing")]
    [InlineData("random-data", "random-relic-data-missing")]
    [InlineData("users-root", "usersRootMissing")]
    [InlineData("player-items-root", "playerItemsRootMissing")]
    public async Task MissingSuccessInfrastructureFailsClosed(string missing, string expectedReason)
    {
        var worldData = missing switch
        {
            "world-data" => null,
            "random-data" => LegacyMaxRandomRelicTestData.CreateWorldData(
                includeRandomAttributes: false),
            _ => LegacyMaxRandomRelicTestData.CreateWorldData(),
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
    public async Task ItemUidExhaustionFailsClosed()
    {
        var result = await _fixture.InvokeAsync(
            OptionId,
            Fixture.Items(Fixture.InventoryItem(uint.MaxValue, 400_041)),
            LegacyMaxRandomRelicTestData.CreateWorldData());

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
            LegacyMaxRandomRelicTestData.CreateWorldData(),
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

    private static void AssertMaxRelic(PlayerItemSnapshotItem item)
    {
        Assert.Contains(item.ItemId, LegacyMaxRandomRelicTestData.ItemIds);
        Assert.Equal((ushort)10_099, item.Durability);
        Assert.Equal((ushort)10_099, item.MaximumDurability);
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
        Assert.Equal(
            LegacyMaxRandomRelicTestData.ExpectedEncodedAttributes.Order().ToArray(),
            new[]
            {
                item.Attribute1,
                item.Attribute2,
                item.Attribute3,
                item.Attribute4,
                item.Attribute5,
            }.Order().ToArray());
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
