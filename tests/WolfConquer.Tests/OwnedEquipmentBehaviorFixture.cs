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
    private readonly List<CancellationTokenSource> closed = [];
    private Exception? observationFailure;
    private uint actorUid;
    internal readonly StringWriter Log = new();
    internal LegacyOwnedAdmissionRuntime Runtime { get; private set; } = null!;
    internal WorldServerFrameHandler Handler { get; private set; } = null!;
    internal bool RetainEvidence;
    internal Action<uint, byte[]>? BeforeSend;
    internal List<byte[]> ActorFrames => Frames(actorUid);
    internal WorldSessionStore Store => Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
        .GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Handler));
    internal LegacyTradeRecoveryState Recovery => Assert.IsType<LegacyTradeRecoveryState>(typeof(WorldServerFrameHandler)
        .GetField("_tradeRecovery", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Handler));

    internal static Task RunAsync(OwnedEquipmentBehaviorInput actor, WorldDataCatalog catalog,
        Func<OwnedEquipmentBehaviorFixture, Task> exercise, params OwnedEquipmentBehaviorInput[] peers) =>
        RunWithOptionsAsync(actor, catalog, LegacyRuntimeUpdateLoopOptions.Disabled, exercise, peers);

    internal static async Task RunWithOptionsAsync(OwnedEquipmentBehaviorInput actor, WorldDataCatalog catalog,
        LegacyRuntimeUpdateLoopOptions options, Func<OwnedEquipmentBehaviorFixture, Task> exercise,
        params OwnedEquipmentBehaviorInput[] peers)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Enabled || options.TimeProvider is null)
            throw new ArgumentException("Owned equipment behavior fixtures require explicit disabled runtime options with a real clock.", nameof(options));
        OwnedEquipmentBehaviorFixture? fixture = null;
        Exception? primary = null;
        try
        {
            fixture = new OwnedEquipmentBehaviorFixture();
            await fixture.InitializeAsync(actor, catalog, options, peers);
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
        LegacyRuntimeUpdateLoopOptions options, OwnedEquipmentBehaviorInput[] peers)
    {
        ArgumentNullException.ThrowIfNull(actor); ArgumentNullException.ThrowIfNull(catalog);
        var inputs = new[] { actor }.Concat(peers).ToArray();
        if (inputs.Any(input => input?.Character is null || input.Items is null || input.Character.Uid is <= 0 or > uint.MaxValue) ||
            inputs.Select(input => input.Character.Uid).Distinct().Count() != inputs.Length)
            throw new ArgumentException("Owned test inputs require distinct valid saved character identities.");
        actorUid = checked((uint)actor.Character.Uid);
        foreach (var input in inputs) Save(input);
        // The shared Data fixture's original, unadmitted sentinel account remains
        // part of the actual scanned dataset unless it is one supplied input.
        Data.Bootstrap();
        Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out var runtime, out var reason), reason);
        Runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(runtime);
        Handler = new WorldServerFrameHandler(Data.Loader, null, catalog, options,
            Log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: Runtime);
        foreach (var input in inputs)
        {
            var uid = checked((uint)input.Character.Uid);
            var output = new List<byte[]>();
            frames.Add(uid, output);
            var native = new LegacyNativeGameClientRuntime();
            var cancellation = new CancellationTokenSource();
            closed.Add(cancellation);
            var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                (memory, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    var bytes = memory.ToArray();
                    try { BeforeSend?.Invoke(uid, bytes); }
                    catch (Exception error) { observationFailure ??= error; throw; }
                    output.Add(bytes);
                    return ValueTask.CompletedTask;
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
        }
    }

    private void Save(OwnedEquipmentBehaviorInput input)
    {
        var uid = input.Character.Uid;
        var values = new Dictionary<string, string>(input.Character.Attributes, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in CharacterSnapshotScalarPersistence.BuildPersistedValues(input.Character)) values[pair.Key] = pair.Value;
        values["Name"] = input.Character.Name;
        // Positive equipment fixtures explicitly use the genuine no-PIN stored
        // form. PIN-denial tests alter the real binding after actual admission.
        if (!values.ContainsKey("SecurityPass")) values["SecurityPass"] = "0,0,0";
        var text = "[Character]\r\n" + string.Join("\r\n", values.Select(pair => pair.Key + "=" + pair.Value)) + "\r\n";
        if (input.Character.PaidNobility.Count != 0)
            text += "[PaidNobility]\r\n" + string.Join("\r\n", input.Character.PaidNobility.Select(pair => pair.Key + "=" + pair.Value)) + "\r\n";
        File.WriteAllText(CharacterPath(checked((uint)uid)), text, new UTF8Encoding(true));
        new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(uid, input.Items);
        new PlayerSpellSnapshotLoader(Data.Spells).SaveByCharacterUid(uid, input.Spells ?? PlayerSpellSnapshot.Empty);
        new PlayerProficiencySnapshotLoader(Data.Proficiencies).SaveByCharacterUid(uid, input.Proficiencies ?? PlayerProficiencySnapshot.Empty);
        new PlayerQuestSnapshotLoader(Data.Quests).SaveByCharacterUid(uid, input.Character.PlayerQuests);
        new CombatGearSnapshotLoader(Data.CombatGear).SaveByCharacterUid(uid, input.CombatGear ?? CombatGearSnapshot.Empty);
        new LegacyMailboxStore(Data.Root).Save(checked((uint)uid));
    }

    internal List<byte[]> Frames(uint uid) => frames[uid];
    internal ConquerTcpConnection Connection(uint uid) => connections[uid];
    internal string CharacterPath(uint uid = Actor) => Path.Combine(Data.Users, uid.ToString(CultureInfo.InvariantCulture) + ".ini");
    internal string ItemPath(uint uid = Actor) => Path.Combine(Data.Items, uid.ToString(CultureInfo.InvariantCulture) + ".bin");
    internal WorldSession Current(uint uid = Actor) => Assert.IsType<WorldSession>(Store.TryGet(Connection(uid), out var current) ? current : null);
    internal async Task SendAsync(uint uid, byte[] frame)
    {
        await Handler.HandleFrameAsync(Connection(uid), ReadFrame(frame));
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
        BeforeSend = null;
        var errors = new List<Exception>();
        foreach (var connection in connections.Values)
        {
            try { await connection.DisconnectAsync(); } catch (Exception error) { errors.Add(error); }
            try { await connection.CompleteTransportCloseAsync(); } catch (Exception error) { errors.Add(error); }
        }
        try { if (Runtime is not null) await Runtime.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
        foreach (var cancellation in closed) try { cancellation.Dispose(); } catch (Exception error) { errors.Add(error); }
        try { beasts.Dispose(); } catch (Exception error) { errors.Add(error); }
        try { Log.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 0 && !RetainEvidence) Data.Dispose();
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }
}
