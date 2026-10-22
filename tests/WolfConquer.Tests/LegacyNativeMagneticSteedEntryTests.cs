using System.Collections.Concurrent;
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
    public async Task NativeMagneticSteedPublicFreshCacheDemountsBeforeFailedCostEvenWithRawCrop(bool rawCrop)
    {
        var observed = 0;
        using var fixture = new NativeMagneticSteedFixture(rawCrop: rawCrop, stamina: 9,
            onSend: (current, recipient, bytes) =>
            {
                if (recipient == TargetPlayerUid && NativeMagneticSteedDemount(bytes) &&
                    Interlocked.CompareExchange(ref observed, 1, 0) == 0)
                {
                    Assert.False(HasLegacyStatusFlag(current.Target.Character.Attributes, 0x32));
                    Assert.Equal("9", current.Target.Character.Attributes["Stamina"]);
                    Assert.Null(current.Target.MagicCooldowns);
                    Assert.Empty(current.MagneticAnimations());
                }
                return ValueTask.CompletedTask;
            });
        await fixture.PrepareAsync(earnCrop: false);
        Assert.Null(fixture.Target.PhysicalStatusOverlay?.RidingCropItemId);
        await fixture.CastIncomingAsync();
        Assert.Equal(1, observed);
        fixture.AssertOwnerThenPeerDemount();
        Assert.False(HasLegacyStatusFlag(fixture.Target.Character.Attributes, 0x32));
        Assert.False(fixture.Target.Character.Attributes.ContainsKey("LegacyRideExpiresAtUtc"));
        Assert.False(fixture.Target.Character.Attributes.ContainsKey("LegacyRideRemoveOnDead"));
        Assert.False(fixture.Target.Character.Attributes.ContainsKey("LegacyRideStampSeconds"));
        Assert.Equal("9", fixture.Target.Character.Attributes["Stamina"]);
        Assert.Null(fixture.Target.MagicCooldowns);
        Assert.Empty(fixture.TargetStamina());
        Assert.Empty(fixture.MagneticAnimations());
    }

    [Fact]
    public async Task NativeMagneticSteedPublicRejectedFirstInvocationLeavesNaturalSecondAttemptToSpendOnce()
    {
        using var fixture = new NativeMagneticSteedFixture(stamina: 100);
        await fixture.PrepareAsync(earnCrop: false);
        await fixture.CastIncomingAsync();
        fixture.AssertOwnerThenPeerDemount();
        Assert.Equal(new ulong[] { 90 }, fixture.TargetStamina());
        Assert.Equal("90", fixture.Target.Character.Attributes["Stamina"]);
        Assert.Equal(fixture.Now.AddSeconds(1), fixture.Target.MagicCooldowns![20_220]);
        Assert.Single(fixture.MagneticAnimations());
        Assert.True(fixture.OwnerDemountIndex < fixture.FirstTargetStaminaIndex);
        Assert.False(HasLegacyStatusFlag(fixture.Target.Character.Attributes, 0x32));
    }

    [Fact]
    public async Task NativeMagneticSteedPublicEarnedCropCachePermitsRidingThroughBothCanUseAttempts()
    {
        using var fixture = new NativeMagneticSteedFixture(rawCrop: true, stamina: 100);
        await fixture.PrepareAsync(earnCrop: true);
        Assert.Equal(203_003U, fixture.Target.PhysicalStatusOverlay!.RidingCropItemId);
        await fixture.CastIncomingAsync();
        Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, 0x32));
        Assert.Empty(fixture.DemountFrames(TargetPlayerUid));
        Assert.Empty(fixture.DemountFrames(PlayerUid));
        Assert.Equal(new ulong[] { 90, 80 }, fixture.TargetStamina());
        Assert.Equal("80", fixture.Target.Character.Attributes["Stamina"]);
        Assert.Single(fixture.MagneticAnimations());
        Assert.Equal(fixture.Now.AddSeconds(1), fixture.Target.MagicCooldowns![20_220]);
    }

    [Theory]
    [InlineData("missing-book")]
    [InlineData("rate-denial")]
    public async Task NativeMagneticSteedPublicEarlierSelectionDenialDoesNotRemoveRide(string denial)
    {
        using var fixture = new NativeMagneticSteedFixture(stamina: 9);
        await fixture.PrepareAsync(earnCrop: false);
        fixture.DenySelection(denial);
        await fixture.CastIncomingAsync();
        Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, 0x32));
        Assert.Empty(fixture.DemountFrames(TargetPlayerUid));
        Assert.Empty(fixture.DemountFrames(PlayerUid));
        Assert.Empty(fixture.TargetStamina());
        Assert.Empty(fixture.MagneticAnimations());
        Assert.Null(fixture.Target.MagicCooldowns);
        Assert.Equal("9", fixture.Target.Character.Attributes["Stamina"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeMagneticSteedPublicOwnerRetirementDuringDemountCannotResumeOrRetarget(bool freshSameUid)
    {
        var observed = 0;
        WorldSession? replacement = null;
        using var fixture = new NativeMagneticSteedFixture(stamina: 100,
            onSend: (current, recipient, bytes) =>
            {
                if (recipient == TargetPlayerUid && NativeMagneticSteedDemount(bytes) &&
                    Interlocked.CompareExchange(ref observed, 1, 0) == 0)
                    replacement = current.RetireOwner(freshSameUid);
                return ValueTask.CompletedTask;
            });
        await fixture.PrepareAsync(earnCrop: false);
        await fixture.CastIncomingAsync();
        Assert.Equal(1, observed);
        Assert.Single(fixture.DemountFrames(TargetPlayerUid));
        Assert.Empty(fixture.DemountFrames(PlayerUid));
        Assert.Empty(fixture.TargetStamina());
        Assert.Empty(fixture.MagneticAnimations());
        Assert.Equal(100_000, fixture.Incoming.Character.MinHitPoints);
        if (freshSameUid)
        {
            Assert.Same(replacement, fixture.TryTarget);
            Assert.Equal(43_210, fixture.TryTarget!.Character.MinHitPoints);
            Assert.Equal("untouched", fixture.TryTarget.Character.Attributes["SteedReplacement"]);
            Assert.Null(fixture.TryTarget.MagicCooldowns);
        }
        else Assert.Null(fixture.TryTarget);
    }

    private static bool NativeMagneticSteedDemount(byte[] bytes) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) && update.Uid == TargetPlayerUid &&
        update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
            entry.Values is { } values && !IsLegacyStatusFlagSet(values, 0x32) && !IsLegacyStatusFlagSet(values, 0x16B));

    private sealed class NativeMagneticSteedFixture : IDisposable
    {
        private readonly WorldSessionStore sessions = new();
        private readonly Dictionary<uint, ConquerTcpConnection> connections = [];
        private readonly ConcurrentQueue<(uint Recipient, byte[] Bytes)> order = new();
        private readonly StringWriter log = new();
        private readonly ManualRuntimeTimeProvider clock = new(
            new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero));
        private readonly WorldDataCatalog data;

        public NativeMagneticSteedFixture(bool rawCrop = false, byte stamina = 9,
            Func<NativeMagneticSteedFixture, uint, byte[], ValueTask>? onSend = null)
        {
            const string raw = "20220@@20220@@105@@MagneticLight@@1@@0@@0@@4@@0@@0@@15000@@0@@500@@1@@0@@2@@363@@0@@0@@0@@0@@0@@0@@0@@264@@0@@0@@0@@0@@10@@1@@0@@0@@0@@1@@5@@1@@2@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1000@@0@@0@@0@@0@@0@@";
            var fields = raw.Split("@@", StringSplitOptions.None);
            data = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
                [CreateMagicRow(ThunderSpellId, sort: 1, useMana: 0, useStamina: 0, damage: 1, range: 18,
                    coldTime: 0, coolDown: 0), CreateMagicRow(7_001, sort: 32, useMana: 0, useStamina: 0,
                    damage: 0, range: 18, coldTime: 0, coolDown: 0), new MagicType(20_220, 20_220, fields[3], fields)]);
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            {
                connections[uid] = new ConquerTcpConnection(null, null, sendAsync: (frame, _) =>
                {
                    var bytes = frame.ToArray();
                    order.Enqueue((uid, bytes));
                    return onSend?.Invoke(this, uid, bytes) ?? ValueTask.CompletedTask;
                });
                var session = CreateSession(uid: uid, name: "MagneticSteed" + uid,
                    x: uid == PlayerUid ? 830 : 831, y: 584, mapId: 1_015, classCode: 13_005,
                    reborn: 2, strength: 0, spirit: 0, minHitPoints: 100_000, minMana: 500,
                    stamina: uid == TargetPlayerUid ? stamina : 100UL,
                    spellIds: uid == TargetPlayerUid ? [ThunderSpellId, 7_001, 20_220] : [ThunderSpellId],
                    attributes: new Dictionary<string, string>
                    {
                        ["DynamicID"] = "0", ["PkMode"] = "0", ["BattlePower"] = "120",
                        ["MaxHitpoints"] = "100000", ["Reborn"] = "2", ["Level"] = "120", ["Angle"] = "0",
                        ["LegacyCombatGearPassiveRoll.20220"] = "0",
                    }, playerItems: rawCrop && uid == TargetPlayerUid ? PlayerItemSnapshot.Empty with
                    { EquipmentItems = [new PlayerItemSnapshotItem { Uid = 99_180, ItemId = 203_003,
                        Position = 18, Durability = 100, MaximumDurability = 100 }] } : PlayerItemSnapshot.Empty);
                if (uid == TargetPlayerUid)
                    session = session with { CombatGear = CombatGearSnapshot.Empty with
                        { Count = 1, Items = [new CombatGearSnapshotItem(1_304, 1, 0, 1, 0, 0, [[], []], [])] } };
                sessions.Set(connections[uid], session);
            }
        }
        public WorldSession Incoming => Current(PlayerUid);
        public WorldSession Target => Current(TargetPlayerUid);
        public WorldSession? TryTarget => sessions.TryGet(connections[TargetPlayerUid], out var value) ? value : null;
        public DateTimeOffset Now => clock.GetUtcNow();
        private WorldSession Current(uint uid)
        {
            Assert.True(sessions.TryGet(connections[uid], out var value)); Assert.NotNull(value); return value;
        }
        private WorldPacketHandlerContext Context(uint uid) => new(connections[uid],
            TextWriter.Synchronized(log), Current(uid), WorldData: data, Sessions: sessions, TimeProvider: clock,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        private async Task QueryAsync(uint uid)
        {
            var start = order.Count;
            await PacketHandlers.HandleAsync(Context(uid), ReadFrame(new MsgActionPacket
                { ObjId = uid, Type = (MsgActionType)251 }.ToFrameBytes()));
            var frames = order.ToArray().Skip(start).Where(frame => frame.Recipient == uid).Select(frame => frame.Bytes).ToArray();
            var statusBytes = Assert.Single(frames, bytes => LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out _));
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(statusBytes), out var status));
            Assert.Equal(1U, status.MagicAttack);
            var echo = Assert.Single(frames, bytes => MsgActionPacket.TryParse(ReadFrame(bytes), out var action) &&
                action.Type == (MsgActionType)251 && action.ObjId == uid);
            Assert.True(Array.IndexOf(frames, echo) > Array.IndexOf(frames, statusBytes));
            Assert.Equal(100_000, Current(uid).Character.MinHitPoints);
        }
        public async Task PrepareAsync(bool earnCrop)
        {
            await QueryAsync(PlayerUid);
            if (earnCrop) await QueryAsync(TargetPlayerUid);
            await CastAsync(TargetPlayerUid, 7_001, TargetPlayerUid);
            Assert.True(HasLegacyStatusFlag(Target.Character.Attributes, 0x32));
            Assert.Contains(ParseSpellAnimations(order.ToArray().Where(frame => frame.Recipient == TargetPlayerUid)
                .Select(frame => frame.Bytes).ToArray()), animation => animation.SpellId == 7_001);
            Assert.True(Target.Character.Attributes.ContainsKey("LegacyRideExpiresAtUtc"));
            order.Clear(); // Only positively completed public query/mount setup frames.
        }
        private Task CastAsync(uint actor, ushort spell, uint target) => PacketHandlers.HandleAsync(Context(actor),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: actor, OpponentUid: target, X: 831, Y: 584,
                AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0, ResponseDamage: 0,
                Effect: MsgAttackEffect.None, Unknown: 0, SpellId: spell)).ToFrameBytes())).AsTask();
        public async Task CastIncomingAsync() { clock.Advance(TimeSpan.FromSeconds(1)); await CastAsync(PlayerUid, ThunderSpellId, TargetPlayerUid); }
        public void DenySelection(string denial)
        {
            var owner = Target;
            if (denial == "missing-book") sessions.Set(connections[TargetPlayerUid], owner with
                { PlayerSpells = new PlayerSpellSnapshot(owner.PlayerSpells.Spells.Where(book => book.Id != 20_220).ToArray()) });
            else
            {
                Assert.Equal("rate-denial", denial);
                sessions.Set(connections[TargetPlayerUid], owner with { Character = owner.Character with
                    { Attributes = new Dictionary<string, string>(owner.Character.Attributes)
                        { ["LegacyCombatGearPassiveRoll.20220"] = "9999" } } });
            }
        }
        public WorldSession? RetireOwner(bool freshSameUid)
        {
            Assert.True(sessions.TryRemove(connections[TargetPlayerUid], out _));
            if (!freshSameUid) return null;
            var fresh = CreateSession(uid: TargetPlayerUid, name: "FreshSteedOwner", x: 831,
                minHitPoints: 43_210, attributes: new Dictionary<string, string> { ["SteedReplacement"] = "untouched" });
            sessions.Set(connections[TargetPlayerUid], fresh); return fresh;
        }
        public byte[][] DemountFrames(uint recipient) => order.ToArray().Where(frame =>
            frame.Recipient == recipient && NativeMagneticSteedDemount(frame.Bytes)).Select(frame => frame.Bytes).ToArray();
        public LegacySpellAnimationPacket[] MagneticAnimations() => ParseSpellAnimations(order.ToArray()
            .Where(frame => frame.Recipient == TargetPlayerUid).Select(frame => frame.Bytes).ToArray())
            .Where(animation => animation.SpellId == 20_220).ToArray();
        public ulong[] TargetStamina() => order.ToArray().Where(frame => frame.Recipient == TargetPlayerUid)
            .Select(frame => NestedProcStamina(frame.Bytes, TargetPlayerUid)).Where(value => value.HasValue)
            .Select(value => value!.Value).ToArray();
        public int OwnerDemountIndex => Array.FindIndex(order.ToArray(), frame =>
            frame.Recipient == TargetPlayerUid && NativeMagneticSteedDemount(frame.Bytes));
        public int FirstTargetStaminaIndex => Array.FindIndex(order.ToArray(), frame =>
            frame.Recipient == TargetPlayerUid && NestedProcStamina(frame.Bytes, TargetPlayerUid).HasValue);
        public void AssertOwnerThenPeerDemount()
        {
            Assert.Single(DemountFrames(TargetPlayerUid)); Assert.Single(DemountFrames(PlayerUid));
            var peer = Array.FindIndex(order.ToArray(), frame => frame.Recipient == PlayerUid && NativeMagneticSteedDemount(frame.Bytes));
            Assert.True(OwnerDemountIndex >= 0 && peer > OwnerDemountIndex);
        }
        public void Dispose() => log.Dispose();
    }
}
