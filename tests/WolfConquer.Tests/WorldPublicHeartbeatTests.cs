using System.Net;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class WorldPublicHeartbeatTests
{
    [Fact]
    public void MissingFileConfigurationDisablesExportWithoutReadingUnusedOptions()
    {
        Assert.True(WorldPublicHeartbeatOptions.TryFromEnvironment(out var options, out var reason, name =>
        {
            Assert.Equal(WorldPublicHeartbeatOptions.FileVariable, name); return null;
        }));
        Assert.Null(options); Assert.Empty(reason);
    }

    [Theory]
    [InlineData("instance")]
    [InlineData("zero-instance")]
    [InlineData("environment")]
    [InlineData("relative-file")]
    [InlineData("wrong-file")]
    [InlineData("remote-file")]
    [InlineData("client-label")]
    public void ExplicitInvalidConfigurationRefusesInsteadOfPublishingInventedHealth(string fault)
    {
        string path = Path.Combine(Path.GetTempPath(), "wolf-public-config", "live-game-status.json");
        var values = new Dictionary<string, string?>
        {
            [WorldPublicHeartbeatOptions.FileVariable] = path,
            [WorldPublicHeartbeatOptions.InstanceVariable] = Guid.NewGuid().ToString("D"),
            [WorldPublicHeartbeatOptions.EnvironmentVariable] = "test",
        };
        switch (fault)
        {
            case "instance": values[WorldPublicHeartbeatOptions.InstanceVariable] = "not-a-guid"; break;
            case "zero-instance": values[WorldPublicHeartbeatOptions.InstanceVariable] = Guid.Empty.ToString("D"); break;
            case "environment": values[WorldPublicHeartbeatOptions.EnvironmentVariable] = "possibly-online"; break;
            case "relative-file": values[WorldPublicHeartbeatOptions.FileVariable] = "live-game-status.json"; break;
            case "wrong-file": values[WorldPublicHeartbeatOptions.FileVariable] = Path.Combine(Path.GetTempPath(), "private.json"); break;
            case "remote-file": values[WorldPublicHeartbeatOptions.FileVariable] = "\\\\example\\share\\live-game-status.json"; break;
            case "client-label": values[WorldPublicHeartbeatOptions.ClientBuildVariable] = "secret\npath"; break;
        }
        Assert.False(WorldPublicHeartbeatOptions.TryFromEnvironment(out var options, out var reason,
            name => values.GetValueOrDefault(name)));
        Assert.Null(options); Assert.NotEmpty(reason); Assert.DoesNotContain(path, reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisabledPublisherDoesNotCreateAnOutputOrWriterLease() => await Run(async f =>
    {
        Assert.Null(WorldPublicHeartbeatPublisher.StartIfEnabled(f.Server, f.Frame, null));
        Assert.False(Directory.Exists(f.Root)); await Task.CompletedTask;
    });

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static async Task Run(Func<Fixture, Task> body, WorldServerFrameHandler? frame = null, int port = 0)
    {
        var f = new Fixture(frame ?? new WorldServerFrameHandler(null, TextWriter.Null), port); Exception? primary = null;
        try { await body(f); } catch (Exception error) { primary = error; }
        try { await f.DisposeAsync(); }
        catch (Exception cleanup)
        {
            if (primary is not null) throw new AggregateException(primary, cleanup);
            throw;
        }
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
    }
    private sealed class Fixture(WorldServerFrameHandler frame, int port) : IAsyncDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-public-heartbeat-" + Guid.NewGuid().ToString("N"));
        internal WorldServerFrameHandler Frame { get; } = frame;
        internal ConquerTcpServer Server { get; } = new(new ConquerTcpServerOptions(
            ConquerRuntimeDefaults.WorldServer, IPAddress.Loopback, port, Log: TextWriter.Null, ErrorLog: TextWriter.Null), frame.HandleFrameAsync);
        internal string FilePath => Path.Combine(Root, "live-game-status.json");
        internal Guid Instance { get; } = Guid.NewGuid();
        internal WorldPublicHeartbeatOptions Options => new(FilePath, Instance, "test", "7937");
        internal WorldPublicHeartbeatPublisher? Publisher;
        internal readonly StringWriter PublicationLog = new();
        private bool serverStopped;
        internal WorldPublicHeartbeatPublisher Publish() => Publisher ??=
            WorldPublicHeartbeatPublisher.StartIfEnabled(Server, Frame, Options, PublicationLog)!;
        internal WorldPublicHeartbeat Read() => Assert.IsType<WorldPublicHeartbeat>(
            JsonSerializer.Deserialize<WorldPublicHeartbeat>(File.ReadAllBytes(FilePath), JsonOptions));
        internal async Task StopServer()
        {
            if (serverStopped) return;
            await Server.DisposeAsync(); serverStopped = true;
        }
        public async ValueTask DisposeAsync()
        {
            var failures = new List<Exception>();
            if (Publisher is not null) try { await Publisher.StopPollingAsync(); } catch (Exception e) { failures.Add(e); }
            try { await StopServer(); } catch (Exception e) { failures.Add(e); }
            if (Publisher is not null) try { await Publisher.DisposeAsync(); } catch (Exception e) { failures.Add(e); }
            PublicationLog.Dispose();
            if (failures.Count != 0) throw new AggregateException(failures);
            string full = Path.GetFullPath(Root), name = Path.GetFileName(Root);
            Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), Path.GetDirectoryName(full));
            Assert.StartsWith("wolf-public-heartbeat-", name);
            Assert.True(Guid.TryParseExact(name[22..], "N", out _));
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
