using WolfConquer.Core.Accounts;
using WolfConquer.Core.Runtime;

var endpoint = ConquerRuntimeDefaults.AccountServer;
var protocolProfile = ConquerProtocolProfileConfiguration.FromEnvironment();
RuntimeConsoleLog.ConfigureFromEnvironment(endpoint.Role);
Console.Out.WriteLine($"Protocol profile: {protocolProfile}");
var startupOptions = AccountServerStartupOptions.Parse(args);
var connectionString = Environment.GetEnvironmentVariable("WOLFCONQUER_ACCOUNT_MYSQL");
var accountLogPaths = Environment.GetEnvironmentVariable("WOLFCONQUER_ACCOUNT_LOG_PATHS");
var accountBanUidPath = Environment.GetEnvironmentVariable("WOLFCONQUER_ACCOUNT_BAN_UID_PATH");
var legacyCaptureOverrideIdentifier = ResolveLegacyCaptureOverrideIdentifier();
var legacyMalformedAuthFallbackRequest = ResolveLegacyMalformedAuthFallbackRequest();
var legacySavedPasswordRebinds = ResolveLegacySavedPasswordRebinds();
var disconnectLegacyAccountAfterReady = IsTruthy(
    Environment.GetEnvironmentVariable("WOLFCONQUER_DISCONNECT_LEGACY_ACCOUNT_AFTER_READY"));
var handoffService = await AccountHandoffStartup.TryCreateServiceAsync(Console.Out);
var handler = AccountServerFrameHandler.FromConnectionString(
    connectionString,
    accountLogPaths,
    accountBanUidPath,
    Console.Out,
    handoffService,
    worldEndpoint: startupOptions.WorldEndpoint,
    legacyCaptureOverrideIdentifier: legacyCaptureOverrideIdentifier,
    legacyMalformedAuthFallbackRequest: legacyMalformedAuthFallbackRequest,
    legacySavedPasswordRebinds: legacySavedPasswordRebinds,
    disconnectLegacyAccountAfterReady: disconnectLegacyAccountAfterReady);

if (startupOptions.WorldEndpoint is not null)
{
    Console.WriteLine(
        $"World forward endpoint: {startupOptions.WorldEndpoint.Address}:{startupOptions.WorldEndpoint.Port}");
}

if (legacyCaptureOverrideIdentifier is uint captureIdentifier)
{
    Console.WriteLine($"Legacy account capture override: Ready identifier {captureIdentifier}");
}

if (legacyMalformedAuthFallbackRequest is not null)
{
    Console.WriteLine(
        $"Legacy malformed auth fallback: {legacyMalformedAuthFallbackRequest.Username}");
}

if (legacySavedPasswordRebinds.Count > 0)
{
    Console.WriteLine($"Legacy saved-password rebinds configured: {legacySavedPasswordRebinds.Count}");
}

if (disconnectLegacyAccountAfterReady)
{
    Console.WriteLine("Legacy account Ready disconnect compatibility enabled.");
}

return await ConquerServerRunner.RunAsync(endpoint, startupOptions.ServerArgs, handler.HandleFrameAsync);

static uint? ResolveLegacyCaptureOverrideIdentifier()
{
    var raw = Environment.GetEnvironmentVariable("WOLFCONQUER_LEGACY_ACCOUNT_CAPTURE_IDENTIFIER");
    if (string.IsNullOrWhiteSpace(raw))
    {
        return null;
    }

    if (!IsTruthy(Environment.GetEnvironmentVariable("WOLFCONQUER_ALLOW_LEGACY_ACCOUNT_CAPTURE_OVERRIDE")))
    {
        throw new InvalidOperationException(
            "WOLFCONQUER_LEGACY_ACCOUNT_CAPTURE_IDENTIFIER requires explicit opt-in with " +
            "WOLFCONQUER_ALLOW_LEGACY_ACCOUNT_CAPTURE_OVERRIDE=1, true, or yes.");
    }

    if (uint.TryParse(raw, out var identifier) && identifier > 0)
    {
        return identifier;
    }

    throw new InvalidOperationException(
        "WOLFCONQUER_LEGACY_ACCOUNT_CAPTURE_IDENTIFIER must be a positive UInt32 value.");
}

static bool IsTruthy(string? value)
{
    return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
}

static AccountLoginRequest? ResolveLegacyMalformedAuthFallbackRequest()
{
    var username = Environment.GetEnvironmentVariable("WOLFCONQUER_LEGACY_MALFORMED_AUTH_FALLBACK_USERNAME");
    var password = Environment.GetEnvironmentVariable("WOLFCONQUER_LEGACY_MALFORMED_AUTH_FALLBACK_PASSWORD");
    if (string.IsNullOrWhiteSpace(username) && string.IsNullOrEmpty(password))
    {
        return null;
    }

    if (!IsTruthy(Environment.GetEnvironmentVariable("WOLFCONQUER_ALLOW_LEGACY_MALFORMED_AUTH_FALLBACK")))
    {
        throw new InvalidOperationException(
            "WOLFCONQUER_LEGACY_MALFORMED_AUTH_FALLBACK_USERNAME/PASSWORD requires explicit opt-in with " +
            "WOLFCONQUER_ALLOW_LEGACY_MALFORMED_AUTH_FALLBACK=1, true, or yes.");
    }

    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
    {
        throw new InvalidOperationException(
            "WOLFCONQUER_LEGACY_MALFORMED_AUTH_FALLBACK_USERNAME and " +
            "WOLFCONQUER_LEGACY_MALFORMED_AUTH_FALLBACK_PASSWORD must both be set.");
    }

    return new AccountLoginRequest(username.Trim(), password);
}

static IReadOnlyDictionary<string, string> ResolveLegacySavedPasswordRebinds()
{
    var raw = Environment.GetEnvironmentVariable("WOLFCONQUER_LEGACY_SAVED_PASSWORD_REBINDS");
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (string.IsNullOrWhiteSpace(raw))
    {
        return map;
    }

    if (!IsTruthy(Environment.GetEnvironmentVariable("WOLFCONQUER_ALLOW_LEGACY_SAVED_PASSWORD_REBINDS")))
    {
        throw new InvalidOperationException(
            "WOLFCONQUER_LEGACY_SAVED_PASSWORD_REBINDS requires explicit opt-in with " +
            "WOLFCONQUER_ALLOW_LEGACY_SAVED_PASSWORD_REBINDS=1, true, or yes.");
    }

    var entries = raw.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    foreach (var entry in entries)
    {
        var separator = entry.IndexOf('=');
        if (separator <= 0 || separator == entry.Length - 1)
        {
            throw new InvalidOperationException(
                "WOLFCONQUER_LEGACY_SAVED_PASSWORD_REBINDS entries must use target=owner format.");
        }

        var target = entry[..separator].Trim();
        var owner = entry[(separator + 1)..].Trim();
        if (target.Length == 0 || owner.Length == 0)
        {
            throw new InvalidOperationException(
                "WOLFCONQUER_LEGACY_SAVED_PASSWORD_REBINDS target and owner usernames cannot be empty.");
        }

        map[target] = owner;
    }

    return map;
}
