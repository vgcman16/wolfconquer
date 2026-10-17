using System.Text.Json;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

internal static partial class Program
{
    static int ProbeEndpoints(string[] args)
    {
        ProbeEndpointOptions options;
        try
        {
            options = ResolveProbeEndpointOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        Console.WriteLine("Passive endpoint probe: no payload bytes are sent.");
        Console.WriteLine($"Timeout: {options.Timeout.TotalMilliseconds:N0} ms");
        Console.WriteLine($"Samples: {options.Samples:N0}");

        var failures = 0;
        var samples = new List<EndpointProbeSample>();
        foreach (var endpoint in options.Endpoints)
        {
            var results = TcpEndpointProbe.ProbeManyAsync(
                endpoint,
                options.Samples,
                options.Timeout,
                options.SampleDelay,
                options.MaxBytes).GetAwaiter().GetResult();

            for (var sampleIndex = 0; sampleIndex < results.Count; sampleIndex++)
            {
                var result = results[sampleIndex];
                var sampleNumber = sampleIndex + 1;
                if (!result.Connected)
                {
                    failures++;
                }

                samples.Add(new EndpointProbeSample(
                    sampleNumber,
                    result.Role,
                    result.Address,
                    result.Port,
                    result.Connected,
                    result.TimedOut,
                    result.RemoteClosed,
                    result.BytesRead,
                    result.PayloadBase64,
                    result.PayloadSha256,
                    result.Elapsed.TotalMilliseconds,
                    result.Error));

                Console.WriteLine(
                    $"{FormatProbeSampleRole(result.Role, sampleNumber, options.Samples)} {result.Address}:{result.Port} " +
                    $"connected={result.Connected} timedOut={result.TimedOut} remoteClosed={result.RemoteClosed} " +
                    $"bytes={result.BytesRead:N0} payloadBase64={result.PayloadBase64} " +
                    $"payloadSha256={result.PayloadSha256}{FormatProbeError(result.Error)} " +
                    $"elapsedMs={result.Elapsed.TotalMilliseconds:N0}");
            }
        }

        if (options.OutputJsonPath is not null)
        {
            var outputDirectory = Path.GetDirectoryName(options.OutputJsonPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            WriteJson(
                options.OutputJsonPath,
                new EndpointProbeCapture(
                    DateTimeOffset.UtcNow,
                    (int)options.Timeout.TotalMilliseconds,
                    options.MaxBytes,
                    options.Samples,
                    (int)options.SampleDelay.TotalMilliseconds,
                    samples));
            Console.WriteLine($"Output JSON: {options.OutputJsonPath}");
        }

        return failures == 0 ? 0 : 2;
    }

    static int AnalyzeEndpointProbes(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Missing endpoint probe JSON path.");
            return 1;
        }

        var path = Path.GetFullPath(args[1]);
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Endpoint probe JSON not found: {path}");
            return 1;
        }

        EndpointProbeCapture? capture;
        try
        {
            capture = JsonSerializer.Deserialize<EndpointProbeCapture>(
                File.ReadAllText(path),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Could not parse endpoint probe JSON: {ex.Message}");
            return 1;
        }

        if (capture is null)
        {
            Console.Error.WriteLine("Endpoint probe JSON is empty.");
            return 1;
        }

        Console.WriteLine($"Endpoint probe analysis: {path}");
        foreach (var analysis in EndpointProbeAnalyzer.Analyze(capture))
        {
            Console.WriteLine(
                $"{analysis.Role} {analysis.Address}:{analysis.Port} samples={analysis.Samples:N0} " +
                $"allConnected={analysis.AllConnected} byteCounts={string.Join(',', analysis.ByteCounts)} " +
                $"uniquePayloads={analysis.UniqueNonEmptyPayloads:N0} " +
                $"stablePrefixBytes={analysis.StablePrefixBytes:N0} stablePrefixHex={analysis.StablePrefixHex} " +
                $"anyTimedOut={analysis.AnyTimedOut} anyRemoteClosed={analysis.AnyRemoteClosed} " +
                $"routeScope={analysis.RouteScope} " +
                $"unexpectedExternalClientRoute={analysis.UnexpectedExternalClientRoute}");
        }

        return 0;
    }

    static int CompareEndpointProbes(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Compare requires at least two endpoint probe JSON paths.");
            return 1;
        }

        var captures = new List<EndpointProbeCapture>();
        for (var index = 1; index < args.Length; index++)
        {
            var path = Path.GetFullPath(args[index]);
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"Endpoint probe JSON not found: {path}");
                return 1;
            }

            EndpointProbeCapture? capture;
            try
            {
                capture = JsonSerializer.Deserialize<EndpointProbeCapture>(
                    File.ReadAllText(path),
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                    });
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine($"Could not parse endpoint probe JSON '{path}': {ex.Message}");
                return 1;
            }

            if (capture is null)
            {
                Console.Error.WriteLine($"Endpoint probe JSON is empty: {path}");
                return 1;
            }

            captures.Add(capture);
        }

        Console.WriteLine($"Endpoint probe comparison: {captures.Count:N0} capture(s)");
        foreach (var comparison in EndpointProbeAnalyzer.Compare(captures))
        {
            Console.WriteLine(
                $"{comparison.Role} {comparison.Address}:{comparison.Port} captures={comparison.Captures:N0} " +
                $"samples={comparison.Samples:N0} allConnected={comparison.AllConnected} " +
                $"byteCounts={string.Join(',', comparison.ByteCounts)} " +
                $"uniquePayloads={comparison.UniqueNonEmptyPayloads:N0} " +
                $"stablePrefixBytes={comparison.StablePrefixBytes:N0} stablePrefixHex={comparison.StablePrefixHex} " +
                $"anyTimedOut={comparison.AnyTimedOut} anyRemoteClosed={comparison.AnyRemoteClosed} " +
                $"routeScope={comparison.RouteScope} " +
                $"unexpectedExternalClientRoute={comparison.UnexpectedExternalClientRoute}");
        }

        return 0;
    }

    static ProbeEndpointOptions ResolveProbeEndpointOptions(string[] args)
    {
        var endpoints = new List<ConquerEndpoint>();
        var timeout = TimeSpan.FromMilliseconds(1000);
        var samples = 1;
        var sampleDelay = TimeSpan.Zero;
        var maxBytes = 256;
        string? outputJsonPath = null;

        for (var index = 1; index < args.Length; index++)
        {
            var option = args[index];
            if (string.Equals(option, "--endpoint", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length)
                {
                    throw new ArgumentException("Missing value for --endpoint.");
                }

                endpoints.Add(ParseProbeEndpoint(args[index]));
                continue;
            }

            if (string.Equals(option, "--timeout-ms", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length)
                {
                    throw new ArgumentException("Missing value for --timeout-ms.");
                }

                if (!int.TryParse(args[index], out var milliseconds) || milliseconds <= 0)
                {
                    throw new ArgumentException("--timeout-ms must be a positive integer.");
                }

                timeout = TimeSpan.FromMilliseconds(milliseconds);
                continue;
            }

            if (string.Equals(option, "--samples", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length)
                {
                    throw new ArgumentException("Missing value for --samples.");
                }

                if (!int.TryParse(args[index], out samples) || samples <= 0)
                {
                    throw new ArgumentException("--samples must be a positive integer.");
                }

                continue;
            }

            if (string.Equals(option, "--sample-delay-ms", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length)
                {
                    throw new ArgumentException("Missing value for --sample-delay-ms.");
                }

                if (!int.TryParse(args[index], out var milliseconds) || milliseconds < 0)
                {
                    throw new ArgumentException("--sample-delay-ms must be zero or a positive integer.");
                }

                sampleDelay = TimeSpan.FromMilliseconds(milliseconds);
                continue;
            }

            if (string.Equals(option, "--max-bytes", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length)
                {
                    throw new ArgumentException("Missing value for --max-bytes.");
                }

                if (!int.TryParse(args[index], out maxBytes) || maxBytes <= 0)
                {
                    throw new ArgumentException("--max-bytes must be a positive integer.");
                }

                continue;
            }

            if (string.Equals(option, "--output-json", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length)
                {
                    throw new ArgumentException("Missing value for --output-json.");
                }

                outputJsonPath = Path.GetFullPath(args[index]);
                continue;
            }

            throw new ArgumentException($"Unknown probe-endpoints option: {option}");
        }

        if (endpoints.Count == 0)
        {
            endpoints.Add(ConquerRuntimeDefaults.Gateway);
            endpoints.Add(ConquerRuntimeDefaults.AccountServer);
            endpoints.Add(ConquerRuntimeDefaults.WorldServer);
        }

        return new ProbeEndpointOptions(endpoints, timeout, samples, sampleDelay, maxBytes, outputJsonPath);
    }

    static ConquerEndpoint ParseProbeEndpoint(string value)
    {
        var equalsIndex = value.IndexOf('=');
        if (equalsIndex <= 0 || equalsIndex == value.Length - 1)
        {
            throw new FormatException("Endpoint must use Role=address:port format.");
        }

        var role = value[..equalsIndex];
        var addressAndPort = value[(equalsIndex + 1)..];
        var colonIndex = addressAndPort.LastIndexOf(':');
        if (colonIndex <= 0 || colonIndex == addressAndPort.Length - 1)
        {
            throw new FormatException("Endpoint must use Role=address:port format.");
        }

        var address = addressAndPort[..colonIndex];
        if (!int.TryParse(addressAndPort[(colonIndex + 1)..], out var port) || port is < 0 or > ushort.MaxValue)
        {
            throw new FormatException("Endpoint port must be between 0 and 65535.");
        }

        return new ConquerEndpoint(role, address, port);
    }

    static string FormatProbeError(string? error)
    {
        return string.IsNullOrWhiteSpace(error) ? string.Empty : $" error={error}";
    }

    static string FormatProbeSampleRole(string role, int sampleNumber, int samples)
    {
        return samples == 1 ? role : $"{role}[{sampleNumber}]";
    }
}

internal sealed record ProbeEndpointOptions(
    IReadOnlyList<ConquerEndpoint> Endpoints,
    TimeSpan Timeout,
    int Samples,
    TimeSpan SampleDelay,
    int MaxBytes,
    string? OutputJsonPath);
