using System.Globalization;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    private const long RuneRankPlayerA = 1_293_600;
    private const long RuneRankPlayerB = 1_293_601;
    private const uint RuneRankTargetUid = 5_348_900;
    private const uint RuneRankStoredUid = 5_348_901;
    private const uint RuneRankMaterialUid = 5_348_902;
    private const uint RuneRankTargetId = 4_020_514;
    private const uint RuneRankStoredId = 4_020_615;
    private const uint RuneRankMaterialId = 4_040_001;

    [Fact]
    public async Task HandlerRuneComposeUpdatesSharedRankBeforeOtherPlayerQuery()
    {
        await AssertRuneRankingComposeAndOtherPlayerQueryAsync(
            [RuneRankSeed(RuneRankPlayerB, 10_000)],
            beforeRank: 1, afterRank: 2);
    }

    [Fact]
    public async Task HandlerRuneComposeEvictsHundredthPlayerFromSharedRank()
    {
        await AssertRuneRankingComposeAndOtherPlayerQueryAsync(
            RuneRankFullSeed(), beforeRank: 100, afterRank: null);
    }

    [Fact]
    public async Task HandlerRuneRankStartupAndFirstQueryDoNotEagerlyUpdateLoadedScore()
    {
        WriteRuneRankingPlayer(RuneRankPlayerA, targetLevel: 16, material: false);
        var catalog = CreateRuneRankingCatalog(
            [RuneRankSeed(RuneRankPlayerB, 10_100), RuneRankSeed(RuneRankPlayerA, 10_000)]);
        using var log = new StringWriter();
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot, _itemsRoot), handoff, catalog, log);
        var (connection, sent) = await EnterRuneRankingPlayerAsync(handler, handoff, RuneRankPlayerA);

        AssertRuneRankingStartup(sent, rank: 2, score: 10_300);
        var beforeItems = GetSession(handler, connection).PlayerItems;
        var beforeBytes = File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA));
        await AssertRuneRankingQueryAsync(handler, connection, sent, RuneRankPlayerA, rank: 2, score: 10_300);
        await AssertRuneRankingQueryAsync(handler, connection, sent, RuneRankPlayerA, rank: 1, score: 10_300);
        Assert.Same(beforeItems, GetSession(handler, connection).PlayerItems);
        Assert.Equal(beforeBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
    }

    private async Task AssertRuneRankingComposeAndOtherPlayerQueryAsync(
        IReadOnlyList<RuneRankingRecord> seed, int beforeRank, int? afterRank)
    {
        WriteRuneRankingPlayer(RuneRankPlayerA, targetLevel: 14, material: true);
        WriteRuneRankingPlayer(RuneRankPlayerB, targetLevel: 15, material: false);
        var catalog = CreateRuneRankingCatalog(seed);
        using var log = new StringWriter();
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot, _itemsRoot), handoff, catalog, log);
        var (connectionA, sentA) = await EnterRuneRankingPlayerAsync(handler, handoff, RuneRankPlayerA);
        var (connectionB, sentB) = await EnterRuneRankingPlayerAsync(handler, handoff, RuneRankPlayerB);
        AssertRuneRankingStartup(sentA, rank: null, score: 9_700);
        AssertRuneRankingStartup(sentB, rank: beforeRank, score: 10_000);
        var originalA = GetSession(handler, connectionA).PlayerItems;
        var originalB = GetSession(handler, connectionB).PlayerItems;
        Assert.Equal(9_700U, LegacyRuneStoragePacket.ComputeStorageScore(originalA));
        Assert.Equal(10_000U, LegacyRuneStoragePacket.ComputeStorageScore(originalB));
        var originalABytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalA);
        Assert.Equal(originalABytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerA)));
        var originalBBytes = File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(originalB), originalBBytes);
        await AssertRuneRankingQueryAsync(
            handler, connectionB, sentB, RuneRankPlayerB, rank: beforeRank, score: 10_000);
        var beforeComposeA = sentA.Count;
        var beforeComposeB = sentB.Count;

        await handler.HandleFrameAsync(connectionA, ReadFrame(CreateRuneRankingComposeUse()));

        // Native ComposeRune sends the material removal and target UPDATE, but no rank text.
        Assert.Equal(beforeComposeA + 2, sentA.Count);
        Assert.Equal(beforeComposeB, sentB.Count);
        AssertItemUsage(sentA[beforeComposeA], new MsgItemUsagePacket(
            RuneRankMaterialUid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0));
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sentA[beforeComposeA + 1]), out var update));
        Assert.Equal(RuneRankTargetUid, update.Uid);
        Assert.Equal(RuneRankTargetId + 2, update.ItemId);
        Assert.Equal(0U, update.RuneExperience);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, update.Mode);
        var changedA = GetSession(handler, connectionA).PlayerItems;
        Assert.Equal(10_300U, LegacyRuneStoragePacket.ComputeStorageScore(changedA));
        Assert.Empty(changedA.InventoryItems);
        Assert.Equal(RuneRankTargetId + 2,
            Assert.Single(changedA.RuneItems, item => item.Uid == RuneRankTargetUid).ItemId);
        Assert.Equal(originalABytes, PlayerItemSnapshotLoader.ToLegacyBytes(originalA));
        var reloadedA = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(RuneRankPlayerA);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(changedA),
            PlayerItemSnapshotLoader.ToLegacyBytes(reloadedA));
        Assert.Equal(10_300U, LegacyRuneStoragePacket.ComputeStorageScore(reloadedA));

        await AssertRuneRankingQueryAsync(
            handler, connectionB, sentB, RuneRankPlayerB, rank: afterRank, score: 10_000);
        await AssertRuneRankingQueryAsync(
            handler, connectionA, sentA, RuneRankPlayerA, rank: afterRank is null ? 100 : 1, score: 10_300);
        Assert.Same(originalB, GetSession(handler, connectionB).PlayerItems);
        Assert.Equal(originalBBytes, File.ReadAllBytes(RuneRankingItemsPath(RuneRankPlayerB)));
        Assert.Contains("handled MsgItemUsage compose rune", log.ToString());
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage compose rune", log.ToString());
    }

    private void WriteRuneRankingPlayer(long uid, uint targetLevel, bool material)
    {
        WriteSnapshot(accountId: uid, uid: uid, mapId: uid == RuneRankPlayerA ? 1_015 : 1_002, x: 826, y: 565);
        var items = new List<PlayerItemSnapshotLoaderTests.TestDbItem>();
        var itemUidOffset = uid == RuneRankPlayerA ? 0U : 100U;
        if (material)
            items.Add(PlayerItemSnapshotLoaderTests.DbItem(
                RuneRankMaterialUid, RuneRankMaterialId, position: 0,
                durability: 100, maximumDurability: 100, stackSize: 1));
        // Physical canonical order: inventory first, then counted rune102, then storage212.
        items.Add(PlayerItemSnapshotLoaderTests.DbItem(
            RuneRankTargetUid + itemUidOffset, RuneRankTargetId - 14 + targetLevel, position: 102,
            durability: 100, maximumDurability: 100, stackSize: 1));
        items.Add(PlayerItemSnapshotLoaderTests.DbItem(
            RuneRankStoredUid + itemUidOffset, RuneRankStoredId, position: 212,
            durability: 100, maximumDurability: 100, stackSize: 1));
        WriteItems(uid, items.ToArray());
    }

    private static async Task<(ConquerTcpConnection Connection, List<byte[]> Sent)> EnterRuneRankingPlayerAsync(
        WorldServerFrameHandler handler, AccountHandoffService handoff, long uid)
    {
        var ticket = handoff.Issue(uid, "rune-ranking-" + uid, ConquerRuntimeDefaults.WorldServer);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        await handler.HandleFrameAsync(
            connection, ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));
        return (connection, sent);
    }

    private static void AssertRuneRankingStartup(IReadOnlyList<byte[]> sent, int? rank, uint score)
    {
        var rankFrames = sent.Select(ReadFrame)
            .Where(frame => LegacyMsgMessagePacket.TryParse(frame, out var message) &&
                message.Content[3].StartsWith("My ranking: ", StringComparison.Ordinal))
            .ToArray();
        var rankFrame = Assert.Single(rankFrames);
        Assert.True(LegacyMsgMessagePacket.TryParse(rankFrame, out var packet));
        Assert.Equal(LegacyMsgMessagePacket.RuneRankChatType, packet.ChatType);
        Assert.Equal(RuneRankingText(rank, score), packet.Content[3]);
    }

    private static async Task AssertRuneRankingQueryAsync(
        WorldServerFrameHandler handler, ConquerTcpConnection connection, List<byte[]> sent,
        long uid, int? rank, uint score)
    {
        var before = sent.Count;
        await handler.HandleFrameAsync(connection, ReadFrame(LegacyRuneStoragePacket.Startup().ToFrameBytes()));
        // Exact native order: rank text, storage response, ten-frame neutral equipment query.
        Assert.Equal(before + 12, sent.Count);
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(sent[before]), out var message));
        Assert.Equal(LegacyMsgMessagePacket.RuneRankChatType, message.ChatType);
        Assert.Equal(RuneRankingText(rank, score), message.Content[3]);
        Assert.True(LegacyRuneStoragePacket.TryParse(ReadFrame(sent[before + 1]), out var storage));
        Assert.Equal(LegacyRuneStoragePacket.Startup(), storage);
        LegacySelectedEquipmentQueryAssertions.NeutralQuery(sent, before + 2, checked((uint)uid));
    }

    private string RuneRankingItemsPath(long uid) =>
        Path.Combine(_itemsRoot, uid.ToString(CultureInfo.InvariantCulture) + ".bin");

    private static string RuneRankingText(int? rank, uint score) =>
        rank is null
            ? $"My ranking: Unranked; Current rune pack score: {score}"
            : $"My ranking: {rank.Value}; Current rune pack score: {score}";

    private static PlayerItemSnapshot RuneRankItems(uint targetLevel) => new(
        InventoryItems: [], RelicFuseItems: [], MythsoulBagItems: [], EquipmentItems: [],
        RuneItems:
        [
            new PlayerItemSnapshotItem
            {
                Uid = RuneRankTargetUid, ItemId = RuneRankTargetId - 14 + targetLevel, Position = 102,
                Durability = 100, MaximumDurability = 100, StackSize = 1,
            },
            new PlayerItemSnapshotItem
            {
                Uid = RuneRankStoredUid, ItemId = RuneRankStoredId, Position = 212,
                Durability = 100, MaximumDurability = 100, StackSize = 1,
            },
        ]);

    private static byte[] CreateRuneRankingComposeUse()
    {
        var writer = new PacketWriter(WorldPacketOpcodes.MsgItemUsage, MsgItemUsagePacket.PayloadLength + sizeof(uint));
        writer.WriteUInt32(0, RuneRankTargetUid);
        writer.WriteUInt64(4, 0);
        writer.WriteUInt16(12, (ushort)MsgItemUsageAction.ComposeRune);
        writer.WriteUInt32(14, 511_043_495);
        writer.WriteUInt32(18, 1);
        writer.WriteUInt32(22, 0);
        writer.WriteUInt32(26, 0);
        writer.WriteUInt32(MsgItemUsagePacket.PayloadLength, RuneRankMaterialUid);
        return writer.ToArray();
    }

    private static CharacterSnapshot RuneRankCharacter(long uid, string name) =>
        CharacterSnapshotParser.ParseText($$"""
            [Character]
            UID={{uid}}
            Body=1003
            Face=154
            Name={{name}}
            Class=7051
            Map=1015
            X=826
            Y=565
            FirstClass=0
            SecoundeClass=7050
            Reborn=2
            Level=140
            Haire=27
            Experience=0
            PMap=1000
            PMapX=500
            PMapY=650
            Agility=100
            Strength=100
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints=10000
            MinMana=5000
            ConquerPoints=0
            BankCps=0
            BoundConquerPoints=0
            Money=0
            PkPoints=0
            Online=0
            """);
}
