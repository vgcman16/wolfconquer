using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNpcServerRequestHandlerTests
{
    private static async Task RunDeityChestAsync(Func<DeityChestFixture, Task> body)
    {
        var fixture = new DeityChestFixture();
        var errors = new List<Exception>();
        try { await body(fixture); }
        catch (Exception error) { errors.Add(error); }
        try { await fixture.CleanupAsync(errors); }
        catch (Exception error)
        {
            fixture.Retain();
            if (!errors.Any(existing => ReferenceEquals(existing, error))) errors.Add(error);
        }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1)
            throw new AggregateException($"Deity chest fixture failed; ownedRoot={fixture.Root}", errors);
    }

    private sealed class DeityChestFixture
    {
        private const string RootPrefix = "wolf-current-deity-npc-";
        private const long CharacterUid = 1_293_600;
        private static readonly TimeSpan Bound = TimeSpan.FromSeconds(5);
        private static readonly ConcurrentBag<DeityChestFixture> RetainedFixtures = new();
        private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> Registry =
            PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        private readonly WorldOperationBarrier operations = new();
        private readonly LegacyTradeStateStore trades = new();
        private readonly List<Task> calls = [];
        private readonly List<CancellationTokenSource> budgets = [];
        private readonly HashSet<Task> observedTerminals = [];
        private Task? transportCloseTask;
        private int retained;
        private LegacyTradeRecoveryState recovery = null!;
        private WorldSession original = null!;
        private byte[] characterBefore = [];
        private byte[] itemsBefore = [];
        private bool ownsRoot;
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), RootPrefix + Guid.NewGuid().ToString("N"));
        private string Users => Path.Combine(Root, "Users");
        private string Items => Path.Combine(Root, "PlayersItems");
        private string CharacterPath => Path.Combine(Users, "1293600.ini");
        private string ItemPath => Path.Combine(Items, "1293600.bin");
        internal StringWriter Log { get; } = new();
        internal List<byte[]> Sent { get; } = [];
        internal WorldSessionStore Sessions { get; } = new();
        internal ConquerTcpConnection Connection { get; }

        internal DeityChestFixture()
        {
            Connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, token) =>
            {
                token.ThrowIfCancellationRequested();
                var bytes = frame.ToArray();
                // Real registered sends observe the committed store and files, not an accepting writer.
                if (MsgItemUsagePacket.TryParse(ReadFrame(bytes), out _) ||
                    LegacyGameItemPacket.TryParse(ReadFrame(bytes), out _) ||
                    LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out _))
                    AssertSavedCurrent();
                Sent.Add(bytes);
                return ValueTask.CompletedTask;
            });
        }

        internal WorldSession Current
        {
            get
            {
                Assert.True(Sessions.TryGet(Connection, out var current));
                return Assert.IsType<WorldSession>(current);
            }
        }

        internal void Seed(WorldSession seed)
        {
            Assert.Equal(CharacterUid, seed.AccountId);
            Assert.Equal(CharacterUid, seed.Character.Uid);
            ValidateRoot();
            if (Directory.Exists(Root) || File.Exists(Root)) throw new IOException("Fixture root already exists.");
            Directory.CreateDirectory(Root);
            ownsRoot = true;
            Directory.CreateDirectory(Users);
            Directory.CreateDirectory(Items);
            File.WriteAllText(CharacterPath, CharacterText, new UTF8Encoding(false, true));
            var character = CharacterSnapshotParser.ParseText(File.ReadAllText(CharacterPath), CharacterPath);
            // Every original character field remains the same; only actual file provenance/dictionaries are supplied.
            Assert.Equal(seed.Character with
            {
                SourceFile = character.SourceFile,
                Attributes = character.Attributes,
                PaidNobility = character.PaidNobility,
            }, character);
            var loader = new PlayerItemSnapshotLoader(Items);
            loader.SaveByCharacterUid(CharacterUid, seed.PlayerItems);
            var loaded = loader.LoadByCharacterUid(CharacterUid);
            characterBefore = File.ReadAllBytes(CharacterPath);
            itemsBefore = File.ReadAllBytes(ItemPath);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(seed.PlayerItems), itemsBefore);
            Assert.Equal(itemsBefore, PlayerItemSnapshotLoader.ToLegacyBytes(loaded));
            original = seed with { Character = character, PlayerItems = loaded, UsersRoot = Users };
            Sessions.Set(Connection, original);
            recovery = new LegacyTradeRecoveryState(Users, Items);
            Assert.True(recovery.TryEnsureInitialized(out var reason), reason);
            AssertReady();
        }

        internal async Task HandleAsync(PacketFrame frame)
        {
            AssertReady();
            var context = new WorldPacketHandlerContext(Connection, Log, Session: Current,
                Sessions: Sessions, PlayerItemsRoot: Items, ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13)
            { Trades = trades, TradeOperations = operations, TradeRecovery = recovery };
            Assert.Same(Connection, context.Connection);
            Assert.Same(Current, context.Session);
            Assert.Contains(frame.Header.Opcode, Registry.Opcodes);
            var budget = new CancellationTokenSource(Bound);
            budgets.Add(budget);
            var actual = Registry.HandleAsync(context, frame, budget.Token).AsTask();
            calls.Add(actual);
            try { await actual.WaitAsync(Bound + Bound); }
            finally { if (actual.IsCompleted) budget.CancelAfter(Timeout.InfiniteTimeSpan); }
        }

        private void AssertReady()
        {
            Assert.False(recovery.IsBlocked);
            Assert.True(recovery.IsReadyFor(Current.UsersRoot, Items));
            Assert.False(Current.LegacyTradeActive);
            lock (trades.SyncRoot)
            {
                Assert.False(trades.HasConnectionEnded(Connection));
                Assert.False(trades.Contains(Connection));
            }
        }

        internal void AssertDialogOnly(uint npcId)
        {
            Assert.Equal(original with { ActiveNpcId = npcId }, Current);
            Assert.Equal(characterBefore, File.ReadAllBytes(CharacterPath));
            Assert.Equal(itemsBefore, File.ReadAllBytes(ItemPath));
            Assert.Equal(itemsBefore, PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems));
            AssertOnlyExpectedFiles();
        }

        internal void AssertCommitted(uint npcId, uint keyUid)
        {
            var key = Assert.Single(original.PlayerItems.InventoryItems, item => item.Uid == keyUid);
            Assert.Equal((ushort)1, key.StackSize);
            var remaining = original.PlayerItems.InventoryItems.Where(item => item.Uid != keyUid).ToArray();
            var expected = original.PlayerItems with { InventoryItems = remaining };
            long expectedCp = 0;
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(Sent[9]), out _))
            {
                Assert.Equal(22_607U, npcId);
                expectedCp = 200_000;
                Assert.Equal(new LegacyMsgUpdatePacket((uint)CharacterUid,
                    [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.ConquerPoints, 200_000)]).ToFrameBytes(), Sent[9]);
            }
            else
            {
                Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(Sent[9]), out var packet));
                Assert.NotEqual(0U, packet.Uid);
                Assert.DoesNotContain(remaining, item => item.Uid == packet.Uid);
                // Observe the real CURRENT creator's UID; no allocator, reservation or fabricated UID is supplied.
                var reward = new PlayerItemSnapshotItem { Uid = packet.Uid, ItemId = packet.ItemId, StackSize = 1 };
                expected = expected with { InventoryItems = remaining.Concat([reward]).ToArray() };
                Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(reward).ToFrameBytes(), Sent[9]);
            }
            Assert.Equal(new MsgItemUsagePacket(keyUid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0)
                .ToFrameBytes(), Sent[8]);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expected),
                PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems));
            Assert.Equal(original with
            {
                ActiveNpcId = npcId,
                Character = original.Character with { ConquerPoints = expectedCp },
                PlayerItems = Current.PlayerItems,
            }, Current);
            Assert.Equal(itemsBefore, PlayerItemSnapshotLoader.ToLegacyBytes(original.PlayerItems));
            AssertSavedCurrent();
            AssertOnlyExpectedFiles();
            AssertReady();
            Assert.DoesNotContain("PARITY-GAP", Log.ToString());
        }

        private void AssertSavedCurrent()
        {
            var current = Current;
            var expectedItems = PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems);
            Assert.Equal(expectedItems, File.ReadAllBytes(ItemPath));
            Assert.Equal(expectedItems, PlayerItemSnapshotLoader.ToLegacyBytes(
                new PlayerItemSnapshotLoader(Items).LoadByCharacterUid(CharacterUid)));
            var expectedText = Encoding.UTF8.GetString(characterBefore).Replace(
                "\r\nConquerPoints=0\r\n",
                "\r\nConquerPoints=" + current.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture) + "\r\n",
                StringComparison.Ordinal);
            Assert.Equal(Encoding.UTF8.GetBytes(expectedText), File.ReadAllBytes(CharacterPath));
            var saved = CharacterSnapshotParser.ParseText(File.ReadAllText(CharacterPath), CharacterPath);
            Assert.Equal(CharacterUid, saved.Uid);
            Assert.Equal(current.Character.ConquerPoints, saved.ConquerPoints);
            Assert.Equal(current.Character.Money, saved.Money);
            Assert.Equal(current.Character.BoundConquerPoints, saved.BoundConquerPoints);
            Assert.Equal(current.Character.BankConquerPoints, saved.BankConquerPoints);
        }

        private void AssertOnlyExpectedFiles() => Assert.Equal(
            new[] { CharacterPath, ItemPath }.Order(StringComparer.Ordinal),
            Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));

        internal async Task CleanupAsync(List<Exception> errors)
        {
            if (calls.Any(call => !call.IsCompleted))
                foreach (var budget in budgets)
                    try { await budget.CancelAsync(); } catch (Exception error) { AddFailure(errors, error); }
            var drained = true;
            foreach (var actual in calls)
            {
                if (!await ObserveTerminalAsync(actual, errors)) drained = false;
            }
            var closed = false;
            if (drained)
            {
                try
                {
                    transportCloseTask ??= Connection.CompleteTransportCloseAsync();
                    closed = await ObserveTerminalAsync(transportCloseTask, errors) &&
                        transportCloseTask.IsCompletedSuccessfully;
                }
                catch (Exception error) { AddFailure(errors, error); }
            }
            // No Frame handler, loop or Host is created. Never remove files/logger while an actual call/close is unproven.
            if (!drained || !closed)
            {
                Retain();
                AddFailure(errors, new InvalidOperationException($"Owned fixture retained for unproven work/transport: {Root}"));
                return;
            }
            foreach (var budget in budgets)
                try { budget.Dispose(); } catch (Exception error) { AddFailure(errors, error); }
            try { if (ownsRoot) { ValidateRoot(); DeleteOrdinaryTree(Root); } }
            catch (Exception error) { AddFailure(errors, error); }
            try { Log.Dispose(); } catch (Exception error) { AddFailure(errors, error); }
        }

        private async Task<bool> ObserveTerminalAsync(Task actual, List<Exception> errors)
        {
            if (observedTerminals.Contains(actual)) return true;
            try { await actual.WaitAsync(Bound); }
            catch (Exception observation)
            {
                AddFailure(errors, observation);
                if (!actual.IsCompleted) return false;
                // A timeout may race actual completion. Observe that SAME task's
                // terminal result before treating the owner as drained.
                try { await actual; }
                catch (Exception completion) { AddFailure(errors, completion); }
            }
            observedTerminals.Add(actual);
            return true;
        }

        internal void Retain()
        {
            if (Interlocked.Exchange(ref retained, 1) == 0) RetainedFixtures.Add(this);
        }

        private static void AddFailure(List<Exception> errors, Exception error)
        {
            if (!errors.Any(existing => ReferenceEquals(existing, error))) errors.Add(error);
        }

        private void ValidateRoot()
        {
            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Root));
            var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            var name = Path.GetFileName(full);
            if (!string.Equals(Path.GetDirectoryName(full), temp, StringComparison.OrdinalIgnoreCase) ||
                !name.StartsWith(RootPrefix, StringComparison.Ordinal) ||
                !Guid.TryParseExact(name[RootPrefix.Length..], "N", out _))
                throw new InvalidOperationException("Not the exact owned temporary fixture.");
            for (string? path = full; !string.IsNullOrEmpty(path); path = Path.GetDirectoryName(path))
            {
                try
                {
                    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidOperationException("Owned fixture has a reparse ancestor.");
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
                if (!Path.GetFullPath(entry).StartsWith(Path.GetFullPath(Root) + Path.DirectorySeparatorChar,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
                    (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Cleanup entry is outside the ordinary owned fixture.");
                if (Directory.Exists(entry)) DeleteOrdinaryTree(entry); else File.Delete(entry);
            }
            Directory.Delete(directory, recursive: false);
        }

        private static readonly string CharacterText = string.Join("\r\n", new[]
        {
            "; original NPC success-fixture character", "[Character]", "UID=1293600", "Name=Demonic", "Body=1003",
            "Face=0", "Haire=0", "Class=7051", "FirstClass=7051", "SecoundeClass=0", "Reborn=0", "Level=140",
            "Experience=0", "Map=10137", "X=93", "Y=403", "PMap=10137", "PMapX=93", "PMapY=403",
            "Strength=0", "Agility=0", "Vitaliti=0", "Spirit=0", "Atributes=0", "MinHitPoints=1", "MinMana=0",
            "Money=0", "ConquerPoints=0", "BoundConquerPoints=0", "BankCps=0", "PkPoints=0", "Online=0",
            "Sentinel = preserve me ", "[Unrelated]", "ConquerPoints=777", "Note=retain exact bytes",
        }) + "\r\n";
    }
}
