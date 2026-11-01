using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacySkyTowerAttributeScopeTests
{
    private const int InstanceMap = 10_429, SourceMap = 10_428, Family = 6_072;
    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> Handlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    private sealed class TowerFixture : IDisposable
    {
        internal readonly MonsterRuntimeStateStore Monsters = new();
        internal readonly WorldSessionStore Sessions = new();
        internal readonly StringWriter Log = new();
        internal readonly List<byte[]> Sent = [];
        internal Action<byte[]>? OnSend;
        private readonly ConquerTcpConnection connection;
        private readonly WorldDataCatalog world;
        internal LegacyMonsterInstance Objective { get; private set; } = null!;
        internal ulong DynamicId { get; private set; }
        internal WorldSession Current => Assert.IsType<WorldSession>(Sessions.TryGet(connection, out var current) ? current : null);

        private TowerFixture()
        {
            connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (memory, _) =>
            {
                var bytes = memory.ToArray(); OnSend?.Invoke(bytes); Sent.Add(bytes);
                return ValueTask.CompletedTask;
            });
            // Declared component metadata, matching the existing SkyTower
            // combat fixture. Exact6072 avoids compatibility-family fallback.
            var family = new Monster(Family, "FlameDevil", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["life"] = "40000000", ["lookface"] = "171", ["level"] = "140",
                ["defense"] = "100", ["defence"] = "100", ["defense2"] = "10000",
                ["defence2"] = "10000", ["attack_range"] = "1",
            }, []);
            var sourceMap = new MapMetadata(SourceMap, "10428.ini", "Anima", MapDoc: SourceMap,
                TypeRaw: "18685966", Type: 18_685_966, Portal0X: null, Portal0Y: null,
                RebornMap: 1002, RebornPortal: 0, ResLev: 0, LinkMap: 0, LinkX: 0, LinkY: 0,
                ColorRaw: "0", Color: 0, Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            world = MsgTeamRuntimeTests.CreateTeamArenaRewardCatalog(maps: [sourceMap], monsters: [family]);
            // DATA constructor only; no native attachment, admission grant,
            // accepting Frame owner or persistence receipt is fabricated.
            var character = OwnedEquipmentBehaviorFixture.Character(strength: 1_000, mapId: 1002, x: 425, y: 404) with
                { Money = 0, ConquerPoints = 0, BoundConquerPoints = 0, BankConquerPoints = 0,
                  Online = 7, SourceFile = "fixture://sky-tower-attribute-scope" };
            Sessions.Set(connection, new WorldSession(character.Uid, "HeroOne", character,
                PlayerItemSnapshot.Empty, PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty));
        }

        internal static async Task<TowerFixture> EnterAsync()
        {
            var f = new TowerFixture();
            try
            {
                await Handlers.HandleAsync(f.Context(), ReadFrame(new LegacyInstancePacket(LegacyInstanceAction.Info,
                    weather: 476, userId: checked((uint)f.Current.Character.Uid)).ToFrameBytes()));
                Assert.Equal(InstanceMap, f.Current.Character.MapId);
                f.DynamicId = ulong.Parse(f.Current.Character.Attributes["DynamicID"], CultureInfo.InvariantCulture);
                f.Objective = Assert.Single(f.Monsters.GetVisibleDynamicMonsters(InstanceMap, f.DynamicId, DateTimeOffset.UtcNow)).Instance;
                Assert.Equal(6_072U, f.Objective.MonsterId);
                Assert.Equal("10", f.Current.Character.Attributes["SkyTowerAvailableAttempts"]);
                Assert.Null(f.Current.NativeGameClient); f.Sent.Clear(); return f;
            }
            catch { f.Dispose(); throw; }
        }

        internal Dictionary<string, string> InstallOrdinalAttributes() => new(Current.Character.Attributes, StringComparer.Ordinal)
            { ["Other"] = "upper", ["other"] = "lower" };

        internal void PublishInput(Dictionary<string, string> fields)
        {
            var before = Current;
            Sessions.Set(connection, before with { Character = before.Character with { Attributes = fields } });
        }

        internal void PrepareLastHit()
        {
            var setup = Monsters.ApplyDamage(Objective, Objective.MaxLife - 1, DateTimeOffset.UtcNow);
            Assert.False(setup.Killed); Assert.Equal(1U, setup.RemainingHitpoints); Sent.Clear();
        }

        internal async Task AttackAsync() => await Handlers.HandleAsync(Context(), ReadFrame(new MsgAttackPacket(
            new MsgAttackQuery(checked((uint)Current.Character.Uid), Objective.Uid, Objective.X, Objective.Y,
                MsgAttackType.Physical, 0, 0, 0, MsgAttackEffect.None, 0)).ToFrameBytes()));

        internal byte[][] ProgressFrames() => Sent.Where(IsProgress).ToArray();
        private WorldPacketHandlerContext Context() => new(connection, Log, Current, WorldData: world,
            Sessions: Sessions, MonsterStates: Monsters, ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        internal static PacketFrame ReadFrame(byte[] bytes)
        {
            Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _)); return frame;
        }
        public void Dispose() { OnSend = null; Sessions.TryRemove(connection, out _); Log.Dispose(); }
    }
}
