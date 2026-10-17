using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

// Payloads and timestamps below are authored fixtures. Account samples retain only
// the fixed protocol signature; their four-byte seeds are deliberately sequential.
public sealed class EndpointProbeAnalysisTests
{
    [Fact]
    public void AnalyzerFindsStablePrefixAndVariablePayloads()
    {
        var capture = new EndpointProbeCapture(
            DateTimeOffset.Parse("2000-01-01T00:00:00Z"),
            TimeoutMs: 1000,
            MaxBytes: 256,
            Samples: 3,
            SampleDelayMs: 100,
            Results:
            [
                Sample("AccountServer", 9960, 1, "xUhpEhAREhM=", "31521cf4b2f801c83f0d5731ce9f90c72a5ffece0030ad35ca17f6209e3a14d8"),
                Sample("AccountServer", 9960, 2, "xUhpEiAhIiM=", "9715ef1cf838cda0a65f5ff63a5f70d014d8e3692199d7ebe461dcee44850e6c"),
                Sample("AccountServer", 9960, 3, "xUhpEjAxMjM=", "0f65b8e0e2cf1dbf35a8ec930e47b845f6f0540cf74fc13347e31cdd317d29b4"),
            ]);

        var analysis = EndpointProbeAnalyzer.Analyze(capture).Single();

        Assert.Equal("AccountServer", analysis.Role);
        Assert.Equal(3, analysis.Samples);
        Assert.True(analysis.AllConnected);
        Assert.Equal([8], analysis.ByteCounts);
        Assert.Equal(3, analysis.UniqueNonEmptyPayloads);
        Assert.Equal(4, analysis.StablePrefixBytes);
        Assert.Equal("c5486912", analysis.StablePrefixHex);
    }

    [Fact]
    public void AnalyzerReportsQuietEndpointWithNoStablePrefix()
    {
        var capture = new EndpointProbeCapture(
            DateTimeOffset.Parse("2000-01-01T00:00:00Z"),
            TimeoutMs: 1000,
            MaxBytes: 256,
            Samples: 2,
            SampleDelayMs: 100,
            Results:
            [
                QuietSample(1),
                QuietSample(2),
            ]);

        var analysis = EndpointProbeAnalyzer.Analyze(capture).Single();

        Assert.Equal("Gateway", analysis.Role);
        Assert.True(analysis.AllConnected);
        Assert.True(analysis.AnyTimedOut);
        Assert.Equal([0], analysis.ByteCounts);
        Assert.Equal(0, analysis.UniqueNonEmptyPayloads);
        Assert.Equal(0, analysis.StablePrefixBytes);
        Assert.Equal(string.Empty, analysis.StablePrefixHex);
    }

    [Fact]
    public void AnalyzerClassifiesLocalAndUnexpectedExternalTraceRoutes()
    {
        var capture = new EndpointProbeCapture(
            DateTimeOffset.Parse("2000-01-02T00:00:00Z"),
            TimeoutMs: 1000,
            MaxBytes: 256,
            Samples: 1,
            SampleDelayMs: 0,
            Results:
            [
                Sample("LocalLoginWeb", 80, 1, "AQID", "039058c6f2c0cb492c533b0a4d14ef77cc0f78abccced5287d84a1a2011cfb81", address: "127.0.0.1"),
                Sample("CachedDx9LoginWeb", 80, 1, "BAUG", "787c798e39a5bc1910355bae6d0cd87a36b2e10fd0202a83e3bb6b005da83472", address: "198.51.100.10"),
                Sample("CachedDx9Route", 9865, 1, "BwgJ", "66a6757151f8ee55db127716c7e3dce0be8074b64e20eda542e5c1e46ca9c41e", address: "192.0.2.10"),
            ]);

        var analyses = EndpointProbeAnalyzer.Analyze(capture);

        var localLoginWeb = analyses.Single(analysis => analysis.Role == "LocalLoginWeb");
        Assert.Equal(EndpointRouteScope.Local, localLoginWeb.RouteScope);
        Assert.False(localLoginWeb.UnexpectedExternalClientRoute);

        var externalLoginWeb = analyses.Single(analysis => analysis.Role == "CachedDx9LoginWeb");
        Assert.Equal(EndpointRouteScope.External, externalLoginWeb.RouteScope);
        Assert.True(externalLoginWeb.UnexpectedExternalClientRoute);

        var externalRoute = analyses.Single(analysis => analysis.Role == "CachedDx9Route");
        Assert.Equal(EndpointRouteScope.External, externalRoute.RouteScope);
        Assert.True(externalRoute.UnexpectedExternalClientRoute);
    }

    [Fact]
    public void CompareGroupsEndpointsAcrossCapturesAndComputesStablePrefixes()
    {
        var firstCapture = new EndpointProbeCapture(
            DateTimeOffset.Parse("2000-01-01T00:00:00Z"),
            TimeoutMs: 1000,
            MaxBytes: 256,
            Samples: 3,
            SampleDelayMs: 100,
            Results:
            [
                Sample("AccountServer", 9960, 1, "xUhpEhAREhM=", "31521cf4b2f801c83f0d5731ce9f90c72a5ffece0030ad35ca17f6209e3a14d8"),
                Sample("AccountServer", 9960, 2, "xUhpEiAhIiM=", "9715ef1cf838cda0a65f5ff63a5f70d014d8e3692199d7ebe461dcee44850e6c"),
                Sample("WorldServer", 5816, 1, "YGFiY2RlZmc=", "5b55852bbfb104bec95903ac5d6f97b8656a6309a4ccb2cca418a23bebf571a0"),
            ]);
        var secondCapture = new EndpointProbeCapture(
            DateTimeOffset.Parse("2000-01-01T00:01:00Z"),
            TimeoutMs: 1000,
            MaxBytes: 256,
            Samples: 2,
            SampleDelayMs: 100,
            Results:
            [
                Sample("AccountServer", 9960, 1, "xUhpEkBBQkM=", "0245b1cfdc58506a125518e1ae1dbd7e43ca28110176be9a26e89b2eeaead54b"),
                Sample("WorldServer", 5816, 1, "cHFyc3R1dnc=", "2dc770fd44d44afea53d10b3282d16d91bb4b0473163ee2c4d3444fe97b2c758"),
            ]);

        var comparisons = EndpointProbeAnalyzer.Compare([firstCapture, secondCapture]);

        Assert.Collection(
            comparisons,
            accountServer =>
            {
                Assert.Equal("AccountServer", accountServer.Role);
                Assert.Equal("127.0.0.1", accountServer.Address);
                Assert.Equal(9960, accountServer.Port);
                Assert.Equal(2, accountServer.Captures);
                Assert.Equal(3, accountServer.Samples);
                Assert.True(accountServer.AllConnected);
                Assert.False(accountServer.AnyTimedOut);
                Assert.False(accountServer.AnyRemoteClosed);
                Assert.Equal([8], accountServer.ByteCounts);
                Assert.Equal(3, accountServer.UniqueNonEmptyPayloads);
                Assert.Equal(4, accountServer.StablePrefixBytes);
                Assert.Equal("c5486912", accountServer.StablePrefixHex);
            },
            worldServer =>
            {
                Assert.Equal("WorldServer", worldServer.Role);
                Assert.Equal("127.0.0.1", worldServer.Address);
                Assert.Equal(5816, worldServer.Port);
                Assert.Equal(2, worldServer.Captures);
                Assert.Equal(2, worldServer.Samples);
                Assert.True(worldServer.AllConnected);
                Assert.False(worldServer.AnyTimedOut);
                Assert.False(worldServer.AnyRemoteClosed);
                Assert.Equal([8], worldServer.ByteCounts);
                Assert.Equal(2, worldServer.UniqueNonEmptyPayloads);
                Assert.Equal(0, worldServer.StablePrefixBytes);
                Assert.Equal(string.Empty, worldServer.StablePrefixHex);
            });
    }

    [Fact]
    public void CompareCountsEndpointPresenceAndOrdersLikeAnalyze()
    {
        var firstCapture = new EndpointProbeCapture(
            DateTimeOffset.Parse("2000-01-01T00:00:00Z"),
            TimeoutMs: 1000,
            MaxBytes: 256,
            Samples: 3,
            SampleDelayMs: 100,
            Results:
            [
                Sample("WorldServer", 5817, 1, "AQID", "039058c6f2c0cb492c533b0a4d14ef77cc0f78abccced5287d84a1a2011cfb81"),
                QuietSample(1),
                Sample("AccountServer", 9960, 1, "xUhpEhAREhM=", "31521cf4b2f801c83f0d5731ce9f90c72a5ffece0030ad35ca17f6209e3a14d8"),
            ]);
        var secondCapture = new EndpointProbeCapture(
            DateTimeOffset.Parse("2000-01-01T00:01:00Z"),
            TimeoutMs: 1000,
            MaxBytes: 256,
            Samples: 2,
            SampleDelayMs: 100,
            Results:
            [
                ClosedSample("Gateway", 2021, 1),
                Sample("WorldServer", 5816, 1, "BAUGBw==", "c6d44cf418f610e3fe9e1d9294ff43def81c6cdcad6cbb1820cff48d3aa4355d"),
            ]);

        var comparisons = EndpointProbeAnalyzer.Compare([firstCapture, secondCapture]);

        Assert.Equal(
            [
                ("AccountServer", 9960, "127.0.0.1"),
                ("Gateway", 2021, "127.0.0.1"),
                ("WorldServer", 5816, "127.0.0.1"),
                ("WorldServer", 5817, "127.0.0.1"),
            ],
            comparisons.Select(comparison => (comparison.Role, comparison.Port, comparison.Address)).ToArray());

        var gateway = comparisons.Single(comparison => comparison.Role == "Gateway");
        Assert.Equal(2, gateway.Captures);
        Assert.Equal(2, gateway.Samples);
        Assert.False(gateway.AllConnected);
        Assert.True(gateway.AnyTimedOut);
        Assert.True(gateway.AnyRemoteClosed);
        Assert.Equal([0], gateway.ByteCounts);
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
}
