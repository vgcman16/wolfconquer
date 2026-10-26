using System.Runtime.ExceptionServices;

namespace WolfConquer.Tests;

// Owns only processes this scenario creates. Never discovers or stops another server.
internal sealed class OwnedHostScenario
{
    internal LegacyOwnedAdmissionRuntimeFixture Data { get; } = new();
    private readonly List<OwnedWorldServerProcess> children = [];

    internal OwnedWorldServerProcess Create(bool encrypted, bool configured = true, bool listen = true)
    {
        var child = new OwnedWorldServerProcess(Data, encrypted, configured, listen);
        children.Add(child);
        return child;
    }

    internal static async Task RunAsync(Func<OwnedHostScenario, Task> body)
    {
        var scenario = new OwnedHostScenario();
        var failures = new List<Exception>();
        try { await body(scenario); }
        catch (Exception error) { failures.Add(error); }
        foreach (var child in scenario.children.AsEnumerable().Reverse())
        {
            try { await child.DisposeAsync(); }
            catch (Exception error) { failures.Add(error); }
        }
        if (scenario.children.All(child => child.Stopped))
        {
            try { scenario.Data.Dispose(); }
            catch (Exception error) { failures.Add(error); }
        }
        else failures.Add(new IOException("Owned server did not terminate; owned fixture retained: " + scenario.Data.Root));
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException("Owned host assertion and/or cleanup failed.", failures);
    }
}
