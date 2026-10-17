using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private sealed class MonsterPostInteractionFixture : IDisposable
    {
        private const uint HelmetId = 111_081, PeerHelmetId = 111_082;
        private readonly WorldSessionStore sessions = new();
        private readonly TeamSessionStateStore teams = new();
        private readonly Dictionary<uint, ConquerTcpConnection> connections = [];
        private readonly ManualRuntimeTimeProvider clock = new(new DateTimeOffset(2026, 10, 16, 14, 0, 0, TimeSpan.Zero));
        private readonly StringWriter log = new();
        private readonly WorldPacketHandlerContext context;
        private readonly Monster family;
        private readonly LegacyMonsterInstance monster;

        public MonsterPostInteractionFixture(long hitpoints = 300, uint maximum = 500, uint attack = 200,
            bool mutableCounteraction = false,
            Func<MonsterPostInteractionFixture, uint, byte[], ValueTask>? onSend = null,
            bool lightningShield = false, bool counterPunch = false, bool mutableBreakthrough = false)
        {
            family = CreateMonsterFamily() with
            {
                Attributes = new Dictionary<string, string>(CreateMonsterFamily().Attributes)
                {
                    ["attack_min"] = attack.ToString(CultureInfo.InvariantCulture),
                    ["attack_max"] = (attack + 1).ToString(CultureInfo.InvariantCulture), ["attack_range"] = "2",
                },
            };
            monster = new LegacyMonsterInstance(400_001, 1_000, 831, 584, 100, 100, 5_000, 5_000,
                9_001, "TrainingMonster", false, 1, 0);
            ItemType Helmet(uint id, uint hp, uint breakthrough, uint counteraction, uint critical = 0)
            {
                var row = DirectOverlayItem(id, "MonsterPostInteraction" + id, critical, breakthrough, counteraction);
                var fields = row.Fields.ToArray();
                fields[18] = hp.ToString(CultureInfo.InvariantCulture); // Legal source ushort item HP.
                return row with { Fields = fields };
            }
            var rows = new List<ItemType> { Helmet(HelmetId, maximum, 60, 50, mutableBreakthrough ? 1_000U : 0U),
                Helmet(PeerHelmetId, 5_000, 100, 0, 1_000) };
            if (mutableCounteraction)
                rows.Add(DirectOverlayItem(DirectOverlaySyntheticImpregnabilityRuneId, "Impregnability(+9)"));
            if (mutableBreakthrough)
                rows.Add(DirectOverlayItem(DirectOverlayFrontBreakRuneId, "Front Break (+9)"));
            var responseRows = new List<MagicType>();
            var ownerSpellIds = new List<ushort> { 1_350, 14_500 };
            if (mutableBreakthrough) ownerSpellIds.Add(17_490);
            if (lightningShield)
            {
                responseRows.Add(CreateMagicRow(15_750, sort: 6, useMana: 0, useStamina: 0, damage: 100,
                    duration: 30, coolDown: 0, coldTime: 0));
                ownerSpellIds.Add(15_750);
            }
            if (counterPunch)
            {
                responseRows.Add(CreateMagicRow(14_470, sort: 1, useMana: 0, useStamina: 0, damage: 30_800,
                    range: 2, coolDown: 0, coldTime: 0));
                ownerSpellIds.Add(14_470);
            }
            var world = CreateMagicCatalogCore(family, [], [],
            [
                CreateMagicRow(1_350, sort: 19, useMana: 0, useStamina: 0, damage: 100, duration: 60,
                    experience: 1_000, coolDown: 0, coldTime: 0),
                CreateMagicRow(1_350, sort: 19, level: 1, useMana: 0, useStamina: 0, damage: 100, duration: 60,
                    experience: 1_000, coolDown: 0, coldTime: 0),
                CreateMagicRow(14_500, sort: 6, useMana: 0, useStamina: 0, damage: 1, duration: 60,
                    coolDown: 0, coldTime: 0),
                CreateMagicRow(17_490, sort: 99, useMana: 0, useStamina: 0, damage: 30_250, range: 4,
                    damageOnHuman: 0, damage2: 33, damage3: 2, damageOnMonster: 30_085, gDamage: 0,
                    coolDown: 0, coldTime: 0),
                .. responseRows,
            ], items: rows, transformInfo:
            [
                new TransformInfo(1_350, 0, "DivineHare", 207, 3_000, "fixture://TransformInfo.txt",
                    LineNumber: 1, RawLine: "1350 0 DivineHare 207 3000",
                    Fields: ["1350", "0", "DivineHare", "207", "3000"]),
            ]);
            PlayerItemSnapshot Items(uint uid, uint id) => PlayerItemSnapshot.Empty with
            {
                EquipmentItems = [new PlayerItemSnapshotItem
                {
                    Uid = uid + 100, ItemId = id, Position = 1, Durability = 100, MaximumDurability = 100,
                }],
            };
            var owner = CreateSession(uid: TargetPlayerUid, name: "MonsterPostInteractionOwner", mapId: 1_000,
                x: 830, y: 584, classCode: 2_005, reborn: 2, strength: 0, spirit: 0,
                minHitPoints: hitpoints, minMana: 1_000, stamina: 100, spellIds: ownerSpellIds,
                playerItems: Items(TargetPlayerUid, HelmetId) with
                {
                    RuneItems = mutableCounteraction ? [new PlayerItemSnapshotItem
                    {
                        Uid = TargetPlayerUid + 200, ItemId = DirectOverlaySyntheticImpregnabilityRuneId,
                        Position = 104, Durability = 100, MaximumDurability = 100,
                    }] : [],
                },
                attributes: new Dictionary<string, string>
                {
                    ["PkMode"] = "0", ["BattlePower"] = "200", ["LegacyPhysicalImpregnabilityRoll"] = "0",
                });
            if (mutableBreakthrough)
            {
                var attributes = new Dictionary<string, string>(owner.Character.Attributes, StringComparer.OrdinalIgnoreCase)
                {
                    ["LegacyCombatGearAttackSpellIndex"] = "3", ["LegacyCombatGearAttackRateRoll"] = "99",
                    ["LegacyPhysicalRefineryRoll"] = "0", ["LegacyPhysicalPlayerLuckyBranchRoll"] = "99",
                    ["LegacyPhysicalPlayerCriticalRoll"] = "99", ["LegacyPhysicalDashRoll"] = "0", ["DashRate"] = "0",
                    ["LegacyPhysicalBreakthroughRefineryRoll"] = "0",
                };
                owner = owner with
                {
                    Character = owner.Character with { Attributes = attributes },
                    CombatGear = CombatGearSnapshot.Empty with
                    {
                        Count = 1, Items = [new CombatGearSnapshotItem(201, Level: 1, Exp: 0, Equip: 1,
                            Transform: 0, MasteryScore: 0, Animas: [], BlackJades: [])],
                    },
                    PlayerItems = owner.PlayerItems with
                    {
                        RuneItems = [.. owner.PlayerItems.RuneItems, new PlayerItemSnapshotItem
                        {
                            Uid = TargetPlayerUid + 201, ItemId = DirectOverlayFrontBreakRuneId,
                            Position = 103, Durability = 100, MaximumDurability = 100,
                        }],
                    },
                };
            }
            if (lightningShield || counterPunch)
            {
                var attributes = new Dictionary<string, string>(owner.Character.Attributes, StringComparer.OrdinalIgnoreCase);
                if (lightningShield) attributes["LegacyLightningShieldRoll"] = "0";
                if (counterPunch) attributes["LegacyMonsterCounterPunchRoll"] = "0";
                owner = owner with { Character = owner.Character with { Attributes = attributes } };
            }
            var peer = CreateSession(uid: PlayerUid, name: "MonsterPostInteractionPeer", mapId: 1_000,
                x: 832, y: 584, classCode: 2_005, strength: mutableCounteraction ? 300 : 0, spirit: 0,
                minHitPoints: 5_000, stamina: 100, spellIds: mutableCounteraction ? [17_490] : [],
                playerItems: Items(PlayerUid, PeerHelmetId), attributes: new Dictionary<string, string>
                {
                    ["PkMode"] = "0", ["BattlePower"] = "100",
                    ["LegacyCombatGearAttackSpellIndex"] = "3", ["LegacyCombatGearAttackRateRoll"] = "99",
                    ["LegacyPhysicalRefineryRoll"] = "0", ["LegacyPhysicalPlayerLuckyBranchRoll"] = "99",
                    ["LegacyPhysicalPlayerCriticalRoll"] = "99", ["LegacyPhysicalDashRoll"] = "0", ["DashRate"] = "0",
                    ["LegacyPhysicalBreakthroughRefineryRoll"] = "0",
                }) with
            {
                CombatGear = mutableCounteraction ? CombatGearSnapshot.Empty with
                {
                    Count = 1, Items = [new CombatGearSnapshotItem(201, Level: 1, Exp: 0, Equip: 1,
                        Transform: 0, MasteryScore: 0, Animas: [], BlackJades: [])],
                } : null,
            };
            foreach (var uid in new[] { TargetPlayerUid, PlayerUid })
                connections[uid] = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                    sendAsync: async (memory, _) =>
                    {
                        var bytes = memory.ToArray();
                        Order.Enqueue((uid, bytes));
                        if (onSend is not null) await onSend(this, uid, bytes);
                    });
            sessions.Set(connections[TargetPlayerUid], owner);
            sessions.Set(connections[PlayerUid], peer);
            context = new WorldPacketHandlerContext(connections[TargetPlayerUid], TextWriter.Synchronized(log), owner,
                WorldData: world, Sessions: sessions, TeamStates: teams, TimeProvider: clock,
                MonsterStates: counterPunch ? new MonsterRuntimeStateStore() : null,
                LegacyClock: LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000),
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            Assert.Null(owner.EquipmentHitpointsStatus);
            Assert.Null(owner.PhysicalStatusOverlay);
            Assert.Null(owner.FineRainEquipmentAdmission);
            Assert.False(owner.Character.Attributes.ContainsKey("MaxHitpoints"));
        }

        public ConcurrentQueue<(uint Recipient, byte[] Bytes)> Order { get; } = new();
        public DateTimeOffset Origin => new(2026, 10, 16, 14, 0, 0, TimeSpan.Zero);
        public int ScheduledTimers => clock.ScheduledTimerCount;
        public WorldSession Owner => Slot(TargetPlayerUid);
        public uint MonsterHitpoints
        {
            get
            {
                var states = Assert.IsType<MonsterRuntimeStateStore>(context.MonsterStates);
                Assert.True(states.TryGetVisibleInstance(monster, clock.GetUtcNow(), out var visible));
                return visible.Hitpoints;
            }
        }
        private WorldSession Slot(uint uid)
        {
            Assert.True(sessions.TryGet(connections[uid], out var current));
            return Assert.IsType<WorldSession>(current);
        }
        private WorldPacketHandlerContext Context(uint uid) =>
            context with { Connection = connections[uid], Session = Slot(uid) };
        public bool TryOwner(out WorldSession? session) => sessions.TryGet(connections[TargetPlayerUid], out session);
        public void BindOwner(WorldSession session) => sessions.Set(connections[TargetPlayerUid], session);
        public void RemoveOwner() => Assert.True(sessions.TryRemove(connections[TargetPlayerUid], out _));
        public WorldSession RebindOwner()
        {
            var replacement = CreateSession(uid: TargetPlayerUid + 9_000, name: "MonsterPostInteractionReplacement",
                mapId: 1_000, x: 830, y: 584, minHitPoints: 77_777, minMana: 77, spellIds: []);
            BindOwner(replacement);
            return replacement;
        }
        public void ChangeVitals(long hp, long mana = 77, long x = 840, long y = 585)
        {
            var owner = Owner;
            BindOwner(owner with { Character = owner.Character with { MinHitPoints = hp, MinMana = mana, X = x, Y = y } });
        }
        public void SetActivePickBranchInput()
        {
            var owner = Owner;
            var attributes = new Dictionary<string, string>(owner.Character.Attributes, StringComparer.OrdinalIgnoreCase)
                { ["ActivePick"] = "1" };
            BindOwner(owner with { Character = owner.Character with { Attributes = attributes } });
        }
        public void Advance(TimeSpan amount) => clock.Advance(amount);
        public byte[][] OwnerFramesSince(int start) => Order.ToArray().Skip(start)
            .Where(frame => frame.Recipient == TargetPlayerUid).Select(frame => frame.Bytes).ToArray();
        public async Task<LegacyPlayerStatusPacket> StatusAsync()
        {
            var start = Order.Count;
            await PacketHandlers.HandleAsync(Context(TargetPlayerUid),
                ReadFrame(new LegacyPlayerStatusQueryPacket(TargetPlayerUid).ToFrameBytes()));
            var bytes = Assert.Single(OwnerFramesSince(start),
                frame => LegacyPlayerStatusPacket.TryParse(ReadFrame(frame), out _));
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out var status));
            return status;
        }
        public async Task PrimeAsync(uint maximum)
        {
            var status = await StatusAsync();
            Assert.Equal(maximum, status.MaxHP);
            Assert.Equal(0U, status.Defence);
            Assert.Equal(0U, status.ItemBless);
            Assert.Equal(0U, status.Dodge);
            Assert.Equal(60U, status.Breakthrough);
            Assert.Equal(50U, status.Counteraction);
            Assert.False(HasLegacyStatusFlag(Owner.Character.Attributes, 5));
        }
        public async Task SitAsync()
        {
            await PacketHandlers.HandleAsync(Context(TargetPlayerUid), ReadFrame(new MsgActionPacket
                { ObjId = TargetPlayerUid, DwParam = 250, Type = MsgActionType.ChangeStance }.ToFrameBytes()));
            Assert.Equal("250", Owner.Character.Attributes["Action"]);
        }
        public async Task JoinAsync()
        {
            await PacketHandlers.HandleAsync(Context(TargetPlayerUid),
                ReadFrame(new MsgTeamPacket(MsgTeamType.Create, TargetPlayerUid).ToFrameBytes()));
            await PacketHandlers.HandleAsync(Context(PlayerUid),
                ReadFrame(new MsgTeamPacket(MsgTeamType.AcceptInvitation, TargetPlayerUid).ToFrameBytes()));
            Assert.Equal(2U, teams.GetMemberCount(connections[TargetPlayerUid]));
        }
        public async Task CastSelfAsync(ushort spellId)
        {
            var owner = Owner.Character;
            await PacketHandlers.HandleAsync(Context(TargetPlayerUid), ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: TargetPlayerUid, OpponentUid: TargetPlayerUid, X: checked((ushort)owner.X), Y: checked((ushort)owner.Y),
                AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0, ResponseDamage: 0,
                Effect: MsgAttackEffect.None, Unknown: 0, SpellId: spellId)).ToFrameBytes()));
        }
        public async Task EarnBreakthroughAsync()
        {
            Assert.Equal(501, Owner.Character.MinHitPoints); // Real14500 fill, not an HP/BT answer seed.
            var before = await StatusAsync();
            Assert.Equal(60U, before.Breakthrough);
            Assert.Equal(1_000U, before.CriticalStrike); // Source Physical400 outer block.
            Assert.False(HasLegacyStatusFlag(Owner.Character.Attributes, 313));
            Assert.Contains(Owner.PlayerSpells.Spells, spell => spell.Id == 17_490 && spell.Level == 0);
            Assert.Contains(Owner.PlayerItems.RuneItems, item => item.ItemId == DirectOverlayFrontBreakRuneId && item.Position == 103);
            var start = Order.Count;
            await PacketHandlers.HandleAsync(Context(TargetPlayerUid), ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: TargetPlayerUid, OpponentUid: PlayerUid, X: 832, Y: 584,
                AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0, ResponseDamage: 0,
                Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes()));
            var frames = Order.ToArray().Skip(start).ToArray();
            var ownerFrames = OwnerFramesSince(start);
            var animation = Assert.Single(ParseSpellAnimations(ownerFrames), packet => packet.SpellId == 17_490);
            Assert.Equal(TargetPlayerUid, animation.AttackerUid);
            var hit = Assert.Single(animation.Targets);
            Assert.Equal(PlayerUid, hit.Uid);
            Assert.True(hit.Damage > 0); // Not a damage-arithmetic oracle; real accepted target/receive proof.
            Assert.True(Slot(PlayerUid).Character.MinHitPoints > 0);
            var vectors = frames.Select((frame, index) => (frame, index)).Where(item =>
                LegacyMsgUpdatePacket.TryParse(ReadFrame(item.frame.Bytes), out var update) &&
                update.Uid == TargetPlayerUid && update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag && entry.Values is { } flags &&
                    IsLegacyStatusFlagSet(flags, 313))).ToArray();
            Assert.Equal(2, vectors.Length);
            Assert.Equal(new[] { PlayerUid, TargetPlayerUid }.Order(), vectors.Select(item => item.frame.Recipient).Order());
            Assert.Equal(vectors[0].frame.Bytes, vectors[1].frame.Bytes);
            var animationIndex = Array.FindIndex(frames, frame => frame.Recipient == TargetPlayerUid &&
                LegacySpellAnimationPacket.TryParse(ReadFrame(frame.Bytes), out var spell) && spell.SpellId == 17_490);
            Assert.All(vectors, item => Assert.True(item.index < animationIndex));
            Assert.True(HasLegacyStatusFlag(Owner.Character.Attributes, 313));
            Assert.Equal(Origin.AddSeconds(10), LegacyPhysicalStatusOverlay.ForOwner(Owner).FrontBreakExpiresAtUtc);
            Assert.Equal(66U, (await StatusAsync()).Breakthrough); // Real source60+floor(60*10/100).
            Assert.Equal(501, Owner.Character.MinHitPoints);
            Assert.DoesNotContain(ownerFrames, bytes => MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) &&
                attack.Query.AttackType == MsgAttackType.Physical); // Real CombatGear replacement consumed it.
        }
        public async Task EarnCounteractionAsync()
        {
            var start = Order.Count;
            await PacketHandlers.HandleAsync(Context(PlayerUid), ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid, OpponentUid: TargetPlayerUid, X: 830, Y: 584,
                AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0, ResponseDamage: 0,
                Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes()));
            var animation = Assert.Single(ParseSpellAnimations(Order.ToArray().Skip(start)
                .Where(frame => frame.Recipient == PlayerUid).Select(frame => frame.Bytes)), packet => packet.SpellId == 17_490);
            var hit = Assert.Single(animation.Targets);
            Assert.Equal(TargetPlayerUid, hit.Uid);
            Assert.Equal(291U, hit.Damage);
            Assert.Equal(210, Owner.Character.MinHitPoints);
            Assert.Equal(75U, (await StatusAsync()).Counteraction);
        }
        public async Task HitAsync() => await WorldPacketHandlers.TrySendLegacyMonsterRetaliationAsync(
            Context(TargetPlayerUid), Owner, monster, family, CancellationToken.None);
        public void Dispose()
        {
            Assert.Equal(0, ScheduledTimers); // Direct public runtime entry; no background loop was started.
            GC.KeepAlive(connections);
            log.Dispose();
        }
    }
}
