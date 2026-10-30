using System.Runtime.ExceptionServices;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

internal static class LocalOperatorMailHost
{
    internal static Task<int> RunAsync(ConquerEndpoint endpoint, string[] args,
        WorldServerFrameHandler handler, LegacyOwnedAdmissionRuntime owner, Guid configuredDomain) =>
        RunAsync(endpoint, args, handler, owner, configuredDomain, null);

    internal static async Task<int> RunAsync(ConquerEndpoint endpoint, string[] args,
        WorldServerFrameHandler handler, LegacyOwnedAdmissionRuntime owner, Guid configuredDomain,
        WorldPublicHeartbeatOptions? publicHeartbeat)
    {
        using var stop = new CancellationTokenSource();
        var endpoints = new List<IAsyncDisposable>();
        var workers = new List<Task>();
        Task<int>? server = null;
        var failures = new List<Exception>();
        var result = 1;
        try
        {
            var mail = LocalOperatorMailPipe.StartIfEnabled(handler, owner, configuredDomain, stop.Token);
            if (mail is not null)
            {
                endpoints.Add(mail); workers.Add(mail.Completion);
                Console.Out.WriteLine("Local operator mail enabled: " + mail.PipeName + "; current OS user only.");
            }
            var inspection = LocalRuntimeInspectionPipe.StartIfEnabled(handler, owner, configuredDomain, stop.Token);
            if (inspection is not null)
            {
                endpoints.Add(inspection); workers.Add(inspection.Completion);
                Console.Out.WriteLine("Local runtime diagnostic enabled: " + inspection.PipeName + "; no atomic native tuple or file/runtime equality proof.");
            }
            server = ConquerServerRunner.RunWorldAsync(endpoint, args, handler, publicHeartbeat, stop.Token);
            var first = await Task.WhenAny(workers.Append(server));
            if (!ReferenceEquals(first, server) && !server.IsCompleted)
            {
                await first;
                throw new InvalidOperationException("Operator endpoint stopped before the actual server.");
            }
            result = await server;
        }
        catch (Exception error) { failures.Add(error); }

        try { stop.Cancel(); }
        catch (Exception error) { failures.Add(error); }
        if (server is not null)
        {
            try { await server; }
            catch (Exception error) { AddFailure(failures, error); }
        }
        foreach (var local in endpoints.AsEnumerable().Reverse())
            try { await local.DisposeAsync(); }
            catch (Exception error) { AddFailure(failures, error); }
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException("World/operator shutdown failed.", failures);
        return result;
    }

    private static void AddFailure(List<Exception> failures, Exception error)
    {
        if (!failures.Any(existing => ReferenceEquals(existing, error))) failures.Add(error);
    }
}
