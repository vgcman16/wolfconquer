using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests : IDisposable
{
    private const int LegacyShieldBlockFlag = 0x71;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "wolf-non-equipment-item-use-" + Guid.NewGuid());
    private readonly string _usersRoot;
    private readonly string _itemsRoot;

    public MsgItemUsageNonEquipmentItemUseTests()
    {
        _usersRoot = Path.Combine(_root, "Users");
        _itemsRoot = Path.Combine(_root, "PlayersItems");
        Directory.CreateDirectory(_usersRoot);
        Directory.CreateDirectory(_itemsRoot);
    }

    [Fact]
    public async Task HandlerItemUsageBirdIslandGateConsumesItemAndTeleports()
    {
        const uint itemUid = 5_322_992;
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_002, x: 430, y: 380);
        WriteItems(1_293_600, PlayerItemSnapshotLoaderTests.DbItem(itemUid, 1_060_024, position: 0));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(connection, ReadFrame(CreateEquipUse(itemUid).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 6, sent.Count);
        AssertItemUsage(
            sent[countBeforeRequest],
            new MsgItemUsagePacket(itemUid, DwParam: 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));
        AssertLegacyTeleportTransition(sent, countBeforeRequest + 1, 1_015, 717, 571);

        var session = GetSession(handler, connection);
        Assert.Empty(session.PlayerItems.InventoryItems);
        Assert.Equal(1_015, session.Character.MapId);
        Assert.Equal(717, session.Character.X);
        Assert.Equal(571, session.Character.Y);
        Assert.Equal(1_002, session.Character.PreviousMapId);

        var output = log.ToString();
        Assert.Contains("handled MsgItemUsage teleport item", output);
        Assert.Contains("itemId=1060024", output);
        Assert.DoesNotContain("non-equipment item effects are not modeled", output);
    }

    [Theory]
    [InlineData(5_338_000U, 1_060_020U, 1_002, 410, 354, 1)]
    [InlineData(5_338_001U, 1_060_021U, 1_000, 500, 650, 1)]
    [InlineData(5_338_002U, 1_060_025U, 1_002, 405, 655, 2)]
    public async Task HandlerItemUsageTeleportScrollConsumesAndTeleportsToMappedLocation(
        uint itemUid,
        uint itemId,
        long expectedMapId,
        long expectedX,
        long expectedY,
        int expectedTeleportCount)
    {
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        WriteItems(1_293_600, PlayerItemSnapshotLoaderTests.DbItem(itemUid, itemId, position: 0));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(connection, ReadFrame(CreateEquipUse(itemUid).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 1 + (expectedTeleportCount * 5), sent.Count);
        AssertItemUsage(
            sent[countBeforeRequest],
            new MsgItemUsagePacket(itemUid, DwParam: 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));
        for (var index = 0; index < expectedTeleportCount; index++)
        {
            Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[countBeforeRequest + 1 + (index * 5)]), out var teleport));
            Assert.Equal(MsgActionType.Teleport, teleport.Type);
        }

        var finalTransitionStart = countBeforeRequest + 1 + ((expectedTeleportCount - 1) * 5);
        AssertLegacyTeleportTransition(sent, finalTransitionStart, expectedMapId, expectedX, expectedY);

        var session = GetSession(handler, connection);
        Assert.Equal(expectedMapId, session.Character.MapId);
        Assert.Equal(expectedX, session.Character.X);
        Assert.Equal(expectedY, session.Character.Y);

        var output = log.ToString();
        Assert.Contains("handled MsgItemUsage teleport item", output);
        Assert.Contains($"itemId={itemId}", output);
        Assert.Contains($"count={expectedTeleportCount}", output);
        Assert.DoesNotContain("non-equipment item effects are not modeled", output);
    }

    [Fact]
    public async Task HandlerItemUsageTeleportScrollBlockedByTeleportRestrictionConsumesWithoutTeleport()
    {
        const uint itemUid = 5_338_100U;
        const uint itemId = 1_060_020U;
        WriteSnapshot(
            accountId: 1_293_600,
            uid: 1_293_600,
            mapId: 700,
            x: 11,
            y: 12);
        WriteItems(1_293_600, PlayerItemSnapshotLoaderTests.DbItem(itemUid, itemId, position: 0));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(connection, ReadFrame(CreateEquipUse(itemUid).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 1, sent.Count);
        AssertItemUsage(
            sent[countBeforeRequest],
            new MsgItemUsagePacket(itemUid, DwParam: 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));

        var session = GetSession(handler, connection);
        Assert.Equal(700, session.Character.MapId);
        Assert.Equal(11, session.Character.X);
        Assert.Equal(12, session.Character.Y);

        var output = log.ToString();
        Assert.Contains("handled MsgItemUsage teleport item consume only", output);
        Assert.Contains("legacy BlockTeleportMap blocks destination 1002 (410,354)", output);
        Assert.Contains($"itemId={itemId}", output);
        Assert.DoesNotContain("non-equipment item effects are not modeled", output);
    }

    [Fact]
    public async Task HandlerItemUsageArroyoScrollConsumesOnlyWithoutGenericDeferral()
    {
        const uint itemUid = 5_322_987;
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        WriteItems(1_293_600, PlayerItemSnapshotLoaderTests.DbItem(itemUid, 1_060_039, position: 0));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(connection, ReadFrame(CreateEquipUse(itemUid).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 1, sent.Count);
        AssertItemUsage(
            sent[countBeforeRequest],
            new MsgItemUsagePacket(itemUid, DwParam: 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));

        var session = GetSession(handler, connection);
        Assert.Empty(session.PlayerItems.InventoryItems);
        Assert.Equal(1_015, session.Character.MapId);
        Assert.Equal(826, session.Character.X);
        Assert.Equal(565, session.Character.Y);

        var output = log.ToString();
        Assert.Contains("handled MsgItemUsage consume-only item", output);
        Assert.Contains("family=ArroyoScroll", output);
        Assert.Contains("itemId=1060039", output);
        Assert.DoesNotContain("non-equipment item effects are not modeled", output);
    }

    [Fact]
    public async Task HandlerItemUsageDailySignInPackConsumesAndGrantsSourceRewardWithoutParityGap()
    {
        const uint itemUid = 5_335_980;
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        WriteItems(1_293_600, PlayerItemSnapshotLoaderTests.DbItem(itemUid, 3_100_011, position: 0));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(connection, ReadFrame(CreateEquipUse(itemUid).ToFrameBytes()));

        Assert.True(sent.Count >= countBeforeRequest + 2);
        AssertItemUsage(
            sent[countBeforeRequest],
            new MsgItemUsagePacket(itemUid, DwParam: 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(sent[^1]), out var rewardMessage));

        var session = GetSession(handler, connection);
        Assert.DoesNotContain(session.PlayerItems.InventoryItems, item => item.Uid == itemUid);
        var hasItemReward = session.PlayerItems.InventoryItems.Any(item =>
            item.ItemId == 3_600_023 &&
            item.Bound == 1);
        var chiPoints = LegacyChiState.FromSnapshot(session.Character).ChiPoints;
        var subclassState = session.Character.Attributes.TryGetValue("SubProfInfo", out var rawSubProfInfo) &&
            LegacySubClassHitpointsCalculator.TryParseLegacyState(rawSubProfInfo, out var parsedSubClass)
                ? parsedSubClass
                : new LegacySubClassState(StudyPoints: 0, ActiveSubClass: 0, Entries: []);
        Assert.True(
            hasItemReward || chiPoints == 200 || subclassState.StudyPoints == 300,
            "Expected one legacy daily sign-in reward: bound item 3600023, 200 ChiPoints, or 300 StudyPoints.");
        Assert.Contains(
            rewardMessage.Content,
            content => content is "You received EminentExploitPack." or "You received 200 ChiPoints." or "You received 300 StudyPoints.");

        if (hasItemReward)
        {
            Assert.Contains(sent.Skip(countBeforeRequest + 1), bytes =>
                LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.ItemId == 3_600_023 &&
                packet.Bound == 1);
        }
        else if (chiPoints == 200)
        {
            Assert.Contains(sent.Skip(countBeforeRequest + 1), bytes =>
                LegacyChiInfoPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.ChiPoints == 200);
        }

        var output = log.ToString();
        Assert.Contains("handled MsgItemUsage daily sign-in pack", output);
        Assert.Contains("itemId=3100011", output);
        Assert.Contains("source roll=", output);
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage daily sign-in pack", output);
        Assert.DoesNotContain("non-equipment item effects are not modeled", output);
    }

    [Fact]
    public async Task HandlerItemUsageDailySignInPackFullInventoryPreservesPack()
    {
        const uint itemUid = 5_335_990;
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        var items = Enumerable.Range(0, 39)
            .Select(index => PlayerItemSnapshotLoaderTests.DbItem(
                checked((uint)(5_400_000 + index)),
                checked((uint)(1_000_000 + index)),
                position: 0))
            .Prepend(PlayerItemSnapshotLoaderTests.DbItem(itemUid, 3_100_011, position: 0))
            .ToArray();
        WriteItems(1_293_600, items);
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(connection, ReadFrame(CreateEquipUse(itemUid).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 1, sent.Count);
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(sent[countBeforeRequest]), out var message));
        Assert.Contains("Please make 1 more space in inventory.", message.Content);

        var session = GetSession(handler, connection);
        Assert.Equal(40, session.PlayerItems.InventoryItems.Count);
        Assert.Contains(session.PlayerItems.InventoryItems, item => item.Uid == itemUid && item.ItemId == 3_100_011);

        var output = log.ToString();
        Assert.Contains("skipped MsgItemUsage daily sign-in pack", output);
        Assert.Contains("inventory is full", output);
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage daily sign-in pack", output);
        Assert.DoesNotContain("non-equipment item effects are not modeled", output);
    }

    [Fact]
    public async Task HandlerItemUsageHouseMaterialPackConsumesIncrementsCounterAndGrantsClassSixPack()
    {
        const uint itemUid = 5_336_100;
        WriteSnapshot(
            accountId: 1_293_600,
            uid: 1_293_600,
            mapId: 1_015,
            x: 826,
            y: 565,
            openHousePack: 2);
        WriteItems(1_293_600, PlayerItemSnapshotLoaderTests.DbItem(itemUid, 3_008_101, position: 0));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(connection, ReadFrame(CreateEquipUse(itemUid).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 3, sent.Count);
        AssertItemUsage(
            sent[countBeforeRequest],
            new MsgItemUsagePacket(itemUid, DwParam: 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest + 1]), out var rewardItem));
        Assert.Equal(3_008_100U, rewardItem.ItemId);
        Assert.Equal(1, rewardItem.StackSize);
        Assert.Equal(0, rewardItem.Bound);
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(sent[countBeforeRequest + 2]), out var message));
        Assert.Contains("You received a Class 6 House Pack!", message.Content);

        var session = GetSession(handler, connection);
        Assert.DoesNotContain(session.PlayerItems.InventoryItems, item => item.Uid == itemUid);
        Assert.Contains(session.PlayerItems.InventoryItems, item => item.ItemId == 3_008_100);
        Assert.Equal("3", session.Character.Attributes["OpenHousePack"]);

        var output = log.ToString();
        Assert.Contains("handled MsgItemUsage house material pack", output);
        Assert.Contains("itemId=3008101", output);
        Assert.Contains("OpenHousePack=2->3", output);
        Assert.DoesNotContain("non-equipment item effects are not modeled", output);
    }

    [Fact]
    public async Task HandlerItemUsageHouseMaterialPackDailyCapPreservesPack()
    {
        const uint itemUid = 5_336_101;
        WriteSnapshot(
            accountId: 1_293_600,
            uid: 1_293_600,
            mapId: 1_015,
            x: 826,
            y: 565,
            openHousePack: 3);
        WriteItems(1_293_600, PlayerItemSnapshotLoaderTests.DbItem(itemUid, 3_008_101, position: 0));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(connection, ReadFrame(CreateEquipUse(itemUid).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 1, sent.Count);
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(sent[countBeforeRequest]), out var message));
        Assert.Contains(
            "You've already opened such a pack for 3 times today. Please retry tomorrow.",
            message.Content);

        var session = GetSession(handler, connection);
        Assert.Contains(session.PlayerItems.InventoryItems, item => item.Uid == itemUid && item.ItemId == 3_008_101);
        Assert.DoesNotContain(session.PlayerItems.InventoryItems, item => item.ItemId == 3_008_100);
        Assert.Equal("3", session.Character.Attributes["OpenHousePack"]);

        var output = log.ToString();
        Assert.Contains("skipped MsgItemUsage house material pack", output);
        Assert.Contains("daily OpenHousePack cap reached", output);
        Assert.DoesNotContain("non-equipment item effects are not modeled", output);
    }

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task HandlerItemUsageExpBallConsumesAndAwardsSourceExperienceWithoutGenericDeferral()
    {
        const uint itemUid = 5_337_000;
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        if (!File.Exists(Path.Combine(seedPath, "manifest.json")))
        {
            return;
        }

        var worldData = await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync();
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565, level: 139);
        WriteItems(1_293_600, PlayerItemSnapshotLoaderTests.DbItem(itemUid, 723_700, position: 0));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, worldData);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(connection, ReadFrame(CreateEquipUse(itemUid).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 2, sent.Count);
        AssertItemUsage(
            sent[countBeforeRequest],
            new MsgItemUsagePacket(itemUid, DwParam: 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[countBeforeRequest + 1]), out var experienceUpdate));
        Assert.Equal(1_293_600U, experienceUpdate.Uid);
        var entry = Assert.Single(experienceUpdate.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.Experience, entry.Type);
        Assert.Equal(403_774_615UL, entry.Value);

        var session = GetSession(handler, connection);
        Assert.Empty(session.PlayerItems.InventoryItems);
        Assert.Equal(139, session.Character.Level);
        Assert.Equal(403_774_615, session.Character.Experience);
        Assert.Equal(1, session.Character.ExpBallUsed);

        var output = log.ToString();
        Assert.Contains("handled MsgItemUsage exp ball item", output);
        Assert.Contains("itemId=723700", output);
        Assert.Contains("ExpBallUsed=0->1", output);
        Assert.DoesNotContain("daily counter is not persisted yet", output);
        Assert.DoesNotContain("non-equipment item effects are not modeled", output);
    }

    [Fact]
    public async Task HandlerItemUsageExpBallDailyCapSendsMessageWithoutConsuming()
    {
        const uint itemUid = 5_337_010;
        WriteSnapshot(
            accountId: 1_293_600,
            uid: 1_293_600,
            mapId: 1_015,
            x: 826,
            y: 565,
            level: 139,
            expBallUsed: 10);
        WriteItems(1_293_600, PlayerItemSnapshotLoaderTests.DbItem(itemUid, 723_700, position: 0));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(connection, ReadFrame(CreateEquipUse(itemUid).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 1, sent.Count);
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(sent[countBeforeRequest]), out var message));
        Assert.Contains("You can use only ten exp balls a day. Try tomorrow.", message.Content);

        var session = GetSession(handler, connection);
        Assert.Contains(session.PlayerItems.InventoryItems, item => item.Uid == itemUid && item.ItemId == 723_700);
        Assert.Equal(10, session.Character.ExpBallUsed);
        Assert.Equal(0, session.Character.Experience);

        var output = log.ToString();
        Assert.Contains("daily ExpBallUsed cap reached", output);
        Assert.Contains("used=10", output);
        Assert.DoesNotContain("non-equipment item effects are not modeled", output);
    }

    [Fact]
    public async Task HandlerItemUsageBoundYellowRuneFragmentConsumesTwentyAndPersistsFallbackRune()
    {
        const uint fragmentUid = 5_337_100;
        const uint fragmentItemId = 3_306_370;
        const uint selectedBlacklistedRuneItemId = 4_032_101;
        const uint expectedFallbackRuneItemId = 4_032_001;
        WriteSnapshot(
            accountId: 1_293_600,
            uid: 1_293_600,
            mapId: 1_015,
            x: 826,
            y: 565,
            legacyRuneFragmentCompleteRuneIndex: 1);
        WriteItems(
            1_293_600,
            PlayerItemSnapshotLoaderTests.DbItem(
                fragmentUid,
                fragmentItemId,
                position: 0,
                stackSize: 20,
                bound: 1));
        var worldData = CreateWorldDataCatalog(
            CreateItemType(fragmentItemId, "YellowRuneFragment(B)", durability: 1),
            CreateItemType(expectedFallbackRuneItemId, "YellowRuneFallback", durability: 700),
            CreateItemType(selectedBlacklistedRuneItemId, "YellowRuneBlacklisted", durability: 888),
            CreateItemType(4_032_002, "YellowRuneWrongEnding", durability: 999),
            CreateItemType(4_022_001, "BlueRune", durability: 999));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, worldData);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(connection, ReadFrame(CreateEquipUse(fragmentUid).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 22, sent.Count);
        for (var debitIndex = 0; debitIndex < 19; debitIndex++)
        {
            Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest + debitIndex]), out var debit));
            Assert.Equal(fragmentUid, debit.Uid);
            Assert.Equal(fragmentItemId, debit.ItemId);
            Assert.Equal((ushort)(19 - debitIndex), debit.StackSize);
            Assert.Equal(LegacyGameItemPacket.UpdateItemMode, debit.Mode);
        }

        AssertItemUsage(
            sent[countBeforeRequest + 19],
            new MsgItemUsagePacket(fragmentUid, DwParam: 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest + 20]), out var rewardPacket));
        Assert.Equal(expectedFallbackRuneItemId, rewardPacket.ItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, rewardPacket.Mode);
        Assert.Equal((ushort)700, rewardPacket.Durability);
        Assert.Equal((ushort)700, rewardPacket.MaximumDurability);
        Assert.Equal((byte)1, rewardPacket.Bound);
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(sent[countBeforeRequest + 21]), out var message));
        Assert.Contains("You have got 1 YellowRuneFallback(B)!.", message.Content);

        var session = GetSession(handler, connection);
        var liveRune = Assert.Single(session.PlayerItems.InventoryItems);
        Assert.Equal(expectedFallbackRuneItemId, liveRune.ItemId);
        Assert.Equal((byte)1, liveRune.Bound);
        Assert.DoesNotContain(session.PlayerItems.InventoryItems, item => item.ItemId == fragmentItemId);

        var persistedItems = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600);
        var persistedRune = Assert.Single(persistedItems.InventoryItems);
        Assert.Equal(expectedFallbackRuneItemId, persistedRune.ItemId);
        Assert.Equal((byte)1, persistedRune.Bound);
        Assert.Equal((ushort)700, persistedRune.Durability);
        Assert.DoesNotContain(persistedItems.InventoryItems, item => item.ItemId == fragmentItemId);

        var output = log.ToString();
        Assert.Contains("handled MsgItemUsage direct rune fragment use", output);
        Assert.Contains("itemId=3306370", output);
        Assert.Contains("selectedRewardItemId=4032101", output);
        Assert.Contains("rewardItemId=4032001", output);
        Assert.Contains("poolIndex=1", output);
        Assert.Contains("persisted MsgItemUsage direct rune fragment use items", output);
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage direct rune fragment use", output);
        Assert.DoesNotContain("non-equipment item effects are not modeled", output);
    }

    [Theory]
    [InlineData(5_337_110U, 3_306_366U, (byte)0, false, 4_012_001U, "RedRuneDirect", (ushort)610)]
    [InlineData(5_337_111U, 3_306_369U, (byte)1, true, 4_012_001U, "RedRuneDirect", (ushort)611)]
    [InlineData(5_337_112U, 3_306_367U, (byte)0, false, 4_032_001U, "YellowRuneDirect", (ushort)612)]
    [InlineData(5_337_113U, 3_306_368U, (byte)0, false, 4_022_001U, "BlueRuneDirect", (ushort)613)]
    [InlineData(5_337_114U, 3_306_371U, (byte)1, true, 4_022_001U, "BlueRuneDirect", (ushort)614)]
    public async Task HandlerItemUsageRuneFragmentVariantConsumesTwentyAndPersistsCompleteRune(
        uint fragmentUid,
        uint fragmentItemId,
        byte fragmentBound,
        bool expectedRewardBound,
        uint rewardItemId,
        string rewardName,
        ushort rewardDurability)
    {
        WriteSnapshot(
            accountId: 1_293_600,
            uid: 1_293_600,
            mapId: 1_015,
            x: 826,
            y: 565,
            legacyRuneFragmentCompleteRuneIndex: 0);
        WriteItems(
            1_293_600,
            PlayerItemSnapshotLoaderTests.DbItem(
                fragmentUid,
                fragmentItemId,
                position: 0,
                stackSize: 20,
                bound: fragmentBound));
        var worldData = CreateWorldDataCatalog(
            CreateItemType(fragmentItemId, "RuneFragment", durability: 1),
            CreateItemType(rewardItemId, rewardName, rewardDurability));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, worldData);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(connection, ReadFrame(CreateEquipUse(fragmentUid).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 22, sent.Count);
        for (var debitIndex = 0; debitIndex < 19; debitIndex++)
        {
            Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest + debitIndex]), out var debit));
            Assert.Equal(fragmentUid, debit.Uid);
            Assert.Equal(fragmentItemId, debit.ItemId);
            Assert.Equal((ushort)(19 - debitIndex), debit.StackSize);
            Assert.Equal(LegacyGameItemPacket.UpdateItemMode, debit.Mode);
        }

        AssertItemUsage(
            sent[countBeforeRequest + 19],
            new MsgItemUsagePacket(fragmentUid, DwParam: 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest + 20]), out var rewardPacket));
        Assert.Equal(rewardItemId, rewardPacket.ItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, rewardPacket.Mode);
        Assert.Equal(rewardDurability, rewardPacket.Durability);
        Assert.Equal(rewardDurability, rewardPacket.MaximumDurability);
        Assert.Equal(expectedRewardBound ? (byte)1 : (byte)0, rewardPacket.Bound);
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(sent[countBeforeRequest + 21]), out var message));
        Assert.Contains(
            "You have got 1 " + rewardName + (expectedRewardBound ? "(B)!." : "!."),
            message.Content);

        var session = GetSession(handler, connection);
        var liveRune = Assert.Single(session.PlayerItems.InventoryItems);
        Assert.Equal(rewardItemId, liveRune.ItemId);
        Assert.Equal(expectedRewardBound ? (byte)1 : (byte)0, liveRune.Bound);
        Assert.DoesNotContain(session.PlayerItems.InventoryItems, item => item.ItemId == fragmentItemId);

        var persistedItems = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600);
        var persistedRune = Assert.Single(persistedItems.InventoryItems);
        Assert.Equal(rewardItemId, persistedRune.ItemId);
        Assert.Equal(expectedRewardBound ? (byte)1 : (byte)0, persistedRune.Bound);
        Assert.Equal(rewardDurability, persistedRune.Durability);
        Assert.DoesNotContain(persistedItems.InventoryItems, item => item.ItemId == fragmentItemId);

        var output = log.ToString();
        Assert.Contains("handled MsgItemUsage direct rune fragment use", output);
        Assert.Contains("itemId=" + fragmentItemId.ToString(CultureInfo.InvariantCulture), output);
        Assert.Contains("rewardItemId=" + rewardItemId.ToString(CultureInfo.InvariantCulture), output);
        Assert.Contains("requiredBound=" + fragmentBound.ToString(CultureInfo.InvariantCulture), output);
        Assert.Contains("persisted MsgItemUsage direct rune fragment use items", output);
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage direct rune fragment use", output);
        Assert.DoesNotContain("non-equipment item effects are not modeled", output);
    }

    [Fact]
    public async Task HandlerItemUsageBoundRuneFragmentRequiresBoundStackBeforeConsuming()
    {
        const uint fragmentUid = 5_337_120;
        const uint fragmentItemId = 3_306_371;
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        WriteItems(
            1_293_600,
            PlayerItemSnapshotLoaderTests.DbItem(
                fragmentUid,
                fragmentItemId,
                position: 0,
                stackSize: 20,
                bound: 0));
        var worldData = CreateWorldDataCatalog(
            CreateItemType(fragmentItemId, "BlueRuneFragment(B)", durability: 1),
            CreateItemType(4_022_001, "BlueRuneDirect", durability: 700));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, worldData);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(connection, ReadFrame(CreateEquipUse(fragmentUid).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 1, sent.Count);
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(sent[countBeforeRequest]), out var message));
        Assert.Contains("You don't have 20 BlueRuneFragment(B)!.", message.Content);

        var session = GetSession(handler, connection);
        var liveFragment = Assert.Single(session.PlayerItems.InventoryItems);
        Assert.Equal(fragmentItemId, liveFragment.ItemId);
        Assert.Equal((ushort)20, liveFragment.StackSize);
        Assert.Equal((byte)0, liveFragment.Bound);

        var persistedFragment = Assert.Single(new PlayerItemSnapshotLoader(_itemsRoot)
            .LoadByCharacterUid(1_293_600)
            .InventoryItems);
        Assert.Equal(fragmentItemId, persistedFragment.ItemId);
        Assert.Equal((ushort)20, persistedFragment.StackSize);
        Assert.Equal((byte)0, persistedFragment.Bound);

        var output = log.ToString();
        Assert.Contains("skipped MsgItemUsage direct rune fragment use", output);
        Assert.Contains("requiredBound=1", output);
        Assert.DoesNotContain("handled MsgItemUsage direct rune fragment use", output);
        Assert.DoesNotContain("non-equipment item effects are not modeled", output);
    }

    [Fact]
    public async Task HandlerDecomposeRelicsConsumesSelectedRelicGrantsCrystalsAndPersists()
    {
        const uint relicUid = 5_337_200;
        const uint relicItemId = 4_100_001;
        const uint crystalItemId = 3_306_885;
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        WriteItems(
            1_293_600,
            PlayerItemSnapshotLoaderTests.DbItem(
                relicUid,
                relicItemId,
                position: 0,
                durability: 500,
                maximumDurability: 500));
        var worldData = CreateWorldDataCatalog(
            CreateItemType(crystalItemId, "RelicCrystal", durability: 1));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, worldData);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(connection, ReadFrame(CreateDecomposeRelicsUse(id: 0, relicUid)));

        // Deliberate native contract correction: REMOVE + nine independent ADDs + echo.
        Assert.Equal(countBeforeRequest + 11, sent.Count);
        AssertItemUsage(
            sent[countBeforeRequest],
            new MsgItemUsagePacket(relicUid, DwParam: 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));
        var packetUids = new HashSet<uint>();
        for (var index = 0; index < 9; index++)
        {
            Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest + 1 + index]), out var crystalPacket));
            Assert.Equal(crystalItemId, crystalPacket.ItemId);
            Assert.Equal((ushort)1, crystalPacket.StackSize);
            Assert.Equal(LegacyGameItemPacket.AddItemMode, crystalPacket.Mode);
            Assert.True(packetUids.Add(crystalPacket.Uid));
        }
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(sent[countBeforeRequest + 10]), out var echoPacket));
        Assert.Equal(new MsgItemUsagePacket(0, 0, MsgItemUsageAction.DecomposeRelics, 511_043_495, 1, 0, 0), echoPacket);
        Assert.True(PacketReader.TryRead(sent[countBeforeRequest + 10], out var echoReader));
        Assert.Equal(relicUid, echoReader.ReadUInt32(MsgItemUsagePacket.PayloadLength));

        var session = GetSession(handler, connection);
        Assert.Equal(9, session.PlayerItems.InventoryItems.Count);
        Assert.All(session.PlayerItems.InventoryItems, crystal =>
        {
            Assert.Equal(crystalItemId, crystal.ItemId);
            Assert.Equal((ushort)1, crystal.StackSize);
            Assert.Contains(crystal.Uid, packetUids);
        });
        Assert.DoesNotContain(session.PlayerItems.InventoryItems, item => item.Uid == relicUid);

        var persistedItems = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(session.PlayerItems),
            PlayerItemSnapshotLoader.ToLegacyBytes(persistedItems));
        Assert.Equal(9, persistedItems.InventoryItems.Count);
        Assert.All(persistedItems.InventoryItems, crystal =>
        {
            Assert.Equal(crystalItemId, crystal.ItemId);
            Assert.Equal((ushort)1, crystal.StackSize);
        });
        Assert.DoesNotContain(persistedItems.InventoryItems, item => item.Uid == relicUid);

        var output = log.ToString();
        Assert.Contains("handled MsgItemUsage decompose relics", output);
        Assert.Contains("rewardItemId=3306885", output);
        Assert.Contains("rewardStack=9", output);
        Assert.Contains("persisted MsgItemUsage decompose relics items", output);
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage decompose relics", output);
    }

    [Fact]
    public async Task HandlerMythsoulBagAddSwapsSameBaseItemEchoesRemovalAndPersists()
    {
        const uint inventoryUid = 5_337_300;
        const uint bagUid = 5_337_301;
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        WriteItems(
            1_293_600,
            PlayerItemSnapshotLoaderTests.DbItem(inventoryUid, 40_701_016, position: 0),
            PlayerItemSnapshotLoaderTests.DbItem(bagUid, 40_701_012, position: 217));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateMythsoulBagUse(
                inventoryUid,
                MsgItemUsageAction.MythForggingMythsoulBagAdd,
                8_888)));

        Assert.Equal(countBeforeRequest + 3, sent.Count);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest]), out var returnedBagItem));
        Assert.Equal(bagUid, returnedBagItem.Uid);
        Assert.Equal((byte)0, returnedBagItem.Position);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, returnedBagItem.Mode);
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(sent[countBeforeRequest + 1]), out var echoPacket));
        Assert.Equal(
            new MsgItemUsagePacket(
                bagUid,
                DwParam: 0,
                MsgItemUsageAction.MythForggingMythsoulBagRemove,
                511_043_495,
                0,
                0,
                0),
            echoPacket);
        Assert.True(PacketReader.TryRead(sent[countBeforeRequest + 1], out var echoReader));
        Assert.Equal(8_888U, echoReader.ReadUInt32(MsgItemUsagePacket.PayloadLength));
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest + 2]), out var storedInventoryItem));
        Assert.Equal(inventoryUid, storedInventoryItem.Uid);
        Assert.Equal((byte)217, storedInventoryItem.Position);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, storedInventoryItem.Mode);

        var session = GetSession(handler, connection);
        Assert.Contains(session.PlayerItems.InventoryItems, item => item.Uid == bagUid && item.Position == 0);
        Assert.DoesNotContain(session.PlayerItems.InventoryItems, item => item.Uid == inventoryUid);
        Assert.Contains(session.PlayerItems.MythsoulBagItems, item => item.Uid == inventoryUid && item.Position == 217);
        Assert.DoesNotContain(session.PlayerItems.MythsoulBagItems, item => item.Uid == bagUid);

        var persistedItems = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600);
        Assert.Contains(persistedItems.InventoryItems, item => item.Uid == bagUid && item.Position == 0);
        Assert.DoesNotContain(persistedItems.InventoryItems, item => item.Uid == inventoryUid);
        Assert.Contains(persistedItems.MythsoulBagItems, item => item.Uid == inventoryUid && item.Position == 217);
        Assert.DoesNotContain(persistedItems.MythsoulBagItems, item => item.Uid == bagUid);

        var output = log.ToString();
        Assert.Contains("handled MsgItemUsage mythsoul bag add", output);
        Assert.Contains("replacedItemUid=5337301", output);
        Assert.Contains("persisted MsgItemUsage mythsoul bag add items", output);
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage mythsoul bag add", output);
    }

    [Fact]
    public async Task HandlerMythsoulBagRemoveMovesBagItemToInventoryAndPersists()
    {
        const uint bagUid = 5_337_310;
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        WriteItems(
            1_293_600,
            PlayerItemSnapshotLoaderTests.DbItem(bagUid, 40_701_016, position: 217));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateMythsoulBagUse(bagUid, MsgItemUsageAction.MythForggingMythsoulBagRemove)));

        Assert.Equal(countBeforeRequest + 1, sent.Count);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest]), out var returnedItem));
        Assert.Equal(bagUid, returnedItem.Uid);
        Assert.Equal((byte)0, returnedItem.Position);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, returnedItem.Mode);

        var session = GetSession(handler, connection);
        Assert.Contains(session.PlayerItems.InventoryItems, item => item.Uid == bagUid && item.Position == 0);
        Assert.Empty(session.PlayerItems.MythsoulBagItems);

        var persistedItems = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600);
        Assert.Contains(persistedItems.InventoryItems, item => item.Uid == bagUid && item.Position == 0);
        Assert.Empty(persistedItems.MythsoulBagItems);

        var output = log.ToString();
        Assert.Contains("handled MsgItemUsage mythsoul bag remove", output);
        Assert.Contains("persisted MsgItemUsage mythsoul bag remove items", output);
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage mythsoul bag remove", output);
    }

    [Fact]
    public async Task HandlerMythsoulBagAddWithReplacementPreservesItemsWhenInventoryFull()
    {
        const uint inventoryUid = 5_337_320;
        const uint bagUid = 5_337_321;
        var fillerItems = Enumerable.Range(0, 39)
            .Select(index => PlayerItemSnapshotLoaderTests.DbItem(
                checked((uint)(5_337_400 + index)),
                checked((uint)(1_000_000 + index)),
                position: 0));
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        WriteItems(
            1_293_600,
            fillerItems
                .Prepend(PlayerItemSnapshotLoaderTests.DbItem(inventoryUid, 40_701_016, position: 0))
                .Append(PlayerItemSnapshotLoaderTests.DbItem(bagUid, 40_701_012, position: 217))
                .ToArray());
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateMythsoulBagUse(inventoryUid, MsgItemUsageAction.MythForggingMythsoulBagAdd)));

        Assert.Equal(countBeforeRequest, sent.Count);
        var session = GetSession(handler, connection);
        Assert.Equal(40, session.PlayerItems.InventoryItems.Count);
        Assert.Contains(session.PlayerItems.InventoryItems, item => item.Uid == inventoryUid && item.Position == 0);
        Assert.Contains(session.PlayerItems.MythsoulBagItems, item => item.Uid == bagUid && item.Position == 217);

        var persistedItems = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600);
        Assert.Equal(40, persistedItems.InventoryItems.Count);
        Assert.Contains(persistedItems.InventoryItems, item => item.Uid == inventoryUid && item.Position == 0);
        Assert.Contains(persistedItems.MythsoulBagItems, item => item.Uid == bagUid && item.Position == 217);

        var output = log.ToString();
        Assert.Contains("skipped MsgItemUsage mythsoul bag add", output);
        Assert.Contains("inventory is full", output);
        Assert.DoesNotContain("handled MsgItemUsage mythsoul bag add", output);
    }

    [Fact]
    public async Task HandlerItemUsageRuneMovesInventoryRuneToBagEchoesAndPersists()
    {
        const uint runeUid = 5_337_500;
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        WriteItems(
            1_293_600,
            PlayerItemSnapshotLoaderTests.DbItem(runeUid, 4_010_501, position: 0));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateRuneBagUse(runeUid, dwParam: 0, 8_888)));

        Assert.Equal(countBeforeRequest + 2, sent.Count);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest]), out var storedRune));
        Assert.Equal(runeUid, storedRune.Uid);
        Assert.Equal(4_010_501U, storedRune.ItemId);
        Assert.Equal((byte)211, storedRune.Position);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, storedRune.Mode);
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(sent[countBeforeRequest + 1]), out var echoPacket));
        Assert.Equal(
            new MsgItemUsagePacket(
                runeUid,
                DwParam: 0,
                MsgItemUsageAction.Rune,
                511_043_495,
                0,
                0,
                0),
            echoPacket);
        Assert.True(PacketReader.TryRead(sent[countBeforeRequest + 1], out var echoReader));
        Assert.Equal(8_888U, echoReader.ReadUInt32(MsgItemUsagePacket.PayloadLength));

        var session = GetSession(handler, connection);
        Assert.DoesNotContain(session.PlayerItems.InventoryItems, item => item.Uid == runeUid);
        Assert.Contains(session.PlayerItems.RuneItems, item => item.Uid == runeUid && item.Position == 211);

        var persistedItems = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600);
        Assert.DoesNotContain(persistedItems.InventoryItems, item => item.Uid == runeUid);
        Assert.Contains(persistedItems.RuneItems, item => item.Uid == runeUid && item.Position == 211);

        var output = log.ToString();
        Assert.Contains("handled MsgItemUsage direct rune bag add", output);
        Assert.Contains("persisted MsgItemUsage direct rune bag add items", output);
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage direct rune bag add", output);
    }

    [Fact]
    public async Task HandlerItemUsageRuneReturnsBagRuneToInventoryEchoesAndPersists()
    {
        const uint runeUid = 5_337_510;
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        WriteItems(
            1_293_600,
            PlayerItemSnapshotLoaderTests.DbItem(runeUid, 4_020_501, position: 211));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateRuneBagUse(runeUid, dwParam: 1)));

        Assert.Equal(countBeforeRequest + 3, sent.Count);
        AssertItemUsage(
            sent[countBeforeRequest],
            new MsgItemUsagePacket(runeUid, DwParam: 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest + 1]), out var returnedRune));
        Assert.Equal(runeUid, returnedRune.Uid);
        Assert.Equal(4_020_501U, returnedRune.ItemId);
        Assert.Equal((byte)0, returnedRune.Position);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, returnedRune.Mode);
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(sent[countBeforeRequest + 2]), out var echoPacket));
        Assert.Equal(
            new MsgItemUsagePacket(
                runeUid,
                DwParam: 1,
                MsgItemUsageAction.Rune,
                511_043_495,
                0,
                0,
                0),
            echoPacket);

        var session = GetSession(handler, connection);
        Assert.Contains(session.PlayerItems.InventoryItems, item => item.Uid == runeUid && item.Position == 0);
        Assert.DoesNotContain(session.PlayerItems.RuneItems, item => item.Uid == runeUid);

        var persistedItems = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600);
        Assert.Contains(persistedItems.InventoryItems, item => item.Uid == runeUid && item.Position == 0);
        Assert.DoesNotContain(persistedItems.RuneItems, item => item.Uid == runeUid);

        var output = log.ToString();
        Assert.Contains("handled MsgItemUsage direct rune bag remove", output);
        Assert.Contains("persisted MsgItemUsage direct rune bag remove items", output);
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage direct rune bag remove", output);
    }

    [Fact]
    public async Task HandlerItemUsageRuneRequiresSecondRebornAndLevelFifteen()
    {
        const uint runeUid = 5_337_520;
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565, level: 14);
        WriteItems(
            1_293_600,
            PlayerItemSnapshotLoaderTests.DbItem(runeUid, 4_030_501, position: 0));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateRuneBagUse(runeUid, dwParam: 0)));

        Assert.Equal(countBeforeRequest, sent.Count);
        var session = GetSession(handler, connection);
        Assert.Contains(session.PlayerItems.InventoryItems, item => item.Uid == runeUid && item.Position == 0);
        Assert.Empty(session.PlayerItems.RuneItems);

        var output = log.ToString();
        Assert.Contains("skipped MsgItemUsage direct rune bag", output);
        Assert.Contains("requires second reborn and level 15", output);
        Assert.DoesNotContain("handled MsgItemUsage direct rune bag add", output);
    }

    [Fact]
    public async Task HandlerItemUsageEquipRuneMovesBagRuneToSlotEchoesAndPersists()
    {
        const uint runeUid = 5_337_530;
        const uint runeItemId = 4_010_501;
        var worldData = CreateWorldDataCatalog(CreateItemType(runeItemId, "RedRune", durability: 1));
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        WriteItems(
            1_293_600,
            PlayerItemSnapshotLoaderTests.DbItem(runeUid, runeItemId, position: 211));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, worldData);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateRuneEquipmentUse(
                runeUid,
                dwParam: 101,
                MsgItemUsageAction.EquipRune,
                8_888)));

        Assert.True(sent.Count >= countBeforeRequest + 1, log.ToString());
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(sent[countBeforeRequest]), out var echoPacket));
        Assert.Equal(
            new MsgItemUsagePacket(
                runeUid,
                DwParam: 101,
                MsgItemUsageAction.EquipRune,
                511_043_495,
                0,
                0,
                0),
            echoPacket);
        Assert.True(PacketReader.TryRead(sent[countBeforeRequest], out var echoReader));
        Assert.Equal(8_888U, echoReader.ReadUInt32(MsgItemUsagePacket.PayloadLength));

        var session = GetSession(handler, connection);
        Assert.Contains(session.PlayerItems.RuneItems, item => item.Uid == runeUid && item.Position == 101);

        var persistedItems = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600);
        Assert.Contains(persistedItems.RuneItems, item => item.Uid == runeUid && item.Position == 101);

        var output = log.ToString();
        Assert.Contains("committed MsgItemUsage equip rune", output);
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage equip rune", output);
    }

    [Fact]
    public async Task HandlerItemUsageEquipRuneUnequipsSameRuneAndOccupiedSlotBeforeEquipping()
    {
        const uint sameRuneUid = 5_337_540;
        const uint slotRuneUid = 5_337_541;
        const uint selectedRuneUid = 5_337_542;
        const uint selectedRuneItemId = 4_010_501;
        const uint slotRuneItemId = 4_010_502;
        var worldData = CreateWorldDataCatalog(
            CreateItemType(selectedRuneItemId, "RedRune", durability: 1),
            CreateItemType(slotRuneItemId, "OtherRedRune", durability: 1));
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        WriteItems(
            1_293_600,
            PlayerItemSnapshotLoaderTests.DbItem(sameRuneUid, selectedRuneItemId, position: 121),
            PlayerItemSnapshotLoaderTests.DbItem(slotRuneUid, slotRuneItemId, position: 101),
            PlayerItemSnapshotLoaderTests.DbItem(selectedRuneUid, selectedRuneItemId, position: 211));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, worldData);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateRuneEquipmentUse(
                selectedRuneUid,
                dwParam: 101,
                MsgItemUsageAction.EquipRune)));

        Assert.True(sent.Count >= countBeforeRequest + 3, log.ToString());
        AssertItemUsage(
            sent[countBeforeRequest],
            new MsgItemUsagePacket(sameRuneUid, DwParam: 121, MsgItemUsageAction.UnequipRune, 0, 0, 0, 0));
        AssertItemUsage(
            sent[countBeforeRequest + 1],
            new MsgItemUsagePacket(slotRuneUid, DwParam: 101, MsgItemUsageAction.UnequipRune, 0, 0, 0, 0));
        AssertItemUsage(
            sent[countBeforeRequest + 2],
            new MsgItemUsagePacket(
                selectedRuneUid,
                DwParam: 101,
                MsgItemUsageAction.EquipRune,
                511_043_495,
                0,
                0,
                0));

        var session = GetSession(handler, connection);
        Assert.Contains(session.PlayerItems.RuneItems, item => item.Uid == sameRuneUid && item.Position == 212);
        Assert.Contains(session.PlayerItems.RuneItems, item => item.Uid == slotRuneUid && item.Position == 212);
        Assert.Contains(session.PlayerItems.RuneItems, item => item.Uid == selectedRuneUid && item.Position == 101);

        var persistedItems = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600);
        Assert.Contains(persistedItems.RuneItems, item => item.Uid == sameRuneUid && item.Position == 212);
        Assert.Contains(persistedItems.RuneItems, item => item.Uid == slotRuneUid && item.Position == 212);
        Assert.Contains(persistedItems.RuneItems, item => item.Uid == selectedRuneUid && item.Position == 101);

        var output = log.ToString();
        Assert.Contains("committed MsgItemUsage equip rune", output);
    }

    [Fact]
    public async Task HandlerItemUsageUnequipRuneMovesWornRuneToStorageEchoesAndPersists()
    {
        const uint runeUid = 5_337_550;
        const uint runeItemId = 4_020_501;
        var worldData = CreateWorldDataCatalog(CreateItemType(runeItemId, "OwnedRune", durability: 1));
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        WriteItems(
            1_293_600,
            PlayerItemSnapshotLoaderTests.DbItem(runeUid, runeItemId, position: 102));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, worldData);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateRuneEquipmentUse(
                runeUid,
                dwParam: 102,
                MsgItemUsageAction.UnequipRune)));

        Assert.True(sent.Count == countBeforeRequest + 5, log.ToString());
        Assert.Equal(countBeforeRequest + 5, sent.Count);
        AssertItemUsage(
            sent[countBeforeRequest],
            new MsgItemUsagePacket(
                runeUid,
                DwParam: 102,
                MsgItemUsageAction.UnequipRune,
                511_043_495,
                0,
                0,
                0));
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[countBeforeRequest + 1]), out var maxHp));
        Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, Assert.Single(maxHp.Updates).Type);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[countBeforeRequest + 2]), out var hp));
        Assert.Equal([LegacyMsgUpdateDataType.MaxHitpoints, LegacyMsgUpdateDataType.Hitpoints], hp.Updates.Select(x => x.Type));
        Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(sent[countBeforeRequest + 3]), out LegacyPlayerStatusPacket _));
        Assert.True(LegacyUserTotalRefineLevPacket.TryParse(ReadFrame(sent[countBeforeRequest + 4]), out _));

        var session = GetSession(handler, connection);
        Assert.Contains(session.PlayerItems.RuneItems, item => item.Uid == runeUid && item.Position == 212);

        var persistedItems = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600);
        Assert.Contains(persistedItems.RuneItems, item => item.Uid == runeUid && item.Position == 212);

        var output = log.ToString();
        Assert.Contains("committed MsgItemUsage unequip rune", output);
    }

    [Fact]
    public async Task HandlerItemUsageRedRuneProgressControlMapsComprehensionOptionAndPersists()
    {
        const uint runeUid = 5_337_560;
        const uint runeItemId = 4_010_001;
        var worldData = CreateWorldDataCatalog(CreateItemType(runeItemId, "Comprehension", durability: 1));
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        WriteItems(
            1_293_600,
            PlayerItemSnapshotLoaderTests.DbItem(runeUid, runeItemId, position: 101));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, worldData);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateRuneEquipmentUse(
                runeUid,
                dwParam: 1,
                MsgItemUsageAction.RedRuneProgressControl)));

        Assert.Equal(countBeforeRequest + 1, sent.Count);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest]), out var runeUpdate));
        Assert.Equal(runeUid, runeUpdate.Uid);
        Assert.Equal(runeItemId, runeUpdate.ItemId);
        Assert.Equal((uint)5_030, runeUpdate.SocketProgress);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, runeUpdate.Mode);

        var session = GetSession(handler, connection);
        Assert.Contains(session.PlayerItems.RuneItems, item => item.Uid == runeUid && item.SocketProgress == 5_030);

        var persistedItems = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600);
        Assert.Contains(persistedItems.RuneItems, item => item.Uid == runeUid && item.SocketProgress == 5_030);

        var output = log.ToString();
        Assert.Contains("handled MsgItemUsage red rune progress", output);
        Assert.Contains("socketProgress=5030", output);
        Assert.Contains("persisted MsgItemUsage red rune progress items", output);
    }

    [Fact]
    public async Task HandlerItemUsageRedRuneProgressControlStoresRawProgressForOtherRune()
    {
        const uint runeUid = 5_337_570;
        const uint runeItemId = 4_010_501;
        var worldData = CreateWorldDataCatalog(CreateItemType(runeItemId, "RedRune", durability: 1));
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        WriteItems(
            1_293_600,
            PlayerItemSnapshotLoaderTests.DbItem(runeUid, runeItemId, position: 101));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, worldData);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateRuneEquipmentUse(
                runeUid,
                dwParam: 42,
                MsgItemUsageAction.RedRuneProgressControl)));

        Assert.Equal(countBeforeRequest + 1, sent.Count);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest]), out var runeUpdate));
        Assert.Equal(runeUid, runeUpdate.Uid);
        Assert.Equal(runeItemId, runeUpdate.ItemId);
        Assert.Equal((uint)42, runeUpdate.SocketProgress);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, runeUpdate.Mode);

        var session = GetSession(handler, connection);
        Assert.Contains(session.PlayerItems.RuneItems, item => item.Uid == runeUid && item.SocketProgress == 42);

        var persistedItems = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600);
        Assert.Contains(persistedItems.RuneItems, item => item.Uid == runeUid && item.SocketProgress == 42);

        var output = log.ToString();
        Assert.Contains("handled MsgItemUsage red rune progress", output);
        Assert.Contains("socketProgress=42", output);
        Assert.Contains("persisted MsgItemUsage red rune progress items", output);
    }

    [Fact]
    public async Task HandlerItemUsageDecomposeRuneRemovesRuneCreatesEssenceAndFragmentsAndPersists()
    {
        const uint runeUid = 5_337_580;
        const uint packetId = 9_999_999;
        const uint runeItemId = 4_020_501;
        const uint essenceItemId = 4_040_001;
        const uint fragmentItemId = 3_306_368;
        var worldData = CreateWorldDataCatalogWithRuneLevelExperience(
            [
                new RuneLevelExperience(
                    1,
                    RuneType: 402,
                    Level: 1,
                    Experience: 200,
                    SourceFile: "rune_levexp.txt",
                    LineNumber: 1,
                    RawLine: "1@@402@@1@@200@@",
                    Fields: ["1", "402", "1", "200", ""]),
            ],
            CreateItemType(fragmentItemId, "BlueRuneFragment", durability: 1));
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        WriteItems(
            1_293_600,
            PlayerItemSnapshotLoaderTests.DbItem(
                runeUid,
                runeItemId,
                position: 211,
                runeExperience: 50));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, worldData);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateRuneDecomposeUse(packetId, runeUid)));

        Assert.Equal(countBeforeRequest + 3, sent.Count);
        AssertItemUsage(
            sent[countBeforeRequest],
            new MsgItemUsagePacket(runeUid, DwParam: 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest + 1]), out var essencePacket));
        Assert.Equal(essenceItemId, essencePacket.ItemId);
        Assert.Equal((byte)211, essencePacket.Position);
        Assert.Equal((ushort)20, essencePacket.StackSize);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, essencePacket.Mode);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest + 2]), out var fragmentPacket));
        Assert.Equal(fragmentItemId, fragmentPacket.ItemId);
        Assert.Equal((byte)0, fragmentPacket.Position);
        Assert.Equal((ushort)10, fragmentPacket.StackSize);
        Assert.Equal((byte)1, fragmentPacket.Bound);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, fragmentPacket.Mode);

        var session = GetSession(handler, connection);
        Assert.DoesNotContain(session.PlayerItems.RuneItems, item => item.Uid == runeUid);
        Assert.Contains(session.PlayerItems.RuneItems, item =>
            item.ItemId == essenceItemId &&
            item.Position == 211 &&
            item.StackSize == 20);
        Assert.Contains(session.PlayerItems.InventoryItems, item =>
            item.ItemId == fragmentItemId &&
            item.Position == 0 &&
            item.StackSize == 10 &&
            item.Bound == 1);

        var persistedItems = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600);
        Assert.DoesNotContain(persistedItems.RuneItems, item => item.Uid == runeUid);
        Assert.Contains(persistedItems.RuneItems, item =>
            item.ItemId == essenceItemId &&
            item.Position == 211 &&
            item.StackSize == 20);
        Assert.Contains(persistedItems.InventoryItems, item =>
            item.ItemId == fragmentItemId &&
            item.Position == 0 &&
            item.StackSize == 10 &&
            item.Bound == 1);

        var output = log.ToString();
        Assert.Contains("handled MsgItemUsage decompose rune", output);
        Assert.Contains("runeFamily=402", output);
        Assert.Contains("essenceItemId=4040001", output);
        Assert.Contains("fragmentItemId=3306368", output);
        Assert.Contains("persisted MsgItemUsage decompose rune items", output);
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage decompose rune", output);
    }

    [Fact]
    public async Task HandlerItemUsageDecomposeRuneRequiresTenInventorySpaces()
    {
        const uint runeUid = 5_337_590;
        const uint runeItemId = 4_010_501;
        var fillerItems = Enumerable.Range(0, 31)
            .Select(index => PlayerItemSnapshotLoaderTests.DbItem(
                checked((uint)(5_338_000 + index)),
                checked((uint)(1_000_000 + index)),
                position: 0));
        var worldData = CreateWorldDataCatalogWithRuneLevelExperience(
            [
                new RuneLevelExperience(
                    1,
                    RuneType: 401,
                    Level: 1,
                    Experience: 200,
                    SourceFile: "rune_levexp.txt",
                    LineNumber: 1,
                    RawLine: "1@@401@@1@@200@@",
                    Fields: ["1", "401", "1", "200", ""]),
            ],
            CreateItemType(3_306_366, "RedRuneFragment", durability: 1));
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        WriteItems(
            1_293_600,
            fillerItems
                .Append(PlayerItemSnapshotLoaderTests.DbItem(runeUid, runeItemId, position: 211))
                .ToArray());
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, worldData);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateRuneDecomposeUse(0, runeUid)));

        Assert.Equal(countBeforeRequest + 1, sent.Count);
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(sent[countBeforeRequest]), out var dialog));
        Assert.Contains(dialog.Content, entry => entry.Contains("10 free spaces", StringComparison.Ordinal));

        var session = GetSession(handler, connection);
        Assert.Contains(session.PlayerItems.RuneItems, item => item.Uid == runeUid && item.Position == 211);
        Assert.Equal(31, session.PlayerItems.InventoryItems.Count);

        var persistedItems = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600);
        Assert.Contains(persistedItems.RuneItems, item => item.Uid == runeUid && item.Position == 211);
        Assert.Equal(31, persistedItems.InventoryItems.Count);

        var output = log.ToString();
        Assert.Contains("skipped MsgItemUsage decompose rune", output);
        Assert.Contains("requires 10 free inventory spaces", output);
        Assert.DoesNotContain("handled MsgItemUsage decompose rune", output);
    }

    [Fact]
    public async Task HandlerItemUsageDropRuneRemovesRuneAndPersists()
    {
        const uint runeUid = 5_337_600;
        const uint runeItemId = 4_030_501;
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        WriteItems(
            1_293_600,
            PlayerItemSnapshotLoaderTests.DbItem(runeUid, runeItemId, position: 211));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateRuneEquipmentUse(
                runeUid,
                dwParam: 0,
                MsgItemUsageAction.DropRune)));

        Assert.Equal(countBeforeRequest + 1, sent.Count);
        AssertItemUsage(
            sent[countBeforeRequest],
            new MsgItemUsagePacket(runeUid, DwParam: 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));

        var session = GetSession(handler, connection);
        Assert.DoesNotContain(session.PlayerItems.RuneItems, item => item.Uid == runeUid);

        var persistedItems = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600);
        Assert.DoesNotContain(persistedItems.RuneItems, item => item.Uid == runeUid);

        var output = log.ToString();
        Assert.Contains("handled MsgItemUsage drop rune", output);
        Assert.Contains("position=211", output);
        Assert.Contains("persisted MsgItemUsage drop rune items", output);
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage drop rune", output);
    }

    [Fact]
    public async Task HandlerItemUsageComposeRuneConsumesMaterialLevelsTargetAndPersists()
    {
        const uint targetRuneUid = 5_337_610;
        const uint materialRuneUid = 5_337_611;
        const uint targetRuneItemId = 4_020_501;
        const uint materialRuneItemId = 4_040_001;
        var worldData = CreateWorldDataCatalogWithRuneLevelExperience(
            [
                new RuneLevelExperience(
                    1,
                    RuneType: 402,
                    Level: 1,
                    Experience: 100,
                    SourceFile: "rune_levexp.txt",
                    LineNumber: 1,
                    RawLine: "1@@402@@1@@100@@",
                    Fields: ["1", "402", "1", "100", ""]),
                new RuneLevelExperience(
                    2,
                    RuneType: 402,
                    Level: 2,
                    Experience: 300,
                    SourceFile: "rune_levexp.txt",
                    LineNumber: 2,
                    RawLine: "2@@402@@2@@300@@",
                    Fields: ["2", "402", "2", "300", ""]),
                new RuneLevelExperience(
                    3,
                    RuneType: 402,
                    Level: 3,
                    Experience: 600,
                    SourceFile: "rune_levexp.txt",
                    LineNumber: 3,
                    RawLine: "3@@402@@3@@600@@",
                    Fields: ["3", "402", "3", "600", ""]),
                new RuneLevelExperience(
                    4,
                    RuneType: 404,
                    Level: 1,
                    Experience: 250,
                    SourceFile: "rune_levexp.txt",
                    LineNumber: 4,
                    RawLine: "4@@404@@1@@250@@",
                    Fields: ["4", "404", "1", "250", ""]),
            ],
            CreateItemType(targetRuneItemId, "BlueRune", durability: 1),
            CreateItemType(targetRuneItemId + 1, "BlueRuneLevel2", durability: 1),
            CreateItemType(materialRuneItemId, "BlueRuneEssence", durability: 1));
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        WriteItems(
            1_293_600,
            PlayerItemSnapshotLoaderTests.DbItem(targetRuneUid, targetRuneItemId, position: 102),
            PlayerItemSnapshotLoaderTests.DbItem(materialRuneUid, materialRuneItemId, position: 211));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, worldData);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateRuneComposeDeclaredCountUse(
                targetRuneUid,
                declaredCount: 1,
                materialRuneUid)));

        Assert.Equal(countBeforeRequest + 2, sent.Count);
        AssertItemUsage(
            sent[countBeforeRequest],
            new MsgItemUsagePacket(materialRuneUid, DwParam: 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest + 1]), out var updatedRunePacket));
        Assert.Equal(targetRuneUid, updatedRunePacket.Uid);
        Assert.Equal(targetRuneItemId + 1, updatedRunePacket.ItemId);
        Assert.Equal((uint)50, updatedRunePacket.RuneExperience);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, updatedRunePacket.Mode);

        var session = GetSession(handler, connection);
        Assert.Contains(session.PlayerItems.RuneItems, item =>
            item.Uid == targetRuneUid &&
            item.ItemId == targetRuneItemId + 1 &&
            item.RuneExperience == 50);
        Assert.DoesNotContain(session.PlayerItems.RuneItems, item => item.Uid == materialRuneUid);

        var persistedItems = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600);
        Assert.Contains(persistedItems.RuneItems, item =>
            item.Uid == targetRuneUid &&
            item.ItemId == targetRuneItemId + 1 &&
            item.RuneExperience == 50);
        Assert.DoesNotContain(persistedItems.RuneItems, item => item.Uid == materialRuneUid);

        var output = log.ToString();
        Assert.Contains("handled MsgItemUsage compose rune", output);
        Assert.Contains("oldItemId=4020501", output);
        Assert.Contains("newItemId=4020502", output);
        Assert.Contains("newRuneExp=50", output);
        Assert.Contains("persisted MsgItemUsage compose rune items", output);
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage compose rune", output);
    }

    [Fact]
    public async Task HandlerItemUsageChanceRuneLevelsAtFullProgressAndPersists()
    {
        const uint runeUid = 5_337_620;
        const uint runeItemId = 4_020_501;
        var worldData = CreateWorldDataCatalogWithRuneLevelExperience(
            [
                new RuneLevelExperience(
                    1,
                    RuneType: 402,
                    Level: 1,
                    Experience: 100,
                    SourceFile: "rune_levexp.txt",
                    LineNumber: 1,
                    RawLine: "1@@402@@1@@100@@",
                    Fields: ["1", "402", "1", "100", ""]),
                new RuneLevelExperience(
                    2,
                    RuneType: 402,
                    Level: 2,
                    Experience: 300,
                    SourceFile: "rune_levexp.txt",
                    LineNumber: 2,
                    RawLine: "2@@402@@2@@300@@",
                    Fields: ["2", "402", "2", "300", ""]),
            ],
            CreateItemType(runeItemId, "BlueRune", durability: 1),
            CreateItemType(runeItemId + 1, "BlueRuneLevel2", durability: 1));
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565);
        WriteItems(
            1_293_600,
            PlayerItemSnapshotLoaderTests.DbItem(
                runeUid,
                runeItemId,
                position: 102,
                runeExperience: 200));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, worldData);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateRuneEquipmentUse(
                runeUid,
                dwParam: 0,
                MsgItemUsageAction.ChanceRune)));

        Assert.Equal(countBeforeRequest + 1, sent.Count);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest]), out var updatedRunePacket));
        Assert.Equal(runeUid, updatedRunePacket.Uid);
        Assert.Equal(runeItemId + 1, updatedRunePacket.ItemId);
        Assert.Equal((uint)0, updatedRunePacket.RuneExperience);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, updatedRunePacket.Mode);

        var session = GetSession(handler, connection);
        Assert.Contains(session.PlayerItems.RuneItems, item =>
            item.Uid == runeUid &&
            item.ItemId == runeItemId + 1 &&
            item.RuneExperience == 0);

        var persistedItems = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600);
        Assert.Contains(persistedItems.RuneItems, item =>
            item.Uid == runeUid &&
            item.ItemId == runeItemId + 1 &&
            item.RuneExperience == 0);

        var output = log.ToString();
        Assert.Contains("handled MsgItemUsage chance rune", output);
        Assert.Contains("chanceRate=100", output);
        Assert.Contains("chancePassed=True", output);
        Assert.Contains("persisted MsgItemUsage chance rune items", output);
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage chance rune", output);
    }

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task HandlerItemUsageManaPotionRestoresManaConsumesItemAndSendsManaUpdate()
    {
        const uint itemUid = 5_337_020;
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        if (!File.Exists(Path.Combine(seedPath, "manifest.json")))
        {
            return;
        }

        var worldData = await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync();
        WriteSnapshot(
            accountId: 1_293_600,
            uid: 1_293_600,
            mapId: 1_015,
            x: 826,
            y: 565,
            minMana: 40,
            maxMana: 5_000);
        WriteItems(1_293_600, PlayerItemSnapshotLoaderTests.DbItem(itemUid, 1_001_000, position: 0));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, worldData);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(connection, ReadFrame(CreateEquipUse(itemUid).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 2, sent.Count);
        AssertItemUsage(
            sent[countBeforeRequest],
            new MsgItemUsagePacket(itemUid, DwParam: 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[countBeforeRequest + 1]), out var manaUpdate));
        Assert.Equal(1_293_600U, manaUpdate.Uid);
        Assert.Equal([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 110)], manaUpdate.Updates);

        var session = GetSession(handler, connection);
        Assert.Equal(110, session.Character.MinMana);
        Assert.Equal(5_000, session.Character.MaxMana);
        Assert.Empty(session.PlayerItems.InventoryItems);

        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgItemUsage consumable", output);
        Assert.Contains("itemId=1001000", output);
        Assert.Contains("mp=40->110", output);
        Assert.DoesNotContain("current mana state is not modeled yet", output);
        Assert.DoesNotContain("non-equipment item effects are not modeled", output);
    }

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task HandlerItemUsagePotionCooldownBlocksImmediateSecondUseWithoutConsuming()
    {
        const uint itemUid = 5_337_021;
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        if (!File.Exists(Path.Combine(seedPath, "manifest.json")))
        {
            return;
        }

        var worldData = await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync();
        WriteSnapshot(
            accountId: 1_293_600,
            uid: 1_293_600,
            mapId: 1_015,
            x: 826,
            y: 565,
            minHitPoints: 10);
        WriteItems(1_293_600, PlayerItemSnapshotLoaderTests.DbItem(
            itemUid,
            1_000_000,
            position: 0,
            stackSize: 2));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, worldData);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(connection, ReadFrame(CreateEquipUse(itemUid).ToFrameBytes()));
        var countAfterFirstUse = sent.Count;
        await handler.HandleFrameAsync(connection, ReadFrame(CreateEquipUse(itemUid).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 3, countAfterFirstUse);
        Assert.Equal(countAfterFirstUse, sent.Count);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest]), out var stackUpdate));
        Assert.Equal(itemUid, stackUpdate.Uid);
        Assert.Equal(1_000_000U, stackUpdate.ItemId);
        Assert.Equal(1, stackUpdate.StackSize);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, stackUpdate.Mode);

        var session = GetSession(handler, connection);
        Assert.Equal(80, session.Character.MinHitPoints);
        var remainingPotion = Assert.Single(session.PlayerItems.InventoryItems, item => item.Uid == itemUid);
        Assert.Equal(1, remainingPotion.StackSize);

        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgItemUsage consumable", output);
        Assert.Contains("hp=10->80", output);
        Assert.Contains("legacy potion cooldown 150ms is active", output);
        Assert.DoesNotContain("non-equipment item effects are not modeled", output);
    }

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task HandlerItemUsageRemoveInventoryHealthPotionUsesSourceUseItemPath()
    {
        const uint itemUid = 5_337_023;
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        if (!File.Exists(Path.Combine(seedPath, "manifest.json")))
        {
            return;
        }

        var worldData = await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync();
        WriteSnapshot(
            accountId: 1_293_600,
            uid: 1_293_600,
            mapId: 1_015,
            x: 826,
            y: 565,
            minHitPoints: 10);
        WriteItems(1_293_600, PlayerItemSnapshotLoaderTests.DbItem(itemUid, 1_000_000, position: 0));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, worldData);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(connection, ReadFrame(CreateRemoveInventoryUse(itemUid).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 3, sent.Count);
        AssertItemUsage(
            sent[countBeforeRequest],
            new MsgItemUsagePacket(itemUid, DwParam: 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));

        var updateFrames = sent.Skip(countBeforeRequest + 1).ToArray();
        Assert.Contains(updateFrames, bytes =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
            update.Updates.Contains(new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Hitpoints, 80)));

        var session = GetSession(handler, connection);
        Assert.Equal(80, session.Character.MinHitPoints);
        Assert.Empty(session.PlayerItems.InventoryItems);

        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgItemUsage consumable", output);
        Assert.Contains("action=RemoveInventory", output);
        Assert.Contains("itemId=1000000", output);
        Assert.Contains("hp=10->80", output);
        Assert.DoesNotContain("unsupported MsgItemUsage", output);
        Assert.DoesNotContain("non-equipment item effects are not modeled", output);
    }

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task HandlerItemUsageHealthPotionDoublesHealWhenShieldBlockIsActive()
    {
        const uint itemUid = 5_337_022;
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        if (!File.Exists(Path.Combine(seedPath, "manifest.json")))
        {
            return;
        }

        var worldData = await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync();
        WriteSnapshot(
            accountId: 1_293_600,
            uid: 1_293_600,
            mapId: 1_015,
            x: 826,
            y: 565,
            minHitPoints: 10,
            statusFlags: BuildStatusFlags(LegacyShieldBlockFlag));
        WriteItems(1_293_600, PlayerItemSnapshotLoaderTests.DbItem(itemUid, 1_000_000, position: 0));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, worldData);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(connection, ReadFrame(CreateEquipUse(itemUid).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 3, sent.Count);
        AssertItemUsage(
            sent[countBeforeRequest],
            new MsgItemUsagePacket(itemUid, DwParam: 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));

        var session = GetSession(handler, connection);
        Assert.Equal(150, session.Character.MinHitPoints);
        Assert.Empty(session.PlayerItems.InventoryItems);

        var output = log.ToString();
        Assert.Contains("WorldServer handled MsgItemUsage consumable", output);
        Assert.Contains("hp=10->150", output);
        Assert.Contains("healAmount=140", output);
        Assert.Contains("hitpointMultiplierPercent=200", output);
        Assert.DoesNotContain("non-equipment item effects are not modeled", output);
    }

    [Theory]
    [InlineData(5_335_005U, 1_200_005U, "tough drill direct use")]
    [InlineData(5_335_006U, 1_200_006U, "star drill direct use")]
    public async Task HandlerItemUsageDrillDirectUseUsesSpecificNoOpWithoutGenericDeferral(
        uint itemUid,
        uint itemId,
        string expectedLog)
    {
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565, level: 139);
        WriteItems(1_293_600, PlayerItemSnapshotLoaderTests.DbItem(itemUid, itemId, position: 0));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(connection, ReadFrame(CreateEquipUse(itemUid).ToFrameBytes()));

        Assert.Equal(countBeforeRequest, sent.Count);
        var session = GetSession(handler, connection);
        Assert.Contains(session.PlayerItems.InventoryItems, item => item.Uid == itemUid && item.ItemId == itemId);

        var output = log.ToString();
        Assert.Contains(expectedLog, output);
        Assert.Contains("itemId=" + itemId.ToString(CultureInfo.InvariantCulture), output);
        Assert.Contains("CreateSocketItem", output);
        Assert.Contains("1200006", output);
        Assert.DoesNotContain("non-equipment item effects are not modeled", output);
    }

    [Fact]
    public async Task HandlerCreateSocketItemSingleSocketMeteorOpensFirstSocketAndConsumesMaterial()
    {
        const uint targetUid = 5_343_000;
        const uint meteorUid = 5_343_100;
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565, level: 139);
        WriteItemsWithSockets(
            1_293_600,
            (PlayerItemSnapshotLoaderTests.DbItem(targetUid, 500_109, position: 0), SocketOne: (byte)0, SocketTwo: (byte)0),
            (PlayerItemSnapshotLoaderTests.DbItem(meteorUid, 1_088_000, position: 0), SocketOne: (byte)0, SocketTwo: (byte)0));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateCreateSocketUse(targetUid, 1, meteorUid)));

        Assert.Equal(countBeforeRequest + 3, sent.Count);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest]), out var updatedItem));
        Assert.Equal(targetUid, updatedItem.Uid);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, updatedItem.Mode);
        Assert.Equal(255, updatedItem.SocketOne);
        Assert.Equal(0, updatedItem.SocketTwo);
        AssertItemUsage(
            sent[countBeforeRequest + 1],
            new MsgItemUsagePacket(meteorUid, DwParam: 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));
        AssertItemUsage(
            sent[countBeforeRequest + 2],
            new MsgItemUsagePacket(targetUid, DwParam: 1, MsgItemUsageAction.CreateSocketItem, 0, 0, 0, 0));

        var session = GetSession(handler, connection);
        var target = Assert.Single(session.PlayerItems.InventoryItems, item => item.Uid == targetUid);
        Assert.Equal(255, target.SocketOne);
        Assert.Equal(0, target.SocketTwo);
        Assert.DoesNotContain(session.PlayerItems.InventoryItems, item => item.Uid == meteorUid);

        var output = log.ToString();
        Assert.Contains("handled MsgItemUsage create socket item", output);
        Assert.Contains("materialItemId=1088000", output);
        Assert.Contains("count=1", output);
        Assert.Contains("socket=first", output);
    }

    [Fact]
    public async Task HandlerCreateSocketItemFiveSocketMeteorsOpenSecondSocketAndConsumeMaterials()
    {
        const uint targetUid = 5_344_000;
        var meteorUids = Enumerable.Range(0, 5).Select(index => checked((uint)(5_344_100 + index))).ToArray();
        var items = new List<(PlayerItemSnapshotLoaderTests.TestDbItem Item, byte SocketOne, byte SocketTwo)>
        {
            (PlayerItemSnapshotLoaderTests.DbItem(targetUid, 500_109, position: 0), SocketOne: (byte)255, SocketTwo: (byte)0),
        };
        items.AddRange(meteorUids.Select(uid =>
            (PlayerItemSnapshotLoaderTests.DbItem(uid, 1_088_000, position: 0), SocketOne: (byte)0, SocketTwo: (byte)0)));
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565, level: 139);
        WriteItemsWithSockets(1_293_600, items.ToArray());
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateCreateSocketUse(targetUid, 5, meteorUids)));

        Assert.Equal(countBeforeRequest + 7, sent.Count);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest]), out var updatedItem));
        Assert.Equal(targetUid, updatedItem.Uid);
        Assert.Equal(255, updatedItem.SocketOne);
        Assert.Equal(255, updatedItem.SocketTwo);

        for (var index = 0; index < meteorUids.Length; index++)
        {
            AssertItemUsage(
                sent[countBeforeRequest + 1 + index],
                new MsgItemUsagePacket(meteorUids[index], DwParam: 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));
        }

        AssertItemUsage(
            sent[countBeforeRequest + 6],
            new MsgItemUsagePacket(targetUid, DwParam: 1, MsgItemUsageAction.CreateSocketItem, 0, 0, 0, 0));

        var session = GetSession(handler, connection);
        var target = Assert.Single(session.PlayerItems.InventoryItems, item => item.Uid == targetUid);
        Assert.Equal(255, target.SocketOne);
        Assert.Equal(255, target.SocketTwo);
        Assert.DoesNotContain(session.PlayerItems.InventoryItems, item => item.ItemId == 1_088_000 && meteorUids.Contains(item.Uid));

        var output = log.ToString();
        Assert.Contains("handled MsgItemUsage create socket item", output);
        Assert.Contains("materialItemId=1088000", output);
        Assert.Contains("count=5", output);
        Assert.Contains("socket=second", output);
    }

    [Fact]
    public async Task HandlerCreateSocketItemTwelveSocketMeteorsOpenFirstSocketAndConsumeMaterials()
    {
        const uint targetUid = 5_345_000;
        var meteorUids = Enumerable.Range(0, 12).Select(index => checked((uint)(5_345_100 + index))).ToArray();
        var items = new List<(PlayerItemSnapshotLoaderTests.TestDbItem Item, byte SocketOne, byte SocketTwo)>
        {
            (PlayerItemSnapshotLoaderTests.DbItem(targetUid, 500_109, position: 0), SocketOne: (byte)0, SocketTwo: (byte)0),
        };
        items.AddRange(meteorUids.Select(uid =>
            (PlayerItemSnapshotLoaderTests.DbItem(uid, 1_088_000, position: 0), SocketOne: (byte)0, SocketTwo: (byte)0)));
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565, level: 139);
        WriteItemsWithSockets(1_293_600, items.ToArray());
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateCreateSocketUse(targetUid, 12, meteorUids)));

        Assert.Equal(countBeforeRequest + 14, sent.Count);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest]), out var updatedItem));
        Assert.Equal(targetUid, updatedItem.Uid);
        Assert.Equal(255, updatedItem.SocketOne);
        Assert.Equal(0, updatedItem.SocketTwo);

        for (var index = 0; index < meteorUids.Length; index++)
        {
            AssertItemUsage(
                sent[countBeforeRequest + 1 + index],
                new MsgItemUsagePacket(meteorUids[index], DwParam: 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));
        }

        AssertItemUsage(
            sent[countBeforeRequest + 13],
            new MsgItemUsagePacket(targetUid, DwParam: 1, MsgItemUsageAction.CreateSocketItem, 0, 0, 0, 0));

        var session = GetSession(handler, connection);
        var target = Assert.Single(session.PlayerItems.InventoryItems, item => item.Uid == targetUid);
        Assert.Equal(255, target.SocketOne);
        Assert.Equal(0, target.SocketTwo);
        Assert.DoesNotContain(session.PlayerItems.InventoryItems, item => item.ItemId == 1_088_000 && meteorUids.Contains(item.Uid));

        var output = log.ToString();
        Assert.Contains("handled MsgItemUsage create socket item", output);
        Assert.Contains("materialItemId=1088000", output);
        Assert.Contains("count=12", output);
        Assert.Contains("socket=first", output);
    }

    [Fact]
    public async Task HandlerCreateSocketItemStarDrillsOpenSecondSocketAndConsumeSevenMaterials()
    {
        const uint targetUid = 5_340_000;
        var starUids = Enumerable.Range(0, 7).Select(index => checked((uint)(5_340_100 + index))).ToArray();
        var items = new List<PlayerItemSnapshotLoaderTests.TestDbItem>
        {
            PlayerItemSnapshotLoaderTests.DbItem(targetUid, 500_109, position: 0),
        };
        items.AddRange(starUids.Select(uid => PlayerItemSnapshotLoaderTests.DbItem(uid, 1_200_006, position: 0)));
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565, level: 139);
        WriteItems(1_293_600, items.ToArray());
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateCreateSocketUse(targetUid, 7, starUids)));

        Assert.Equal(countBeforeRequest + 9, sent.Count);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest]), out var updatedItem));
        Assert.Equal(targetUid, updatedItem.Uid);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, updatedItem.Mode);
        Assert.Equal(255, updatedItem.SocketTwo);

        for (var index = 0; index < starUids.Length; index++)
        {
            AssertItemUsage(
                sent[countBeforeRequest + 1 + index],
                new MsgItemUsagePacket(starUids[index], DwParam: 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));
        }

        AssertItemUsage(
            sent[countBeforeRequest + 8],
            new MsgItemUsagePacket(targetUid, DwParam: 1, MsgItemUsageAction.CreateSocketItem, 0, 0, 0, 0));

        var session = GetSession(handler, connection);
        var target = Assert.Single(session.PlayerItems.InventoryItems, item => item.Uid == targetUid);
        Assert.Equal(255, target.SocketTwo);
        Assert.DoesNotContain(session.PlayerItems.InventoryItems, item => item.ItemId == 1_200_006 && starUids.Contains(item.Uid));

        var output = log.ToString();
        Assert.Contains("handled MsgItemUsage create socket item", output);
        Assert.Contains("materialItemId=1200006", output);
        Assert.Contains("count=7", output);
    }

    [Fact]
    public async Task HandlerCreateSocketItemToughDrillForcedSuccessOpensSecondSocket()
    {
        const uint targetUid = 5_341_000;
        const uint toughUid = 5_341_100;
        WriteSnapshot(
            accountId: 1_293_600,
            uid: 1_293_600,
            mapId: 1_015,
            x: 826,
            y: 565,
            level: 139,
            legacyToughDrillSocketRoll: 0);
        WriteItemsWithSockets(
            1_293_600,
            (PlayerItemSnapshotLoaderTests.DbItem(targetUid, 500_109, position: 0), SocketOne: (byte)255, SocketTwo: (byte)0),
            (PlayerItemSnapshotLoaderTests.DbItem(toughUid, 1_200_005, position: 0), SocketOne: (byte)0, SocketTwo: (byte)0));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateCreateSocketUse(targetUid, 1, toughUid)));

        Assert.Equal(countBeforeRequest + 3, sent.Count);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest]), out var updatedItem));
        Assert.Equal(targetUid, updatedItem.Uid);
        Assert.Equal(255, updatedItem.SocketTwo);
        AssertItemUsage(
            sent[countBeforeRequest + 1],
            new MsgItemUsagePacket(toughUid, DwParam: 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));
        AssertItemUsage(
            sent[countBeforeRequest + 2],
            new MsgItemUsagePacket(targetUid, DwParam: 1, MsgItemUsageAction.CreateSocketItem, 0, 0, 0, 0));

        var session = GetSession(handler, connection);
        var target = Assert.Single(session.PlayerItems.InventoryItems, item => item.Uid == targetUid);
        Assert.Equal(255, target.SocketTwo);
        Assert.DoesNotContain(session.PlayerItems.InventoryItems, item => item.Uid == toughUid);

        var output = log.ToString();
        Assert.Contains("materialItemId=1200005", output);
        Assert.Contains("roll=0", output);
        Assert.Contains("result=success", output);
    }

    [Fact]
    public async Task HandlerCreateSocketItemToughDrillForcedFailureReturnsStarDrill()
    {
        const uint targetUid = 5_342_000;
        const uint toughUid = 5_342_100;
        WriteSnapshot(
            accountId: 1_293_600,
            uid: 1_293_600,
            mapId: 1_015,
            x: 826,
            y: 565,
            level: 139,
            legacyToughDrillSocketRoll: 20);
        WriteItemsWithSockets(
            1_293_600,
            (PlayerItemSnapshotLoaderTests.DbItem(targetUid, 500_109, position: 0), SocketOne: (byte)255, SocketTwo: (byte)0),
            (PlayerItemSnapshotLoaderTests.DbItem(toughUid, 1_200_005, position: 0), SocketOne: (byte)0, SocketTwo: (byte)0));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(CreateCreateSocketUse(targetUid, 1, toughUid)));

        Assert.Equal(countBeforeRequest + 3, sent.Count);
        AssertItemUsage(
            sent[countBeforeRequest],
            new MsgItemUsagePacket(toughUid, DwParam: 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest + 1]), out var returnedItem));
        Assert.Equal(1_200_006U, returnedItem.ItemId);
        AssertItemUsage(
            sent[countBeforeRequest + 2],
            new MsgItemUsagePacket(targetUid, DwParam: 0, MsgItemUsageAction.CreateSocketItem, 0, 0, 0, 0));

        var session = GetSession(handler, connection);
        var target = Assert.Single(session.PlayerItems.InventoryItems, item => item.Uid == targetUid);
        Assert.Equal(0, target.SocketTwo);
        Assert.DoesNotContain(session.PlayerItems.InventoryItems, item => item.Uid == toughUid);
        Assert.Contains(session.PlayerItems.InventoryItems, item => item.ItemId == 1_200_006);

        var output = log.ToString();
        Assert.Contains("materialItemId=1200005", output);
        Assert.Contains("roll=20", output);
        Assert.Contains("result=failure", output);
        Assert.Contains("returnedItemId=1200006", output);
    }

    [Theory]
    [InlineData(5_322_987U, 1_060_039U)]
    [InlineData(5_322_992U, 1_060_024U)]
    [InlineData(5_338_000U, 1_060_020U)]
    [InlineData(5_338_001U, 1_060_021U)]
    [InlineData(5_338_002U, 1_060_025U)]
    [InlineData(5_335_980U, 3_100_011U)]
    [InlineData(5_336_100U, 3_008_101U)]
    [InlineData(5_337_000U, 723_700U)]
    [InlineData(5_335_005U, 1_200_005U)]
    [InlineData(5_335_006U, 1_200_006U)]
    public async Task HandlerItemUsageLiveNonEquipmentIdsUseSourceAliveGuardWithoutGenericDeferral(
        uint itemUid,
        uint itemId)
    {
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId: 1_015, x: 826, y: 565, minHitPoints: 0);
        WriteItems(1_293_600, PlayerItemSnapshotLoaderTests.DbItem(itemUid, itemId, position: 0));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(connection, ReadFrame(CreateEquipUse(itemUid).ToFrameBytes()));

        Assert.Equal(countBeforeRequest, sent.Count);
        var session = GetSession(handler, connection);
        Assert.Contains(session.PlayerItems.InventoryItems, item => item.Uid == itemUid && item.ItemId == itemId);

        var output = log.ToString();
        Assert.Contains("legacy UseItem requires the character to be alive", output);
        Assert.Contains("itemId=" + itemId.ToString(CultureInfo.InvariantCulture), output);
        Assert.DoesNotContain("non-equipment item effects are not modeled", output);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<(WorldServerFrameHandler Handler, ConquerTcpConnection Connection, List<byte[]> Sent)> EnterWorldAsync(
        TextWriter log,
        WorldDataCatalog? worldData = null)
    {
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(1_293_600, "user02", ConquerRuntimeDefaults.WorldServer);
        var handler = worldData is null
            ? new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot, _itemsRoot), handoff, log)
            : new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot, _itemsRoot), handoff, worldData, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));

        return (handler, connection, sent);
    }

    private void WriteItems(long uid, params PlayerItemSnapshotLoaderTests.TestDbItem[] items)
    {
        File.WriteAllBytes(
            Path.Combine(_itemsRoot, uid.ToString(CultureInfo.InvariantCulture) + ".bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(items, []));
    }

    private void WriteItemsWithSockets(
        long uid,
        params (PlayerItemSnapshotLoaderTests.TestDbItem Item, byte SocketOne, byte SocketTwo)[] items)
    {
        var bytes = PlayerItemSnapshotLoaderTests.BuildPlayerItems(items.Select(item => item.Item).ToArray(), []);
        for (var index = 0; index < items.Length; index++)
        {
            var itemOffset = sizeof(int) + (index * PlayerItemSnapshotLoader.LegacyDbItemSize);
            bytes[itemOffset + 20] = items[index].SocketOne;
            bytes[itemOffset + 21] = items[index].SocketTwo;
        }

        File.WriteAllBytes(
            Path.Combine(_itemsRoot, uid.ToString(CultureInfo.InvariantCulture) + ".bin"),
            bytes);
    }

    private void WriteSnapshot(
        long accountId,
        long uid,
        long mapId,
        long x,
        long y,
        long minHitPoints = 10_000,
        long minMana = 5_000,
        long? maxMana = null,
        long level = 140,
        long experience = 0,
        long expBallUsed = 0,
        long openHousePack = 0,
        long? legacyRuneFragmentCompleteRuneIndex = null,
        long? legacyToughDrillSocketRoll = null,
        string? statusFlags = null)
    {
        var expBallUsedLine = expBallUsed == 0
            ? ""
            : $"ExpBallUsed={expBallUsed}\r\n";
        var openHousePackLine = openHousePack == 0
            ? ""
            : $"OpenHousePack={openHousePack}\r\n";
        var legacyRuneFragmentCompleteRuneIndexLine = legacyRuneFragmentCompleteRuneIndex is null
            ? ""
            : $"LegacyRuneFragmentCompleteRuneIndex={legacyRuneFragmentCompleteRuneIndex.Value}\r\n";
        var legacyToughDrillSocketRollLine = legacyToughDrillSocketRoll is null
            ? ""
            : $"LegacyToughDrillSocketRoll={legacyToughDrillSocketRoll.Value}\r\n";
        var maxManaLine = maxMana is null
            ? ""
            : $"MaxMana={maxMana.Value}\r\n";
        var statusFlagsLine = statusFlags is null
            ? ""
            : $"StatusFlags={statusFlags}\r\n";
        File.WriteAllText(
            Path.Combine(_usersRoot, accountId.ToString(CultureInfo.InvariantCulture) + ".ini"),
            $$"""
            [Character]
            UID={{uid}}
            Body=1003
            Face=154
            Name=HeroOne
            Class=7051
            Map={{mapId}}
            X={{x}}
            Y={{y}}
            FirstClass=0
            SecoundeClass=7050
            Conquer=0
            Reborn=2
            Level={{level}}
            Haire=27
            Experience={{experience}}
            PMap=1000
            PMapX=500
            PMapY=650
            Agility=100
            Strength=100
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints={{minHitPoints}}
            MinMana={{minMana}}
            {{maxManaLine}}
            {{statusFlagsLine}}
            ConquerPoints=0
            BankCps=123
            BoundConquerPoints=0
            Money=50000
            PkPoints=0
            {{expBallUsedLine}}
            {{openHousePackLine}}
            {{legacyRuneFragmentCompleteRuneIndexLine}}
            {{legacyToughDrillSocketRollLine}}
            Online=7

            [PaidNobility]
            Rank=King
            Identifier={{uid}}
            """);
    }

    private static string BuildStatusFlags(params int[] flags)
    {
        var words = new ulong[10];
        foreach (var flag in flags)
        {
            var wordIndex = flag / 32;
            var bitIndex = flag % 32;
            words[wordIndex] |= 1UL << bitIndex;
        }

        return string.Join('/', words) + "/";
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find WolfConquer.sln from test base directory.");
    }

    private static WorldDataCatalog CreateWorldDataCatalog(params ItemType[] items)
    {
        return CreateWorldDataCatalogWithRuneLevelExperience([], items);
    }

    private static WorldDataCatalog CreateWorldDataCatalogWithRuneLevelExperience(
        IReadOnlyList<RuneLevelExperience> runeLevelExperience,
        params ItemType[] items)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-05T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://non-equipment-item-use",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: items,
            itemAddBonuses: [],
            levelExperience: [],
            runeLevelExperience: runeLevelExperience,
            runeStorageAttributes: [],
            itemRefineEffects: [],
            itemRefineEffectEx: [],
            itemRefineUpgrades: [],
            transformInfo: [],
            furniture: [],
            hairColorTypes: [],
            hairFaceStorageTypes: [],
            coatStorageTypes: [],
            coatStorageAttributes: [],
            coatColorRules: [],
            xuanbaoAdditionAttributes: [],
            xuanbaoComposeAttributeLimits: [],
            xuanbaoRandomAttributes: [],
            christmasNpcs: [],
            tutorTypes: [],
            tutorBattleLimitTypes: [],
            beastAttributes: [],
            fateExperience: [],
            spiritRates: [],
            hundredWeapons: [],
            gouyuTypes: [],
            gouyuImmortals: [],
            specialEffectAttributes: [],
            daoqiDictTypes: [],
            syndicateFormationTypes: [],
            syndicateSkillTypes: [],
            disdainRules: [],
            jiangHuAttributes: [],
            jiangHuCultivateStatuses: [],
            refineryBoxes: [],
            refinerySouls: [],
            refineryMaterials: [],
            stateCountFiles: [],
            binarySeedFiles: [],
            emptySeedFiles: [],
            backupSeedFiles: [],
            pokerTables: [],
            rouletteTables: [],
            npcTimeEntries: [],
            activityTasks: new ActivityTaskCatalog(new ActivityTaskMetadata(0, "", 0, []), [], "fixture://activity"),
            meltingTypeRules: [],
            lotteryPrizes: [],
            arenaRecords: [],
            elitePkRecords: [],
            teamElitePkRecords: [],
            skillTeamPkRecords: [],
            classPkWarRecords: [],
            combatGearRanks: [],
            prestigeRanks: [],
            innerPowerRanks: [],
            runeRankings: [],
            staticStatues: [],
            newPlayerRewards: [],
            votes: [],
            couplesPkRecords: [],
            socialAssociations: [],
            guildWarInfo: EmptyGuildWarData("guildwar"),
            superGuildWarInfo: EmptyGuildWarData("superguildwar"),
            classLevelStats: [],
            professionLevelUps: [],
            professionLevelBenefits: [],
            professionTitleBenefits: [],
            questInfo: new QuestInfoMetadata(ActionBase: null, RecommendInterval: null, TotalMission: null),
            questTaskTypes: [],
            questMissions: [],
            quizQuestions: [],
            magic: [],
            magicEffects: [],
            magicTypeOperations: [],
            titleTypes: [],
            combatGear: [],
            traps: [],
            npcs: [],
            sobNpcs: [],
            portals: [],
            maps: [],
            mapNames: [],
            monsters: [],
            monsterSpawns: [],
            lotteryDictionary: [],
            collectionRewardTiers: [],
            taskRewardTypes: [],
            taskRewardTypeIniEntries: [],
            neiGongHeader: new NeiGongHeader(0, [], "fixture://neigong", 0, "", new Dictionary<string, string>()),
            neiGongSecrets: [],
            neiGongTypes: [],
            subProfessions: [],
            syndicateLevels: [],
            stageGoalInfo: new StageGoalInfo(0),
            stageGoalStages: [],
            stageGoalTasks: [],
            shops: [],
            shopItems: [],
            exchangeShopGoods: [],
            exchangeShopGoodsEx: [],
            exchangeShopLevels: [],
            honorShopItems: [],
            honorShopRecommendations: [],
            racePointShopItems: [],
            racePointShopRecommendations: [],
            goldenLeagueShopItems: [],
            goldenLeagueShopRecommendations: [],
            goldenLeagueShopConfig: new Dictionary<string, string>(),
            booths: [],
            boothItems: []);
    }

    private static ItemType CreateItemType(uint itemTypeId, string name, ushort durability)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemTypeId.ToString(CultureInfo.InvariantCulture);
        fields[1] = name;
        fields[ItemType.DurabilityFieldIndex] = durability.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)itemTypeId), name, fields);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    private static MsgItemUsagePacket CreateEquipUse(uint itemUid)
    {
        return new MsgItemUsagePacket(
            itemUid,
            DwParam: 0,
            MsgItemUsageAction.Equip,
            Timestamp: 511_043_495,
            DwParam2: 0,
            DwParam3: 0,
            DwParam4: 0);
    }

    private static MsgItemUsagePacket CreateRemoveInventoryUse(uint itemUid)
    {
        return new MsgItemUsagePacket(
            itemUid,
            DwParam: 0,
            MsgItemUsageAction.RemoveInventory,
            Timestamp: 511_043_495,
            DwParam2: 0,
            DwParam3: 0,
            DwParam4: 0);
    }

    private static byte[] CreateCreateSocketUse(uint itemUid, uint materialCount, params uint[] materialUids)
    {
        var writer = new PacketWriter(
            WorldPacketOpcodes.MsgItemUsage,
            MsgItemUsagePacket.PayloadLength + (materialUids.Length * sizeof(uint)));
        writer.WriteUInt32(0, itemUid);
        writer.WriteUInt64(4, 0);
        writer.WriteUInt16(12, (ushort)MsgItemUsageAction.CreateSocketItem);
        writer.WriteUInt32(14, 511_043_495);
        writer.WriteUInt32(18, materialCount);
        writer.WriteUInt32(22, 0);
        writer.WriteUInt32(26, 0);
        for (var index = 0; index < materialUids.Length; index++)
        {
            writer.WriteUInt32(MsgItemUsagePacket.PayloadLength + (index * sizeof(uint)), materialUids[index]);
        }

        return writer.ToArray();
    }

    private static byte[] CreateDecomposeRelicsUse(uint id, params uint[] itemUids)
    {
        var writer = new PacketWriter(
            WorldPacketOpcodes.MsgItemUsage,
            MsgItemUsagePacket.PayloadLength + (itemUids.Length * sizeof(uint)));
        writer.WriteUInt32(0, id);
        writer.WriteUInt64(4, 0);
        writer.WriteUInt16(12, (ushort)MsgItemUsageAction.DecomposeRelics);
        writer.WriteUInt32(14, 511_043_495);
        writer.WriteUInt32(18, checked((uint)itemUids.Length));
        writer.WriteUInt32(22, 0);
        writer.WriteUInt32(26, 0);
        for (var index = 0; index < itemUids.Length; index++)
        {
            writer.WriteUInt32(MsgItemUsagePacket.PayloadLength + (index * sizeof(uint)), itemUids[index]);
        }

        return writer.ToArray();
    }

    private static byte[] CreateMythsoulBagUse(
        uint itemUid,
        MsgItemUsageAction action,
        params uint[] args)
    {
        var writer = new PacketWriter(
            WorldPacketOpcodes.MsgItemUsage,
            MsgItemUsagePacket.PayloadLength + (args.Length * sizeof(uint)));
        writer.WriteUInt32(0, itemUid);
        writer.WriteUInt64(4, 0);
        writer.WriteUInt16(12, (ushort)action);
        writer.WriteUInt32(14, 511_043_495);
        writer.WriteUInt32(18, 0);
        writer.WriteUInt32(22, 0);
        writer.WriteUInt32(26, 0);
        for (var index = 0; index < args.Length; index++)
        {
            writer.WriteUInt32(MsgItemUsagePacket.PayloadLength + (index * sizeof(uint)), args[index]);
        }

        return writer.ToArray();
    }

    private static byte[] CreateRuneBagUse(
        uint itemUid,
        ulong dwParam,
        params uint[] args)
    {
        var writer = new PacketWriter(
            WorldPacketOpcodes.MsgItemUsage,
            MsgItemUsagePacket.PayloadLength + (args.Length * sizeof(uint)));
        writer.WriteUInt32(0, itemUid);
        writer.WriteUInt64(4, dwParam);
        writer.WriteUInt16(12, (ushort)MsgItemUsageAction.Rune);
        writer.WriteUInt32(14, 511_043_495);
        writer.WriteUInt32(18, 0);
        writer.WriteUInt32(22, 0);
        writer.WriteUInt32(26, 0);
        for (var index = 0; index < args.Length; index++)
        {
            writer.WriteUInt32(MsgItemUsagePacket.PayloadLength + (index * sizeof(uint)), args[index]);
        }

        return writer.ToArray();
    }

    private static byte[] CreateRuneEquipmentUse(
        uint itemUid,
        ulong dwParam,
        MsgItemUsageAction action,
        params uint[] args)
    {
        var writer = new PacketWriter(
            WorldPacketOpcodes.MsgItemUsage,
            MsgItemUsagePacket.PayloadLength + (args.Length * sizeof(uint)));
        writer.WriteUInt32(0, itemUid);
        writer.WriteUInt64(4, dwParam);
        writer.WriteUInt16(12, (ushort)action);
        writer.WriteUInt32(14, 511_043_495);
        writer.WriteUInt32(18, 0);
        writer.WriteUInt32(22, 0);
        writer.WriteUInt32(26, 0);
        for (var index = 0; index < args.Length; index++)
        {
            writer.WriteUInt32(MsgItemUsagePacket.PayloadLength + (index * sizeof(uint)), args[index]);
        }

        return writer.ToArray();
    }

    private static byte[] CreateRuneDecomposeUse(uint packetId, params uint[] args)
    {
        var writer = new PacketWriter(
            WorldPacketOpcodes.MsgItemUsage,
            MsgItemUsagePacket.PayloadLength + (args.Length * sizeof(uint)));
        writer.WriteUInt32(0, packetId);
        writer.WriteUInt64(4, 0);
        writer.WriteUInt16(12, (ushort)MsgItemUsageAction.DecomposeRune);
        writer.WriteUInt32(14, 511_043_495);
        writer.WriteUInt32(18, checked((uint)args.Length));
        writer.WriteUInt32(22, 0);
        writer.WriteUInt32(26, 0);
        for (var index = 0; index < args.Length; index++)
        {
            writer.WriteUInt32(MsgItemUsagePacket.PayloadLength + (index * sizeof(uint)), args[index]);
        }

        return writer.ToArray();
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static WorldSession GetSession(WorldServerFrameHandler handler, ConquerTcpConnection connection)
    {
        var field = typeof(WorldServerFrameHandler).GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var sessions = Assert.IsType<WorldSessionStore>(field.GetValue(handler));
        Assert.True(sessions.TryGet(connection, out var session));
        Assert.NotNull(session);
        return session;
    }

    private static void AssertItemUsage(byte[] bytes, MsgItemUsagePacket expected)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(expected, packet);
    }

    private static void AssertLegacyTeleportTransition(
        List<byte[]> sent,
        int startIndex,
        long expectedMapId,
        long expectedX,
        long expectedY)
    {
        var expectedMap = checked((uint)expectedMapId);
        var expectedPositionX = checked((uint)expectedX);
        var expectedPositionY = checked((uint)expectedY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[startIndex]), out var teleport));
        Assert.Equal(MsgActionType.Teleport, teleport.Type);
        Assert.Equal(expectedMap, teleport.DwParam);
        Assert.Equal(expectedPositionX, teleport.PositionX);
        Assert.Equal(expectedPositionY, teleport.PositionY);
        Assert.Equal(expectedMap, teleport.DwParam3);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[startIndex + 1]), out var merchantStartup));
        Assert.Equal(MsgActionType.LegacyMerchantStartup, merchantStartup.Type);
        Assert.Equal(2U, merchantStartup.DwParam);
        Assert.Equal(expectedMap, merchantStartup.DwParam3);
        Assert.Equal(expectedPositionX, merchantStartup.PositionX);
        Assert.Equal(expectedPositionY, merchantStartup.PositionY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[startIndex + 2]), out var stopVending));
        Assert.Equal(MsgActionType.StopVending, stopVending.Type);
        Assert.Equal(expectedMap, stopVending.DwParam);
        Assert.Equal(expectedMap, stopVending.DwParam3);
        Assert.Equal(expectedPositionX, stopVending.PositionX);
        Assert.Equal(expectedPositionY, stopVending.PositionY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[startIndex + 3]), out var color));
        Assert.Equal(MsgActionType.LegacyColor, color.Type);
        Assert.Equal(expectedPositionX, color.PositionX);
        Assert.Equal(expectedPositionY, color.PositionY);

        Assert.True(LegacyMapStatusPacket.TryParse(ReadFrame(sent[startIndex + 4]), out var mapStatus));
        Assert.Equal(LegacyMapStatusPacket.FromMap(expectedMapId, null), mapStatus);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
