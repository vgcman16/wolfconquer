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
    public async Task DirectEarlyShieldAzureCallbackPrecedesFreshLethalityAndCapturedDeathPosition(bool heal)
    {
        var armed = false;
        var mutations = 0;
        using var fixture = new DirectEarlyShieldFixture((current, recipient, bytes) =>
        {
            if (!armed || recipient != TargetPlayerUid || !DirectEarlyShieldAzure(bytes, 0) ||
                Interlocked.CompareExchange(ref mutations, 1, 0) != 0) return ValueTask.CompletedTask;
            var target = current.Slot(TargetPlayerUid);
            Assert.Equal(300, target.Character.MinHitPoints);
            Assert.False(HasLegacyStatusFlag(target.Character.Attributes, 5));
            Assert.False(target.Character.Attributes.ContainsKey(LegacyPlayerDeathState.DeadStampAttribute));
            Assert.Equal("0", target.Character.Attributes["LightningShieldLeft"]);
            current.Bind(TargetPlayerUid, target with
            {
                Character = target.Character with { X = 834, MinHitPoints = heal ? 500 : 300 },
            });
            return ValueTask.CompletedTask;
        });
        await fixture.PrimeAsync();
        var start = fixture.Order.Count;
        armed = true;
        await fixture.HitAsync();
        armed = false;
        Assert.Equal(1, mutations);
        var frames = fixture.Order.ToArray().Skip(start).ToArray();
        DirectEarlyShieldAssertPrefix(frames);
        var target = fixture.Slot(TargetPlayerUid);
        Assert.Equal(834, target.Character.X);
        Assert.Equal(heal ? 100 : 0, target.Character.MinHitPoints); // 20001+399-20000 = 400.
        Assert.Equal(!heal, HasLegacyStatusFlag(target.Character.Attributes, 5));
        var hp = ParseMsgUpdates(frames.Where(frame => frame.Recipient == TargetPlayerUid).Select(frame => frame.Bytes))
            .Where(packet => packet.Uid == TargetPlayerUid)
            .SelectMany(packet => packet.Updates)
            .Where(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints).Select(entry => entry.Value).ToArray();
        if (heal)
        {
            Assert.Equal(new ulong[] { 100 }, hp);
            Assert.False(target.Character.Attributes.ContainsKey(LegacyPlayerDeathState.DeadStampAttribute));
            Assert.DoesNotContain(frames, frame => MsgAttackPacket.TryParse(ReadFrame(frame.Bytes), out var attack) &&
                attack.Query.AttackType == MsgAttackType.Death && attack.Query.OpponentUid == TargetPlayerUid);
        }
        else
        {
            Assert.Equal(new ulong[] { 300, 300, 300, 0 }, hp);
            var death = Assert.Single(ParseDeathAttacks(frames.Where(frame => frame.Recipient == PlayerUid)
                .Select(frame => frame.Bytes)));
            Assert.Equal((ushort)834, death.Query.X);
            Assert.Equal((ushort)584, death.Query.Y);
        }
        Assert.Equal(0, fixture.ScheduledTimers);
    }

    [Theory]
    [InlineData("attacker", "remove")]
    [InlineData("attacker", "rebind")]
    [InlineData("target", "remove")]
    [InlineData("target", "rebind")]
    public async Task DirectEarlyShieldDrainAzureIdentityLossStopsBeforeLaterModifiersDeathAndFinalAnimation(
        string participant, string mutation)
    {
        var armed = false;
        var mutations = 0;
        var boundary = -1;
        WorldSession? replacement = null;
        using var fixture = new DirectEarlyShieldFixture((current, recipient, bytes) =>
        {
            if (!armed || recipient != TargetPlayerUid || !DirectEarlyShieldAzure(bytes, 0) ||
                Interlocked.CompareExchange(ref mutations, 1, 0) != 0) return ValueTask.CompletedTask;
            Assert.Equal(300, current.Slot(TargetPlayerUid).Character.MinHitPoints);
            Assert.False(HasLegacyStatusFlag(current.Slot(TargetPlayerUid).Character.Attributes, 5));
            Assert.False(current.Slot(TargetPlayerUid).Character.Attributes.ContainsKey(LegacyPlayerDeathState.DeadStampAttribute));
            boundary = current.Order.Count - 1;
            var slot = participant == "attacker" ? PlayerUid : TargetPlayerUid;
            if (mutation == "remove") current.Remove(slot);
            else replacement = current.Rebind(slot);
            return ValueTask.CompletedTask;
        });
        await fixture.PrimeAsync();
        var start = fixture.Order.Count;
        armed = true;
        await fixture.HitAsync();
        armed = false;
        Assert.Equal(1, mutations);
        Assert.True(boundary >= start);
        var frames = fixture.Order.ToArray().Skip(start).ToArray();
        DirectEarlyShieldAssertPrefix(frames, expectHp: false);
        Assert.Empty(fixture.Order.ToArray().Skip(boundary + 1));
        Assert.Empty(ParseSpellAnimations(frames.Select(frame => frame.Bytes)));
        var slot = participant == "attacker" ? PlayerUid : TargetPlayerUid;
        if (mutation == "remove")
        {
            Assert.False(fixture.TrySlot(slot, out _));
        }
        else
        {
            Assert.True(fixture.TrySlot(slot, out var current));
            Assert.Same(replacement, current);
            Assert.NotNull(current);
            Assert.Equal((long)slot + 9_000, current.Character.Uid);
            Assert.Equal(77_777, current.Character.MinHitPoints);
            Assert.Equal(77, current.Character.MinMana);
            Assert.Equal("33", current.Character.Attributes["Stamina"]);
            Assert.Equal("replacement", current.Character.Attributes["DirectEarlyShieldIdentity"]);
            Assert.False(HasLegacyStatusFlag(current.Character.Attributes, 5));
            Assert.Null(current.EquipmentHitpointsStatus);
            Assert.Null(current.PhysicalStatusOverlay);
        }
        if (participant == "attacker")
        {
            var survivor = fixture.Slot(TargetPlayerUid);
            Assert.Equal(300, survivor.Character.MinHitPoints);
            Assert.False(HasLegacyStatusFlag(survivor.Character.Attributes, 5));
            Assert.False(survivor.Character.Attributes.ContainsKey(LegacyPlayerDeathState.DeadStampAttribute));
        }
        Assert.Equal(0, fixture.ScheduledTimers);
    }

    private static bool DirectEarlyShieldAzure(byte[] bytes, uint left) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == TargetPlayerUid &&
        packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.AzureShield &&
            entry.Flag == LegacyLightningShieldActivatedFlag && entry.Damage == left);

    private static void DirectEarlyShieldAssertPrefix((uint Recipient, byte[] Bytes)[] frames, bool expectHp = true)
    {
        var targetFrames = frames.Where(frame => frame.Recipient == TargetPlayerUid).Select(frame => frame.Bytes).ToArray();
        var azure = Enumerable.Range(0, targetFrames.Length)
            .Where(index => LegacyMsgUpdatePacket.TryParse(ReadFrame(targetFrames[index]), out var packet) &&
                packet.Uid == TargetPlayerUid &&
                packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.AzureShield)).ToArray();
        Assert.Equal(2, azure.Length);
        for (var index = 0; index < 2; index++)
        {
            Assert.Equal((ushort)2176, ReadFrame(targetFrames[azure[index]]).Header.Opcode);
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(targetFrames[azure[index]]), out var packet));
            var entry = Assert.Single(packet.Updates);
            Assert.Equal(LegacyMsgUpdateDataType.AzureShield, entry.Type);
            Assert.Equal((uint)LegacyLightningShieldActivatedFlag, entry.Flag);
            if (index == 0) Assert.Equal(10U, entry.Time); // Drain's remaining-time owner is separate debt.
            Assert.Equal(index == 0 ? 20_000U : 0U, entry.Damage);
            Assert.Equal(0U, entry.SpellLevel);
        }
        var activatedVector = Array.FindIndex(targetFrames, bytes =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == TargetPlayerUid &&
            packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null && IsLegacyStatusFlagSet(entry.Values, LegacyLightningShieldActivatedFlag)));
        Assert.True(activatedVector >= 0 && activatedVector < azure[0]);
        Assert.True(azure[0] < azure[1]);
        var firstHp = Array.FindIndex(targetFrames, bytes =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == TargetPlayerUid &&
            packet.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints));
        if (expectHp) Assert.True(firstHp > azure[1]);
        else Assert.Equal(-1, firstHp);
    }

    private sealed class DirectEarlyShieldFixture : IDisposable
    {
        private readonly WorldSessionStore sessions = new();
        private readonly Dictionary<uint, ConquerTcpConnection> connections = [];
        private readonly StringWriter log = new();
        private readonly ManualRuntimeTimeProvider clock = new(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
        private readonly WorldPacketHandlerContext context;
        private readonly bool hawks;

        public DirectEarlyShieldFixture(Func<DirectEarlyShieldFixture, uint, byte[], ValueTask> onSend, bool hawks = false)
        {
            this.hawks = hawks;
            var casterRow = DirectOverlayItem(111_097, "EarlyShieldCasterHelmet");
            var casterFields = casterRow.Fields.ToArray();
            casterFields[18] = "500";
            casterFields[29] = "20000"; // Real modeled item magic attack; source ushort-safe.
            var targetRow = DirectOverlayItem(111_098, "EarlyShieldTargetHelmet");
            var targetFields = targetRow.Fields.ToArray();
            targetFields[18] = "500";
            var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
            [
                CreateMagicRow(1_000, sort: 1, useMana: 0, useStamina: 0, damage: 399,
                    range: 18, coolDown: 0, coldTime: 0),
                CreateMagicRow(15_750, sort: 6, useMana: 0, useStamina: 0, damage: 100,
                    duration: 30, coolDown: 0, coldTime: 0),
                .. (hawks ? new MagicType[]
                {
                    CreateMagicRow(DirectEarlyHawksSpell, sort: 6, useMana: 0, useStamina: 0, damage: 50,
                        duration: 60, coolDown: 0, coldTime: 0),
                    DirectEarlyHawksUseRuneRow(),
                } : Array.Empty<MagicType>()),
            ], items:
            [
                casterRow with { Fields = casterFields }, targetRow with { Fields = targetFields },
                .. (hawks ? new ItemType[]
                {
                    DirectEarlyHawksWarmItem(111_099, "EarlyHawksWarmHelmet"),
                    DirectEarlyHawksWarmItem(130_099, "EarlyHawksWarmArmor"),
                } : Array.Empty<ItemType>()),
            ], daoqiDictTypes: hawks ? [DirectEarlyHawksJade()] : []);
            foreach (var uid in hawks ? new[] { PlayerUid, TargetPlayerUid, DirectEarlyHawksWarmUid } : new[] { PlayerUid, TargetPlayerUid })
            {
                connections[uid] = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                    sendAsync: async (memory, token) =>
                    {
                        var bytes = memory.ToArray();
                        Order.Enqueue((uid, bytes));
                        await onSend(this, uid, bytes);
                    });
                var session = CreateSession(uid: uid, name: "EarlyShield" + uid,
                    mapId: 1_000, x: uid == PlayerUid ? 830 : uid == TargetPlayerUid ? 832 : 834, y: 584,
                    classCode: hawks && uid == PlayerUid ? 7_005 : 2_005, reborn: 2,
                    strength: 0, spirit: 0, minHitPoints: uid == PlayerUid ? 500 : uid == TargetPlayerUid ? 300 : 100_000,
                    minMana: 1_000, stamina: 100, spellIds: hawks
                        ? uid == PlayerUid ? [1_000, DirectEarlyHawksSpell, DirectEarlyHawksUseRune]
                            : uid == TargetPlayerUid ? [15_750, DirectEarlyHawksSpell] : []
                        : uid == PlayerUid ? [1_000] : [15_750],
                    attributes: new Dictionary<string, string>
                    {
                        ["PkMode"] = "0", ["BattlePower"] = "100", ["LegacyLightningShieldRoll"] = "0",
                    },
                    playerItems: PlayerItemSnapshot.Empty with
                    {
                        EquipmentItems = uid == DirectEarlyHawksWarmUid
                            ? [new PlayerItemSnapshotItem { Uid = uid + 100, ItemId = 111_099, Position = 1,
                                    Durability = 100, MaximumDurability = 100 },
                                new PlayerItemSnapshotItem { Uid = uid + 101, ItemId = 130_099, Position = 3,
                                    Durability = 100, MaximumDurability = 100 }]
                            : [new PlayerItemSnapshotItem { Uid = uid + 100,
                                ItemId = uid == PlayerUid ? 111_097U : 111_098U, Position = 1,
                                Durability = 100, MaximumDurability = 100 }],
                    });
                sessions.Set(connections[uid], session);
            }
            context = new WorldPacketHandlerContext(connections[PlayerUid], TextWriter.Synchronized(log),
                Slot(PlayerUid), WorldData: world, Sessions: sessions,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13, TimeProvider: clock);
        }

        public ConcurrentQueue<(uint Recipient, byte[] Bytes)> Order { get; } = new();
        public int ScheduledTimers => clock.ScheduledTimerCount;
        public bool TrySlot(uint uid, out WorldSession? session) => sessions.TryGet(connections[uid], out session);
        public WorldSession Slot(uint uid)
        {
            Assert.True(TrySlot(uid, out var session));
            return Assert.IsType<WorldSession>(session);
        }
        public void Bind(uint slot, WorldSession session) => sessions.Set(connections[slot], session);
        public void Remove(uint slot) => Assert.True(sessions.TryRemove(connections[slot], out _));
        public WorldSession Rebind(uint slot)
        {
            var replacement = CreateSession(uid: slot + 9_000, name: "EarlyShieldReplacement",
                mapId: 1_000, x: 832, y: 584, classCode: 2_005, strength: 0, spirit: 0,
                minHitPoints: 77_777, minMana: 77, stamina: 33, spellIds: [],
                attributes: new Dictionary<string, string> { ["DirectEarlyShieldIdentity"] = "replacement" });
            Bind(slot, replacement);
            return replacement;
        }
        private WorldPacketHandlerContext Context(uint uid) =>
            context with { Connection = connections[uid], Session = Slot(uid) };
        private async Task CastAsync(uint owner, uint target, ushort spell) =>
            await PacketHandlers.HandleAsync(Context(owner), ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: owner, OpponentUid: target, X: 832, Y: 584, AttackType: MsgAttackType.Magic,
                Damage: 0, KilledMonster: 0, ResponseDamage: 0, Effect: MsgAttackEffect.None,
                Unknown: 0, SpellId: spell)).ToFrameBytes()));
        public Task HitAsync() => CastAsync(PlayerUid, TargetPlayerUid, 1_000);
        public async Task EquipAttackUpJadeAndWarmAsync()
        {
            Assert.True(hawks);
            var caster = Slot(PlayerUid);
            Assert.False(HasLegacyStatusFlag(caster.Character.Attributes, LegacyAttackUpFlag));
            Bind(PlayerUid, caster with
            {
                CombatGear = CombatGearSnapshot.Empty with
                {
                    Count = 1,
                    Items = [new CombatGearSnapshotItem(701, 1, 0, 1, 0, 0, [[], []], [(uint)DirectEarlyHawksJadeId])],
                },
            });
            // A real second public input must reach the native shared500ms boundary.
            // The outer receive may resume afterward and must still read the newly earned AttackUp.
            clock.Advance(TimeSpan.FromMilliseconds(500));
            await CastAsync(PlayerUid, DirectEarlyHawksWarmUid, 1_000);
            Assert.Equal(clock.GetUtcNow().ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                Slot(PlayerUid).Character.Attributes["LegacyAttackStampUtc"]);
        }
        public async Task PrimeAsync()
        {
            await CastAsync(TargetPlayerUid, TargetPlayerUid, 15_750);
            Assert.True(HasLegacyStatusFlag(Slot(TargetPlayerUid).Character.Attributes, LegacyLightningShieldFlag));
            Assert.False(HasLegacyStatusFlag(Slot(TargetPlayerUid).Character.Attributes, LegacyLightningShieldActivatedFlag));
            if (hawks)
            {
                await CastAsync(PlayerUid, PlayerUid, DirectEarlyHawksSpell);
                await CastAsync(TargetPlayerUid, TargetPlayerUid, DirectEarlyHawksSpell);
                foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
                {
                    Assert.True(HasLegacyStatusFlag(Slot(uid).Character.Attributes, LegacyHawksAmbitionFlag));
                    Assert.Equal("50", Slot(uid).Character.Attributes["HawksAmbitionPower"]);
                }
                Assert.False(HasLegacyStatusFlag(Slot(PlayerUid).Character.Attributes, LegacyAttackUpFlag));
            }
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            {
                var start = Order.Count;
                await PacketHandlers.HandleAsync(Context(uid),
                    ReadFrame(new LegacyPlayerStatusQueryPacket(uid).ToFrameBytes()));
                var bytes = Assert.Single(Order.ToArray().Skip(start), frame => frame.Recipient == uid &&
                    LegacyPlayerStatusPacket.TryParse(ReadFrame(frame.Bytes), out _)).Bytes;
                Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out var status));
                Assert.Equal(500U, status.MaxHP);
                Assert.Equal(uid == PlayerUid ? 20_001U : 1U, status.MagicAttack);
                Assert.Equal(0U, status.MagicDefence);
                Assert.Equal(0U, status.ItemBless);
                Assert.Equal(0U, status.Fields[LegacyPlayerStatusPacket.MagicDamageIncreaseIndex]);
                Assert.Equal(0U, status.Fields[LegacyPlayerStatusPacket.MagicDamageDecreaseIndex]);
            }
        }
        public void Dispose() { GC.KeepAlive(connections); log.Dispose(); }
    }
}
