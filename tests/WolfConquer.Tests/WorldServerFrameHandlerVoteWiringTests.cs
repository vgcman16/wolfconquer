using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class WorldServerFrameHandlerVoteWiringTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "wolf-vote-wiring-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ConstructorLoadsMissingVotesFileAsEmptyStoreFromUsersParent()
    {
        var usersRoot = CreateUsersRoot();

        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot));

        var voteStates = GetVoteStates(handler);
        Assert.Equal(Path.GetFullPath(Path.Combine(_root, "Votes.txt")), voteStates.VotesFilePath);
        Assert.True(voteStates.LoadResult.Success);
        Assert.False(voteStates.LoadResult.FileExisted);
        Assert.Empty(voteStates.Snapshot);
    }

    [Fact]
    public void ConstructorRetainsMalformedLoadResultAndLogsOneParityGap()
    {
        var usersRoot = CreateUsersRoot();
        File.WriteAllText(
            Path.Combine(_root, "Votes.txt"),
            "Count=1\r\n42/192.0.2.42/not-ticks/\r\n");
        using var log = new StringWriter();

        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot), log);

        Assert.False(GetVoteStates(handler).LoadResult.Success);
        var parityLines = log.ToString()
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.Contains(
                "PARITY-GAP legacy vote runtime load failed",
                StringComparison.Ordinal));
        Assert.Single(parityLines);
    }

    [Fact]
    public void FullConstructorUsesInjectedVoteStore()
    {
        var usersRoot = CreateUsersRoot();
        File.WriteAllText(
            Path.Combine(_root, "Votes.txt"),
            "Count=1\r\n42/192.0.2.42/not-ticks/\r\n");
        var injectedVoteStates = LegacyVoteRuntimeStateStore.Load(
            Path.Combine(_root, "injected", "Votes.txt"));
        using var log = new StringWriter();

        var handler = new WorldServerFrameHandler(
            snapshotLoader: new CharacterSnapshotLoader(usersRoot),
            handoffService: null,
            arenaStatistics: null,
            teamArenaStatistics: null,
            worldData: null,
            runtimeUpdateLoop: null,
            log: log,
            voteStates: injectedVoteStates);

        Assert.Same(injectedVoteStates, GetVoteStates(handler));
        Assert.DoesNotContain("legacy vote runtime load failed", log.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void PacketHandlerContextRetainsVoteStoreAndTimeProvider()
    {
        var voteStates = LegacyVoteRuntimeStateStore.Load(Path.Combine(_root, "Votes.txt"));
        var timeProvider = new TestTimeProvider();
        var connection = new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958));

        var context = new WorldPacketHandlerContext(
            connection,
            TextWriter.Null,
            VoteStates: voteStates,
            TimeProvider: timeProvider);

        Assert.Same(voteStates, context.VoteStates);
        Assert.Same(timeProvider, context.TimeProvider);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string CreateUsersRoot()
    {
        var usersRoot = Path.Combine(_root, "Users");
        Directory.CreateDirectory(usersRoot);
        return usersRoot;
    }

    private static LegacyVoteRuntimeStateStore GetVoteStates(WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField(
            "_voteStates",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<LegacyVoteRuntimeStateStore>(field.GetValue(handler));
    }

    private sealed class TestTimeProvider : TimeProvider
    {
    }
}
