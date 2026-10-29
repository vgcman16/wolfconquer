using System.Buffers;
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyConsumableManaPersistenceParityTests : IDisposable
{
    private const long AccountId = 1_293_600;
    private const uint CharacterUid = 1_293_600;

    private static readonly Lazy<Task<WorldDataCatalog>> SeedWorldData = new(LoadSeedWorldDataAsync);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "wolf-consumable-mana-persistence-" + Guid.NewGuid());
    private readonly string _usersRoot;
    private readonly string _itemsRoot;

    public LegacyConsumableManaPersistenceParityTests()
    {
        _usersRoot = Path.Combine(_root, "Users");
        _itemsRoot = Path.Combine(_root, "PlayersItems");
        Directory.CreateDirectory(_usersRoot);
        Directory.CreateDirectory(_itemsRoot);
    }



    [Fact]
    public Task FullRestoreItemRestoresHitpointsAndManaAndPersistsCharacterAndStackedItemToDisk()
    {
        const uint itemUid = 5_337_121, maxHitpoints = 100_000;
        var character = OwnedEquipmentBehaviorFixture.Character(name: "OwnedLegacyFullRestore", reborn: 0, minHitPoints: 123) with
        {
            MinMana = 40, MaxMana = 5000,
            Attributes = new Dictionary<string, string> { ["MaxHitpoints"] = "100000" }
        };
        var items = new PlayerItemSnapshot([new() { Uid = itemUid, ItemId = 723726, StackSize = 2,
            Durability = 1, MaximumDurability = 1 }], [], [], [], []);
        var catalog = OwnedEquipmentBehaviorFixture.Catalog(OwnedEquipmentBehaviorFixture.Item(723726, "OwnedLegacyFullRestore",
            new Dictionary<int, string> { [ItemType.StackSizeFieldIndex] = "10", [ItemType.DurabilityFieldIndex] = "1" }));
        return OwnedEquipmentBehaviorFixture.RunAsync(new(character, items), catalog, async fixture =>
        {
            var sent = fixture.ActorFrames; var countBeforeRequest = sent.Count;
            await fixture.DispatchAsync(CreateEquipUse(itemUid));
            Assert.Equal(countBeforeRequest + 4, sent.Count);
            AssertStackUpdate(sent[countBeforeRequest], itemUid, itemId: 723_726, stackSize: 1);
            AssertScalarUpdate(sent[countBeforeRequest + 1], LegacyMsgUpdateDataType.MaxHitpoints, maxHitpoints);
            AssertHitpointUpdate(sent[countBeforeRequest + 2], maxHitpoints, maxHitpoints);
            AssertScalarUpdate(sent[countBeforeRequest + 3], LegacyMsgUpdateDataType.Mana, value: 5_000);
            var reloaded = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(fixture.CharacterPath()), out _));
            Assert.Equal((long)maxHitpoints, reloaded.MinHitPoints); Assert.Equal(5000L, reloaded.MinMana); Assert.Equal(5000L, reloaded.MaxMana);
            Assert.Equal((long)maxHitpoints, fixture.Current().Character.MinHitPoints); Assert.Equal(5000L, fixture.Current().Character.MinMana);
            Assert.Equal((int)maxHitpoints, Assert.IsType<LegacyNativePlayerRuntime>(fixture.Current().NativePlayer).HitPoints);
            var potion = Assert.Single(new PlayerItemSnapshotLoader(fixture.Data.Items).LoadByCharacterUid(CharacterUid).InventoryItems,
                item => item.Uid == itemUid);
            Assert.Equal(723_726U, potion.ItemId); Assert.Equal((ushort)1, potion.StackSize);
            Assert.False(fixture.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(fixture.Data.Users));
            Assert.DoesNotContain("runtime exception", fixture.Log.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PARITY-BUG", fixture.Log.ToString(), StringComparison.Ordinal);
        });
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
        var worldData = await SeedWorldData.Value.ConfigureAwait(false);
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

    private CharacterSnapshot AssertLoadedCharacter()
    {
        var reloadedCharacter = new CharacterSnapshotLoader(_usersRoot, _itemsRoot).LoadByAccountId(AccountId);
        Assert.NotNull(reloadedCharacter);
        return reloadedCharacter;
    }

    private void WriteItems(params PlayerItemSnapshotLoaderTests.TestDbItem[] items)
    {
        File.WriteAllBytes(
            Path.Combine(_itemsRoot, CharacterUid.ToString(CultureInfo.InvariantCulture) + ".bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(items, []));
    }

    private void WriteSnapshot(
        long minHitPoints,
        long minMana,
        long maxMana,
        uint? maxHitpoints = null)
    {
        var maxHitpointsLine = maxHitpoints is null
            ? ""
            : $"MaxHitpoints={maxHitpoints.Value}\r\n";

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
            {{maxHitpointsLine}}MinMana={{minMana}}
            MaxMana={{maxMana}}
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

    private static async Task<WorldDataCatalog> LoadSeedWorldDataAsync()
    {
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        Assert.True(
            File.Exists(Path.Combine(seedPath, "manifest.json")),
            $"Expected seed world data at {seedPath}.");

        return await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync().ConfigureAwait(false);
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        var startPaths = new[]
        {
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory(),
            Path.GetDirectoryName(sourceFilePath) ?? string.Empty,
        };

        foreach (var startPath in startPaths.Where(path => path.Length > 0).Distinct())
        {
            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate WolfConquer.sln from the test output, working, or source directory.");
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

    private static void AssertStackUpdate(byte[] bytes, uint itemUid, uint itemId, ushort stackSize)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(itemUid, packet.Uid);
        Assert.Equal(itemId, packet.ItemId);
        Assert.Equal(stackSize, packet.StackSize);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, packet.Mode);
    }

    private static void AssertScalarUpdate(byte[] bytes, LegacyMsgUpdateDataType type, ulong value)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
        var update = Assert.Single(packet.Updates);
        Assert.Equal(type, update.Type);
        Assert.Equal(value, update.Value);
    }

    private static void AssertHitpointUpdate(byte[] bytes, uint maxHitpoints, uint hitpoints)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(
            [
                new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, maxHitpoints),
                new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Hitpoints, hitpoints),
            ],
            packet.Updates);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
