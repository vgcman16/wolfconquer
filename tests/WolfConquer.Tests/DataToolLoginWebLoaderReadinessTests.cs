using System.Diagnostics;
using System.Net;
using WolfConquer.Core.Web;

namespace WolfConquer.Tests;

[Collection(LoginLoaderConfigurationCollection.Name)]
public sealed class DataToolLoginWebLoaderReadinessTests
{
    private static LoginWebLoaderReadinessOptions SyntheticOptions(Uri? baseUri = null) => new(
        baseUri ?? new Uri("http://127.0.0.1:8080/ignored/"),
        TimeSpan.FromSeconds(5),
        LoginLoaderPayloadFixture.Version,
        LoginLoaderPayloadFixture.Manifest);

    [Fact]
    public async Task ReadinessSendsProtocolHostAndComparesIndependentExpectations()
    {
        var requests = new List<(Uri? Uri, string? Host)>();
        var report = await LoginWebLoaderReadinessLogic.CheckAsync(
            SyntheticOptions(),
            (request, _) =>
            {
                requests.Add((request.RequestUri, request.Headers.Host));
                // Construct the response independently of the server's file configuration.
                var body = request.RequestUri!.AbsolutePath == LoginLoaderHttpResponder.VersionPath
                    ? LoginLoaderPayloadFixture.Version
                    : LoginLoaderPayloadFixture.Manifest;
                return Task.FromResult(new LoginWebLoaderHttpProbeResponse(200, body));
            });

        Assert.True(report.Ready);
        Assert.Equal(LoginLoaderHttpResponder.LoginLoaderHost, report.Host);
        Assert.All(report.Items, item => Assert.Equal(200, item.StatusCode));
        Assert.Equal(LoginLoaderPayloadFixture.Version.Length, report.Items[0].Length);
        Assert.Equal(LoginLoaderPayloadFixture.Manifest.Length, report.Items[1].Length);
        Assert.All(requests, request => Assert.Equal(LoginLoaderHttpResponder.LoginLoaderHost, request.Host));
        Assert.Equal(
            new[]
            {
                new Uri("http://127.0.0.1:8080" + LoginLoaderHttpResponder.VersionPath),
                new Uri("http://127.0.0.1:8080" + LoginLoaderHttpResponder.ManifestPath)
            },
            requests.Select(request => request.Uri).ToArray());
        var formatted = LoginWebLoaderReadinessLogic.FormatReport(report);
        Assert.Contains("Ready: True", formatted);
        Assert.DoesNotContain(System.Text.Encoding.ASCII.GetString(LoginLoaderPayloadFixture.Version), formatted);
    }

    [Fact]
    public async Task MissingExpectationsCannotPassEvenWithAnAvailableSender()
    {
        var requests = 0;
        var report = await LoginWebLoaderReadinessLogic.CheckAsync(
            new LoginWebLoaderReadinessOptions(new Uri("http://127.0.0.1/"), TimeSpan.FromSeconds(1)),
            (_, _) =>
            {
                requests++;
                return Task.FromResult(new LoginWebLoaderHttpProbeResponse(200, []));
            });

        Assert.False(report.Ready);
        Assert.Equal(0, requests);
        Assert.All(report.Items, item =>
        {
            Assert.False(item.Ready);
            Assert.Equal(0, item.StatusCode);
            Assert.Contains("expected payload is not configured", item.Error);
        });
    }

    [Theory]
    [InlineData(200, false, true, "expected length 7")]
    [InlineData(200, false, false, "unexpected body")]
    [InlineData(503, true, false, "expected status 200")]
    public async Task ReadinessRejectsWrongLengthContentOrStatus(int status, bool matchingBody, bool shortened, string error)
    {
        var report = await LoginWebLoaderReadinessLogic.CheckAsync(
            SyntheticOptions(),
            (request, _) =>
            {
                if (request.RequestUri!.AbsolutePath == LoginLoaderHttpResponder.VersionPath)
                {
                    return Task.FromResult(new LoginWebLoaderHttpProbeResponse(200, LoginLoaderPayloadFixture.Version));
                }

                var body = LoginLoaderPayloadFixture.Manifest;
                if (!matchingBody)
                {
                    body[0] = 0x22;
                }

                if (shortened)
                {
                    body = body[..^1];
                }

                return Task.FromResult(new LoginWebLoaderHttpProbeResponse(status, body));
            });

        Assert.False(report.Ready);
        Assert.True(report.Items[0].Ready);
        Assert.Equal(error, report.Items[1].Error);
        Assert.Contains("Ready: False", LoginWebLoaderReadinessLogic.FormatReport(report));
    }

    [Fact]
    public async Task ReadinessUsesHttpClientWithSeparatelySuppliedExpectationFiles()
    {
        using var fixture = new LoginLoaderPayloadFixture();
        await using var server = LoginLoaderHttpServer.Start(IPAddress.Loopback, port: 0);
        var options = LoginWebLoaderReadinessOptions.FromEnvironment(
            new Uri($"http://127.0.0.1:{server.BoundEndPoint.Port}/"),
            TimeSpan.FromSeconds(5),
            fixture.ExpectedVersionFile,
            fixture.ExpectedManifestFile);

        Assert.True((await LoginWebLoaderReadinessLogic.CheckAsync(options)).Ready);

        // Mutating the served file must not also change the expected file.
        var changed = LoginLoaderPayloadFixture.Manifest;
        changed[0] = 0x33;
        File.WriteAllBytes(fixture.FileFor(LoginLoaderHttpResponder.ManifestPathEnvironmentVariable), changed);
        var changedReport = await LoginWebLoaderReadinessLogic.CheckAsync(options);
        Assert.False(changedReport.Ready);
        Assert.Equal("unexpected body", changedReport.Items[1].Error);
    }

    [Fact]
    public async Task UnavailableExpectedFileProducesAnUnconfiguredResult()
    {
        using var fixture = new LoginLoaderPayloadFixture();
        File.Delete(fixture.ExpectedManifestFile);
        var options = LoginWebLoaderReadinessOptions.FromEnvironment(
            new Uri("http://127.0.0.1/"), TimeSpan.FromSeconds(1),
            fixture.ExpectedVersionFile, fixture.ExpectedManifestFile);
        var report = await LoginWebLoaderReadinessLogic.CheckAsync(options,
            (_, _) => Task.FromResult(new LoginWebLoaderHttpProbeResponse(200, LoginLoaderPayloadFixture.Version)));

        Assert.False(report.Ready);
        Assert.Equal("unconfigured", report.Items[1].ExpectedDescription);
        Assert.DoesNotContain(fixture.ExpectedManifestFile, LoginWebLoaderReadinessLogic.FormatReport(report));
    }

    [Theory]
    [InlineData("127.0.0.1:8080/path", "http://127.0.0.1:8080/")]
    [InlineData("http://localhost:80", "http://localhost/")]
    public void NormalizeBaseUriKeepsOnlyHttpAuthority(string value, string expected)
    {
        Assert.Equal(new Uri(expected), LoginWebLoaderReadinessLogic.NormalizeBaseUri(value));
    }

    [Fact]
    public async Task CommandLineExpectedFilesEnableAnActualReadinessProbe()
    {
        using var fixture = new LoginLoaderPayloadFixture();
        await using var server = LoginLoaderHttpServer.Start(IPAddress.Loopback, port: 0);
        var result = await RunDataToolAsync(
            "loginweb-loader-readiness", $"http://127.0.0.1:{server.BoundEndPoint.Port}/",
            "--expected-version-file", fixture.ExpectedVersionFile,
            "--expected-manifest-file", fixture.ExpectedManifestFile,
            "--timeout-ms", "5000");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Ready: True", result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task CommandWithoutExpectationsExitsNotReady()
    {
        var result = await RunDataToolAsync("loginweb-loader-readiness");
        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Ready: False", result.StandardOutput);
        Assert.Contains("expected=unconfigured", result.StandardOutput);
    }

    [Fact]
    public async Task CommandRejectsAnExpectedFileOptionWithoutItsPath()
    {
        var result = await RunDataToolAsync("loginweb-loader-readiness", "--expected-version-file");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("--expected-version-file requires a file path", result.StandardError);
    }

    [Fact]
    public async Task HelpMentionsIndependentExpectedFiles()
    {
        var result = await RunDataToolAsync("help");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool loginweb-loader-readiness [baseUrl] [--timeout-ms ms] [--expected-version-file path] [--expected-manifest-file path]",
            result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    private static async Task<ConsoleRunResult> RunDataToolAsync(params string[] arguments)
    {
        var repositoryRoot = FindRepositoryRoot();
        using var process = TestProjectProcess.Create(repositoryRoot, "WolfConquer.DataTool");
        process.StartInfo.Environment.Remove(LoginWebLoaderReadinessOptions.ExpectedVersionFileEnvironmentVariable);
        process.StartInfo.Environment.Remove(LoginWebLoaderReadinessOptions.ExpectedManifestFileEnvironmentVariable);
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        Assert.True(process.Start(), "Could not start DataTool.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail($"DataTool did not exit. stdout: {await outputTask} stderr: {await errorTask}");
        }

        return new ConsoleRunResult(process.ExitCode, await outputTask, await errorTask);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate WolfConquer.sln from the test output directory.");
    }

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);
}
