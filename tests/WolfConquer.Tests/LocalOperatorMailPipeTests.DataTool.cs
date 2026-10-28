using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LocalOperatorMailPipeTests
{
    [WindowsOperatorPipeFact]
    public Task ActualDataToolCommandAcknowledgesRealGrantAndSameOperationReplyWithoutRegrant() => WithPipeAsync(async (fixture, endpoint) =>
    {
        var request = fixture.Request(Guid.NewGuid());
        var requestPath = Path.Combine(fixture.Fixture.Root, "operator-command-" + request.OperationId.ToString("N") + ".json");
        var requestBytes = JsonSerializer.SerializeToUtf8Bytes(request, RequestJson);
        using (var file = new FileStream(requestPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) file.Write(requestBytes);

        var first = await RunActualOperatorCommand(fixture.Fixture.Root, endpoint.PipeName, requestPath);
        Assert.Equal(0, first.ExitCode);
        Assert.Empty(first.Error);
        var granted = LocalOperatorMailPipe.ParseResult(Encoding.UTF8.GetBytes(first.Output));
        Assert.Equal(request.OperationId, granted.OperationId);
        Assert.Equal(LegacyOperatorGearMailStatus.CommittedNotificationCompleted, granted.Status);
        var itemUid = Assert.Single(granted.ItemUids);
        var mailId = Assert.Single(granted.MailIds);
        Assert.Equal(itemUid, Assert.Single(fixture.Items().WarehouseItems).Uid);
        Assert.Equal(mailId, Assert.Single(fixture.Mail()).Id);
        using (var receiptDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            await WaitStatus(endpoint, "typedResponseAcknowledged", receiptDeadline.Token);
        var highWater = fixture.HighWater;

        // Explicit idempotency control with the SAME retained file/operation;
        // the actual CLI itself never automatically resubmits after uncertainty.
        var second = await RunActualOperatorCommand(fixture.Fixture.Root, endpoint.PipeName, requestPath);
        Assert.Equal(3, second.ExitCode);
        Assert.Empty(second.Error);
        var repeated = LocalOperatorMailPipe.ParseResult(Encoding.UTF8.GetBytes(second.Output));
        Assert.Equal(request.OperationId, repeated.OperationId);
        Assert.Equal(LegacyOperatorGearMailStatus.PreviouslySubmittedOutcomeUnproven, repeated.Status);
        using (var receiptDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            await WaitStatus(endpoint, "typedResponseAcknowledged", receiptDeadline.Token);
        Assert.Equal(highWater, fixture.HighWater);
        Assert.Equal(itemUid, Assert.Single(fixture.Items().WarehouseItems).Uid);
        Assert.Equal(mailId, Assert.Single(fixture.Mail()).Id);
        Assert.Equal(requestBytes, File.ReadAllBytes(requestPath));
        Assert.False(endpoint.Completion.IsCompleted);
    });

    private static async Task<OperatorCliRun> RunActualOperatorCommand(string directory, string pipeName, string requestPath)
    {
        // Existing test-project helper selects the actually built DataTool DLL;
        // this does not invoke dotnet run/build or a substitute sender method.
        using var process = TestProjectProcess.Create(FindPipeCliRepositoryRoot(), "WolfConquer.DataTool");
        process.StartInfo.WorkingDirectory = directory;
        process.StartInfo.ArgumentList.Add("send-operator-gear-mail");
        process.StartInfo.ArgumentList.Add(pipeName);
        process.StartInfo.ArgumentList.Add(requestPath);
        Assert.True(process.Start(), "Could not start the actual owned DataTool operator command.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try { await process.WaitForExitAsync(deadline.Token); }
        catch (OperationCanceledException primary)
        {
            var failures = new List<Exception>
            { new TimeoutException("Actual owned operator command timed out; grant outcome may be unknown and is not retried.", primary) };
            try
            {
                if (!process.HasExited)
                    try { process.Kill(entireProcessTree: true); }
                    catch (InvalidOperationException) when (process.HasExited) { }
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (Exception cleanup) { failures.Add(cleanup); }
            try { await Task.WhenAll(output, error).WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (Exception cleanup) { failures.Add(cleanup); }
            if (failures.Count == 1) throw failures[0];
            throw new AggregateException("Owned operator command timeout/cleanup failures; completion is not assumed.", failures);
        }
        return new(process.ExitCode, await output, await error);
    }

    private static string FindPipeCliRepositoryRoot([CallerFilePath] string source = "")
    {
        for (var directory = new DirectoryInfo(Path.GetDirectoryName(source)!); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln"))) return directory.FullName;
        throw new InvalidOperationException("Could not locate the actual test repository for DataTool.");
    }

    private sealed record OperatorCliRun(int ExitCode, string Output, string Error);
}
