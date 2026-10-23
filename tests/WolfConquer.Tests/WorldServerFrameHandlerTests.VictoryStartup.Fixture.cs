using System.Net;
using System.Reflection;
using System.Text;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    private sealed partial class VictoryStartupFixture
    {
        private const long Uid = 1_293_600; // Actual auth loader requires equal account/character identity.
        private const uint QuestId = 3_944;
        private const string Prefix = "wolf-victory-frame-startup-";
        private readonly bool explicitQuestRoot;
        private bool ownsRoot;
        private WorldServerFrameHandler? handler;
        private WorldSessionStore sessions = null!;
        private WorldOperationBarrier operations = null!;
        private LegacyTradeRecoveryState recovery = null!;
        private LegacyTradeStateStore trades = null!;
        private readonly string root = Path.Combine(Path.GetTempPath(), Prefix + Guid.NewGuid().ToString("N"));
        private string Users => Path.Combine(root, "Users");
        private string Items => Path.Combine(root, "PlayersItems");
        private string Quests => Path.Combine(root, "Quests");
        private string CharacterPath => Path.Combine(Users, "1293600.ini");
        private string ItemPath => Path.Combine(Items, "1293600.bin");
        private string QuestPath => Path.Combine(Quests, "1293600.bin");
        private readonly StringWriter log = new();
        private readonly List<byte[]> sent = [];
        private readonly ConquerTcpConnection connection;
        private byte[] characterSeed = [];
        private byte[] itemSeed = [];
        private byte[] questSeed = [];
        private WorldSession Current => sessions.TryGet(connection, out var value)
            ? Assert.IsType<WorldSession>(value) : throw new InvalidOperationException("Actual startup did not publish a session.");

        internal VictoryStartupFixture(bool explicitQuestRoot)
        {
            this.explicitQuestRoot = explicitQuestRoot;
            connection = new(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
            {
                sent.Add(frame.ToArray());
                if (observeCommittedPublication)
                {
                    AssertDurableCommit(publicationBefore!);
                    completedPublicationObservations++;
                }
                return ValueTask.CompletedTask;
            });
        }

        internal async Task EnterAsync()
        {
            Seed();
            await EnterSeededAsync();
        }

        private async Task RequestSeededAdmissionAsync()
        {
            var loader = new CharacterSnapshotLoader(Users, Items, playerSpellsRoot: null,
                playerProficienciesRoot: null, playersCombatGearRoot: null,
                playerQuestsRoot: explicitQuestRoot ? Quests : null);
            Assert.Equal(explicitQuestRoot ? Quests : null, loader.PlayerQuestsRoot);
            var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
            var ticket = handoff.Issue(Uid, "victory-frame-startup", ConquerRuntimeDefaults.WorldServer);
            var catalog = CreateStaticVisibilityCatalog(npcs:
            [
                CreateFrameHandlerVisibilityNpc(21_972, 2, 47_496, 10_137, 93, 403),
                CreateFrameHandlerVisibilityNpc(22_577, 2, 47_496, 10_137, 94, 403),
            ]);
            handler = new WorldServerFrameHandler(loader, handoff, catalog,
                LegacyRuntimeUpdateLoopOptions.Disabled, log,
                protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            // Read actual constructor-owned instances only. No session/field setter,
            // artificial recovery initializer or runtime-start sentinel.
            sessions = ReadField<WorldSessionStore>("_sessions");
            operations = ReadField<WorldOperationBarrier>("_worldOperations");
            recovery = ReadField<LegacyTradeRecoveryState>("_tradeRecovery");
            trades = ReadField<LegacyTradeStateStore>("_tradeStates");
            Assert.False(sessions.TryGet(connection, out _));
            await FrameAsync(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes());
        }

        private async Task EnterSeededAsync()
        {
            await RequestSeededAdmissionAsync();
            Assert.NotEmpty(sent);
            Assert.True(PacketReader.TryRead(sent[0], out var reader));
            Assert.Equal(WorldSnapshotWireStatus.Success, WorldSnapshotResponsePacket.ReadStatus(reader));
            Assert.Equal(Uid, WorldSnapshotResponsePacket.ReadAccountId(reader));
            Assert.Equal(Uid, WorldSnapshotResponsePacket.ReadUid(reader));
            Assert.Contains(sent, bytes => LegacyDragonSkinPacket.TryParse(ReadFrame(bytes), out var skin) &&
                skin == LegacyDragonSkinPacket.CreateStartup(Current.Character));
            Assert.Equal(Uid, Current.AccountId);
            Assert.Equal(Uid, Current.Character.Uid);
            Assert.Equal("victory-frame-startup", Current.Username);
            Assert.True(recovery.IsReadyFor(Users, Items));
            Assert.Equal(explicitQuestRoot, recovery.IsReadyFor(Users, Items, Quests));
            Assert.False(recovery.IsBlocked);
            Assert.False(trades.HasConnectionEnded(connection));
            Assert.Equal(itemSeed, PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems));
            if (explicitQuestRoot)
                Assert.Equal(questSeed, PlayerQuestSnapshotLoader.Serialize(Current.Character.PlayerQuests));
            else
                Assert.False(Current.Character.PlayerQuests.ContainsQuest(QuestId));
            AssertSeedFiles();

            sent.Clear();
            await FrameAsync(new MsgActionPacket { ObjId = (uint)Uid, Timestamp = 1234,
                Type = MsgActionType.SetLocation }.ToFrameBytes());
            Assert.Contains(sent, bytes => MsgActionPacket.TryParse(ReadFrame(bytes), out var action) &&
                action.Type == MsgActionType.SetLocation && action.ObjId == Uid &&
                action.DwParam == 10_137 && action.PositionX == 93 && action.PositionY == 403);
            Assert.Contains(sent, bytes => LegacyMapStatusPacket.TryParse(ReadFrame(bytes), out var map) && map.Id == 10_137);
            AssertSeedFiles();
            sent.Clear();
        }

        private T ReadField<T>(string name) where T : class => Assert.IsType<T>(
            typeof(WorldServerFrameHandler).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(handler));

        private void Seed()
        {
            ValidateRoot();
            if (Directory.Exists(root) || File.Exists(root)) throw new IOException("Owned fixture root already exists.");
            Directory.CreateDirectory(root); ownsRoot = true;
            Directory.CreateDirectory(Users); Directory.CreateDirectory(Items); Directory.CreateDirectory(Quests);
            File.WriteAllText(CharacterPath, CharacterText, new UTF8Encoding(false, true));
            var items = new PlayerItemSnapshot([Item(900, 2), Item(800)], [Item(3500).WithPosition(213)],
                [Item(3000).WithPosition(217)], [Item(5000).WithPosition(4)], [Item(4000).WithPosition(211)],
                [Item(2500).WithWarehouseId(100)])
            { WarehouseItems = [Item(4500).WithWarehouseId(1000).WithDepositedItems([Item(4510)])] };
            new PlayerItemSnapshotLoader(Items).SaveByCharacterUid(Uid, items);
            var quests = new PlayerQuestSnapshot([
                new(804, PlayerQuestStatus.Accepted, 17, [7U, 11U]),
                new(QuestId, PlayerQuestStatus.Accepted, 12345, [100U]),
            ]);
            new PlayerQuestSnapshotLoader(Quests).SaveByCharacterUid(Uid, quests);
            characterSeed = File.ReadAllBytes(CharacterPath);
            itemSeed = File.ReadAllBytes(ItemPath);
            questSeed = File.ReadAllBytes(QuestPath);
            Assert.Equal(Uid, CharacterSnapshotParser.ParseText(File.ReadAllText(CharacterPath), CharacterPath).Uid);
            Assert.Equal(itemSeed, PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshotLoader(Items).LoadByCharacterUid(Uid)));
            Assert.Equal(questSeed, PlayerQuestSnapshotLoader.Serialize(new PlayerQuestSnapshotLoader(Quests).LoadByCharacterUid(Uid)));
        }

        private static PlayerItemSnapshotItem Item(uint uid, ushort stack = 1) => new()
        {
            Uid = uid, ItemId = 1_000_000 + uid, StackSize = stack, Durability = 91, MaximumDurability = 100,
            Bound = 1, Plus = 3, Bless = 1, Enchant = 7, Color = 4,
            PerfectionLevel = 2, OwnerUid = 123, OwnerName = "keeper", Signature = "retain",
        };

        private const string CharacterText = "; retain owned startup authority\r\n[Character]\r\n" +
            "UID=1293600\r\nName=VictoryFrame\r\nBody=1003\r\nFace=0\r\nHaire=0\r\nClass=7051\r\nFirstClass=7051\r\n" +
            "SecoundeClass=0\r\nReborn=0\r\nLevel=140\r\nExperience=123\r\nMap=10137\r\nX=93\r\nY=403\r\n" +
            "PMap=10137\r\nPMapX=93\r\nPMapY=403\r\nStrength=10\r\nAgility=20\r\nVitaliti=30\r\nSpirit=40\r\n" +
            "Atributes=5\r\nMinHitPoints=123\r\nMinMana=45\r\nMoney=1234\r\nConquerPoints=55\r\nBoundConquerPoints=37\r\n" +
            "BankCps=50\r\nPkPoints=0\r\nOnline=0\r\nSentinel = preserve bytes \r\n[Unrelated]\r\nKeep=exact\r\n";
    }
}
