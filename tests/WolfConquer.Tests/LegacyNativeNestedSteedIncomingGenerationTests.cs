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
    public async Task NativeNestedSteedIncomingRetirementDuringOwnerDemountCannotResume(bool freshSameUid)
    {
        var observed = 0;
        var retirementFrameCount = -1;
        WorldSession? replacement = null;
        KeyValuePair<string, string>[]? replacementAttributes = null;
        using var fixture = new NativeIncomingG1Fixture(onSend: (current, recipient, bytes) =>
        {
            if (recipient == TargetPlayerUid && NativeIncomingG1Demount(bytes) &&
                Interlocked.CompareExchange(ref observed, 1, 0) == 0)
            {
                current.AssertSelectedOwnerAtDemount();
                retirementFrameCount = current.FrameCount;
                replacement = current.Retire(PlayerUid, freshSameUid);
                replacementAttributes = replacement?.Character.Attributes.OrderBy(pair => pair.Key,
                    StringComparer.Ordinal).ToArray();
            }
            return ValueTask.CompletedTask;
        });
        await fixture.PrepareAsync();
        await fixture.CastIncomingAsync();
        Assert.Equal(1, observed); // Missing real steed publication is a prerequisite failure here.
        Assert.True(retirementFrameCount > 0);
        if (freshSameUid)
        {
            Assert.NotNull(replacement);
            fixture.AssertFreshUntouched(PlayerUid, replacement, replacementAttributes!);
            fixture.AssertOwnerThenViewDemount(); // VIEW is about the still-live DEFENDER, not B.
        }
        else
        {
            Assert.Null(fixture.TryCurrent(PlayerUid));
            fixture.AssertOwnerOnlyDemount();
        }
        fixture.AssertOnlyDefenderDemountAfter(retirementFrameCount, PlayerUid);
        Assert.Equal("100", fixture.Target.Character.Attributes["Stamina"]);
        Assert.Equal(500, fixture.Target.Character.MinMana);
        Assert.Null(fixture.Target.MagicCooldowns);
        Assert.Empty(fixture.TargetStamina());
        Assert.Empty(fixture.MagneticAnimations());
    }

    [Fact]
    public async Task NativeNestedSteedSameHolderImmutableCopyDuringOwnerDemountStillContinues()
    {
        var observed = 0;
        WorldSession? copied = null;
        using var fixture = new NativeIncomingG1Fixture(onSend: (current, recipient, bytes) =>
        {
            if (recipient == TargetPlayerUid && NativeIncomingG1Demount(bytes) &&
                Interlocked.CompareExchange(ref observed, 1, 0) == 0)
            {
                current.AssertSelectedOwnerAtDemount();
                copied = current.CopyIncomingWithoutRetiringHolder();
            }
            return ValueTask.CompletedTask;
        });
        await fixture.PrepareAsync();
        await fixture.CastIncomingAsync();
        Assert.Equal(1, observed);
        Assert.NotNull(copied);
        Assert.Equal("retained", fixture.Incoming.Character.Attributes["IncomingG1SameHolderCopy"]);
        fixture.AssertOwnerThenViewDemount();
        Assert.Equal(new ulong[] { 90 }, fixture.TargetStamina());
        Assert.Equal("90", fixture.Target.Character.Attributes["Stamina"]);
        Assert.Equal(fixture.Now.AddSeconds(1), fixture.Target.MagicCooldowns![20_220]);
        var animation = Assert.Single(fixture.MagneticAnimations());
        Assert.Equal(TargetPlayerUid, animation.AttackerUid);
        Assert.Equal(PlayerUid, animation.AttackedUid);
        Assert.InRange(animation.Targets.Count, 0, 1);
        Assert.All(animation.Targets, target => Assert.Equal(PlayerUid, target.Uid));
        Assert.True(fixture.OwnerDemountIndex < fixture.FirstTargetStaminaIndex);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeNestedSteedProcOwnerRetirementDuringOwnerDemountCannotResume(bool freshSameUid)
    {
        var observed = 0;
        WorldSession? replacement = null;
        KeyValuePair<string, string>[]? replacementAttributes = null;
        WorldSession? incomingAtRetirement = null;
        using var fixture = new NativeIncomingG1Fixture(onSend: (current, recipient, bytes) =>
        {
            if (recipient == TargetPlayerUid && NativeIncomingG1Demount(bytes) &&
                Interlocked.CompareExchange(ref observed, 1, 0) == 0)
            {
                current.AssertSelectedOwnerAtDemount();
                incomingAtRetirement = current.Incoming;
                replacement = current.Retire(TargetPlayerUid, freshSameUid);
                replacementAttributes = replacement?.Character.Attributes.OrderBy(pair => pair.Key,
                    StringComparer.Ordinal).ToArray();
            }
            return ValueTask.CompletedTask;
        });
        await fixture.PrepareAsync();
        await fixture.CastIncomingAsync();
        Assert.Equal(1, observed);
        Assert.NotNull(incomingAtRetirement);
        Assert.Same(incomingAtRetirement, fixture.Incoming);
        fixture.AssertOwnerOnlyDemount();
        Assert.Empty(fixture.TargetStamina());
        Assert.Empty(fixture.MagneticAnimations());
        if (freshSameUid)
        {
            Assert.NotNull(replacement);
            fixture.AssertFreshUntouched(TargetPlayerUid, replacement, replacementAttributes!);
        }
        else Assert.Null(fixture.TryCurrent(TargetPlayerUid));
    }

    private static bool NativeIncomingG1Demount(byte[] bytes) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) && update.Uid == TargetPlayerUid &&
        update.Updates.Count == 1 && update.Updates.Any(entry =>
            entry.Type == LegacyMsgUpdateDataType.StatusFlag && entry.Values is { } values &&
            !IsLegacyStatusFlagSet(values, 0x32) && !IsLegacyStatusFlagSet(values, 0x16B));

    private sealed class NativeIncomingG1Fixture : IDisposable
    {
        private readonly WorldSessionStore sessions = new();
        private readonly Dictionary<uint, ConquerTcpConnection> connections = [];
        private readonly ConcurrentQueue<(uint Recipient, byte[] Bytes)> order = new();
        private readonly StringWriter log = new();
        private readonly ManualRuntimeTimeProvider clock = new(
            new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero));
        private readonly WorldDataCatalog data;

        public NativeIncomingG1Fixture(Func<NativeIncomingG1Fixture, uint, byte[], ValueTask>? onSend = null)
        {
            const string raw = "20220@@20220@@105@@MagneticLight@@1@@0@@0@@4@@0@@0@@15000@@0@@500@@1@@0@@2@@363@@0@@0@@0@@0@@0@@0@@0@@264@@0@@0@@0@@0@@10@@1@@0@@0@@0@@1@@5@@1@@2@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1000@@0@@0@@0@@0@@0@@";
            var fields = raw.Split("@@", StringSplitOptions.None);
            data = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
                [CreateMagicRow(ThunderSpellId, sort: 1, useMana: 0, useStamina: 0, damage: 1,
                    range: 18, coldTime: 0, coolDown: 0),
                 CreateMagicRow(7_001, sort: 32, useMana: 0, useStamina: 0, damage: 0,
                    range: 18, coldTime: 0, coolDown: 0),
                 new MagicType(20_220, 20_220, fields[3], fields)]);
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            {
                connections[uid] = new ConquerTcpConnection(null, null, sendAsync: (frame, _) =>
                {
                    var bytes = frame.ToArray();
                    order.Enqueue((uid, bytes));
                    return onSend?.Invoke(this, uid, bytes) ?? ValueTask.CompletedTask;
                });
                var session = CreateSession(uid: uid, name: "IncomingG1" + uid,
                    x: uid == PlayerUid ? 830 : 831, y: 584, mapId: 1_015, classCode: 13_005,
                    reborn: 2, strength: 0, spirit: 0, minHitPoints: 100_000, minMana: 500,
                    stamina: 100, spellIds: uid == TargetPlayerUid
                        ? [ThunderSpellId, 7_001, 20_220] : [ThunderSpellId],
                    attributes: new Dictionary<string, string>
                    {
                        ["DynamicID"] = "0", ["PkMode"] = "0", ["BattlePower"] = "120",
                        ["MaxHitpoints"] = "100000", ["Reborn"] = "2", ["Level"] = "120", ["Angle"] = "0",
                        ["LegacyCombatGearPassiveRoll.20220"] = "0",
                        ["LegacyCombatGearMagneticBattlePowerRoll"] = "0",
                    });
                if (uid == TargetPlayerUid)
                    session = session with { CombatGear = CombatGearSnapshot.Empty with
                        { Count = 1, Items = [new CombatGearSnapshotItem(1_304, 1, 0, 1, 0, 0, [[], []], [])] } };
                sessions.Set(connections[uid], session);
            }
        }

        public WorldSession Incoming => Current(PlayerUid);
        public WorldSession Target => Current(TargetPlayerUid);
        public DateTimeOffset Now => clock.GetUtcNow();
        public int FrameCount => order.Count;
        public WorldSession? TryCurrent(uint uid) => sessions.TryGet(connections[uid], out var value) ? value : null;
        private WorldSession Current(uint uid)
        {
            var current = TryCurrent(uid); Assert.NotNull(current); return current;
        }
        private WorldPacketHandlerContext Context(uint uid) => new(connections[uid],
            TextWriter.Synchronized(log), Current(uid), WorldData: data, Sessions: sessions, TimeProvider: clock,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        public async Task PrepareAsync()
        {
            var start = order.Count;
            await PacketHandlers.HandleAsync(Context(PlayerUid), ReadFrame(new MsgActionPacket
                { ObjId = PlayerUid, Type = (MsgActionType)251 }.ToFrameBytes()));
            var frames = order.ToArray().Skip(start).Where(frame => frame.Recipient == PlayerUid)
                .Select(frame => frame.Bytes).ToArray();
            var statusBytes = Assert.Single(frames, bytes => LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out _));
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(statusBytes), out var status));
            Assert.Equal(1U, status.MagicAttack);
            var echo = Assert.Single(frames, bytes => MsgActionPacket.TryParse(ReadFrame(bytes), out var action) &&
                action.Type == (MsgActionType)251 && action.ObjId == PlayerUid);
            Assert.True(Array.IndexOf(frames, statusBytes) < Array.IndexOf(frames, echo));
            Assert.Equal(100_000, Incoming.Character.MinHitPoints);
            Assert.Equal(500, Incoming.Character.MinMana);
            await CastAsync(TargetPlayerUid, 7_001, TargetPlayerUid);
            Assert.True(HasLegacyStatusFlag(Target.Character.Attributes, 0x32));
            Assert.True(Target.Character.Attributes.ContainsKey("LegacyRideExpiresAtUtc"));
            Assert.Contains(ParseSpellAnimations(order.ToArray().Where(frame => frame.Recipient == TargetPlayerUid)
                .Select(frame => frame.Bytes).ToArray()), animation => animation.SpellId == 7_001);
            Assert.Null(Target.PhysicalStatusOverlay?.RidingCropItemId);
            Assert.Null(Target.MagicCooldowns);
            order.Clear(); // Only completed and asserted public setup frames.
        }
        private Task CastAsync(uint actor, ushort spell, uint target) => PacketHandlers.HandleAsync(Context(actor),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: actor, OpponentUid: target, X: 831, Y: 584,
                AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0, ResponseDamage: 0,
                Effect: MsgAttackEffect.None, Unknown: 0, SpellId: spell)).ToFrameBytes())).AsTask();
        public async Task CastIncomingAsync()
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            await CastAsync(PlayerUid, ThunderSpellId, TargetPlayerUid);
        }
        public void AssertSelectedOwnerAtDemount()
        {
            Assert.False(HasLegacyStatusFlag(Target.Character.Attributes, 0x32));
            Assert.False(Target.Character.Attributes.ContainsKey("LegacyRideExpiresAtUtc"));
            Assert.Equal((ushort)20_220, Target.LegacyRandomSpell); // Earned C7 prerequisite, never seeded.
            Assert.Equal("100", Target.Character.Attributes["Stamina"]);
            Assert.Null(Target.MagicCooldowns);
            Assert.Empty(MagneticAnimations());
        }
        public WorldSession? Retire(uint uid, bool freshSameUid)
        {
            Assert.True(sessions.TryRemove(connections[uid], out _));
            if (!freshSameUid) return null;
            var fresh = CreateSession(uid: uid, name: "FreshIncomingG1" + uid,
                x: uid == PlayerUid ? 830 : 831, y: 584, mapId: 1_015, classCode: 13_005,
                reborn: 2, strength: 0, spirit: 0, minHitPoints: 43_210, minMana: 321, stamina: 77,
                spellIds: [ThunderSpellId], attributes: new Dictionary<string, string>
                {
                    ["DynamicID"] = "0", ["PkMode"] = "0", ["BattlePower"] = "120",
                    ["MaxHitpoints"] = "43210", ["IncomingG1Fresh"] = "untouched",
                });
            sessions.Set(connections[uid], fresh);
            return fresh;
        }
        public WorldSession CopyIncomingWithoutRetiringHolder()
        {
            var previous = Incoming;
            var copy = previous with { Character = previous.Character with
                { Attributes = new Dictionary<string, string>(previous.Character.Attributes)
                    { ["IncomingG1SameHolderCopy"] = "retained" } } };
            Assert.NotSame(previous, copy);
            sessions.Set(connections[PlayerUid], copy); // No Remove: ordinary same-GameClient record copy.
            return copy;
        }
        public void AssertFreshUntouched(uint uid, WorldSession expected, KeyValuePair<string, string>[] attributes)
        {
            var actual = TryCurrent(uid); Assert.NotNull(actual); Assert.Same(expected, actual);
            Assert.Equal(43_210, actual.Character.MinHitPoints);
            Assert.Equal(321, actual.Character.MinMana);
            Assert.Equal("77", actual.Character.Attributes["Stamina"]);
            Assert.Equal(expected.Character.X, actual.Character.X);
            Assert.Equal(expected.Character.Y, actual.Character.Y);
            Assert.Equal(attributes, actual.Character.Attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray());
            Assert.Null(actual.MagicCooldowns);
            Assert.Null(actual.PhysicalStatusOverlay);
            Assert.Equal((ushort)0, actual.LegacyRandomSpell);
            Assert.False(HasLegacyStatusFlag(actual.Character.Attributes, 0x32));
            Assert.False(HasLegacyStatusFlag(actual.Character.Attributes, 0x16B));
        }
        public void AssertOnlyDefenderDemountAfter(int frameCount, uint recipient)
        {
            foreach (var frame in order.ToArray().Skip(frameCount).Where(frame => frame.Recipient == recipient))
            {
                // Strict allow-list: excludes own-result HP/MP/stamina/status/cold packets,
                // attack/movement packets, spell animations and every other effect packet.
                Assert.True(NativeIncomingG1Demount(frame.Bytes));
            }
        }
        private byte[][] DemountFrames(uint recipient) => order.ToArray().Where(frame =>
            frame.Recipient == recipient && NativeIncomingG1Demount(frame.Bytes)).Select(frame => frame.Bytes).ToArray();
        public void AssertOwnerThenViewDemount()
        {
            var owner = Assert.Single(DemountFrames(TargetPlayerUid));
            var view = Assert.Single(DemountFrames(PlayerUid));
            Assert.Equal(owner, view);
            var peerIndex = Array.FindIndex(order.ToArray(), frame =>
                frame.Recipient == PlayerUid && NativeIncomingG1Demount(frame.Bytes));
            Assert.True(OwnerDemountIndex >= 0 && peerIndex > OwnerDemountIndex);
        }
        public void AssertOwnerOnlyDemount()
        {
            Assert.Single(DemountFrames(TargetPlayerUid)); Assert.Empty(DemountFrames(PlayerUid));
        }
        public LegacySpellAnimationPacket[] MagneticAnimations() => ParseSpellAnimations(order.ToArray()
            .Where(frame => frame.Recipient == TargetPlayerUid).Select(frame => frame.Bytes).ToArray())
            .Where(animation => animation.SpellId == 20_220).ToArray();
        public ulong[] TargetStamina() => order.ToArray().Where(frame => frame.Recipient == TargetPlayerUid)
            .Select(frame => NestedProcStamina(frame.Bytes, TargetPlayerUid)).Where(value => value.HasValue)
            .Select(value => value!.Value).ToArray();
        public int OwnerDemountIndex => Array.FindIndex(order.ToArray(), frame =>
            frame.Recipient == TargetPlayerUid && NativeIncomingG1Demount(frame.Bytes));
        public int FirstTargetStaminaIndex => Array.FindIndex(order.ToArray(), frame =>
            frame.Recipient == TargetPlayerUid && NestedProcStamina(frame.Bytes, TargetPlayerUid).HasValue);
        public void Dispose() => log.Dispose();
    }
}
