using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
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

    internal sealed class OwnedRootEquipmentFixture : IAsyncDisposable
    {
        internal readonly LegacyOwnedAdmissionRuntimeFixture Data = new(bootstrap: false);
        private readonly NativeLoginBeastMetadataScope beasts = new(1000);
        private readonly ManualRuntimeTimeProvider clock = new(RootMagicEntryOrigin);
        private readonly Dictionary<uint, ConquerTcpConnection> connections = [];
        private readonly List<MsgItemUsagePacket> queuedGear = [];
        private readonly StringWriter log = new();
        private LegacyOwnedAdmissionRuntime runtime = null!;
        private WorldServerFrameHandler handler = null!;
        private bool workers;
        private Task[] runtimeWorkers = [];
        private Exception? observationFailure;
        private Exception? reportedObservationFailure;
        private byte[]? gearExpected;
        private bool gearObserved;
        internal bool RetainEvidence;
        internal Action<OwnedRootEquipmentFixture, uint, byte[]>? OnSend;
        internal readonly ConcurrentQueue<(uint Recipient, byte[] Bytes)> Order = new();
        internal WorldSessionStore Sessions => Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
            .GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(handler));
        internal WorldSession Owner => Current(PlayerUid);
        internal WorldSession Peer => Current(TargetPlayerUid);
        internal DateTimeOffset Now => clock.GetUtcNow();
        internal string Output => log.ToString();
        internal string ItemPath => Path.Combine(Data.Items, PlayerUid + ".bin");
        internal WorldSession Current(uint uid) => Assert.IsType<WorldSession>(Sessions.TryGet(connections[uid], out var owner) ? owner : null);
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
            workers = scenario is "xp" or "manual";
            var inputs = OwnedRootMagicInputs(scenario);
            Save(inputs.Owner); Save(inputs.Peer);
            Data.Bootstrap();
            Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out var opened, out var reason), reason);
            runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(opened);
            handler = new WorldServerFrameHandler(Data.Loader, null, inputs.World,
                new LegacyRuntimeUpdateLoopOptions(workers, TimeSpan.FromMilliseconds(20), clock,
                    LegacySignedMillisecondClock.FromTimeProvider(clock, 10000)), log,
                protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: runtime);
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            {
                var native = new LegacyNativeGameClientRuntime();
                connections.Add(uid, new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                    (memory, token) =>
                    {
                        token.ThrowIfCancellationRequested();
                        var bytes = memory.ToArray(); Order.Enqueue((uid, bytes));
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
                        }
                        catch (Exception error) { Interlocked.CompareExchange(ref observationFailure, error, null); throw; }
                        return ValueTask.CompletedTask;
                    }, _ => ValueTask.CompletedTask, native));
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
                    var expected = connections.Count == 1 ? 2 : 3;
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
        internal Task Query() => Send(PlayerUid, new MsgActionPacket { ObjId = PlayerUid, Type = MsgActionType.AllowAnimation }.ToFrameBytes());
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
            if (runtimeWorkers.Length != 3 || runtimeWorkers.Any(task => task.IsCompleted) ||
                !GlobalLoopStarted() || clock.ScheduledTimerCount != 3) return false;
            var actual = handler.CaptureLegacyRuntimeWorkerTasks();
            return actual.Length == 3 && runtimeWorkers.All(actual.Contains);
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
            OnSend = null;
            var errors = new List<Exception>();
            foreach (var connection in connections.Values)
            {
                try { await connection.DisconnectAsync(); }
                catch (Exception error) { errors.Add(error); }
                try { await connection.CompleteTransportCloseAsync(); }
                catch (Exception error) { errors.Add(error); }
            }
            var runtimeDisposed = false;
            try { if (runtime is not null) { await runtime.DisposeAsync(); runtimeDisposed = true; } } catch (Exception error) { errors.Add(error); }
            try
            {
                if (workers && runtimeDisposed)
                {
                    await Task.WhenAll(runtimeWorkers);
                    Assert.All(runtimeWorkers, task => Assert.True(task.IsCompleted));
                    Assert.Equal(0, clock.ScheduledTimerCount);
                }
            }
            catch (Exception error) { errors.Add(error); }
            // A sender failure can race the last exercise assertion. Actual
            // worker join precedes this check; never delete its evidence or
            // silently turn it green, nor duplicate an already reported primary.
            var unreported = Volatile.Read(ref observationFailure);
            if (unreported is not null && !ReferenceEquals(unreported, Volatile.Read(ref reportedObservationFailure)) &&
                !errors.Any(error => ReferenceEquals(error, unreported)))
            {
                RetainEvidence = true;
                errors.Add(unreported);
            }
            try { beasts.Dispose(); } catch (Exception error) { errors.Add(error); }
            try { log.Dispose(); } catch (Exception error) { errors.Add(error); }
            if (errors.Count == 0 && !RetainEvidence) Data.Dispose();
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException(errors);
        }
    }
}
