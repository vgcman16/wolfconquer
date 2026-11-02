using System.Buffers;
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

// Consumers belong to NativeItemCounterSourcePrerequisite. Only owned test files
// are seeded, before actual bootstrap; every action uses the real Frame route.
internal sealed partial class OwnedEquipmentBehaviorFixture : IAsyncDisposable
{
    internal readonly LegacyOwnedAdmissionRuntimeFixture Data = new(bootstrap: false);
    private readonly NativeLoginBeastMetadataScope beasts = new(1000);
    private readonly Dictionary<uint, List<byte[]>> frames = new();
    private readonly Dictionary<uint, ConquerTcpConnection> connections = new();
    private readonly Dictionary<uint, RootLoopbackFramePeer> wirePeers = new();
    private readonly List<CancellationTokenSource> closed = [];
    private Exception? observationFailure;
    private NativeCombatGearProcFixture? nativeGearMetadata;
    internal LegacyNativeCombatGearRuntime? NativeCombatGear => nativeGearMetadata?.Runtime;
    private uint actorUid;
    internal readonly SynchronizedFixtureLog Log = new();
    internal LegacyOwnedAdmissionRuntime Runtime { get; private set; } = null!;
    internal WorldServerFrameHandler Handler { get; private set; } = null!;
    internal bool RetainEvidence;
    internal byte[]? OmittedOptionalScalarInput { get; private set; }
    internal Action<uint, byte[]>? WireFrameRecorded;
    internal Action<uint, byte[]>? BeforeSend;
    internal Func<uint, byte[], ValueTask>? BeforeSendAsync;
    internal List<byte[]> ActorFrames => Frames(actorUid);
    internal WorldSessionStore Store => Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
        .GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Handler));
    internal LegacyTradeRecoveryState Recovery => Assert.IsType<LegacyTradeRecoveryState>(typeof(WorldServerFrameHandler)
        .GetField("_tradeRecovery", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Handler));

    internal static Task RunAsync(OwnedEquipmentBehaviorInput actor, WorldDataCatalog catalog,
        Func<OwnedEquipmentBehaviorFixture, Task> exercise, params OwnedEquipmentBehaviorInput[] peers) =>
        RunWithOptionsAsync(actor, catalog, LegacyRuntimeUpdateLoopOptions.Disabled, exercise, peers);

    internal static Task RunWithNativeGearAsync(OwnedEquipmentBehaviorInput actor, WorldDataCatalog catalog,
        Func<OwnedEquipmentBehaviorFixture, Task> exercise, params OwnedEquipmentBehaviorInput[] peers) =>
        RunActualAsync(actor, catalog, LegacyRuntimeUpdateLoopOptions.Disabled, exercise, peers, useNativeGear: true);

    internal static async Task RunWithNativeGearOptionsAsync(OwnedEquipmentBehaviorInput actor, WorldDataCatalog catalog,
        LegacyRuntimeUpdateLoopOptions options, Func<OwnedEquipmentBehaviorFixture, Task> exercise,
        params OwnedEquipmentBehaviorInput[] peers)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Enabled || options.TimeProvider is null || options.TickInterval <= TimeSpan.Zero)
            throw new ArgumentException("Native gear options require disabled scheduling, a real clock and positive interval.", nameof(options));
        await RunActualAsync(actor, catalog, options, exercise, peers, useNativeGear: true);
    }

    // Real native table publisher plus genuine enabled Frame workers. Clock
    // callbacks never grant admission, cast authority or a successful outcome.
    internal static async Task RunWithNativeGearAndSchedulingAsync(OwnedEquipmentBehaviorInput actor, WorldDataCatalog catalog,
        LegacyRuntimeUpdateLoopOptions options, Func<OwnedEquipmentBehaviorFixture, Task> exercise,
        params OwnedEquipmentBehaviorInput[] peers)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Enabled || options.TimeProvider is null || options.TickInterval <= TimeSpan.Zero)
            throw new ArgumentException("Native gear scheduling requires enabled options, a real clock and positive interval.", nameof(options));
        await RunActualAsync(actor, catalog, options, exercise, peers, useNativeGear: true);
    }

    internal static async Task RunWithOptionsAsync(OwnedEquipmentBehaviorInput actor, WorldDataCatalog catalog,
        LegacyRuntimeUpdateLoopOptions options, Func<OwnedEquipmentBehaviorFixture, Task> exercise,
        params OwnedEquipmentBehaviorInput[] peers)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Enabled || options.TimeProvider is null)
            throw new ArgumentException("Owned equipment behavior fixtures require explicit disabled runtime options with a real clock.", nameof(options));
        await RunActualAsync(actor, catalog, options, exercise, peers);
    }

    internal static async Task RunWithEnabledSchedulingAsync(OwnedEquipmentBehaviorInput actor, WorldDataCatalog catalog,
        LegacyRuntimeUpdateLoopOptions options, Func<OwnedEquipmentBehaviorFixture, Task> exercise,
        params OwnedEquipmentBehaviorInput[] peers)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Enabled || options.TimeProvider is null)
            throw new ArgumentException("Enabled scheduling fixtures require enabled runtime options and a real clock.", nameof(options));
        await RunActualAsync(actor, catalog, options, exercise, peers);
    }

    private static async Task RunActualAsync(OwnedEquipmentBehaviorInput actor, WorldDataCatalog catalog,
        LegacyRuntimeUpdateLoopOptions options, Func<OwnedEquipmentBehaviorFixture, Task> exercise,
        OwnedEquipmentBehaviorInput[] peers, bool useNativeGear = false, LegacyGameMapTerrainQuery? terrain = null)
    {
        OwnedEquipmentBehaviorFixture? fixture = null;
        Exception? primary = null;
        try
        {
            fixture = new OwnedEquipmentBehaviorFixture();
            await fixture.InitializeAsync(actor, catalog, options, peers, useNativeGear, terrain);
            await exercise(fixture);
        }
        catch (Exception error) { primary = error; }
        Exception? cleanup = null;
        if (fixture is not null)
            try { fixture.RetainEvidence = primary is not null; await fixture.DisposeAsync(); }
            catch (Exception error) { cleanup = error; }
        if (primary is not null && cleanup is not null) throw new AggregateException(primary, cleanup);
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        if (cleanup is not null) ExceptionDispatchInfo.Capture(cleanup).Throw();
    }

    private async Task InitializeAsync(OwnedEquipmentBehaviorInput actor, WorldDataCatalog catalog,
        LegacyRuntimeUpdateLoopOptions options, OwnedEquipmentBehaviorInput[] peers, bool useNativeGear, LegacyGameMapTerrainQuery? terrain = null)
    {
        ArgumentNullException.ThrowIfNull(actor); ArgumentNullException.ThrowIfNull(catalog);
        var inputs = new[] { actor }.Concat(peers).ToArray();
        if (inputs.Any(input => input?.Character is null || input.Items is null || input.Character.Uid is <= 0 or > uint.MaxValue) ||
            inputs.Select(input => input.Character.Uid).Distinct().Count() != inputs.Length)
            throw new ArgumentException("Owned test inputs require distinct valid saved character identities.");
        actorUid = checked((uint)actor.Character.Uid);
        foreach (var input in inputs) Save(input);
        ApplyProficiencyInputImage();
        if (useNativeGear)
        {
            nativeGearMetadata = new NativeCombatGearProcFixture(Data.Root);
            nativeGearMetadata.Load(catalog);
        }
        // The shared Data fixture's original, unadmitted sentinel account remains
        // part of the actual scanned dataset unless it is one supplied input.
        Data.Bootstrap();
        Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out var runtime, out var reason), reason);
        Runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(runtime);
        Handler = new WorldServerFrameHandler(Data.Loader, null, catalog, options,
            Log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: Runtime, nativeCombatGear: NativeCombatGear, terrain: terrain);
        foreach (var input in inputs)
        {
            var uid = checked((uint)input.Character.Uid);
            var output = new List<byte[]>();
            frames.Add(uid, output);
            var native = new LegacyNativeGameClientRuntime();
            var cancellation = new CancellationTokenSource();
            closed.Add(cancellation);
            ConquerTcpConnection connection;
            if (options.Enabled)
            {
                // Runtime-owned writes must reach the same real TCP gate and
                // registered writer as production. The reader records wire
                // order independently of business observers/reentrant sends.
                var peer = new RootLoopbackFramePeer(bytes =>
                {
                    lock (output) output.Add(bytes);
                    WireFrameRecorded?.Invoke(uid, bytes);
                }, async bytes =>
                {
                    try
                    {
                        BeforeSend?.Invoke(uid, bytes);
                        if (BeforeSendAsync is { } observer) await observer(uid, bytes).ConfigureAwait(false);
                    }
                    catch (Exception error) { Interlocked.CompareExchange(ref observationFailure, error, null); throw; }
                }, native);
                wirePeers.Add(uid, peer);
                connection = peer.Connection;
            }
            else connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                async (memory, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    var bytes = memory.ToArray();
                    try
                    {
                        BeforeSend?.Invoke(uid, bytes);
                        if (BeforeSendAsync is { } beforeSendAsync)
                            await beforeSendAsync(uid, bytes).ConfigureAwait(false);
                    }
                    catch (Exception error) { observationFailure ??= error; throw; }
                    lock (output) output.Add(bytes);
                }, _ => { cancellation.Cancel(); return ValueTask.CompletedTask; }, native);
            connections.Add(uid, connection);
            await SendAsync(uid, new LegacyMsgLoginGamePacket(uid).ToFrameBytes());
            var admitted = Current(uid);
            Assert.Equal((long)uid, admitted.AccountId);
            Assert.Equal((long)uid, admitted.Character.Uid);
            Assert.Same(native, admitted.NativeGameClient);
            Assert.NotNull(native.OrdinarySpellBinding);
            Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(connection, admitted));
            Assert.True(Recovery.IsReadyFor(Data.Users, Data.Items));
            if (options.Enabled)
            {
                var workers = Handler.CaptureLegacyRuntimeWorkerTasks();
                Assert.True(Log.ToString().Contains($"WorldServer legacy MsgLoginGame Success key={uid} ", StringComparison.Ordinal),
                    $"Admitted login did not finish startup before scheduling. Workers={workers.Length}; log:\n{Log}");
                Assert.True(workers.Length >= 2 && workers.All(worker => !worker.IsCompleted),
                    $"Completed login did not retain both live runtime workers. Workers={workers.Length}, " +
                    $"states={string.Join(",", workers.Select(worker => worker.Status))}, " +
                    $"faulted={Handler.LegacyRuntimeFaultedWorkerCount}; log:\n{Log}");
            }
        }
    }

    private void Save(OwnedEquipmentBehaviorInput input)
    {
        var uid = input.Character.Uid;
        var values = new Dictionary<string, string>(input.Character.Attributes, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in CharacterSnapshotScalarPersistence.BuildPersistedValues(input.Character)) values[pair.Key] = pair.Value;
        // Declared saved-input variant, applied before real bootstrap/admission.
        // Parser semantics are MinMana fallback for MaxMana and zero for ExpBallUsed.
        if (input.OmitOptionalCharacterScalars)
        { values.Remove("MaxMana"); values.Remove("ExpBallUsed"); }
        values["Name"] = input.Character.Name;
        // Positive equipment fixtures explicitly use the genuine no-PIN stored
        // form. PIN-denial tests alter the real binding after actual admission.
        if (!values.ContainsKey("SecurityPass")) values["SecurityPass"] = "0,0,0";
        var text = "[Character]\r\n" + string.Join("\r\n", values.Select(pair => pair.Key + "=" + pair.Value)) + "\r\n";
        if (input.Character.PaidNobility.Count != 0)
            text += "[PaidNobility]\r\n" + string.Join("\r\n", input.Character.PaidNobility.Select(pair => pair.Key + "=" + pair.Value)) + "\r\n";
        File.WriteAllText(CharacterPath(checked((uint)uid)), text, new UTF8Encoding(true));
        if (input.OmitOptionalCharacterScalars)
            OmittedOptionalScalarInput = File.ReadAllBytes(CharacterPath(checked((uint)uid)));
        new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(uid, input.Items);
        if (input.OmitSpellsFile)
        {
            // Declared saved DATA before Bootstrap/admission. Never delete or
            // replace a bound owner's file to manufacture a missing baseline.
            if (input.Spells is { Spells.Count: > 0 })
                throw new ArgumentException("Missing Spells input cannot contain learned rows.", nameof(input));
            Assert.False(File.Exists(Path.Combine(Data.Spells, uid.ToString(CultureInfo.InvariantCulture) + ".bin")));
        }
        else new PlayerSpellSnapshotLoader(Data.Spells).SaveByCharacterUid(uid, input.Spells ?? PlayerSpellSnapshot.Empty);
        new PlayerProficiencySnapshotLoader(Data.Proficiencies).SaveByCharacterUid(uid, input.Proficiencies ?? PlayerProficiencySnapshot.Empty);
        new PlayerQuestSnapshotLoader(Data.Quests).SaveByCharacterUid(uid, input.Character.PlayerQuests);
        new CombatGearSnapshotLoader(Data.CombatGear).SaveByCharacterUid(uid, input.CombatGear ?? CombatGearSnapshot.Empty);
        new LegacyMailboxStore(Data.Root).Save(checked((uint)uid));
    }

    internal List<byte[]> Frames(uint uid) => frames[uid];
    internal List<byte[]> SnapshotFrames(uint uid) { var output = frames[uid]; lock (output) return [.. output]; }
    internal void ClearRecordedFrames(uint uid) { var output = frames[uid]; lock (output) output.Clear(); }
    internal ConquerTcpConnection Connection(uint uid) => connections[uid];
    internal bool HasActualWire(uint uid = Actor) => wirePeers.ContainsKey(uid);
    internal RootLoopbackFramePeer WirePeer(uint uid = Actor) => wirePeers.TryGetValue(uid, out var peer)
        ? peer : throw new InvalidOperationException("This fixture was not started with enabled real-wire scheduling.");
    internal async Task DrainWireObservationsAsync()
    {
        foreach (var peer in wirePeers.Values) await peer.DrainObservations().ConfigureAwait(false);
        if (observationFailure is { } failure) ExceptionDispatchInfo.Capture(failure).Throw();
    }
    internal string CharacterPath(uint uid = Actor) => Path.Combine(Data.Users, uid.ToString(CultureInfo.InvariantCulture) + ".ini");
    internal string ItemPath(uint uid = Actor) => Path.Combine(Data.Items, uid.ToString(CultureInfo.InvariantCulture) + ".bin");
    internal WorldSession Current(uint uid = Actor) => Assert.IsType<WorldSession>(Store.TryGet(Connection(uid), out var current) ? current : null);
    internal async Task SendAsync(uint uid, byte[] frame)
    {
        await Handler.HandleFrameAsync(Connection(uid), ReadFrame(frame));
        if (wirePeers.TryGetValue(uid, out var peer)) await peer.DrainObservations().ConfigureAwait(false);
        if (observationFailure is { } failure) ExceptionDispatchInfo.Capture(failure).Throw();
    }
    internal Task DispatchAsync(MsgItemUsagePacket packet, uint uid = Actor) => SendAsync(uid, packet.ToFrameBytes());
    internal Task ToggleAsync(uint uid = Actor) => DispatchAsync(new MsgItemUsagePacket(0, 0, MsgItemUsageAction.Alternante, 0, 0, 0, 0), uid);

    internal static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    public async ValueTask DisposeAsync()
    {
        var errors = new List<Exception>();
        var runtimeDrained = Runtime is null;
        if (wirePeers.Count != 0)
        {
            // Release only the test filler, then drain actual producers and
            // observations before disconnecting either socket endpoint.
            foreach (var peer in wirePeers.Values) peer.ReleaseBackpressuredWrite();
            try { if (Runtime is not null) await Runtime.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(20)); runtimeDrained = true; }
            catch (Exception error) { errors.Add(error); }
            foreach (var peer in wirePeers.Values)
                try { if (!peer.ReaderAborted) await peer.DrainObservations(); } catch (Exception error) { errors.Add(error); }
            if (observationFailure is { } observation) errors.Add(observation);
        }
        BeforeSend = null;
        BeforeSendAsync = null;
        WireFrameRecorded = null;
        foreach (var connection in connections.Values)
        {
            try { await connection.DisconnectAsync(); } catch (Exception error) { errors.Add(error); }
            try { await connection.CompleteTransportCloseAsync(); } catch (Exception error) { errors.Add(error); }
        }
        if (wirePeers.Count == 0)
            try { if (Runtime is not null) await Runtime.DisposeAsync(); runtimeDrained = true; }
            catch (Exception error) { errors.Add(error); }
        foreach (var peer in wirePeers.Values)
            try { await peer.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
        if (runtimeDrained)
            try { nativeGearMetadata?.Dispose(); } catch (Exception error) { errors.Add(error); }
        else if (nativeGearMetadata is not null)
            errors.Add(new InvalidOperationException("Native gear metadata retained because the actual runtime did not drain."));
        foreach (var cancellation in closed) try { cancellation.Dispose(); } catch (Exception error) { errors.Add(error); }
        try { beasts.Dispose(); } catch (Exception error) { errors.Add(error); }
        try { Log.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 0 && !RetainEvidence) Data.Dispose();
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }
}
