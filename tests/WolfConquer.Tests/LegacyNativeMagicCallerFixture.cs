using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private sealed class NativeMagicCallerFixture : IDisposable
    {
        private readonly WorldSessionStore sessions = new();
        private readonly StringWriter log = new(CultureInfo.InvariantCulture);
        private readonly ConquerTcpConnection ownerConnection;
        private readonly ConquerTcpConnection peerConnection;
        private readonly WorldPacketHandlerContext context;
        private readonly ManualRuntimeTimeProvider clock = new(
            new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero));

        public NativeMagicCallerFixture(ushort spellId, uint rawDamage, byte soul)
        {
            var baseFamily = CreateMonsterFamily();
            var family = baseFamily with
            {
                Attributes = new Dictionary<string, string>(baseFamily.Attributes) { ["defense2"] = "0" },
            };
            var spawn = CreateMonsterSpawn(boundX: 832, boundY: 584);
            var fields = Enumerable.Repeat("0", 60).ToArray();
            fields[0] = "111095"; fields[1] = "NativeCallerHelmet";
            fields[18] = "5000"; fields[19] = "1000"; // Declared raw HP/MP, no MagicAttack bonus.
            var auraFields = "11200@@11200@@51@@MagicDefender@@0@@0@@1@@4@@0@@0@@0@@0@@100@@15@@4@@0@@126@@0@@950@@157@@0@@0@@900@@0@@0@@0@@0@@0@@0@@30@@0@@0@@0@@0@@0@@3015@@5@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@20000@@800@@0@@0@@0@@0@@".Split("@@", StringSplitOptions.None);
            var world = CreateMagicCatalogCore(family, [spawn], [],
                [CreateMagicRow(spellId, sort: 1, damage: rawDamage, useMana: 0,
                    useStamina: 0, range: 18, coolDown: 0, coldTime: 0),
                    new MagicType(11_200, 11_200, auraFields[3], auraFields)],
                items: [new ItemType(111_095, fields[1], fields)]);
            Monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
                spawn, family, world.GetLegacyMonsterRuntimeUidBase(spawn)));
            MonsterStates = new MonsterRuntimeStateStore();
            var owner = CreateSession(mapId: 1_015, strength: 0, spirit: 0,
                minHitPoints: 5_000, minMana: 1_000, stamina: 100,
                spellEntries: [new PlayerSpellSnapshotEntry(spellId, Level: 0, Experience: 0,
                    PreviousLevel: 0, SoulLevel: soul, UseSpellSoul: soul)],
                playerItems: PlayerItemSnapshot.Empty with
                {
                    EquipmentItems = [new PlayerItemSnapshotItem
                    {
                        Uid = 99_291, ItemId = 111_095, Position = 1,
                        Durability = 100, MaximumDurability = 100,
                    }],
                },
                attributes: new Dictionary<string, string> { ["PkMode"] = "0" });
            var peer = CreateSession(uid: TargetPlayerUid, name: "NativeCallerPeer", mapId: 1_015,
                x: 832, y: 584, strength: 0, spirit: 0, minHitPoints: 5_000,
                minMana: 0, stamina: 100, spellIds: [11_200],
                attributes: new Dictionary<string, string> { ["PkMode"] = "0" });
            ownerConnection = CreateSendingConnection(OwnerFrames);
            peerConnection = CreateSendingConnection(PeerFrames);
            sessions.Set(ownerConnection, owner);
            sessions.Set(peerConnection, peer);
            context = new WorldPacketHandlerContext(ownerConnection, log, owner,
                WorldData: world, Sessions: sessions, MonsterStates: MonsterStates,
                TimeProvider: clock, ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        }

        public List<byte[]> OwnerFrames { get; } = [];
        public List<byte[]> PeerFrames { get; } = [];
        public LegacyMonsterInstance Monster { get; }
        public MonsterRuntimeStateStore MonsterStates { get; }
        public DateTimeOffset Now => clock.GetUtcNow();
        public WorldSession Owner => Assert.IsType<WorldSession>(
            sessions.TryGet(ownerConnection, out var current) ? current : null);
        public WorldSession Peer => Assert.IsType<WorldSession>(
            sessions.TryGet(peerConnection, out var current) ? current : null);

        public async Task QueryMagicAttackAsync()
        {
            var start = OwnerFrames.Count;
            await PacketHandlers.HandleAsync(context with { Session = Owner },
                ReadFrame(new MsgActionPacket { ObjId = PlayerUid, Type = (MsgActionType)251 }.ToFrameBytes()));
            var bytes = Assert.Single(OwnerFrames.Skip(start),
                bytes => LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out _));
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out var status));
            Assert.Equal(PlayerUid, status.Uid);
            Assert.Equal(1U, status.MagicAttack); // Source Equip1540: unconditional ++ after raw Spirit0.
            Assert.Equal(5_000U, status.MaxHP);
            Assert.Equal(1_000U, status.MaxMana);
            Assert.Equal(5_000L, Owner.Character.MinHitPoints);
            Assert.Equal(1_000L, Owner.Character.MinMana);
        }

        public async Task EarnPeerMagicDefenderAsync()
        {
            Assert.False(HasLegacyStatusFlag(Peer.Character.Attributes, 128));
            var start = PeerFrames.Count;
            await PacketHandlers.HandleAsync(context with { Connection = peerConnection, Session = Peer },
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                    Uid: TargetPlayerUid, OpponentUid: TargetPlayerUid, X: 832, Y: 584,
                    AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
                    ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0,
                    SpellId: 11_200)).ToFrameBytes()));
            Assert.True(HasLegacyStatusFlag(Peer.Character.Attributes, 128));
            Assert.Equal(5_000L, Peer.Character.MinHitPoints);
            Assert.Contains(ParseMsgUpdates(PeerFrames.Skip(start)), packet =>
                packet.Uid == TargetPlayerUid && packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.AzureShield && entry.Flag == 128 &&
                    entry.Time == 15 && entry.Damage == 0));
            Assert.Single(ParseSpellAnimations(PeerFrames.Skip(start)),
                packet => packet.SpellId == 11_200); // Genuine Auras producer, not Azure93 or a seeded flag.
        }

        public Task CastAsync(ushort spellId, uint opponentUid) =>
            PacketHandlers.HandleAsync(context with { Session = Owner },
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                    Uid: PlayerUid, OpponentUid: opponentUid, X: 832, Y: 584,
                    AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
                    ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0,
                    SpellId: spellId)).ToFrameBytes())).AsTask();

        public void Dispose()
        {
            GC.KeepAlive(ownerConnection);
            GC.KeepAlive(peerConnection);
            log.Dispose(); // No loops, filesystem persistence, login or background task is started.
        }
    }
}
