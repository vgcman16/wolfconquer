using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyFullAccountRuneMutationTests : IDisposable
{
    private readonly LegacyFullAccountItemMutationFixture _fixture =
        new("runes", accountId: 1_293_860);

    [Theory]
    [InlineData(12, 4010101U, 4010102U, 4010001U, 401U, 1U)]
    [InlineData(13, 4020227U, 4020226U, 4020127U, 402U, 27U)]
    [InlineData(14, 4030209U, 4030208U, 4030109U, 403U, 9U)]
    public async Task EligibleGrantUsesSourcePredicateCatalogOrderAndCurrentRunePacket(
        byte optionId,
        uint firstItemId,
        uint wrongLevelItemId,
        uint secondItemId,
        uint expectedFamily,
        uint expectedMaxLevel)
    {
        var firstType = RuneType(firstItemId, durability: 321);
        var secondType = RuneType(secondItemId, durability: 123);
        var worldData = LegacyItemWorldDataFixture.Create(
            firstType,
            RuneType(wrongLevelItemId, durability: 456),
            secondType,
            RuneType(4100001, durability: 100));

        var result = await _fixture.InvokeAsync(
            optionId,
            LegacyFullAccountItemMutationFixture.Items(),
            worldData,
            observerCount: 1);

        Assert.True(result.Handled);
        Assert.Equal("commit", result.ExposureOrder[0]);
        Assert.NotNull(result.PersistedAtCommit);
        Assert.Equal([firstItemId, secondItemId], result.UpdatedSession.PlayerItems.InventoryItems.Select(item => item.ItemId));
        Assert.Equal([firstItemId, secondItemId], result.PersistedAtCommit!.Items.InventoryItems.Select(item => item.ItemId));
        Assert.Equal([firstItemId, secondItemId], result.PersistedItems.InventoryItems.Select(item => item.ItemId));
        Assert.All(result.UpdatedSession.PlayerItems.InventoryItems, item =>
        {
            Assert.Equal((byte)0, item.Bound);
            Assert.Equal((ushort)1, item.StackSize);
            Assert.InRange(item.Color, 3U, 8U);
            Assert.Equal((ushort)0, item.Position);
        });
        Assert.Equal((ushort)321, result.UpdatedSession.PlayerItems.InventoryItems[0].Durability);
        Assert.Equal((ushort)123, result.UpdatedSession.PlayerItems.InventoryItems[1].Durability);

        Assert.Equal(2, result.ClaimantSent.Count);
        var packets = result.ClaimantSent.Select(ParseGameItem).ToArray();
        Assert.Equal([firstItemId, secondItemId], packets.Select(packet => packet.ItemId));
        Assert.All(packets, packet =>
        {
            Assert.Equal(LegacyGameItemPacket.AddItemMode, packet.Mode);
            Assert.Equal(packet.ItemId - packet.ItemId % 100 + 1, packet.SpellId);
            Assert.Equal(LegacyGameItemPacket.RuneEffect, packet.Effect);
            Assert.Equal((byte)0, packet.Bound);
            Assert.Equal((ushort)1, packet.StackSize);
        });
        Assert.All(result.ObserverSent, Assert.Empty);
        Assert.Contains($"family={expectedFamily}", result.Log, StringComparison.Ordinal);
        Assert.Contains($"maxLevel={expectedMaxLevel}", result.Log, StringComparison.Ordinal);
        Assert.Contains("catalogMatches=2 granted=2 capacitySkipped=0", result.Log, StringComparison.Ordinal);
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(12, 12, "Please make 12 more space in your inventory.")]
    [InlineData(13, 22, "Please make 2 more space in your inventory.")]
    [InlineData(14, 36, "Please make 36 more space in your inventory.")]
    public async Task SourceSpaceGateAndExactMessagePrecedeCatalogAndPersistence(
        byte optionId,
        int requiredSpaces,
        string expectedMessage)
    {
        var original = ItemsWithDummyInventory(40 - requiredSpaces + 1);
        var result = await _fixture.InvokeAsync(
            optionId,
            original,
            worldData: null,
            includeSessions: false,
            includeUsersRoot: false,
            includePlayerItemsRoot: false,
            observerCount: 1);

        Assert.True(result.Handled);
        LegacyFullAccountItemMutationFixture.AssertPlayerItemsEqual(original, result.UpdatedSession.PlayerItems);
        LegacyFullAccountItemMutationFixture.AssertPlayerItemsEqual(original, result.PersistedItems);
        Assert.Null(result.PersistedAtCommit);
        var frame = Assert.Single(result.ClaimantSent);
        Assert.True(LegacyNpcReplyPacket.TryParse(
            LegacyFullAccountItemMutationFixture.ReadFrame(frame),
            out var reply));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, reply.InteractType);
        Assert.Equal(expectedMessage, reply.Text);
        Assert.All(result.ObserverSent, Assert.Empty);
        Assert.Contains($"requiredSpaces={requiredSpaces}", result.Log, StringComparison.Ordinal);
        Assert.Contains("source bag check precedes catalog and persistence", result.Log, StringComparison.Ordinal);
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(13, 402U, 27U, 23, 18, 22, 1)]
    [InlineData(14, 403U, 9U, 41, 0, 40, 1)]
    public async Task SourceLoopUsesAvailableCapacityWhenNewCatalogExceedsOldSpaceGate(
        byte optionId,
        uint family,
        uint maxLevel,
        int catalogCount,
        int initialItemCount,
        int expectedGranted,
        int expectedSkipped)
    {
        var itemTypes = Enumerable.Range(1, catalogCount)
            .Select(index => RuneType(
                checked((family * 10_000) + ((uint)index * 100) + maxLevel),
                durability: 100))
            .ToArray();
        var original = ItemsWithDummyInventory(initialItemCount);
        var result = await _fixture.InvokeAsync(
            optionId,
            original,
            LegacyItemWorldDataFixture.Create(itemTypes),
            observerCount: 1);

        Assert.True(result.Handled);
        Assert.Equal(40, result.UpdatedSession.PlayerItems.InventoryItems.Count);
        Assert.Equal(expectedGranted, result.ClaimantSent.Count);
        Assert.Equal(
            itemTypes.Take(expectedGranted).Select(item => checked((uint)item.Id)),
            result.ClaimantSent.Select(ParseGameItem).Select(packet => packet.ItemId));
        Assert.Equal(
            result.UpdatedSession.PlayerItems.InventoryItems.Select(item => item.ItemId),
            result.PersistedItems.InventoryItems.Select(item => item.ItemId));
        Assert.All(result.ObserverSent, Assert.Empty);
        Assert.Contains($"catalogMatches={catalogCount} granted={expectedGranted}", result.Log, StringComparison.Ordinal);
        Assert.Contains($"capacitySkipped={expectedSkipped}", result.Log, StringComparison.Ordinal);
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("world-data")]
    [InlineData("sessions")]
    [InlineData("users-root")]
    [InlineData("items-root")]
    public async Task MissingEligibleMutationInfrastructureFailsClosed(string missing)
    {
        var original = LegacyFullAccountItemMutationFixture.Items();
        var worldData = LegacyItemWorldDataFixture.Create(RuneType(4010001, durability: 100));
        var result = await _fixture.InvokeAsync(
            optionId: 12,
            original,
            missing == "world-data" ? null : worldData,
            includeSessions: missing != "sessions",
            includeUsersRoot: missing != "users-root",
            includePlayerItemsRoot: missing != "items-root",
            observerCount: 1);

        AssertUnchanged(result, original);
        var expectedReason = missing switch
        {
            "world-data" => "world-data-missing",
            "sessions" => "session-store-missing",
            "users-root" => "usersRootMissing",
            _ => "playerItemsRootMissing",
        };
        Assert.Contains(expectedReason, result.Log, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(LegacyFullAccountItemMutationFixture.PersistenceLock.Character)]
    [InlineData(LegacyFullAccountItemMutationFixture.PersistenceLock.Items)]
    public async Task ForcedPersistenceFailureRollsBackWholeRuneGrant(
        LegacyFullAccountItemMutationFixture.PersistenceLock persistenceLock)
    {
        var original = LegacyFullAccountItemMutationFixture.Items();
        var result = await _fixture.InvokeAsync(
            optionId: 12,
            original,
            LegacyItemWorldDataFixture.Create(RuneType(4010001, durability: 100)),
            observerCount: 1,
            persistenceLock: persistenceLock);

        AssertUnchanged(result, original);
        Assert.Contains("transactionWriteFailed", result.Log, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFiles(result.UsersRoot, "*.reward-points-*"));
        Assert.Empty(Directory.EnumerateFiles(result.PlayerItemsRoot, "*.reward-points-*"));
    }

    [Fact]
    public async Task ExhaustedItemUidFailsClosed()
    {
        var original = LegacyFullAccountItemMutationFixture.Items(
            LegacyFullAccountItemMutationFixture.InventoryItem(uint.MaxValue, 1000));
        var result = await _fixture.InvokeAsync(
            optionId: 12,
            original,
            LegacyItemWorldDataFixture.Create(RuneType(4010001, durability: 100)),
            observerCount: 1);

        AssertUnchanged(result, original);
        Assert.Contains("item-uid-or-field-exhausted", result.Log, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(4010002U, 100U, "matching-rune-catalog-empty")]
    [InlineData(4010001U, 65536U, "itemtype-invalid")]
    public async Task InvalidOrNonMatchingCatalogFailsClosed(
        uint itemId,
        uint durability,
        string expectedReason)
    {
        var original = LegacyFullAccountItemMutationFixture.Items();
        var result = await _fixture.InvokeAsync(
            optionId: 12,
            original,
            LegacyItemWorldDataFixture.Create(RuneType(itemId, durability)),
            observerCount: 1);

        AssertUnchanged(result, original);
        Assert.Contains(expectedReason, result.Log, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }

    private static ItemType RuneType(uint itemId, uint durability)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemId.ToString(CultureInfo.InvariantCulture);
        fields[1] = "Rune" + itemId.ToString(CultureInfo.InvariantCulture);
        fields[ItemType.DurabilityFieldIndex] = durability.ToString(CultureInfo.InvariantCulture);
        fields[ItemType.RuneSpellIdFieldIndex] = itemId.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)itemId), fields[1], fields);
    }

    private static PlayerItemSnapshot ItemsWithDummyInventory(int count)
    {
        var items = Enumerable.Range(0, count)
            .Select(index => LegacyFullAccountItemMutationFixture.InventoryItem(
                checked((uint)(10_000 + index)),
                checked((uint)(1_000 + index))))
            .ToArray();
        return LegacyFullAccountItemMutationFixture.Items(items);
    }

    private static LegacyGameItemPacket ParseGameItem(byte[] bytes)
    {
        Assert.True(LegacyGameItemPacket.TryParse(
            LegacyFullAccountItemMutationFixture.ReadFrame(bytes),
            out var packet));
        return packet;
    }

    private static void AssertUnchanged(
        LegacyFullAccountItemMutationFixture.InvokeResult result,
        PlayerItemSnapshot original)
    {
        Assert.True(result.Handled);
        LegacyFullAccountItemMutationFixture.AssertPlayerItemsEqual(original, result.UpdatedSession.PlayerItems);
        LegacyFullAccountItemMutationFixture.AssertPlayerItemsEqual(original, result.PersistedItems);
        Assert.Null(result.PersistedAtCommit);
        Assert.Empty(result.ClaimantSent);
        Assert.All(result.ObserverSent, Assert.Empty);
        Assert.Empty(result.ExposureOrder);
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
        Assert.Equal(result.OriginalItemBytes, File.ReadAllBytes(result.PlayerItemsPath));
        Assert.Contains("PARITY-GAP", result.Log, StringComparison.Ordinal);
    }
}
