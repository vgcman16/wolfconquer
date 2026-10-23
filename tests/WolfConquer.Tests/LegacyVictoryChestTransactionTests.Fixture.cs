using System.Buffers;
using System.Net;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyVictoryChestTransactionTests
{
    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private sealed partial class Fixture
    {
        private const long AccountId = 301;
        private const long CharacterUid = 1_293_600;
        private const uint QuestId = 3_944;
        private const string RootPrefix = "wolf-victory-transaction-";
        private static readonly TimeSpan Bound = TimeSpan.FromSeconds(5);
        private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> Registry =
            PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        private WorldSessionStore sessions = null!;
        private LegacyTradeStateStore trades = null!;
        private WorldOperationBarrier operations = null!;
        private LegacyTradeRecoveryState recovery = null!;
        private WorldSession original = null!;
        private byte[] characterBefore = [];
        private byte[] itemsBefore = [];
        private byte[] questsBefore = [];
        private bool questAuthority = true;
        private bool ownsRoot;
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), RootPrefix + Guid.NewGuid().ToString("N"));
        private string Users => Path.Combine(Root, "Users");
        private string Items => Path.Combine(Root, "PlayersItems");
        private string Quests => Path.Combine(Root, "Quests");
        private string CharacterPath => Path.Combine(Users, "301.ini");
        private string ItemPath => Path.Combine(Items, "1293600.bin");
        private string QuestPath => Path.Combine(Quests, "1293600.bin");
        internal List<byte[]> Sent { get; } = [];
        private StringWriter Log { get; } = new();
        private ConquerTcpConnection Connection { get; }
        private WorldSession Current
        {
            get { Assert.True(sessions.TryGet(Connection, out var current)); return Assert.IsType<WorldSession>(current); }
        }

        internal Fixture()
        {
            Connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                (frame, _) => SendObservedVictoryFrame(frame));
        }

        internal void Seed()
        {
            ValidateRoot();
            if (Directory.Exists(Root) || File.Exists(Root)) throw new IOException("Fixture root already exists.");
            Directory.CreateDirectory(Root);
            ownsRoot = true;
            Directory.CreateDirectory(Users);
            Directory.CreateDirectory(Items);
            Directory.CreateDirectory(Quests);
            File.WriteAllText(CharacterPath, CharacterText, new UTF8Encoding(false, true));
            var character = CharacterSnapshotParser.ParseText(File.ReadAllText(CharacterPath), CharacterPath);
            var rawItems = new PlayerItemSnapshot(new[] { Item(900, 1_000_900, 2), Item(800, 1_000_800, 1) },
                new[] { Item(3500, 1_003_500, 1).WithPosition(213) },
                new[] { Item(3000, 1_003_000, 1).WithPosition(217) },
                new[] { Item(5000, 1_005_000, 1).WithPosition(4) },
                new[] { Item(4000, 1_004_000, 1).WithPosition(211) },
                new[] { Item(2500, 1_002_500, 1).WithWarehouseId(100) })
            {
                WarehouseItems = new[] { Item(4500, 1_004_500, 1).WithWarehouseId(1000)
                    .WithDepositedItems(new[] { Item(4510, 1_004_510, 3) }) },
            };
            var itemLoader = new PlayerItemSnapshotLoader(Items);
            itemLoader.SaveByCharacterUid(CharacterUid, rawItems);
            var loadedItems = itemLoader.LoadByCharacterUid(CharacterUid);
            var rawQuests = new PlayerQuestSnapshot(new[]
            {
                new PlayerQuestSnapshotEntry(804, PlayerQuestStatus.Accepted, 17, new uint[] { 7, 11 }),
                new PlayerQuestSnapshotEntry(QuestId, PlayerQuestStatus.Accepted, 12345, new uint[] { 100 }),
            });
            var questLoader = new PlayerQuestSnapshotLoader(Quests);
            questLoader.SaveByCharacterUid(CharacterUid, rawQuests);
            var loadedQuests = questLoader.LoadByCharacterUid(CharacterUid);
            characterBefore = File.ReadAllBytes(CharacterPath);
            itemsBefore = File.ReadAllBytes(ItemPath);
            questsBefore = File.ReadAllBytes(QuestPath);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(rawItems), itemsBefore);
            Assert.Equal(itemsBefore, PlayerItemSnapshotLoader.ToLegacyBytes(loadedItems));
            Assert.Equal(PlayerQuestSnapshotLoader.Serialize(rawQuests), questsBefore);
            Assert.Equal(questsBefore, PlayerQuestSnapshotLoader.Serialize(loadedQuests));
            Assert.True(LegacyDragonSkinInventory.TryCapture(loadedItems, out _, out var reason), reason);
            // All three real physical files exist and have round-tripped BEFORE
            // the current Store/Trade/Recovery/barrier owners are born.
            sessions = new WorldSessionStore();
            trades = new LegacyTradeStateStore();
            operations = new WorldOperationBarrier();
            recovery = new LegacyTradeRecoveryState(Users, Items, Quests);
            Assert.True(recovery.TryEnsureInitialized(out reason), reason);
            Assert.True(recovery.IsReadyFor(Users, Items, Quests));
            original = new WorldSession(AccountId, "victory-fixture", character with { PlayerQuests = loadedQuests },
                loadedItems, PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty, UsersRoot: Users);
            sessions.Set(Connection, original);
            Assert.Equal(AccountId, Current.AccountId);
            Assert.Equal(CharacterUid, Current.Character.Uid);
            AssertAllFiles();
        }

        private WorldPacketHandlerContext Context => new(Connection, Log, Session: Current, Sessions: sessions,
            PlayerItemsRoot: Items, PlayerQuestsRoot: questAuthority ? Quests : null,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13)
        { Trades = trades, TradeOperations = operations, TradeRecovery = recovery };

        internal async Task OpenAsync(uint npcId)
        {
            await HandleAsync(ReadFrame(LegacyNpcServerRequestPacket.Script(npcId, optionId: 0, interactType: 1).ToFrameBytes()));
            Assert.Equal(original with { ActiveNpcId = npcId }, Current);
            Assert.Equal(characterBefore, File.ReadAllBytes(CharacterPath));
            Assert.Equal(itemsBefore, File.ReadAllBytes(ItemPath));
            Assert.Equal(questsBefore, File.ReadAllBytes(QuestPath));
            AssertDialog();
            Sent.Clear();
            Log.GetStringBuilder().Clear();
        }

        internal Task ReplyAsync()
        {
            var writer = new PacketWriter(WorldPacketOpcodes.LegacyNpcReply, payloadLength: 11);
            writer.WriteUInt32(0, 0); writer.WriteUInt16(4, 0);
            writer.Payload[6] = 1; writer.Payload[7] = 1; writer.Payload[8] = 0; writer.WriteUInt16(9, 0);
            return HandleAsync(ReadFrame(writer.ToArray()));
        }

        private static PlayerItemSnapshotItem Item(uint uid, uint id, ushort stack) => new()
        {
            Uid = uid, ItemId = id, StackSize = stack, Durability = 91, MaximumDurability = 100, Bound = 1,
            Plus = 3, Bless = 1, Enchant = 7, SocketOne = 1, SocketProgress = 77, PlusProgress = 88,
            Color = 4, Locked = 1, PerfectionLevel = 2, OwnerUid = 123, OwnerName = "keeper", Signature = "retain",
        };

        private const string CharacterText =
            "; full owned Victory account\r\n[Character]\r\nUID=1293600\r\nName=VictoryTester\r\nBody=1003\r\n" +
            "Face=0\r\nHaire=0\r\nClass=7051\r\nFirstClass=7051\r\nSecoundeClass=0\r\nReborn=0\r\nLevel=140\r\n" +
            "Experience=123\r\nMap=10137\r\nX=93\r\nY=403\r\nPMap=10137\r\nPMapX=93\r\nPMapY=403\r\n" +
            "Strength=10\r\nAgility=20\r\nVitaliti=30\r\nSpirit=40\r\nAtributes=5\r\nMinHitPoints=123\r\nMinMana=45\r\n" +
            "Money=1234\r\nConquerPoints=55\r\nBoundConquerPoints=37\r\nBankCps=50\r\nPkPoints=0\r\nOnline=0\r\n" +
            "Sentinel = retain me \r\n[Unrelated]\r\nConquerPoints=999\r\nNote=preserve exact bytes\r\n";
    }
}
