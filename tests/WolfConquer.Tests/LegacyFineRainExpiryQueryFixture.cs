using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private sealed class FineRainExpiryFixture : IAsyncDisposable
    {
        private readonly ManualRuntimeTimeProvider clock;
        private readonly CancellationTokenSource cancel = new();
        private readonly FineRainExpiryLog log = new();
        private readonly WorldServerFrameHandler handler;
        private readonly WorldSessionStore sessions;
        private readonly Dictionary<uint, ConquerTcpConnection> connections = [];
        private readonly WorldPacketHandlerContext context;
        private bool started;
        private readonly TeamSessionStateStore? initialAcquisitionTeams;

        public FineRainExpiryFixture(Func<FineRainExpiryFixture, uint, byte[], ValueTask>? onSend = null, uint duration = 8,
            bool mutableCounteraction = false, uint power = 1, uint? recastPower = null,
            int nativeEpochMilliseconds = 10_000, int environmentEpochMilliseconds = 10_000,
            ILegacySignedMillisecondClock? nativeClock = null, ILegacySignedMillisecondClock? environmentClock = null,
            bool peerFineRainOwner = false, uint? recastDuration = null, bool initialAcquisitionTeam = false)
        {
            Duration = duration;
            Power = power;
            initialAcquisitionTeams = initialAcquisitionTeam ? new TeamSessionStateStore() : null;
            clock = new ManualRuntimeTimeProvider(Origin);
            ItemType Helmet(uint id, string hp, uint breakthrough, uint counteraction, uint critical = 0)
            {
                var row = DirectOverlayItem(id, "FineRainExpiry" + id, critical: critical, breakthrough: breakthrough, counteraction: counteraction);
                var fields = row.Fields.ToArray();
                fields[18] = hp; // Both500/700 are source ushort-safe item HP, not a MaxHP alias.
                return row with { Fields = fields };
            }
            var spells = new List<MagicType>
            {
                CreateMagicRow(14_500, sort: 6, useMana: 0, useStamina: 0, damage: power,
                    duration: duration, coolDown: 0, coldTime: 0),
            };
            if (recastPower is { } higher)
                spells.Add(CreateMagicRow(14_500, sort: 6, level: 1, useMana: 0, useStamina: 0, damage: higher,
                    duration: recastDuration ?? duration, coolDown: 0, coldTime: 0));
            var items = new List<ItemType> { Helmet(111_094, "500", 60, 50), Helmet(111_095, "700", 80, 70) };
            if (mutableCounteraction)
            {
                spells.Add(CreateMagicRow(17_490, sort: 99, useMana: 0, damage: 30_250, range: 4,
                    damageOnHuman: 0, damage2: 33, damage3: 2, damageOnMonster: 30_085, gDamage: 0,
                    coolDown: 0, coldTime: 0));
                items.Add(Helmet(111_096, "5000", 100, 0, critical: 1_000));
                // Existing fixture-local eligible yellow rune: source names Impregnability,
                // but this is NOT claimed to be a supplied itemtype row.
                items.Add(DirectOverlayItem(DirectOverlaySyntheticImpregnabilityRuneId, "Impregnability(+9)"));
            }
            var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [], spells, items: items);
            var owner = CreateSession(mapId: 1_000, classCode: 2_005, reborn: 2, strength: 0, spirit: 0,
                minHitPoints: 300, minMana: 1_000, stamina: 100, spellIds: [14_500],
                attributes: new Dictionary<string, string>
                { ["PkMode"] = "0", ["BattlePower"] = "200", ["LegacyPhysicalImpregnabilityRoll"] = "0" },
                playerItems: Items(111_094) with
                {
                    RuneItems = mutableCounteraction ? [new PlayerItemSnapshotItem
                    {
                        Uid = 98_302, ItemId = DirectOverlaySyntheticImpregnabilityRuneId, Position = 104,
                        Durability = 100, MaximumDurability = 100,
                    }] : [],
                });
            var peer = CreateSession(uid: TargetPlayerUid, name: "FineRainExpiryPeer", mapId: 1_000,
                x: 832, y: 584, classCode: 2_005, strength: mutableCounteraction ? 300 : 0, spirit: 0,
                minHitPoints: mutableCounteraction ? 5_000 : 500, stamina: 100,
                spellIds: mutableCounteraction ? [17_490] : peerFineRainOwner ? [14_500] : [],
                playerItems: mutableCounteraction ? Items(111_096) : peerFineRainOwner ? Items(111_094) : PlayerItemSnapshot.Empty,
                attributes: new Dictionary<string, string>
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
            Assert.Equal(500U, LegacyMaxHitpointsCalculator.Calculate(owner.Character, owner.PlayerItems,
                CombatGearSnapshot.Empty, world).Total);
            Assert.Null(owner.EquipmentHitpointsStatus);
            Assert.Null(owner.PhysicalStatusOverlay);
            Assert.Null(owner.FineRainEquipmentAdmission);
            Assert.False(owner.Character.Attributes.ContainsKey("MaxHitpoints"));
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
                connections[uid] = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                    sendAsync: async (memory, _) =>
                    {
                        var bytes = memory.ToArray();
                        Order.Enqueue((uid, bytes));
                        if (onSend is not null) await onSend(this, uid, bytes);
                    });
            var signedClock = nativeClock ?? LegacySignedMillisecondClock.FromTimeProvider(clock, nativeEpochMilliseconds);
            var environment = environmentClock ??
                LegacyEnvironmentMillisecondClock.FromTimeProvider(clock, environmentEpochMilliseconds);
            handler = new WorldServerFrameHandler(snapshotLoader: null, handoffService: null, worldData: world,
                runtimeUpdateLoop: new LegacyRuntimeUpdateLoopOptions(true, TimeSpan.FromSeconds(1), clock, signedClock,
                    LegacyEnvironmentClock: environment),
                log: log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            sessions = GetHandlerSessions(handler);
            sessions.Set(connections[PlayerUid], owner);
            sessions.Set(connections[TargetPlayerUid], peer);
            context = new WorldPacketHandlerContext(connections[PlayerUid], log, owner,
                WorldData: world, Sessions: sessions, TeamStates: initialAcquisitionTeams, TimeProvider: clock, LegacyClock: signedClock,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        }

        public DateTimeOffset Origin { get; } = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
        public uint Duration { get; }
        public uint Power { get; }
        public DateTimeOffset WallNow => clock.GetUtcNow();
        public void AdvanceWall(TimeSpan amount) => clock.Advance(amount);
        public ConcurrentQueue<(uint Recipient, byte[] Bytes)> Order { get; } = new();
        public WorldSession Owner => AssertHandlerSession(handler, connections[PlayerUid]);
        public bool TryOwner(out WorldSession? session) => sessions.TryGet(connections[PlayerUid], out session);
        public int WaitCount => OwnerFramesSince(0).Count(FineRainExpiryIsWait);
        public uint[] SubjectOrder => sessions.Snapshot().Select(active => checked((uint)active.Session.Character.Uid)).ToArray();
        public WorldSession Subject(uint uid) => AssertHandlerSession(handler, connections[uid]);
        public byte[][] SubjectFramesSince(uint uid, int start) => Order.ToArray().Skip(start)
            .Where(frame => frame.Recipient == uid).Select(frame => frame.Bytes).ToArray();
        public byte[][] OwnerFramesSince(int start) => Order.ToArray().Skip(start)
            .Where(entry => entry.Recipient == PlayerUid).Select(entry => entry.Bytes).ToArray();
        public byte[][] PeerFramesSince(int start) => Order.ToArray().Skip(start)
            .Where(entry => entry.Recipient == TargetPlayerUid).Select(entry => entry.Bytes).ToArray();

        public async Task AcquireAsync()
        {
            var raw = await QueryAsync();
            Assert.Equal(500U, raw.MaxHP);
            Assert.Equal(60U, raw.Breakthrough);
            Assert.Equal(50U, raw.Counteraction);
            await CastAsync(); // REAL D13 acquisition; no seeded flag/power/deadline/cache/stamp.
            Assert.True(HasLegacyStatusFlag(Owner.Character.Attributes, 212));
            Assert.True(HasLegacyStatusFlag(Owner.Character.Attributes, 213));
            if (Power == 1) FineRainExpiryAssertRetainedPowers(Owner); // Keep the original nine-row default assertion.
            else
            {
                Assert.Equal(Power.ToString(CultureInfo.InvariantCulture), Owner.Character.Attributes["FineRainPower"]);
                Assert.Equal(Power.ToString(CultureInfo.InvariantCulture), Owner.Character.Attributes["defFineRainPower"]);
            }
            foreach (var flag in new[] { 212, 213 })
                Assert.Equal(Origin.AddSeconds(Duration).ToString("O"), Owner.Character.Attributes[DeadlineKey(flag)]);
            Assert.Equal(10_000, LegacyFineRainEquipmentAdmission.ForOwner(Owner).StampMilliseconds);
            if (Power == 1)
            {
                Assert.Equal(501U, (await QueryAsync()).MaxHP);
                Assert.Equal(501, Owner.Character.MinHitPoints);
                DirectFineRainHpAssertOwnedMaximum(Owner, 501);
            }
            else
            {
                var maximum = unchecked(500U + Power); // Literal500 item base plus real cast input, not a calculator oracle.
                Assert.Equal(maximum, (await QueryAsync()).MaxHP);
                Assert.Equal((long)maximum, Owner.Character.MinHitPoints);
                DirectFineRainHpAssertOwnedMaximum(Owner, maximum);
            }
        }

        public async Task CastAsync() =>
            await PacketHandlers.HandleAsync(context with { Session = Owner },
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: PlayerUid, OpponentUid: PlayerUid,
                    X: 830, Y: 584, AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
                    ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0, SpellId: 14_500)).ToFrameBytes()));

        public async Task<LegacyPlayerStatusPacket> QueryAsync()
        {
            var start = Order.Count;
            await PacketHandlers.HandleAsync(context with { Session = Owner },
                ReadFrame(new LegacyPlayerStatusQueryPacket(PlayerUid).ToFrameBytes()));
            var bytes = Assert.Single(OwnerFramesSince(start),
                frame => LegacyPlayerStatusPacket.TryParse(ReadFrame(frame), out _));
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out var status));
            return status;
        }

        public async Task CastSubjectFineRainAsync(uint uid)
        {
            var subject = Subject(uid);
            await PacketHandlers.HandleAsync(context with { Connection = connections[uid], Session = subject },
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: uid, OpponentUid: uid,
                    X: checked((ushort)subject.Character.X), Y: checked((ushort)subject.Character.Y),
                    AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0, ResponseDamage: 0,
                    Effect: MsgAttackEffect.None, Unknown: 0, SpellId: 14_500)).ToFrameBytes()));
        }

        public async Task<LegacyPlayerStatusPacket> QuerySubjectAsync(uint uid)
        {
            var start = Order.Count;
            await PacketHandlers.HandleAsync(context with { Connection = connections[uid], Session = Subject(uid) },
                ReadFrame(new LegacyPlayerStatusQueryPacket(uid).ToFrameBytes()));
            var bytes = Assert.Single(SubjectFramesSince(uid, start),
                frame => LegacyPlayerStatusPacket.TryParse(ReadFrame(frame), out _));
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out var status));
            Assert.Equal(uid, status.Uid);
            return status;
        }

        public async Task EarnCounteractionAsync()
        {
            var start = Order.Count;
            var peer = AssertHandlerSession(handler, connections[TargetPlayerUid]);
            await PacketHandlers.HandleAsync(context with { Connection = connections[TargetPlayerUid], Session = peer },
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: TargetPlayerUid, OpponentUid: PlayerUid,
                    X: 830, Y: 584, AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0,
                    ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes()));
            var animation = Assert.Single(ParseSpellAnimations(PeerFramesSince(start)), packet => packet.SpellId == 17_490);
            var hit = Assert.Single(animation.Targets);
            Assert.Equal(PlayerUid, hit.Uid);
            Assert.Equal(291U, hit.Damage); // Source390->Defense2 half195->late penetration floor195/.67.
            Assert.Equal(75U, (await QueryAsync()).Counteraction); // Actual receive earned50+50%=75.
            Assert.Equal(210, Owner.Character.MinHitPoints);
        }

        // Default-off INITIAL14500 acquisition controls. Only callback inputs touch accepted current state.
        public void ChangeInitialAcquisitionPower(uint power) => ChangeAttributes(attributes =>
            attributes["FineRainPower"] = power.ToString(CultureInfo.InvariantCulture));
        public void ChangeInitialAcquisitionHitpoints(long hitpoints)
        {
            var current = Owner;
            sessions.Set(connections[PlayerUid], current with
                { Character = current.Character with { MinHitPoints = hitpoints } });
        }
        public WorldSession? InitialAcquisitionSlot(uint uid) =>
            sessions.TryGet(connections[uid], out var current) ? current : null;
        public void RemoveInitialAcquisitionSubject(uint uid) =>
            Assert.True(sessions.TryRemove(connections[uid], out _));
        public WorldSession RebindInitialAcquisitionSubject(uint uid)
        {
            var replacement = CreateSession(uid: uid + 9_000, name: "FineRainInitialReplacement", mapId: 1_000,
                x: uid == PlayerUid ? 830 : 832, y: 584, strength: 0, spirit: 0,
                minHitPoints: 77_777, minMana: 77, spellIds: [],
                attributes: new Dictionary<string, string> { ["FineRainExpiryIdentity"] = "initial-replacement" });
            sessions.Set(connections[uid], replacement);
            return replacement;
        }
        public async Task JoinInitialAcquisitionTeamAsync()
        {
            Assert.NotNull(initialAcquisitionTeams);
            await PacketHandlers.HandleAsync(context with { Session = Owner },
                ReadFrame(new MsgTeamPacket(MsgTeamType.Create, PlayerUid).ToFrameBytes()));
            await PacketHandlers.HandleAsync(context with
                { Connection = connections[TargetPlayerUid], Session = Subject(TargetPlayerUid) },
                ReadFrame(new MsgTeamPacket(MsgTeamType.AcceptInvitation, PlayerUid).ToFrameBytes()));
            Assert.Equal(2U, initialAcquisitionTeams.GetMemberCount(connections[PlayerUid]));
            Assert.True(initialAcquisitionTeams.IsTeamMember(connections[PlayerUid], TargetPlayerUid));
        }

        public void SelectLearnedFineRainLevel(ushort level)
        {
            var current = Owner;
            var spells = current.PlayerSpells.Spells.Select(spell => spell.Id == 14_500
                ? spell with { Level = level } : spell).ToArray();
            sessions.Set(connections[PlayerUid], current with { PlayerSpells = new PlayerSpellSnapshot(spells) });
        }

        public async Task QueryEquipmentAsync() =>
            await PacketHandlers.HandleAsync(context with { Session = Owner },
                ReadFrame(LegacyRuneStoragePacket.Startup().ToFrameBytes()));

        // Explicit timestamp-only branch bridge: public14500 sets equal deadlines,
        // so these controls stagger only its already-acquired registration timestamps.
        public void HoldSiblingDeadline(int flag) => ChangeAttributes(attributes =>
            attributes[DeadlineKey(flag)] = Origin.AddSeconds(Duration + 20).ToString("O"));
        public void Remove212ForMalformedMetadataControl()
        {
            ClearAcquiredFlagForBranchInput(212);
            ChangeAttributes(attributes =>
            {
                // Explicit malformed/compatibility inputs, NOT public canonical acquisition.
                attributes[LegacyStatusFlagDeathRegistration.Key(212)] = "true";
                attributes[LegacyStatusFlagDeathRegistration.Key(213)] = "false";
            });
        }
        public void ClearAcquiredFlagForBranchInput(int flag) => ChangeAttributes(attributes =>
        {
            var words = attributes["StatusFlags"].Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(value =>
                ulong.Parse(value, CultureInfo.InvariantCulture)).ToArray();
            words[flag / 32] &= ~(1UL << (flag % 32));
            attributes["StatusFlags"] = string.Join('/', words); // Clear ONE acquired bit, not query/cache/power state.
        });

        public void ChangeOwnerEquipmentAndVitals()
        {
            var current = Owner;
            var attributes = new Dictionary<string, string>(current.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            { ["FineRainExpiryIdentity"] = "callback" };
            sessions.Set(connections[PlayerUid], current with
            {
                Character = current.Character with { MinHitPoints = 400, MinMana = 77, Attributes = attributes },
                PlayerItems = Items(111_095), // Real700HP/BT80/CA70 item, no projected result seed.
            });
        }
        public void RemoveOwner() => Assert.True(sessions.TryRemove(connections[PlayerUid], out _));
        public WorldSession RebindOwner()
        {
            var replacement = CreateSession(uid: PlayerUid + 9_000, name: "FineRainExpiryReplacement", mapId: 1_000,
                x: 830, y: 584, strength: 0, spirit: 0, minHitPoints: 77_777, minMana: 77,
                spellIds: [], attributes: new Dictionary<string, string> { ["FineRainExpiryIdentity"] = "replacement" });
            sessions.Set(connections[PlayerUid], replacement);
            return replacement;
        }

        public async Task StartAsync()
        {
            Assert.False(started);
            // Test-only bridge into the ACTUAL global expiry owner. No player ticker
            // or claim that ordinary login/startup is proved by this focused fixture.
            FineRainExpiryStartGlobal(handler, cancel.Token);
            started = true;
            await WaitUntilAsync(() => clock.ScheduledTimerCount >= 1, TimeSpan.FromSeconds(2));
        }
        public async Task AdvanceAsync(TimeSpan amount)
        {
            await WaitUntilAsync(() => clock.ScheduledTimerCount >= 1, TimeSpan.FromSeconds(2));
            clock.Advance(amount);
            // A newly registered delay follows completion of the entire global pass.
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
        private void ChangeAttributes(Action<Dictionary<string, string>> change)
        {
            var current = Owner;
            var attributes = new Dictionary<string, string>(current.Character.Attributes, StringComparer.OrdinalIgnoreCase);
            change(attributes);
            sessions.Set(connections[PlayerUid], current with { Character = current.Character with { Attributes = attributes } });
        }
        private static string DeadlineKey(int flag) => "LegacyFineRain" + (flag - 211) + "ExpiresAtUtc";
        private static PlayerItemSnapshot Items(uint itemId) => PlayerItemSnapshot.Empty with
        {
            EquipmentItems = [new PlayerItemSnapshotItem { Uid = 98_301, ItemId = itemId, Position = 1,
                Durability = 100, MaximumDurability = 100 }],
        };
    }

    private static void FineRainExpiryStartGlobal(WorldServerFrameHandler handler, CancellationToken token)
    {
        var method = typeof(WorldServerFrameHandler).GetMethod("TryStartLegacyGlobalRuntimeUpdateLoop",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(handler, [token]);
    }
    private static bool FineRainExpiryGlobalStopped(WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField("_legacyRuntimeGlobalLoopStarted",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Equals(0, field.GetValue(handler));
    }

    // Both writes and snapshots are independently synchronized, unlike a raw
    // StringWriter.ToString() read beside a synchronized wrapper writer.
    private sealed class FineRainExpiryLog : TextWriter
    {
        private readonly ConcurrentQueue<string> lines = new();
        public override Encoding Encoding => Encoding.UTF8;
        public override Task WriteLineAsync(string? value)
        {
            lines.Enqueue(value ?? string.Empty);
            return Task.CompletedTask;
        }
        public override void WriteLine(string? value) => lines.Enqueue(value ?? string.Empty);
        public override string ToString() => string.Join(Environment.NewLine, lines.ToArray());
    }
}
