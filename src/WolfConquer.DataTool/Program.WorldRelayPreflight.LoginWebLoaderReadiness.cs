using System.IO;
using System.Net.Http;
using System.Text;
using WolfConquer.Core.Web;

internal static partial class Program
{
    static LoginWebLoaderReadinessOptions ResolveLoginWebLoaderReadinessOptions(string[] args)
    {
        Uri? baseUri = null;
        var timeoutMs = 5000;
        string? expectedVersionFile = null;
        string? expectedManifestFile = null;

        for (var index = 1; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, "--timeout-ms", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length || !int.TryParse(args[index], out timeoutMs) || timeoutMs <= 0)
                {
                    throw new FormatException("--timeout-ms requires a positive integer.");
                }
            }
            else if (string.Equals(argument, "--expected-version-file", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(argument, "--expected-manifest-file", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length || string.IsNullOrWhiteSpace(args[index]) || args[index].StartsWith("--", StringComparison.Ordinal))
                {
                    throw new FormatException($"{argument} requires a file path.");
                }

                if (string.Equals(argument, "--expected-version-file", StringComparison.OrdinalIgnoreCase))
                {
                    expectedVersionFile = args[index];
                }
                else
                {
                    expectedManifestFile = args[index];
                }
            }
            else if (argument.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unknown loginweb-loader-readiness option: {argument}");
            }
            else if (baseUri is not null)
            {
                throw new ArgumentException("LoginWeb loader readiness accepts at most one base URL.");
            }
            else
            {
                baseUri = LoginWebLoaderReadinessLogic.NormalizeBaseUri(argument);
            }
        }

        return LoginWebLoaderReadinessOptions.FromEnvironment(
            baseUri ?? LoginWebLoaderReadinessLogic.DefaultBaseUri,
            TimeSpan.FromMilliseconds(timeoutMs),
            expectedVersionFile,
            expectedManifestFile);
    }

    static int LoginWebLoaderReadiness(string[] args)
    {
        LoginWebLoaderReadinessOptions options;
        try
        {
            options = ResolveLoginWebLoaderReadinessOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or UriFormatException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        try
        {
            var report = LoginWebLoaderReadinessLogic.CheckAsync(options)
                .GetAwaiter()
                .GetResult();
            Console.Write(LoginWebLoaderReadinessLogic.FormatReport(report));
            return report.Ready ? 0 : 2;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException)
        {
            Console.Error.WriteLine($"LoginWeb loader readiness failed: {ex.Message}");
            return 2;
        }
    }
}

internal static class LoginWebLoaderReadinessLogic
{
    public static readonly Uri DefaultBaseUri = new("http://127.0.0.1/");

    public static Uri NormalizeBaseUri(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("LoginWeb loader readiness base URL is required.", nameof(value));
        }

        var text = value.Contains("://", StringComparison.Ordinal)
            ? value
            : "http://" + value;
        var uri = new Uri(text, UriKind.Absolute);

        if (uri.Scheme is not ("http" or "https"))
        {
            throw new UriFormatException("LoginWeb loader readiness base URL must use http or https.");
        }

        return uri.AbsolutePath == "/"
            ? uri
            : new Uri(uri.GetLeftPart(UriPartial.Authority) + "/");
    }

    public static async Task<LoginWebLoaderReadinessReport> CheckAsync(
        LoginWebLoaderReadinessOptions options,
        Func<HttpRequestMessage, CancellationToken, Task<LoginWebLoaderHttpProbeResponse>>? sendAsync = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Timeout must be positive.");
        }

        using var timeout = new CancellationTokenSource(options.Timeout);
        using var client = sendAsync is null
            ? new HttpClient()
            : null;
        if (sendAsync is null)
        {
            sendAsync = async (request, cancellationToken) =>
            {
                using var response = await client!.SendAsync(request, cancellationToken).ConfigureAwait(false);
                var body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                return new LoginWebLoaderHttpProbeResponse((int)response.StatusCode, body);
            };
        }

        var version = await CheckItemAsync(
                options.BaseUri,
                LoginLoaderHttpResponder.VersionPath,
                options.ExpectedVersion,
                sendAsync,
                timeout.Token)
            .ConfigureAwait(false);
        var manifest = await CheckItemAsync(
                options.BaseUri,
                LoginLoaderHttpResponder.ManifestPath,
                options.ExpectedManifest,
                sendAsync,
                timeout.Token)
            .ConfigureAwait(false);

        return new LoginWebLoaderReadinessReport(
            options.BaseUri,
            LoginLoaderHttpResponder.LoginLoaderHost,
            [version, manifest]);
    }

    public static string FormatReport(LoginWebLoaderReadinessReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("LoginWeb loader readiness:");
        builder.AppendLine($"BaseUrl: {report.BaseUri}");
        builder.AppendLine($"Host: {report.Host}");
        foreach (var item in report.Items)
        {
            builder.AppendLine(
                $"{item.Path} status={item.StatusCode} length={item.Length} " +
                $"expected={item.ExpectedDescription} ready={item.Ready} error={FormatError(item.Error)}");
        }

        builder.AppendLine($"Ready: {report.Ready}");
        return builder.ToString();
    }

    private static async Task<LoginWebLoaderReadinessItem> CheckItemAsync(
        Uri baseUri,
        string path,
        byte[]? expectedBody,
        Func<HttpRequestMessage, CancellationToken, Task<LoginWebLoaderHttpProbeResponse>> sendAsync,
        CancellationToken cancellationToken)
    {
        if (expectedBody is null)
        {
            return new LoginWebLoaderReadinessItem(
                path, 0, 0, "unconfigured", false, "expected payload is not configured or its file is unavailable");
        }

        var expectedDescription = $"{expectedBody.Length} bytes";
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, path));
        request.Headers.Host = LoginLoaderHttpResponder.LoginLoaderHost;
        var response = await sendAsync(request, cancellationToken).ConfigureAwait(false);
        var bodyMatches = response.Body.SequenceEqual(expectedBody);
        var ready = response.StatusCode == 200 && bodyMatches;
        var error = ready
            ? null
            : CreateError(response.StatusCode, response.Body, expectedBody);

        return new LoginWebLoaderReadinessItem(
            path,
            response.StatusCode,
            response.Body.Length,
            expectedDescription,
            ready,
            error);
    }

    private static string CreateError(int statusCode, byte[] body, byte[] expectedBody)
    {
        if (statusCode != 200)
        {
            return "expected status 200";
        }

        if (body.Length != expectedBody.Length)
        {
            return $"expected length {expectedBody.Length}";
        }

        return "unexpected body";
    }

    private static string FormatError(string? error)
    {
        return error ?? "<none>";
    }
}

internal sealed record LoginWebLoaderReadinessOptions(
    Uri BaseUri,
    TimeSpan Timeout,
    byte[]? ExpectedVersion = null,
    byte[]? ExpectedManifest = null)
{
    // These inputs are independent of the files served by LoginWeb.
    public const string ExpectedVersionFileEnvironmentVariable = "WOLFCONQUER_LOGINWEB_EXPECTED_VERSION_FILE";
    public const string ExpectedManifestFileEnvironmentVariable = "WOLFCONQUER_LOGINWEB_EXPECTED_MANIFEST_FILE";

    public static LoginWebLoaderReadinessOptions FromEnvironment(
        Uri baseUri,
        TimeSpan timeout,
        string? expectedVersionFile = null,
        string? expectedManifestFile = null)
    {
        return new LoginWebLoaderReadinessOptions(
            baseUri,
            timeout,
            ReadExpectedFile(expectedVersionFile ?? Environment.GetEnvironmentVariable(ExpectedVersionFileEnvironmentVariable)),
            ReadExpectedFile(expectedManifestFile ?? Environment.GetEnvironmentVariable(ExpectedManifestFileEnvironmentVariable)));
    }

    private static byte[]? ReadExpectedFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return File.ReadAllBytes(Environment.ExpandEnvironmentVariables(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }
}

internal sealed record LoginWebLoaderHttpProbeResponse(int StatusCode, byte[] Body);

internal sealed record LoginWebLoaderReadinessItem(
    string Path,
    int StatusCode,
    int Length,
    string ExpectedDescription,
    bool Ready,
    string? Error);

internal sealed record LoginWebLoaderReadinessReport(
    Uri BaseUri,
    string Host,
    IReadOnlyList<LoginWebLoaderReadinessItem> Items)
{
    public bool Ready => Items.All(item => item.Ready);
}
