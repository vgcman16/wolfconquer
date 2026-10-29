using System.Buffers;
using System.Collections.Concurrent;
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

public sealed partial class LegacyOwnedPhysicalMutableStatusEquipmentTests
{
    private const uint PlayerUid = LegacyOwnedAdmissionRuntimeFixture.Uid;
    private const uint TargetPlayerUid = PlayerUid + 1;
    private const ushort FineRainAttachStatusSpellId = 14_500;

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private sealed class OwnedEquipmentFixture : IAsyncDisposable
    {
        internal readonly LegacyOwnedAdmissionRuntimeFixture Data = new(bootstrap: false);
        private readonly NativeLoginBeastMetadataScope beasts = new(1000);
        internal readonly ConcurrentQueue<byte[]> Captures = new(), AllFrames = new();
        internal readonly DirectOverlayEquipmentTimeProvider Clock = new();
        internal readonly StringWriter Log = new();
        internal readonly Dictionary<uint, ConquerTcpConnection> Connections = new();
        internal readonly Dictionary<uint, LegacyNativeGameClientRuntime> Natives = new();
        internal LegacyOwnedAdmissionRuntime Runtime = null!;
        internal WorldServerFrameHandler Handler = null!;
        internal WorldDataCatalog World = null!;
        internal Func<uint, byte[], ValueTask>? OnSend;
        internal bool RetainEvidence;
        internal WorldSessionStore Sessions => Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
            .GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Handler));
        internal WorldSession Owner => Current(PlayerUid);
        internal string ItemsPath => Path.Combine(Data.Items, PlayerUid + ".bin");

        internal async Task Initialize(string scenario)
        {
            var inputs = LegacyMagicAttackRuntimeTests.CreateOwnedPhysicalEquipmentInputs(scenario, PlayerUid, TargetPlayerUid);
            World = inputs.World;
            Save(inputs.Owner); Save(inputs.Striker);
            Data.Bootstrap();
            Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out var runtime, out var reason), reason);
            Runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(runtime);
            var options = new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), Clock,
                LegacySignedMillisecondClock.FromTimeProvider(Clock, 10_000));
            Handler = new WorldServerFrameHandler(Data.Loader, null, World, options, Log,
                protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: Runtime);
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            {
                var native = new LegacyNativeGameClientRuntime();
                Natives.Add(uid, native);
                Connections.Add(uid, new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                    async (memory, token) =>
                    {
                        token.ThrowIfCancellationRequested();
                        var bytes = memory.ToArray();
                        AllFrames.Enqueue(bytes);
                        if (uid == PlayerUid) Captures.Enqueue(bytes);
                        if (OnSend is { } actualObserver) await actualObserver(uid, bytes);
                    }, _ => ValueTask.CompletedTask, native));
                await Dispatch(uid, new LegacyMsgLoginGamePacket(uid).ToFrameBytes());
                var current = Current(uid);
                Assert.Equal((long)uid, current.AccountId);
                Assert.Equal((long)uid, current.Character.Uid);
                Assert.Same(native, current.NativeGameClient);
                Assert.NotNull(native.OrdinarySpellBinding);
                Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(Connections[uid], current));
            }
            if (inputs.Owner.AlternateEquipmentActive)
            {
                await Dispatch(PlayerUid, new MsgItemUsagePacket(0, 0, MsgItemUsageAction.Alternante, 0, 0, 0, 0).ToFrameBytes());
                Assert.True(Owner.AlternateEquipmentActive);
            }
            Captures.Clear(); AllFrames.Clear();
        }

        private void Save(WorldSession input)
        {
            var uid = input.Character.Uid;
            var fields = new Dictionary<string, string>(input.Character.Attributes, StringComparer.OrdinalIgnoreCase);
            foreach (var pair in CharacterSnapshotScalarPersistence.BuildPersistedValues(input.Character)) fields[pair.Key] = pair.Value;
            fields["Name"] = input.Character.Name;
            fields["SecurityPass"] = "0,0,0";
            // Saved complete real loader inputs before any registry/bootstrap.
            var path = Path.Combine(Data.Users, uid + ".ini");
            File.WriteAllText(path, "[Character]\r\n" + string.Join("\r\n", fields.Select(pair => pair.Key + "=" + pair.Value)) + "\r\n",
                new UTF8Encoding(true));
            new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(uid, input.PlayerItems);
            new PlayerSpellSnapshotLoader(Data.Spells).SaveByCharacterUid(uid, input.PlayerSpells);
            new PlayerProficiencySnapshotLoader(Data.Proficiencies).SaveByCharacterUid(uid, input.PlayerProficiencies);
            new PlayerQuestSnapshotLoader(Data.Quests).SaveByCharacterUid(uid, PlayerQuestSnapshot.Empty);
            new CombatGearSnapshotLoader(Data.CombatGear).SaveByCharacterUid(uid, input.CombatGear ?? CombatGearSnapshot.Empty);
            new LegacyMailboxStore(Data.Root).Save(checked((uint)uid));
        }

        internal WorldSession Current(uint uid)
        {
            Assert.True(Sessions.TryGet(Connections[uid], out var current));
            return Assert.IsType<WorldSession>(current);
        }

        internal Task Dispatch(uint uid, byte[] bytes) => Handler.HandleFrameAsync(Connections[uid], ReadFrame(bytes)).AsTask();

        internal async Task<LegacyPlayerStatusPacket> QueryAsync()
        {
            var start = Captures.Count;
            await Dispatch(PlayerUid, new LegacyPlayerStatusQueryPacket(PlayerUid).ToFrameBytes());
            var bytes = Assert.Single(Captures.ToArray().Skip(start), candidate => LegacyPlayerStatusPacket.TryParse(ReadFrame(candidate), out _));
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out var packet));
            return packet;
        }

        public async ValueTask DisposeAsync()
        {
            OnSend = null;
            var errors = new List<Exception>();
            foreach (var connection in Connections.Values)
                try { await connection.DisconnectAsync(); await connection.CompleteTransportCloseAsync(); }
                catch (Exception error) { errors.Add(error); }
            try { if (Runtime is not null) await Runtime.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
            try { beasts.Dispose(); Log.Dispose(); } catch (Exception error) { errors.Add(error); }
            if (errors.Count == 0 && !RetainEvidence) Data.Dispose();
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException(errors);
        }
    }

    private sealed class DirectOverlayEquipmentTimeProvider : TimeProvider
    {
        private long ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => ticks;
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero).AddTicks(ticks);
        public void AdvanceMilliseconds(long milliseconds) => ticks = checked(ticks + milliseconds * TimeSpan.TicksPerMillisecond);
    }
}
