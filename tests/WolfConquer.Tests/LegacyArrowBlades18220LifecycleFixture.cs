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
    private const int ArrowLifeFire = 332;
    private const int ArrowLifeWater = 333;
    private const int ArrowLifeSob = 65_182;

    private sealed partial class ArrowLifecycleFixture : IAsyncDisposable
    {
        private readonly ManualRuntimeTimeProvider clock = new(ArrowLifeOrigin);
        private readonly CancellationTokenSource cancel = new();
        private readonly FineRainExpiryLog log = new();
        private readonly WorldServerFrameHandler handler;
        private readonly WorldSessionStore sessions;
        private readonly Dictionary<uint, ConquerTcpConnection> connections = [];
        private readonly WorldPacketHandlerContext context;
        private readonly string route;
        private readonly LegacyMonsterInstance monster;
        private readonly SobNpc sob;
        private readonly MonsterRuntimeStateStore monsterStates = new();
        private readonly SobNpcRuntimeStateStore sobStates = new();
        private bool started;
        private readonly bool nativeDeathCasterQueryControls;
        private readonly bool nativeStarFlowCallerQueryControls;
        private bool nativeStarFlowCallerQueryPrepared;
        private DateTimeOffset? lastInput;
        private LegacyClientCompatibilityOptions? arrowAttackCompatibility;

        public ArrowLifecycleFixture(string route = "physical-player", bool learnedArrow = true,
            Func<ArrowLifecycleFixture, uint, byte[], ValueTask>? onSend = null, bool sourcePassiveArrow = false,
            bool learnedStarFlow = false, MagicType? starFlowRow = null, bool catalogStarFlow = false,
            ushort mapId = 1_000, bool mpMasterCatalog = false, bool starFlowTargetEffects = false,
            bool nativeDeathCasterQueryControls = false, bool nativeStarFlowCallerQueryControls = false)
        {
            this.route = route;
            this.nativeDeathCasterQueryControls = nativeDeathCasterQueryControls;
            this.nativeStarFlowCallerQueryControls = nativeStarFlowCallerQueryControls;
            Assert.Contains(route, new[] { "physical-player", "archer-player", "monster", "archer-sob" });
            var monsterFamily = CreateMonsterFamily() with
            {
                Attributes = new Dictionary<string, string>(CreateMonsterFamily().Attributes)
                {
                    ["life"] = "100000", ["level"] = "121", ["defense"] = "0", ["defense2"] = "10000",
                    ["Boss"] = "1", ["attack_range"] = "0",
                },
            };
            var spawn = CreateSingleInstanceMonsterSpawn(generatorId: 31_823, boundX: 832, boundY: 584)
                with { MapId = mapId };
            sob = CreateMoveLineSobNpc(ArrowLifeSob, 832, 584) with
                { MapId = mapId, Life = 100_000, MaxLife = 100_000 };
            var warm = CreateMagicRow(17_400, sort: 87, useMana: 0, damage: 30_400, range: 8,
                maxTargets: 1, damageOnMonster: 30_100, gDamage: 100, coolDown: 0, coldTime: 0);
            var arrow = CreateMagicRow(18_220, sort: 103, useMana: 0, damage: 30_300, range: 20,
                maxTargets: 4, needLevel: 40, duration: 10, damageOnHuman: 30_080, damage2: 5,
                damage3: 8_000, damageOnMonster: 100, gDamage: 100, coolDown: 0, coldTime: 0);
            Assert.Equal(new[] { "30300", "100", "10", "4", "20", "30080", "5", "8000", "100" },
                new[] { 10, 12, 13, 14, 15, 35, 36, 37, 44 }.Select(index => arrow.Fields[index]));
            ItemType HpItem(uint id, string hp)
            {
                var item = DirectOverlayItem(id, "ArrowLifecycle" + id);
                var fields = item.Fields.ToArray();
                fields[18] = hp; // Actual ushort-safe item HP; no stored MaxHP alias.
                if (nativeStarFlowCallerQueryControls && id != 131_094)
                {
                    // Legal UInt16 item inputs. Query earns ownerMA9002 / peerMA1000;
                    // raw spell9000 then native /2 retains old9001 and lethal5000 setups.
                    fields[19] = "1000";
                    fields[29] = id == 111_094 ? "9001" : "999";
                }
                return item with { Fields = fields };
            }
            var magicRows = new List<MagicType>
            {
                warm, arrow, CreateMagicRow(1_000, sort: 1, useMana: 0,
                    damage: nativeDeathCasterQueryControls ? 9_999U : 9_000U,
                    range: 18, coolDown: 0, coldTime: 0),
            };
            IReadOnlyList<ushort> ownerSpells = learnedArrow ? [17_400, 18_220, 1_000] : [17_400, 1_000];
            if (learnedStarFlow || catalogStarFlow)
            {
                magicRows.Add(starFlowRow ?? CreateMagicRow(10_950, sort: 5, useMana: 0, damage: 100,
                    range: 3, maxTargets: 1, coolDown: 0, coldTime: 0));
            }
            if (learnedStarFlow) ownerSpells = [.. ownerSpells, 10_950];
            if (starFlowTargetEffects)
            {
                magicRows.Add(CreateMagicRow(14_710, sort: 6, useMana: 0, damage: 100,
                    duration: 60, range: 18, coolDown: 0, coldTime: 0));
                magicRows.Add(CreateMagicRow(16_370, sort: 98, useMana: 0, damage: 30_100,
                    range: 18, maxTargets: 1, coolDown: 0, coldTime: 0));
            }
            var world = CreateMagicCatalogCore(monsterFamily, route == "monster" ? [spawn] : [],
                route == "archer-sob" ? [sob] : [], magicRows,
                items: mpMasterCatalog
                    ? [HpItem(111_094, "4100"), HpItem(111_095, "50000"), HpItem(131_094, "50000"),
                        new ItemType(4_030_803, "MPMaster(+3)", ["4030803", "MPMaster(+3)", "0"])]
                    : [HpItem(111_094, "4100"), HpItem(111_095, "50000"), HpItem(131_094, "50000")],
                magicEffects: sourcePassiveArrow ? MagicEffectParser.Parse(
                    "[1822000]\nName=ArrowBlades\nDescEx=Active:~Consumes~3~throwing~knives,~dealing~300%~damage~to~enemies~in~a~rectangular~area~ahead~(100%~damage~to~human~targets).~Passive:~After~two~normal~attacks,~the~3rd~attack~will~trigger~Arrow~Blades.\n",
                    "fixture://source-MagicEffect-1822000.ini") : null,
                combatGear: nativeStarFlowCallerQueryControls
                    ? [CombatGearParser.ParseRow("fixture://starflow-native-loaded-neutral-401", 1,
                        "151@@401@@1@@100@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@")]
                    : null);
            SourceArrowEffect = sourcePassiveArrow ? Assert.Single(world.MagicEffectsById[1_822_000]) : null;
            monster = CreateRuntimeMonsterInstance(world, spawn, monsterFamily);
            var owner = CreateSession(mapId: mapId, classCode: 4_005, strength: 300, spirit: 0,
                reborn: mpMasterCatalog ? 2 : 0,
                minHitPoints: 5_000, stamina: 100, minMana: 1_000,
                spellIds: ownerSpells,
                playerItems: PlayerItemSnapshot.Empty with
                {
                    EquipmentItems = [new PlayerItemSnapshotItem { Uid = 98_401, ItemId = 111_094,
                        Position = 1, Durability = 100, MaximumDurability = 100 }],
                },
                attributes: new Dictionary<string, string>
                {
                    ["PkMode"] = "0", ["BattlePower"] = "120",
                    ["LegacyCombatGearAttackSpellIndex"] = "0", ["LegacyCombatGearAttackRateRoll"] = "99",
                    ["LegacyPhysicalPlayerLuckyBranchRoll"] = "99", ["LegacyPhysicalPlayerCriticalRoll"] = "99",
                    ["LegacyPhysicalDashRoll"] = "99", ["DashRate"] = "0",
                }) with
            {
                CombatGear = CombatGearSnapshot.Empty with
                {
                    Count = 1, Items = [new CombatGearSnapshotItem(401, Level: 1, Exp: 0, Equip: 1,
                        Transform: 0, MasteryScore: 0, Animas: [], BlackJades: [])],
                },
            };
            var peer = CreateSession(uid: TargetPlayerUid, name: "ArrowLifecyclePeer", mapId: mapId,
                x: route.EndsWith("player", StringComparison.Ordinal) ? 832 : 830,
                y: route.EndsWith("player", StringComparison.Ordinal) ? 584 : 593,
                classCode: 2_005, strength: 0, spirit: 0, minHitPoints: 100_000,
                minMana: 1_000, stamina: 100, spellIds: starFlowTargetEffects ? [1_000, 14_710, 16_370] : [1_000],
                attributes: new Dictionary<string, string> { ["PkMode"] = "0", ["BattlePower"] = "120" },
                playerItems: PlayerItemSnapshot.Empty with
                {
                    EquipmentItems =
                    [
                        new PlayerItemSnapshotItem { Uid = 98_402, ItemId = 111_095, Position = 1,
                            Durability = 100, MaximumDurability = 100 },
                        new PlayerItemSnapshotItem { Uid = 98_403, ItemId = 131_094, Position = 3,
                            Durability = 100, MaximumDurability = 100 },
                    ],
                });
            if (mapId == 1_039)
            {
                owner = owner with { Character = owner.Character with { Attributes =
                    new Dictionary<string, string>(owner.Character.Attributes) { ["DynamicID"] = "1" } } };
                peer = peer with { Character = peer.Character with { Attributes =
                    new Dictionary<string, string>(peer.Character.Attributes) { ["DynamicID"] = "1" } } };
            }
            if (starFlowTargetEffects)
            {
                // Declared view input: warm17400 must not pre-acquire untimed historical FlashingName.
                owner = owner with { Character = owner.Character with { Attributes =
                    new Dictionary<string, string>(owner.Character.Attributes) { ["DynamicID"] = "1" } } };
                peer = peer with { Character = peer.Character with { Attributes =
                    new Dictionary<string, string>(peer.Character.Attributes) { ["DynamicID"] = "1" } } };
            }
            Assert.Equal(5_000U, LegacyMaxHitpointsCalculator.Calculate(owner.Character,
                owner.PlayerItems, owner.CombatGear!, world).Total);
            Assert.Equal(100_000U, LegacyMaxHitpointsCalculator.Calculate(peer.Character,
                peer.PlayerItems, CombatGearSnapshot.Empty, world).Total);
            var statusFields = LegacyPlayerStatusCalculator.CalculateFields(owner.Character, owner.PlayerItems,
                world, 5_000, prestigeLevel: 0);
            Assert.Equal(300U, statusFields[LegacyPlayerStatusPacket.MinAttackIndex]);
            Assert.Equal(301U, statusFields[LegacyPlayerStatusPacket.MaxAttackIndex]);
            Assert.Equal(0U, statusFields[LegacyPlayerStatusPacket.CriticalStrikeIndex]);
            Assert.Equal(0U, statusFields[LegacyPlayerStatusPacket.BreakthroughIndex]);
            Assert.Equal(0U, ArrowLifeCounter(owner, "ArrowBlades"));
            Assert.Equal(0U, ArrowLifeCounter(owner, "StarFlow"));
            Assert.Null(owner.EquipmentHitpointsStatus);
            Assert.False(owner.Character.Attributes.ContainsKey("MaxHitpoints"));
            Assert.False(HasLegacyStatusFlag(owner.Character.Attributes, ArrowLifeFire));
            Assert.False(HasLegacyStatusFlag(owner.Character.Attributes, ArrowLifeWater));
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
                connections[uid] = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                    sendAsync: async (memory, _) =>
                    {
                        var bytes = memory.ToArray();
                        Order.Enqueue((uid, bytes));
                        if (onSend is not null) await onSend(this, uid, bytes);
                    });
            handler = new WorldServerFrameHandler(snapshotLoader: null, handoffService: null, worldData: world,
                runtimeUpdateLoop: new LegacyRuntimeUpdateLoopOptions(true, TimeSpan.FromMilliseconds(20), clock),
                log: log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            sessions = GetHandlerSessions(handler);
            sessions.Set(connections[PlayerUid], owner);
            sessions.Set(connections[TargetPlayerUid], peer);
            context = new WorldPacketHandlerContext(connections[PlayerUid], log, owner, WorldData: world,
                Sessions: sessions, MonsterStates: monsterStates, SobNpcStates: sobStates, TimeProvider: clock,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        }

        public DateTimeOffset Now => clock.GetUtcNow();
        public MagicEffect? SourceArrowEffect { get; }
        public ConcurrentQueue<(uint Recipient, byte[] Bytes)> Order { get; } = new();
        public WorldSession Owner => AssertHandlerSession(handler, connections[PlayerUid]);
        public WorldSession Peer => AssertHandlerSession(handler, connections[TargetPlayerUid]);
        public WorldSession? OwnerSlot => sessions.TryGet(connections[PlayerUid], out var current) ? current : null;
        public WorldSession? PeerSlot => sessions.TryGet(connections[TargetPlayerUid], out var current) ? current : null;
        public uint PrimaryUid => route == "monster" ? monster.Uid :
            route == "archer-sob" ? checked((uint)ArrowLifeSob) : TargetPlayerUid;
        public long PrimaryHitpoints => route == "monster" ? MonsterHitpoints() :
            route == "archer-sob" ? sobStates.GetVisible(sob).Life : Peer.Character.MinHitPoints;
        public byte[][] FramesSince(uint recipient, int start) => Order.ToArray().Skip(start)
            .Where(entry => entry.Recipient == recipient).Select(entry => entry.Bytes).ToArray();

        private uint MonsterHitpoints()
        {
            Assert.True(monsterStates.TryGetVisibleInstance(monster, Now, out var current));
            return current.Hitpoints;
        }

        public async Task AttackAsync()
        {
            await PrepareStarFlowNativeCallerQueriesAsync();
            await SpaceInputAsync();
            await SendAsync(PlayerUid, PrimaryUid,
                route == "archer-player" || route == "archer-sob" ? MsgAttackType.Archer : MsgAttackType.Physical);
        }

        private async Task SpaceInputAsync()
        {
            // Source ordinary AttackSpeed(800) is700ms for this neutral fixture.
            // Use declared1s spacing, not three fictional same-tick ordinary inputs.
            if (lastInput is { } previous && Now < previous.AddSeconds(1))
            {
                var remaining = previous.AddSeconds(1) - Now;
                if (started) await AdvanceAsync(remaining);
                else clock.Advance(remaining); // No runtime has been started yet.
            }
            lastInput = Now;
        }

        public async Task EarnWaterAsync()
        {
            for (uint hit = 1; hit <= 3; hit++)
            {
                var start = Order.Count;
                var previous = PrimaryHitpoints;
                await AttackAsync(); // Real ordinary attack -> selected17400 -> actual receive counter.
                Assert.Single(ParseSpellAnimations(FramesSince(PlayerUid, start)), packet => packet.SpellId == 17_400);
                Assert.True(PrimaryHitpoints < previous);
                Assert.Equal(hit, ArrowLifeCounter(Owner, "ArrowBlades"));
                Assert.Equal(hit == 3, HasLegacyStatusFlag(Owner.Character.Attributes, ArrowLifeWater));
                if (hit < 3)
                    Assert.False(LegacyArrowBladesFlagRegistration.TryRead(Owner.Character, ArrowLifeWater, out _));
            }
            Assert.Equal(0U, ArrowLifeCounter(Owner, "StarFlow")); // Source third attempt consumed;10950 book absent.
            Assert.DoesNotContain(Owner.PlayerSpells.Spells, spell => spell.Id == 10_950);
            Assert.Equal("true", Owner.Character.Attributes[LegacyStatusFlagDeathRegistration.Key(ArrowLifeWater)]);
            AssertRegistration(ArrowLifeWater, Now.DateTime, 2_592_000);
        }

        public LegacyArrowBladesFlagRegistration AssertRegistration(int flag, DateTime timer, int seconds)
        {
            Assert.True(LegacyArrowBladesFlagRegistration.TryRead(Owner.Character, flag, out var value));
            Assert.Equal((long)PlayerUid, value.OwnerUid);
            Assert.Equal(flag, value.Flag);
            Assert.NotEqual(Guid.Empty, value.Identity);
            Assert.Equal(DateTimeKind.Unspecified, value.Timer.Kind);
            Assert.Equal(timer, value.Timer);
            Assert.Equal(seconds, value.Seconds);
            Assert.Equal(timer.AddSeconds(seconds), value.ExpiresAtLocal);
            return value;
        }

        public async Task Cast18220MagicAsync()
        {
            await SpaceInputAsync();
            await SendAsync(PlayerUid, PrimaryUid, MsgAttackType.Magic, 18_220);
        }
        public async Task OrdinaryMagicAsync()
        {
            await SpaceInputAsync();
            await SendAsync(PlayerUid, TargetPlayerUid, MsgAttackType.Magic, 1_000);
        }
        public Task KillOwnerAsync() => SendAsync(TargetPlayerUid, PlayerUid, MsgAttackType.Magic, 1_000);
        public Task CastPeerImmortalForceAsync() => SendAsync(
            TargetPlayerUid, TargetPlayerUid, MsgAttackType.Magic, 14_710);
        public void EnterStarFlowPlayerGateView()
        {
            Assert.Equal((long)PlayerUid, Owner.Character.Uid);
            Assert.Equal((long)TargetPlayerUid, Peer.Character.Uid);
            ChangeOwnerDuringArrowSend(value => value with { Character = value.Character with { Attributes =
                new Dictionary<string, string>(value.Character.Attributes) { ["DynamicID"] = "0" } } });
            ChangePeerDuringArrowSend(value => value with { Character = value.Character with { Attributes =
                new Dictionary<string, string>(value.Character.Attributes) { ["DynamicID"] = "0" } } });
        }
        public void EquipPeerHellVortexForGate()
        {
            // Declared current Ninja/rate/Strength inputs, NOT an earned-Ninja acquisition claim.
            ChangePeerDuringArrowSend(value => value with { Character = value.Character with
            {
                Strength = 300,
                Attributes = new Dictionary<string, string>(value.Character.Attributes)
                { ["LegacyNinjaHellVortexEquipped"] = "true", ["LegacyHellVortexRoll"] = "0" },
            } });
        }
        public Task Cast18220UnspacedAsync() => SendAsync(PlayerUid, PrimaryUid, MsgAttackType.Magic, 18_220);
        public void AdvanceUnstartedArrowInputClock(TimeSpan amount)
        {
            Assert.False(started); // This opt-in must never bypass a running global-owner rearm barrier.
            clock.Advance(amount);
        }
        public void AdvanceArrowClockDuringEnteredRuntimeSend(TimeSpan amount)
        {
            Assert.True(started); // Test-only clock input inside an ACTUAL awaited runtime send callback.
            clock.Advance(amount); // Do not await a rearm from within its still-entered owner.
        }

        public void UseArrowAttackCompatibility(LegacyClientCompatibilityOptions policy) =>
            arrowAttackCompatibility = policy;

        private async Task SendAsync(uint caster, uint target, MsgAttackType type, ushort spell = 0)
        {
            var current = caster == PlayerUid ? Owner : Peer;
            await PacketHandlers.HandleAsync(context with
                { Connection = connections[caster], Session = current, ClientCompatibility = arrowAttackCompatibility },
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: caster, OpponentUid: target, X: 832, Y: 584,
                    AttackType: type, Damage: 0, KilledMonster: 0, ResponseDamage: 0, Effect: MsgAttackEffect.None,
                    Unknown: 0, SpellId: spell)).ToFrameBytes()));
        }

        public void RemoveOwner() => Assert.True(sessions.TryRemove(connections[PlayerUid], out _));
        public void ChangeOwnerDuringArrowSend(Func<WorldSession, WorldSession> change)
        {
            var current = Owner;
            var changed = change(current);
            Assert.Equal(current.Character.Uid, changed.Character.Uid);
            sessions.Set(connections[PlayerUid], changed);
        }
        public void ChangePeerDuringArrowSend(Func<WorldSession, WorldSession> change)
        {
            var current = Peer;
            Assert.Equal((long)TargetPlayerUid, current.Character.Uid);
            var changed = change(current);
            Assert.Equal(current.Character.Uid, changed.Character.Uid);
            sessions.Set(connections[TargetPlayerUid], changed);
        }
        public WorldSession RebindOwner()
        {
            var replacement = CreateSession(uid: PlayerUid + 9_000, name: "ArrowLifecycleReplacement",
                mapId: 1_000, x: 830, y: 584, strength: 0, spirit: 0, minHitPoints: 77_777,
                minMana: 77, spellIds: [], attributes: new Dictionary<string, string> { ["ArrowIdentity"] = "replacement" });
            sessions.Set(connections[PlayerUid], replacement);
            return replacement;
        }

        public void RemovePeer() => Assert.True(sessions.TryRemove(connections[TargetPlayerUid], out _));
        public WorldSession RebindPeer()
        {
            var replacement = CreateSession(uid: TargetPlayerUid + 9_000, name: "ArrowLifecycleTargetReplacement",
                mapId: 1_000, x: 832, y: 584, strength: 0, spirit: 0, minHitPoints: 88_888,
                minMana: 88, spellIds: [], attributes: new Dictionary<string, string> { ["ArrowIdentity"] = "target-replacement" });
            sessions.Set(connections[TargetPlayerUid], replacement);
            return replacement;
        }

        public async Task StartAsync()
        {
            Assert.False(started);
            FineRainExpiryStartGlobal(handler, cancel.Token); // ACTUAL global owner only; no player ticker/login claim.
            started = true;
            await WaitUntilAsync(() => clock.ScheduledTimerCount >= 1, TimeSpan.FromSeconds(2));
        }
        public async Task AdvanceAsync(TimeSpan amount)
        {
            await WaitUntilAsync(() => clock.ScheduledTimerCount >= 1, TimeSpan.FromSeconds(2));
            clock.Advance(amount);
            await WaitUntilAsync(() => clock.ScheduledTimerCount >= 1, TimeSpan.FromSeconds(2));
        }
        public async ValueTask DisposeAsync()
        {
            await cancel.CancelAsync();
            if (started)
                await WaitUntilAsync(() => clock.ScheduledTimerCount == 0 && FineRainExpiryGlobalStopped(handler),
                    TimeSpan.FromSeconds(3));
            GC.KeepAlive(connections);
            cancel.Dispose();
            log.Dispose();
        }
    }

    private static readonly DateTimeOffset ArrowLifeOrigin = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
    private static uint ArrowLifeCounter(WorldSession session, string key) => session.Character.Attributes.TryGetValue(key, out var text)
        ? uint.Parse(text, CultureInfo.InvariantCulture) : 0;
}
