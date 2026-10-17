using System.Collections.Concurrent;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectTransformReceiveRealDefenderDeathFinishesBeforeClearFlagsAndCannotRefillDeadHp(bool withFineRain)
    {
        var finishSeen = 0;
        var waitSeen = 0;
        using var fixture = new DirectTransformReceiveFixture(attackDamage: 4_000, withFineRain,
            onSend: async (current, recipient, bytes) =>
            {
                if (recipient != PlayerUid) return;
                if (DirectOverlayLifecycleIsWait(bytes))
                {
                    Interlocked.Increment(ref waitSeen);
                    Assert.True(withFineRain);
                    Assert.False(LegacyTransformState.TryGetActiveMaxHitpoints(current.Owner.Character, out _));
                    Assert.True(HasLegacyStatusFlag(current.Owner.Character.Attributes, FineRain1StatusFlag));
                    Assert.False(HasLegacyStatusFlag(current.Owner.Character.Attributes, FineRainDeadStatusFlag));
                    await current.AssertOwnerAsync(3_000, 3_000, 10_000);
                }
                if (!DirectTransformReceiveIsFinish(bytes)) return;
                Interlocked.Increment(ref finishSeen);
                // Player.Dead finishes the DEFENDER transform before ClearFlags,
                // its second query and the source's final HitPoints=0 assignment.
                Assert.False(LegacyTransformState.TryGetActiveMaxHitpoints(current.Owner.Character, out _));
                Assert.False(HasLegacyStatusFlag(current.Owner.Character.Attributes, FineRainDeadStatusFlag));
                Assert.Equal(withFineRain, HasLegacyStatusFlag(current.Owner.Character.Attributes, FineRain1StatusFlag));
                await current.AssertOwnerAsync(withFineRain ? 3_000U : 500U,
                    withFineRain ? 3_000 : 500, withFineRain ? 10_000 : 0);
            });
        await fixture.AssertNeutralMagicAsync();
        await fixture.TransformAsync();
        var start = fixture.OwnerFrames.Count;
        await fixture.StrikeOwnerAsync();
        Assert.Equal(1, finishSeen);
        Assert.Equal(withFineRain ? 1 : 0, waitSeen);
        Assert.Equal(withFineRain ? 1 : 0, fixture.WaitCount);
        Assert.False(LegacyTransformState.TryGetActiveMaxHitpoints(fixture.Owner.Character, out _));
        Assert.Equal("0", fixture.Owner.Character.Attributes[LegacyTransformState.TransformationIdAttribute]);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, FineRain1StatusFlag));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, FineRain2StatusFlag));
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, FineRainDeadStatusFlag));
        await fixture.AssertOwnerAsync(500, 0, withFineRain ? 10_000 : 0);
        if (withFineRain) Assert.Equal("1", fixture.Owner.Character.Attributes["FineRainPower"]);
        var received = fixture.OwnerFrames.ToArray().Skip(start).ToArray();
        var animation = Assert.Single(ParseSpellAnimations(received), packet => packet.SpellId == ThunderSpellId);
        var hit = Assert.Single(animation.Targets);
        Assert.Equal(PlayerUid, hit.Uid);
        Assert.True(hit.Hit);
        Assert.True(hit.Damage > 0); // Real positive lethal receive, not a fake HP/death mutation.
        DirectFineRainHpAssertMaximumUpdates(received, 500);
        var hp = ParseMsgUpdates(received).Where(packet => packet.Uid == PlayerUid)
            .SelectMany(packet => packet.Updates)
            .Where(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints).ToArray();
        Assert.NotEmpty(hp);
        Assert.Equal(0UL, hp[^1].Value);
        Assert.Single(received, DirectTransformReceiveIsFinish);
        // Same handler wall/source tick throughout; no runtime timer is started.
    }

    [Fact]
    public async Task DirectTransformReceiveRealWoundThenAdmittedActiveQueryRefillsInsteadOfMerelyClamping()
    {
        using var fixture = new DirectTransformReceiveFixture(attackDamage: 119, withFineRain: false);
        await fixture.AssertNeutralMagicAsync();
        await fixture.TransformAsync();
        var woundStart = fixture.OwnerFrames.Count;
        await fixture.StrikeOwnerAsync();
        Assert.True(LegacyTransformState.TryGetActiveMaxHitpoints(fixture.Owner.Character, out var maximum));
        Assert.Equal(3_000U, maximum);
        await fixture.AssertOwnerAsync(3_000, 2_880, 0);
        var woundFrames = fixture.OwnerFrames.ToArray().Skip(woundStart).ToArray();
        var hit = Assert.Single(Assert.Single(ParseSpellAnimations(woundFrames),
            packet => packet.SpellId == ThunderSpellId).Targets);
        Assert.Equal(120U, hit.Damage); // Source Equip1540 MagicAttack1 + real raw119 spell.
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, FineRainDeadStatusFlag));
        Assert.DoesNotContain(woundFrames, DirectTransformReceiveIsFinish);
        DirectFineRainHpAssertMaximumUpdates(woundFrames, 3_000);
        var queryStart = fixture.OwnerFrames.Count;
        await fixture.QueryEquipmentAsync(); // Actual eligible Reborn2 2286 query.
        await fixture.AssertOwnerAsync(3_000, 3_000, 0);
        Assert.True(LegacyTransformState.TryGetActiveMaxHitpoints(fixture.Owner.Character, out _));
        Assert.Equal(0, fixture.WaitCount);
        var queryUpdates = ParseMsgUpdates(fixture.OwnerFrames.ToArray().Skip(queryStart))
            .Where(packet => packet.Uid == PlayerUid).SelectMany(packet => packet.Updates).ToArray();
        Assert.Contains(queryUpdates, entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints && entry.Value == 3_000);
        Assert.Contains(queryUpdates, entry => entry.Type == LegacyMsgUpdateDataType.MaxHitpoints && entry.Value == 3_000);
        Assert.DoesNotContain(fixture.OwnerFrames.ToArray().Skip(queryStart), DirectTransformReceiveIsFinish);
    }

    private static bool DirectTransformReceiveIsFinish(byte[] bytes) =>
        MsgActionPacket.TryParse(ReadFrame(bytes), out var action) &&
        action.ObjId == PlayerUid && action.Type == MsgActionType.FinishTransform;

    private sealed class DirectTransformReceiveFixture : IDisposable
    {
        private readonly WorldSessionStore _sessions = new();
        private readonly Dictionary<uint, ConquerTcpConnection> _connections = [];
        private readonly Dictionary<uint, ConcurrentQueue<byte[]>> _frames = [];
        private readonly WorldPacketHandlerContext _context;
        private readonly StringWriter _log = new();
        private readonly bool _withFineRain;

        public DirectTransformReceiveFixture(uint attackDamage, bool withFineRain,
            Func<DirectTransformReceiveFixture, uint, byte[], ValueTask>? onSend = null)
        {
            _withFineRain = withFineRain;
            var clock = new ManualRuntimeTimeProvider(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
            var signed = LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000);
            const uint helmetId = 111_095;
            var helmet = DirectOverlayItem(helmetId, "TransformReceiveRawHp500");
            var fields = helmet.Fields.ToArray();
            fields[18] = "500";
            helmet = helmet with { Fields = fields };
            var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
            [
                CreateMagicRow(TransformSpellId, sort: 19, level: 0, useMana: 0, useStamina: 0,
                    damage: 100, duration: 60, experience: 1_000, coolDown: 0, coldTime: 0),
                CreateMagicRow(TransformSpellId, sort: 19, level: 1, useMana: 0, useStamina: 0,
                    damage: 100, duration: 60, experience: 1_000, coolDown: 0, coldTime: 0),
                CreateMagicRow(FineRainAttachStatusSpellId, sort: 6, useMana: 0, damage: 1,
                    duration: 60, coolDown: 0, coldTime: 0),
                CreateMagicRow(ThunderSpellId, sort: 1, useMana: 0, damage: attackDamage,
                    coolDown: 0, coldTime: 0),
            ], items: [helmet], transformInfo:
            [
                new TransformInfo(TransformSpellId, 0, "DivineHare", 207, 3_000,
                    "fixture://TransformInfo.txt", LineNumber: 1, RawLine: "1350 0 DivineHare 207 3000",
                    Fields: ["1350", "0", "DivineHare", "207", "3000"]),
            ]);
            PlayerItemSnapshot Items(uint uid) => PlayerItemSnapshot.Empty with
            {
                EquipmentItems = [new PlayerItemSnapshotItem
                {
                    Uid = uid, ItemId = helmetId, Position = 1, Durability = 100, MaximumDurability = 100,
                }],
            };
            var owner = CreateSession(mapId: 1_000, classCode: 2_005, reborn: 2, strength: 0, spirit: 0,
                minHitPoints: 300, stamina: 100, spellIds: [TransformSpellId, FineRainAttachStatusSpellId],
                playerItems: Items(98_101), attributes: new Dictionary<string, string>
                {
                    ["MaxHitpoints"] = "500", ["PkMode"] = "0", ["BattlePower"] = "120",
                });
            var attacker = CreateSession(uid: TargetPlayerUid, name: "TransformReceiveOtherCaster", mapId: 1_000,
                x: 832, y: 584, classCode: 2_005, strength: 0, spirit: 0, minHitPoints: 500,
                stamina: 100, spellIds: [ThunderSpellId], playerItems: Items(98_102),
                attributes: new Dictionary<string, string>
                {
                    ["MaxHitpoints"] = "500", ["PkMode"] = "0", ["BattlePower"] = "120",
                });
            Assert.Equal(500U, LegacyMaxHitpointsCalculator.Calculate(owner.Character, owner.PlayerItems,
                CombatGearSnapshot.Empty, world).Total);
            Assert.Equal(500U, LegacyMaxHitpointsCalculator.Calculate(attacker.Character, attacker.PlayerItems,
                CombatGearSnapshot.Empty, world).Total);
            Assert.Equal(2, owner.Character.Reborn);
            Assert.Null(owner.EquipmentHitpointsStatus);
            Assert.Null(attacker.EquipmentHitpointsStatus);
            Assert.True(LegacyMagicRuntime.TryResolveSpell(world, ThunderSpellId, 0, out var resolved));
            Assert.Equal(attackDamage, resolved.RawDamage);
            Assert.Equal(attackDamage, resolved.Damage);
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            {
                _frames[uid] = new ConcurrentQueue<byte[]>();
                _connections[uid] = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                    sendAsync: async (memory, _) =>
                    {
                        var bytes = memory.ToArray();
                        _frames[uid].Enqueue(bytes);
                        if (onSend is not null) await onSend(this, uid, bytes);
                    });
            }
            _sessions.Set(_connections[PlayerUid], owner);
            _sessions.Set(_connections[TargetPlayerUid], attacker);
            _context = new WorldPacketHandlerContext(_connections[PlayerUid], TextWriter.Synchronized(_log), owner,
                WorldData: world, Sessions: _sessions, TimeProvider: clock, LegacyClock: signed,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        }

        public ConcurrentQueue<byte[]> OwnerFrames => _frames[PlayerUid];
        public WorldSession Owner => Current(PlayerUid);
        public int WaitCount => OwnerFrames.Count(DirectOverlayLifecycleIsWait);
        private WorldSession Current(uint uid)
        {
            Assert.True(_sessions.TryGet(_connections[uid], out var current));
            return Assert.IsType<WorldSession>(current);
        }

        private WorldPacketHandlerContext Context(uint uid) =>
            _context with { Connection = _connections[uid], Session = Current(uid) };

        public async Task AssertNeutralMagicAsync()
        {
            var attacker = await QueryAsync(TargetPlayerUid);
            var target = await QueryAsync(PlayerUid);
            Assert.Equal(1U, attacker.MagicAttack); // Source Equip1540 unconditional increment.
            Assert.Equal(0U, attacker.MagicPercent);
            Assert.Equal(0U, attacker.SkillCStrike);
            Assert.Equal(0U, attacker.MagicDamageIncrease);
            Assert.Equal(0U, target.MagicDefence);
            Assert.Equal(0U, target.ItemBless);
            Assert.Equal(0U, target.MagicDamageDecrease);
        }

        public async Task TransformAsync()
        {
            if (_withFineRain)
            {
                await PacketHandlers.HandleAsync(Context(PlayerUid),
                    ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: PlayerUid, OpponentUid: PlayerUid,
                        X: 830, Y: 584, AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
                        ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0,
                        SpellId: FineRainAttachStatusSpellId)).ToFrameBytes()));
                Assert.Equal("1", Owner.Character.Attributes["FineRainPower"]);
                await AssertOwnerAsync(501, 501, 10_000);
            }
            await PacketHandlers.HandleAsync(Context(PlayerUid), ReadFrame(CreateTransformAttack().ToFrameBytes()));
            Assert.True(LegacyTransformState.TryGetActiveMaxHitpoints(Owner.Character, out var maximum));
            Assert.Equal(3_000U, maximum);
            await AssertOwnerAsync(3_000, 3_000, _withFineRain ? 10_000 : 0);
        }

        public async Task StrikeOwnerAsync() =>
            await PacketHandlers.HandleAsync(Context(TargetPlayerUid),
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: TargetPlayerUid, OpponentUid: PlayerUid,
                    X: 830, Y: 584, AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
                    ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0,
                    SpellId: ThunderSpellId)).ToFrameBytes()));

        public async Task QueryEquipmentAsync() =>
            await PacketHandlers.HandleAsync(Context(PlayerUid), ReadFrame(LegacyRuneStoragePacket.Startup().ToFrameBytes()));

        private async Task<LegacyPlayerStatusPacket> QueryAsync(uint uid)
        {
            var start = _frames[uid].Count;
            await PacketHandlers.HandleAsync(Context(uid), ReadFrame(new LegacyPlayerStatusQueryPacket(uid).ToFrameBytes()));
            var bytes = Assert.Single(_frames[uid].ToArray().Skip(start),
                frame => LegacyPlayerStatusPacket.TryParse(ReadFrame(frame), out _));
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out var status));
            return status;
        }

        public async Task AssertOwnerAsync(uint maximum, long hitpoints, int stamp)
        {
            Assert.Equal(hitpoints, Owner.Character.MinHitPoints);
            Assert.Equal(maximum, (await QueryAsync(PlayerUid)).MaxHP);
            DirectFineRainHpAssertOwnedMaximum(Owner, maximum);
            Assert.Equal(stamp, LegacyFineRainEquipmentAdmission.ForOwner(Owner).StampMilliseconds);
        }

        public void Dispose()
        {
            GC.KeepAlive(_connections);
            _log.Dispose();
        }
    }
}
