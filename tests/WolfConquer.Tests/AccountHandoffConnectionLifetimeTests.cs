using System.Buffers;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Component sends use the real handler and real ticket store with an owned
// output collector. The separate EOF case uses a real TCP-born connection.
public sealed class AccountHandoffConnectionLifetimeTests
{
    [Fact]
    public async Task SecondValidTicketOnBoundConnectionIsNotConsumedOrPublished()
    {
        using var f = new Fixture();
        var first = f.Issue(101); var second = f.Issue(102);
        var (connection, sent) = SendingConnection();
        await f.Enter(connection, first.Token);
        Assert.True(f.Store.TryGet(first.Token, out var consumed)); Assert.True(consumed.IsRedeemed);
        var holder = Assert.IsType<WorldSession>(f.Handler.ReadCurrentSessionForNativeOwnerObservation(connection));
        Assert.Equal(101L, holder.AccountId); AssertSuccess(sent, 101);
        var frames = sent.Select(row => row.ToArray()).ToArray(); var files = f.Capture();

        await f.Enter(connection, second.Token);

        Assert.True(f.Store.TryGet(second.Token, out var stillPending)); Assert.Equal(second, stillPending);
        Assert.False(stillPending.IsRedeemed); Assert.Same(holder, f.Handler.ReadCurrentSessionForNativeOwnerObservation(connection));
        AssertFrames(frames, sent); f.AssertFiles(files);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuccessfulRedemptionStillBindsWhenSnapshotIsMissingOrInvalid(bool invalid)
    {
        using var f = new Fixture();
        if (invalid) f.WriteCharacter(101, 999); else File.Delete(f.PathFor(101));
        var first = f.Issue(101); var second = f.Issue(102); var files = f.Capture();
        var (connection, sent) = SendingConnection();
        await f.Enter(connection, first.Token);
        Assert.True(f.Store.TryGet(first.Token, out var consumed)); Assert.True(consumed.IsRedeemed);
        Assert.Null(f.Handler.ReadCurrentSessionForNativeOwnerObservation(connection));
        Assert.Single(sent); AssertStatus(sent[0], invalid ? WorldSnapshotWireStatus.SnapshotError : WorldSnapshotWireStatus.MissingSnapshot, 101);
        var frames = sent.Select(row => row.ToArray()).ToArray();

        await f.Enter(connection, second.Token);

        Assert.True(f.Store.TryGet(second.Token, out var pending)); Assert.Equal(second, pending); Assert.False(pending.IsRedeemed);
        Assert.Null(f.Handler.ReadCurrentSessionForNativeOwnerObservation(connection));
        AssertFrames(frames, sent); f.AssertFiles(files);
    }

    [Fact]
    public async Task DifferentFrameCannotConsumeAnotherTicketOnConnectionOwnedByFirstFrame()
    {
        using var f = new Fixture(); File.Delete(f.PathFor(101));
        var first = f.Issue(101); var second = f.Issue(102); var files = f.Capture();
        var (connection, sent) = SendingConnection();
        await f.Enter(connection, first.Token);
        Assert.True(f.Store.TryGet(first.Token, out var consumed)); Assert.True(consumed.IsRedeemed);
        Assert.Single(sent); AssertStatus(sent[0], WorldSnapshotWireStatus.MissingSnapshot, 101);
        Assert.Null(f.Handler.ReadCurrentSessionForNativeOwnerObservation(connection));
        var frames = sent.Select(row => row.ToArray()).ToArray();
        var other = new WorldServerFrameHandler(new CharacterSnapshotLoader(Path.GetDirectoryName(f.PathFor(101))!), f.Handoff);

        await other.HandleFrameAsync(connection, Frame(second.Token));

        Assert.True(f.Store.TryGet(second.Token, out var pending)); Assert.Equal(second, pending); Assert.False(pending.IsRedeemed);
        Assert.Null(other.ReadCurrentSessionForNativeOwnerObservation(connection));
        Assert.Null(f.Handler.ReadCurrentSessionForNativeOwnerObservation(connection));
        AssertFrames(frames, sent); f.AssertFiles(files);
    }

    [Fact]
    public async Task UnknownTicketDoesNotBindAndValidTicketThenEnters()
    {
        using var f = new Fixture(); var ticket = f.Issue(101); var (connection, sent) = SendingConnection();
        await f.Enter(connection, "unknown-ticket");
        Assert.Single(sent); AssertStatus(sent[0], WorldSnapshotWireStatus.UnknownTicket, 0);
        Assert.True(f.Store.TryGet(ticket.Token, out var pending)); Assert.Equal(ticket, pending);
        await f.Enter(connection, ticket.Token);
        Assert.True(f.Store.TryGet(ticket.Token, out var consumed)); Assert.True(consumed.IsRedeemed);
        Assert.NotNull(f.Handler.ReadCurrentSessionForNativeOwnerObservation(connection));
        Assert.True(sent.Count > 1); AssertStatus(sent[1], WorldSnapshotWireStatus.Success, 101);
    }

    [Fact]
    public async Task NewConnectionCanRedeemASeparateTicket()
    {
        using var f = new Fixture(); var first = f.Issue(101); var second = f.Issue(102);
        var (one, firstSent) = SendingConnection(); var (two, secondSent) = SendingConnection();
        await f.Enter(one, first.Token); await f.Enter(two, second.Token);
        AssertSuccess(firstSent, 101); AssertSuccess(secondSent, 102);
        Assert.Equal(101L, Assert.IsType<WorldSession>(f.Handler.ReadCurrentSessionForNativeOwnerObservation(one)).AccountId);
        Assert.Equal(102L, Assert.IsType<WorldSession>(f.Handler.ReadCurrentSessionForNativeOwnerObservation(two)).AccountId);
        Assert.True(f.Store.TryGet(first.Token, out var savedFirst)); Assert.True(savedFirst.IsRedeemed);
        Assert.True(f.Store.TryGet(second.Token, out var savedSecond)); Assert.True(savedSecond.IsRedeemed);
        var (replay, replaySent) = SendingConnection();
        await f.Enter(replay, first.Token);
        Assert.Single(replaySent); AssertStatus(replaySent[0], WorldSnapshotWireStatus.AlreadyRedeemedTicket, 101);
        Assert.Null(f.Handler.ReadCurrentSessionForNativeOwnerObservation(replay));
    }

    [Fact]
    public async Task RealLegacyLoginOccupiedConnectionDoesNotConsumeInternalTicket()
    {
        using var f = new Fixture(); var ticket = f.Issue(102); var (connection, sent) = SendingConnection();
        await f.Handler.HandleFrameAsync(connection, ReadFrame(new LegacyMsgLoginGamePacket(101).ToFrameBytes()));
        var holder = Assert.IsType<WorldSession>(f.Handler.ReadCurrentSessionForNativeOwnerObservation(connection));
        Assert.Equal(101L, holder.AccountId); Assert.NotEmpty(sent);
        var frames = sent.Select(row => row.ToArray()).ToArray(); var files = f.Capture();

        await f.Enter(connection, ticket.Token);

        Assert.True(f.Store.TryGet(ticket.Token, out var pending)); Assert.Equal(ticket, pending); Assert.False(pending.IsRedeemed);
        Assert.Same(holder, f.Handler.ReadCurrentSessionForNativeOwnerObservation(connection));
        AssertFrames(frames, sent); f.AssertFiles(files);
    }

    [Fact]
    public async Task OutputFailureAfterRedemptionKeepsBindingAndDoesNotConsumeAnotherTicket()
    {
        using var f = new Fixture(); var first = f.Issue(101); var second = f.Issue(102);
        int sends = 0; var laterOutput = new List<byte[]>();
        var connection = new ConquerTcpConnection(null, null, (bytes, _) =>
        {
            if (++sends == 1) throw new InvalidOperationException("owned output failure");
            laterOutput.Add(bytes.ToArray()); return ValueTask.CompletedTask;
        });
        var files = f.Capture();
        await f.Enter(connection, first.Token);
        Assert.True(f.Store.TryGet(first.Token, out var committed)); Assert.True(committed.IsRedeemed);
        Assert.Equal(1, sends); Assert.Empty(laterOutput); f.AssertFiles(files);
        Assert.Null(f.Handler.ReadCurrentSessionForNativeOwnerObservation(connection));

        await f.Enter(connection, second.Token);

        Assert.True(f.Store.TryGet(second.Token, out var pending)); Assert.Equal(second, pending); Assert.False(pending.IsRedeemed);
        Assert.Equal(1, sends); Assert.Empty(laterOutput); f.AssertFiles(files);
        Assert.Null(f.Handler.ReadCurrentSessionForNativeOwnerObservation(connection));
    }

    [Fact]
    public async Task ConcurrentActualDispatcherRequestsOnOneConnectionConsumeOnlyOneTicket()
    {
        using var f = new Fixture(); var first = f.Issue(101); var second = f.Issue(102);
        var (connection, sent) = SendingConnection();
        var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int arrivals = 0;
        Task Send(string token) => Task.Run(async () =>
        {
            if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult(true);
            await release.Task;
            await f.Enter(connection, token);
        });
        var firstCall = Send(first.Token); var secondCall = Send(second.Token);
        try { await ready.Task; }
        finally { release.TrySetResult(true); await Task.WhenAll(firstCall, secondCall); }
        Assert.True(f.Store.TryGet(first.Token, out var savedFirst)); Assert.True(f.Store.TryGet(second.Token, out var savedSecond));
        Assert.NotEqual(savedFirst.IsRedeemed, savedSecond.IsRedeemed);
        var owner = Assert.IsType<WorldSession>(f.Handler.ReadCurrentSessionForNativeOwnerObservation(connection));
        Assert.Equal(savedFirst.IsRedeemed ? 101L : 102L, owner.AccountId);
        AssertSuccess(sent, owner.AccountId);
        Assert.Single(sent, row => TryStatus(row, out var status) && status == WorldSnapshotWireStatus.Success);
        Assert.Equal(savedFirst.IsRedeemed ? second : first, savedFirst.IsRedeemed ? savedSecond : savedFirst);
    }

    [Fact]
    public async Task RealTcpPeerEofPreventsLaterRedemptionOnTheBornConnection()
    {
        using var f = new Fixture(); var ticket = f.Issue(101);
        await NativePlayerLoopback.RunAsync(ConquerTcpWireMode.LegacyGameServer, async fixture =>
        {
            var wire = await fixture.ConnectGameAsync(false); var birth = await fixture.NextAsync();
            var client = Assert.IsType<LegacyNativeGameClientRuntime>(birth.Connection.NativeGameClient);
            var before = LegacyNativePlayerRuntime.ExistingPrefixLoad.Read(client.Player); var files = f.Capture();
            await fixture.CloseClientTransportAsync();
            Assert.True(birth.Connection.HasObservedTransportClose);
            await f.Enter(birth.Connection, ticket.Token);
            Assert.True(f.Store.TryGet(ticket.Token, out var pending)); Assert.Equal(ticket, pending); Assert.False(pending.IsRedeemed);
            Assert.Equal(before, LegacyNativePlayerRuntime.ExistingPrefixLoad.Read(client.Player));
            Assert.Null(f.Handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection)); f.AssertFiles(files);
            GC.KeepAlive(wire);
        }, f.Handler.HandleFrameAsync, f.Handler.ReadCurrentSessionForNativeOwnerObservation);
    }

    [Theory]
    [InlineData("uid")]
    [InlineData("full-loading")]
    [InlineData("fake")]
    public async Task InitializedTcpBornNativeOwnerCannotRedeemInternalTicket(string state)
    {
        using var f = new Fixture(); var ticket = f.Issue(101); var files = f.Capture();
        await NativePlayerLoopback.RunAsync(ConquerTcpWireMode.LegacyGameServer, async fixture =>
        {
            var wire = await fixture.ConnectGameAsync(false, captureOutput: true);
            var birth = await fixture.NextAsync();
            var client = Assert.IsType<LegacyNativeGameClientRuntime>(birth.Connection.NativeGameClient);
            var player = client.Player;
            if (state == "uid") player.UID = 123;
            if (state == "full-loading") client.FullLoading = true;
            if (state == "fake") client.Fake = true;
            var before = LegacyNativePlayerRuntime.ExistingPrefixLoad.Read(player);

            await wire.SendAsync(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes(), fixture.Token);
            var dispatch = await fixture.NextAsync();

            Assert.Same(birth.Connection, dispatch.Connection); Assert.Null(dispatch.Before); Assert.Null(dispatch.After);
            Assert.True(f.Store.TryGet(ticket.Token, out var pending)); Assert.Equal(ticket, pending); Assert.False(pending.IsRedeemed);
            Assert.Same(client, birth.Connection.NativeGameClient); Assert.Same(player, client.Player); Assert.Same(client, player.Owner);
            Assert.Equal(before, LegacyNativePlayerRuntime.ExistingPrefixLoad.Read(player));
            Assert.Equal(state == "uid" ? 123U : 0U, player.UID);
            Assert.Equal(state == "full-loading", client.FullLoading); Assert.Equal(state == "fake", client.Fake);
            Assert.Null(f.Handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection)); f.AssertFiles(files);
            await fixture.CloseClientTransportAsync();
            Assert.Empty(wire.CopyCapturedServerFramesAtEof());
        }, f.Handler.HandleFrameAsync, f.Handler.ReadCurrentSessionForNativeOwnerObservation);
    }

    private static (ConquerTcpConnection Connection, List<byte[]> Sent) SendingConnection()
    {
        var sent = new List<byte[]>();
        return (new ConquerTcpConnection(null, null, (bytes, _) => { sent.Add(bytes.ToArray()); return ValueTask.CompletedTask; }), sent);
    }

    private static PacketFrame Frame(string token) => ReadFrame(new InternalWorldEnterRequestPacket(token).ToFrameBytes());
    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static bool TryStatus(byte[] bytes, out WorldSnapshotWireStatus status)
    {
        status = default;
        if (!PacketReader.TryRead(bytes, out var reader) || reader.Header.Opcode != WorldPacketOpcodes.InternalSnapshotResponse) return false;
        status = WorldSnapshotResponsePacket.ReadStatus(reader); return true;
    }

    private static void AssertStatus(byte[] bytes, WorldSnapshotWireStatus status, long account)
    {
        Assert.True(PacketReader.TryRead(bytes, out var reader)); Assert.Equal(status, WorldSnapshotResponsePacket.ReadStatus(reader));
        Assert.Equal(account, WorldSnapshotResponsePacket.ReadAccountId(reader));
    }
    private static void AssertSuccess(List<byte[]> sent, long account) { Assert.NotEmpty(sent); AssertStatus(sent[0], WorldSnapshotWireStatus.Success, account); }
    private static void AssertFrames(byte[][] expected, List<byte[]> actual)
    { Assert.Equal(expected.Length, actual.Count); for (int i = 0; i < expected.Length; i++) Assert.Equal(expected[i], actual[i]); }

    private sealed class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-handoff-lifetime-" + Guid.NewGuid().ToString("N"));
        internal readonly InMemoryAccountHandoffTicketStore Store = new();
        internal readonly AccountHandoffService Handoff;
        internal readonly WorldServerFrameHandler Handler;
        internal Fixture()
        {
            Directory.CreateDirectory(root); WriteCharacter(101, 101); WriteCharacter(102, 102);
            Handoff = new(Store); Handler = new(new CharacterSnapshotLoader(root), Handoff);
        }
        internal string PathFor(uint account) => Path.Combine(root, account + ".ini");
        internal void WriteCharacter(uint account, uint uid) => File.WriteAllBytes(PathFor(account), SqlWorldStateTestData.Character(uid));
        internal AccountHandoffTicket Issue(long account) => Handoff.Issue(account, "owned-" + account, ConquerRuntimeDefaults.WorldServer);
        internal Task Enter(ConquerTcpConnection connection, string token) => Handler.HandleFrameAsync(connection, Frame(token)).AsTask();
        internal Dictionary<string, byte[]> Capture() => Directory.GetFiles(root).ToDictionary(path => Path.GetFileName(path), File.ReadAllBytes);
        internal void AssertFiles(Dictionary<string, byte[]> expected)
        {
            var actual = Capture(); Assert.Equal(expected.Keys.Order(), actual.Keys.Order());
            foreach (var pair in expected) Assert.Equal(pair.Value, actual[pair.Key]);
        }
        public void Dispose()
        {
            var absolute = Path.GetFullPath(root); var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            if (!string.Equals(Path.GetDirectoryName(absolute), temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(absolute).StartsWith("wolf-handoff-lifetime-", StringComparison.Ordinal)) throw new InvalidOperationException("Unsafe fixture cleanup.");
            if (Directory.Exists(absolute)) Directory.Delete(absolute, true);
        }
    }
}
