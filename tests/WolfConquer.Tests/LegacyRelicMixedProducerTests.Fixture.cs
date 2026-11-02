using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyRelicMixedProducerTests
{
    private static async Task Run(Func<MixedFixture, Task> body)
    {
        var fixture = new MixedFixture(); var errors = new List<Exception>();
        try { await fixture.InitializeAsync(); await body(fixture); } catch (Exception error) { errors.Add(error); }
        try { await fixture.CloseAsync(); } catch (Exception error) { errors.Add(error); }
        try { fixture.RestoreMetadata(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 0) try { fixture.Data.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }

    // Real saved inputs precede bootstrap. The same runtime, Frame and shared
    // registry run both producers; no result or allocation owner is injected.
    private sealed partial class MixedFixture
    {
        internal readonly LegacyOwnedAdmissionRuntimeFixture Data = new(bootstrap: false);
        internal readonly List<byte[]> Sent = [];
        private readonly StringWriter logBuffer = new();
        private readonly TextWriter log;
        internal WorldDataCatalog Catalog = null!;
        internal WorldServerFrameHandler Handler = null!;
        internal LegacyOwnedAdmissionRuntime Runtime = null!;
        internal Action<byte[]>? BeforeSend;
        private NativeLoginBeastMetadataScope? beasts;
        private ConquerTcpConnection? connection;
        private CancellationTokenSource? lifetime;
        private Exception? observerFailure;
        internal MixedFixture() { log = TextWriter.Synchronized(logBuffer); }
        internal string CharacterPath => Data.UserPath;
        internal string ItemsPath => Path.Combine(Data.Items, LegacyRelicMixedProducerData.CharacterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
        internal string SpellsPath => Path.Combine(Data.Spells, LegacyRelicMixedProducerData.CharacterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
        internal uint HighWater => State()["HighWater"]!.GetValue<uint>();
        internal JsonObject State() => JsonNode.Parse(File.ReadAllBytes(Data.State))!.AsObject();
        internal WorldSessionStore Store => Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
            .GetField("_sessions", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Handler));
        internal WorldSession Current
        {
            get
            {
                string output; lock (log) output = logBuffer.ToString();
                Assert.True(Store.TryGet(connection!, out var current), "Mixed-producer owned admission unavailable:\n" + output);
                return Assert.IsType<WorldSession>(current);
            }
        }
        internal LegacyNativeGameClientRuntime Native => Current.NativeGameClient!;

        internal async Task InitializeAsync()
        {
            new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(LegacyRelicMixedProducerData.CharacterUid,
                LegacyRelicMixedProducerData.InitialItems());
            Catalog = LegacyRelicMixedProducerData.Catalog();
            beasts = new(1000); // Actual successful-login metadata prerequisite.
            Data.Bootstrap();
            await ReopenFrameAsync();
            AssertSettled(HighWater, version: 1);
            Assert.Equal(LegacyOwnedAdmissionRuntimeFixture.HistoricalFloor, HighWater);
        }

        internal void RestoreMetadata()
        {
            try { beasts?.Dispose(); }
            finally { beasts = null; log.Dispose(); }
        }
    }
}
