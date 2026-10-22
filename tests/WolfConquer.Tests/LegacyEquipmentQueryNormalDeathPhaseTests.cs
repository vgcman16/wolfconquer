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
    [Fact]
    public async Task DirectNormalDeathPublicThunderQueriesBeforeHpZeroSetterAndAddsDeadAfterTeamAndPair()
    {
        using var fixture = new DirectNormalDeathFixture();
        await fixture.PrepareNativeQueryInputsAsync();
        await fixture.JoinAsync();
        Assert.Equal(1U, (await fixture.StatusAsync(PlayerUid)).MagicAttack);
        var targetStatus = await fixture.StatusAsync(TargetPlayerUid);
        Assert.Equal(500U, targetStatus.MaxHP);
        Assert.Equal(0U, targetStatus.MagicDefence);
        Assert.Equal(0U, targetStatus.ItemBless);
        var start = fixture.Order.Count;
        await fixture.KillAsync();
        Assert.Equal(0, fixture.Target.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, 5));
        var frames = fixture.Order.ToArray().Skip(start).ToArray();
        var clear = Array.FindIndex(frames, frame => frame.Recipient == TargetPlayerUid &&
            DirectNormalDeathVector(frame.Bytes, dead: false));
        var query = Array.FindIndex(frames, frame => frame.Recipient == TargetPlayerUid &&
            LegacyShowEquipmentPacket.TryParse(ReadFrame(frame.Bytes), out _));
        var addedDead = Array.FindIndex(frames, frame => frame.Recipient == TargetPlayerUid &&
            DirectNormalDeathVector(frame.Bytes, dead: true));
        Assert.True(clear >= 0 && query > clear && addedDead > query);
        DirectNormalDeathAssertPairs(frames, [(500U, 300U), (500U, 300U), (500U, 300U), (500U, 0U)]);
        var hp = frames.Select((frame, index) => (frame, index)).Where(item =>
            item.frame.Recipient == TargetPlayerUid && DirectNormalDeathIsHp(item.frame.Bytes)).ToArray();
        Assert.Equal(8, hp.Length);
        Assert.True(hp[0].index > query && hp[^1].index < addedDead);
        var zeroTeam = frames.Select((frame, index) => (frame, index)).Where(item =>
            LegacyTeamMemberInfoPacket.TryParse(ReadFrame(item.frame.Bytes), out var team) &&
            team.Count == 1 && team.Entries.Count == 1 && team.Entries[0].Uid == TargetPlayerUid &&
            team.Entries[0].MaxHitpoints == 500 && team.Entries[0].MinHitpoints == 0).ToArray();
        Assert.Equal(2, zeroTeam.Length);
        Assert.Equal(new[] { PlayerUid, TargetPlayerUid }.Order(), zeroTeam.Select(item => item.frame.Recipient).Order());
        Assert.All(zeroTeam, item => Assert.True(item.index > hp[5].index && item.index < hp[6].index));
        Assert.Single(frames, frame => frame.Recipient == TargetPlayerUid && DirectNormalDeathTalk(frame.Bytes));
        Assert.Single(frames, frame => frame.Recipient == TargetPlayerUid && DirectNormalDeathVector(frame.Bytes, dead: true));
        Assert.Contains(frames.Skip(addedDead + 1), frame => frame.Recipient == TargetPlayerUid &&
            MsgAttackPacket.TryParse(ReadFrame(frame.Bytes), out var death) && death.Query.AttackType == MsgAttackType.Death);
        Assert.Equal(0, fixture.ScheduledTimers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectNormalDeathRealSacrificeFalseLifetimeSurvivesAndDistinctClocksAreCapturedBeforeQuery(
        bool advanceDuringDeadTalk)
    {
        var armed = false;
        var talks = 0;
        var queries = 0;
        var origin = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
        using var fixture = new DirectNormalDeathFixture(onSend: (current, recipient, bytes) =>
        {
            if (!armed || recipient != TargetPlayerUid) return ValueTask.CompletedTask;
            if (DirectNormalDeathTalk(bytes) && Interlocked.Increment(ref talks) == 1)
            {
                Assert.Equal(origin.AddSeconds(1), DirectNormalDeathTimestamp(current.Target,
                    LegacyPlayerDeathState.GhostStampAttribute));
                Assert.False(current.Target.Character.Attributes.ContainsKey(LegacyPlayerDeathState.DeadStampAttribute));
                if (advanceDuringDeadTalk) current.Advance(TimeSpan.FromSeconds(2));
            }
            if (LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _) && Interlocked.Increment(ref queries) == 1)
            {
                Assert.Equal(300, current.Target.Character.MinHitPoints); // Not atomically dead before source query.
                Assert.False(HasLegacyStatusFlag(current.Target.Character.Attributes, 5));
                Assert.True(HasLegacyStatusFlag(current.Target.Character.Attributes, 0xD3));
                Assert.Equal(origin.AddSeconds(advanceDuringDeadTalk ? 2 : 0), DirectNormalDeathTimestamp(
                    current.Target, LegacyPlayerDeathState.DeadStampAttribute));
                current.Advance(TimeSpan.FromSeconds(5)); // The query callback must NOT restamp death later.
            }
            return ValueTask.CompletedTask;
        });
        await fixture.PrepareNativeQueryInputsAsync();
        var castStarted = DateTimeOffset.UtcNow;
        await fixture.CastSacrificeAsync();
        var castCompleted = DateTimeOffset.UtcNow;
        Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, 0xD3));
        var sacrificeDeadline = fixture.Target.Character.Attributes["LegacySacrificeExpiresAtUtc"];
        // Existing14320 uses wall UTC, independently of the new death owner's manual clock.
        Assert.InRange(DateTimeOffset.Parse(sacrificeDeadline, CultureInfo.InvariantCulture),
            castStarted.AddDays(30), castCompleted.AddDays(30));
        Assert.Empty(fixture.Target.PlayerItems.RuneItems); // No invented rune/HP-reduction input.
        await fixture.JoinAsync();
        armed = true;
        await fixture.KillAsync();
        Assert.Equal(1, talks);
        Assert.Equal(1, queries);
        Assert.Equal(0, fixture.Target.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, 5));
        Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, 0xD3)); // Source registration is RemoveOnDead:false.
        Assert.Equal(sacrificeDeadline, fixture.Target.Character.Attributes["LegacySacrificeExpiresAtUtc"]);
        Assert.Equal(origin.AddSeconds(1), DirectNormalDeathTimestamp(fixture.Target, LegacyPlayerDeathState.GhostStampAttribute));
        Assert.Equal(origin.AddSeconds(advanceDuringDeadTalk ? 2 : 0), DirectNormalDeathTimestamp(
            fixture.Target, LegacyPlayerDeathState.DeadStampAttribute));
        Assert.Equal(origin.AddSeconds(advanceDuringDeadTalk ? 7 : 5), fixture.UtcNow);
        Assert.Equal(0, fixture.ScheduledTimers);
    }

    private static DateTimeOffset DirectNormalDeathTimestamp(WorldSession session, string key) =>
        DateTimeOffset.Parse(session.Character.Attributes[key], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static bool DirectNormalDeathTalk(byte[] bytes) =>
        LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var message) &&
        message.Content.Contains(LegacyPlayerDeathState.DeadTalkText);

    private static bool DirectNormalDeathVector(byte[] bytes, bool dead) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) && update.Uid == TargetPlayerUid &&
        update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
            entry.Values is { } values && IsLegacyStatusFlagSet(values, 5) == dead);

    private static bool DirectNormalDeathIsHp(byte[] bytes) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) && update.Uid == TargetPlayerUid &&
        update.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints);

    private static void DirectNormalDeathAssertPairs(IEnumerable<(uint Recipient, byte[] Bytes)> frames,
        (uint Maximum, uint Hp)[] phases)
    {
        var packets = ParseMsgUpdates(frames.Where(frame => frame.Recipient == TargetPlayerUid).Select(frame => frame.Bytes))
            .Where(update => update.Uid == TargetPlayerUid && update.Updates.Any(entry =>
                entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints)).ToArray();
        Assert.Equal(phases.Length * 2, packets.Length);
        for (var index = 0; index < phases.Length; index++)
        {
            var maximum = Assert.Single(packets[index * 2].Updates);
            Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, maximum.Type);
            Assert.Equal((ulong)phases[index].Maximum, maximum.Value);
            var phase = phases[index];
            Assert.Collection(packets[index * 2 + 1].Updates,
                max => { Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, max.Type); Assert.Equal((ulong)phase.Maximum, max.Value); },
                hp => { Assert.Equal(LegacyMsgUpdateDataType.Hitpoints, hp.Type); Assert.Equal((ulong)phase.Hp, hp.Value); });
        }
    }

    private sealed class DirectNormalDeathFixture : IDisposable
    {
        private const ushort Thunder = 1_000, Sacrifice = 14_320;
        private readonly WorldSessionStore _sessions = new();
        private readonly TeamSessionStateStore _teams = new();
        private readonly Dictionary<uint, ConquerTcpConnection> _connections = [];
        private readonly ManualRuntimeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
        private readonly StringWriter _log = new();
        private readonly WorldPacketHandlerContext _context;

        public DirectNormalDeathFixture(Func<DirectNormalDeathFixture, uint, byte[], ValueTask>? onSend = null,
            long targetHitpoints = 300, bool xpListReady = false, bool passiveFlowKnack = false,
            bool deathIdentityLine = false)
        {
            const uint helmet = 111_097;
            var row = DirectOverlayItem(helmet, "NormalDeathHelmet");
            var fields = row.Fields.ToArray();
            fields[18] = "500";
            fields[19] = "500"; // Legal raw ItemMP; real251 preserves existing current500MP.
            var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
            [
                CreateMagicRow(Thunder, sort: 1, useMana: 0, useStamina: 0, damage: 799, range: 18,
                    coolDown: 0, coldTime: 0),
                CreateMagicRow(Sacrifice, sort: 6, useMana: 0, useStamina: 0, damage: 100,
                    coolDown: 0, coldTime: 0),
                CreateMagicRow(10_420, sort: 51, useMana: 0, useStamina: 0, damage: 20,
                    duration: 60, coolDown: 0, coldTime: 0),
                CreateMagicRow(11_200, sort: 51, useMana: 0, useStamina: 0, damage: 100,
                    duration: 60, coolDown: 0, coldTime: 0),
                CreateMagicRow(1_025, sort: 6, useMana: 0, useStamina: 0, duration: 20,
                    coolDown: 0, coldTime: 0),
                CreateMagicRow(1_110, sort: 6, useMana: 0, useStamina: 0, duration: 20,
                    coolDown: 0, coldTime: 0),
                CreateMagicRow(12_200, sort: 6, useMana: 0, useStamina: 0, damage: 3_000,
                    duration: 60, coolDown: 0, coldTime: 0),
                CreateMagicRow(6_001, sort: 27, useMana: 0, useStamina: 0, damage: 0,
                    duration: 60, coolDown: 0, coldTime: 0),
                CreateMagicRow(10_470, sort: 52, useMana: 0, useStamina: 0, damage: 10,
                    duration: 60, coolDown: 0, coldTime: 0),
                CreateMagicRow(20_210, sort: 6, useMana: 0, useStamina: 0, damage: 1,
                    gDamage: 1, duration: 10, coolDown: 0, coldTime: 0),
                .. (deathIdentityLine ? new[]
                {
                    CreateMagicRow(DirectNormalDeathIdentitySpell, sort: 99, useMana: 0, useStamina: 0,
                        damage: 100, range: 15, coolDown: 0, coldTime: 0),
                } : Array.Empty<MagicType>()),
            ], items: [row with { Fields = fields }],
                combatGear:
                [
                    .. (deathIdentityLine ? new[] { CreateNativeOrdinaryMagicNeutralLoadedGear(201) } : Array.Empty<CombatGear>()),
                    .. (passiveFlowKnack ? new[] { CreateNativeOrdinaryMagicNeutralLoadedGear(1302) } : Array.Empty<CombatGear>()),
                ]);
            WorldSession Session(uint uid, string name, ushort x, ushort spell) => CreateSession(uid: uid,
                name: name, mapId: 1_000, x: x, y: 584,
                classCode: uid == TargetPlayerUid && passiveFlowKnack ? 13_005 : 2_005, reborn: 2,
                strength: 0, spirit: 0, minHitPoints: uid == TargetPlayerUid ? targetHitpoints : 300,
                stamina: 100, spellIds: uid == TargetPlayerUid ? [spell, 10_420, 11_200, 1_025, 1_110, 12_200, 20_210, 10_470] : [spell, 6_001],
                attributes: new Dictionary<string, string>
                {
                    ["PkMode"] = "0", ["BattlePower"] = "100",
                    ["StatusFlags"] = BuildStatusFlags(xpListReady && uid == TargetPlayerUid ? [4] : []),
                    ["LegacyCombatGearPassiveRoll.20210"] = "0",
                },
                playerItems: PlayerItemSnapshot.Empty with
                {
                    EquipmentItems = [new PlayerItemSnapshotItem { Uid = uid + 100, ItemId = helmet, Position = 1,
                        Durability = 100, MaximumDurability = 100 }],
                });
            var owner = Session(PlayerUid, "NormalDeathCaster", 830, Thunder);
            var target = Session(TargetPlayerUid, "NormalDeathTarget", 832, Sacrifice);
            if (deathIdentityLine)
            {
                owner = owner with
                {
                    PlayerSpells = new PlayerSpellSnapshot(owner.PlayerSpells.Spells
                        .Append(CreatePlayerSpell(DirectNormalDeathIdentitySpell)).ToArray()),
                    CombatGear = CombatGearSnapshot.Empty with
                    {
                        Count = 1, Items = [new CombatGearSnapshotItem(201, 1, 0, 1, 0, 0, [[], []], [])],
                    },
                    Character = owner.Character with
                    {
                        Attributes = new Dictionary<string, string>(owner.Character.Attributes)
                        {
                            ["LegacyCombatGearAttackSpellIndex"] = "3",
                        },
                    },
                };
                target = target with { Character = target.Character with { MinHitPoints = 1 } };
            }
            if (passiveFlowKnack)
                target = target with
                {
                    CombatGear = CombatGearSnapshot.Empty with
                    {
                        Items = [new CombatGearSnapshotItem(1_302, 1, 0, 1, 0, 0, [[], []], [])],
                    },
                };
            foreach (var uid in deathIdentityLine
                         ? new[] { PlayerUid, TargetPlayerUid, DirectNormalDeathIdentityOtherTarget }
                         : new[] { PlayerUid, TargetPlayerUid })
                _connections[uid] = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                    sendAsync: async (memory, token) =>
                    {
                        var bytes = memory.ToArray();
                        Order.Enqueue((uid, bytes));
                        if (onSend is not null) await onSend(this, uid, bytes);
                    });
            _sessions.Set(_connections[PlayerUid], owner);
            _sessions.Set(_connections[TargetPlayerUid], target);
            if (deathIdentityLine)
            {
                var other = Session(DirectNormalDeathIdentityOtherTarget, "NormalDeathSecondTarget", 833, Sacrifice);
                _sessions.Set(_connections[DirectNormalDeathIdentityOtherTarget],
                    other with { Character = other.Character with { MinHitPoints = 1 } });
            }
            _context = new WorldPacketHandlerContext(_connections[PlayerUid], TextWriter.Synchronized(_log), owner,
                WorldData: world, Sessions: _sessions, TeamStates: _teams, TimeProvider: _clock,
                LegacyClock: LegacySignedMillisecondClock.FromTimeProvider(_clock, 10_000),
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        }

        public ConcurrentQueue<(uint Recipient, byte[] Bytes)> Order { get; } = new();
        public WorldSession Target => Slot(TargetPlayerUid);
        public DateTimeOffset UtcNow => _clock.GetUtcNow();
        public int ScheduledTimers => _clock.ScheduledTimerCount;
        public void Advance(TimeSpan amount) => _clock.Advance(amount);
        private WorldSession Slot(uint uid)
        {
            Assert.True(_sessions.TryGet(_connections[uid], out var current));
            return Assert.IsType<WorldSession>(current);
        }
        private WorldPacketHandlerContext Context(uint uid) =>
            _context with { Connection = _connections[uid], Session = Slot(uid) };

        public async Task JoinAsync()
        {
            await PacketHandlers.HandleAsync(Context(PlayerUid), ReadFrame(new MsgTeamPacket(MsgTeamType.Create, PlayerUid).ToFrameBytes()));
            await PacketHandlers.HandleAsync(Context(TargetPlayerUid), ReadFrame(new MsgTeamPacket(MsgTeamType.AcceptInvitation, PlayerUid).ToFrameBytes()));
            Assert.Equal(2U, _teams.GetMemberCount(_connections[PlayerUid]));
        }
        public async Task<LegacyPlayerStatusPacket> StatusAsync(uint uid)
        {
            var start = Order.Count;
            await PacketHandlers.HandleAsync(Context(uid), ReadFrame(new LegacyPlayerStatusQueryPacket(uid).ToFrameBytes()));
            var bytes = Assert.Single(Order.ToArray().Skip(start), frame => frame.Recipient == uid &&
                LegacyPlayerStatusPacket.TryParse(ReadFrame(frame.Bytes), out _)).Bytes;
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out var status));
            return status;
        }
        public async Task PrepareNativeQueryInputsAsync()
        {
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
                await PrepareNativeOrdinaryMagicInputQueryAsync(Context(uid),
                    () => Order.ToArray().Where(frame => frame.Recipient == uid).Select(frame => frame.Bytes).ToArray(),
                    expectedMagicAttack: 1, expectedMaximumHitpoints: 500, expectedMaximumMana: 500);
        }
        public async Task KillPhysicalAsync() => await PacketHandlers.HandleAsync(Context(PlayerUid),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: PlayerUid, OpponentUid: TargetPlayerUid,
                X: checked((ushort)Target.Character.X), Y: checked((ushort)Target.Character.Y),
                AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0, ResponseDamage: 0,
                Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes()));
        public async Task KillAsync() => await CastAsync(PlayerUid, TargetPlayerUid, Thunder);
        public async Task CastSacrificeAsync() => await CastAsync(TargetPlayerUid, TargetPlayerUid, Sacrifice);
        public async Task CastStatusAsync(ushort spell) => await CastAsync(TargetPlayerUid, TargetPlayerUid, spell);
        public async Task CastPoisonAsync() => await CastAsync(PlayerUid, TargetPlayerUid, 6_001);
        public void BindTarget(WorldSession session) => _sessions.Set(_connections[TargetPlayerUid], session);
        public bool TryIdentitySlot(uint slot, out WorldSession? session) =>
            _sessions.TryGet(_connections[slot], out session);
        public WorldSession RemoveIdentitySlot(uint slot)
        {
            Assert.True(_sessions.TryRemove(_connections[slot], out var removed));
            Assert.NotNull(removed);
            Assert.Equal((long)slot, removed.Character.Uid);
            return removed;
        }
        public WorldSession RebindIdentitySlot(uint slot)
        {
            var replacement = CreateSession(uid: slot + 9_000, name: "NormalDeathReplacement",
                mapId: 1_000, x: 832, y: 584, classCode: 2_005, strength: 0, spirit: 0,
                minHitPoints: 77_777, minMana: 77, stamina: 33, spellIds: [],
                attributes: new Dictionary<string, string>
                {
                    ["MaxHitpoints"] = "77777", ["DirectNormalDeathIdentity"] = "replacement",
                });
            _sessions.Set(_connections[slot], replacement);
            return replacement;
        }
        public async Task KillIdentityLineAsync() => await PacketHandlers.HandleAsync(Context(PlayerUid),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: PlayerUid, OpponentUid: TargetPlayerUid,
                X: 120, Y: 121, AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes()));
        public void KeepOrdinaryDeathPosition(WorldSession earned)
        {
            // Registration was earned by the real tournament owner; only leave
            // its tournament/routing context so this test enters normal Dead.
            var attributes = new Dictionary<string, string>(earned.Character.Attributes, StringComparer.OrdinalIgnoreCase);
            foreach (var key in new[] { "DynamicID", "DynamicId", "LegacyTournamentInTournament",
                         "LegacyTournamentType", "LegacyTournamentProcess" })
                attributes.Remove(key);
            BindTarget(earned with
            {
                Character = earned.Character with { MapId = 1_000, X = 832, Y = 584, Attributes = attributes },
            });
        }
        private async Task CastAsync(uint caster, uint target, ushort spell) => await PacketHandlers.HandleAsync(Context(caster),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: caster, OpponentUid: target, X: 832, Y: 584,
                AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0, ResponseDamage: 0,
                Effect: MsgAttackEffect.None, Unknown: 0, SpellId: spell)).ToFrameBytes()));
        public void Dispose()
        {
            GC.KeepAlive(_connections);
            _log.Dispose();
        }
    }
}
