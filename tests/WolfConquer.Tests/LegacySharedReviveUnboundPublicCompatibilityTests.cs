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
    [Theory]
    [InlineData(1_050)]
    [InlineData(1_100)]
    [InlineData(16_610)]
    public async Task UnboundSharedRevivePublicDispatchPreservesCompatibilityReset(int spellId)
    {
        Assert.Contains(spellId, new[] { 1_050, 1_100, 16_610 });
        using var fixture = new UnboundSharedReviveCompatibilityFixture(checked((ushort)spellId));
        await fixture.PrimeAndJoinByPublicPacketsAsync();
        Assert.Equal(300, fixture.Target.Character.MinHitPoints); // Native Revive also accepts ALIVE targets.
        var start = fixture.Order.Count;
        fixture.Arm(start);

        await fixture.CastByPublicPacketAsync();
        await fixture.CallbackCompleted.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Empty(fixture.CallbackErrors);
        Assert.Equal(1, fixture.CallbackCount);
        Assert.True(fixture.CallbackCompleted.IsCompletedSuccessfully);
        Assert.True(fixture.MaximumGlobalIndex >= start);
        var order = fixture.Order.ToArray().Skip(start).ToArray();
        var target = fixture.Frames(TargetPlayerUid, start);
        Assert.Equal(8, target.Length); // Two HP frames, XP, stamina, vector, mesh, map, animation.
        AssertUnboundSharedReviveHpPair(target, TargetPlayerUid, maximum: 500, hitpoints: 500);
        AssertUnboundSharedReviveScalar(target[2], TargetPlayerUid, LegacyMsgUpdateDataType.XPCircle, 0);
        AssertUnboundSharedReviveScalar(target[3], TargetPlayerUid, LegacyMsgUpdateDataType.Stamina, 100);
        AssertUnboundSharedReviveResetVector(target[4], TargetPlayerUid); // ENTIRE18 reset words, null physical owner.
        AssertUnboundSharedReviveScalar(target[5], TargetPlayerUid, LegacyMsgUpdateDataType.Mesh, 1_003);
        Assert.True(LegacyMapStatusPacket.TryParse(ReadFrame(target[6]), out var map));
        Assert.Equal(1_000U, map.Id);
        Assert.True(LegacySpellAnimationPacket.TryParse(ReadFrame(target[7]), out var animation));
        Assert.Equal(checked((ushort)spellId), animation.SpellId);
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal((ushort)830, animation.X);
        Assert.Equal((ushort)584, animation.Y);
        var revived = Assert.Single(animation.Targets);
        Assert.Equal(TargetPlayerUid, revived.Uid);
        Assert.Equal(0U, revived.Damage);
        Assert.True(revived.Hit);

        var maximumAt = SingleUnboundSharedReviveGlobalIndex(order, TargetPlayerUid,
            bytes => IsUnboundSharedReviveFirstMaximum(bytes, TargetPlayerUid));
        Assert.Equal(fixture.MaximumGlobalIndex - start, maximumAt);
        var staminaAt = SingleUnboundSharedReviveGlobalIndex(order, PlayerUid,
            bytes => IsUnboundSharedReviveScalar(bytes, PlayerUid, LegacyMsgUpdateDataType.Stamina, 84));
        var manaAt = SingleUnboundSharedReviveGlobalIndex(order, PlayerUid,
            bytes => IsUnboundSharedReviveScalar(bytes, PlayerUid, LegacyMsgUpdateDataType.Mana, 463));
        Assert.True(staminaAt < manaAt && manaAt < maximumAt); // Real nonzero16/37 resource sends.
        var caster = fixture.Frames(PlayerUid, start);
        if (spellId == 1_050)
        {
            var xpAt = SingleUnboundSharedReviveGlobalIndex(order, PlayerUid,
                bytes => IsUnboundSharedReviveScalar(bytes, PlayerUid, LegacyMsgUpdateDataType.XPList, 0));
            Assert.True(manaAt < xpAt && xpAt < maximumAt);
        }
        else
        {
            Assert.DoesNotContain(caster, bytes =>
                LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
                update.Uid == PlayerUid && update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.XPList));
        }

        var spawnAt = SingleUnboundSharedReviveGlobalIndex(order, PlayerUid, bytes =>
            LegacyPlayerSpawnPacket.TryParse(ReadFrame(bytes), out var spawn) &&
            spawn.GetNumericField(2) == TargetPlayerUid);
        Assert.True(LegacyPlayerSpawnPacket.TryParse(ReadFrame(order[spawnAt].Bytes), out var peerSpawn));
        Assert.Equal(1_003UL, peerSpawn.GetNumericField(1));
        Assert.Equal(500UL, peerSpawn.GetNumericField(21));
        Assert.Equal(500UL, peerSpawn.GetNumericField(23));
        var mapAt = SingleUnboundSharedReviveGlobalIndex(order, TargetPlayerUid,
            bytes => LegacyMapStatusPacket.TryParse(ReadFrame(bytes), out _));
        Assert.True(mapAt < spawnAt); // The revived target is excluded, NOT the caster.
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            var animationAt = SingleUnboundSharedReviveGlobalIndex(order, uid, bytes =>
                LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var spell) &&
                spell.SpellId == checked((ushort)spellId));
            Assert.True(spawnAt < animationAt);
            Assert.True(LegacySpellAnimationPacket.TryParse(ReadFrame(order[animationAt].Bytes), out var spell));
            Assert.Equal(PlayerUid, spell.AttackerUid);
            var hit = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, hit.Uid);
            Assert.Equal(0U, hit.Damage);
            Assert.True(hit.Hit);
        }
        Assert.DoesNotContain(target, bytes =>
            LegacyPlayerSpawnPacket.TryParse(ReadFrame(bytes), out var spawn) &&
            spawn.GetNumericField(2) == TargetPlayerUid);
        Assert.Equal(463, fixture.Caster.Character.MinMana);
        Assert.Equal("84", fixture.Caster.Character.Attributes["Stamina"]);
        Assert.Equal(500, fixture.Target.Character.MinHitPoints);
        Assert.Equal("0", fixture.Target.Character.Attributes["XPCount"]);
        Assert.Equal("100", fixture.Target.Character.Attributes["Stamina"]);
        Assert.Equal("SharedReviveTarget", fixture.Target.Character.Name);
        Assert.Equal(1_000, fixture.Target.Character.MapId);
        Assert.Equal(832, fixture.Target.Character.X);
        Assert.Equal(584, fixture.Target.Character.Y);
        Assert.False(HasLegacyStatusFlag(fixture.Target.Character.Attributes, 5));
        Assert.False(HasLegacyStatusFlag(fixture.Target.Character.Attributes, 10));
        Assert.False(fixture.Target.Character.Attributes.ContainsKey(LegacyPlayerDeathState.DeadStampAttribute));
        Assert.False(fixture.Target.Character.Attributes.ContainsKey(LegacyPlayerDeathState.GhostStampAttribute));
        Assert.False(fixture.Target.Character.Attributes.ContainsKey("StatusFlags"));
        Assert.False(fixture.Target.Character.Attributes.ContainsKey("BitVector"));
        fixture.AssertUnboundOwners();
        fixture.AssertCompatibilityTeamMembership();
        Assert.Equal(1, fixture.VectorCallbackCount);
        Assert.Contains("WorldServer handled legacy revive magic", fixture.LogText);
        Assert.Contains("spellId=" + spellId.ToString(CultureInfo.InvariantCulture), fixture.LogText);
        Assert.Equal(0, fixture.ScheduledTimers);
        GC.KeepAlive(fixture);
    }

    private static readonly string CompatibilityBeforeResetText =
        "64/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/1"; // Raw compatibility input, reset rather than retained native flags.

    private static bool IsUnboundSharedReviveFirstMaximum(byte[] bytes, uint uid) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == uid &&
        packet.Updates.Count == 1 && packet.Updates[0].Type == LegacyMsgUpdateDataType.MaxHitpoints;

    private static void AssertUnboundSharedReviveResetVector(byte[] bytes, uint uid)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(uid, packet.Uid);
        var entry = Assert.Single(packet.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.StatusFlag, entry.Type);
        var values = Assert.IsAssignableFrom<IReadOnlyList<ulong>>(entry.Values);
        Assert.Equal(18, values.Count);
        Assert.Equal(new ulong[18], values); // Entire real TryRevive reset fallback, not a bit6 mask.
    }

    private static int SingleUnboundSharedReviveGlobalIndex(
        IReadOnlyList<(uint Recipient, byte[] Bytes)> frames, uint uid, Func<byte[], bool> predicate) =>
        Assert.Single(Enumerable.Range(0, frames.Count).Where(index =>
            frames[index].Recipient == uid && predicate(frames[index].Bytes)));

    private static bool IsUnboundSharedReviveScalar(byte[] bytes, uint uid,
        LegacyMsgUpdateDataType type, ulong value) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) && update.Uid == uid &&
        update.Updates.Count == 1 && update.Updates[0].Type == type && update.Updates[0].Value == value;

    private static void AssertUnboundSharedReviveScalar(byte[] bytes, uint uid,
        LegacyMsgUpdateDataType type, ulong value)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update));
        Assert.Equal(uid, update.Uid);
        Assert.Equal(new[] { new LegacyMsgUpdateEntry(type, value) }, update.Updates);
    }

    private static void AssertUnboundSharedReviveHpPair(byte[][] frames, uint uid, ulong maximum, ulong hitpoints)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[0]), out var first));
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[1]), out var second));
        Assert.Equal(uid, first.Uid);
        Assert.Equal(uid, second.Uid);
        Assert.Equal(new[] { new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, maximum) }, first.Updates);
        Assert.Equal(new[]
        {
            new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, maximum),
            new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Hitpoints, hitpoints),
        }, second.Updates);
    }

    // Public packet ingress through the EXPLICIT existing unbound compatibility branch.
    // Raw spell/book/equipment/session flags are declared compatibility inputs, not native owners.
    // No physical Player/client, shared9DF, earned flag, socket birth or fake death is constructed.
    private sealed class UnboundSharedReviveCompatibilityFixture : IDisposable
    {
        private readonly WorldSessionStore sessions = new();
        private readonly TeamSessionStateStore teams = new(); // Deliberate compatibility authority.
        private readonly Dictionary<uint, ConquerTcpConnection> connections = [];
        private readonly ManualRuntimeTimeProvider clock = new(
            new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));
        private readonly StringWriter log = new();
        private readonly WorldPacketHandlerContext context;
        private readonly ushort spellId;
        private readonly TaskCompletionSource callbackCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool armed;
        private uint compatibilityTeamUid;
        public UnboundSharedReviveCompatibilityFixture(ushort spellId)
        {
            this.spellId = spellId;
            const uint helmet = 111_098;
            var item = DirectOverlayItem(helmet, "SharedReviveNeutralHpMana500");
            var fields = item.Fields.ToArray();
            fields[18] = "500";
            fields[19] = "500";
            var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
                [CreateMagicRow(spellId, sort: 7, useMana: 37, useStamina: 16, damage: 100,
                    duration: 125, range: 18, coolDown: 0, coldTime: 0)],
                items: [item with { Fields = fields }]);
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            {
                connections[uid] = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                    sendAsync: (memory, _) =>
                    {
                        var bytes = memory.ToArray();
                        Order.Enqueue((uid, bytes));
                        if (!armed || uid != TargetPlayerUid) return ValueTask.CompletedTask;
                        try
                        {
                            AssertUnboundOwners(); // Actual current records and original connections stay null.
                            if (IsUnboundSharedReviveFirstMaximum(bytes, TargetPlayerUid))
                            {
                                CallbackCount++;
                                Assert.Equal(1, CallbackCount);
                                MaximumGlobalIndex = Order.Count - 1;
                                Assert.Equal(500, Target.Character.MinHitPoints);
                                Assert.False(Target.Character.Attributes.ContainsKey("StatusFlags"));
                                Assert.False(Target.Character.Attributes.ContainsKey("BitVector"));
                                AssertCompatibilityTeamMembership();
                                callbackCompleted.TrySetResult();
                            }
                            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
                                update.Uid == TargetPlayerUid &&
                                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag))
                            {
                                VectorCallbackCount++;
                                Assert.Equal(1, VectorCallbackCount);
                                AssertUnboundSharedReviveResetVector(bytes, TargetPlayerUid);
                            }
                            return ValueTask.CompletedTask; // Observation only; no dictionary or Store mutation.
                        }
                        catch (Exception error)
                        {
                            CallbackErrors.Enqueue(error);
                            callbackCompleted.TrySetException(error);
                            throw;
                        }
                    }); // nativeGameClient omitted: constructor default remains null.
                // XPList4 is declared compatibility caster prestate, not an earned/full physical flag producer.
                var attributes = new Dictionary<string, string>
                {
                    ["StatusFlags"] = uid == PlayerUid ? "16/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0" :
                        CompatibilityBeforeResetText,
                    ["XPCount"] = "88", ["OnMyOwnServer"] = "true", ["PkMode"] = "0", ["BattlePower"] = "120",
                };
                sessions.Set(connections[uid], CreateSession(uid: uid,
                    name: uid == PlayerUid ? "SharedReviveCaster" : "SharedReviveTarget",
                    mapId: 1_000, x: uid == PlayerUid ? 830 : 832, y: 584,
                    classCode: 2_005, reborn: 2, strength: 0, spirit: 0, minHitPoints: 300,
                    minMana: 500, stamina: 100, spellIds: [spellId], attributes: attributes,
                    playerItems: PlayerItemSnapshot.Empty with
                    {
                        EquipmentItems = [new PlayerItemSnapshotItem
                        {
                            Uid = uid + 100, ItemId = helmet, Position = 1,
                            Durability = 100, MaximumDurability = 100,
                        }],
                    })); // NativeGameClient init remains its null default.
            }
            context = new WorldPacketHandlerContext(connections[PlayerUid], TextWriter.Synchronized(log),
                Slot(PlayerUid), WorldData: world, Sessions: sessions, TeamStates: teams,
                TournamentWatcherHolders: null, TimeProvider: clock,
                LegacyClock: LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000),
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        }
        public ConcurrentQueue<(uint Recipient, byte[] Bytes)> Order { get; } = new();
        public WorldSession Caster => Slot(PlayerUid);
        public WorldSession Target => Slot(TargetPlayerUid);
        public int CallbackCount { get; private set; }
        public int MaximumGlobalIndex { get; private set; } = -1;
        public int VectorCallbackCount { get; private set; }
        public ConcurrentQueue<Exception> CallbackErrors { get; } = new();
        public int ScheduledTimers => clock.ScheduledTimerCount;
        public string LogText => log.ToString();
        public Task CallbackCompleted => callbackCompleted.Task;
        private WorldSession Slot(uint uid)
        {
            Assert.True(sessions.TryGet(connections[uid], out var session));
            return Assert.IsType<WorldSession>(session);
        }
        private WorldPacketHandlerContext Context(uint uid) =>
            context with { Connection = connections[uid], Session = Slot(uid) };
        public byte[][] Frames(uint uid, int start) => Order.ToArray().Skip(start)
            .Where(frame => frame.Recipient == uid).Select(frame => frame.Bytes).ToArray();
        public void Arm(int start) { Assert.Equal(Order.Count, start); armed = true; }
        public async Task PrimeAndJoinByPublicPacketsAsync()
        {
            AssertUnboundOwners();
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
                await PrepareNativeOrdinaryMagicInputQueryAsync(Context(uid), () => Frames(uid, 0),
                    expectedMagicAttack: 1, expectedMaximumHitpoints: 500, expectedMaximumMana: 500);
            Assert.True(Caster.PlayerItems.EquipmentItems.All(item => item.Position is not (4 or 5)));
            Assert.True(Target.PlayerItems.EquipmentItems.All(item => item.Position is not (4 or 5)));
            Assert.False(HasLegacyStatusFlag(Target.Character.Attributes, 0x6F)); // Source SoulShackle.
            Assert.False(teams.HasTeam(connections[PlayerUid]));
            Assert.False(teams.HasTeam(connections[TargetPlayerUid]));
            await PacketHandlers.HandleAsync(Context(PlayerUid),
                ReadFrame(new MsgTeamPacket(MsgTeamType.Create, PlayerUid).ToFrameBytes()));
            var inviteStart = Order.Count;
            await PacketHandlers.HandleAsync(Context(PlayerUid),
                ReadFrame(new MsgTeamPacket(MsgTeamType.InviteRequest, TargetPlayerUid).ToFrameBytes()));
            Assert.Contains(Frames(TargetPlayerUid, inviteStart), bytes =>
                MsgTeamPacket.TryParse(ReadFrame(bytes), out var invitation) &&
                invitation.Type == MsgTeamType.InviteRequest && invitation.Uid == PlayerUid);
            await PacketHandlers.HandleAsync(Context(TargetPlayerUid),
                ReadFrame(new MsgTeamPacket(MsgTeamType.AcceptInvitation, PlayerUid).ToFrameBytes()));
            compatibilityTeamUid = teams.GetTeamUid(connections[PlayerUid]);
            Assert.True(compatibilityTeamUid > 0);
            AssertCompatibilityTeamMembership();
            Assert.Equal(300, Target.Character.MinHitPoints);
            Assert.Equal(0, CallbackCount);
            AssertUnboundOwners();
            Assert.True(HasLegacyStatusFlag(Caster.Character.Attributes, 4));
            Assert.Equal(CompatibilityBeforeResetText, Target.Character.Attributes["StatusFlags"]);
            Assert.False(Target.Character.Attributes.ContainsKey("BitVector"));
        }
        public void AssertUnboundOwners()
        {
            Assert.Null(context.TournamentWatcherHolders);
            Assert.Null(teams.NativeClientAuthority);
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            {
                Assert.Null(connections[uid].NativeGameClient);
                Assert.Null(Slot(uid).NativeGameClient);
                Assert.Null(Slot(uid).NativePlayer);
            }
        }
        public void AssertCompatibilityTeamMembership()
        {
            Assert.Equal(compatibilityTeamUid, teams.GetTeamUid(connections[PlayerUid]));
            Assert.Equal(compatibilityTeamUid, teams.GetTeamUid(connections[TargetPlayerUid]));
            Assert.True(teams.IsTeamMember(connections[PlayerUid], PlayerUid));
            Assert.True(teams.IsTeamMember(connections[PlayerUid], TargetPlayerUid));
            Assert.True(teams.IsTeamMember(connections[TargetPlayerUid], PlayerUid));
            Assert.True(teams.IsTeamMember(connections[TargetPlayerUid], TargetPlayerUid));
            Assert.Equal(2U, teams.GetMemberCount(connections[PlayerUid]));
            Assert.True(teams.IsLeader(connections[PlayerUid], PlayerUid));
            Assert.False(teams.IsLeader(connections[TargetPlayerUid], TargetPlayerUid));
        }
        public async Task CastByPublicPacketAsync() => await PacketHandlers.HandleAsync(Context(PlayerUid),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: PlayerUid, OpponentUid: TargetPlayerUid,
                X: 830, Y: 584, AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0, SpellId: spellId)).ToFrameBytes()));
        public void Dispose()
        {
            Assert.Equal(0, ScheduledTimers);
            GC.KeepAlive(connections);
            log.Dispose();
        }
    }
}
