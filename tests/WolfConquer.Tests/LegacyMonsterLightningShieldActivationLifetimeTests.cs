using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const string MonsterAzureLifeDeadline = "LegacyLightningShieldActivatedExpiresAtLocal";

    [Theory]
    [InlineData(0U)]
    [InlineData(99U)]
    public async Task MonsterAzureLifeAlreadyActiveDoesNotRefillOrRenewEvenWhenRollPasses(uint roll)
    {
        await using var fixture = new MonsterAzureLifeFixture();
        await fixture.PrimeAsync();
        var first = fixture.Order.Count;
        await fixture.HitAsync();
        Assert.Equal(new uint[] { 10, 10 }, MonsterAzureLifeEntries(fixture.OwnerFramesSince(first)).Select(entry => entry.Time));
        Assert.Equal(new uint[] { 20_000, 19_600 }, MonsterAzureLifeEntries(fixture.OwnerFramesSince(first)).Select(entry => entry.Damage));
        Assert.Equal(fixture.Deadline, fixture.Owner.Character.Attributes[MonsterAzureLifeDeadline]);
        Assert.Equal("true", fixture.Owner.Character.Attributes["LegacyStatusFlagRemoveOnDead.237"]);
        var firstFrames = fixture.Order.ToArray().Skip(first).ToArray();
        var vector = Assert.Single(Enumerable.Range(0, firstFrames.Length), index =>
            firstFrames[index].Recipient == TargetPlayerUid && MonsterAzureLifeVector(firstFrames[index].Bytes, true));
        var peerVector = Assert.Single(Enumerable.Range(0, firstFrames.Length), index =>
            firstFrames[index].Recipient == PlayerUid && MonsterAzureLifeVector(firstFrames[index].Bytes, true));
        var azure = Array.FindIndex(firstFrames, frame => frame.Recipient == TargetPlayerUid &&
            MonsterAzureLifeEntries([frame.Bytes]).Length != 0);
        var attack = Array.FindIndex(firstFrames, frame => MonsterAzureLifePhysical(frame.Bytes));
        Assert.True(vector < peerVector && peerVector < azure && azure < attack);
        Assert.Equal(3_000, fixture.Owner.Character.MinHitPoints);

        fixture.Advance(TimeSpan.FromMilliseconds(4_250));
        fixture.SetRoll(roll);
        var start = fixture.Order.Count;
        await fixture.HitAsync();
        var drain = Assert.Single(MonsterAzureLifeEntries(fixture.OwnerFramesSince(start)));
        Assert.Equal(5U, drain.Time);
        Assert.Equal(19_200U, drain.Damage);
        Assert.Equal(0U, drain.SpellLevel);
        Assert.Equal(fixture.Deadline, fixture.Owner.Character.Attributes[MonsterAzureLifeDeadline]);
        Assert.DoesNotContain(fixture.Order.ToArray().Skip(start), frame => MonsterAzureLifeVector(frame.Bytes, true));
        Assert.Equal("19200", fixture.Owner.Character.Attributes["LightningShieldLeft"]);
        Assert.Equal(3_000, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(0, AssertSinglePhysicalAttack(fixture.OwnerFramesSince(start), TargetPlayerUid).Query.Damage);
    }

    [Theory]
    [InlineData("vector")]
    [InlineData("activation")]
    public async Task MonsterAzureLifeDrainUsesPostPublicationTimeAndCarriesCurrentSameUidState(string boundary)
    {
        var armed = false;
        var callbacks = 0;
        await using var fixture = new MonsterAzureLifeFixture((current, recipient, bytes) =>
        {
            if (!armed || recipient != TargetPlayerUid ||
                !(boundary == "vector" ? MonsterAzureLifeVector(bytes, true) :
                    MonsterAzureLifeEntries([bytes]).Any(entry => entry.Time == 10 && entry.Damage == 20_000)) ||
                Interlocked.CompareExchange(ref callbacks, 1, 0) != 0) return ValueTask.CompletedTask;
            Assert.Equal(current.Deadline, current.Owner.Character.Attributes[MonsterAzureLifeDeadline]);
            current.Advance(TimeSpan.FromMilliseconds(1_250));
            var owner = current.Owner;
            var attributes = new Dictionary<string, string>(owner.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            {
                ["MonsterAzureLifeCurrent"] = "preserved",
            };
            current.BindOwner(owner with { Character = owner.Character with { MinMana = 77, Attributes = attributes } });
            return ValueTask.CompletedTask;
        });
        await fixture.PrimeAsync();
        var start = fixture.Order.Count;
        armed = true;
        await fixture.HitAsync();
        Assert.Equal(1, callbacks);
        Assert.Equal(new uint[] { 10, 8 }, MonsterAzureLifeEntries(fixture.OwnerFramesSince(start)).Select(entry => entry.Time));
        Assert.Equal(new uint[] { 20_000, 19_600 }, MonsterAzureLifeEntries(fixture.OwnerFramesSince(start)).Select(entry => entry.Damage));
        Assert.Equal(fixture.Deadline, fixture.Owner.Character.Attributes[MonsterAzureLifeDeadline]);
        Assert.Equal(77, fixture.Owner.Character.MinMana);
        Assert.Equal("preserved", fixture.Owner.Character.Attributes["MonsterAzureLifeCurrent"]);
        Assert.Equal(3_000, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(0, fixture.ScheduledTimers);
    }

    [Fact]
    public async Task MonsterAzureLifeFirstRegistrationExpiresAtTenSecondsResetBeforeRemoval()
    {
        await using var fixture = new MonsterAzureLifeFixture();
        await fixture.PrimeAsync();
        await fixture.HitAsync();
        Assert.Equal(fixture.Deadline, fixture.Owner.Character.Attributes[MonsterAzureLifeDeadline]);
        await fixture.StartRuntimeAsync();
        await fixture.AdvanceRuntimeAsync(TimeSpan.FromMilliseconds(9_980));
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, LegacyLightningShieldActivatedFlag));
        var start = fixture.Order.Count;
        await fixture.AdvanceRuntimeAsync(TimeSpan.FromMilliseconds(20));
        var owner = fixture.Owner;
        Assert.False(HasLegacyStatusFlag(owner.Character.Attributes, LegacyLightningShieldActivatedFlag));
        Assert.True(HasLegacyStatusFlag(owner.Character.Attributes, LegacyLightningShieldFlag));
        Assert.False(owner.Character.Attributes.ContainsKey(MonsterAzureLifeDeadline));
        Assert.False(owner.Character.Attributes.ContainsKey("LegacyStatusFlagRemoveOnDead.237"));
        Assert.Equal("19600", owner.Character.Attributes["LightningShieldLeft"]);
        Assert.Equal(3_000, owner.Character.MinHitPoints);
        var frames = fixture.OwnerFramesSince(start);
        var reset = Assert.Single(Enumerable.Range(0, frames.Length), index => MonsterAzureLifeEntries([frames[index]])
            .Any(entry => entry.Time == 0 && entry.Damage == 0 && entry.SpellLevel == 0));
        var removed = Assert.Single(Enumerable.Range(0, frames.Length), index => MonsterAzureLifeVector(frames[index], false));
        Assert.True(reset < removed);
        Assert.Single(fixture.Order.ToArray().Skip(start), frame =>
            frame.Recipient == PlayerUid && MonsterAzureLifeVector(frame.Bytes, false));
        Assert.DoesNotContain(ParseMsgUpdates(frames).SelectMany(packet => packet.Updates),
            entry => entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints);
    }

    [Theory]
    [InlineData("remove")]
    [InlineData("rebind")]
    public async Task MonsterAzureLifeInsertionIdentityLossStopsAzureDamageAndOldPeerFanout(string mutation)
    {
        var armed = false;
        var callbacks = 0;
        var boundary = -1;
        WorldSession? replacement = null;
        await using var fixture = new MonsterAzureLifeFixture((current, recipient, bytes) =>
        {
            if (!armed || recipient != TargetPlayerUid || !MonsterAzureLifeVector(bytes, true) ||
                Interlocked.CompareExchange(ref callbacks, 1, 0) != 0) return ValueTask.CompletedTask;
            boundary = current.Order.Count;
            if (mutation == "remove") current.RemoveOwner();
            else replacement = current.RebindOwner();
            return ValueTask.CompletedTask;
        });
        await fixture.PrimeAsync();
        armed = true;
        await fixture.HitAsync();
        Assert.Equal(1, callbacks);
        Assert.True(boundary >= 0);
        var later = fixture.Order.ToArray().Skip(boundary).ToArray();
        Assert.DoesNotContain(later, frame => MonsterAzureLifeVector(frame.Bytes, true));
        Assert.Empty(MonsterAzureLifeEntries(later.Select(frame => frame.Bytes)));
        Assert.DoesNotContain(later, frame => MonsterAzureLifePhysical(frame.Bytes));
        Assert.DoesNotContain(ParseMsgUpdates(later.Select(frame => frame.Bytes)).SelectMany(packet => packet.Updates),
            entry => entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints);
        if (mutation == "remove") Assert.False(fixture.TryOwner(out _));
        else
        {
            Assert.True(fixture.TryOwner(out var current));
            Assert.Same(replacement, current);
            Assert.Equal((long)TargetPlayerUid + 9_000, current!.Character.Uid);
            Assert.Equal(77_777, current.Character.MinHitPoints);
            Assert.Equal(77, current.Character.MinMana);
            Assert.False(current.Character.Attributes.ContainsKey(MonsterAzureLifeDeadline));
            Assert.False(current.Character.Attributes.ContainsKey("LightningShieldLeft"));
        }
    }

    [Theory]
    [InlineData(50L)]
    [InlineData(0L)]
    public async Task MonsterAzureLifeInsertionCallbackUsesCurrentMeterBookHpAndPositionWithoutReacceptingAttack(long hitpoints)
    {
        var armed = false;
        var callbacks = 0;
        await using var fixture = new MonsterAzureLifeFixture((current, recipient, bytes) =>
        {
            if (!armed || recipient != TargetPlayerUid || !MonsterAzureLifeVector(bytes, true) ||
                Interlocked.CompareExchange(ref callbacks, 1, 0) != 0) return ValueTask.CompletedTask;
            var owner = current.Owner;
            var attributes = new Dictionary<string, string>(owner.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            {
                ["LightningShieldLeft"] = "100",
            };
            current.BindOwner(owner with
            {
                Character = owner.Character with { MinHitPoints = hitpoints, MinMana = 77, X = 840, Y = 585, Attributes = attributes },
                PlayerSpells = new PlayerSpellSnapshot([CreatePlayerSpell(15_750, level: 3)]),
            });
            return ValueTask.CompletedTask;
        });
        await fixture.PrimeAsync();
        var start = fixture.Order.Count;
        armed = true;
        await fixture.HitAsync();
        Assert.Equal(1, callbacks);
        var frames = fixture.OwnerFramesSince(start);
        var azure = MonsterAzureLifeEntries(frames);
        Assert.Equal(new uint[] { 10, 10 }, azure.Select(entry => entry.Time));
        Assert.Equal(new uint[] { 100, 0 }, azure.Select(entry => entry.Damage));
        Assert.Equal(new uint[] { 0, 3 }, azure.Select(entry => entry.SpellLevel));
        var attack = AssertSinglePhysicalAttack(frames, TargetPlayerUid).Query;
        Assert.Equal(300, attack.Damage); //400 source physical minus CURRENT100 shield.
        Assert.Equal((ushort)840, attack.X);
        Assert.Equal((ushort)585, attack.Y);
        Assert.Equal(0, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(77, fixture.Owner.Character.MinMana);
        Assert.Equal(840, fixture.Owner.Character.X);
        Assert.Equal(585, fixture.Owner.Character.Y);
        var deaths = ParseDeathAttacks(frames);
        if (hitpoints > 0)
        {
            var death = Assert.Single(deaths).Query;
            Assert.Equal((ushort)840, death.X);
            Assert.Equal((ushort)585, death.Y);
            Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag));
            Assert.Equal("840", fixture.Owner.Character.Attributes["Dead_X"]);
            Assert.Equal("585", fixture.Owner.Character.Attributes["Dead_Y"]);
        }
        else
        {
            Assert.Empty(deaths); //Source CheckForOponnentDead1063 returns for currentHP0.
            Assert.DoesNotContain(ParseMsgUpdates(frames).SelectMany(packet => packet.Updates),
                entry => entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints);
            Assert.False(fixture.Owner.Character.Attributes.ContainsKey("Dead_X"));
        }
    }

    private static LegacyMsgUpdateEntry[] MonsterAzureLifeEntries(IEnumerable<byte[]> frames) =>
        ParseMsgUpdates(frames).Where(packet => packet.Uid == TargetPlayerUid).SelectMany(packet => packet.Updates)
            .Where(entry => entry.Type == LegacyMsgUpdateDataType.AzureShield && entry.Flag == LegacyLightningShieldActivatedFlag).ToArray();

    private static bool MonsterAzureLifeVector(byte[] bytes, bool active) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == TargetPlayerUid &&
        packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag && entry.Values is { } values &&
            IsLegacyStatusFlagSet(values, LegacyLightningShieldActivatedFlag) == active &&
            IsLegacyStatusFlagSet(values, LegacyLightningShieldFlag));

    private static bool MonsterAzureLifePhysical(byte[] bytes) =>
        MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) && packet.Query.Uid == 400_001 &&
        packet.Query.OpponentUid == TargetPlayerUid && packet.Query.AttackType == MsgAttackType.Physical;

    private sealed class MonsterAzureLifeFixture : IAsyncDisposable
    {
        private readonly StringWriter log = new();
        private readonly CancellationTokenSource cancel = new();
        private readonly ManualRuntimeTimeProvider clock;
        private readonly WorldServerFrameHandler handler;
        private readonly WorldSessionStore sessions;
        private readonly Dictionary<uint, ConquerTcpConnection> connections = [];
        private readonly WorldPacketHandlerContext context;
        private readonly Monster family;
        private readonly LegacyMonsterInstance monster;
        private bool started;

        public MonsterAzureLifeFixture(Func<MonsterAzureLifeFixture, uint, byte[], ValueTask>? onSend = null)
        {
            clock = new ManualRuntimeTimeProvider(Origin);
            family = CreateMonsterFamily() with
            {
                Attributes = new Dictionary<string, string>(CreateMonsterFamily().Attributes)
                {
                    ["attack_min"] = "200", ["attack_max"] = "201", ["attack_range"] = "2",
                },
            };
            monster = new LegacyMonsterInstance(400_001, 1_000, 831, 584, 100, 100, 5_000, 5_000,
                9_001, "TrainingMonster", false, 1, 0);
            var fields = Enumerable.Repeat("0", 64).ToArray();
            fields[0] = "111092"; fields[1] = "MonsterAzureLife5000"; fields[18] = "5000";
            var world = CreateMagicCatalogCore(family, [], [],
                [CreateMagicRow(15_750, sort: 6, useMana: 0, useStamina: 0, damage: 100,
                    duration: 30, coolDown: 0, coldTime: 0)],
                items: [new ItemType(111_092, fields[1], fields)]);
            handler = new WorldServerFrameHandler(snapshotLoader: null, handoffService: null, worldData: world,
                runtimeUpdateLoop: new LegacyRuntimeUpdateLoopOptions(true, TimeSpan.FromMilliseconds(20), clock),
                log: TextWriter.Synchronized(log), protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            sessions = GetHandlerSessions(handler);
            foreach (var uid in new[] { TargetPlayerUid, PlayerUid })
            {
                connections[uid] = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                    sendAsync: async (memory, _) =>
                    {
                        var bytes = memory.ToArray();
                        Order.Enqueue((uid, bytes));
                        if (onSend is not null) await onSend(this, uid, bytes);
                    });
                sessions.Set(connections[uid], CreateSession(uid: uid, name: "MonsterAzureLife" + uid, mapId: 1_000,
                    x: uid == TargetPlayerUid ? 830 : 832, y: 584, classCode: 2_005, reborn: 2,
                    strength: 0, spirit: 0, minHitPoints: uid == TargetPlayerUid ? 3_000 : 500,
                    minMana: 1_000, stamina: 100, spellIds: uid == TargetPlayerUid ? [15_750] : [],
                    attributes: new Dictionary<string, string> { ["PkMode"] = "0", ["LegacyLightningShieldRoll"] = "0" },
                    playerItems: uid == TargetPlayerUid ? PlayerItemSnapshot.Empty with
                    {
                        EquipmentItems = [new PlayerItemSnapshotItem { Uid = uid + 100, ItemId = 111_092,
                            Position = 1, Durability = 100, MaximumDurability = 100 }],
                    } : PlayerItemSnapshot.Empty));
            }
            context = handler.CreateMonsterRuntimeContext(new WorldActiveSession(connections[TargetPlayerUid], Owner));
        }

        public DateTimeOffset Origin { get; } = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
        public string Deadline => Origin.DateTime.AddSeconds(10).ToString("O", CultureInfo.InvariantCulture);
        public ConcurrentQueue<(uint Recipient, byte[] Bytes)> Order { get; } = new();
        public int ScheduledTimers => clock.ScheduledTimerCount;
        public WorldSession Owner => AssertHandlerSession(handler, connections[TargetPlayerUid]);
        public bool TryOwner(out WorldSession? session) => sessions.TryGet(connections[TargetPlayerUid], out session);
        public void BindOwner(WorldSession session) => sessions.Set(connections[TargetPlayerUid], session);
        public void RemoveOwner() => Assert.True(sessions.TryRemove(connections[TargetPlayerUid], out _));
        public WorldSession RebindOwner()
        {
            var replacement = CreateSession(uid: TargetPlayerUid + 9_000, minHitPoints: 77_777, minMana: 77, spellIds: []);
            BindOwner(replacement);
            return replacement;
        }
        public void Advance(TimeSpan amount) => clock.Advance(amount);
        public void SetRoll(uint roll)
        {
            var owner = Owner;
            var attributes = new Dictionary<string, string>(owner.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            {
                ["LegacyLightningShieldRoll"] = roll.ToString(CultureInfo.InvariantCulture),
            };
            BindOwner(owner with { Character = owner.Character with { Attributes = attributes } });
        }
        public byte[][] OwnerFramesSince(int start) => Order.ToArray().Skip(start)
            .Where(frame => frame.Recipient == TargetPlayerUid).Select(frame => frame.Bytes).ToArray();
        public async Task PrimeAsync()
        {
            await PacketHandlers.HandleAsync(context with { Session = Owner },
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: TargetPlayerUid, OpponentUid: TargetPlayerUid,
                    X: 830, Y: 584, AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
                    ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0, SpellId: 15_750)).ToFrameBytes()));
            Assert.True(HasLegacyStatusFlag(Owner.Character.Attributes, LegacyLightningShieldFlag));
            Assert.False(HasLegacyStatusFlag(Owner.Character.Attributes, LegacyLightningShieldActivatedFlag));
            Assert.False(Owner.Character.Attributes.ContainsKey(MonsterAzureLifeDeadline));
            var start = Order.Count;
            await PacketHandlers.HandleAsync(context with { Session = Owner },
                ReadFrame(new LegacyPlayerStatusQueryPacket(TargetPlayerUid).ToFrameBytes()));
            var bytes = Assert.Single(OwnerFramesSince(start),
                frame => LegacyPlayerStatusPacket.TryParse(ReadFrame(frame), out _));
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out var status));
            Assert.Equal(5_000U, status.MaxHP);
            Assert.Equal(0U, status.Defence);
            Assert.Equal(0U, status.ItemBless);
            Assert.Equal(0U, status.Dodge);
        }
        public async Task HitAsync() =>
            await WorldPacketHandlers.TrySendLegacyMonsterRetaliationAsync(
                context with { Session = Owner }, Owner, monster, family, CancellationToken.None);
        public async Task StartRuntimeAsync()
        {
            var method = typeof(WorldServerFrameHandler).GetMethod("TryStartLegacyGlobalRuntimeUpdateLoop",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method.Invoke(handler, [cancel.Token]);
            started = true;
            await WaitUntilAsync(() => clock.ScheduledTimerCount >= 1, TimeSpan.FromSeconds(2));
        }
        public async Task AdvanceRuntimeAsync(TimeSpan amount)
        {
            await WaitUntilAsync(() => clock.ScheduledTimerCount >= 1, TimeSpan.FromSeconds(2));
            clock.Advance(amount);
            await WaitUntilAsync(() => clock.ScheduledTimerCount >= 1, TimeSpan.FromSeconds(2));
        }
        public async ValueTask DisposeAsync()
        {
            await cancel.CancelAsync();
            if (started)
            {
                var field = typeof(WorldServerFrameHandler).GetField("_legacyRuntimeGlobalLoopStarted",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(field);
                await WaitUntilAsync(() => clock.ScheduledTimerCount == 0 && Equals(0, field.GetValue(handler)),
                    TimeSpan.FromSeconds(3));
            }
            GC.KeepAlive(connections);
            cancel.Dispose();
            log.Dispose();
        }
    }
}
