using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class OwnedNativeCpsPublicationTests
{
    private static async Task WithCpsFrame(long cps, long bound, long bank, Func<CpsFrameFixture, Task> body)
    {
        CpsFrameFixture? fixture = null; Exception? primary = null;
        try { fixture = new(); fixture.Initialize(cps, bound, bank); await body(fixture); }
        catch (Exception error) { primary = error; }
        var cleanup = fixture is null ? new List<Exception>() : await fixture.CloseAsync();
        if (primary is null && cleanup.Count == 0 && fixture is not null)
            try { fixture.Data.Dispose(); } catch (Exception error) { cleanup.Add(error); }
        if (primary is not null) cleanup.Insert(0, primary);
        if (cleanup.Count == 1) ExceptionDispatchInfo.Capture(cleanup[0]).Throw();
        if (cleanup.Count > 1) throw new AggregateException("Owned CP assertion and/or cleanup failed.", cleanup);
    }

    private sealed class CpsFrameFixture
    {
        internal readonly LegacyOwnedAdmissionRuntimeFixture Data = new(bootstrap: false);
        internal readonly LegacyNativeGameClientRuntime Native = new();
        internal readonly List<byte[]> Sent = [];
        internal readonly StringWriter Log = new();
        internal Action<byte[]>? OnSend;
        internal LegacyOwnedAdmissionRuntime? Runtime;
        internal WorldServerFrameHandler Handler = null!;
        internal ConquerTcpConnection Connection = null!;
        private NativeLoginBeastMetadataScope? beasts;
        internal WorldSessionStore Store => Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
            .GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Handler));
        internal WorldSession Current => Assert.IsType<WorldSession>(Store.TryGet(Connection, out var current) ? current : null);

        internal void Initialize(long cps, long bound, long bank)
        {
            var text = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(Data.UserPath), out var encoding);
            foreach (var (key, value) in new[] { ("ConquerPoints", cps), ("BoundConquerPoints", bound), ("BankCps", bank) })
            {
                var pattern = "(?m)^" + Regex.Escape(key) + "=[^\\r\\n]*";
                Assert.Single(Regex.Matches(text, pattern).Cast<Match>());
                text = Regex.Replace(text, pattern, key + "=" + value.ToString(CultureInfo.InvariantCulture));
            }
            File.WriteAllBytes(Data.UserPath, encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
            Data.Bootstrap();
            Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out Runtime, out var reason), reason);
            Assert.NotNull(Runtime);
            beasts = new NativeLoginBeastMetadataScope(1000);
            var fields = Enumerable.Repeat("0", 66).ToArray();
            fields[0] = "1000000"; fields[1] = "OwnedCpSavedItem";
            fields[ItemType.DurabilityFieldIndex] = "1"; fields[ItemType.StackSizeFieldIndex] = "1";
            var catalog = LegacyItemWorldDataFixture.Create([new ItemType(1000000, fields[1], fields)], []);
            Handler = new WorldServerFrameHandler(Data.Loader, null, catalog, LegacyRuntimeUpdateLoopOptions.Disabled,
                Log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: Runtime!);
            Connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                (frame, _) => { var bytes = frame.ToArray(); OnSend?.Invoke(bytes); Sent.Add(bytes); return ValueTask.CompletedTask; },
                _ => ValueTask.CompletedTask, Native);
        }

        internal async Task LoginAsync()
        {
            var bytes = new LegacyMsgLoginGamePacket(LegacyOwnedAdmissionRuntimeFixture.Uid).ToFrameBytes();
            Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
            await Handler.HandleFrameAsync(Connection, frame);
        }

        internal async Task<List<Exception>> CloseAsync()
        {
            var errors = new List<Exception>();
            if (Connection is not null) try { await Connection.CompleteTransportCloseAsync(); } catch (Exception e) { errors.Add(e); }
            if (Runtime is not null) try { await Runtime.DisposeAsync(); } catch (Exception e) { errors.Add(e); }
            try { beasts?.Dispose(); } catch (Exception e) { errors.Add(e); }
            try { Log.Dispose(); } catch (Exception e) { errors.Add(e); }
            return errors;
        }
    }

    private static (uint Cps, int Bound, ulong Bank) NativeCps(CpsFrameFixture f) =>
        (f.Native.Player.ConquerPoints, f.Native.Player.BoundConquerPoints, f.Native.ConquerPointsSave);
}
