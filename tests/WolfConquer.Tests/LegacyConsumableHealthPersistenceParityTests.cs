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

public sealed class LegacyConsumableHealthPersistenceParityTests : IDisposable
{
    private const long AccountId = 1_293_600;
    private const uint CharacterUid = 1_293_600;
    private const uint StancherItemUid = 1_000_000;
    private const uint StancherItemId = 1_000_000;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "wolf-consumable-health-persistence-" + Guid.NewGuid());
    private readonly string _usersRoot;
    private readonly string _itemsRoot;

    public LegacyConsumableHealthPersistenceParityTests()
    {
        _usersRoot = Path.Combine(_root, "Users");
        _itemsRoot = Path.Combine(_root, "PlayersItems");
        Directory.CreateDirectory(_usersRoot);
        Directory.CreateDirectory(_itemsRoot);
    }

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task HealthPotionUsePersistsHitpointsAndConsumedStackToLegacySnapshots()
    {
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        if (!File.Exists(Path.Combine(seedPath, "manifest.json")))
        {
            return;
        }

        var worldData = await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync();
        WriteSnapshot(minHitPoints: 10);
        WriteItems(PlayerItemSnapshotLoaderTests.DbItem(
            StancherItemUid,
            StancherItemId,
            position: 0,
            stackSize: 2));
        using var log = new StringWriter();
        var (handler, connection, sent) = await EnterWorldAsync(log, worldData);

        var countBeforeRequest = sent.Count;
        await handler.HandleFrameAsync(connection, ReadFrame(CreateEquipUse(StancherItemUid).ToFrameBytes()));

        Assert.True(sent.Count >= countBeforeRequest + 2);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[countBeforeRequest]), out var stackUpdate));
        Assert.Equal(StancherItemUid, stackUpdate.Uid);
        Assert.Equal(StancherItemId, stackUpdate.ItemId);
        Assert.Equal(1, stackUpdate.StackSize);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, stackUpdate.Mode);

        var session = GetSession(handler, connection);
        Assert.Equal(80, session.Character.MinHitPoints);
        var sessionPotion = Assert.Single(session.PlayerItems.InventoryItems, item => item.Uid == StancherItemUid);
        Assert.Equal(1, sessionPotion.StackSize);

        var reloadedCharacter = new CharacterSnapshotLoader(_usersRoot, _itemsRoot).LoadByAccountId(AccountId);
        Assert.NotNull(reloadedCharacter);
        Assert.Equal(80, reloadedCharacter.MinHitPoints);

        var reloadedItems = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(CharacterUid);
        var reloadedPotion = Assert.Single(reloadedItems.InventoryItems, item => item.Uid == StancherItemUid);
        Assert.Equal(StancherItemId, reloadedPotion.ItemId);
        Assert.Equal(1, reloadedPotion.StackSize);
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
        WorldDataCatalog worldData)
    {
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(AccountId, "user02", ConquerRuntimeDefaults.WorldServer);
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot, _itemsRoot),
            handoff,
            worldData,
            log);
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
            Path.Combine(_itemsRoot, CharacterUid.ToString(CultureInfo.InvariantCulture) + ".bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(items, []));
    }

    private void WriteSnapshot(long minHitPoints)
    {
        File.WriteAllText(
            Path.Combine(_usersRoot, AccountId.ToString(CultureInfo.InvariantCulture) + ".ini"),
            $$"""
            [Character]
            UID={{CharacterUid}}
            Body=1003
            Face=154
            Name=HeroOne
            Class=7051
            Map=1015
            X=826
            Y=565
            FirstClass=0
            SecoundeClass=7050
            Conquer=0
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
            MinHitPoints={{minHitPoints}}
            MinMana=5000
            ConquerPoints=0
            BankCps=123
            BoundConquerPoints=0
            Money=50000
            PkPoints=0
            Online=7

            [PaidNobility]
            Rank=King
            Identifier={{CharacterUid}}
            """);
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
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
