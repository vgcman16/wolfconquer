using System.Net.Sockets;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Networking;

internal static partial class Program
{
    static int LocalLiveStackSmoke(string[] args)
    {
        LocalLiveStackSmokeOptions options;
        try
        {
            options = ResolveLocalLiveStackSmokeOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException or UriFormatException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        try
        {
            var result = RunLocalLiveStackSmokeAsync(options).GetAwaiter().GetResult();
            Console.WriteLine("Local live stack smoke:");
            Console.WriteLine($"loginweb={options.LoginWebBaseUri} ready={result.LoginWeb.Ready}");
            Console.WriteLine($"gateway={options.Address}:{options.GatewayPort} response={result.GuardShield.Response}");
            Console.WriteLine(
                $"account={options.Address}:{options.AccountPort} forward identifier={result.Account.Forward.Identifier} " +
                $"type={result.Account.Forward.Type} ip={result.Account.Forward.IP} port={result.Account.Forward.Port}");
            Console.WriteLine(
                $"world={result.Account.Forward.IP}:{result.Account.Forward.Port} " +
                $"accountKey={result.Account.Forward.Identifier} answerOk={result.World.AnswerOkSeen} status={result.World.Status}");
            Console.WriteLine($"observed-opcodes={FormatLegacyWorldSmokeOpcodes(result.World.ObservedOpcodes)}");
            return 0;
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or InvalidDataException)
        {
            Console.Error.WriteLine($"Local live stack smoke failed: {ex.Message}");
            return 2;
        }
    }

    static LocalLiveStackSmokeOptions ResolveLocalLiveStackSmokeOptions(string[] args)
    {
        if (args.Length < 7)
        {
            throw new ArgumentException(
                "Usage: WolfConquer.DataTool local-live-stack-smoke <address> <gatewayPort> <accountPort> <username> <password> <serverName> [--login-web-base-url url] [--expect-world-ip ip] [--expect-world-port port] [--timeout-ms ms]");
        }

        if (!int.TryParse(args[2], out var gatewayPort) || gatewayPort is < 1 or > 65535)
        {
            throw new FormatException($"Invalid gateway port: {args[2]}");
        }

        if (!int.TryParse(args[3], out var accountPort) || accountPort is < 1 or > 65535)
        {
            throw new FormatException($"Invalid account port: {args[3]}");
        }

        var loginWebBaseUri = LoginWebLoaderReadinessLogic.DefaultBaseUri;
        string? expectedWorldIp = null;
        ushort? expectedWorldPort = null;
        var timeoutMs = 5000;
        for (var index = 7; index < args.Length; index++)
        {
            var option = args[index];
            if (string.Equals(option, "--login-web-base-url", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
                {
                    throw new FormatException("--login-web-base-url requires a value.");
                }

                loginWebBaseUri = LoginWebLoaderReadinessLogic.NormalizeBaseUri(args[index]);
            }
            else if (string.Equals(option, "--expect-world-ip", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
                {
                    throw new FormatException("--expect-world-ip requires a value.");
                }

                expectedWorldIp = args[index];
            }
            else if (string.Equals(option, "--expect-world-port", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length ||
                    !ushort.TryParse(args[index], out var parsedExpectedWorldPort) ||
                    parsedExpectedWorldPort == 0)
                {
                    throw new FormatException("--expect-world-port requires a port between 1 and 65535.");
                }

                expectedWorldPort = parsedExpectedWorldPort;
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
                throw new ArgumentException($"Unknown local-live-stack-smoke option: {option}");
            }
        }

        return new LocalLiveStackSmokeOptions(
            args[1],
            gatewayPort,
            accountPort,
            args[4],
            args[5],
            args[6],
            loginWebBaseUri,
            expectedWorldIp,
            expectedWorldPort,
            TimeSpan.FromMilliseconds(timeoutMs));
    }

    static async Task<LocalLiveStackSmokeResult> RunLocalLiveStackSmokeAsync(LocalLiveStackSmokeOptions options)
    {
        var loginWeb = await LoginWebLoaderReadinessLogic.CheckAsync(
                LoginWebLoaderReadinessOptions.FromEnvironment(options.LoginWebBaseUri, options.Timeout))
            .ConfigureAwait(false);
        if (!loginWeb.Ready)
        {
            throw new InvalidDataException(
                "LoginWeb loader readiness failed: " + LoginWebLoaderReadinessLogic.FormatReport(loginWeb).Trim());
        }

        var expectedGuardShieldResponse = LegacyGuardShieldTextProtocolOptions.FromEnvironment().Hashes.ToLegacyResponse();
        var guardShield = await RunLegacyGuardShieldSmokeAsync(
                new LegacyGuardShieldSmokeOptions(
                    options.Address,
                    options.GatewayPort,
                    "Welcome:Hash:HD123:HWID456",
                    expectedGuardShieldResponse,
                    options.Timeout))
            .ConfigureAwait(false);
        if (!string.Equals(guardShield.Response, expectedGuardShieldResponse, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"GuardShield response mismatch; expected {expectedGuardShieldResponse} but received {guardShield.Response}.");
        }

        var account = await RunLegacyAccountSmokeAsync(
                new LegacyAccountSmokeOptions(
                    options.Address,
                    options.AccountPort,
                    options.Username,
                    options.Password,
                    options.ServerName,
                    options.ExpectedWorldIp,
                    options.ExpectedWorldPort,
                    options.Timeout))
            .ConfigureAwait(false);
        if (account.Forward.Type != LegacyAccountForwardType.Ready)
        {
            throw new InvalidDataException(
                $"Account smoke did not return Ready; received {account.Forward.Type}.");
        }

        if (options.ExpectedWorldIp is not null &&
            !string.Equals(account.Forward.IP, options.ExpectedWorldIp, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Expected account forward ip {options.ExpectedWorldIp} but received {account.Forward.IP}.");
        }

        if (options.ExpectedWorldPort is not null && account.Forward.Port != options.ExpectedWorldPort.Value)
        {
            throw new InvalidDataException(
                $"Expected account forward port {options.ExpectedWorldPort.Value} but received {account.Forward.Port}.");
        }

        var world = await RunLegacyWorldSmokeAsync(
                new LegacyWorldSmokeOptions(
                    account.Forward.IP,
                    account.Forward.Port,
                    account.Forward.Identifier,
                    options.Timeout))
            .ConfigureAwait(false);
        if (!world.AnswerOkSeen)
        {
            throw new InvalidDataException(
                $"World smoke did not see answer-ok; status={world.Status}.");
        }

        return new LocalLiveStackSmokeResult(loginWeb, guardShield, account, world);
    }
}

internal sealed record LocalLiveStackSmokeOptions(
    string Address,
    int GatewayPort,
    int AccountPort,
    string Username,
    string Password,
    string ServerName,
    Uri LoginWebBaseUri,
    string? ExpectedWorldIp,
    ushort? ExpectedWorldPort,
    TimeSpan Timeout);

internal sealed record LocalLiveStackSmokeResult(
    LoginWebLoaderReadinessReport LoginWeb,
    LegacyGuardShieldSmokeResult GuardShield,
    LegacyAccountSmokeResult Account,
    LegacyWorldSmokeResult World);
