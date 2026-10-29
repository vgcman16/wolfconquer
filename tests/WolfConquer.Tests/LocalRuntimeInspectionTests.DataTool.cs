using System.Runtime.CompilerServices;
using System.Text;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed partial class LocalRuntimeInspectionTests
{
    [WindowsOperatorPipeFact]
    public Task ActualDataToolQueriesTheRealEndpointWithoutGrantSaveOrAcceptancePromotion() => WithPipe(async (f, endpoint) =>
    {
        var before = Files(f); var sent = f.SentSnapshot(); var water = f.HighWater;
        foreach (var uid in new[] { OperatorGearMailFixture.Uid, OperatorGearMailFixture.Uid + 1 })
        {
            using var process = TestProjectProcess.Create(Repository(), "WolfConquer.DataTool");
            process.StartInfo.WorkingDirectory = f.Fixture.Root;
            foreach (var argument in new[] { "inspect-runtime-player", endpoint.PipeName, f.Domain.ToString("N"),
                OperatorGearMailFixture.AccountId.ToString(System.Globalization.CultureInfo.InvariantCulture), uid.ToString(System.Globalization.CultureInfo.InvariantCulture) })
                process.StartInfo.ArgumentList.Add(argument);
            Assert.True(process.Start()); var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try { await process.WaitForExitAsync(deadline.Token); }
            catch (OperationCanceledException primary)
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
                catch (Exception cleanup) { throw new AggregateException(primary, cleanup); }
                throw;
            }
            Assert.Empty(await stderr); var output = await stdout;
            var result = LocalRuntimeInspectionProtocol.ParseResult(Encoding.UTF8.GetBytes(output));
            Assert.Equal(uid == OperatorGearMailFixture.Uid ? 0 : 2, process.ExitCode);
            if (uid == OperatorGearMailFixture.Uid) Diagnostic(result); else Rejected(result);
            Assert.DoesNotContain("ServerStateVerified", output); Assert.DoesNotContain("\"atomicNativeTupleProven\":true", output);
            Assert.DoesNotContain("\"liveStoreSynchronized\":true", output);
        }
        Assert.Equal(water, f.HighWater); Unchanged(f, before, sent);
    });

    private static string Repository([CallerFilePath] string source = "")
    {
        for (var directory = new DirectoryInfo(Path.GetDirectoryName(source)!); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Actual test repository unavailable.");
    }
}
