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
    [InlineData(1_050, false)]
    [InlineData(1_050, true)]
    [InlineData(16_610, false)]
    [InlineData(16_610, true)]
    public async Task NativeFlag6SharedReviveCallsRetainReceiverAcrossFirstMaximumSend(
        int spellId, bool originalVisible)
    {
        Assert.Contains(spellId, new[] { 1_050, 16_610 });
        var model = new Flag6PublicReceiverSupplementModel(originalVisible);
        using var fixture = new Flag6SharedReviveFixture(checked((ushort)spellId), model);
        await fixture.PrimeAndJoinByPublicPacketsAsync();
        Assert.Equal(300, fixture.Target.Character.MinHitPoints); // Native Revive also accepts ALIVE targets.
        var start = fixture.Order.Count;
        fixture.Arm(start);

        await fixture.CastByPublicPacketAsync();
        await fixture.CallbackCompleted.WaitAsync(TimeSpan.FromSeconds(2));
        await model.Swapped.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, fixture.CallbackCount);
        Assert.True(fixture.CallbackCompleted.IsCompletedSuccessfully);
        Assert.True(fixture.SwapGlobalIndex >= start);
        var order = fixture.Order.ToArray().Skip(start).ToArray();
        var target = fixture.Frames(TargetPlayerUid, start);
        Assert.Equal(8, target.Length); // Two HP frames, XP, stamina, vector, mesh, map, animation.
        AssertSharedReviveHpPair(target, TargetPlayerUid, maximum: 500, hitpoints: 500);
        AssertSharedReviveScalar(target[2], TargetPlayerUid, LegacyMsgUpdateDataType.XPCircle, 0);
        AssertSharedReviveScalar(target[3], TargetPlayerUid, LegacyMsgUpdateDataType.Stamina, 100);
        model.AssertVector(target[4], TargetPlayerUid, dead: false); // ENTIRE18 words, held original Player.
        AssertSharedReviveScalar(target[5], TargetPlayerUid, LegacyMsgUpdateDataType.Mesh, 1_003);
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

        var maximumAt = SingleSharedReviveGlobalIndex(order, TargetPlayerUid,
            bytes => Flag6PublicReceiverSupplementModel.FirstMaximum(bytes, TargetPlayerUid));
        Assert.Equal(fixture.SwapGlobalIndex - start, maximumAt);
        var staminaAt = SingleSharedReviveGlobalIndex(order, PlayerUid,
            bytes => IsSharedReviveScalar(bytes, PlayerUid, LegacyMsgUpdateDataType.Stamina, 84));
        var manaAt = SingleSharedReviveGlobalIndex(order, PlayerUid,
            bytes => IsSharedReviveScalar(bytes, PlayerUid, LegacyMsgUpdateDataType.Mana, 463));
        Assert.True(staminaAt < manaAt && manaAt < maximumAt); // Real nonzero16/37 resource sends.
        var caster = fixture.Frames(PlayerUid, start);
        if (spellId == 1_050)
        {
            var xpAt = SingleSharedReviveGlobalIndex(order, PlayerUid,
                bytes => IsSharedReviveScalar(bytes, PlayerUid, LegacyMsgUpdateDataType.XPList, 0));
            Assert.True(manaAt < xpAt && xpAt < maximumAt);
        }
        else
        {
            Assert.DoesNotContain(caster, bytes =>
                LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
                update.Uid == PlayerUid && update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.XPList));
        }

        var spawnAt = SingleSharedReviveGlobalIndex(order, PlayerUid, bytes =>
            LegacyPlayerSpawnPacket.TryParse(ReadFrame(bytes), out var spawn) &&
            spawn.GetNumericField(2) == TargetPlayerUid);
        Assert.True(LegacyPlayerSpawnPacket.TryParse(ReadFrame(order[spawnAt].Bytes), out var peerSpawn));
        Assert.Equal(1_003UL, peerSpawn.GetNumericField(1));
        Assert.Equal(500UL, peerSpawn.GetNumericField(21));
        Assert.Equal(500UL, peerSpawn.GetNumericField(23));
        var mapAt = SingleSharedReviveGlobalIndex(order, TargetPlayerUid,
            bytes => LegacyMapStatusPacket.TryParse(ReadFrame(bytes), out _));
        Assert.True(mapAt < spawnAt); // The revived target is excluded, NOT the caster.
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            var animationAt = SingleSharedReviveGlobalIndex(order, uid, bytes =>
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
        model.AssertRetained(fixture.Target);
        Assert.Same(model.Client, fixture.TargetConnection.NativeGameClient);
        fixture.AssertOriginalTeamMembership();
        Assert.Contains("WorldServer handled legacy revive magic", fixture.LogText);
        Assert.Contains("spellId=" + spellId.ToString(CultureInfo.InvariantCulture), fixture.LogText);
        Assert.Equal(0, fixture.ScheduledTimers);
        GC.KeepAlive(fixture);
    }

    private static int SingleSharedReviveGlobalIndex(
        IReadOnlyList<(uint Recipient, byte[] Bytes)> frames, uint uid, Func<byte[], bool> predicate) =>
        Assert.Single(Enumerable.Range(0, frames.Count), index =>
            frames[index].Recipient == uid && predicate(frames[index].Bytes));

    private static bool IsSharedReviveScalar(byte[] bytes, uint uid,
        LegacyMsgUpdateDataType type, ulong value) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) && update.Uid == uid &&
        update.Updates.Count == 1 && update.Updates[0].Type == type && update.Updates[0].Value == value;

    private static void AssertSharedReviveScalar(byte[] bytes, uint uid,
        LegacyMsgUpdateDataType type, ulong value)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update));
        Assert.Equal(uid, update.Uid);
        Assert.Equal(new[] { new LegacyMsgUpdateEntry(type, value) }, update.Updates);
    }

    private static void AssertSharedReviveHpPair(byte[][] frames, uint uid, ulong maximum, ulong hitpoints)
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

    // Public packet ingress; declared MODEL spell rows/book/physical Flag6 are not learned skills/socket birth.
    // Native DetachStatus/Revive permit these ALIVE targets. No death path or fake membership is needed.
    private sealed class Flag6SharedReviveFixture : IDisposable
    {
        private readonly WorldSessionStore sessions = new();
        private readonly LegacyTournamentWatcherHolders holders;
        private readonly TeamSessionStateStore teams;
        private readonly Dictionary<uint, ConquerTcpConnection> connections = [];
        private readonly ManualRuntimeTimeProvider clock = new(
            new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));
        private readonly StringWriter log = new();
        private readonly WorldPacketHandlerContext context;
        private readonly Flag6PublicReceiverSupplementModel model;
        private readonly ushort spellId;
        private readonly TaskCompletionSource callbackCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool armed;
        private LegacyTournamentTeam? originalTeam;
        public Flag6SharedReviveFixture(ushort spellId, Flag6PublicReceiverSupplementModel model)
        {
            this.spellId = spellId;
            this.model = model;
            holders = new(sessions);
            teams = new(sessions, holders);
            const uint helmet = 111_098;
            var item = DirectOverlayItem(helmet, "SharedReviveNeutralHpMana500");
            var fields = item.Fields.ToArray();
            fields[18] = "500";
            fields[19] = "500";
            var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
                [CreateMagicRow(spellId, sort: 7, useMana: 37, useStamina: 16, damage: 100,
                    duration: 125, range: 18, coolDown: 0, coldTime: 0)],
                items: [item with { Fields = fields }]);
            var casterClient = new LegacyNativeGameClientRuntime();
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            {
                var client = uid == TargetPlayerUid ? model.Client : casterClient;
                connections[uid] = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                    sendAsync: async (memory, _) =>
                    {
                        var bytes = memory.ToArray();
                        Order.Enqueue((uid, bytes));
                        if (!armed || uid != TargetPlayerUid ||
                            !Flag6PublicReceiverSupplementModel.FirstMaximum(bytes, TargetPlayerUid)) return;
                        try
                        {
                            CallbackCount++;
                            Assert.Equal(1, CallbackCount);
                            SwapGlobalIndex = Order.Count - 1;
                            Assert.Equal(500, Target.Character.MinHitPoints);
                            Assert.Same(model.Original, Target.NativePlayer);
                            AssertOriginalTeamMembership();
                            await model.SwapAsync(connections[TargetPlayerUid], () => Target);
                            Assert.Same(model.Replacement, Target.NativePlayer);
                            Assert.Same(model.OriginalRow, NativeFlag6TestModel.Current(model.Original));
                            Assert.Same(model.ReplacementRow, NativeFlag6TestModel.Current(model.Replacement));
                            callbackCompleted.TrySetResult();
                        }
                        catch (Exception error)
                        {
                            model.CaptureCallbackFailure(error);
                            callbackCompleted.TrySetException(error);
                            throw;
                        }
                    }, nativeGameClient: client);
                // XPList4 is declared compatibility caster prestate, not an earned/full physical flag producer.
                var attributes = new Dictionary<string, string>
                {
                    ["StatusFlags"] = uid == PlayerUid ? "16/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0" :
                        "0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0",
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
                    }) with { NativeGameClient = client });
            }
            context = new WorldPacketHandlerContext(connections[PlayerUid], TextWriter.Synchronized(log),
                Slot(PlayerUid), WorldData: world, Sessions: sessions, TeamStates: teams,
                TournamentWatcherHolders: holders, TimeProvider: clock,
                LegacyClock: LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000),
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        }
        public ConcurrentQueue<(uint Recipient, byte[] Bytes)> Order { get; } = new();
        public WorldSession Caster => Slot(PlayerUid);
        public WorldSession Target => Slot(TargetPlayerUid);
        public ConquerTcpConnection TargetConnection => connections[TargetPlayerUid];
        public int CallbackCount { get; private set; }
        public int SwapGlobalIndex { get; private set; } = -1;
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
            Assert.True(holders.TryCaptureClient(connections[PlayerUid], out var leader));
            Assert.True(teams.TryCaptureTournamentTeam(leader!, out originalTeam));
            AssertOriginalTeamMembership();
            Assert.Equal(300, Target.Character.MinHitPoints);
            Assert.Equal(0, CallbackCount);
            Assert.Same(model.Original, Target.NativePlayer);
            Assert.True(HasLegacyStatusFlag(Caster.Character.Attributes, 4));
            Assert.Equal(new ulong[18], LegacyPlayerDeathState.CreateStatusFlagVector(Target.Character.Attributes, 18));
        }
        public void AssertOriginalTeamMembership()
        {
            Assert.NotNull(originalTeam);
            Assert.True(holders.TryCaptureClient(connections[PlayerUid], out var leader));
            Assert.True(holders.TryCaptureClient(connections[TargetPlayerUid], out var member));
            Assert.True(teams.TryCaptureTournamentTeam(leader!, out var team));
            Assert.Same(teams, originalTeam!.Owner);
            Assert.Same(originalTeam.Owner, team!.Owner);
            Assert.Same(originalTeam.Identity, team.Identity);
            Assert.Equal(originalTeam.Uid, team.Uid);
            Assert.True(teams.TryCaptureTournamentTeam(member!, out var memberTeam));
            Assert.Same(team.Owner, memberTeam!.Owner);
            Assert.Same(team.Identity, memberTeam.Identity);
            Assert.Equal(team.Uid, memberTeam.Uid);
            Assert.True(teams.IsTournamentTeamMember(team!, leader!));
            Assert.True(teams.IsTournamentTeamMember(team!, member!));
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
