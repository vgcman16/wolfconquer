using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.Web;

namespace WolfConquer.Core.Client;

public sealed record ClientServerListEntry(
    string GroupName,
    int ServerIndex,
    string ServerName,
    string Address,
    int Port,
    string SourcePath);

public sealed record ClientRoutingStep(
    int Order,
    string Role,
    string Source,
    string CurrentAddress,
    int CurrentPort,
    string LocalAddress,
    int? LocalPort,
    string Status,
    string Action,
    string Verification);

public sealed record ClientRoutingPlan(IReadOnlyList<ClientRoutingStep> Steps);

public static class ClientRoutingPlanner
{
    public static ClientRoutingPlan Create(
        IReadOnlyList<ClientServerListEntry> serverListEntries,
        IReadOnlyList<ClientConfigRecord> clientConfigRecords,
        IReadOnlyList<ClientSocketConfigEndpoint>? socketConfigEndpoints = null,
        string localAddress = "127.0.0.1")
    {
        ArgumentNullException.ThrowIfNull(serverListEntries);
        ArgumentNullException.ThrowIfNull(clientConfigRecords);
        ArgumentException.ThrowIfNullOrWhiteSpace(localAddress);

        var steps = new List<ClientRoutingStep>();

        AddReadyStep(
            steps,
            "LoginWeb",
            "configured login loader files",
            LoginLoaderHttpResponder.LoginLoaderHost,
            LoginLoaderWebServerRunner.DefaultPort,
            localAddress,
            LoginLoaderWebServerRunner.DefaultPort,
            "Configure the loader response files, add a hosts mapping for p.99.com, and run WolfConquer.LoginWeb before changing client routing.",
            "Validate cqimiver.txt and cqimi.txt using loginweb-loader-readiness with independently supplied expected files, then test login. Ready here means a route plan is available; HTTP readiness still requires verification.");
        AddRuntimeEndpointStep(steps, ConquerRuntimeDefaults.Gateway, localAddress);
        AddRuntimeEndpointStep(steps, ConquerRuntimeDefaults.AccountServer, localAddress);
        AddRuntimeEndpointStep(steps, ConquerRuntimeDefaults.WorldServer, localAddress);

        foreach (var endpoint in socketConfigEndpoints ?? [])
        {
            AddDeferredStep(
                steps,
                $"AutoPatch {endpoint.SectionName}/Server{endpoint.ServerIndex} {endpoint.ServerName} {endpoint.EndpointKind}",
                endpoint.SourcePath,
                endpoint.Address,
                endpoint.Port,
                localAddress,
                "Route this socket only after a local AutoPatch listener or relay capture is running; the live client opens it after GuardShield.",
                "Launch the client and confirm no sockets remain to the original AutoPatch host/port.");
        }

        foreach (var entry in serverListEntries)
        {
            AddDeferredStep(
                steps,
                $"server38.dat {entry.GroupName}/{entry.ServerName}",
                entry.SourcePath,
                entry.Address,
                entry.Port,
                localAddress,
                "Inventory only. Do not edit server38.dat until the matching route is proven locally.",
                "Move this route in a separate checkpoint and verify login immediately after the single-file change.");
        }

        foreach (var record in clientConfigRecords)
        {
            AddDeferredStep(
                steps,
                $"{record.SourceFile}:{record.LineNumber} {record.ServerName}",
                record.SourceFile,
                record.Host,
                record.Port,
                localAddress,
                "Inventory only. These DBServer client_config rows are not the runtime 2021/9960/5818 endpoints.",
                "Keep deferred until the exact consumer for this host/port row is proven.");
        }

        return new ClientRoutingPlan(steps);
    }

    private static void AddRuntimeEndpointStep(
        List<ClientRoutingStep> steps,
        ConquerEndpoint endpoint,
        string localAddress)
    {
        AddReadyStep(
            steps,
            endpoint.Role,
            nameof(ConquerRuntimeDefaults),
            endpoint.Address,
            endpoint.Port,
            localAddress,
            endpoint.Port,
            $"Run WolfConquer.{endpoint.Role} in --listen mode on the local endpoint before routing clients.",
            "Probe the local listener and repeat the current login flow after this endpoint is moved.");
    }

    private static void AddReadyStep(
        List<ClientRoutingStep> steps,
        string role,
        string source,
        string currentAddress,
        int currentPort,
        string localAddress,
        int localPort,
        string action,
        string verification)
    {
        steps.Add(new ClientRoutingStep(
            steps.Count + 1,
            role,
            source,
            currentAddress,
            currentPort,
            localAddress,
            localPort,
            "Ready",
            action,
            verification));
    }

    private static void AddDeferredStep(
        List<ClientRoutingStep> steps,
        string role,
        string source,
        string currentAddress,
        int currentPort,
        string localAddress,
        string action,
        string verification)
    {
        steps.Add(new ClientRoutingStep(
            steps.Count + 1,
            role,
            source,
            currentAddress,
            currentPort,
            localAddress,
            LocalPort: null,
            "Deferred",
            action,
            verification));
    }
}
