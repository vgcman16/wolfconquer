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
    private const string DirectAzureLifeDeadline = "LegacyLightningShieldActivatedExpiresAtLocal";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectAzureLifeRefillDoesNotExtendFirstTimerAndExpiryIsInclusiveResetThenVector(bool refill)
    {
        var resets = 0;
        var expectedLeft = refill ? 19_600U : 19_200U;
        await using var fixture = new DirectAzureLifeFixture((current, recipient, bytes) =>
        {
            if (recipient != TargetPlayerUid || !DirectAzureLifeReset(bytes) ||
                Interlocked.CompareExchange(ref resets, 1, 0) != 0) return ValueTask.CompletedTask;
            Assert.True(HasLegacyStatusFlag(current.Target.Character.Attributes, LegacyLightningShieldActivatedFlag));
            Assert.Equal(expectedLeft.ToString(CultureInfo.InvariantCulture),
                current.Target.Character.Attributes["LightningShieldLeft"]);
            Assert.Equal(current.Origin.DateTime.AddSeconds(10).ToString("O"), current.Target.Character.Attributes[DirectAzureLifeDeadline]);
            current.BindTarget(current.Target with
            {
                Character = current.Target.Character with { MinHitPoints = 1_666, MinMana = 77 },
            });
            return ValueTask.CompletedTask;
        });
        await fixture.PrimeAsync();
        Assert.False(fixture.Target.Character.Attributes.ContainsKey(DirectAzureLifeDeadline));
        await fixture.HitAsync();
        var deadline = fixture.Origin.DateTime.AddSeconds(10).ToString("O");
        Assert.Equal(deadline, fixture.Target.Character.Attributes[DirectAzureLifeDeadline]);
        Assert.Equal(3_000, fixture.Target.Character.MinHitPoints);

        fixture.Advance(TimeSpan.FromMilliseconds(4_250));
        fixture.SetRoll(refill ? 0U : 99U);
        var hitStart = fixture.Order.Count;
        await fixture.HitAsync();
        var azure = DirectAzureLifeEntries(fixture.OwnerFramesSince(hitStart));
        Assert.Equal(refill ? new uint[] { 10, 5 } : new uint[] { 5 }, azure.Select(entry => entry.Time));
        Assert.Equal(refill ? new uint[] { 20_000, 19_600 } : new uint[] { 19_200 }, azure.Select(entry => entry.Damage));
        Assert.Equal(deadline, fixture.Target.Character.Attributes[DirectAzureLifeDeadline]);
        Assert.DoesNotContain(fixture.OwnerFramesSince(hitStart), bytes => DirectAzureLifeVector(bytes, active: true));

        await fixture.StartRuntimeAsync();
        await fixture.AdvanceRuntimeAsync(TimeSpan.FromMilliseconds(5_730)); //9.980s: not due.
        Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, LegacyLightningShieldActivatedFlag));
        Assert.Equal(0, resets);
        var expiryStart = fixture.Order.Count;
        await fixture.AdvanceRuntimeAsync(TimeSpan.FromMilliseconds(20)); //Exactly10.000s: source >=.
        Assert.Equal(1, resets);
        Assert.False(HasLegacyStatusFlag(fixture.Target.Character.Attributes, LegacyLightningShieldActivatedFlag));
        Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, LegacyLightningShieldFlag));
        Assert.False(fixture.Target.Character.Attributes.ContainsKey(DirectAzureLifeDeadline));
        Assert.False(fixture.Target.Character.Attributes.ContainsKey("LegacyStatusFlagRemoveOnDead.237"));
        Assert.Equal(expectedLeft.ToString(CultureInfo.InvariantCulture), fixture.Target.Character.Attributes["LightningShieldLeft"]);
        Assert.Equal(1_666, fixture.Target.Character.MinHitPoints);
        Assert.Equal(77, fixture.Target.Character.MinMana);

        var ownerFrames = fixture.OwnerFramesSince(expiryStart);
        var resetIndex = Assert.Single(Enumerable.Range(0, ownerFrames.Length), index => DirectAzureLifeReset(ownerFrames[index]));
        var vectorIndex = Assert.Single(Enumerable.Range(0, ownerFrames.Length),
            index => DirectAzureLifeVector(ownerFrames[index], active: false));
        Assert.True(resetIndex < vectorIndex);
        Assert.Single(fixture.Order.ToArray().Skip(expiryStart), frame => frame.Recipient == PlayerUid &&
            DirectAzureLifeVector(frame.Bytes, active: false));
        Assert.DoesNotContain(fixture.Order.ToArray().Skip(expiryStart), frame => frame.Recipient == PlayerUid &&
            DirectAzureLifeReset(frame.Bytes));
        Assert.DoesNotContain(ParseMsgUpdates(ownerFrames).SelectMany(packet => packet.Updates),
            entry => entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints);
        Assert.DoesNotContain(ownerFrames, bytes => LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _));
    }

    [Fact]
    public async Task DirectAzureLifeDrainReadsWallTimeAfterTheActivationVectorCallback()
    {
        var armed = false;
        var callbacks = 0;
        await using var fixture = new DirectAzureLifeFixture((current, recipient, bytes) =>
        {
            if (!armed || recipient != TargetPlayerUid || !DirectAzureLifeVector(bytes, active: true) ||
                Interlocked.CompareExchange(ref callbacks, 1, 0) != 0) return ValueTask.CompletedTask;
            Assert.Equal(current.Origin.DateTime.AddSeconds(10).ToString("O"), current.Target.Character.Attributes[DirectAzureLifeDeadline]);
            Assert.Equal("20000", current.Target.Character.Attributes["LightningShieldLeft"]);
            current.Advance(TimeSpan.FromMilliseconds(1_250));
            return ValueTask.CompletedTask;
        });
        await fixture.PrimeAsync();
        var start = fixture.Order.Count;
        armed = true;
        await fixture.HitAsync();
        armed = false;
        Assert.Equal(1, callbacks);
        var azure = DirectAzureLifeEntries(fixture.OwnerFramesSince(start));
        Assert.Equal(new uint[] { 10, 8 }, azure.Select(entry => entry.Time));
        Assert.Equal(new uint[] { 20_000, 19_600 }, azure.Select(entry => entry.Damage));
        Assert.Equal(fixture.Origin.DateTime.AddSeconds(10).ToString("O"), fixture.Target.Character.Attributes[DirectAzureLifeDeadline]);
        Assert.Equal(3_000, fixture.Target.Character.MinHitPoints);
        Assert.Equal(0, fixture.ScheduledTimers);
    }

    [Theory]
    [InlineData("remove")]
    [InlineData("rebind")]
    public async Task DirectAzureLifeExpiryOwnerIdentityLossAtResetStopsRemovalAndVectorFanout(string mutation)
    {
        var boundary = -1;
        var callbacks = 0;
        WorldSession? replacement = null;
        await using var fixture = new DirectAzureLifeFixture((current, recipient, bytes) =>
        {
            if (recipient != TargetPlayerUid || !DirectAzureLifeReset(bytes) ||
                Interlocked.CompareExchange(ref callbacks, 1, 0) != 0) return ValueTask.CompletedTask;
            Assert.True(HasLegacyStatusFlag(current.Target.Character.Attributes, LegacyLightningShieldActivatedFlag));
            Assert.Equal("19600", current.Target.Character.Attributes["LightningShieldLeft"]);
            boundary = current.Order.Count - 1;
            if (mutation == "remove") current.RemoveTarget();
            else replacement = current.RebindTarget();
            return ValueTask.CompletedTask;
        });
        await fixture.PrimeAsync();
        await fixture.HitAsync();
        Assert.Equal(fixture.Origin.DateTime.AddSeconds(10).ToString("O"), fixture.Target.Character.Attributes[DirectAzureLifeDeadline]);
        await fixture.StartRuntimeAsync();
        await fixture.AdvanceRuntimeAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, callbacks);
        Assert.True(boundary >= 0);
        var later = fixture.Order.ToArray().Skip(boundary + 1).ToArray();
        Assert.DoesNotContain(later, frame => DirectAzureLifeVector(frame.Bytes, active: false));
        Assert.DoesNotContain(later, frame => DirectAzureLifeReset(frame.Bytes));
        if (mutation == "remove") Assert.False(fixture.TryTarget(out _));
        else
        {
            Assert.True(fixture.TryTarget(out var current));
            Assert.Same(replacement, current);
            Assert.NotNull(current);
            Assert.Equal((long)TargetPlayerUid + 9_000, current.Character.Uid);
            Assert.Equal(77_777, current.Character.MinHitPoints);
            Assert.Equal(77, current.Character.MinMana);
            Assert.Equal("33", current.Character.Attributes["Stamina"]);
            Assert.Equal("replacement", current.Character.Attributes["DirectAzureLifeIdentity"]);
            Assert.False(current.Character.Attributes.ContainsKey(DirectAzureLifeDeadline));
            Assert.Null(current.EquipmentHitpointsStatus);
            Assert.Null(current.PhysicalStatusOverlay);
        }
    }

    [Fact]
    public async Task DirectAzureLifeRealToggleRetiresTimerWithoutASecondLaterExpiryReset()
    {
        await using var fixture = new DirectAzureLifeFixture();
        await fixture.PrimeAsync();
        await fixture.HitAsync();
        Assert.Equal(fixture.Origin.DateTime.AddSeconds(10).ToString("O"), fixture.Target.Character.Attributes[DirectAzureLifeDeadline]);
        var toggleStart = fixture.Order.Count;
        await fixture.ToggleAsync();
        Assert.False(HasLegacyStatusFlag(fixture.Target.Character.Attributes, LegacyLightningShieldActivatedFlag));
        Assert.False(HasLegacyStatusFlag(fixture.Target.Character.Attributes, LegacyLightningShieldFlag));
        Assert.False(fixture.Target.Character.Attributes.ContainsKey(DirectAzureLifeDeadline));
        Assert.Single(DirectAzureLifeEntries(fixture.OwnerFramesSince(toggleStart)), entry => entry.Time == 0 && entry.Damage == 0);
        var expiryStart = fixture.Order.Count;
        await fixture.StartRuntimeAsync();
        await fixture.AdvanceRuntimeAsync(TimeSpan.FromSeconds(10));
        Assert.Empty(DirectAzureLifeEntries(fixture.OwnerFramesSince(expiryStart)));
    }

    [Fact]
    public async Task DirectAzureLifeDbReplacementCannotReuseTheOldEarlyRegistrationTimer()
    {
        await using var fixture = new DirectAzureLifeFixture(jade: true);
        await fixture.PrimeAsync();
        await fixture.HitAsync();
        Assert.Equal(fixture.Origin.DateTime.AddSeconds(10).ToString("O"), fixture.Target.Character.Attributes[DirectAzureLifeDeadline]);
        fixture.Advance(TimeSpan.FromSeconds(4));
        var replacementStart = fixture.Order.Count;
        await fixture.ReplaceThroughUseRuneAsync();
        Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, LegacyLightningShieldActivatedFlag));
        Assert.False(fixture.Target.Character.Attributes.ContainsKey(DirectAzureLifeDeadline));
        Assert.Equal("1", fixture.Target.Character.Attributes["DirectAzureLifeDbPower"]);
        Assert.Contains(ParseSpellAnimations(fixture.Order.ToArray().Skip(replacementStart).Select(frame => frame.Bytes)),
            packet => packet.AttackerUid == TargetPlayerUid && packet.SpellId == 21_730);
        Assert.Equal(100, fixture.Player.Character.MinHitPoints);
        var oldExpiryStart = fixture.Order.Count;
        await fixture.StartRuntimeAsync();
        await fixture.AdvanceRuntimeAsync(TimeSpan.FromSeconds(6));
        Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, LegacyLightningShieldActivatedFlag));
        Assert.Equal("19600", fixture.Target.Character.Attributes["LightningShieldLeft"]);
        Assert.Empty(DirectAzureLifeEntries(fixture.OwnerFramesSince(oldExpiryStart)));
        // Synthetic DB registration duration11 began atW4, so W10 cannot use the old W0 timer.
        // Its own W15 expiry remains an explicitly held generic DB timer owner.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectAzureLifeRemovedVectorCallbackRearmStopsOldPeerFanout(bool dbReplacement)
    {
        var armed = false;
        var callbacks = 0;
        var callbackEnd = -1;
        await using var fixture = new DirectAzureLifeFixture(async (current, recipient, bytes) =>
        {
            if (!armed || recipient != TargetPlayerUid || !DirectAzureLifeVector(bytes, active: false) ||
                Interlocked.CompareExchange(ref callbacks, 1, 0) != 0) return;
            // Removal already committed, unlike the earlier Azure-reset boundary.
            Assert.False(HasLegacyStatusFlag(current.Target.Character.Attributes, LegacyLightningShieldActivatedFlag));
            Assert.False(current.Target.Character.Attributes.ContainsKey(DirectAzureLifeDeadline));
            if (dbReplacement) await current.ReplaceThroughUseRuneAsync();
            else await current.HitAsync();
            Assert.True(HasLegacyStatusFlag(current.Target.Character.Attributes, LegacyLightningShieldActivatedFlag));
            if (dbReplacement)
            {
                Assert.False(current.Target.Character.Attributes.ContainsKey(DirectAzureLifeDeadline));
                Assert.Equal("1", current.Target.Character.Attributes["DirectAzureLifeDbPower"]);
            }
            else
            {
                Assert.Equal(current.Origin.DateTime.AddSeconds(20).ToString("O"),
                    current.Target.Character.Attributes[DirectAzureLifeDeadline]);
            }
            callbackEnd = current.Order.Count;
        }, jade: dbReplacement);
        await fixture.PrimeAsync();
        await fixture.HitAsync();
        Assert.Equal(fixture.Origin.DateTime.AddSeconds(10).ToString("O"),
            fixture.Target.Character.Attributes[DirectAzureLifeDeadline]);
        var expiryStart = fixture.Order.Count;
        armed = true;
        await fixture.StartRuntimeAsync();
        await fixture.AdvanceRuntimeAsync(TimeSpan.FromSeconds(10));
        armed = false;
        Assert.Equal(1, callbacks);
        Assert.True(callbackEnd > expiryStart);
        Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, LegacyLightningShieldActivatedFlag));
        Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, LegacyLightningShieldFlag));
        Assert.Equal("19600", fixture.Target.Character.Attributes["LightningShieldLeft"]);
        Assert.Equal(3_000, fixture.Target.Character.MinHitPoints);
        Assert.Equal(dbReplacement ? 100 : 500, fixture.Player.Character.MinHitPoints);
        var expiryFrames = fixture.Order.ToArray().Skip(expiryStart).ToArray();
        Assert.Single(expiryFrames, frame => frame.Recipient == TargetPlayerUid && DirectAzureLifeReset(frame.Bytes));
        Assert.Contains(expiryFrames, frame => frame.Recipient == PlayerUid && DirectAzureLifeVector(frame.Bytes, active: true));
        // DB's legitimate pre-add FlashingName vector may still lack237. Only
        // prohibit OLD removed-state fanout AFTER the real public rearm completed.
        Assert.DoesNotContain(fixture.Order.ToArray().Skip(callbackEnd), frame =>
            frame.Recipient == PlayerUid && DirectAzureLifeVector(frame.Bytes, active: false));
        if (dbReplacement)
            Assert.False(fixture.Target.Character.Attributes.ContainsKey(DirectAzureLifeDeadline));
        else
            Assert.Equal(fixture.Origin.DateTime.AddSeconds(20).ToString("O"),
                fixture.Target.Character.Attributes[DirectAzureLifeDeadline]);
    }

    private static LegacyMsgUpdateEntry[] DirectAzureLifeEntries(IEnumerable<byte[]> frames) =>
        ParseMsgUpdates(frames).Where(packet => packet.Uid == TargetPlayerUid).SelectMany(packet => packet.Updates)
            .Where(entry => entry.Type == LegacyMsgUpdateDataType.AzureShield && entry.Flag == LegacyLightningShieldActivatedFlag).ToArray();

    private static bool DirectAzureLifeReset(byte[] bytes) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == TargetPlayerUid &&
        packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.AzureShield &&
            entry.Flag == LegacyLightningShieldActivatedFlag && entry.Time == 0 && entry.Damage == 0 && entry.SpellLevel == 0);

    private static bool DirectAzureLifeVector(byte[] bytes, bool active) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == TargetPlayerUid &&
        packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag && entry.Values is { } values &&
            IsLegacyStatusFlagSet(values, LegacyLightningShieldActivatedFlag) == active &&
            IsLegacyStatusFlagSet(values, LegacyLightningShieldFlag));

    private sealed class DirectAzureLifeFixture : IAsyncDisposable
    {
        private const int JadeId = 21_730_903;
        private readonly Dictionary<uint, ConquerTcpConnection> connections = [];
        private readonly StringWriter log = new();
        private readonly CancellationTokenSource cancel = new();
        private readonly ManualRuntimeTimeProvider clock;
        private readonly WorldServerFrameHandler handler;
        private readonly WorldSessionStore sessions;
        private readonly WorldPacketHandlerContext context;
        private bool started;

        public DirectAzureLifeFixture(Func<DirectAzureLifeFixture, uint, byte[], ValueTask>? onSend = null,
            bool jade = false, DateTimeOffset? origin = null, int jadeStatus = 237)
        {
            Origin = origin ?? new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
            clock = new ManualRuntimeTimeProvider(Origin);
            ItemType Item(uint id, string name, string hp)
            {
                var row = DirectOverlayItem(id, name);
                var fields = row.Fields.ToArray();
                fields[18] = hp;
                return row with { Fields = fields };
            }
            var rows = new List<MagicType>
            {
                CreateMagicRow(1_000, sort: 1, useMana: 0, useStamina: 0, damage: 799, range: 18, coolDown: 0, coldTime: 0),
                CreateMagicRow(15_750, sort: 6, useMana: 0, useStamina: 0, damage: 100, duration: 30, coolDown: 0, coldTime: 0),
            };
            DaoqiDictType[] jades = [];
            if (jade)
            {
                var row = CreateMagicRow(21_730, sort: 6, useMana: 0, useStamina: 0,
                    damage: 1, duration: 11, gDamage: 100, range: 18, coolDown: 0, coldTime: 0);
                var fields = row.Fields.ToArray();
                fields[3] = "DirectAzureLifeDbPower";
                fields[16] = jadeStatus.ToString(CultureInfo.InvariantCulture);
                rows.Add(row with { Name = fields[3], Fields = fields });
                string[] jadeFields = [JadeId.ToString(CultureInfo.InvariantCulture), "2", "24",
                    "21730000", "0", "0", "600", "0", "0", "0", "10", "0", "0", "10", "701", ""];
                jades = [new DaoqiDictType(JadeId, Type: 2, Level: 24,
                    Values: jadeFields.Skip(3).Take(12).Select(value => int.TryParse(value, out var parsed) ? parsed : 0).ToArray(),
                    SourceFile: "fixture://azure-life-db", LineNumber: 1, RawLine: string.Join("@@", jadeFields), Fields: jadeFields)];
            }
            var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [], rows,
                items: [Item(111_092, "AzureLifeCaster500", "500"), Item(111_093, "AzureLifeTarget5000", "5000")],
                daoqiDictTypes: jades,
                combatGear: jade ? [CreateRootMissingArrowSourceGear() with
                    { Id = 701, GearTypeId = 701, MagicTypeIds = [],
                        SourceFile = "fixture://direct-azure-life/loaded701" }] : []);
            handler = new WorldServerFrameHandler(snapshotLoader: null, handoffService: null, worldData: world,
                runtimeUpdateLoop: new LegacyRuntimeUpdateLoopOptions(true, TimeSpan.FromMilliseconds(20), clock),
                log: TextWriter.Synchronized(log), protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            sessions = GetHandlerSessions(handler);
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            {
                connections[uid] = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                    sendAsync: async (memory, _) =>
                    {
                        var bytes = memory.ToArray();
                        Order.Enqueue((uid, bytes));
                        if (onSend is not null) await onSend(this, uid, bytes);
                    });
                var session = CreateSession(uid: uid, name: "AzureLife" + uid, mapId: 1_000,
                    x: uid == PlayerUid ? 830 : 832, y: 584, classCode: jade && uid == TargetPlayerUid ? 7_005 : 2_005,
                    reborn: 2, strength: 0, spirit: 0, minHitPoints: uid == PlayerUid ? 500 : 3_000,
                    minMana: 1_000, stamina: 100, spellIds: uid == PlayerUid ? [1_000]
                        : jade ? [15_750, 1_000, 21_730] : [15_750],
                    attributes: new Dictionary<string, string>
                    {
                        ["PkMode"] = "0", ["BattlePower"] = "100", ["XPCount"] = "0", ["LegacyLightningShieldRoll"] = "0",
                    },
                    playerItems: PlayerItemSnapshot.Empty with
                    {
                        EquipmentItems = [new PlayerItemSnapshotItem { Uid = uid + 100,
                            ItemId = uid == PlayerUid ? 111_092U : 111_093U, Position = 1,
                            Durability = 100, MaximumDurability = 100 }],
                    });
                sessions.Set(connections[uid], session);
            }
            context = new WorldPacketHandlerContext(connections[PlayerUid], TextWriter.Synchronized(log), Player,
                WorldData: world, Sessions: sessions, TimeProvider: clock, ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        }

        public DateTimeOffset Origin { get; }
        public ConcurrentQueue<(uint Recipient, byte[] Bytes)> Order { get; } = new();
        public int ScheduledTimers => clock.ScheduledTimerCount;
        public WorldSession Target => Current(TargetPlayerUid);
        public WorldSession Player => Current(PlayerUid);
        private WorldSession Current(uint uid) => AssertHandlerSession(handler, connections[uid]);
        public bool TryTarget(out WorldSession? session) => sessions.TryGet(connections[TargetPlayerUid], out session);
        public void BindTarget(WorldSession session) => sessions.Set(connections[TargetPlayerUid], session);
        public void RemoveTarget() => Assert.True(sessions.TryRemove(connections[TargetPlayerUid], out _));
        public WorldSession RebindTarget()
        {
            var replacement = CreateSession(uid: TargetPlayerUid + 9_000, name: "AzureLifeReplacement", mapId: 1_000,
                x: 832, y: 584, classCode: 2_005, strength: 0, spirit: 0, minHitPoints: 77_777, minMana: 77, stamina: 33,
                spellIds: [], attributes: new Dictionary<string, string> { ["DirectAzureLifeIdentity"] = "replacement" });
            BindTarget(replacement);
            return replacement;
        }
        public byte[][] OwnerFramesSince(int start) => Order.ToArray().Skip(start)
            .Where(frame => frame.Recipient == TargetPlayerUid).Select(frame => frame.Bytes).ToArray();
        public void Advance(TimeSpan amount) => clock.Advance(amount);
        public void SetRoll(uint roll)
        {
            var target = Target;
            var attributes = new Dictionary<string, string>(target.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            {
                ["LegacyLightningShieldRoll"] = roll.ToString(CultureInfo.InvariantCulture),
            };
            BindTarget(target with { Character = target.Character with { Attributes = attributes } });
        }
        private async Task CastAsync(uint caster, uint target, ushort spell) =>
            await PacketHandlers.HandleAsync(context with { Connection = connections[caster], Session = Current(caster) },
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: caster, OpponentUid: target, X: 832, Y: 584,
                    AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0, ResponseDamage: 0,
                    Effect: MsgAttackEffect.None, Unknown: 0, SpellId: spell)).ToFrameBytes()));
        public Task HitAsync() => CastAsync(PlayerUid, TargetPlayerUid, 1_000);
        public Task ToggleAsync() => CastAsync(TargetPlayerUid, TargetPlayerUid, 15_750);
        public async Task PrimeAsync()
        {
            // Earn native CURRENT equipment status before the real toggle/hits; StatusQuery alone does not query equipment.
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
                await PacketHandlers.HandleAsync(context with { Connection = connections[uid], Session = Current(uid) },
                    ReadFrame(new MsgActionPacket { ObjId = uid, Type = (MsgActionType)251 }.ToFrameBytes()));
            Assert.False(Target.Character.Attributes.ContainsKey("LightningShieldLeft"));
            Assert.False(Target.Character.Attributes.ContainsKey(DirectAzureLifeDeadline));
            await ToggleAsync();
            Assert.True(HasLegacyStatusFlag(Target.Character.Attributes, LegacyLightningShieldFlag));
            Assert.False(HasLegacyStatusFlag(Target.Character.Attributes, LegacyLightningShieldActivatedFlag));
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            {
                var start = Order.Count;
                await PacketHandlers.HandleAsync(context with { Connection = connections[uid], Session = Current(uid) },
                    ReadFrame(new LegacyPlayerStatusQueryPacket(uid).ToFrameBytes()));
                var bytes = Assert.Single(Order.ToArray().Skip(start), frame => frame.Recipient == uid &&
                    LegacyPlayerStatusPacket.TryParse(ReadFrame(frame.Bytes), out _)).Bytes;
                Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out var status));
                Assert.Equal(uid == PlayerUid ? 500U : 5_000U, status.MaxHP);
                Assert.Equal(1U, status.MagicAttack);
                Assert.Equal(0U, status.MagicDefence);
                Assert.Equal(0U, status.ItemBless);
            }
        }
        public async Task ReplaceThroughUseRuneAsync()
        {
            BindTarget(Target with
            {
                CombatGear = CombatGearSnapshot.Empty with
                {
                    Count = 1, Items = [new CombatGearSnapshotItem(701, 1, 0, 1, 0, 0, [[], []], [(uint)JadeId])],
                },
            });
            await CastAsync(TargetPlayerUid, PlayerUid, 1_000);
        }
        public async Task StartRuntimeAsync()
        {
            // Exercise the real global expiry owner without an unrelated player
            // XP/stamina ticker mutating a removed/rebound connection's replacement.
            var start = typeof(WorldServerFrameHandler).GetMethod("TryStartLegacyGlobalRuntimeUpdateLoop",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(start);
            start.Invoke(handler, [cancel.Token]);
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
                var global = typeof(WorldServerFrameHandler).GetField("_legacyRuntimeGlobalLoopStarted",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(global);
                await WaitUntilAsync(() => clock.ScheduledTimerCount == 0 && Equals(0, global.GetValue(handler)),
                    TimeSpan.FromSeconds(3));
            }
            GC.KeepAlive(connections);
            cancel.Dispose();
            log.Dispose();
        }
    }
}
