using System.Buffers;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyDeitylandChestCapacitySafetyTests
{
    private static async Task RunAsync(Func<Fixture, Task> body)
    {
        var fixture = new Fixture();
        var errors = new List<Exception>();
        try { await body(fixture); }
        catch (Exception error) { errors.Add(error); }
        // Registered calls and all captured sends are awaited by body; no worker,
        // frame handler, runtime loop, child process or UID Host is created here.
        var transportClosed = false;
        try { await fixture.Connection.CompleteTransportCloseAsync(); transportClosed = true; }
        catch (Exception error) { errors.Add(error); }
        if (transportClosed)
        {
            try { fixture.DeleteOwnedRoot(); }
            catch (Exception error) { errors.Add(error); }
        }
        try { fixture.Log.Dispose(); }
        catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private sealed partial class Fixture
    {
        private const string RootPrefix = "wolf-deity-capacity-";
        private const long CharacterUid = 1_293_600;
        private const uint KeyItemId = 3_303_973;
        private readonly string root = Path.Combine(Path.GetTempPath(), RootPrefix + Guid.NewGuid().ToString("N"));
        private long accountId = CharacterUid;
        private WorldSessionStore sessions = new();
        private readonly PacketHandlerRegistry<WorldPacketHandlerContext> registry =
            PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        private bool ownsRoot;
        private byte[] characterBefore = [];
        private byte[] itemsBefore = [];
        internal List<byte[]> Sent { get; } = [];
        internal StringWriter Log { get; } = new();
        internal ConquerTcpConnection Connection { get; }
        internal WorldSession Session { get; private set; } = null!;
        private string Users => Path.Combine(root, "Users");
        private string Items => Path.Combine(root, "PlayersItems");
        private string CharacterPath => Path.Combine(Users, accountId + ".ini");
        private string ItemPath => Path.Combine(Items, CharacterUid + ".bin");
        internal WorldSession Current
        {
            get
            {
                Assert.True(sessions.TryGet(Connection, out var current));
                return Assert.IsType<WorldSession>(current);
            }
        }

        internal Fixture()
        {
            Connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
            {
                Sent.Add(frame.ToArray());
                ChestSendObserver?.Invoke();
                return ValueTask.CompletedTask;
            });
        }

        internal void Seed(int slots, ushort firstStack, ushort secondStack, byte firstBound, bool includeKeys = true,
            uint firstKeyItemId = KeyItemId, uint secondKeyItemId = KeyItemId, uint npcId = 22_598,
            long accountId = CharacterUid, bool firstKeyHasDepositedChild = false)
        {
            this.accountId = accountId;
            ValidateRoot();
            if (Directory.Exists(root) || File.Exists(root)) throw new IOException("Fixture root already exists.");
            Directory.CreateDirectory(root);
            ownsRoot = true;
            Directory.CreateDirectory(Users);
            Directory.CreateDirectory(Items);
            var fields = new[]
            {
                "; retained heading", "[Character]", "UID=1293600", "Name=ChestTester", "Body=1003",
                "Face=0", "Haire=0", "Class=7051", "FirstClass=7051", "SecoundeClass=0", "Reborn=0",
                "Level=140", "Experience=123", "Map=10250", "X=1009", "Y=1283", "PMap=1002",
                "PMapX=577", "PMapY=586", "Strength=10", "Agility=20", "Vitaliti=30", "Spirit=40",
                "Atributes=5", "MinHitPoints=123", "MinMana=45", "Money=1234", "ConquerPoints=55",
                "BoundConquerPoints=37", "BankCps=50", "PkPoints=0", "Online=0", "Sentinel = retain me ",
                "[Unrelated]", "ConquerPoints=999", "Note=preserve",
            };
            var text = string.Join(Environment.NewLine, fields) + Environment.NewLine;
            File.WriteAllText(CharacterPath, text, new UTF8Encoding(false, true));
            // Enumeration order deliberately disagrees with UID order.
            var firstKey = Item(900, includeKeys ? firstKeyItemId : 1_000_900, firstStack).WithBound(firstBound);
            if (firstKeyHasDepositedChild)
                firstKey = firstKey.WithDepositedItems([Item(1900, 1_001_900, 2)]);
            var inventory = new List<PlayerItemSnapshotItem>
            {
                firstKey,
                Item(800, includeKeys ? secondKeyItemId : 1_000_800, secondStack).WithBound((byte)(1 - firstBound)),
            };
            inventory.AddRange(Enumerable.Range(0, slots - 2).Select(index => Item((uint)(100 + index), 1_000_000, 1)));
            var snapshot = new PlayerItemSnapshot(inventory.ToArray(),
                [Item(3500, 1_003_500, 1).WithPosition(213)],
                [Item(3000, 1_003_000, 1).WithPosition(217)],
                [Item(5000, 1_005_000, 1).WithPosition(4)],
                [Item(4000, 1_004_000, 1).WithPosition(211)],
                [Item(2500, 1_002_500, 1).WithWarehouseId(100)])
            {
                WarehouseItems = [Item(4500, 1_004_500, 1).WithWarehouseId(1000)
                    .WithDepositedItems([Item(4510, 1_004_510, 1)])],
            };
            File.WriteAllBytes(ItemPath, PlayerItemSnapshotLoader.ToLegacyBytes(snapshot));
            var loaded = new PlayerItemSnapshotLoader(Items).LoadByCharacterUid(CharacterUid);
            itemsBefore = File.ReadAllBytes(ItemPath);
            Assert.Equal(itemsBefore, PlayerItemSnapshotLoader.ToLegacyBytes(loaded));
            Assert.Equal(new uint[] { 900, 800 }, loaded.InventoryItems.Take(2).Select(item => item.Uid));
            characterBefore = File.ReadAllBytes(CharacterPath);
            var character = CharacterSnapshotParser.ParseText(text, CharacterPath);
            Session = new WorldSession(accountId, "fixture", character, loaded, PlayerSpellSnapshot.Empty,
                PlayerProficiencySnapshot.Empty, UsersRoot: Users, ActiveNpcId: npcId);
            InitializeRequiredChestSaveOwners();
            sessions.Set(Connection, Session);
        }

        private WorldPacketHandlerContext Context => new(Connection, Log, Session: Current,
            Sessions: sessions, PlayerItemsRoot: Items, ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13)
        {
            Trades = requiredChestTrades,
            TradeOperations = requiredChestOperations,
            TradeRecovery = requiredChestRecovery,
        };

        internal async Task UnlockRareAsync() => await registry.HandleAsync(Context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(22_598, optionId: 1, interactType: 1).ToFrameBytes()));

        internal async Task UnlockEpicAsync() => await registry.HandleAsync(Context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(22_607, optionId: 1, interactType: 1).ToFrameBytes()));

        internal async Task UnlockSelectedAsync(uint rewardItemId, uint conquerPointsReward)
        {
            // Select the actual post-roll owner, without replacing RNG or adding
            // accepting business providers. Registered rare coverage is separate.
            var method = typeof(WorldPacketHandlers).GetMethod("TryUnlockLegacyDeitylandChestAsync",
                BindingFlags.Static | BindingFlags.NonPublic)!;
            object? result;
            try
            {
                result = method.Invoke(null, [Context, Current, "component-capacity-post-roll",
                    KeyItemId, rewardItemId, conquerPointsReward, CancellationToken.None]);
            }
            catch (TargetInvocationException error) when (error.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                throw;
            }
            await Assert.IsType<ValueTask>(result);
        }

        internal PlayerItemSnapshot ExpectedDebit(int keyIndex = 0)
        {
            var rows = Session.PlayerItems.InventoryItems.ToList();
            var selected = rows[keyIndex];
            if (selected.StackSize == 1) rows.RemoveAt(keyIndex);
            else rows[keyIndex] = selected.WithStackSize((ushort)(selected.StackSize - 1));
            return Session.PlayerItems with
            {
                InventoryItems = rows.ToArray(),
            };
        }

        internal void AssertUnchanged(WorldSession before)
        {
            Assert.Equal(before, Current);
            Assert.Equal(itemsBefore, PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems));
            Assert.Equal(itemsBefore, PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems));
            Assert.Equal(itemsBefore, File.ReadAllBytes(ItemPath));
            Assert.Equal(characterBefore, File.ReadAllBytes(CharacterPath));
            AssertOnlyExpectedFiles();
        }

        internal void AssertItems(PlayerItemSnapshot expected)
        {
            var bytes = PlayerItemSnapshotLoader.ToLegacyBytes(expected);
            Assert.Equal(bytes, PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems));
            Assert.Equal(bytes, File.ReadAllBytes(ItemPath));
            Assert.Equal(bytes, PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshotLoader(Items).LoadByCharacterUid(CharacterUid)));
            Assert.Equal(itemsBefore, PlayerItemSnapshotLoader.ToLegacyBytes(Session.PlayerItems));
            AssertOnlyExpectedFiles();
        }

        internal void AssertItemReward(uint rewardItemId, int keyIndex = 0)
        {
            var debit = ExpectedDebit(keyIndex);
            // Preserve CURRENT creator golden, not a claim of global UID safety.
            var reward = new PlayerItemSnapshotItem { Uid = 5001, ItemId = rewardItemId, StackSize = 1 };
            var expected = debit with { InventoryItems = debit.InventoryItems.Concat([reward]).ToArray() };
            AssertItems(expected);
            Assert.Equal(40, Current.PlayerItems.InventoryItems.Count);
            Assert.Equal(Session with { PlayerItems = Current.PlayerItems }, Current);
            Assert.Equal(characterBefore, File.ReadAllBytes(CharacterPath));
            Assert.Equal(2, Sent.Count);
            AssertDebitFrame(keyIndex);
            Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(reward).ToFrameBytes(), Sent[1]);
            Assert.Contains("chest item reward", Log.ToString());
            Assert.DoesNotContain("PARITY-GAP", Log.ToString());
        }

        internal void AssertDebitFrame(int keyIndex = 0)
        {
            var first = Session.PlayerItems.InventoryItems[keyIndex];
            var expected = first.StackSize == 1
                ? new MsgItemUsagePacket(first.Uid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes()
                : LegacyGameItemPacket.FromSnapshotItem(first.WithStackSize((ushort)(first.StackSize - 1)),
                    mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes();
            Assert.Equal(expected, Sent[0]);
        }

        internal void AssertEpicOutcome(int keyIndex)
        {
            // Keep the real registered Random.Shared selection. Either actual
            // payout must charge this exact Epic row and preserve every Rare row.
            Assert.Equal(2, Sent.Count);
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(Sent[1]), out var update))
            {
                AssertItems(ExpectedDebit(keyIndex));
                Assert.Equal(Session with
                {
                    Character = Session.Character with { ConquerPoints = 200_055 },
                    PlayerItems = Current.PlayerItems,
                }, Current);
                AssertDebitFrame(keyIndex);
                Assert.Equal((uint)Session.Character.Uid, update.Uid);
                Assert.Equal(new[] { new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.ConquerPoints, 200_055) }, update.Updates);
                AssertCharacterCpSaved();
                Assert.Contains("chest CP reward", Log.ToString());
            }
            else
            {
                Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(Sent[1]), out var reward));
                Assert.Contains(reward.ItemId, new uint[] { 4_200_005, 3_007_269, 3_004_247 });
                AssertItemReward(reward.ItemId, keyIndex);
            }
            Assert.Contains("source=NpcHandler.EpicDeitylandChests", Log.ToString());
            Assert.DoesNotContain("PARITY-GAP", Log.ToString());
        }

        internal void AssertCharacterCpSaved()
        {
            var expected = Encoding.UTF8.GetString(characterBefore).Replace(
                Environment.NewLine + "ConquerPoints=55" + Environment.NewLine,
                Environment.NewLine + "ConquerPoints=200055" + Environment.NewLine, StringComparison.Ordinal);
            Assert.Equal(Encoding.UTF8.GetBytes(expected), File.ReadAllBytes(CharacterPath));
            var saved = CharacterSnapshotParser.ParseText(File.ReadAllText(CharacterPath), CharacterPath);
            Assert.Equal(200_055U, saved.ConquerPoints);
            Assert.Equal(1234, saved.Money);
            Assert.Equal(37U, saved.BoundConquerPoints);
            Assert.Equal(50U, saved.BankConquerPoints);
        }

        private void AssertOnlyExpectedFiles() => Assert.Equal(
            new[] { ItemPath, CharacterPath }.Concat(accountProofFiles).Order(StringComparer.Ordinal),
            Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));

        private static PlayerItemSnapshotItem Item(uint uid, uint itemId, ushort stack) => new()
        {
            Uid = uid, ItemId = itemId, StackSize = stack, Durability = 91, MaximumDurability = 100,
            Plus = 3, Bless = 1, Enchant = 7, SocketOne = 1, SocketProgress = 77, PlusProgress = 88,
            Color = 4, PerfectionLevel = 2, OwnerUid = 123, OwnerName = "keeper", Signature = "retain",
        };

        internal void DeleteOwnedRoot()
        {
            if (!ownsRoot) return;
            ValidateRoot();
            DeleteOrdinaryTree(root);
        }

        private void ValidateRoot()
        {
            var full = Path.GetFullPath(root);
            var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            var name = Path.GetFileName(full);
            if (!string.Equals(Path.GetDirectoryName(full), temp, StringComparison.OrdinalIgnoreCase) ||
                !name.StartsWith(RootPrefix, StringComparison.Ordinal) ||
                !Guid.TryParseExact(name[RootPrefix.Length..], "N", out _))
                throw new InvalidOperationException("Not the exact owned temporary domain.");
            for (string? current = full; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            {
                try
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidOperationException("Owned domain has a reparse ancestor.");
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }
        }

        private void DeleteOrdinaryTree(string directory)
        {
            if (!Directory.Exists(directory)) return;
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                if (!Path.GetFullPath(entry).StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
                    (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Cleanup entry is outside the ordinary owned domain.");
                if (Directory.Exists(entry)) DeleteOrdinaryTree(entry); else File.Delete(entry);
            }
            Directory.Delete(directory, recursive: false);
        }
    }
}
