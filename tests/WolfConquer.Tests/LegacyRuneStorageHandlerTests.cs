using System.Buffers;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyRuneStorageHandlerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wolf-rune-storage-handler-" + Guid.NewGuid());
    private readonly string _usersRoot;
    private readonly string _itemsRoot;

    public LegacyRuneStorageHandlerTests()
    {
        _usersRoot = Path.Combine(_root, "Users");
        _itemsRoot = Path.Combine(_root, "PlayersItems");
        Directory.CreateDirectory(_usersRoot);
        Directory.CreateDirectory(_itemsRoot);
    }

    [Fact]
    public Task HandlerMovesRuneBagItemToStorageSlotAndPersists() => AssertOwnedStorageMoveAsync(0, 5_450_001, 4_010_501, 211, 212, 7000, 1425000);

    [Fact]
    public Task HandlerMovesStorageRuneBackToBagAndPersists() => AssertOwnedStorageMoveAsync(1, 5_450_010, 4_020_501, 212, 211, 6000, 1416000);

    [Fact]
    public async Task HandlerMovesInventoryRuneIntoStorageAndPersists()
    {
        const uint runeUid = 5_450_020;
        WriteSnapshot(reborn: 2);
        WriteItems(PlayerItemSnapshotLoaderTests.DbItem(runeUid, 4_030_501, position: 0));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyRuneStoragePacket(3, runeUid, 0).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 21, sent.Count);
        LegacySelectedEquipmentQueryAssertions.NeutralQuery(sent, countBeforeRequest, 1_293_600U);
        AssertRuneStorage(sent[countBeforeRequest + 10], type: 3, itemUid: runeUid, hpAdd: 0);
        LegacySelectedEquipmentQueryAssertions.NeutralQuery(sent, countBeforeRequest + 11, 1_293_600U);

        var session = GetSession(handler, connection);
        Assert.DoesNotContain(session.PlayerItems.InventoryItems, item => item.Uid == runeUid);
        Assert.Contains(session.PlayerItems.RuneItems, item => item.Uid == runeUid && item.Position == 212);
        var persisted = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(1_293_600);
        Assert.DoesNotContain(persisted.InventoryItems, item => item.Uid == runeUid);
        Assert.Contains(persisted.RuneItems, item => item.Uid == runeUid && item.Position == 212);

        var output = log.ToString();
        Assert.Contains("handled LegacyRuneStorage", output);
        Assert.Contains("type=3", output);
        Assert.Contains("changed=True", output);
        Assert.Contains("persisted MsgItemUsage rune storage items", output);
    }

    [Fact]
    public async Task HandlerBlocksRuneStorageForFirstRebornCharacters()
    {
        const uint runeUid = 5_450_030;
        WriteSnapshot(reborn: 1);
        WriteItems(PlayerItemSnapshotLoaderTests.DbItem(runeUid, 4_010_501, position: 211));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyRuneStoragePacket(0, runeUid, 0).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 1, sent.Count);
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(sent[countBeforeRequest]), out var message));
        Assert.Contains(
            message.Content,
            content => content.Contains("second reborn hero", StringComparison.Ordinal));

        var session = GetSession(handler, connection);
        Assert.Contains(session.PlayerItems.RuneItems, item => item.Uid == runeUid && item.Position == 211);
        var output = log.ToString();
        Assert.Contains("skipped LegacyRuneStorage", output);
        Assert.Contains("requires second reborn", output);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<(WorldServerFrameHandler Handler, ConquerTcpConnection Connection, List<byte[]> Sent)> EnterWorldAsync(
        TextWriter log)
    {
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(1_293_600, "user02", ConquerRuntimeDefaults.WorldServer);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot), handoff, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));

        return (handler, connection, sent);
    }

    private void WriteItems(params PlayerItemSnapshotLoaderTests.TestDbItem[] items)
    {
        File.WriteAllBytes(
            Path.Combine(_itemsRoot, "1293600.bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(items, []));
    }

    private void WriteSnapshot(long reborn)
    {
        File.WriteAllText(
            Path.Combine(_usersRoot, "1293600.ini"),
            $$"""
            [Character]
            UID=1293600
            Body=1008
            Face=154
            Name=HeroOne
            Class=7051
            Map=1015
            X=830
            Y=584
            FirstClass=0
            SecoundeClass=7050
            Conquer=0
            Reborn={{reborn}}
            Level=140
            Haire=27
            Experience=0
            PMap=1000
            PMapX=500
            PMapY=650
            Agility=36
            Strength=0
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints=61258
            MinMana=5000
            ConquerPoints=0
            BankCps=123
            BoundConquerPoints=0
            Money=50000
            PkPoints=0
            Online=7

            [PaidNobility]
            Rank=King
            Identifier=1293600
            """);
    }

    private static void AssertRuneStorage(byte[] bytes, byte type, uint itemUid, uint hpAdd)
    {
        Assert.True(LegacyRuneStoragePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(type, packet.Type);
        Assert.Equal(itemUid, packet.ItemUID);
        Assert.Equal(hpAdd, packet.HPAdd);
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

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
