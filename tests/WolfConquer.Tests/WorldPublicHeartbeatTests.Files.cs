using System.Text.Json;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed partial class WorldPublicHeartbeatTests
{
    [Fact]
    public async Task ActualExclusiveWriterLeaseRefusesAnotherExporterAndCanBeReacquiredAfterTerminalDisposal() => await Run(async f =>
    {
        var first = f.Publish(); Assert.True(await first.PublishOnceAsync());
        Assert.ThrowsAny<IOException>(() => WorldPublicHeartbeatPublisher.StartIfEnabled(f.Server, f.Frame, f.Options));
        await first.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await first.PublishOnceAsync());
        await using var next = WorldPublicHeartbeatPublisher.StartIfEnabled(f.Server, f.Frame, f.Options)!;
        Assert.True(await next.PublishOnceAsync()); Assert.Equal(f.Instance, f.Read().InstanceId);
    });

    [Fact]
    public async Task PublicPayloadContainsOnlyBoundedApprovedFieldsAndExplicitTargetLabel() => await Run(async f =>
    {
        Assert.True(await f.Publish().PublishOnceAsync());
        byte[] bytes = File.ReadAllBytes(f.FilePath); Assert.InRange(bytes.Length, 1, 4096);
        using var document = JsonDocument.Parse(bytes);
        Assert.Equal(new[] { "capturedAtUtc", "clientBuild", "environment", "instanceId", "onlinePlayers", "profile",
            "schemaVersion", "source", "startedAtUtc", "state", "uptimeSeconds" }.Order(),
            document.RootElement.EnumerateObject().Select(p => p.Name).Order());
        var status = f.Read(); Assert.Equal(1, status.SchemaVersion); Assert.Equal("WolfConquer.WorldServer", status.Source);
        Assert.Equal(f.Instance, status.InstanceId); Assert.Equal(TimeSpan.Zero, status.CapturedAtUtc.Offset);
        Assert.Equal("7937", status.ClientBuild); Assert.Equal("test", status.Environment);
        Assert.DoesNotContain(f.Root, System.Text.Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
    });

    [Fact]
    public async Task RealOutputConflictPreservesExternalInputAndDoesNotPublishAHealthyFallback() => await Run(async f =>
    {
        var publisher = f.Publish(); Assert.True(await publisher.PublishOnceAsync()); await publisher.StopPollingAsync();
        byte[] before = File.ReadAllBytes(f.FilePath);
        File.Move(f.FilePath, f.FilePath + ".held"); Directory.CreateDirectory(f.FilePath);
        try
        {
            Assert.False(await publisher.PublishOnceAsync()); Assert.True(Directory.Exists(f.FilePath));
            Assert.Equal(before, File.ReadAllBytes(f.FilePath + ".held"));
            Assert.Empty(Directory.GetFiles(f.Root, "*.tmp"));
        }
        finally { Directory.Delete(f.FilePath); File.Move(f.FilePath + ".held", f.FilePath); }
    });

    [Fact]
    public async Task RetainedOldReaderRemainsUnchangedWhileNewOpensObserveTheNewActualListenerImage() => await Run(async f =>
    {
        var publisher = f.Publish();
        Assert.True(await publisher.PublishOnceAsync(), f.PublicationLog.ToString());
        await publisher.StopPollingAsync();
        byte[] before = File.ReadAllBytes(f.FilePath);
        using var beforeDocument = JsonDocument.Parse(before);
        Assert.Equal(JsonValueKind.Null, beforeDocument.RootElement.GetProperty("startedAtUtc").ValueKind);
        await using var retainedReader = new FileStream(f.FilePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);

        f.Server.Start(CancellationToken.None);
        Assert.NotNull(f.Server.BoundEndPoint);
        Assert.True(await publisher.PublishOnceAsync(), f.PublicationLog.ToString());
        byte[] after = File.ReadAllBytes(f.FilePath);
        using var afterDocument = JsonDocument.Parse(after);
        Assert.Equal(JsonValueKind.String, afterDocument.RootElement.GetProperty("startedAtUtc").ValueKind);
        Assert.Equal(f.Instance, afterDocument.RootElement.GetProperty("instanceId").GetGuid());
        Assert.False(before.AsSpan().SequenceEqual(after));

        using var retainedBytes = new MemoryStream();
        await retainedReader.CopyToAsync(retainedBytes);
        Assert.Equal(before, retainedBytes.ToArray());
        retainedReader.Position = 0;
        using var retainedDocument = await JsonDocument.ParseAsync(retainedReader);
        Assert.Equal(JsonValueKind.Null, retainedDocument.RootElement.GetProperty("startedAtUtc").ValueKind);
        Assert.Empty(Directory.GetFiles(f.Root, "*.tmp"));
    });

    [Fact]
    public async Task ConcurrentActualFileReadersObserveCompleteOldOrNewSnapshots() => await Run(async f =>
    {
        var publisher = f.Publish(); Assert.True(await publisher.PublishOnceAsync());
        var reader = Task.Run(async () =>
        {
            for (int index = 0; index < 40; index++)
            {
                await using var input = new FileStream(f.FilePath, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
                using var document = await JsonDocument.ParseAsync(input);
                Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
                Assert.Equal(f.Instance, document.RootElement.GetProperty("instanceId").GetGuid());
            }
        });
        Exception? primary = null;
        try { for (int index = 0; index < 20; index++) Assert.True(await publisher.PublishOnceAsync(), f.PublicationLog.ToString()); }
        catch (Exception error) { primary = error; }
        try { await reader; } // Retain actual owners until every read is terminal.
        catch (Exception error)
        {
            if (primary is not null) throw new AggregateException(primary, error);
            throw;
        }
        if (primary is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw();
        Assert.Empty(Directory.GetFiles(f.Root, "*.tmp"));
    });
}
