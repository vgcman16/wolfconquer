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
    [InlineData(false, "attacker")]
    [InlineData(true, "attacker")]
    [InlineData(false, "target")]
    [InlineData(true, "target")]
    public async Task NativeFlag6NormalDeadRetainsEntryReceiverAcrossTransformFinish(
        bool originalVisible, string transformOwner)
    {
        Assert.Contains(transformOwner, new[] { "attacker", "target" });
        var model = new Flag6PublicReceiverSupplementModel(originalVisible); // Shared 9DF MODEL, not a second helper.
        using var fixture = new Flag6NormalDeadTransformFixture(model, transformOwner);
        await fixture.PrimeAndTransformAsync();
        var start = fixture.Order.Count;
        fixture.Arm(start);

        await fixture.KillByPublicThunderAsync();
        await fixture.FinishCallbackCompleted.WaitAsync(TimeSpan.FromSeconds(2));
        await model.Swapped.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, fixture.FinishCallbacks);
        Assert.True(fixture.FinishCallbackCompleted.IsCompletedSuccessfully);
        Assert.True(fixture.SwapGlobalIndex >= start);
        var ordered = fixture.Order.ToArray().Skip(start).ToArray();
        var finish = SingleGlobalTransformFinish(ordered, fixture.TransformUid);
        Assert.Equal(fixture.SwapGlobalIndex - start, finish);
        var target = fixture.Frames(TargetPlayerUid, start);
        var targetBeforeFinish = ordered.Take(finish)
            .Where(frame => frame.Recipient == TargetPlayerUid).Select(frame => frame.Bytes).ToArray();
        Assert.DoesNotContain(targetBeforeFinish, DirectNormalDeathTalk);
        Assert.DoesNotContain(targetBeforeFinish, bytes =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) && update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        AssertTransformOwnerQueryBeforeFinish(ordered.Take(finish)
            .Where(frame => frame.Recipient == fixture.TransformUid).Select(frame => frame.Bytes).ToArray(),
            fixture.TransformUid);

        // The target transform owns its complete extra query BEFORE normal Dead.
        // If only the attacker transformed, target HP has not been touched before DeadTalk.
        if (transformOwner == "target")
            AssertTransformOwnerQueryBeforeFinish(targetBeforeFinish, TargetPlayerUid);
        else
            Assert.DoesNotContain(targetBeforeFinish, DirectNormalDeathIsHp);

        var deathFrames = ordered.Skip(finish + 1).Where(frame => frame.Recipient == TargetPlayerUid)
            .Select(frame => frame.Bytes).ToArray();
        var deathHp = transformOwner == "target" ? 500U : 300U;
        var hp = AssertExistingNormalDeathCombatPhases(deathFrames, TargetPlayerUid,
            (500U, deathHp), (500U, deathHp), (500U, deathHp), (500U, 0U));
        var talk = SingleFrameIndex(deathFrames, frame => LegacyMsgMessagePacket.TryParse(frame, out var message) &&
            message.Content.Contains(LegacyPlayerDeathState.DeadTalkText));
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(deathFrames[talk]), out var deadTalk));
        Assert.Equal(LegacyMsgMessagePacket.TalkChatType, deadTalk.ChatType);
        Assert.Equal(LegacyMsgMessagePacket.RedColor, deadTalk.Color);
        Assert.Equal(new[] { "SYSTEM", "ALL", string.Empty, LegacyPlayerDeathState.DeadTalkText,
            string.Empty, string.Empty, string.Empty }, deadTalk.Content);
        var vectors = Enumerable.Range(0, deathFrames.Length).Where(index =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(deathFrames[index]), out var update) &&
            update.Uid == TargetPlayerUid && update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag)).ToArray();
        Assert.Equal(2, vectors.Length);
        Assert.True(talk < vectors[0] && vectors[0] < hp[0] && hp[^1] < vectors[1]);
        model.AssertVector(deathFrames[vectors[0]], TargetPlayerUid, dead: false);
        model.AssertVector(deathFrames[vectors[1]], TargetPlayerUid, dead: true);
        Assert.Equal(2, target.Count(bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
            update.Uid == TargetPlayerUid && update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag))); // No discarded extra vector.

        var deathIndex = SingleFrameIndex(deathFrames, frame =>
            MsgAttackPacket.TryParse(frame, out var attack) && attack.Query.AttackType == MsgAttackType.Death);
        Assert.True(deathIndex > vectors[1]);
        Assert.True(MsgAttackPacket.TryParse(ReadFrame(deathFrames[deathIndex]), out var death));
        Assert.Equal(PlayerUid, death.Query.Uid);
        Assert.Equal(TargetPlayerUid, death.Query.OpponentUid);
        Assert.Equal((ushort)830, death.Query.X);
        Assert.Equal((ushort)584, death.Query.Y);
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            var animation = Assert.Single(ParseSpellAnimations(fixture.Frames(uid, start)),
                packet => packet.SpellId == ThunderSpellId);
            var hit = Assert.Single(animation.Targets);
            Assert.Equal(TargetPlayerUid, hit.Uid);
            Assert.True(hit.Hit);
            Assert.True(hit.Damage > 0 && (ulong)hit.Damage >= (ulong)fixture.TargetHpBeforeHit);
            var attacks = fixture.Frames(uid, start).Select(bytes =>
                    MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) ? (MsgAttackPacket?)attack : null)
                .Where(attack => attack.HasValue && attack.Value.Query.AttackType == MsgAttackType.Death).ToArray();
            Assert.Single(attacks);
            Assert.Equal(PlayerUid, attacks[0]!.Value.Query.Uid);
            Assert.Equal(TargetPlayerUid, attacks[0]!.Value.Query.OpponentUid);
        }

        // Both admitted source appearances precede118 and normal Dead, never one invented final spawn.
        var otherUid = fixture.TransformUid == PlayerUid ? TargetPlayerUid : PlayerUid;
        var peerSpawns = ordered.Take(finish).Where(frame => frame.Recipient == otherUid &&
            LegacyPlayerSpawnPacket.TryParse(ReadFrame(frame.Bytes), out var spawn) &&
            spawn.GetNumericField(2) == fixture.TransformUid).ToArray();
        Assert.Equal(2, peerSpawns.Length);
        for (var index = 0; index < peerSpawns.Length; index++)
        {
            Assert.True(LegacyPlayerSpawnPacket.TryParse(ReadFrame(peerSpawns[index].Bytes), out var spawn));
            Assert.Equal(1_003UL, spawn.GetNumericField(1));
            Assert.Equal(index == 0 ? 3_000UL : 500UL, spawn.GetNumericField(21));
            Assert.Equal(500UL, spawn.GetNumericField(23));
        }
        Assert.Equal(0, fixture.Target.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, 5));
        Assert.False(HasLegacyStatusFlag(fixture.Target.Character.Attributes, 10));
        Assert.Equal(PlayerUid.ToString(CultureInfo.InvariantCulture),
            fixture.Target.Character.Attributes[LegacyPlayerDeathState.DeadKillerUidAttribute]);
        Assert.Equal("0", fixture.TransformedOwner.Character.Attributes[LegacyTransformState.TransformationIdAttribute]);
        Assert.False(LegacyTransformState.TryGetActiveMaxHitpoints(fixture.TransformedOwner.Character, out _));
        Assert.False(fixture.TransformedOwner.Character.Attributes.ContainsKey(LegacyTransformState.ExpiresAtUtcAttribute));
        model.AssertRetained(fixture.Target);
        Assert.Equal(0, fixture.ScheduledTimers);
        GC.KeepAlive(fixture);
    }

    private static int SingleGlobalTransformFinish(
        IReadOnlyList<(uint Recipient, byte[] Bytes)> frames, uint ownerUid)
    {
        var indices = Enumerable.Range(0, frames.Count).Where(index =>
            frames[index].Recipient == ownerUid &&
            MsgActionPacket.TryParse(ReadFrame(frames[index].Bytes), out var action) &&
            action.ObjId == ownerUid && action.Type == MsgActionType.FinishTransform).ToArray();
        return Assert.Single(indices);
    }

    private static void AssertTransformOwnerQueryBeforeFinish(IReadOnlyList<byte[]> frames, uint ownerUid)
    {
        var hp = Enumerable.Range(0, frames.Count).Where(index =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[index]), out var packet) && packet.Uid == ownerUid &&
            packet.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or
                LegacyMsgUpdateDataType.Hitpoints)).ToArray();
        Assert.Equal(6, hp.Length);
        (ulong Maximum, ulong Hitpoints)[] phases = [(500, 3_000), (500, 500), (500, 500)];
        for (var phase = 0; phase < phases.Length; phase++)
        {
            Assert.Equal(hp[phase * 2] + 1, hp[phase * 2 + 1]);
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[hp[phase * 2]]), out var maximum));
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[hp[phase * 2 + 1]]), out var current));
            Assert.Equal(new[] { new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints,
                phases[phase].Maximum) }, maximum.Updates);
            Assert.Equal(new[] {
                new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, phases[phase].Maximum),
                new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Hitpoints, phases[phase].Hitpoints)
            }, current.Updates);
        }
        var equipment = SingleFrameIndex(frames, frame =>
            LegacyShowEquipmentPacket.TryParse(frame, out var packet) &&
            packet.Action == LegacyShowEquipmentPacket.ShowAction);
        var status = SingleFrameIndex(frames, frame =>
            LegacyPlayerStatusPacket.TryParse(frame, out var packet) && packet.Uid == ownerUid);
        Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(frames[status]), out var snapshot));
        Assert.Equal(500U, snapshot.MaxHP);
        Assert.Equal(500U, snapshot.MaxMana);
        Assert.Equal(1U, snapshot.MagicAttack);
        var perfection = SingleFrameIndex(frames, frame =>
            LegacyUserTotalRefineLevPacket.TryParse(frame, out var packet) && packet.Uid == ownerUid);
        Assert.True(equipment < hp[0] && hp[1] < status && status < hp[2]);
        Assert.Equal(hp[3] + 1, hp[4]);
        Assert.True(hp[^1] < perfection);
    }

    // Genuine public ordinary Thunder1000 + Transform1350; explicit MODEL physical owner/Flag6.
    // No Team/Thundercloud/periodic loop, no reflection, receive helper call or desired status cache.
    private sealed class Flag6NormalDeadTransformFixture : IDisposable
    {
        private readonly WorldSessionStore sessions = new();
        private readonly Dictionary<uint, ConquerTcpConnection> connections = [];
        private readonly ManualRuntimeTimeProvider clock = new(
            new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));
        private readonly StringWriter log = new();
        private readonly WorldPacketHandlerContext context;
        private readonly Flag6PublicReceiverSupplementModel model;
        private readonly TaskCompletionSource finishCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool armed;
        private int operationStart;
        public Flag6NormalDeadTransformFixture(Flag6PublicReceiverSupplementModel model, string transformOwner)
        {
            this.model = model;
            TransformUid = transformOwner == "attacker" ? PlayerUid : TargetPlayerUid;
            const uint helmet = 111_099;
            var item = DirectOverlayItem(helmet, "Flag6TransformEntryRawHp500");
            var fields = item.Fields.ToArray();
            fields[18] = "500";
            fields[19] = "500";
            var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
                [
                    CreateMagicRow(TransformSpellId, sort: 19, level: 0, useMana: 0, useStamina: 0,
                        damage: 100, duration: 60, experience: 1_000, coolDown: 0, coldTime: 0),
                    CreateMagicRow(TransformSpellId, sort: 19, level: 1, useMana: 0, useStamina: 0,
                        damage: 100, duration: 60, experience: 1_000, coolDown: 0, coldTime: 0),
                    CreateMagicRow(ThunderSpellId, sort: 1, useMana: 0, useStamina: 0,
                        damage: 6_000, range: 18, coolDown: 0, coldTime: 0),
                ], items: [item with { Fields = fields }], transformInfo:
                [
                    new TransformInfo(TransformSpellId, 0, "DivineHare", 207, 3_000,
                        "fixture://TransformInfo.txt", LineNumber: 1, RawLine: "1350 0 DivineHare 207 3000",
                        Fields: ["1350", "0", "DivineHare", "207", "3000"]),
                ]);
            var attackerClient = new LegacyNativeGameClientRuntime();
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            {
                var client = uid == TargetPlayerUid ? model.Client : attackerClient;
                connections[uid] = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                    sendAsync: async (memory, callbackCancellationToken) =>
                    {
                        var bytes = memory.ToArray();
                        Order.Enqueue((uid, bytes));
                        if (!armed || uid != TransformUid ||
                            !MsgActionPacket.TryParse(ReadFrame(bytes), out var action) ||
                            action.ObjId != TransformUid || action.Type != MsgActionType.FinishTransform) return;
                        try
                        {
                            FinishCallbacks++;
                            Assert.Equal(1, FinishCallbacks);
                            SwapGlobalIndex = Order.Count - 1;
                            Assert.Equal("0", TransformedOwner.Character.Attributes[LegacyTransformState.TransformationIdAttribute]);
                            Assert.False(LegacyTransformState.TryGetActiveMaxHitpoints(TransformedOwner.Character, out _));
                            Assert.Equal(TransformUid == TargetPlayerUid ? 500 : 300, Target.Character.MinHitPoints);
                            Assert.False(Target.Character.Attributes.ContainsKey(LegacyPlayerDeathState.GhostStampAttribute));
                            Assert.False(Target.Character.Attributes.ContainsKey(LegacyPlayerDeathState.DeadStampAttribute));
                            Assert.DoesNotContain(Frames(TargetPlayerUid, operationStart), DirectNormalDeathTalk);
                            // Even in attacker phase, mutate TARGET's SAME client, never the attacker's Player.
                            await model.SwapAsync(connections[TargetPlayerUid], () => Target);
                            Assert.Same(model.Replacement, Target.NativePlayer);
                            Assert.Same(model.OriginalRow, NativeFlag6TestModel.Current(model.Original));
                            Assert.Same(model.ReplacementRow, NativeFlag6TestModel.Current(model.Replacement));
                            finishCompleted.TrySetResult();
                        }
                        catch (Exception error)
                        {
                            model.CaptureCallbackFailure(error);
                            finishCompleted.TrySetException(error);
                            throw;
                        }
                    }, nativeGameClient: client);
                sessions.Set(connections[uid], CreateSession(uid: uid,
                    name: uid == PlayerUid ? "Flag6TransformCaster" : "Flag6TransformTarget",
                    mapId: 1_000, x: uid == PlayerUid ? 832 : 830, y: 584,
                    classCode: 2_005, reborn: 2, strength: 0, spirit: 0, minHitPoints: 300,
                    minMana: 500, stamina: 100, spellIds: [ThunderSpellId, TransformSpellId],
                    attributes: new Dictionary<string, string> { ["PkMode"] = "0", ["BattlePower"] = "120" },
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
                Slot(PlayerUid), WorldData: world, Sessions: sessions, TimeProvider: clock,
                LegacyClock: LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000),
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        }
        public ConcurrentQueue<(uint Recipient, byte[] Bytes)> Order { get; } = new();
        public uint TransformUid { get; }
        public int FinishCallbacks { get; private set; }
        public int SwapGlobalIndex { get; private set; } = -1;
        public long TargetHpBeforeHit { get; private set; }
        public int ScheduledTimers => clock.ScheduledTimerCount;
        public Task FinishCallbackCompleted => finishCompleted.Task;
        public WorldSession Target => Slot(TargetPlayerUid);
        public WorldSession TransformedOwner => Slot(TransformUid);
        private WorldSession Slot(uint uid)
        {
            Assert.True(sessions.TryGet(connections[uid], out var current));
            return Assert.IsType<WorldSession>(current);
        }
        private WorldPacketHandlerContext Context(uint uid) =>
            context with { Connection = connections[uid], Session = Slot(uid) };
        public byte[][] Frames(uint recipient, int start) => Order.ToArray().Skip(start)
            .Where(frame => frame.Recipient == recipient).Select(frame => frame.Bytes).ToArray();
        public void Arm(int start) { operationStart = start; armed = true; }
        public async Task PrimeAndTransformAsync()
        {
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
                await PrepareNativeOrdinaryMagicInputQueryAsync(Context(uid), () => Frames(uid, 0),
                    expectedMagicAttack: 1, expectedMaximumHitpoints: 500, expectedMaximumMana: 500);
            Assert.Same(model.Client, connections[TargetPlayerUid].NativeGameClient);
            Assert.Same(model.Original, Target.NativePlayer);
            Assert.Equal(new ulong[18], LegacyPlayerDeathState.CreateStatusFlagVector(Target.Character.Attributes, 18));
            await PacketHandlers.HandleAsync(Context(TransformUid),
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: TransformUid, OpponentUid: TransformUid,
                    X: checked((ushort)TransformedOwner.Character.X), Y: 584, AttackType: MsgAttackType.Magic,
                    Damage: 0, KilledMonster: 0, ResponseDamage: 0, Effect: MsgAttackEffect.None,
                    Unknown: 0, SpellId: TransformSpellId)).ToFrameBytes()));
            Assert.True(LegacyTransformState.TryGetActiveMaxHitpoints(TransformedOwner.Character, out var maximum));
            Assert.Equal(3_000U, maximum);
            Assert.Equal(3_000, TransformedOwner.Character.MinHitPoints);
            Assert.Equal("207", TransformedOwner.Character.Attributes[LegacyTransformState.TransformationIdAttribute]);
            Assert.Equal(TransformUid == TargetPlayerUid ? 3_000 : 300, Target.Character.MinHitPoints);
            TargetHpBeforeHit = Target.Character.MinHitPoints;
            Assert.Equal(0, FinishCallbacks);
            Assert.Equal(0, model.SwapCount);
        }
        public async Task KillByPublicThunderAsync() => await PacketHandlers.HandleAsync(Context(PlayerUid),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: PlayerUid, OpponentUid: TargetPlayerUid,
                X: 830, Y: 584, AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0, SpellId: ThunderSpellId)).ToFrameBytes()));
        public void Dispose()
        {
            Assert.Equal(0, ScheduledTimers);
            GC.KeepAlive(connections); // Store weak keys must not turn a callback/lifetime test into a GC oracle.
            log.Dispose();
        }
    }
}
