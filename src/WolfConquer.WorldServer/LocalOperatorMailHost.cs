using System.Runtime.ExceptionServices;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

internal static class LocalOperatorMailHost
{
    internal static async Task<int> RunAsync(ConquerEndpoint endpoint, string[] args,
        WorldServerFrameHandler handler, LegacyOwnedAdmissionRuntime owner, Guid configuredDomain)
    {
        using var stop = new CancellationTokenSource();
        var pipe = LocalOperatorMailPipe.StartIfEnabled(handler, owner, configuredDomain, stop.Token);
        if (pipe is null)
            return await ConquerServerRunner.RunAsync(endpoint, args, handler.HandleFrameAsync);
        Console.Out.WriteLine("Local operator mail enabled: " + pipe.PipeName + "; current OS user only.");
        Task<int>? server = null;
        var failures = new List<Exception>();
        var result = 1;
        try
        {
            server = ConquerServerRunner.RunAsync(endpoint, args, handler.HandleFrameAsync, stop.Token);
            var first = await Task.WhenAny(server, pipe.Completion);
            if (ReferenceEquals(first, pipe.Completion) && !server.IsCompleted)
            {
                await pipe.Completion;
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
        try { await pipe.DisposeAsync(); }
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
