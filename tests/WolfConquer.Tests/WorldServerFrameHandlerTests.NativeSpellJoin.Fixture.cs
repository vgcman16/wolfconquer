using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    internal static async Task WithNativeSpellJoinAsync(Func<NativeSpellJoinFixture, Task> body,
        PlayerSpellSnapshotEntry[]? rows = null, bool lineage = true, bool missingSpellFile = false, bool combined = false, bool training = false,
        long? initialMana = null, long? maximumMana = null, string? extraCharacterLines = null,
        PlayerItemSnapshot? playerItems = null)
    {
        var fixture = new NativeSpellJoinFixture();
        Exception? primary = null;
        try { fixture.Initialize(rows, lineage, missingSpellFile, combined, training, initialMana, maximumMana, extraCharacterLines, playerItems); await body(fixture); }
        catch (Exception error) { fixture.RetainRoot = true; primary = error; }
        try { await fixture.DisposeAsync(); }
        catch (Exception cleanup)
        {
            if (primary is not null) throw new AggregateException($"Native spell fixture retained at {fixture.Root}.", primary, cleanup);
            ExceptionDispatchInfo.Capture(cleanup).Throw();
        }
        if (primary is not null)
            throw new AggregateException($"Native spell assertion/operation failed; owned evidence retained at {fixture.Root}.", primary);
    }

    internal sealed partial class NativeSpellJoinFixture : IAsyncDisposable
    {
        internal const uint Uid = 1293600;
        internal readonly string Root = Path.Combine(Path.GetTempPath(), "wolf-world-handler-" + Guid.NewGuid().ToString("N"));
        internal string Users => Path.Combine(Root, "Users");
        internal string Spells => Path.Combine(Root, "PlayersSpells");
        internal string CharacterPath => Path.Combine(Users, Uid.ToString(CultureInfo.InvariantCulture) + ".ini");
        internal string SpellPath => Path.Combine(Spells, Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        internal readonly List<byte[]> Sent = [];
        internal Func<byte[], CancellationToken, ValueTask>? ObserveSend { get; set; }
        internal readonly StringWriter Log = new();
        internal LegacyNativeGameClientRuntime Native { get; private set; } = null!;
        internal LegacyNativePlayerRuntime Player => Native.Player;
        internal ConquerTcpConnection Connection { get; private set; } = null!;
        internal WorldServerFrameHandler Handler { get; private set; } = null!;
        internal WorldSessionStore Store => Actual<WorldSessionStore>("_sessions");
        internal LegacyTradeRecoveryState Recovery => Actual<LegacyTradeRecoveryState>("_tradeRecovery");
        internal LegacyTradeStateStore Trades => Actual<LegacyTradeStateStore>("_tradeStates");
        internal WorldOperationBarrier Operations => Actual<WorldOperationBarrier>("_worldOperations");
        internal WorldSession? CurrentOrNull => Store.TryGet(Connection, out var current) ? current : null;
        internal WorldSession Current => Assert.IsType<WorldSession>(CurrentOrNull);
        internal byte[] SavedSpellsBefore { get; private set; } = null!;
        internal bool RetainRoot { get; set; }
        internal LegacyMailboxPackageProducer? PackageProducer { get; private set; }
        internal bool Disconnected { get; private set; }
        private OrdinaryLoginFixture? admission;
        private NativeLoginBeastMetadataScope? beasts;
        private readonly List<ConquerTcpConnection> connections = [];
        private bool ownsRoot;

        internal void Initialize(PlayerSpellSnapshotEntry[]? rows, bool lineage, bool missingSpellFile, bool combined, bool training,
            long? initialMana = null, long? maximumMana = null, string? extraCharacterLines = null,
        PlayerItemSnapshot? playerItems = null)
        {
            ValidateRoot();
            Assert.False(Directory.Exists(Root) || File.Exists(Root));
            Directory.CreateDirectory(Users); ownsRoot = true;
            WriteSnapshot(Users, Uid, uid: Uid, classCode: combined ? 10 : lineage ? 1052 : 7051, level: 110,
                reborn: 0, secondClass: 0, money: 1000, conquerPoints: 200, boundConquerPoints: 300,
                minHitPoints: 1000, name: "OrdinarySpellOwner", extraCharacterLines: extraCharacterLines);
            // Genuine saved inputs BEFORE OrdinaryLoginFixture captures its
            // exact admission baseline. Never seed private native resource fields.
            if (initialMana is not null || maximumMana is not null)
            {
                var vitals = new Dictionary<string, string>();
                if (initialMana is { } mana) vitals["MinMana"] = mana.ToString(CultureInfo.InvariantCulture);
                if (maximumMana is { } maximum) vitals["MaxMana"] = maximum.ToString(CultureInfo.InvariantCulture);
                Assert.True(CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(CharacterPath, Uid,
                    vitals, detailedIoFailures: true, out var reason), reason);
            }
            Directory.CreateDirectory(Spells);
            rows ??= [new(6001, 4, 1111, 2, 3, 2)];
            SavedSpellsBefore = PlayerSpellSnapshotLoader.ToLegacyBytes(new PlayerSpellSnapshot(rows));
            if (rows.Length != 0) SavedSpellsBefore[15] = 0xA5;
            if (missingSpellFile) Assert.Empty(rows);
            else File.WriteAllBytes(SpellPath, SavedSpellsBefore);
            beasts = new NativeLoginBeastMetadataScope(1000); // Existing parsed/raw genuine login prerequisite; exact prior cache restored.
            if (combined)
            {
                Assert.True(CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(CharacterPath, Uid,
                    new Dictionary<string, string> { ["ClassExperience"] = "0" }, detailedIoFailures: true, out var baselineReason), baselineReason);
                new PlayerItemSnapshotLoader(Path.Combine(Root, "PlayersItems")).SaveByCharacterUid(Uid, PlayerItemSnapshot.Empty);
                new CombatGearSnapshotLoader(Path.Combine(Root, "PlayersCombatGear")).SaveByCharacterUid(Uid, CombatGearSnapshot.Empty);
                var mailbox = new LegacyMailboxStore(Root);
                mailbox.Add(Uid, new LegacyMailboxEntry { Id = 21, Title = "Owned package", SenderName = "Fixture",
                    Content = "Synthetic package", Flag = 1, Timer = new DateTime(2099, 1, 1).ToBinary() });
                mailbox.Save(Uid);
            }
            if (playerItems is not null)
                new PlayerItemSnapshotLoader(Path.Combine(Root, "PlayersItems")).SaveByCharacterUid(Uid, playerItems);
            var loader = new CharacterSnapshotLoader(Users);
            if (training)
            {
                var profs = Assert.IsType<string>(loader.PlayerProficienciesRoot);
                Directory.CreateDirectory(profs);
                new PlayerProficiencySnapshotLoader(profs).SaveByCharacterUid(Uid,
                    new PlayerProficiencySnapshot([new(410, 5, 0, 1000, 0), new(420, 5, 0, 2000, 0)]));
            }
            admission = new OrdinaryLoginFixture(loader, combined ? CombinedPackageCatalog() : null);
            if (combined)
            {
                var issuer = Assert.IsType<LegacyOrdinaryAdmissionUidOwner>(typeof(OrdinaryLoginFixture)
                    .GetField("owner", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(admission));
                var roots = issuer.Roots;
                var recovery = new LegacyTradeRecoveryState(roots.Users, roots.Items, roots.Quests,
                    issuer.MailboxRoot, roots.CombatGear, roots.Spells, roots.Proficiencies);
                Assert.True(recovery.TryEnsureInitialized(out var reason), reason);
                Assert.True(LegacyMailboxPackageProducer.TryCreate(issuer.Registry, issuer.Domain, recovery,
                    roots.Users, roots.Items!, issuer.MailboxRoot, roots.CombatGear!, out var producer, out reason), reason);
                PackageProducer = Assert.IsType<LegacyMailboxPackageProducer>(producer);
                Handler = new WorldServerFrameHandler(loader, null!, admission.WorldData, PackageProducer, issuer, Log,
                    ConquerProtocolProfile.VisibleDx8D13);
            }
            else Handler = admission.CreateHandler(Log);
            Native = new LegacyNativeGameClientRuntime();
            Assert.Same(Native, Native.Player.Owner);
            Assert.Null(Native.Socket); // No socket or reflected native SendQueue substitute is installed.
            Connection = NewConnection(Native);
        }

        private static WorldDataCatalog CombinedPackageCatalog()
        {
            var baseline = OrdinaryLoginFixture.Catalog();
            int[] ids = [3_324_226, 4_050_001, 4_040_001, 3_009_003, 3_316_793, 3_330_770];
            var extras = ids.Select(id =>
            {
                var fields = Enumerable.Repeat("0", 66).ToArray();
                fields[0] = id.ToString(CultureInfo.InvariantCulture); fields[1] = "OwnedPackage" + id;
                fields[ItemType.DurabilityFieldIndex] = "321"; fields[ItemType.StackSizeFieldIndex] = "1000";
                return new ItemType(id, fields[1], fields);
            });
            return LegacyItemWorldDataFixture.Create(baseline.Items.Concat(extras).ToArray(), [],
                classLevelStats: baseline.ClassLevelStats, magicTypeOperations: baseline.MagicTypeOperations);
        }

        internal ConquerTcpConnection NewConnection(LegacyNativeGameClientRuntime native)
        {
            var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                (frame, token) =>
                {
                    byte[] bytes = frame.ToArray();
                    if (ObserveSend is { } observer) return observer(bytes, token);
                    Sent.Add(bytes); return ValueTask.CompletedTask;
                },
                _ => { Disconnected = true; return ValueTask.CompletedTask; }, native);
            connections.Add(connection);
            return connection;
        }

        internal async Task LoginAsync() =>
            await Handler.HandleFrameAsync(Connection, ReadFrame(new LegacyMsgLoginGamePacket(Uid).ToFrameBytes()));

        internal async Task FrameAsync(byte[] bytes) => await Handler.HandleFrameAsync(Connection, ReadFrame(bytes));

        internal Dictionary<string, byte[]> Files()
        {
            ValidateRoot();
            RequireOrdinaryTree(Root);
            var lease = Path.Combine(Root, "PlayersItems", ".managed-item-uids", "owner.lock");
            Assert.True(File.Exists(lease)); // Actual live exclusive issuer lease; not a readable save image.
            return Directory.GetFiles(Root, "*", SearchOption.AllDirectories)
                .Where(path => !StringComparer.OrdinalIgnoreCase.Equals(path, lease))
                .ToDictionary(path => path, File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);
        }

        internal void RequireFiles(Dictionary<string, byte[]> before)
        {
            var after = Files();
            Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
            foreach (var file in before) Assert.Equal(file.Value, after[file.Key]);
        }

        internal void RequireCommitObserved() => admission!.AssertCommitObserved();

        private T Actual<T>(string name) where T : class => Assert.IsType<T>(typeof(WorldServerFrameHandler)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Handler)); // Observe real owners; never inject.
    }
}
