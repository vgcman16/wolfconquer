using System.Buffers;
using System.Net.Sockets;
using System.Text;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

internal static partial class Program
{
    static int LegacyGuardShieldSmoke(string[] args)
    {
        LegacyGuardShieldSmokeOptions options;
        try
        {
            options = ResolveLegacyGuardShieldSmokeOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        try
        {
            var result = RunLegacyGuardShieldSmokeAsync(options).GetAwaiter().GetResult();
            Console.WriteLine("Legacy GuardShield smoke:");
            Console.WriteLine($"endpoint={options.Address}:{options.Port}");
            Console.WriteLine($"command={options.Command}");
            Console.WriteLine($"response={result.Response}");
            Console.WriteLine($"expected-response={options.ExpectedResponse}");

            if (!string.Equals(result.Response, options.ExpectedResponse, StringComparison.Ordinal))
            {
                Console.Error.WriteLine(
                    $"Expected GuardShield response {options.ExpectedResponse} but received {result.Response}.");
                return 2;
            }

            return 0;
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or InvalidDataException)
        {
            Console.Error.WriteLine($"Legacy GuardShield smoke failed: {ex.Message}");
            return 2;
        }
    }

    static LegacyGuardShieldSmokeOptions ResolveLegacyGuardShieldSmokeOptions(string[] args)
    {
        if (args.Length < 3)
        {
            throw new ArgumentException(
                "Usage: WolfConquer.DataTool legacy-guard-shield-smoke <address> <port> [--command text] [--expect-response text] [--timeout-ms ms]");
        }

        if (!int.TryParse(args[2], out var port) || port is < 1 or > 65535)
        {
            throw new FormatException($"Invalid port: {args[2]}");
        }

        var command = "Welcome:Hash:HD123:HWID456";
        var expectedResponse = LegacyGuardShieldTextProtocolOptions.FromEnvironment().Hashes.ToLegacyResponse();
        var timeoutMs = 5000;
        for (var index = 3; index < args.Length; index++)
        {
            var option = args[index];
            if (string.Equals(option, "--command", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
                {
                    throw new FormatException("--command requires a value.");
                }

                command = args[index];
            }
            else if (string.Equals(option, "--expect-response", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length)
                {
                    throw new FormatException("--expect-response requires a value.");
                }

                expectedResponse = args[index];
            }
            else if (string.Equals(option, "--timeout-ms", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length || !int.TryParse(args[index], out timeoutMs) || timeoutMs <= 0)
                {
                    throw new FormatException("--timeout-ms requires a positive integer.");
                }
            }
            else
            {
                throw new ArgumentException($"Unknown legacy-guard-shield-smoke option: {option}");
            }
        }

        return new LegacyGuardShieldSmokeOptions(
            args[1],
            port,
            command,
            expectedResponse,
            TimeSpan.FromMilliseconds(timeoutMs));
    }

    static async Task<LegacyGuardShieldSmokeResult> RunLegacyGuardShieldSmokeAsync(
        LegacyGuardShieldSmokeOptions options)
    {
        using var timeout = new CancellationTokenSource(options.Timeout);
        using var client = new TcpClient();
        await client.ConnectAsync(options.Address, options.Port, timeout.Token).ConfigureAwait(false);
        var stream = client.GetStream();

        var commandBytes = Encoding.ASCII.GetBytes(options.Command);
        await stream.WriteAsync(commandBytes, timeout.Token).ConfigureAwait(false);
        await stream.FlushAsync(timeout.Token).ConfigureAwait(false);

        var expectedBytes = Encoding.ASCII.GetBytes(options.ExpectedResponse);
        var responseBytes = await ReadLegacyAccountSmokeBytesAsync(
            stream,
            expectedBytes.Length,
            timeout.Token).ConfigureAwait(false);
        var response = Encoding.ASCII.GetString(responseBytes);
        return new LegacyGuardShieldSmokeResult(response);
    }

    static int LegacyAccountSmoke(string[] args)
    {
        LegacyAccountSmokeOptions options;
        try
        {
            options = ResolveLegacyAccountSmokeOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        try
        {
            var result = RunLegacyAccountSmokeAsync(options).GetAwaiter().GetResult();
            Console.WriteLine("Legacy account smoke:");
            Console.WriteLine($"endpoint={options.Address}:{options.Port}");
            Console.WriteLine(
                $"forward identifier={result.Forward.Identifier} type={result.Forward.Type} " +
                $"ip={result.Forward.IP} port={result.Forward.Port}");

            var failures = 0;
            if (options.ExpectedIp is not null &&
                !string.Equals(result.Forward.IP, options.ExpectedIp, StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine(
                    $"Expected forward ip {options.ExpectedIp} but received {result.Forward.IP}.");
                failures++;
            }

            if (options.ExpectedPort is not null && result.Forward.Port != options.ExpectedPort.Value)
            {
                Console.Error.WriteLine(
                    $"Expected forward port {options.ExpectedPort.Value} but received {result.Forward.Port}.");
                failures++;
            }

            return failures == 0 ? 0 : 2;
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or InvalidDataException)
        {
            Console.Error.WriteLine($"Legacy account smoke failed: {ex.Message}");
            return 2;
        }
    }

    static LegacyAccountSmokeOptions ResolveLegacyAccountSmokeOptions(string[] args)
    {
        if (args.Length < 6)
        {
            throw new ArgumentException(
                "Usage: WolfConquer.DataTool legacy-account-smoke <address> <port> <username> <password> <serverName> [--expect-ip ip] [--expect-port port] [--timeout-ms ms]");
        }

        if (!int.TryParse(args[2], out var port) || port is < 1 or > 65535)
        {
            throw new FormatException($"Invalid port: {args[2]}");
        }

        string? expectedIp = null;
        ushort? expectedPort = null;
        var timeoutMs = 5000;
        for (var index = 6; index < args.Length; index++)
        {
            var option = args[index];
            if (string.Equals(option, "--expect-ip", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
                {
                    throw new FormatException("--expect-ip requires a value.");
                }

                expectedIp = args[index];
            }
            else if (string.Equals(option, "--expect-port", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length ||
                    !ushort.TryParse(args[index], out var parsedExpectedPort) ||
                    parsedExpectedPort == 0)
                {
                    throw new FormatException("--expect-port requires a port between 1 and 65535.");
                }

                expectedPort = parsedExpectedPort;
            }
            else if (string.Equals(option, "--timeout-ms", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length || !int.TryParse(args[index], out timeoutMs) || timeoutMs <= 0)
                {
                    throw new FormatException("--timeout-ms requires a positive integer.");
                }
            }
            else
            {
                throw new ArgumentException($"Unknown legacy-account-smoke option: {option}");
            }
        }

        return new LegacyAccountSmokeOptions(
            args[1],
            port,
            args[3],
            args[4],
            args[5],
            expectedIp,
            expectedPort,
            TimeSpan.FromMilliseconds(timeoutMs));
    }

    static async Task<LegacyAccountSmokeResult> RunLegacyAccountSmokeAsync(LegacyAccountSmokeOptions options)
    {
        using var timeout = new CancellationTokenSource(options.Timeout);
        using var client = new TcpClient();
        await client.ConnectAsync(options.Address, options.Port, timeout.Token).ConfigureAwait(false);
        var stream = client.GetStream();

        var serverDecryptCipher = new LegacyAccServerCipher();
        var encryptedSeed = await ReadLegacyAccountSmokeBytesAsync(
            stream,
            LegacyAccountPasswordSeedPacket.FrameLength,
            timeout.Token).ConfigureAwait(false);
        serverDecryptCipher.DecryptFromServer(encryptedSeed);
        if (!LegacyAccountPasswordSeedPacket.TryParse(ReadFrame(encryptedSeed), out _))
        {
            throw new InvalidDataException("The server-first legacy password seed frame was malformed.");
        }

        var encryptedRequest = new LegacyAccountAuthenticationPacket(
            options.Username,
            options.Password,
            options.ServerName).ToFrameBytes();
        new LegacyAccServerCipher().EncryptForServer(encryptedRequest);
        await stream.WriteAsync(encryptedRequest, timeout.Token).ConfigureAwait(false);
        await stream.FlushAsync(timeout.Token).ConfigureAwait(false);

        var encryptedForward = await ReadLegacyAccountSmokeBytesAsync(
            stream,
            LegacyAccountForwardPacket.FrameLength,
            timeout.Token).ConfigureAwait(false);
        serverDecryptCipher.DecryptFromServer(encryptedForward);
        if (!LegacyAccountForwardPacket.TryParse(ReadFrame(encryptedForward), out var forward))
        {
            throw new InvalidDataException("The legacy account forward frame was malformed.");
        }

        return new LegacyAccountSmokeResult(forward);
    }

    static async Task<byte[]> ReadLegacyAccountSmokeBytesAsync(
        NetworkStream stream,
        int length,
        CancellationToken cancellationToken)
    {
        var bytes = new byte[length];
        await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        return bytes;
    }

    static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        if (!PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _))
        {
            throw new InvalidDataException("The received bytes do not contain a complete packet frame.");
        }

        return frame;
    }
}

internal sealed record LegacyAccountSmokeOptions(
    string Address,
    int Port,
    string Username,
    string Password,
    string ServerName,
    string? ExpectedIp,
    ushort? ExpectedPort,
    TimeSpan Timeout);

internal sealed record LegacyAccountSmokeResult(LegacyAccountForwardPacket Forward);

internal sealed record LegacyGuardShieldSmokeOptions(
    string Address,
    int Port,
    string Command,
    string ExpectedResponse,
    TimeSpan Timeout);

internal sealed record LegacyGuardShieldSmokeResult(string Response);
