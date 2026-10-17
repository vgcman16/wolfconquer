using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

public sealed class EndpointProtocolEvidenceReporterTests
{
    [Fact]
    public void CreateClassifiesEndpointProtocolEvidenceFromSavedCaptures()
    {
        var firstCapture = new EndpointProbeCapture(
            DateTimeOffset.Parse("2026-06-03T14:11:38Z"),
            TimeoutMs: 1000,
            MaxBytes: 256,
            Samples: 4,
            SampleDelayMs: 100,
            Results:
            [
                Sample("AccountServer", 9960, 1, "xUhpEqgcLYw=", "account-a"),
                Sample("AccountServer", 9960, 2, "xUhpEiQcnj0=", "account-b"),
                QuietSample(1),
                Sample("WorldServer", 5816, 1, Payload(firstByte: 0x01, length: 256), "world-a"),
                ClosedSample("PatchServer", 9528, 1),
            ]);
        var secondCapture = new EndpointProbeCapture(
            DateTimeOffset.Parse("2026-06-03T14:12:38Z"),
            TimeoutMs: 1000,
            MaxBytes: 256,
            Samples: 3,
            SampleDelayMs: 100,
            Results:
            [
                Sample("AccountServer", 9960, 1, "xUhpEm+7Rms=", "account-c"),
                QuietSample(1),
                Sample("WorldServer", 5816, 1, Payload(firstByte: 0x02, length: 256), "world-b"),
            ]);

        var report = EndpointProtocolEvidenceReporter.Create([firstCapture, secondCapture]);

        Assert.Equal(4, report.Count);

        var accountServer = report.Single(endpoint => endpoint.Role == "AccountServer");
        Assert.Equal(EndpointProtocolEvidenceClassification.ServerFirstVariable, accountServer.Classification);
        Assert.Equal(EndpointRouteScope.Local, accountServer.RouteScope);
        Assert.False(accountServer.UnexpectedExternalClientRoute);
        Assert.Equal(2, accountServer.Captures);
        Assert.Equal(3, accountServer.Samples);
        Assert.Equal([8], accountServer.ByteCounts);
        Assert.Equal(3, accountServer.UniqueNonEmptyPayloads);
        Assert.Equal("c5486912", accountServer.StablePrefixHex);
        Assert.Equal(8, accountServer.ServerFirstBytes);
        Assert.Equal(
            "AccountServer routeScope=Local unexpectedExternalClientRoute=False captures=2 samples=3 byteCounts=8 uniqueNonEmptyPayloads=3 stablePrefixHex=c5486912 serverFirstBytes=8 is server-first variable with stable prefix.",
            accountServer.EvidenceSummary);

        var gateway = report.Single(endpoint => endpoint.Role == "Gateway");
        Assert.Equal(EndpointProtocolEvidenceClassification.Quiet, gateway.Classification);
        Assert.Equal(EndpointRouteScope.Local, gateway.RouteScope);
        Assert.False(gateway.UnexpectedExternalClientRoute);
        Assert.Equal(2, gateway.Captures);
        Assert.Equal(2, gateway.Samples);
        Assert.Equal([0], gateway.ByteCounts);
        Assert.Equal(0, gateway.UniqueNonEmptyPayloads);
        Assert.Equal(string.Empty, gateway.StablePrefixHex);
        Assert.Equal(0, gateway.ServerFirstBytes);
        Assert.Equal(
            "Gateway routeScope=Local unexpectedExternalClientRoute=False captures=2 samples=2 byteCounts=0 uniqueNonEmptyPayloads=0 stablePrefixHex=<none> serverFirstBytes=0 is quiet.",
            gateway.EvidenceSummary);

        var worldServer = report.Single(endpoint => endpoint.Role == "WorldServer");
        Assert.Equal(EndpointProtocolEvidenceClassification.ServerFirstVariable, worldServer.Classification);
        Assert.Equal(EndpointRouteScope.Local, worldServer.RouteScope);
        Assert.False(worldServer.UnexpectedExternalClientRoute);
        Assert.Equal(2, worldServer.Captures);
        Assert.Equal(2, worldServer.Samples);
        Assert.Equal([256], worldServer.ByteCounts);
        Assert.Equal(2, worldServer.UniqueNonEmptyPayloads);
        Assert.Equal(string.Empty, worldServer.StablePrefixHex);
        Assert.Equal(256, worldServer.ServerFirstBytes);
        Assert.Equal(
            "WorldServer routeScope=Local unexpectedExternalClientRoute=False captures=2 samples=2 byteCounts=256 uniqueNonEmptyPayloads=2 stablePrefixHex=<none> serverFirstBytes=256 is server-first variable 256 bytes with no stable prefix.",
            worldServer.EvidenceSummary);

        var patchServer = report.Single(endpoint => endpoint.Role == "PatchServer");
        Assert.Equal(EndpointProtocolEvidenceClassification.MissingEvidence, patchServer.Classification);
        Assert.Equal(EndpointRouteScope.Local, patchServer.RouteScope);
        Assert.False(patchServer.UnexpectedExternalClientRoute);
        Assert.Equal(1, patchServer.Captures);
        Assert.Equal(1, patchServer.Samples);
        Assert.Equal([0], patchServer.ByteCounts);
        Assert.Equal(0, patchServer.UniqueNonEmptyPayloads);
        Assert.Equal(string.Empty, patchServer.StablePrefixHex);
        Assert.Equal(0, patchServer.ServerFirstBytes);
        Assert.Equal(
            "PatchServer routeScope=Local unexpectedExternalClientRoute=False captures=1 samples=1 byteCounts=0 uniqueNonEmptyPayloads=0 stablePrefixHex=<none> serverFirstBytes=0 is missing evidence.",
            patchServer.EvidenceSummary);
    }

    [Fact]
    public void CreateFlagsUnexpectedExternalLauncherTraceRoutes()
    {
        var capture = new EndpointProbeCapture(
            DateTimeOffset.Parse("2026-07-06T14:12:38Z"),
            TimeoutMs: 1000,
            MaxBytes: 256,
            Samples: 1,
            SampleDelayMs: 0,
            Results:
            [
                Sample("LocalLoginWeb", 80, 1, "AQID", "local-loginweb", address: "127.0.0.1"),
                Sample("CachedDx9LoginWeb", 80, 1, "BAUG", "external-loginweb", address: "198.51.100.10"),
                Sample("CachedDx9Route", 9865, 1, "BwgJ", "external-route", address: "192.0.2.10"),
            ]);

        var report = EndpointProtocolEvidenceReporter.Create([capture]);

        var localLoginWeb = report.Single(endpoint => endpoint.Role == "LocalLoginWeb");
        Assert.Equal(EndpointRouteScope.Local, localLoginWeb.RouteScope);
        Assert.False(localLoginWeb.UnexpectedExternalClientRoute);
        Assert.Contains(
            "routeScope=Local unexpectedExternalClientRoute=False",
            localLoginWeb.EvidenceSummary);

        var externalLoginWeb = report.Single(endpoint => endpoint.Role == "CachedDx9LoginWeb");
        Assert.Equal(EndpointRouteScope.External, externalLoginWeb.RouteScope);
        Assert.True(externalLoginWeb.UnexpectedExternalClientRoute);
        Assert.Contains(
            "routeScope=External unexpectedExternalClientRoute=True",
            externalLoginWeb.EvidenceSummary);

        var externalRoute = report.Single(endpoint => endpoint.Role == "CachedDx9Route");
        Assert.Equal(EndpointRouteScope.External, externalRoute.RouteScope);
        Assert.True(externalRoute.UnexpectedExternalClientRoute);
        Assert.Contains(
            "routeScope=External unexpectedExternalClientRoute=True",
            externalRoute.EvidenceSummary);
    }

    private static EndpointProbeSample Sample(
        string role,
        int port,
        int sample,
        string payloadBase64,
        string payloadSha256,
        string address = "127.0.0.1")
    {
        return new EndpointProbeSample(
            sample,
            role,
            address,
            port,
            Connected: true,
            TimedOut: false,
            RemoteClosed: false,
            BytesRead: Convert.FromBase64String(payloadBase64).Length,
            payloadBase64,
            payloadSha256,
            ElapsedMs: 10,
            Error: null);
    }

    private static EndpointProbeSample QuietSample(int sample)
    {
        return new EndpointProbeSample(
            sample,
            "Gateway",
            "127.0.0.1",
            2021,
            Connected: true,
            TimedOut: true,
            RemoteClosed: false,
            BytesRead: 0,
            PayloadBase64: string.Empty,
            PayloadSha256: string.Empty,
            ElapsedMs: 1000,
            Error: null);
    }

    private static EndpointProbeSample ClosedSample(
        string role,
        int port,
        int sample)
    {
        return new EndpointProbeSample(
            sample,
            role,
            "127.0.0.1",
            port,
            Connected: false,
            TimedOut: false,
            RemoteClosed: true,
            BytesRead: 0,
            PayloadBase64: string.Empty,
            PayloadSha256: string.Empty,
            ElapsedMs: 7,
            Error: "remote closed");
    }

    private static string Payload(int firstByte, int length)
    {
        var payload = Enumerable
            .Range(0, length)
            .Select(index => (byte)((firstByte + index) % 256))
            .ToArray();

        payload[0] = (byte)firstByte;
        return Convert.ToBase64String(payload);
    }
}
