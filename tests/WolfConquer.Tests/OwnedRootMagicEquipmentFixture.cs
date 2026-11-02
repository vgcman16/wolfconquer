using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    internal static async Task RunOwnedRootEquipmentAsync(string scenario, Func<OwnedRootEquipmentFixture, Task> exercise)
    {
        OwnedRootEquipmentFixture? fixture = null;
        Exception? primary = null;
        try { fixture = new(); await fixture.Initialize(scenario); await exercise(fixture); }
        catch (Exception error) { primary = error; }
        Exception? cleanup = null;
        if (fixture is not null)
            try { fixture.RetainEvidence = primary is not null; await fixture.DisposeAsync(); }
            catch (Exception error) { cleanup = error; }
        if (primary is not null && cleanup is not null) throw new AggregateException(primary, cleanup);
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        if (cleanup is not null) ExceptionDispatchInfo.Capture(cleanup).Throw();
    }

    // Preserve an assertion failure even if a bounded asynchronous cleanup fails.
    private static async Task PreserveOwnedPauseFailuresAsync(Func<Task> exercise, Func<Task> cleanup)
    {
        Exception? primary = null, cleanupError = null;
        try { await exercise(); } catch (Exception error) { primary = error; }
        try { await cleanup(); } catch (Exception error) { cleanupError = error; }
        if (primary is not null && cleanupError is not null) throw new AggregateException(primary, cleanupError);
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        if (cleanupError is not null) ExceptionDispatchInfo.Capture(cleanupError).Throw();
    }

    private static async Task FinishOwnedPauseAsync(OwnedRootEquipmentFixture fixture, uint uid,
        Task blocked, bool drained, bool started, bool completed, params Task?[] actions)
    {
        var errors = new List<Exception>();
        if (!drained) try { fixture.CloseLoopbackReader(uid); } catch (Exception error) { errors.Add(error); }
        try { await blocked.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (Exception error) when (!drained && (error is IOException or System.Net.Sockets.SocketException or ObjectDisposedException)) { }
        catch (Exception error) { errors.Add(error); }
        foreach (var action in actions)
            if (action is not null) try { await action.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception error) { errors.Add(error); }
        if (started && !completed)
            try { await fixture.CompletePausedRuntimeAdvance(() => true); } catch (Exception error) { errors.Add(error); }
        if (errors.Count != 0) throw new AggregateException("Owned pause cleanup failed.", errors);
    }

    internal sealed class OwnedRootEquipmentFixture : IAsyncDisposable
    {
        internal const uint IndependentCasterUid = 1_293_602, IndependentTargetUid = 1_293_603;
        internal const uint ExpiryObserverUid = 1_293_604;
        internal readonly LegacyOwnedAdmissionRuntimeFixture Data = new(bootstrap: false);
        private readonly NativeLoginBeastMetadataScope beasts = new(1000);
        private readonly ManualRuntimeTimeProvider clock = new(RootMagicEntryOrigin);
        private readonly Dictionary<uint, ConquerTcpConnection> connections = [];
        private readonly Dictionary<uint, RootLoopbackFramePeer> loopbacks = [];
        private readonly List<MsgItemUsagePacket> queuedGear = [];
        private readonly StringWriter log = new();
        private LegacyOwnedAdmissionRuntime runtime = null!;
        private NativeCombatGearProcFixture? nativeGearMetadata;
        private WorldServerFrameHandler handler = null!;
        private bool workers;
        private Task[] runtimeWorkers = [];
        private Exception? observationFailure;
        private Exception? reportedObservationFailure;
        private byte[]? gearExpected;
        private bool gearObserved;
        internal bool RetainEvidence;
        internal Action<OwnedRootEquipmentFixture, uint, byte[]>? OnSend;
        internal Func<OwnedRootEquipmentFixture, uint, byte[], ValueTask>? OnSendAsync;
        internal readonly ConcurrentQueue<(uint Recipient, byte[] Bytes)> Order = new();
        internal WorldSessionStore Sessions => Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
            .GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(handler));
        internal WorldSession Owner => Current(PlayerUid);
        internal WorldSession Peer => Current(TargetPlayerUid);
        internal DateTimeOffset Now => clock.GetUtcNow();
        internal string Output => log.ToString();
        internal string ItemPath => Path.Combine(Data.Items, PlayerUid + ".bin");
        internal WorldSession Current(uint uid) => Assert.IsType<WorldSession>(Sessions.TryGet(connections[uid], out var owner) ? owner : null);
        // Read-only diagnostic through the real holder/native gates. It never
        // creates a query epoch or supplies a maximum to the Reviver.
        internal string DescribeRealMaximumsQuery(uint uid)
        {
            string outcome = "holder-missing";
            var session = Current(uid);
            if (session.NativeGameClient?.OrdinarySpellBinding is not { } binding) return "binding-missing";
            bool held = Sessions.WithCurrentNativeClientIdentity(connections[uid], uid, identity =>
            {
                if (!ReferenceEquals(identity, binding.NativeIdentity) || !ReferenceEquals(Current(uid), session))
                { outcome = "holder-changed"; return; }
                lock (binding.Client.SpellAdmissionSyncRoot)
                lock (binding.Player.SpellAdmissionSyncRoot)
                {
                    bool captured = LegacyNativeMagicQueryClient.TryCaptureRecordedReceive(binding, session, out var query, out var values);
                    outcome = captured && query is not null
                        ? $"current-completed hp={values.MaximumHitpoints} mana={values.MaximumMana}"
                        : "no-current-completed-query";
                }
            });
            return held ? outcome : "holder-missing";
        }
        internal Task StartBackpressuredWrite(uint uid) => loopbacks[uid].StartBackpressuredWrite();
        internal bool ActualBackpressure(uint uid) => loopbacks[uid].ActualBackpressure;
        internal bool ReviverWaitingAtRealGate(uint uid) => loopbacks[uid].ExpiryWriter.ReviverWaitingForGate > 0;
        internal bool TickerWaitingAtRealGate(uint uid) => loopbacks[uid].ExpiryWriter.TickerWaitingForGate > 0;
        internal bool AttemptUngatedActualTickerPermit(uint uid)
        {
            var table = typeof(WorldServerFrameHandler).GetField("playerTickXpPermits", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            var connection = connections[uid];
            var rows = ((System.Collections.IEnumerable)table).Cast<object>();
            var permit = Assert.Single(rows.Select(row => (Key: row.GetType().GetProperty("Key")!.GetValue(row), Value: row.GetType().GetProperty("Value")!.GetValue(row)))
                .Where(row => ReferenceEquals(row.Value!.GetType().GetField("Connection", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(row.Value), connection))
                .Select(row => Assert.IsType<WorldServerFrameHandler.PlayerTickXpPermit>(row.Key)));
            var identity = Assert.IsType<LegacyOrdinarySpellBinding>(Current(uid).NativeGameClient!.OrdinarySpellBinding).NativeIdentity;
            return Sessions.TryStartPlayerTickXp(handler, permit, connection, identity, loopbacks[uid].ExpiryWriter,
                new object(), CancellationToken.None, out _);
        }
        internal bool StaminaWaitingAtRealGate(uint uid) => loopbacks[uid].ExpiryWriter.StaminaWaitingForGate > 0;
        internal bool ExpiryWaitingAtRealGate(uint uid) => loopbacks[uid].ExpiryWriter.WaitingForGate > 0;
        internal Task WaitForBackpressure(uint uid) => loopbacks[uid].WaitForBackpressure();
        internal void CloseLoopbackReader(uint uid) => loopbacks[uid].CloseReader();
        internal Task DrainBackpressuredWrite(uint uid) => loopbacks[uid].DrainBackpressuredWrite();
        internal long WireStarts(uint uid) => loopbacks[uid].WireStarts;
        internal bool AttemptUngatedActualExpiryPermit(uint uid)
        {
            var table = typeof(WorldSessionStore).GetField("rootExpiryWrites", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Sessions)!;
            var keys = ((System.Collections.IEnumerable)table).Cast<object>()
                .Select(row => row.GetType().GetProperty("Key")!.GetValue(row))
                .OfType<WorldSessionStore.RootExpiryWritePermit>().ToArray();
            var permit = Assert.Single(keys); // The real timer has issued this permit and waits at the actual gate.
            var connection = connections[uid];
            using var unusedGate = new SemaphoreSlim(1, 1);
            var unregistered = new RootExpiryWireWriter(connection, unusedGate,
                (_, _) => throw new InvalidOperationException("Unregistered writer must never start."),
                () => false, () => throw new InvalidOperationException("Unregistered writer must never close."));
            var unregisteredAccepted = Sessions.TryStartRootExpiryWrite(permit, connection, unregistered,
                new object(), CancellationToken.None, out _);
            var ungatedAccepted = Sessions.TryStartRootExpiryWrite(permit, connection, loopbacks[uid].ExpiryWriter,
                new object(), CancellationToken.None, out _);
            // This is the same real, still-pending timer-issued permit and the
            // actual registered writer. No active gate start exists here.
            var nullStartAccepted = Sessions.TryStartRootExpiryWrite(permit, connection, loopbacks[uid].ExpiryWriter,
                null!, CancellationToken.None, out _);
            return unregisteredAccepted || ungatedAccepted || nullStartAccepted;
        }
        internal bool HasFlag(int flag) => HasLegacyStatusFlag(Owner.Character.Attributes, flag);
        internal byte[][] FramesSince(uint uid, int start) => Order.ToArray().Skip(start).Where(row => row.Recipient == uid).Select(row => row.Bytes).ToArray();
        internal LegacySpellAnimationPacket[] Animations(uint uid, int start) => FramesSince(uid, start).Select(ReadFrame)
            .Where(frame => LegacySpellAnimationPacket.TryParse(frame, out _))
            .Select(frame => { Assert.True(LegacySpellAnimationPacket.TryParse(frame, out var packet)); return packet; }).ToArray();
        internal (LegacyMsgUpdateDataType Type, ulong Value)[] Costs(int start) => RootMagicEntryCosts(FramesSince(PlayerUid, start));
        internal DateTimeOffset Deadline(int flag) => DateTimeOffset.Parse(Owner.Character.Attributes[flag == 9
            ? WorldPacketHandlers.RuntimeLegacyStigmaExpiresAtUtcAttribute : WorldPacketHandlers.RuntimeLegacyMagicShieldExpiresAtUtcAttribute], CultureInfo.InvariantCulture);
        internal ulong[] Vectors(uint uid, int start) => RootManualLifetimeVectors(FramesSince(uid, start));

        internal async Task Initialize(string scenario)
        {
            workers = scenario is "xp" or "manual" or "manual-other" or "expiry-authority" or "independent" or "successor" or "reviver" or "reviver-far" or "generation";
            var inputs = OwnedRootMagicInputs(scenario is "independent" or "successor" ? "manual" : scenario);
            Save(inputs.Owner); Save(inputs.Peer);
            if (scenario == "manual-other")
            {
                var observer = CreateSession(uid: ExpiryObserverUid, name: "ExpiryObserver", mapId: 1_000,
                    x: 833, y: 585, classCode: 142, minHitPoints: 100_000, minMana: 0, stamina: 100,
                    spellIds: [], playerItems: new([], [], [], new PlayerItemSnapshotItem[]
                    {
                        new() { Uid = 99_291, ItemId = 111_005, Position = 1, Durability = 100, MaximumDurability = 100, StackSize = 1 },
                        new() { Uid = 99_292, ItemId = 120_005, Position = 2, Durability = 100, MaximumDurability = 100, StackSize = 1 },
                    }, []), attributes: new Dictionary<string, string> { ["PkMode"] = "0", ["BattlePower"] = "120" });
                Save(new(observer.Character, observer.PlayerItems, observer.PlayerSpells, observer.PlayerProficiencies));
            }
            if (scenario is "independent" or "successor" or "reviver" or "reviver-far")
            {
                var caster = CreateSession(uid: IndependentCasterUid, name: "IndependentCaster", mapId: scenario == "successor" ? 1_000 : 1_010, x: 830, y: 584,
                    classCode: 142, minHitPoints: 5_000, minMana: 1_000, stamina: 100, spellIds: [1000],
                    attributes: new Dictionary<string, string> { ["PkMode"] = "0", ["BattlePower"] = "120" });
                var target = CreateSession(uid: IndependentTargetUid, name: "IndependentTarget", mapId: scenario == "successor" ? 1_000 : 1_010, x: 832, y: 584,
                    classCode: 142, minHitPoints: 5_000, minMana: 1_000, stamina: 100, spellIds: [],
                    attributes: new Dictionary<string, string> { ["PkMode"] = "0", ["BattlePower"] = "120" });
                Save(new(caster.Character, caster.PlayerItems, caster.PlayerSpells, caster.PlayerProficiencies));
                Save(new(target.Character, target.PlayerItems, target.PlayerSpells, target.PlayerProficiencies));
            }
            if (scenario is "manual-other" or "reviver" or "reviver-far")
            {
                nativeGearMetadata = new NativeCombatGearProcFixture(Data.Root, 703, 21_930_000);
                nativeGearMetadata.Load(inputs.World);
            }
            Data.Bootstrap();
            Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out var opened, out var reason), reason);
            runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(opened);
            handler = new WorldServerFrameHandler(Data.Loader, null, inputs.World,
                new LegacyRuntimeUpdateLoopOptions(workers, TimeSpan.FromMilliseconds(20), clock,
                    LegacySignedMillisecondClock.FromTimeProvider(clock, 10000)), log,
                protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: runtime, nativeCombatGear: nativeGearMetadata?.Runtime);
            var participantIds = scenario is "independent" or "successor" or "reviver" or "reviver-far"
                ? new[] { PlayerUid, TargetPlayerUid, IndependentCasterUid, IndependentTargetUid }
                : scenario == "manual-other" ? new[] { PlayerUid, TargetPlayerUid, ExpiryObserverUid }
                : new[] { PlayerUid, TargetPlayerUid };
            foreach (var uid in participantIds)
            {
                var native = new LegacyNativeGameClientRuntime();
                var peer = new RootLoopbackFramePeer(bytes => Order.Enqueue((uid, bytes)), async bytes =>
                {
                    try
                    {
                        if (uid == PlayerUid && gearExpected is { } expected && !gearObserved)
                        {
                            gearObserved = true;
                            Assert.Equal(expected, File.ReadAllBytes(ItemPath));
                            Assert.False(LegacyTradeJournal.HasPending(Data.Users));
                            Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
                            Assert.False(Monitor.IsEntered(LegacySecondaryPasswordRuntime.SyncRoot));
                        }
                        OnSend?.Invoke(this, uid, bytes);
                        if (OnSendAsync is { } observer) await observer(this, uid, bytes).ConfigureAwait(false);
                    }
                    catch (Exception error) { Interlocked.CompareExchange(ref observationFailure, error, null); throw; }
                }, native);
                loopbacks.Add(uid, peer);
                connections.Add(uid, peer.Connection);
                await Send(uid, new LegacyMsgLoginGamePacket(uid).ToFrameBytes());
                Assert.Same(native, Current(uid).NativeGameClient);
                Assert.NotNull(native.OrdinarySpellBinding);
                Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(connections[uid], Current(uid)));
                if (uid == TargetPlayerUid)
                {
                    Assert.Equal(100_000L, Current(uid).Character.MinHitPoints);
                    //251 is the genuine AllowAnimation equipment-query route,
                    // not the separate408 peer-view QueryEquipment action.
                    await Send(uid, new MsgActionPacket { ObjId = uid, Type = MsgActionType.AllowAnimation }.ToFrameBytes());
                    Assert.Equal(100_000L, Current(uid).Character.MinHitPoints);
                    var reloaded = Assert.IsType<CharacterSnapshot>(Data.Loader.LoadByAccountId(uid));
                    Assert.Equal(100_000L, reloaded.MinHitPoints);
                    Assert.False(HasLegacyStatusFlag(Current(uid).Character.Attributes, 0x05));
                    Assert.False(HasLegacyStatusFlag(Current(uid).Character.Attributes, 0x0A));
                    Assert.Equal(new uint[] { 111005, 120005 }, Current(uid).PlayerItems.EquipmentItems.OrderBy(item => item.Position).Select(item => item.ItemId));
                    foreach (var id in new[] { 111005, 120005 })
                    {
                        var metadata = Assert.Single(inputs.World.ItemsById[id]);
                        Assert.Equal(50_000U, metadata.ItemHP);
                        Assert.InRange(metadata.ItemHP, 0U, ushort.MaxValue);
                    }
                }
                if (workers)
                {
                    // After the first real login source starts a player and the
                    // single global loop; the second starts one more player.
                    // Capture the actual tasks, not synthetic timer labels.
                    var expected = connections.Count + 1;
                    await WaitUntilAsync(() =>
                    {
                        ThrowObservationFailure();
                        return clock.ScheduledTimerCount == expected &&
                            handler.CaptureLegacyRuntimeWorkerTasks().Length == expected &&
                            handler.CaptureLegacyRuntimeWorkerTasks().All(task => !task.IsCompleted) && GlobalLoopStarted();
                    }, TimeSpan.FromSeconds(3));
                    var next = handler.CaptureLegacyRuntimeWorkerTasks();
                    Assert.Equal(expected, next.Length);
                    Assert.All(runtimeWorkers, task => Assert.Contains(task, next));
                    Assert.Equal(expected - runtimeWorkers.Length, next.Count(task => !runtimeWorkers.Contains(task)));
                    runtimeWorkers = next;
                }
            }
            if (workers) Assert.True(WorkersCurrentAndRearmed());
            Order.Clear();
        }

        private void Save(OwnedEquipmentBehaviorInput input)
        {
            var uid = input.Character.Uid;
            var values = new Dictionary<string, string>(input.Character.Attributes, StringComparer.OrdinalIgnoreCase);
            foreach (var pair in CharacterSnapshotScalarPersistence.BuildPersistedValues(input.Character)) values[pair.Key] = pair.Value;
            values["Name"] = input.Character.Name; values["SecurityPass"] = "0,0,0";
            File.WriteAllText(Path.Combine(Data.Users, uid + ".ini"), "[Character]\r\n" +
                string.Join("\r\n", values.Select(pair => pair.Key + "=" + pair.Value)) + "\r\n", new UTF8Encoding(true));
            new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(uid, input.Items);
            new PlayerSpellSnapshotLoader(Data.Spells).SaveByCharacterUid(uid, input.Spells ?? PlayerSpellSnapshot.Empty);
            new PlayerProficiencySnapshotLoader(Data.Proficiencies).SaveByCharacterUid(uid, input.Proficiencies ?? PlayerProficiencySnapshot.Empty);
            new PlayerQuestSnapshotLoader(Data.Quests).SaveByCharacterUid(uid, PlayerQuestSnapshot.Empty);
            new CombatGearSnapshotLoader(Data.CombatGear).SaveByCharacterUid(uid, input.CombatGear ?? CombatGearSnapshot.Empty);
            new LegacyMailboxStore(Data.Root).Save(checked((uint)uid));
        }

        private async Task Send(uint uid, byte[] frame)
        {
            await handler.HandleFrameAsync(connections[uid], ReadFrame(frame));
            ThrowObservationFailure();
        }

        private void ThrowObservationFailure()
        {
            if (Volatile.Read(ref observationFailure) is not { } failure) return;
            Volatile.Write(ref reportedObservationFailure, failure);
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        internal LegacyRootItemEffectFlagClock? ActualClock(int flag) =>
            LegacyRootItemEffectFlagClock.TryRead(Sessions, connections[PlayerUid], Owner.Character, flag, out var value) ? value : null;
        internal LegacyRootItemEffectFlagClock.ExpiryObservation? ObserveRealClockExpiry(int flag)
        {
            LegacyRootItemEffectFlagClock.ExpiryObservation? observed = null;
            Assert.True(Sessions.WithCurrentNativeClientIdentity(connections[PlayerUid], PlayerUid, identity =>
                observed = LegacyRootItemEffectFlagClock.CaptureExpiry(Sessions, connections[PlayerUid], Owner, identity, flag, Now, clock)));
            return observed;
        }
        internal void AttemptAmbiguousRootProjectionThroughStore(string ownedKey)
        {
            var current = Owner;
            var fields = new Dictionary<string, string>(current.Character.Attributes, StringComparer.Ordinal);
            var originalKey = Assert.Single(fields.Keys, key => key.Equals(ownedKey, StringComparison.OrdinalIgnoreCase));
            var collision = originalKey == ownedKey ? ownedKey.ToLowerInvariant() : ownedKey;
            Assert.NotEqual(originalKey, collision);
            fields.Add(collision, fields[originalKey]);
            Sessions.Set(connections[PlayerUid], current with { Character = current.Character with { Attributes = fields } });
        }
        // Actual Store publication after the genuine ticker committed and is
        // waiting at the real socket gate. Preserve every unrelated attribute.
        internal void PublishXpKeySuccessor(uint uid, bool duplicate)
        {
            var current = Current(uid);
            var fields = new Dictionary<string, string>(current.Character.Attributes, StringComparer.Ordinal);
            var key = Assert.Single(fields.Keys, value => value.Equals("XPCount", StringComparison.OrdinalIgnoreCase));
            var value = fields[key];
            fields.Remove(key);
            fields.Add("xpcount", value);
            if (duplicate) fields.Add("XPCount", value);
            Sessions.Set(connections[uid], current with { Character = current.Character with { Attributes = fields } });
        }
        internal bool AttemptUnassociatedManualProc()
        {
            var expected = Owner;
            Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(Sessions, connections[PlayerUid], PlayerUid, out var identity));
            bool applied = Sessions.TryCommitRootManualItemEffect(connections[PlayerUid], identity, expected,
                new WorldPacketHandlers.RootManualItemEffectAdmission(), 8, 15,
                WorldPacketHandlers.RuntimeLegacyMagicShieldRemoveOnDeadAttribute, clock, out var final);
            Assert.Null(final);
            return applied;
        }
        internal bool AttemptUnassociatedExpiry()
        {
            var expected = Owner;
            Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(Sessions, connections[PlayerUid], PlayerUid, out var identity));
            bool applied = Sessions.TryCommitRootItemEffectExpiry(connections[PlayerUid], identity, expected,
                new WorldServerFrameHandler.RootExpiryAuthorization(), out var final, out var removed);
            Assert.Null(final); Assert.False(removed);
            return applied;
        }
        internal bool AttemptInvalidFlagPreparation(int flag)
        {
            var expected = Owner;
            var data = new LegacyRootItemEffectFlagClock.ExpiryObservation(flag, null, false, false);
            bool accepted = false;
            Assert.True(Sessions.WithCurrentNativeClientIdentity(connections[PlayerUid], PlayerUid, identity =>
            {
                using var prepared = LegacyRootItemEffectFlagClock.PreparedExpiry.TryCreate(Sessions, connections[PlayerUid], expected, identity, data);
                accepted = prepared is not null;
            }));
            return accepted || WorldServerFrameHandler.TryProjectRootItemEffectExpiry(expected.Character, data, out _, out _);
        }
        internal Task DispelOwner() => Send(TargetPlayerUid, new MsgAttackPacket(new MsgAttackQuery(
            Uid: TargetPlayerUid, OpponentUid: PlayerUid, X: checked((ushort)Owner.Character.X), Y: checked((ushort)Owner.Character.Y),
            AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0, ResponseDamage: 0, Effect: MsgAttackEffect.None,
            Unknown: 0, SpellId: 1001)).ToFrameBytes());
        internal Task Query() => Send(PlayerUid, new MsgActionPacket { ObjId = PlayerUid, Type = MsgActionType.AllowAnimation }.ToFrameBytes());
        internal Task QueryIndependent(uint uid) => Send(uid,
            new MsgActionPacket { ObjId = uid, Type = MsgActionType.AllowAnimation }.ToFrameBytes());
        internal Task CastIndependent() => Send(IndependentCasterUid, new MsgAttackPacket(new MsgAttackQuery(
            Uid: IndependentCasterUid, OpponentUid: IndependentTargetUid, X: 832, Y: 584,
            AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0, ResponseDamage: 0,
            Effect: MsgAttackEffect.None, Unknown: 0, SpellId: 1000)).ToFrameBytes());
        internal Task DamageOwnerFromIndependent() => Send(IndependentCasterUid, new MsgAttackPacket(new MsgAttackQuery(
            Uid: IndependentCasterUid, OpponentUid: PlayerUid, X: checked((ushort)Owner.Character.X), Y: checked((ushort)Owner.Character.Y),
            AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0, ResponseDamage: 0,
            Effect: MsgAttackEffect.None, Unknown: 0, SpellId: 1000)).ToFrameBytes());
        internal Task MoveDiagonal() => Send(PlayerUid, new MsgMovementPacket
            { Uid = PlayerUid, Direction = 7, Running = MsgMovementMode.Walk, Timestamp = 1234, MapId = 1000 }.ToFrameBytes());
        internal Task Cast(ushort id, uint? target = null, ushort x = 832, ushort y = 584) => Send(PlayerUid,
            new MsgAttackPacket(new MsgAttackQuery(Uid: PlayerUid, OpponentUid: target ?? (id == 7001 ? PlayerUid : TargetPlayerUid),
                X: x, Y: y, AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0, ResponseDamage: 0,
                Effect: MsgAttackEffect.None, Unknown: 0, SpellId: id)).ToFrameBytes());
        internal void Advance(TimeSpan amount) { Assert.False(workers); clock.Advance(amount); }
        internal void QueueUnequip(ushort slot)
        {
            var row = Assert.Single(Owner.PlayerItems.EquipmentItems, item => item.Position == slot);
            queuedGear.Add(new(row.Uid, slot, MsgItemUsageAction.Unequip, 0, 0, 0, 0));
        }
        internal void QueueEquip(uint uid, ushort slot) => queuedGear.Add(new(uid, slot, MsgItemUsageAction.Equip, 0, 0, 0, 0));
        internal async Task DrainQueuedGear()
        {
            var pending = queuedGear.ToArray(); queuedGear.Clear();
            foreach (var packet in pending) await Gear(packet);
        }
        internal Task Unequip(ushort slot)
        {
            var row = Assert.Single(Owner.PlayerItems.EquipmentItems, item => item.Position == slot);
            return Gear(new(row.Uid, slot, MsgItemUsageAction.Unequip, 0, 0, 0, 0));
        }
        private async Task Gear(MsgItemUsagePacket packet)
        {
            var expected = File.ReadAllBytes(ItemPath);
            var count = BinaryPrimitives.ReadInt32LittleEndian(expected);
            for (var i = 0; i < count; i++)
            {
                var offset = 4 + i * PlayerItemSnapshotLoader.LegacyDbItemSize;
                Assert.Equal(0U, BinaryPrimitives.ReadUInt32LittleEndian(expected.AsSpan(offset + 136, 4)));
                var uid = BinaryPrimitives.ReadUInt32LittleEndian(expected.AsSpan(offset, 4));
                var position = BinaryPrimitives.ReadUInt16LittleEndian(expected.AsSpan(offset + 12, 2));
                if (uid == packet.Id || packet.Action == MsgItemUsageAction.Equip && position == packet.DwParam)
                    BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(offset + 12, 2),
                        packet.Action == MsgItemUsageAction.Equip && uid == packet.Id ? checked((ushort)packet.DwParam) : (ushort)0);
            }
            var currency = (Owner.Character.Money, Owner.Character.ConquerPoints, Owner.Character.BoundConquerPoints);
            var registry = File.ReadAllBytes(Data.State);
            gearExpected = expected; gearObserved = false;
            try { await Send(PlayerUid, packet.ToFrameBytes()); }
            finally { gearExpected = null; }
            Assert.True(gearObserved, Output);
            Assert.Equal(expected, File.ReadAllBytes(ItemPath));
            Assert.Equal(currency, (Owner.Character.Money, Owner.Character.ConquerPoints, Owner.Character.BoundConquerPoints));
            Assert.Equal(registry, File.ReadAllBytes(Data.State));
            Assert.False(LegacyTradeJournal.HasPending(Data.Users));
        }

        private bool GlobalLoopStarted() => (int)typeof(WorldServerFrameHandler)
            .GetField("_legacyRuntimeGlobalLoopStarted", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(handler)! == 1;

        private bool WorkersCurrentAndRearmed()
        {
            // Real workers catch/log transport errors before returning. Surface
            // our retained assertion before a generic missing-worker timeout.
            ThrowObservationFailure();
            var expected = connections.Count + 1;
            if (runtimeWorkers.Length != expected || runtimeWorkers.Any(task => task.IsCompleted) ||
                !GlobalLoopStarted() || clock.ScheduledTimerCount != expected) return false;
            var actual = handler.CaptureLegacyRuntimeWorkerTasks();
            return actual.Length == expected && runtimeWorkers.All(actual.Contains);
        }

        internal async Task AdvanceRuntime(TimeSpan amount, Func<bool>? expectedState = null)
        {
            Assert.True(workers);
            await WaitUntilAsync(WorkersCurrentAndRearmed, TimeSpan.FromSeconds(3));
            // Expiry is actual global-loop work, not inferred from elapsed time
            // or from an invented timer. Preserve both sides of the boundary.
            var end = Now.Add(amount);
            var activeFlags = new[] { 8, 9 }.Where(HasFlag)
                .Select(flag => (Flag: flag, Expires: Deadline(flag))).ToArray();
            clock.Advance(amount);
            await WaitUntilAsync(() => WorkersCurrentAndRearmed() &&
                activeFlags.All(entry => HasFlag(entry.Flag) == (entry.Expires > end)) &&
                (expectedState is null || expectedState()), TimeSpan.FromSeconds(3));
            foreach (var peer in loopbacks.Values) await peer.DrainObservations();
            ThrowObservationFailure();
        }
        internal async Task AdvanceRuntimeObserving(TimeSpan amount, Func<bool> expectedState)
        {
            Assert.True(workers);
            await WaitUntilAsync(WorkersCurrentAndRearmed, TimeSpan.FromSeconds(3));
            clock.Advance(amount);
            await WaitUntilAsync(() => WorkersCurrentAndRearmed() && expectedState(), TimeSpan.FromSeconds(3));
            foreach (var peer in loopbacks.Values) await peer.DrainObservations();
            ThrowObservationFailure();
        }
        // The actual one-shot timers fire here; no rearm wait starts while the
        // caller intentionally holds a real peer transport gate.
        internal async Task BeginPausedRuntimeAdvance(TimeSpan amount)
        {
            Assert.True(workers);
            await WaitUntilAsync(WorkersCurrentAndRearmed, TimeSpan.FromSeconds(3));
            clock.Advance(amount);
        }
        internal async Task CompletePausedRuntimeAdvance(Func<bool> expectedState)
        {
            await WaitUntilAsync(() => WorkersCurrentAndRearmed() && expectedState(), TimeSpan.FromSeconds(3));
            foreach (var peer in loopbacks.Values) await peer.DrainObservations();
            ThrowObservationFailure();
        }
        internal async Task EarnXp()
        {
            Assert.True(workers); Assert.False(HasFlag(4));
            Assert.False(Owner.Character.Attributes.TryGetValue("XPCount", out var count) && count != "0");
            var began = Now; var first = Order.Count;
            for (var tick = 1; tick <= 100; tick++)
            {
                var expected = (tick == 100 ? 0 : tick).ToString(CultureInfo.InvariantCulture);
                await AdvanceRuntime(TimeSpan.FromSeconds(8), () => Owner.Character.Attributes.TryGetValue("XPCount", out var actual) &&
                    actual == expected && HasFlag(4) == (tick == 100));
                Assert.Equal(expected, Owner.Character.Attributes["XPCount"]);
                Assert.Equal(tick == 100, HasFlag(4));
            }
            Assert.Equal(began.AddSeconds(800), Now);
            Assert.Equal(Enumerable.Range(1, 100).Select(value => (ulong)value), ParseMsgUpdates(FramesSince(PlayerUid, first))
                .Where(packet => packet.Uid == PlayerUid).SelectMany(packet => packet.Updates)
                .Where(row => row.Type == LegacyMsgUpdateDataType.XPCircle).Select(row => row.Value));
        }

        public async ValueTask DisposeAsync()
        {
            OnSend = null; OnSendAsync = null;
            var errors = new List<Exception>();
            bool transportsDrained = true;
            foreach (var connection in connections.Values)
            {
                try { await connection.DisconnectAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (Exception error) { transportsDrained = false; errors.Add(new InvalidOperationException("Owned Root fixture disconnect did not drain.", error)); }
                try { await connection.CompleteTransportCloseAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (Exception error) { transportsDrained = false; errors.Add(new InvalidOperationException("Owned Root fixture transport cleanup did not drain.", error)); }
            }
            bool runtimeDisposed = runtime is null, workersDrained = !workers;
            try
            {
                if (runtime is not null)
                {
                    await runtime.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                    runtimeDisposed = true;
                }
            }
            catch (Exception error) { errors.Add(new InvalidOperationException("Owned Root fixture runtime shutdown did not drain.", error)); }
            if (workers && runtimeDisposed)
            {
                try
                {
                    await Task.WhenAll(runtimeWorkers).WaitAsync(TimeSpan.FromSeconds(5));
                    Assert.All(runtimeWorkers, task => Assert.True(task.IsCompleted));
                    Assert.Equal(0, clock.ScheduledTimerCount);
                    workersDrained = true;
                }
                catch (Exception error) { errors.Add(new InvalidOperationException("Owned Root fixture workers did not drain.", error)); }
            }
            // Do not restore shared metadata, dispose the log or remove owned
            // evidence while any transport/runtime/worker may still use them.
            bool drained = transportsDrained && runtimeDisposed && workersDrained;
            if (!drained) RetainEvidence = true;
            var unreported = Volatile.Read(ref observationFailure);
            if (unreported is not null && !ReferenceEquals(unreported, Volatile.Read(ref reportedObservationFailure)) &&
                !errors.Any(error => ReferenceEquals(error, unreported)))
            {
                RetainEvidence = true;
                errors.Add(unreported);
            }
            if (drained)
            {
                foreach (var peer in loopbacks.Values) try { await peer.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
                try { nativeGearMetadata?.Dispose(); } catch (Exception error) { errors.Add(error); }
                try { beasts.Dispose(); } catch (Exception error) { errors.Add(error); }
                try { log.Dispose(); } catch (Exception error) { errors.Add(error); }
                if (errors.Count == 0 && !RetainEvidence) Data.Dispose();
            }
            else if (nativeGearMetadata is not null)
                errors.Add(new InvalidOperationException("Native gear metadata retained because the actual fixture did not drain."));
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException(errors);
        }

    }
}
