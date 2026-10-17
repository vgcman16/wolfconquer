using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.LegacyFullAccountItemMutationFixture;

namespace WolfConquer.Tests;

public sealed class LegacyFullAccountRedBodhiBeanMutationTests : IDisposable
{
    private const byte OptionId = 16;
    private const uint RewardItemId = 3_009_104;
    private const ushort StackLimit = 10_000;

    private readonly Fixture _fixture = new("red-bodhi-bean", accountId: 1_293_850);

    [Fact]
    public async Task EmptyRewardStackPersistsThenSendsOneAddAndNineUpdates()
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
        AssertCreatedRewardStack(reward, StackLimit);

        Assert.NotNull(result.PersistedAtCommit);
        var committedReward = Assert.Single(
            result.PersistedAtCommit!.Items.InventoryItems,
            item => item.ItemId == RewardItemId);
        AssertCreatedRewardStack(committedReward, StackLimit);
        Fixture.AssertPlayerItemsEqual(result.UpdatedSession.PlayerItems, result.PersistedItems);
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
        Assert.Equal("commit", result.ExposureOrder[0]);
        Assert.Equal(11, result.ExposureOrder.Count);

        var packets = ParseItemPackets(result.ClaimantSent);
        Assert.Equal(10, packets.Count);
        Assert.All(packets, packet => Assert.Equal(reward.Uid, packet.Uid));
        Assert.Equal(
            [
                LegacyGameItemPacket.AddItemMode,
                LegacyGameItemPacket.UpdateItemMode,
                LegacyGameItemPacket.UpdateItemMode,
                LegacyGameItemPacket.UpdateItemMode,
                LegacyGameItemPacket.UpdateItemMode,
                LegacyGameItemPacket.UpdateItemMode,
                LegacyGameItemPacket.UpdateItemMode,
                LegacyGameItemPacket.UpdateItemMode,
                LegacyGameItemPacket.UpdateItemMode,
                LegacyGameItemPacket.UpdateItemMode,
            ],
            packets.Select(packet => packet.Mode).ToArray());
        Assert.Equal(
            Enumerable.Range(1, 10).Select(index => checked((ushort)(index * 1_000))).ToArray(),
            packets.Select(packet => packet.StackSize).ToArray());
        Assert.All(result.ObserverSent, Assert.Empty);
        Assert.DoesNotContain(
            result.ClaimantSent,
            bytes => LegacyNpcReplyPacket.TryParse(Fixture.ReadFrame(bytes), out _));
        Assert.DoesNotContain(
            result.ClaimantSent,
            bytes => LegacyMsgUpdatePacket.TryParse(Fixture.ReadFrame(bytes), out _));
        Assert.Contains("sourceCalls=10", result.Log, StringComparison.Ordinal);
        Assert.Contains("amountPerCall=1000 totalAmount=10000", result.Log, StringComparison.Ordinal);
        Assert.Contains("packetOrder=GameItemAddOrUpdatex10", result.Log, StringComparison.Ordinal);
        Assert.DoesNotContain("PARITY-GAP", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExistingCompatibleStackFillsThenNewStackCarriesRemainderInSourceCallOrder()
    {
        var originalItems = Fixture.Items(
            Fixture.InventoryItem(41, 400_041),
            Fixture.InventoryItem(
                42,
                RewardItemId,
                stackSize: 6_000,
                locked: 1,
                color: 7));
        var result = await _fixture.InvokeAsync(
            OptionId,
            originalItems,
            LegacyItemWorldDataFixture.Create(ItemType()));

        var stacks = result.UpdatedSession.PlayerItems.InventoryItems
            .Where(item => item.ItemId == RewardItemId)
            .OrderBy(item => item.Uid)
            .ToArray();
        Assert.Equal(2, stacks.Length);
        Assert.Equal(42U, stacks[0].Uid);
        Assert.Equal(StackLimit, stacks[0].StackSize);
        Assert.Equal((byte)0, stacks[0].Locked);
        Assert.Equal(7U, stacks[0].Color);
        Assert.Equal(43U, stacks[1].Uid);
        AssertCreatedRewardStack(stacks[1], expectedStackSize: 6_000);
        Fixture.AssertPlayerItemsEqual(result.UpdatedSession.PlayerItems, result.PersistedItems);

        var packets = ParseItemPackets(result.ClaimantSent);
        Assert.Equal(
            [42U, 42U, 42U, 42U, 43U, 43U, 43U, 43U, 43U, 43U],
            packets.Select(packet => packet.Uid).ToArray());
        Assert.Equal(
            [
                LegacyGameItemPacket.UpdateItemMode,
                LegacyGameItemPacket.UpdateItemMode,
                LegacyGameItemPacket.UpdateItemMode,
                LegacyGameItemPacket.UpdateItemMode,
                LegacyGameItemPacket.AddItemMode,
                LegacyGameItemPacket.UpdateItemMode,
                LegacyGameItemPacket.UpdateItemMode,
                LegacyGameItemPacket.UpdateItemMode,
                LegacyGameItemPacket.UpdateItemMode,
                LegacyGameItemPacket.UpdateItemMode,
            ],
            packets.Select(packet => packet.Mode).ToArray());
        Assert.Equal(
            [
                (ushort)7_000,
                (ushort)8_000,
                (ushort)9_000,
                (ushort)10_000,
                (ushort)1_000,
                (ushort)2_000,
                (ushort)3_000,
                (ushort)4_000,
                (ushort)5_000,
                (ushort)6_000,
            ],
            packets.Select(packet => packet.StackSize).ToArray());
        Assert.All(packets, packet => Assert.Equal((byte)0, packet.Locked));
    }

    [Fact]
    public async Task TwentySpaceRequirementWinsEvenWhenACompatibleStackExists()
    {
        var inventory = Enumerable.Range(1, 21)
            .Select(index => Fixture.InventoryItem(
                checked((uint)index),
                index == 1 ? RewardItemId : checked((uint)(400_000 + index)),
                stackSize: index == 1 ? (ushort)0 : (ushort)1))
            .ToArray();
        var result = await _fixture.InvokeAsync(
            OptionId,
            Fixture.Items(inventory),
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
        Assert.Equal("Please make 20 more space in your inventory.", reply.Text);
        Assert.All(result.ObserverSent, Assert.Empty);
        Assert.Contains("reason=inventory-needs-20-spaces", result.Log, StringComparison.Ordinal);
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
    public async Task StackLimitBelowSourceChunkFailsClosed()
    {
        var result = await _fixture.InvokeAsync(
            OptionId,
            Fixture.Items(Fixture.InventoryItem(41, 400_041)),
            LegacyItemWorldDataFixture.Create(ItemType(stackLimit: 999)));

        AssertUnchanged(result, "itemtype-invalid");
    }

    [Fact]
    public async Task ItemUidExhaustionFailsClosedWhenANewStackIsRequired()
    {
        var result = await _fixture.InvokeAsync(
            OptionId,
            Fixture.Items(Fixture.InventoryItem(uint.MaxValue, 400_041)),
            LegacyItemWorldDataFixture.Create(ItemType()));

        AssertUnchanged(result, "item-uid-exhausted");
    }

    [Fact]
    public async Task CompatibleStackDoesNotNeedANewUidEvenWhenAnotherItemUsesUInt32Maximum()
    {
        var result = await _fixture.InvokeAsync(
            OptionId,
            Fixture.Items(
                Fixture.InventoryItem(uint.MaxValue, 400_041),
                Fixture.InventoryItem(5, RewardItemId, stackSize: 0, locked: 1)),
            LegacyItemWorldDataFixture.Create(ItemType()));

        var reward = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == RewardItemId);
        Assert.Equal(5U, reward.Uid);
        Assert.Equal(StackLimit, reward.StackSize);
        Assert.Equal((byte)0, reward.Locked);
        Assert.Equal(10, result.ClaimantSent.Count);
        Assert.All(
            ParseItemPackets(result.ClaimantSent),
            packet => Assert.Equal(LegacyGameItemPacket.UpdateItemMode, packet.Mode));
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

    private static ItemType ItemType(ushort stackLimit = StackLimit)
    {
        return Fixture.ItemType(
            RewardItemId,
            "RedBodhiBean",
            durability: 0,
            stackLimit);
    }

    private static IReadOnlyList<LegacyGameItemPacket> ParseItemPackets(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyGameItemPacket>();
        foreach (var bytes in sent)
        {
            var frame = Fixture.ReadFrame(bytes);
            Assert.Equal((ushort)2_116, frame.Header.Opcode);
            Assert.True(LegacyGameItemPacket.TryParse(frame, out var packet));
            Assert.Equal(RewardItemId, packet.ItemId);
            Assert.Equal((byte)0, packet.Bound);
            packets.Add(packet);
        }

        return packets;
    }

    private static void AssertCreatedRewardStack(
        PlayerItemSnapshotItem item,
        ushort expectedStackSize)
    {
        Assert.Equal(RewardItemId, item.ItemId);
        Assert.Equal((ushort)0, item.Durability);
        Assert.Equal((ushort)0, item.MaximumDurability);
        Assert.Equal((ushort)0, item.Position);
        Assert.Equal(expectedStackSize, item.StackSize);
        Assert.Equal((byte)0, item.Bound);
        Assert.Equal((byte)0, item.Plus);
        Assert.Equal((byte)0, item.Bless);
        Assert.Equal((byte)0, item.Enchant);
        Assert.Equal((byte)0, item.Locked);
        Assert.Equal((byte)0, item.SocketOne);
        Assert.Equal((byte)0, item.SocketTwo);
        Assert.Equal(0U, item.SocketProgress);
        Assert.Equal(0U, item.Effect);
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
