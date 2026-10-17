using System.Buffers;
using System.Globalization;
using System.Net;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyFullAccountItemMutationFixture : IDisposable
{
    private const uint NpcId = 465_123;

    private readonly long _accountId;
    private readonly string _root;

    public LegacyFullAccountItemMutationFixture(string testName, long accountId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(testName);
        _accountId = accountId;
        _root = Path.Combine(
            Path.GetTempPath(),
            "wolf-full-account-" + testName + "-" + Guid.NewGuid().ToString("N"));
    }

    public async Task<InvokeResult> InvokeAsync(
        byte optionId,
        PlayerItemSnapshot originalItems,
        WorldDataCatalog? worldData,
        bool includeSessions = true,
        bool includeUsersRoot = true,
        bool includePlayerItemsRoot = true,
        int observerCount = 0,
        PersistenceLock persistenceLock = PersistenceLock.None)
    {
        var invocationRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        var usersRoot = Path.Combine(invocationRoot, "Users");
        var playerItemsRoot = Path.Combine(invocationRoot, "PlayersItems");
        Directory.CreateDirectory(usersRoot);
        new PlayerItemSnapshotLoader(playerItemsRoot)
            .SaveByCharacterUid(_accountId, originalItems);

        var characterPath = Path.Combine(
            usersRoot,
            _accountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        WriteCharacter(characterPath);
        var playerItemsPath = Path.Combine(
            playerItemsRoot,
            _accountId.ToString(CultureInfo.InvariantCulture) + ".bin");
        var originalCharacterBytes = File.ReadAllBytes(characterPath);
        var originalItemBytes = File.ReadAllBytes(playerItemsPath);

        var claimantSent = new List<byte[]>();
        var observerSent = new List<IReadOnlyList<byte[]>>();
        var exposureOrder = new List<string>();
        var claimant = CreateConnection(claimantSent, exposureOrder, "claimant", 9_958);
        var session = CreateSession(
            originalItems,
            includeUsersRoot ? usersRoot : null);
        var characterLoader = new CharacterSnapshotLoader(usersRoot);
        var itemLoader = new PlayerItemSnapshotLoader(playerItemsRoot);
        PersistedState? persistedAtCommit = null;
        var sessions = new WorldSessionStore((connection, previous, _) =>
        {
            if (!ReferenceEquals(connection, claimant) || previous is null)
            {
                return;
            }

            persistedAtCommit = new PersistedState(
                Assert.IsType<CharacterSnapshot>(characterLoader.LoadByAccountId(_accountId)),
                itemLoader.LoadByCharacterUid(_accountId));
            exposureOrder.Add("commit");
        });
        sessions.Set(claimant, session);

        for (var index = 0; index < observerCount; index++)
        {
            var sent = new List<byte[]>();
            var observer = CreateConnection(
                sent,
                exposureOrder,
                "observer" + index.ToString(CultureInfo.InvariantCulture),
                9_959 + index);
            sessions.Set(
                observer,
                session with
                {
                    AccountId = _accountId + index + 1,
                    Character = session.Character with
                    {
                        Uid = _accountId + index + 1,
                        Name = "FullAccountObserver" + index.ToString(CultureInfo.InvariantCulture),
                    },
                    UsersRoot = null,
                });
            observerSent.Add(sent);
        }

        FileStream? locked = persistenceLock switch
        {
            PersistenceLock.Character => OpenPersistenceLock(characterPath),
            PersistenceLock.Items => OpenPersistenceLock(playerItemsPath),
            _ => null,
        };

        bool handled;
        using var log = new StringWriter();
        try
        {
            handled = await WorldPacketHandlers.TryHandleLegacyAccountSecurityNpcAsync(
                new WorldPacketHandlerContext(
                    claimant,
                    log,
                    Session: session,
                    WorldData: worldData,
                    Sessions: includeSessions ? sessions : null,
                    PlayerItemsRoot: includePlayerItemsRoot ? playerItemsRoot : null),
                session,
                NpcId,
                optionId,
                input: string.Empty,
                CancellationToken.None);
        }
        finally
        {
            locked?.Dispose();
        }

        Assert.True(sessions.TryGet(claimant, out var stored));
        return new InvokeResult(
            handled,
            session,
            Assert.IsType<WorldSession>(stored),
            persistedAtCommit,
            Assert.IsType<CharacterSnapshot>(characterLoader.LoadByAccountId(_accountId)),
            itemLoader.LoadByCharacterUid(_accountId),
            claimantSent,
            observerSent,
            exposureOrder,
            log.ToString(),
            usersRoot,
            playerItemsRoot,
            characterPath,
            playerItemsPath,
            originalCharacterBytes,
            originalItemBytes);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    public static PlayerItemSnapshot Items(params PlayerItemSnapshotItem[] inventoryItems)
    {
        return new PlayerItemSnapshot(inventoryItems, [], [], [], []);
    }

    public static PlayerItemSnapshotItem InventoryItem(
        uint uid,
        uint itemId,
        ushort stackSize = 1,
        byte bound = 0,
        byte locked = 0,
        uint color = 5)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Durability = 1,
            MaximumDurability = 1,
            Position = 0,
            StackSize = stackSize,
            Bound = bound,
            Locked = locked,
            Color = color,
        };
    }

    public static ItemType ItemType(
        uint itemId,
        string name,
        uint durability,
        ushort stackLimit = 0)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemId.ToString(CultureInfo.InvariantCulture);
        fields[1] = name;
        fields[WolfConquer.Core.Data.ItemType.DurabilityFieldIndex] =
            durability.ToString(CultureInfo.InvariantCulture);
        fields[46] = stackLimit.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)itemId), name, fields);
    }

    public static void AssertPlayerItemsEqual(PlayerItemSnapshot expected, PlayerItemSnapshot actual)
    {
        Assert.Equal(
            PlayerItemSnapshotLoader.ToLegacyBytes(expected),
            PlayerItemSnapshotLoader.ToLegacyBytes(actual));
    }

    public static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(
            new ReadOnlySequence<byte>(bytes),
            out var frame,
            out _,
            out _));
        return frame;
    }

    private WorldSession CreateSession(
        PlayerItemSnapshot playerItems,
        string? usersRoot)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = "0",
            ["Sentinel"] = "keep-me",
        };
        return new WorldSession(
            _accountId,
            Username: "full-account-item-test",
            Character: new CharacterSnapshot(
                Uid: _accountId,
                Name: "FullAccountItemHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 15,
                FirstClass: 15,
                SecondClass: 0,
                Reborn: 2,
                Level: 140,
                Experience: 0,
                MapId: 1_002,
                X: 430,
                Y: 378,
                PreviousMapId: 1_002,
                PreviousMapX: 430,
                PreviousMapY: 378,
                Strength: 0,
                Agility: 0,
                Vitality: 0,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: 1,
                MinMana: 0,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: usersRoot is null ? "fixture" : Path.Combine(
                    usersRoot,
                    _accountId.ToString(CultureInfo.InvariantCulture) + ".ini"),
                Attributes: attributes,
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: playerItems,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private void WriteCharacter(string path)
    {
        File.WriteAllText(
            path,
            "[Character]\r\n" +
            $"UID={_accountId}\r\n" +
            "Name=FullAccountItemHero\r\nBody=1003\r\nFace=0\r\nHaire=0\r\nClass=15\r\n" +
            "FirstClass=15\r\nSecoundeClass=0\r\nReborn=2\r\nLevel=140\r\nExperience=0\r\n" +
            "Map=1002\r\nX=430\r\nY=378\r\nPMap=1002\r\nPMapX=430\r\nPMapY=378\r\n" +
            "Strength=0\r\nAgility=0\r\nVitaliti=0\r\nSpirit=0\r\nAtributes=0\r\n" +
            "MinHitPoints=1\r\nMinMana=0\r\nMoney=0\r\nConquerPoints=0\r\n" +
            "BoundConquerPoints=0\r\nBankCps=0\r\nPkPoints=0\r\nOnline=0\r\n" +
            "Sentinel=keep-me\r\n",
            Encoding.Latin1);
    }

    private static FileStream OpenPersistenceLock(string path)
    {
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    private static ConquerTcpConnection CreateConnection(
        List<byte[]> sent,
        List<string> exposureOrder,
        string label,
        int port)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, port), (frame, _) =>
        {
            var bytes = frame.ToArray();
            sent.Add(bytes);
            exposureOrder.Add(
                label + ":" + ReadFrame(bytes).Header.Opcode.ToString(CultureInfo.InvariantCulture));
            return ValueTask.CompletedTask;
        });
    }

    public enum PersistenceLock
    {
        None,
        Character,
        Items,
    }

    public sealed record PersistedState(CharacterSnapshot Character, PlayerItemSnapshot Items);

    public sealed record InvokeResult(
        bool Handled,
        WorldSession OriginalSession,
        WorldSession UpdatedSession,
        PersistedState? PersistedAtCommit,
        CharacterSnapshot ReloadedCharacter,
        PlayerItemSnapshot PersistedItems,
        IReadOnlyList<byte[]> ClaimantSent,
        IReadOnlyList<IReadOnlyList<byte[]>> ObserverSent,
        IReadOnlyList<string> ExposureOrder,
        string Log,
        string UsersRoot,
        string PlayerItemsRoot,
        string CharacterPath,
        string PlayerItemsPath,
        byte[] OriginalCharacterBytes,
        byte[] OriginalItemBytes);
}
