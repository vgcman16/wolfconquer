using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Text;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Direct public startup API + genuine managed Frame admission. Real child-host
// transport/authentication tests are a separate independent helper scope.
public sealed partial class LegacyOwnedAdmissionRuntimeTests
{
    [Fact]
    public async Task PublicStartupOwnerEnablesTheSavedFrameWithoutAnInternalTestConstructorOrRegrant()
    {
        using var fixture = new LegacyOwnedAdmissionRuntimeFixture();
        fixture.WriteConfiguration();
        Assert.True(LegacyOwnedAdmissionOptions.TryReadFile(fixture.ConfigPath, out var configured, out var reason), reason);
        Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(fixture.Loader, configured, out var opened, out reason), reason);
        await using var runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(opened);
        var files = fixture.Files();
        using var log = new StringWriter();
        var handler = CreateHandler(fixture, runtime, log);
        var sent = new ConcurrentQueue<byte[]>();
        var connection = Connection(sent);
        try
        {
            await handler.HandleFrameAsync(connection, Frame(new LegacyMsgLoginGamePacket(LegacyOwnedAdmissionRuntimeFixture.Uid).ToFrameBytes()));

            Assert.Contains(sent, IsAnswerOk);
            var store = Field<WorldSessionStore>(handler, "_sessions");
            Assert.True(store.TryGet(connection, out var current));
            Assert.NotNull(current);
            Assert.Equal(LegacyOwnedAdmissionRuntimeFixture.Uid, current.Character.Uid);
            Assert.Equal(1, current.Character.Online);
            Assert.Null(current.NativeGameClient);
            // Match the real loader's BOM-aware text boundary. The independent
            // complete raw-byte expectation below remains unchanged.
            var beforeText = CharacterSnapshotFileEncoding.Decode(files[Path.GetRelativePath(fixture.Root, fixture.UserPath)], out _);
            var before = CharacterSnapshotParser.ParseText(beforeText, Path.GetFileName(fixture.UserPath));
            var empty = new Dictionary<string, string>();
            Assert.Equal(before with { Online = 1, Attributes = empty, PaidNobility = empty, PlayerQuests = PlayerQuestSnapshot.Empty },
                current.Character with { Attributes = empty, PaidNobility = empty, PlayerQuests = PlayerQuestSnapshot.Empty });
            var saved = Encoding.UTF8.GetString(files[Path.GetRelativePath(fixture.Root, fixture.UserPath)]);
            Assert.Equal(1, saved.Split("Online=0\r\n", StringSplitOptions.None).Length - 1);
            files[Path.GetRelativePath(fixture.Root, fixture.UserPath)] = Encoding.UTF8.GetBytes(
                saved.Replace("Online=0\r\n", "Online=1\r\n", StringComparison.Ordinal));
            fixture.AssertFiles(files);
            Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
            Assert.Equal(1, log.ToString().Split('\n').Count(line => line.Contains("ordinary admission committed", StringComparison.Ordinal)));
            var count = sent.Count;
            await handler.HandleFrameAsync(connection, Frame(new LegacyMsgLoginGamePacket(LegacyOwnedAdmissionRuntimeFixture.Uid).ToFrameBytes()));
            Assert.Same(current, store.TryGet(connection, out var repeated) ? repeated : null);
            Assert.Equal(count, sent.Count);
            fixture.AssertFiles(files);
        }
        finally { await connection.CompleteTransportCloseAsync(); }
    }

    [Fact]
    public void PublicConstructorRetainsEnabledRuntimeHandoffProfileAndCompatibilityInsteadOfUsingTheDisabledTestOverload()
    {
        using var fixture = new LegacyOwnedAdmissionRuntimeFixture();
        using var runtime = Open(fixture);
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var worldData = LegacyItemWorldDataFixture.Create([], []);
        var loop = LegacyRuntimeUpdateLoopOptions.EnabledDefault with
        { TickInterval = TimeSpan.FromMilliseconds(37), LocalServerIdentity = new(2) };
        var compatibility = LegacyClientCompatibilityOptions.NewerClient;
        var probe = new LegacyWorldEntryStartupProbeOptions(LegacyWorldEntryStartupPrefixProbeMode.ConfigBeforeTimer);
        var handler = new WorldServerFrameHandler(fixture.Loader, handoff, worldData, loop,
            TextWriter.Null, compatibility, probe, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: runtime);

        Assert.Same(fixture.Loader, Field<CharacterSnapshotLoader>(handler, "_snapshotLoader"));
        Assert.Same(handoff, Field<AccountHandoffService>(handler, "_handoffService"));
        Assert.Same(worldData, Field<WorldDataCatalog>(handler, "_worldData"));
        Assert.Same(loop, Field<LegacyRuntimeUpdateLoopOptions>(handler, "_runtimeUpdateLoop"));
        Assert.True(Field<LegacyRuntimeUpdateLoopOptions>(handler, "_runtimeUpdateLoop").Enabled);
        Assert.Same(compatibility, Field<LegacyClientCompatibilityOptions>(handler, "_clientCompatibility"));
        Assert.Same(probe, Field<LegacyWorldEntryStartupProbeOptions>(handler, "_worldEntryStartupProbe"));
        Assert.Equal(ConquerProtocolProfile.VisibleDx8D13, Field<ConquerProtocolProfile>(handler, "_protocolProfile"));
        // No Frame was dispatched, so this retention test starts no background loop.
    }

    [Fact]
    public async Task DisposedStartupLifetimeCannotLeaveAnAdmittingHandlerAfterReleasingItsLease()
    {
        using var fixture = new LegacyOwnedAdmissionRuntimeFixture();
        using var runtime = Open(fixture);
        using var log = new StringWriter();
        var handler = CreateHandler(fixture, runtime, log);
        runtime.Dispose();
        var files = fixture.Files();
        var sent = new ConcurrentQueue<byte[]>();
        var connection = Connection(sent);
        try
        {
            await handler.HandleFrameAsync(connection, Frame(new LegacyMsgLoginGamePacket(LegacyOwnedAdmissionRuntimeFixture.Uid).ToFrameBytes()));

            Assert.DoesNotContain(sent, IsAnswerOk);
            Assert.False(Field<WorldSessionStore>(handler, "_sessions").TryGet(connection, out _));
            fixture.AssertFiles(files);
            using var reopened = Open(fixture);
        }
        finally { await connection.CompleteTransportCloseAsync(); }
    }

    [Fact]
    public void OneStartupLifetimeCannotBindASecondIndependentFrameOwner()
    {
        using var fixture = new LegacyOwnedAdmissionRuntimeFixture();
        using var runtime = Open(fixture);
        using var log = new StringWriter();
        _ = CreateHandler(fixture, runtime, log);
        var files = fixture.Files();

        var error = Record.Exception(() => CreateHandler(fixture, runtime, log));

        Assert.NotNull(error);
        Assert.True(error is ArgumentException or InvalidOperationException, error.ToString());
        fixture.AssertFiles(files);
    }

    private static WorldServerFrameHandler CreateHandler(LegacyOwnedAdmissionRuntimeFixture fixture,
        LegacyOwnedAdmissionRuntime runtime, TextWriter log) =>
        new(fixture.Loader, null, LegacyItemWorldDataFixture.Create([], []), LegacyRuntimeUpdateLoopOptions.Disabled,
            log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: runtime);

    private static ConquerTcpConnection Connection(ConcurrentQueue<byte[]> sent) =>
        new(null, new IPEndPoint(IPAddress.Loopback, 9958), (bytes, _) =>
        { sent.Enqueue(bytes.ToArray()); return ValueTask.CompletedTask; });

    private static bool IsAnswerOk(byte[] bytes) => LegacyMsgMessagePacket.TryParse(Frame(bytes), out var reply) &&
        reply.Content.Count > 3 && reply.Content[3] == "ANSWER_OK";

    private static PacketFrame Frame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static T Field<T>(WorldServerFrameHandler handler, string name)
    {
        var field = typeof(WorldServerFrameHandler).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field.GetValue(handler));
    }
}
