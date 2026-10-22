using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(1000, 0, 10421U)]
    [InlineData(1001, 0, 10421U)]
    [InlineData(1000, 7, 9937U)]
    [InlineData(1001, 7, 9937U)]
    public async Task NativeOrdinaryRiseUsesReal15440LateExtraAfterBlessBeforeCallerHalf(
        ushort spellId, byte targetBless, uint expectedDamage)
    {
        using var fixture = new NativeRiseLateDamageFixture(spellId, targetBless);
        await fixture.PrepareQueriesAndRealRiseAsync();
        await fixture.CastOrdinaryAsync(spellId);

        // Native neutral: (13301 + 541 + 7000)/2 =10421.
        // Bless7: (13842 - floor(13842*7/100) +7000)/2 =9937.
        // Moving Rise before Bless would incorrectly yield9692 in the latter rows.
        Assert.Equal(100_000L - expectedDamage, fixture.Peer.Character.MinHitPoints);
        fixture.AssertOrdinaryWire(spellId, expectedDamage);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, LegacyRiseOfTaoismFlag));
        Assert.Equal("7000", fixture.Owner.Character.Attributes["RiseofTaoismExtraMDamage"]);
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(1001)]
    public async Task NativeOrdinaryRiseRemovedFlagKeepsQueriedMagicAttackButDoesNotAddLateExtra(
        ushort spellId)
    {
        using var fixture = new NativeRiseLateDamageFixture(spellId);
        await fixture.PrepareQueriesAndRealRiseAsync();
        // Declared boundary input, not a claimed real expiry/death producer.
        // Native cached13301 remains; stale extra-field7000 alone must not activate Rise.
        fixture.RemoveOnlyRiseFlagWithoutQuery();
        await fixture.CastOrdinaryAsync(spellId);

        Assert.Equal(93_079L, fixture.Peer.Character.MinHitPoints);
        fixture.AssertOrdinaryWire(spellId, 6921U);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, LegacyRiseOfTaoismFlag));
        Assert.Equal("7000", fixture.Owner.Character.Attributes["RiseofTaoismExtraMDamage"]);
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(1001)]
    public async Task NativeOrdinaryRiseReadsCurrentCasterAfterRealRevengeTailCallback(
        ushort spellId)
    {
        using var fixture = new NativeRiseLateDamageFixture(spellId);
        await fixture.PrepareQueriesAndRealRiseAsync();
        await fixture.EarnPeerRevengeTailAsync();
        fixture.RemoveRiseDuringNextTailAnimation = true;
        await fixture.CastOrdinaryAsync(spellId);

        Assert.Equal(1, fixture.TailCallbackCount);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, LegacyRiseOfTaoismFlag));
        Assert.Equal("7000", fixture.Owner.Character.Attributes["RiseofTaoismExtraMDamage"]);
        Assert.Equal(35_000L, fixture.Owner.Character.MinHitPoints); // Genuine20000 counter to55000.
        Assert.Equal("4", fixture.Peer.Character.Attributes["LegacyRevengeTailChange"]);
        Assert.Equal(93_079L, fixture.Peer.Character.MinHitPoints);
        fixture.AssertOrdinaryWire(spellId, 6921U);
        var counter = Assert.Single(ParseSpellAnimations(fixture.PeerFrames), packet => packet.SpellId == 13_030);
        Assert.Equal(20_000U, Assert.Single(counter.Targets).Damage);
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(1001)]
    public async Task NativeOrdinaryRiseDoesNotRunAfterRealMagicDefenderEarlyReturn(
        ushort spellId)
    {
        using var fixture = new NativeRiseLateDamageFixture(spellId);
        await fixture.PrepareQueriesAndRealRiseAsync();
        await fixture.EarnPeerMagicDefenderAsync();
        await fixture.CastOrdinaryAsync(spellId);

        Assert.Equal(100_000L, fixture.Peer.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, LegacyRiseOfTaoismFlag));
        fixture.AssertOrdinaryWire(spellId, 0U, MsgAttackEffect.Imunity);
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(1001)]
    public async Task NativeOrdinaryRiseDoesNotRunAfterPublicWildPhoenixDefenderEarlyReturn(
        ushort spellId)
    {
        using var fixture = new NativeRiseLateDamageFixture(spellId);
        await fixture.PrepareQueriesAndRealRiseAsync();
        await fixture.EarnPeerWildPhoenixAsync();
        await fixture.CastOrdinaryAsync(spellId);

        Assert.Equal(100_000L, fixture.Peer.Character.MinHitPoints);
        Assert.Equal("1", fixture.Peer.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, LegacyRiseOfTaoismFlag));
        fixture.AssertOrdinaryWire(spellId, 0U);
    }

    private sealed class NativeRiseLateDamageFixture : IDisposable
    {
        private readonly WorldSessionStore sessions = new();
        private readonly StringWriter log = new(CultureInfo.InvariantCulture);
        private readonly ManualRuntimeTimeProvider clock = new(
            new DateTimeOffset(2026, 10, 17, 18, 0, 0, TimeSpan.Zero));
        private readonly ConquerTcpConnection ownerConnection;
        private readonly ConquerTcpConnection peerConnection;
        private readonly WorldPacketHandlerContext context;
        private readonly byte targetBless;

        public NativeRiseLateDamageFixture(ushort spellId, byte targetBless = 0)
        {
            this.targetBless = targetBless;
            var ownerFields = Enumerable.Repeat("0", 64).ToArray();
            ownerFields[0] = "111097"; ownerFields[1] = "NativeRiseCasterHelmet";
            ownerFields[18] = "4700"; ownerFields[29] = "200";
            var peerFields = Enumerable.Repeat("0", 64).ToArray();
            peerFields[0] = "111096"; peerFields[1] = "NativeRiseTargetHelmet";
            // Native DBItem.ItemHP is UInt16 per row; aggregate two legal50000 items.
            peerFields[18] = "50000";
            var peerArmorFields = Enumerable.Repeat("0", 64).ToArray();
            peerArmorFields[0] = "130003"; peerArmorFields[1] = "NativeRiseTargetArmor";
            peerArmorFields[18] = "50000";
            // Exact native DBServer/magictype.txt15440 level0: Damage7000, Duration30.
            var riseFields = "15440@@15440@@6@@RiseofTaoism@@0@@0@@0@@4@@0@@0@@7000@@0@@100@@30@@0@@0@@227@@0@@0@@0@@0@@0@@0@@0@@520@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1@@50000@@300@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@155000@@0@@0@@0@@0@@0@@".Split("@@", StringSplitOptions.None);
            var defenderFields = "11200@@11200@@51@@MagicDefender@@0@@0@@1@@4@@0@@0@@0@@0@@100@@15@@4@@0@@126@@0@@950@@157@@0@@0@@900@@0@@0@@0@@0@@0@@0@@30@@0@@0@@0@@0@@0@@3015@@5@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@20000@@800@@0@@0@@0@@0@@".Split("@@", StringSplitOptions.None);
            var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
                [CreateMagicRow(spellId, sort: 1, damage: 541, useMana: 0, useStamina: 0,
                    range: 18, coolDown: 0, coldTime: 0),
                 new MagicType(15_440, 15_440, riseFields[3], riseFields),
                 new MagicType(11_200, 11_200, defenderFields[3], defenderFields),
                 CreateMagicRow(13_030, sort: 6, damage: 20_000, useMana: 0, useStamina: 0,
                    duration: 10, coolDown: 0, coldTime: 0),
                 // Declared mutable charged-flag input through a public cast, not native20370 parser proof.
                 // Damage=GDamage=2 deliberately makes both producer charge sources agree here.
                 CreateMagicRow(20_370, sort: 6, damage: 2, gDamage: 2, damageOnHuman: 18,
                    damage2: 7, useMana: 0, useStamina: 0, duration: 32, coolDown: 0, coldTime: 0)],
                items: [new ItemType(111_097, ownerFields[1], ownerFields),
                    new ItemType(111_096, peerFields[1], peerFields),
                    new ItemType(130_003, peerArmorFields[1], peerArmorFields)]);
            var owner = CreateSession(classCode: 2005, spirit: 100, minHitPoints: 5000, minMana: 500, stamina: 100,
                spellIds: [spellId, 15_440], playerItems: PlayerItemSnapshot.Empty with
                { EquipmentItems = [new PlayerItemSnapshotItem { Uid = 99301, ItemId = 111_097,
                    Position = 1, Durability = 100, MaximumDurability = 100 }] });
            var peer = CreateSession(uid: TargetPlayerUid, name: "NativeRiseLatePeer", classCode: 2005,
                x: 832, y: 584, spirit: 0, minHitPoints: 100_000, minMana: 0, stamina: 100,
                spellIds: [11_200, 13_030, 20_370], playerItems: PlayerItemSnapshot.Empty with
                { EquipmentItems = [new PlayerItemSnapshotItem { Uid = 99302, ItemId = 111_096,
                    Position = 1, Bless = targetBless, Durability = 100, MaximumDurability = 100 },
                    new PlayerItemSnapshotItem { Uid = 99303, ItemId = 130_003,
                    Position = 3, Bless = 0, Durability = 100, MaximumDurability = 100 }] });
            ownerConnection = CreateSendingConnection(OwnerFrames);
            peerConnection = new ConquerTcpConnection(localEndPoint: null, remoteEndPoint: null,
                sendAsync: (frame, _) =>
                {
                    var bytes = frame.ToArray();
                    PeerFrames.Add(bytes);
                    if (RemoveRiseDuringNextTailAnimation &&
                        LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var animation) &&
                        animation.SpellId == 13_030)
                    {
                        RemoveRiseDuringNextTailAnimation = false;
                        TailCallbackCount++;
                        RemoveOnlyRiseFlagWithoutQuery();
                    }
                    return ValueTask.CompletedTask;
                });
            sessions.Set(ownerConnection, owner); sessions.Set(peerConnection, peer);
            context = new WorldPacketHandlerContext(ownerConnection, log, owner, WorldData: world,
                Sessions: sessions, ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13,
                TimeProvider: clock);
        }

        public List<byte[]> OwnerFrames { get; } = [];
        public List<byte[]> PeerFrames { get; } = [];
        public bool RemoveRiseDuringNextTailAnimation { get; set; }
        public int TailCallbackCount { get; private set; }
        public WorldSession Owner => Assert.IsType<WorldSession>(
            sessions.TryGet(ownerConnection, out var owner) ? owner : null);
        public WorldSession Peer => Assert.IsType<WorldSession>(
            sessions.TryGet(peerConnection, out var peer) ? peer : null);

        public async Task PrepareQueriesAndRealRiseAsync()
        {
            await QueryAsync(ownerConnection, OwnerFrames, Owner, 301U, 5000U, 500U);
            await QueryAsync(peerConnection, PeerFrames, Peer, 1U, 100_000U, 0U);
            Assert.False(HasLegacyStatusFlag(Owner.Character.Attributes, LegacyRiseOfTaoismFlag));
            var start = OwnerFrames.Count;
            await CastAsync(ownerConnection, Owner, PlayerUid, 15_440);
            Assert.True(HasLegacyStatusFlag(Owner.Character.Attributes, LegacyRiseOfTaoismFlag));
            Assert.Equal("7000", Owner.Character.Attributes["RiseofTaoismExtraMDamage"]);
            var query = Assert.Single(OwnerFrames.Skip(start), bytes =>
                LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out _));
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(query), out var status));
            Assert.Equal(13_301U, status.MagicAttack);
            Assert.Equal(55_000U, status.MaxHP);
            Assert.Equal(500U, status.MaxMana);
            Assert.Equal(55_000L, Owner.Character.MinHitPoints);
            Assert.Equal(500L, Owner.Character.MinMana);
            Assert.Equal(0U, Assert.Single(Assert.Single(ParseSpellAnimations(OwnerFrames.Skip(start))).Targets).Damage);
            ClearFrames();
        }

        private async Task QueryAsync(ConquerTcpConnection connection, List<byte[]> frames,
            WorldSession actor, uint magicAttack, uint maxHitpoints, uint maxMana)
        {
            var start = frames.Count;
            await PacketHandlers.HandleAsync(context with { Connection = connection, Session = actor },
                ReadFrame(new MsgActionPacket { ObjId = checked((uint)actor.Character.Uid),
                    Type = (MsgActionType)251 }.ToFrameBytes()));
            var bytes = Assert.Single(frames.Skip(start), frame =>
                LegacyPlayerStatusPacket.TryParse(ReadFrame(frame), out _));
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out var status));
            Assert.Equal(checked((uint)actor.Character.Uid), status.Uid);
            Assert.Equal(magicAttack, status.MagicAttack);
            Assert.Equal(maxHitpoints, status.MaxHP);
            Assert.Equal(maxMana, status.MaxMana);
            if (actor.Character.Uid == TargetPlayerUid) Assert.Equal((uint)targetBless, status.ItemBless);
        }

        public void RemoveOnlyRiseFlagWithoutQuery()
        {
            var owner = Owner;
            var attributes = new Dictionary<string, string>(owner.Character.Attributes);
            var words = attributes["StatusFlags"].Split('/',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(word => ulong.Parse(word, NumberStyles.Integer, CultureInfo.InvariantCulture)).ToArray();
            words[LegacyRiseOfTaoismFlag / 32] &= ~(1UL << (LegacyRiseOfTaoismFlag % 32));
            attributes["StatusFlags"] = string.Join('/', words);
            sessions.Set(ownerConnection, owner with { Character = owner.Character with { Attributes = attributes } });
        }

        public async Task EarnPeerRevengeTailAsync()
        {
            await CastAsync(peerConnection, Peer, TargetPlayerUid, 13_030);
            Assert.True(HasLegacyStatusFlag(Peer.Character.Attributes, 0xCA));
            Assert.Equal("5", Peer.Character.Attributes["LegacyRevengeTailChange"]);
            Assert.Single(ParseSpellAnimations(PeerFrames), packet => packet.SpellId == 13_030);
            ClearFrames();
        }

        public async Task EarnPeerMagicDefenderAsync()
        {
            await CastAsync(peerConnection, Peer, TargetPlayerUid, 11_200);
            Assert.True(HasLegacyStatusFlag(Peer.Character.Attributes, 0x80));
            Assert.Single(ParseSpellAnimations(PeerFrames), packet => packet.SpellId == 11_200);
            ClearFrames();
        }

        public async Task EarnPeerWildPhoenixAsync()
        {
            await CastAsync(peerConnection, Peer, TargetPlayerUid, 20_370);
            Assert.True(HasLegacyStatusFlag(Peer.Character.Attributes, LegacyWildPhoenixFlag));
            Assert.Equal("2", Peer.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
            Assert.Single(ParseSpellAnimations(PeerFrames), packet => packet.SpellId == 20_370);
            ClearFrames();
        }

        public Task CastOrdinaryAsync(ushort spellId)
        {
            // Real15440 now earns the shared native AttackStamp. Present the next
            // ordinary input at the exact admitted500ms boundary, without editing it.
            clock.Advance(TimeSpan.FromMilliseconds(500));
            return CastAsync(ownerConnection, Owner, TargetPlayerUid, spellId);
        }

        private Task CastAsync(ConquerTcpConnection connection, WorldSession actor,
            uint targetUid, ushort spellId) => PacketHandlers.HandleAsync(
            context with { Connection = connection, Session = actor },
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: checked((uint)actor.Character.Uid),
                OpponentUid: targetUid, X: 832, Y: 584, AttackType: MsgAttackType.Magic,
                Damage: 0, KilledMonster: 0, ResponseDamage: 0, Effect: MsgAttackEffect.None,
                Unknown: 0, SpellId: spellId)).ToFrameBytes())).AsTask();

        public void AssertOrdinaryWire(ushort spellId, uint damage, MsgAttackEffect effect = MsgAttackEffect.None)
        {
            foreach (var frames in new[] { OwnerFrames, PeerFrames })
            {
                var animation = Assert.Single(ParseSpellAnimations(frames), packet => packet.SpellId == spellId);
                var target = Assert.Single(animation.Targets);
                Assert.Equal(TargetPlayerUid, target.Uid); Assert.Equal(damage, target.Damage);
                Assert.True(target.Hit); Assert.Equal(effect, target.Effect);
            }
        }

        private void ClearFrames() { OwnerFrames.Clear(); PeerFrames.Clear(); }
        public void Dispose() { log.Dispose(); GC.KeepAlive(ownerConnection); GC.KeepAlive(peerConnection); }
    }
}
