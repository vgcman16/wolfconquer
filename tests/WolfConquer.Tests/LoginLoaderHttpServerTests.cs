using System.Net;
using System.Text;
using WolfConquer.Core.Web;

namespace WolfConquer.Tests;

[Collection(LoginLoaderConfigurationCollection.Name)]
public sealed class LoginLoaderHttpServerTests
{
    public static IEnumerable<object[]> Routes => LoginLoaderHttpResponder.Routes.Select(route => new object[] { route.Path });

    [Theory]
    [MemberData(nameof(Routes))]
    public void ResponderServesOnlyConfiguredBytesAndNormalizesQueryCaseAndLeadingSlash(string path)
    {
        using var fixture = new LoginLoaderPayloadFixture();
        var route = Assert.Single(LoginLoaderHttpResponder.Routes, item => item.Path == path);

        var response = LoginLoaderHttpResponder.Get("get", path.TrimStart('/').ToUpperInvariant() + "?fixture=1");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("OK", response.ReasonPhrase);
        Assert.Equal(route.ContentType, response.ContentType);
        Assert.Equal(LoginLoaderPayloadFixture.PayloadFor(path), response.Body);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void EverySupportedRouteIsUnavailableWithoutConfiguration(string path)
    {
        using var fixture = new LoginLoaderPayloadFixture();
        var route = Assert.Single(LoginLoaderHttpResponder.Routes, item => item.Path == path);
        Environment.SetEnvironmentVariable(route.EnvironmentVariable, null);

        var response = LoginLoaderHttpResponder.Get("GET", path);

        Assert.Equal(503, response.StatusCode);
        Assert.Equal("Service Unavailable", response.ReasonPhrase);
        Assert.Contains(route.EnvironmentVariable, Encoding.ASCII.GetString(response.Body));
    }

    [Fact]
    public void MissingConfiguredFileReturnsUnavailableWithoutExposingItsPath()
    {
        using var fixture = new LoginLoaderPayloadFixture();
        var file = fixture.FileFor(LoginLoaderHttpResponder.ManifestPathEnvironmentVariable);
        File.Delete(file);

        var response = LoginLoaderHttpResponder.Get("GET", LoginLoaderHttpResponder.ManifestPath);

        Assert.Equal(503, response.StatusCode);
        Assert.DoesNotContain(file, Encoding.ASCII.GetString(response.Body));
    }

    [Fact]
    public void ConfiguredDirectoryReturnsUnavailable()
    {
        using var fixture = new LoginLoaderPayloadFixture();
        var file = fixture.FileFor(LoginLoaderHttpResponder.ManifestPathEnvironmentVariable);
        Environment.SetEnvironmentVariable(LoginLoaderHttpResponder.ManifestPathEnvironmentVariable, Path.GetDirectoryName(file));

        Assert.Equal(503, LoginLoaderHttpResponder.Get("GET", LoginLoaderHttpResponder.ManifestPath).StatusCode);
    }

    [Fact]
    public void ResponderDoesNotReplaceAnExplicitlyEmptyFileWithFallbackContent()
    {
        using var fixture = new LoginLoaderPayloadFixture();
        File.WriteAllBytes(fixture.FileFor(LoginLoaderHttpResponder.LauncherUpdateManifestPathEnvironmentVariable), []);

        var response = LoginLoaderHttpResponder.Get("GET", LoginLoaderHttpResponder.LauncherUpdateManifestPath);

        Assert.Equal(200, response.StatusCode);
        Assert.Empty(response.Body);
    }

    [Fact]
    public void ResponderReturnsNotFoundForUnknownPath()
    {
        var response = LoginLoaderHttpResponder.Get("GET", "/missing-fixture");
        Assert.Equal(404, response.StatusCode);
        Assert.Empty(response.Body);
    }

    [Fact]
    public void ResponderRejectsUnsupportedMethods()
    {
        var response = LoginLoaderHttpResponder.Get("POST", LoginLoaderHttpResponder.VersionPath);
        Assert.Equal(405, response.StatusCode);
        Assert.Empty(response.Body);
    }

    [Fact]
    public async Task ServerPreservesBinaryBytesAndHttpStatusOverLoopback()
    {
        using var fixture = new LoginLoaderPayloadFixture();
        await using var server = LoginLoaderHttpServer.Start(IPAddress.Loopback, port: 0);
        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{server.BoundEndPoint.Port}/") };

        using var manifest = await client.GetAsync(LoginLoaderHttpResponder.ManifestPath + "?fixture=1");
        Assert.Equal(HttpStatusCode.OK, manifest.StatusCode);
        Assert.Equal(LoginLoaderPayloadFixture.Manifest, await manifest.Content.ReadAsByteArrayAsync());
        Assert.Equal((long)LoginLoaderPayloadFixture.Manifest.Length, manifest.Content.Headers.ContentLength);
        Assert.Equal("application/octet-stream", manifest.Content.Headers.ContentType?.MediaType);

        Environment.SetEnvironmentVariable(LoginLoaderHttpResponder.VersionPathEnvironmentVariable, null);
        using var unavailable = await client.GetAsync(LoginLoaderHttpResponder.VersionPath);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        using var unknown = await client.GetAsync("/missing-fixture");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        using var unsupported = await client.PostAsync(LoginLoaderHttpResponder.ManifestPath, null);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, unsupported.StatusCode);
    }
}
