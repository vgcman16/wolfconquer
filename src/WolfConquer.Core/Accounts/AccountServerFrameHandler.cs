using System.Buffers;
using System.Globalization;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Accounts;

public sealed partial class AccountServerFrameHandler
{
    private readonly AccountHandoffService? _handoffService;
    private readonly uint? _legacyCaptureOverrideIdentifier;
    private readonly IReadOnlyDictionary<string, string> _legacySavedPasswordRebinds;
    private readonly bool _disconnectLegacyAccountAfterReady;
    private readonly AccountLoginService? _loginService;
    private readonly TextWriter _log;
    private readonly ConquerEndpoint _worldEndpoint;

    public AccountServerFrameHandler(AccountLoginService? loginService, TextWriter? log = null)
        : this(loginService, handoffService: null, log)
    {
    }

    public AccountServerFrameHandler(
        AccountLoginService? loginService,
        AccountHandoffService? handoffService,
        TextWriter? log = null,
        ConquerEndpoint? worldEndpoint = null,
        uint? legacyCaptureOverrideIdentifier = null,
        AccountLoginRequest? legacyMalformedAuthFallbackRequest = null,
        IReadOnlyDictionary<string, string>? legacySavedPasswordRebinds = null,
        bool disconnectLegacyAccountAfterReady = false)
    {
        _loginService = loginService;
        _handoffService = handoffService;
        _legacyCaptureOverrideIdentifier = legacyCaptureOverrideIdentifier;
        // Retain source compatibility, but malformed packets never use saved credentials.
        _ = legacyMalformedAuthFallbackRequest;
        _legacySavedPasswordRebinds = legacySavedPasswordRebinds ??
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _disconnectLegacyAccountAfterReady = disconnectLegacyAccountAfterReady;
        _log = log ?? TextWriter.Null;
        _worldEndpoint = worldEndpoint ?? ConquerRuntimeDefaults.WorldServer;
    }

    public static AccountServerFrameHandler FromConnectionString(
        string? connectionString,
        string? accountLogPaths,
        string? accountBanUidPath,
        TextWriter? log = null,
        AccountHandoffService? handoffService = null,
        ConquerEndpoint? worldEndpoint = null,
        uint? legacyCaptureOverrideIdentifier = null,
        AccountLoginRequest? legacyMalformedAuthFallbackRequest = null,
        IReadOnlyDictionary<string, string>? legacySavedPasswordRebinds = null,
        bool disconnectLegacyAccountAfterReady = false)
    {
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return new AccountServerFrameHandler(
                new AccountLoginService(new MySqlAccountRepository(connectionString)),
                handoffService,
                log,
                worldEndpoint,
                legacyCaptureOverrideIdentifier,
                legacyMalformedAuthFallbackRequest,
                legacySavedPasswordRebinds,
                disconnectLegacyAccountAfterReady);
        }

        var logPathList = SplitAccountLogPaths(accountLogPaths);
        if (logPathList.Count > 0)
        {
            log?.WriteLine($"Account repository: using legacy AccServer logs ({logPathList.Count} files).");
            return new AccountServerFrameHandler(
                new AccountLoginService(LegacyAccServerLogAccountRepository.FromLogFiles(
                    logPathList,
                    accountBanUidPath)),
                handoffService,
                log,
                worldEndpoint,
                legacyCaptureOverrideIdentifier,
                legacyMalformedAuthFallbackRequest,
                legacySavedPasswordRebinds,
                disconnectLegacyAccountAfterReady);
        }

        return new AccountServerFrameHandler(
            null,
            handoffService,
            log,
            worldEndpoint,
            legacyCaptureOverrideIdentifier,
            legacyMalformedAuthFallbackRequest,
            legacySavedPasswordRebinds,
            disconnectLegacyAccountAfterReady);
    }

    public static AccountServerFrameHandler FromConnectionString(
        string? connectionString,
        TextWriter? log = null,
        AccountHandoffService? handoffService = null,
        ConquerEndpoint? worldEndpoint = null,
        uint? legacyCaptureOverrideIdentifier = null,
        AccountLoginRequest? legacyMalformedAuthFallbackRequest = null,
        IReadOnlyDictionary<string, string>? legacySavedPasswordRebinds = null,
        bool disconnectLegacyAccountAfterReady = false)
    {
        return FromConnectionString(
            connectionString,
            accountLogPaths: null,
            accountBanUidPath: null,
            log,
            handoffService,
            worldEndpoint,
            legacyCaptureOverrideIdentifier,
            legacyMalformedAuthFallbackRequest,
            legacySavedPasswordRebinds,
            disconnectLegacyAccountAfterReady);
    }

    private static IReadOnlyList<string> SplitAccountLogPaths(string? accountLogPaths)
    {
        if (string.IsNullOrWhiteSpace(accountLogPaths))
        {
            return [];
        }

        return accountLogPaths
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(File.Exists)
            .ToArray();
    }

    public async ValueTask HandleFrameAsync(
        ConquerTcpConnection connection,
        PacketFrame frame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        try
        {
            await HandleFrameCoreAsync(connection, frame, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            await LogFrameExceptionAsync(connection, frame, ex).ConfigureAwait(false);
        }
    }

    private async ValueTask HandleFrameCoreAsync(
        ConquerTcpConnection connection,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        if (frame.Header.Opcode == AccountPacketOpcodes.InternalLoginRequest)
        {
            await HandleInternalLoginAsync(connection, frame, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (frame.Header.Opcode == AccountPacketOpcodes.LegacyAuthentication)
        {
            await HandleLegacyAuthenticationAsync(connection, frame, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (LegacyAccountResourceProbePacket.TryParse(frame, out _))
        {
            return;
        }

        await _log.WriteLineAsync(
                LegacyParityGapDiagnostics.UnsupportedPacket(
                    frame.Header.Opcode,
                    packet: "Account",
                    reason: "unknown account opcode is not modeled")
                .FormatLogMessage())
            .ConfigureAwait(false);
        await _log.WriteLineAsync(
                "PARITY-GAP unknown account opcode " +
                $"{FormatFrameDiagnostic(frame)} from {connection.RemoteEndPoint}")
            .ConfigureAwait(false);
    }

    private async ValueTask LogFrameExceptionAsync(
        ConquerTcpConnection connection,
        PacketFrame frame,
        Exception exception)
    {
        await _log.WriteLineAsync(
                LegacyParityGapDiagnostics.RuntimeException(
                    "account-frame-dispatch",
                    frame.Header.Opcode,
                    (int)frame.Payload.Length,
                    exception.GetType().Name,
                    sourceSeam: "AccountServerFrameHandler.Dispatch",
                    nextSeam: "inspect-runtime-exception",
                    reason: "account runtime exception was caught without terminating the live server; inspect the error log for parity triage")
                .FormatLogMessage())
            .ConfigureAwait(false);
        await _log.WriteLineAsync(
                "PARITY-BUG account runtime exception " +
                $"{FormatFrameDiagnostic(frame)} from {connection.RemoteEndPoint} " +
                $"exception={exception.GetType().Name}: {exception.Message}")
            .ConfigureAwait(false);
        Console.Error.WriteLine(
            "PARITY-BUG account runtime exception " +
            $"{DateTimeOffset.UtcNow:O} {FormatFrameDiagnostic(frame)} from {connection.RemoteEndPoint}" +
            $"{Environment.NewLine}{exception}");
    }

    private async ValueTask HandleInternalLoginAsync(
        ConquerTcpConnection connection,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        if (_loginService is null)
        {
            await SendResponseAsync(
                    connection,
                    AccountLoginWireStatus.ServiceUnavailable,
                    accountId: 0,
                    ticket: null,
                    cancellationToken)
                .ConfigureAwait(false);
            await _log.WriteLineAsync("AccountServer login request received but no account repository is configured.")
                .ConfigureAwait(false);
            return;
        }

        if (!InternalAccountLoginPacket.TryParse(frame, out var request))
        {
            await SendResponseAsync(
                    connection,
                    AccountLoginWireStatus.InvalidPacket,
                    accountId: 0,
                    ticket: null,
                    cancellationToken)
                .ConfigureAwait(false);
            await _log.WriteLineAsync("AccountServer rejected malformed internal login packet.").ConfigureAwait(false);
            return;
        }

        var result = await _loginService.ValidateAsync(
            new AccountLoginRequest(request.Username, request.Password),
            cancellationToken).ConfigureAwait(false);
        var status = MapStatus(result.Status);
        var account = result.Account;
        var accountId = result.IsSuccess ? account?.Id ?? 0 : 0;
        var characterId = result.IsSuccess && account is not null ? GetHandoffCharacterId(account) : 0;
        AccountHandoffTicket? ticket = null;
        if (status == AccountLoginWireStatus.Success &&
            characterId > 0 &&
            _handoffService is not null)
        {
            ticket = _handoffService.Issue(characterId, request.Username, _worldEndpoint);
        }

        await SendResponseAsync(connection, status, accountId, ticket, cancellationToken).ConfigureAwait(false);
        await _log.WriteLineAsync($"AccountServer login {status} for {request.Username}.").ConfigureAwait(false);
    }

    private async ValueTask HandleLegacyAuthenticationAsync(
        ConquerTcpConnection connection,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        if (await RejectIncoherentLegacyAuthenticationEnvelopeAsync(connection, frame, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        if (!LegacyAccountAuthenticationPacket.TryParse(frame, out var request))
        {
            await SendLegacyForwardAsync(
                    connection,
                    LegacyAccountForwardType.InvalidAuthenticationProtocol,
                    identifier: 0,
                    worldEndpoint: null,
                    cancellationToken)
                .ConfigureAwait(false);
            await LogMalformedLegacyAuthenticationAsync(frame).ConfigureAwait(false);
            return;
        }

        if (_legacyCaptureOverrideIdentifier is uint captureIdentifier)
        {
            await SendLegacyForwardAsync(
                    connection,
                    LegacyAccountForwardType.Ready,
                    captureIdentifier,
                    _worldEndpoint,
                    cancellationToken)
                .ConfigureAwait(false);
            await _log.WriteLineAsync(
                    $"AccountServer legacy capture override Ready for {request.Username}.")
                .ConfigureAwait(false);
            return;
        }

        if (_loginService is null)
        {
            await SendLegacyForwardAsync(
                    connection,
                    LegacyAccountForwardType.ServersNotConfigured,
                    identifier: 0,
                    worldEndpoint: null,
                    cancellationToken)
                .ConfigureAwait(false);
            await _log.WriteLineAsync("AccountServer legacy login request received but no account repository is configured.")
                .ConfigureAwait(false);
            return;
        }

        var validation = await ValidateLegacyPasswordCandidatesAsync(request, cancellationToken).ConfigureAwait(false);
        var result = validation.Result;
        if (result.Status == AccountLoginStatus.WrongPassword &&
            TryResolveLegacySavedPasswordRebind(request.Username, result.PasswordDiagnostic, out var submittedPasswordOwner))
        {
            var reboundResult = await _loginService.ValidateLegacySavedPasswordOwnerRebindAsync(
                    new AccountLoginRequest(request.Username, validation.Password),
                    submittedPasswordOwner,
                    cancellationToken)
                .ConfigureAwait(false);
            if (reboundResult.Status != AccountLoginStatus.WrongPassword)
            {
                result = reboundResult;
            }
        }

        var forwardType = MapLegacyForwardType(result.Status);
        var legacyLoginReason = result.Status.ToString();
        var identifier = 0U;
        var worldEndpoint = forwardType == LegacyAccountForwardType.Ready ? _worldEndpoint : null;
        if (forwardType == LegacyAccountForwardType.Ready)
        {
            var account = result.Account;
            if (account is null ||
                account.Id <= 0 ||
                !TryGetLegacyForwardIdentifier(account, out identifier))
            {
                forwardType = LegacyAccountForwardType.InvalidInfo;
                worldEndpoint = null;
                legacyLoginReason = "InvalidForwardIdentifier";
            }
            else if (_handoffService is not null)
            {
                _handoffService.Issue(GetHandoffCharacterId(account), request.Username, _worldEndpoint);
            }
        }

        await SendLegacyForwardAsync(connection, forwardType, identifier, worldEndpoint, cancellationToken)
            .ConfigureAwait(false);
        if (result.Status == AccountLoginStatus.WrongPassword &&
            result.PasswordDiagnostic is { } passwordDiagnostic)
        {
            var exactPasswordOwnerDiagnostic = passwordDiagnostic.ExactPasswordOwnerCount > 0
                ? $"exactPasswordOwnerCount={passwordDiagnostic.ExactPasswordOwnerCount.ToString(CultureInfo.InvariantCulture)} " +
                    $"firstExactPasswordOwnerId={passwordDiagnostic.FirstExactPasswordOwnerId?.ToString(CultureInfo.InvariantCulture) ?? "none"} " +
                    $"firstExactPasswordOwnerUsername={FormatLogValue(passwordDiagnostic.FirstExactPasswordOwnerUsername ?? "none")} "
                : string.Empty;
            await _log.WriteLineAsync(
                    "AccountServer legacy login password diagnostic " +
                    $"requestLength={passwordDiagnostic.RequestLength.ToString(CultureInfo.InvariantCulture)} " +
                    $"candidateCount={passwordDiagnostic.CandidateCount.ToString(CultureInfo.InvariantCulture)} " +
                    $"bestStoredLength={passwordDiagnostic.BestStoredLength.ToString(CultureInfo.InvariantCulture)} " +
                    $"bestCommonPrefixLength={passwordDiagnostic.BestCommonPrefixLength.ToString(CultureInfo.InvariantCulture)} " +
                    $"bestLengthDelta={passwordDiagnostic.BestLengthDelta.ToString(CultureInfo.InvariantCulture)} " +
                    $"tailShape={passwordDiagnostic.TailShape} " +
                    exactPasswordOwnerDiagnostic +
                    $"for {request.Username}.")
                .ConfigureAwait(false);
        }

        if (result.PasswordMatchKind == AccountPasswordMatchKind.LegacyTrailingNoise &&
            result.Account is not null)
        {
            await _log.WriteLineAsync(
                    "AccountServer legacy login password normalized " +
                    $"match={result.PasswordMatchKind} " +
                    $"requestLength={validation.Password.Length.ToString(CultureInfo.InvariantCulture)} " +
                    $"storedLength={result.Account.Password.Length.ToString(CultureInfo.InvariantCulture)} " +
                    $"trailingLength={(validation.Password.Length - result.Account.Password.Length).ToString(CultureInfo.InvariantCulture)} " +
                    $"for {request.Username}.")
                .ConfigureAwait(false);
        }

        if (result.PasswordMatchKind == AccountPasswordMatchKind.LegacySavedPasswordOwnerRebind &&
            result.Account is not null &&
            _legacySavedPasswordRebinds.TryGetValue(request.Username, out var reboundPasswordOwner))
        {
            await _log.WriteLineAsync(
                    "AccountServer legacy login password rebound " +
                    $"match={result.PasswordMatchKind} " +
                    $"submittedPasswordOwnerUsername={FormatLogValue(reboundPasswordOwner)} " +
                    $"targetUsername={FormatLogValue(request.Username)} " +
                    $"targetAccountId={result.Account.Id.ToString(CultureInfo.InvariantCulture)}.")
                .ConfigureAwait(false);
        }

        if (validation.UsedFallback &&
            result.Status != AccountLoginStatus.WrongPassword)
        {
            await _log.WriteLineAsync(
                    "AccountServer legacy login password decoded fallback=RawEncryptedNull " +
                    $"sourceLength={request.Password.Length.ToString(CultureInfo.InvariantCulture)} " +
                    $"fallbackLength={validation.Password.Length.ToString(CultureInfo.InvariantCulture)} " +
                    $"for {request.Username}.")
                .ConfigureAwait(false);
        }

        await _log.WriteLineAsync(
                $"AccountServer legacy login {forwardType} reason={legacyLoginReason} for {request.Username}.")
            .ConfigureAwait(false);
    }


    private async ValueTask<LegacyPasswordValidation> ValidateLegacyPasswordCandidatesAsync(
        LegacyAccountAuthenticationPacket request,
        CancellationToken cancellationToken)
    {
        if (_loginService is null)
        {
            throw new InvalidOperationException("Account login service is required for legacy password validation.");
        }

        AccountLoginResult? bestWrongPasswordResult = null;
        string? bestWrongPasswordCandidate = null;
        foreach (var password in request.PasswordCandidates.Count == 0 ? [request.Password] : request.PasswordCandidates)
        {
            var result = await _loginService.ValidateLegacyAsync(
                new AccountLoginRequest(request.Username, password),
                cancellationToken).ConfigureAwait(false);
            if (result.Status != AccountLoginStatus.WrongPassword)
            {
                return new LegacyPasswordValidation(
                    result,
                    password,
                    !string.Equals(password, request.Password, StringComparison.Ordinal));
            }

            if (bestWrongPasswordResult is null ||
                (result.PasswordDiagnostic?.BestCommonPrefixLength ?? 0) >
                (bestWrongPasswordResult.PasswordDiagnostic?.BestCommonPrefixLength ?? 0))
            {
                bestWrongPasswordResult = result;
                bestWrongPasswordCandidate = password;
            }
        }

        return new LegacyPasswordValidation(
            bestWrongPasswordResult ?? new AccountLoginResult(AccountLoginStatus.WrongPassword),
            bestWrongPasswordCandidate ?? request.Password,
            bestWrongPasswordCandidate is not null &&
                !string.Equals(bestWrongPasswordCandidate, request.Password, StringComparison.Ordinal));
    }

    private static ValueTask SendResponseAsync(
        ConquerTcpConnection connection,
        AccountLoginWireStatus status,
        long accountId,
        AccountHandoffTicket? ticket,
        CancellationToken cancellationToken)
    {
        var frame = ticket is null
            ? AccountLoginResponsePacket.Build(status, accountId)
            : AccountLoginResponsePacket.Build(status, accountId, ticket.Token, FormatEndpoint(ticket.WorldEndpoint));
        return connection.SendAsync(frame, cancellationToken);
    }

    private async ValueTask SendLegacyForwardAsync(
        ConquerTcpConnection connection,
        LegacyAccountForwardType type,
        uint identifier,
        ConquerEndpoint? worldEndpoint,
        CancellationToken cancellationToken)
    {
        var frame = new LegacyAccountForwardPacket(
            identifier,
            type,
            worldEndpoint is null ? (ushort)0 : checked((ushort)worldEndpoint.Port),
            worldEndpoint?.Address ?? string.Empty).ToFrameBytes();
        await connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        if (_disconnectLegacyAccountAfterReady &&
            type == LegacyAccountForwardType.Ready)
        {
            await connection.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static string FormatEndpoint(ConquerEndpoint endpoint)
    {
        return endpoint.Address + ":" + endpoint.Port;
    }

    private static string FormatLogValue(string value)
    {
        return value.Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private static string FormatFrameDiagnostic(PacketFrame frame)
    {
        const int sampleBytes = 32;
        var sampleLength = (int)Math.Min(frame.Payload.Length, sampleBytes);
        var sample = sampleLength == 0
            ? string.Empty
            : Convert.ToHexString(frame.Payload.Slice(0, sampleLength).ToArray());
        var truncated = frame.Payload.Length > sampleLength ? "..." : string.Empty;

        return "opcode=" + frame.Header.Opcode +
            " payloadLength=" + frame.Payload.Length.ToString(CultureInfo.InvariantCulture) +
            " payloadSampleHex=" + sample + truncated;
    }

    private static AccountLoginWireStatus MapStatus(AccountLoginStatus status)
    {
        return status switch
        {
            AccountLoginStatus.Success => AccountLoginWireStatus.Success,
            AccountLoginStatus.MissingUsername => AccountLoginWireStatus.MissingUsername,
            AccountLoginStatus.WrongPassword => AccountLoginWireStatus.WrongPassword,
            AccountLoginStatus.Banned => AccountLoginWireStatus.Banned,
            AccountLoginStatus.AlreadyOnline => AccountLoginWireStatus.AlreadyOnline,
            AccountLoginStatus.AmbiguousAccount => AccountLoginWireStatus.AmbiguousAccount,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown account login status."),
        };
    }

    private static LegacyAccountForwardType MapLegacyForwardType(AccountLoginStatus status)
    {
        return status switch
        {
            AccountLoginStatus.Success => LegacyAccountForwardType.Ready,
            AccountLoginStatus.MissingUsername => LegacyAccountForwardType.WrongAccount,
            AccountLoginStatus.WrongPassword => LegacyAccountForwardType.InvalidInfo,
            AccountLoginStatus.Banned => LegacyAccountForwardType.Banned,
            AccountLoginStatus.AlreadyOnline => LegacyAccountForwardType.InvalidInfo,
            AccountLoginStatus.AmbiguousAccount => LegacyAccountForwardType.InvalidInfo,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown account login status."),
        };
    }

    private async ValueTask LogMalformedLegacyAuthenticationAsync(PacketFrame frame)
    {
        await _log.WriteLineAsync(
                "AccountServer rejected malformed legacy authentication packet. " +
                FormatMalformedLegacyAuthenticationDiagnostic(frame))
            .ConfigureAwait(false);
    }

    private static string FormatMalformedLegacyAuthenticationDiagnostic(PacketFrame frame)
    {
        var payload = frame.Payload.ToArray();
        var sampleLength = Math.Min(payload.Length, 96);
        var payloadSample = sampleLength == 0
            ? string.Empty
            : Convert.ToHexString(payload.AsSpan(0, sampleLength));
        var legacyLengths = payload.Length >= 3
            ? $" legacyLens={payload[0]}/{payload[1]}/{payload[2]}"
            : " legacyLens=<short>";

        return $"frameLength={frame.Header.Length} opcode={frame.Header.Opcode} " +
            $"payloadLength={payload.Length}{legacyLengths} payloadHex={payloadSample}";
    }

    private static bool TryGetLegacyForwardIdentifier(AccountRecord account, out uint identifier)
    {
        var value = GetHandoffCharacterId(account);
        if (value <= 0 || value > uint.MaxValue)
        {
            identifier = 0;
            return false;
        }

        identifier = (uint)value;
        return true;
    }

    private static long GetHandoffCharacterId(AccountRecord account)
    {
        return account.Id > 0 ? account.Id : account.EntityId;
    }

    private bool TryResolveLegacySavedPasswordRebind(
        string targetUsername,
        AccountLoginPasswordDiagnostic? passwordDiagnostic,
        out string submittedPasswordOwner)
    {
        submittedPasswordOwner = string.Empty;
        if (passwordDiagnostic is null ||
            !_legacySavedPasswordRebinds.TryGetValue(targetUsername, out var configuredOwner) ||
            string.IsNullOrWhiteSpace(configuredOwner))
        {
            return false;
        }

        if (!string.Equals(
                passwordDiagnostic.FirstExactPasswordOwnerUsername,
                configuredOwner,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        submittedPasswordOwner = configuredOwner;
        return true;
    }

    private readonly record struct LegacyPasswordValidation(
        AccountLoginResult Result,
        string Password,
        bool UsedFallback);
}
