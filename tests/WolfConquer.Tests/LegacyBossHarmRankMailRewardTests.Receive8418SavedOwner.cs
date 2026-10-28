using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyBossHarmRankMailRewardTests
{
    // A real ordinary-admission COMPONENT prerequisite for the existing D11
    // continuation vectors. It runs no full world startup/equipment query and
    // issues no fake D13 binding, persistence callback or replacement queue.
    private sealed partial class Receive8418SavedOwner : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-receive8418-owner-" + Guid.NewGuid().ToString("N"));
        private LegacyItemUidRegistry? registry;
        private NativeLoginBeastMetadataScope? beasts;
        private CharacterSnapshotLoader loader = null!;
        private WorldServerFrameHandler handler = null!;
        private bool ownsRoot, disposed;
        internal readonly StringWriter Log = new();
        internal ConquerTcpConnection Connection { get; private set; } = null!;
        internal WorldSession Session { get; private set; } = null!;
        internal WorldSessionStore Sessions => Actual<WorldSessionStore>("_sessions");
        private LegacyTradeRecoveryState Recovery => Actual<LegacyTradeRecoveryState>("_tradeRecovery");

        internal static Receive8418SavedOwner Create(WorldSession input, WorldDataCatalog world,
            Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> send)
        {
            var owner = new Receive8418SavedOwner();
            try { owner.Initialize(input, world, send); return owner; }
            catch (Exception primary)
            {
                try { owner.Dispose(); }
                catch (Exception cleanup) { throw new AggregateException("Receive8418 owner setup and cleanup failed.", primary, cleanup); }
                throw;
            }
        }

        private void Initialize(WorldSession input, WorldDataCatalog world,
            Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> send)
        {
            ValidateRoot();
            Assert.False(Directory.Exists(root) || File.Exists(root));
            var users = Path.Combine(root, "Users");
            Directory.CreateDirectory(users); ownsRoot = true;
            var attributes = new Dictionary<string, string>(input.Character.Attributes, StringComparer.OrdinalIgnoreCase)
                { ["ClassExperience"] = "0" };
            WriteCharacterSnapshotForSession(users, input with { Character = input.Character with { Attributes = attributes } });
            loader = new CharacterSnapshotLoader(users);
            var roots = LegacyOrdinaryAdmissionRoots.FromLoader(loader);
            foreach (var kind in roots.Kinds) Directory.CreateDirectory(roots.Root(kind)!);
            new PlayerItemSnapshotLoader(roots.Items!).SaveByCharacterUid(input.Character.Uid, input.PlayerItems);
            var spells = PlayerSpellSnapshotLoader.ToLegacyBytes(input.PlayerSpells);
            File.WriteAllBytes(roots.PathFor(LegacyTradeJournalFileKind.Spells, input.Character.Uid), spells);
            new PlayerProficiencySnapshotLoader(roots.Proficiencies!).SaveByCharacterUid(input.Character.Uid, input.PlayerProficiencies);
            new PlayerQuestSnapshotLoader(roots.Quests!).SaveByCharacterUid(input.Character.Uid, input.Character.PlayerQuests);
            beasts = new NativeLoginBeastMetadataScope(1000);
            var mailbox = Path.Combine(root, "PlayersMailList");
            Directory.CreateDirectory(mailbox);
            var options = new LegacyItemUidRegistryOptions(roots.Users, roots.Items!, mailbox,
                Path.Combine(root, "RedeemContainer.bin"), Path.Combine(root, "ClaimContainer.bin"), 2_000_000,
                "owned empty-item synthetic receive8418 fixture; no historical issuance");
            Assert.True(LegacyItemUidRegistry.TryBootstrap(options, out var actualRegistry, out var reason), reason);
            registry = Assert.IsType<LegacyItemUidRegistry>(actualRegistry);
            var state = File.ReadAllBytes(Path.Combine(roots.Items!, ".managed-item-uids", "state.json"));
            var domain = Guid.ParseExact(JsonNode.Parse(state)!["Domain"]!.GetValue<string>(), "N");
            Assert.True(LegacyOrdinaryAdmissionUidOwner.TryCreate(registry, domain, roots, mailbox, out var issuer, out reason), reason);
            handler = new(loader, null, world, Assert.IsType<LegacyOrdinaryAdmissionUidOwner>(issuer), Log,
                ConquerProtocolProfile.RootD11_7899);
            var client = Assert.IsType<LegacyNativeGameClientRuntime>(input.NativeGameClient);
            Assert.Equal(0U, client.Player.UID);
            Assert.Same(client, client.Player.Owner);
            Assert.Null(client.OrdinarySpellBinding);
            Connection = new(null, null, send, _ => new ValueTask(Connection.CompleteTransportCloseAsync()), client);
            RegisterRealCloseCleanup();
            Assert.True(Recovery.TryEnsureInitialized(out reason), reason);
            var saved = Assert.IsType<CharacterSnapshot>(loader.TryLoadByAccountIdForAdmission(input.AccountId).Snapshot);
            Assert.True(LegacyOrdinaryAdmissionPlan.TryPrepare(handler, Connection, input.AccountId, input.Username,
                saved, loader, issuer, Recovery, TimeProvider.System, out var plan, out reason), reason);
            Assert.NotNull(plan);
            Assert.True(Sessions.TryCommitOrdinaryAdmission(Connection, plan, out var admitted, out reason), reason);
            Session = Assert.IsType<WorldSession>(admitted);
            Assert.Same(client, Connection.NativeGameClient);
            Assert.Same(client, Session.NativeGameClient);
            Assert.Equal(checked((uint)input.Character.Uid), client.Player.UID);
            Assert.Null(client.OrdinarySpellBinding); // Preserve this existing D11 component's actual profile.
            Assert.Equal(input.Character.MinHitPoints, Session.Character.MinHitPoints);
            Assert.Equal(input.Character.Level, Session.Character.Level);
            Assert.False(Recovery.IsBlocked);
        }

        private T Actual<T>(string name) where T : class => Assert.IsType<T>(typeof(WorldServerFrameHandler)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(handler));

        internal WorldPacketHandlerContext CreateContext(WorldDataCatalog world, MonsterRuntimeStateStore states) =>
            new(Connection, Log, Session, WorldData: world, Sessions: Sessions, MonsterStates: states,
                PlayerSpellsRoot: loader.PlayerSpellsRoot, PlayerItemsRoot: loader.PlayerItemsRoot,
                PlayerProficienciesRoot: loader.PlayerProficienciesRoot, PlayerQuestsRoot: loader.PlayerQuestsRoot,
                ProtocolProfile: ConquerProtocolProfile.RootD11_7899)
            {
                Trades = Actual<LegacyTradeStateStore>("_tradeStates"),
                TradeOperations = Actual<WorldOperationBarrier>("_worldOperations"),
                TradeRecovery = Recovery,
            };

        internal void AssertSavedSpells(WorldSession current)
        {
            var path = Path.Combine(loader.PlayerSpellsRoot!, current.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
            var bytes = File.ReadAllBytes(path);
            Assert.Equal(current.PlayerSpells.Spells.ToArray(), PlayerSpellSnapshotLoader.Parse(bytes).Spells.ToArray());
            Assert.False(Recovery.IsBlocked);
        }
    }
}
