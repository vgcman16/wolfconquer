using System.Collections.Concurrent;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(0U, 100U)]
    [InlineData(1U, 1U)]
    [InlineData(477U, 477U)]
    [InlineData(577U, 577U)]
    [InlineData(50_123U, 50_123U)]
    public void DirectFineRainHpParserPreservesSourceRawIntegerExceptZero(uint raw, uint expected)
    {
        var world = CreateMagicCatalog(CreateMonsterFamily(), [],
            CreateMagicRow(FineRainAttachStatusSpellId, sort: 6, damage: raw, duration: 60));
        Assert.True(LegacyMagicRuntime.TryResolveSpell(world, FineRainAttachStatusSpellId, 0, out var spell));
        Assert.Equal(raw, spell.RawDamage);
        Assert.Equal(expected, spell.Damage); // Literal source MagicType245/268-270.
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DirectFineRainHpCacheOnlyBelongsToItsRegisteredOwner(bool sameOwner)
    {
        var session = CreateSession() with
        {
            EquipmentHitpointsStatus = new LegacyEquipmentHitpointsStatus(
                sameOwner ? PlayerUid : PlayerUid + 1, 600),
        };
        Assert.Equal(sameOwner, LegacyEquipmentHitpointsStatus.TryGetForOwner(session, out var maximum));
        Assert.Equal(sameOwner ? 600U : 0U, maximum);
    }

    [Fact]
    public async Task DirectFineRainHpRealAdmittedRepeatCastAddsPowerOnlyOnceAndPublishesItsCurrentMaximum()
    {
        var beforeQuery = 0;
        using var fixture = new DirectFineRainHpFixture(onSend: async (current, bytes) =>
        {
            if (!LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) || update.Uid != PlayerUid ||
                !update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.FineRain &&
                    entry.Flag == FineRain1StatusFlag) || Interlocked.CompareExchange(ref beforeQuery, 1, 0) != 0) return;
            // First real countdown is before QueryEquipment and before HP fill.
            Assert.Equal(300, current.Current.Character.MinHitPoints);
            Assert.Equal(500U, (await current.QueryAsync()).MaxHP);
            Assert.Equal(0, LegacyFineRainEquipmentAdmission.ForOwner(current.Current).StampMilliseconds);
            DirectFineRainHpAssertOwnedMaximum(current.Current, 500);
        });
        Assert.Equal(500U, (await fixture.QueryAsync()).MaxHP);
        Assert.Equal(300, fixture.Current.Character.MinHitPoints);
        var first = fixture.Frames.Count;
        await fixture.CastAsync();
        await fixture.AssertStateAsync(maximum: 600, hitpoints: 600, power: "100", stamp: 10_000);
        fixture.AssertPublishedHitpointPhases(first,
            [(500U, 300U), (600U, 300U), (600U, 300U), (600U, 600U)]);
        Assert.Equal(1, beforeQuery);
        Assert.Equal(0, fixture.WaitCount);
        // A public2301 query observes the cache; it does not rebuild or renew it.
        Assert.Equal(600U, (await fixture.QueryAsync()).MaxHP);
        Assert.Equal(10_000, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Current).StampMilliseconds);
        fixture.Advance(TimeSpan.FromSeconds(6));
        var second = fixture.Frames.Count;
        await fixture.CastAsync();
        await fixture.AssertStateAsync(maximum: 600, hitpoints: 600, power: "100", stamp: 16_000);
        fixture.AssertPublishedHitpointPhases(second,
            [(500U, 600U), (600U, 600U), (600U, 600U), (600U, 600U)]);
        Assert.Equal(0, fixture.WaitCount); // No600->700 accumulation from currentHP.
    }

    [Fact]
    public async Task DirectFineRainHpRejectedChangedPowerRetainsOldMaximumUntilActualAdmittedQueryClampsIt()
    {
        using var fixture = new DirectFineRainHpFixture();
        await fixture.CastAsync();
        await fixture.AssertStateAsync(maximum: 600, hitpoints: 600, power: "100", stamp: 10_000);
        fixture.Advance(TimeSpan.FromSeconds(1));
        // Learned-level input changes, never a desired HP/cache/stamp answer.
        fixture.SelectLearnedFineRainLevel(1);
        var rejected = fixture.Frames.Count;
        await fixture.CastAsync();
        await fixture.AssertStateAsync(maximum: 600, hitpoints: 600, power: "1", stamp: 10_000);
        fixture.AssertPublishedHitpoints(rejected, 600);
        Assert.Equal(1, fixture.WaitCount);
        fixture.Advance(TimeSpan.FromMilliseconds(4_999));
        Assert.Equal(600U, (await fixture.QueryAsync()).MaxHP);
        Assert.Equal(600, fixture.Current.Character.MinHitPoints);
        Assert.Equal(1, fixture.WaitCount);
        fixture.Advance(TimeSpan.FromMilliseconds(1));
        // Real2286 is a source QueryEquipment owner. Inclusive6000 admission
        // reads NEW current power1, rebuilds500+1, and OnDequeue clamps600->501.
        await fixture.QueryEquipmentAsync();
        await fixture.AssertStateAsync(maximum: 501, hitpoints: 501, power: "1", stamp: 16_000);
        Assert.Equal(1, fixture.WaitCount);
        await fixture.QueryEquipmentAsync(); // Same tick rejection is non-renewing.
        await fixture.AssertStateAsync(maximum: 501, hitpoints: 501, power: "1", stamp: 16_000);
        Assert.Equal(2, fixture.WaitCount);
    }

    [Fact]
    public async Task DirectFineRainHpThreeRealNonlethalImpReceivesExposeCurrent75Then112Then168()
    {
        await using var fixture = new DirectOverlayLifecycleFixture(lethal: false);
        await fixture.CastFineRainAsync();
        Assert.Equal(100_100, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(100_100U, (await fixture.QueryAsync()).MaxHP);
        Assert.Equal("100", fixture.Owner.Character.Attributes["FineRainPower"]);
        Assert.Equal(291U, await fixture.StrikeOwnerAsync());
        Assert.Equal(99_809, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(75U, (await fixture.QueryAsync()).Counteraction);
        Assert.Equal(225U, await fixture.StrikeOwnerAsync());
        Assert.Equal(99_584, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(112U, (await fixture.QueryAsync()).Counteraction);
        Assert.Equal(225U, await fixture.StrikeOwnerAsync());
        Assert.Equal(99_359, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(168U, (await fixture.QueryAsync()).Counteraction);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, FineRainDeadStatusFlag));
        Assert.Equal(0, fixture.WaitCount);
        Assert.Equal(10_000, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Owner).StampMilliseconds);
    }

    [Fact]
    public async Task DirectFineRainHpUncachedOrdinaryReceiveKeepsItsPreviousHpCompatibilityFloor()
    {
        var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
        [
            CreateMagicRow(DirectOverlaySpellId, sort: 99, useMana: 0, damage: 30_250, range: 4,
                damageOnHuman: 0, damage2: 33, damage3: 2, damageOnMonster: 30_085, gDamage: 0,
                coolDown: 0, coldTime: 0),
        ]);
        var caster = CreateSession(mapId: 1_000, classCode: 2_005, reborn: 2, strength: 300, spirit: 0,
            minHitPoints: 100_000, stamina: 100, spellIds: [DirectOverlaySpellId],
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "0", ["BattlePower"] = "100",
                ["LegacyCombatGearAttackSpellIndex"] = "3", ["LegacyCombatGearAttackRateRoll"] = "99",
            }) with
        {
            CombatGear = CombatGearSnapshot.Empty with
            {
                Count = 1, Items = [new CombatGearSnapshotItem(201, Level: 1, Exp: 0, Equip: 1,
                    Transform: 0, MasteryScore: 0, Animas: [], BlackJades: [])],
            },
        };
        var target = CreateSession(uid: TargetPlayerUid, name: "FineRainUncachedRawZero", mapId: 1_000,
            x: 839, y: 584, classCode: 2_005, strength: 0, spirit: 0, minHitPoints: 10_000,
            spellIds: [], attributes: new Dictionary<string, string> { ["BattlePower"] = "100" });
        Assert.Null(target.EquipmentHitpointsStatus);
        Assert.False(target.Character.Attributes.ContainsKey("MaxHitpoints"));
        Assert.Equal(0U, LegacyMaxHitpointsCalculator.Calculate(target.Character, target.PlayerItems,
            CombatGearSnapshot.Empty, world).Total); // Deliberately incomplete legacy raw inputs.
        var casterFrames = new ConcurrentQueue<byte[]>();
        var targetFrames = new ConcurrentQueue<byte[]>();
        ConquerTcpConnection Connection(ConcurrentQueue<byte[]> frames) =>
            new(null, new IPEndPoint(IPAddress.Loopback, 9958), sendAsync: (memory, _) =>
            {
                frames.Enqueue(memory.ToArray());
                return ValueTask.CompletedTask;
            });
        var casterConnection = Connection(casterFrames);
        var targetConnection = Connection(targetFrames);
        var sessions = new WorldSessionStore();
        sessions.Set(casterConnection, caster);
        sessions.Set(targetConnection, target);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(casterConnection, TextWriter.Synchronized(log), caster,
            WorldData: world, Sessions: sessions, MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore(), ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        await PacketHandlers.HandleAsync(context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: PlayerUid, OpponentUid: TargetPlayerUid,
                X: 120, Y: 121, AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes()));
        Assert.True(sessions.TryGet(targetConnection, out var damaged));
        Assert.NotNull(damaged);
        Assert.Equal(9_550, damaged.Character.MinHitPoints);
        var animation = Assert.Single(ParseSpellAnimations(casterFrames.ToArray()),
            packet => packet.SpellId == DirectOverlaySpellId);
        Assert.Equal(450U, Assert.Single(animation.Targets).Damage);
        DirectFineRainHpAssertMaximumUpdates(targetFrames.ToArray(), 10_000, TargetPlayerUid);
        Assert.Contains(ParseMsgUpdates(targetFrames), update => update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints && entry.Value == 9_550));
        GC.KeepAlive(casterConnection);
        GC.KeepAlive(targetConnection);
    }

    private static void DirectFineRainHpAssertMaximumUpdates(IEnumerable<byte[]> frames, uint expected,
        uint uid = PlayerUid)
    {
        var maxima = ParseMsgUpdates(frames).Where(update => update.Uid == uid)
            .SelectMany(update => update.Updates)
            .Where(entry => entry.Type == LegacyMsgUpdateDataType.MaxHitpoints).ToArray();
        Assert.NotEmpty(maxima);
        Assert.All(maxima, entry => Assert.Equal((ulong)expected, entry.Value));
    }

    private static void DirectFineRainHpAssertOwnedMaximum(WorldSession owner, uint expected)
    {
        Assert.True(LegacyEquipmentHitpointsStatus.TryGetForOwner(owner, out var maximum));
        Assert.Equal(expected, maximum);
    }

    private sealed class DirectFineRainHpFixture : IDisposable
    {
        private readonly WorldSessionStore _sessions = new();
        private readonly ConquerTcpConnection _connection;
        private readonly WorldPacketHandlerContext _context;
        private readonly ManualRuntimeTimeProvider _clock;
        private readonly StringWriter _log = new();

        public DirectFineRainHpFixture(Func<DirectFineRainHpFixture, byte[], ValueTask>? onSend = null)
        {
            _clock = new ManualRuntimeTimeProvider(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
            var signed = LegacySignedMillisecondClock.FromTimeProvider(_clock, 10_000);
            const uint helmetId = 111_070;
            var helmet = DirectOverlayItem(helmetId, "FineRainHpLiteral500");
            var fields = helmet.Fields.ToArray();
            fields[18] = "500";
            helmet = helmet with { Fields = fields };
            var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
            [
                CreateMagicRow(FineRainAttachStatusSpellId, sort: 6, level: 0, useMana: 0,
                    damage: 0, duration: 60, coolDown: 0, coldTime: 0),
                CreateMagicRow(FineRainAttachStatusSpellId, sort: 6, level: 1, useMana: 0,
                    damage: 1, duration: 60, coolDown: 0, coldTime: 0),
            ], items: [helmet]);
            var owner = CreateSession(mapId: 1_000, classCode: 2_005, reborn: 2, strength: 0, spirit: 0,
                minHitPoints: 300, stamina: 100, spellIds: [FineRainAttachStatusSpellId],
                playerItems: PlayerItemSnapshot.Empty with
                {
                    EquipmentItems = [new PlayerItemSnapshotItem
                    {
                        Uid = 96_001, ItemId = helmetId, Position = 1, Durability = 100, MaximumDurability = 100,
                    }],
                }, attributes: new Dictionary<string, string> { ["MaxHitpoints"] = "500" });
            Assert.Equal(500U, LegacyMaxHitpointsCalculator.Calculate(owner.Character, owner.PlayerItems,
                CombatGearSnapshot.Empty, world).Total); // Real itemHP, not the stored/currentHP floor.
            Assert.Equal(2, owner.Character.Reborn); // Real2286 admission owner requires second rebirth.
            _connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                sendAsync: async (memory, _) =>
                {
                    var bytes = memory.ToArray();
                    Frames.Enqueue(bytes);
                    if (onSend is not null) await onSend(this, bytes);
                });
            _sessions.Set(_connection, owner);
            _context = new WorldPacketHandlerContext(_connection, TextWriter.Synchronized(_log), owner,
                WorldData: world, Sessions: _sessions, TimeProvider: _clock, LegacyClock: signed,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        }

        public ConcurrentQueue<byte[]> Frames { get; } = new();
        public int WaitCount => Frames.Count(DirectOverlayLifecycleIsWait);
        public WorldSession Current
        {
            get
            {
                Assert.True(_sessions.TryGet(_connection, out var current));
                Assert.NotNull(current);
                return current;
            }
        }

        public void Advance(TimeSpan amount) => _clock.Advance(amount); // No background runtime was started.

        public void SelectLearnedFineRainLevel(ushort level)
        {
            var current = Current;
            var spells = current.PlayerSpells.Spells.Select(spell => spell.Id == FineRainAttachStatusSpellId
                ? spell with { Level = level } : spell).ToArray();
            _sessions.Set(_connection, current with { PlayerSpells = new PlayerSpellSnapshot(spells) });
        }

        public async Task CastAsync()
        {
            await PacketHandlers.HandleAsync(_context with { Session = Current },
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: PlayerUid, OpponentUid: PlayerUid,
                    X: 830, Y: 584, AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
                    ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0,
                    SpellId: FineRainAttachStatusSpellId)).ToFrameBytes()));
        }

        public async Task QueryEquipmentAsync()
        {
            await PacketHandlers.HandleAsync(_context with { Session = Current },
                ReadFrame(LegacyRuneStoragePacket.Startup().ToFrameBytes()));
        }

        public async Task<LegacyPlayerStatusPacket> QueryAsync()
        {
            var start = Frames.Count;
            await PacketHandlers.HandleAsync(_context with { Session = Current },
                ReadFrame(new LegacyPlayerStatusQueryPacket(PlayerUid).ToFrameBytes()));
            var frame = Assert.Single(Frames.ToArray().Skip(start), bytes =>
                LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out _));
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(frame), out var status));
            Assert.Equal(PlayerUid, status.Uid);
            return status;
        }

        public async Task AssertStateAsync(uint maximum, long hitpoints, string power, int stamp)
        {
            Assert.Equal(hitpoints, Current.Character.MinHitPoints);
            Assert.Equal(maximum, (await QueryAsync()).MaxHP);
            Assert.Equal(power, Current.Character.Attributes["FineRainPower"]);
            Assert.Equal(power, Current.Character.Attributes["defFineRainPower"]);
            Assert.True(HasLegacyStatusFlag(Current.Character.Attributes, FineRain1StatusFlag));
            Assert.True(HasLegacyStatusFlag(Current.Character.Attributes, FineRain2StatusFlag));
            Assert.Equal(stamp, LegacyFineRainEquipmentAdmission.ForOwner(Current).StampMilliseconds);
            var cached = Current.EquipmentHitpointsStatus;
            Assert.NotNull(cached);
            Assert.Equal((long)PlayerUid, cached.OwnerUid);
            Assert.Equal(maximum, cached.MaximumHitpoints);
        }

        public void AssertPublishedHitpointPhases(int start, (uint Maximum, uint Hitpoints)[] phases)
        {
            // Include EVERY HP-bearing packet: early maxima are expected phases,
            // not filtered away in favor of the final600 total.
            var packets = ParseMsgUpdates(Frames.ToArray().Skip(start)).Where(packet => packet.Uid == PlayerUid &&
                packet.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or
                    LegacyMsgUpdateDataType.Hitpoints)).ToArray();
            Assert.Equal(phases.Length * 2, packets.Length);
            for (var index = 0; index < phases.Length; index++)
            {
                var maximumPacket = packets[index * 2];
                var maximum = Assert.Single(maximumPacket.Updates);
                Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, maximum.Type);
                Assert.Equal((ulong)phases[index].Maximum, maximum.Value);
                var phase = phases[index];
                Assert.Collection(packets[index * 2 + 1].Updates,
                    entry =>
                    {
                        Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, entry.Type);
                        Assert.Equal((ulong)phase.Maximum, entry.Value);
                    },
                    entry =>
                    {
                        Assert.Equal(LegacyMsgUpdateDataType.Hitpoints, entry.Type);
                        Assert.Equal((ulong)phase.Hitpoints, entry.Value);
                    });
            }
        }

        public void AssertPublishedHitpoints(int start, uint expected)
        {
            var updates = ParseMsgUpdates(Frames.ToArray().Skip(start)).Where(update => update.Uid == PlayerUid)
                .SelectMany(update => update.Updates).ToArray();
            var maxima = updates.Where(entry => entry.Type == LegacyMsgUpdateDataType.MaxHitpoints).ToArray();
            Assert.NotEmpty(maxima);
            Assert.All(maxima, entry => Assert.Equal((ulong)expected, entry.Value));
            Assert.Contains(updates, entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints &&
                entry.Value == expected);
        }

        public void Dispose()
        {
            GC.KeepAlive(_connection);
            _log.Dispose();
        }
    }
}
