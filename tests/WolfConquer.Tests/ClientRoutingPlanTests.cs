using WolfConquer.Core.Client;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class ClientRoutingPlanTests
{
    [Fact]
    public void ParseReadsGroupedCp936ServerListEntries()
    {
        var bytes = EncodeCp936(
            "[Server]\r\n" +
            "GroupAmount=2\r\n" +
            "Group1=GroupPic8\r\n" +
            "Group2=GroupPic9\r\n" +
            "\r\n" +
            "[Group1]\r\n" +
            "ServerAmount=1\r\n" +
            "Server1=Group1Server1\r\n" +
            "Ip1=203.0.113.10\r\n" +
            "Port1=9958\r\n" +
            "ServerName1=龙拳一\r\n" +
            "\r\n" +
            "[Group2]\r\n" +
            "ServerAmount=1\r\n" +
            "Server1=Group2Server1\r\n" +
            "Ip1=198.51.100.20\r\n" +
            "Port1=9959\r\n" +
            "ServerName1=Dragon Crossing\r\n");

        var entries = ClientServerListParser.Parse(bytes, ClientTextEncodingKind.CodePage936, "fixture-server38.dat");

        Assert.Equal(2, entries.Count);
        Assert.Equal(new ClientServerListEntry("Group1", 1, "龙拳一", "203.0.113.10", 9958, "fixture-server38.dat"), entries[0]);
        Assert.Equal(new ClientServerListEntry("Group2", 1, "Dragon Crossing", "198.51.100.20", 9959, "fixture-server38.dat"), entries[1]);
    }

    [Fact]
    public void ParseReadsAutoPatchSocketConfigPrimaryAndFallbackEndpoints()
    {
        var bytes = EncodeCp936(
            "[Header]\r\n" +
            "GroupAmount=1\r\n" +
            "\r\n" +
            "[Group1]\r\n" +
            "count=1\r\n" +
            "Server4=U4 198.51.100.11 9537 198.51.100.17 9532 1 1\r\n");

        var entries = ClientSocketConfigParser.Parse(bytes, ClientTextEncodingKind.CodePage936);

        Assert.Equal(2, entries.Count);
        Assert.Equal(
            new ClientSocketConfigEndpoint(
                "Group1",
                4,
                "U4",
                "primary",
                "198.51.100.11",
                9537,
                "AutoPatch/SocketConfig.ini",
                6),
            entries[0]);
        Assert.Equal(
            new ClientSocketConfigEndpoint(
                "Group1",
                4,
                "U4",
                "fallback",
                "198.51.100.17",
                9532,
                "AutoPatch/SocketConfig.ini",
                6),
            entries[1]);
    }

    [Fact]
    public void CreateOrdersReadyLocalRoutesBeforeDeferredInventory()
    {
        ClientServerListEntry[] serverListEntries =
        [
            new("Group1", 1, "龙拳一", "203.0.113.10", 9958, "server38.dat"),
            new("Group2", 1, "Dragon Crossing", "198.51.100.20", 9959, "server38.dat"),
        ];
        ClientConfigRecord[] clientConfigRecords =
        [
            CreateClientConfig(2, "TestSrv", "127.0.0.1", 9220, 1),
            CreateClientConfig(101, "Realm1", "198.51.100.16", 9221, 2),
        ];

        var plan = ClientRoutingPlanner.Create(serverListEntries, clientConfigRecords);

        Assert.Equal(8, plan.Steps.Count);
        Assert.Equal(Enumerable.Range(1, 8), plan.Steps.Select(step => step.Order));

        AssertRoute(
            plan.Steps[0],
            "LoginWeb",
            "configured login loader files",
            "p.99.com",
            80,
            "127.0.0.1",
            80,
            "Ready");
        Assert.Contains("hosts mapping", plan.Steps[0].Action, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LoginWeb", plan.Steps[0].Action, StringComparison.Ordinal);
        Assert.Contains("cqimiver.txt", plan.Steps[0].Verification, StringComparison.Ordinal);
        Assert.Contains("cqimi.txt", plan.Steps[0].Verification, StringComparison.Ordinal);

        AssertRoute(plan.Steps[1], "Gateway", "ConquerRuntimeDefaults", "127.0.0.1", 2021, "127.0.0.1", 2021, "Ready");
        AssertRoute(plan.Steps[2], "AccountServer", "ConquerRuntimeDefaults", "127.0.0.1", 9960, "127.0.0.1", 9960, "Ready");
        AssertRoute(plan.Steps[3], "WorldServer", "ConquerRuntimeDefaults", "127.0.0.1", 5818, "127.0.0.1", 5818, "Ready");

        AssertRoute(plan.Steps[4], "server38.dat Group1/龙拳一", "server38.dat", "203.0.113.10", 9958, "127.0.0.1", null, "Deferred");
        AssertRoute(plan.Steps[5], "server38.dat Group2/Dragon Crossing", "server38.dat", "198.51.100.20", 9959, "127.0.0.1", null, "Deferred");
        AssertRoute(plan.Steps[6], "client_config.ini:1 TestSrv", "client_config.ini", "127.0.0.1", 9220, "127.0.0.1", null, "Deferred");
        AssertRoute(plan.Steps[7], "client_config.ini:2 Realm1", "client_config.ini", "198.51.100.16", 9221, "127.0.0.1", null, "Deferred");
    }

    [Fact]
    public void CreateKeepsDbServerClientConfigPortsDeferredAndSeparateFromRuntimeRoutes()
    {
        var clientConfigRecords = new[]
        {
            CreateClientConfig(2, "TestSrv", "127.0.0.1", 9220, 1),
            CreateClientConfig(101, "Realm1", "198.51.100.16", 9221, 2),
        };

        var plan = ClientRoutingPlanner.Create([], clientConfigRecords);

        Assert.Equal([80, 2021, 9960, 5818], plan.Steps.Where(step => step.Status == "Ready").Select(step => step.CurrentPort));

        var dbServerInventory = plan.Steps.Where(step => step.CurrentPort is 9220 or 9221).ToArray();
        Assert.Equal(2, dbServerInventory.Length);
        Assert.All(dbServerInventory, step =>
        {
            Assert.Equal("Deferred", step.Status);
            Assert.Null(step.LocalPort);
            Assert.StartsWith("client_config.ini:", step.Role, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void CreateUsesSelectedLocalAddressForStagedRoutes()
    {
        var plan = ClientRoutingPlanner.Create([], [], localAddress: "127.0.0.1");

        Assert.Equal(4, plan.Steps.Count);
        Assert.All(plan.Steps, step => Assert.Equal("127.0.0.1", step.LocalAddress));
        Assert.Equal([80, 2021, 9960, 5818], plan.Steps.Select(step => step.LocalPort));
    }

    [Fact]
    public void CreateIncludesAutoPatchSocketConfigAsDeferredLiveClientInventory()
    {
        ClientSocketConfigEndpoint[] socketConfigEndpoints =
        [
            new("Group1", 4, "U4", "primary", "198.51.100.11", 9537, "AutoPatch/SocketConfig.ini", 6),
            new("Group1", 4, "U4", "fallback", "198.51.100.17", 9532, "AutoPatch/SocketConfig.ini", 6),
        ];

        var plan = ClientRoutingPlanner.Create([], [], socketConfigEndpoints);

        Assert.Equal(6, plan.Steps.Count);
        AssertRoute(plan.Steps[4], "AutoPatch Group1/Server4 U4 primary", "AutoPatch/SocketConfig.ini", "198.51.100.11", 9537, "127.0.0.1", null, "Deferred");
        AssertRoute(plan.Steps[5], "AutoPatch Group1/Server4 U4 fallback", "AutoPatch/SocketConfig.ini", "198.51.100.17", 9532, "127.0.0.1", null, "Deferred");
        Assert.All(plan.Steps.Skip(4), step =>
        {
            Assert.Contains("AutoPatch listener", step.Action, StringComparison.Ordinal);
            Assert.Contains("original AutoPatch host/port", step.Verification, StringComparison.Ordinal);
        });
    }

    private static byte[] EncodeCp936(string text)
    {
        return ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936).GetBytes(text);
    }

    private static ClientConfigRecord CreateClientConfig(int serverId, string serverName, string host, int port, int lineNumber)
    {
        var rawLine = $"{serverId} {serverName} 3935 0222 0214 0001 0000 1 3 {host} {port}";
        var fields = rawLine.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        return new ClientConfigRecord(
            serverId,
            serverName,
            3935,
            "0222",
            222,
            "0214",
            214,
            "0001",
            1,
            "0000",
            0,
            1,
            3,
            host,
            port,
            "client_config.ini",
            lineNumber,
            rawLine,
            fields);
    }

    private static void AssertRoute(
        ClientRoutingStep step,
        string role,
        string source,
        string currentAddress,
        int currentPort,
        string localAddress,
        int? localPort,
        string status)
    {
        Assert.Equal(role, step.Role);
        Assert.Equal(source, step.Source);
        Assert.Equal(currentAddress, step.CurrentAddress);
        Assert.Equal(currentPort, step.CurrentPort);
        Assert.Equal(localAddress, step.LocalAddress);
        Assert.Equal(localPort, step.LocalPort);
        Assert.Equal(status, step.Status);
    }
}
