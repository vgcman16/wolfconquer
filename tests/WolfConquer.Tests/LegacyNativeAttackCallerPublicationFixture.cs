using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private sealed class NativeAttackCallerPublicationFixture : IDisposable
    {
        private readonly WorldSessionStore sessions = new();
        private readonly StringWriter log = new(CultureInfo.InvariantCulture);
        private readonly ConquerTcpConnection ownerConnection;
        private readonly ConquerTcpConnection peerConnection;
        private readonly WorldPacketHandlerContext context;
        private readonly string root = Path.Combine(Path.GetTempPath(),
            "wolf-native-attack-caller-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        private readonly string proficienciesRoot;
        private readonly string spellsRoot;
        private readonly long initialPeerHitpoints;

        public NativeAttackCallerPublicationFixture(ushort spellId, byte soul, long peerHitpoints,
            bool lightningShield = false)
        {
            initialPeerHitpoints = peerHitpoints;
            proficienciesRoot = Path.Combine(root, "PlayersProfs");
            spellsRoot = Path.Combine(root, "PlayersSpells");
            var fields = Enumerable.Repeat("0", 64).ToArray();
            fields[0] = "111095"; fields[1] = "NativeCallerPublicationHelmet";
            fields[18] = "5000"; fields[19] = "1000";
            var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
                [CreateMagicRow(spellId, sort: 1, level: 0, damage: 8999,
                    useMana: 0, useStamina: 0, range: 18, coolDown: 0, coldTime: 0,
                    duration: 1, experience: 1_000_000),
                 CreateMagicRow(spellId, sort: 1, level: 1, damage: 8999,
                    useMana: 0, useStamina: 0, range: 18, coolDown: 0, coldTime: 0,
                    duration: 1, experience: 1_000_000),
                 .. (lightningShield ? new[] { CreateMagicRow(15_750, sort: 6,
                     damage: 100, duration: 30, useMana: 0, useStamina: 0,
                     coolDown: 0, coldTime: 0) } : Array.Empty<MagicType>())],
                items: [new ItemType(111_095, fields[1], fields),
                    CreateStatusItemType(421_003, "NativeCallerPublicationBacksword")]);
            InitialSpell = new PlayerSpellSnapshotEntry(spellId, Level: 0, Experience: 10,
                PreviousLevel: 0, SoulLevel: soul, UseSpellSoul: soul);
            InitialProficiency = new PlayerProficiencySnapshotEntry(421, Level: 0, PreviousLevel: 0,
                Experience: 100, NeededExperience: LegacyProficiencyPacket.GetNeededExperience(0));
            var owner = CreateSession(spirit: 0, minHitPoints: 5000, minMana: 1000,
                spellEntries: [InitialSpell],
                playerProficiencies: new PlayerProficiencySnapshot([InitialProficiency]),
                playerItems: PlayerItemSnapshot.Empty with { EquipmentItems =
                    [new PlayerItemSnapshotItem { Uid = 99201, ItemId = 111_095, Position = 1,
                        Durability = 100, MaximumDurability = 100 },
                     new PlayerItemSnapshotItem { Uid = 99202, ItemId = 421_003, Position = 4,
                        Durability = 100, MaximumDurability = 100 }] });
            var peer = CreateSession(uid: TargetPlayerUid, name: "NativeCallerPublicationPeer",
                x: 832, y: 584, spirit: 0, minHitPoints: peerHitpoints, minMana: 1000,
                spellIds: lightningShield ? [15_750] : [],
                attributes: lightningShield
                    ? new Dictionary<string, string> { ["LegacyLightningShieldRoll"] = "0" } : null,
                playerItems: PlayerItemSnapshot.Empty with { EquipmentItems =
                    [new PlayerItemSnapshotItem { Uid = 99203, ItemId = 111_095, Position = 1,
                        Durability = 100, MaximumDurability = 100 }] });
            ownerConnection = CreateSendingConnection(OwnerFrames);
            peerConnection = CreateSendingConnection(PeerFrames);
            sessions.Set(ownerConnection, owner);
            sessions.Set(peerConnection, peer);
            // Declared initial snapshot input, not an earned or desired post-cast outcome.
            new PlayerProficiencySnapshotLoader(proficienciesRoot).SaveByCharacterUid(PlayerUid, owner.PlayerProficiencies);
            new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(PlayerUid, owner.PlayerSpells);
            context = new WorldPacketHandlerContext(ownerConnection, log, owner, WorldData: world,
                Sessions: sessions, PlayerSpellsRoot: spellsRoot, PlayerProficienciesRoot: proficienciesRoot,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13,
                TimeProvider: new ManualRuntimeTimeProvider(new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero)));
        }

        public List<byte[]> OwnerFrames { get; } = [];
        public List<byte[]> PeerFrames { get; } = [];
        public PlayerSpellSnapshotEntry InitialSpell { get; }
        public PlayerProficiencySnapshotEntry InitialProficiency { get; }
        public WorldSession Owner => Assert.IsType<WorldSession>(
            sessions.TryGet(ownerConnection, out var current) ? current : null);
        public WorldSession Peer => Assert.IsType<WorldSession>(
            sessions.TryGet(peerConnection, out var current) ? current : null);
        public PlayerProficiencySnapshotEntry PersistedProficiency => Assert.Single(
            new PlayerProficiencySnapshotLoader(proficienciesRoot).LoadByCharacterUid(PlayerUid).Proficiencies);
        public PlayerSpellSnapshotEntry PersistedSpell => Assert.Single(
            new PlayerSpellSnapshotLoader(spellsRoot).LoadByCharacterUid(PlayerUid).Spells);

        public async Task PrepareRealQueriesAsync()
        {
            async Task Query(ConquerTcpConnection connection, List<byte[]> frames, long expectedHitpoints)
            {
                var actor = Assert.IsType<WorldSession>(
                    sessions.TryGet(connection, out var current) ? current : null);
                var start = frames.Count;
                await PacketHandlers.HandleAsync(context with { Connection = connection, Session = actor },
                    ReadFrame(new MsgActionPacket { ObjId = checked((uint)actor.Character.Uid),
                        Type = (MsgActionType)251 }.ToFrameBytes()));
                var bytes = Assert.Single(frames.Skip(start),
                    bytes => LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out _));
                Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out var status));
                Assert.Equal(1U, status.MagicAttack);
                Assert.Equal(5000U, status.MaxHP);
                Assert.Equal(1000U, status.MaxMana);
                Assert.True(sessions.TryGet(connection, out var refreshed));
                Assert.Equal(expectedHitpoints, Assert.IsType<WorldSession>(refreshed).Character.MinHitPoints);
            }
            await Query(ownerConnection, OwnerFrames, 5000);
            await Query(peerConnection, PeerFrames, initialPeerHitpoints);
            OwnerFrames.Clear(); PeerFrames.Clear(); // Both raw-input query proofs completed above.
        }

        public async Task PrepareRealLightningShieldAsync()
        {
            Assert.False(HasLegacyStatusFlag(Peer.Character.Attributes, LegacyLightningShieldFlag));
            Assert.False(HasLegacyStatusFlag(Peer.Character.Attributes, LegacyLightningShieldActivatedFlag));
            await PacketHandlers.HandleAsync(context with { Connection = peerConnection, Session = Peer },
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: TargetPlayerUid,
                    OpponentUid: TargetPlayerUid, X: 832, Y: 584, AttackType: MsgAttackType.Magic,
                    Damage: 0, KilledMonster: 0, ResponseDamage: 0, Effect: MsgAttackEffect.None,
                    Unknown: 0, SpellId: 15_750)).ToFrameBytes()));
            Assert.True(HasLegacyStatusFlag(Peer.Character.Attributes, LegacyLightningShieldFlag));
            Assert.False(HasLegacyStatusFlag(Peer.Character.Attributes, LegacyLightningShieldActivatedFlag));
            Assert.False(Peer.Character.Attributes.ContainsKey("LightningShieldLeft"));
            Assert.Equal(5000L, Peer.Character.MinHitPoints);
            var animation = Assert.Single(ParseSpellAnimations(PeerFrames));
            Assert.Equal((ushort)15_750, animation.SpellId);
            var self = Assert.Single(animation.Targets);
            Assert.Equal(TargetPlayerUid, self.Uid);
            Assert.Equal(0U, self.Damage);
            Assert.True(self.Hit);
            OwnerFrames.Clear(); PeerFrames.Clear(); // Only completed, positively proved setup frames.
        }

        public Task CastAsync(ushort spellId) => PacketHandlers.HandleAsync(context with { Session = Owner },
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: PlayerUid, OpponentUid: TargetPlayerUid,
                X: 832, Y: 584, AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0, SpellId: spellId)).ToFrameBytes())).AsTask();

        public void Dispose()
        {
            log.Dispose();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            GC.KeepAlive(ownerConnection); GC.KeepAlive(peerConnection);
        }
    }
}
