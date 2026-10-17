using System.Buffers;
using System.IO.Pipelines;
using System.Text;

namespace WolfConquer.Core.Networking;

public static class LegacyGuardShieldTextProtocol
{
    private static readonly Encoding WireEncoding = Encoding.ASCII;

    public static LegacyGuardShieldTextCommandResult HandleCommand(
        string command,
        LegacyGuardShieldTextProtocolOptions? options = null)
    {
        options ??= LegacyGuardShieldTextProtocolOptions.Default;
        var normalized = command.TrimEnd('\0', '\r', '\n');
        if (normalized.Length == 0)
        {
            return LegacyGuardShieldTextCommandResult.Ignored;
        }

        var fields = normalized.Split(':');
        var commandName = fields[0].Trim();
        if (IsPartialCommand(commandName, "Welcome") || IsPartialCommand(commandName, "Used"))
        {
            return ReportCommand(
                options,
                commandName,
                fields.Length,
                LegacyGuardShieldTextCommandResult.Incomplete);
        }

        if (string.Equals(commandName, "Welcome", StringComparison.Ordinal))
        {
            return ReportCommand(options, commandName, fields.Length, HandleWelcome(fields, options));
        }

        if (string.Equals(commandName, "Used", StringComparison.Ordinal))
        {
            return ReportCommand(options, commandName, fields.Length, HandleUsed(fields, options));
        }

        return ReportCommand(
            options,
            commandName,
            fields.Length,
            LegacyGuardShieldTextCommandResult.Ignored);
    }

    public static async ValueTask ReadCommandsAsync(
        PipeReader reader,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> writeAsync,
        LegacyGuardShieldTextProtocolOptions? options,
        int maxCommandSize,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(writeAsync);

        options ??= LegacyGuardShieldTextProtocolOptions.Default;
        var pending = new List<byte>();

        while (true)
        {
            var result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            var buffer = result.Buffer;

            try
            {
                if (buffer.Length > 0)
                {
                    if (pending.Count + buffer.Length > maxCommandSize)
                    {
                        throw new InvalidDataException(
                            $"Legacy GuardShield text command exceeded {maxCommandSize} bytes.");
                    }

                    foreach (var segment in buffer)
                    {
                        pending.AddRange(segment.ToArray());
                    }
                }

                await FlushPendingCommandsAsync(
                    pending,
                    writeAsync,
                    options,
                    force: result.IsCompleted,
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                reader.AdvanceTo(buffer.End);
            }

            if (result.IsCompleted)
            {
                break;
            }
        }
    }

    private static LegacyGuardShieldTextCommandResult HandleWelcome(
        IReadOnlyList<string> fields,
        LegacyGuardShieldTextProtocolOptions options)
    {
        if (fields.Count == 1)
        {
            return LegacyGuardShieldTextCommandResult.Incomplete;
        }

        if (fields[1].Length == 0 || IsPartialCommand(fields[1], "Hash"))
        {
            return LegacyGuardShieldTextCommandResult.Incomplete;
        }

        if (!string.Equals(fields[1], "Hash", StringComparison.Ordinal))
        {
            return LegacyGuardShieldTextCommandResult.Ignored;
        }

        if (fields.Count < 4)
        {
            return LegacyGuardShieldTextCommandResult.Incomplete;
        }

        return LegacyGuardShieldTextCommandResult.Handled(options.Hashes.ToLegacyResponse());
    }

    private static LegacyGuardShieldTextCommandResult HandleUsed(
        IReadOnlyList<string> fields,
        LegacyGuardShieldTextProtocolOptions options)
    {
        if (fields.Count == 1)
        {
            return LegacyGuardShieldTextCommandResult.Incomplete;
        }

        var kind = fields[1].Trim();
        if (kind.Length == 0 ||
            IsPartialCommand(kind, "Clicker") ||
            IsPartialCommand(kind, "Program") ||
            IsPartialCommand(kind, "Speed"))
        {
            return LegacyGuardShieldTextCommandResult.Incomplete;
        }

        if (!IsUsageKind(kind))
        {
            return LegacyGuardShieldTextCommandResult.Ignored;
        }

        if (fields.Count < 5)
        {
            return LegacyGuardShieldTextCommandResult.Incomplete;
        }

        var detail = fields[2].Trim();
        var username = fields[3].Trim();
        var server = fields[4].Trim();
        if (server.Length == 0)
        {
            return LegacyGuardShieldTextCommandResult.Incomplete;
        }

        if (detail.Length == 0 || username.Length == 0)
        {
            return LegacyGuardShieldTextCommandResult.Ignored;
        }

        var report = new LegacyGuardShieldUsageReport(
            kind,
            detail,
            username,
            server,
            options.EffectiveBanPolicy.CreateDecision(kind));
        options.ReportUsage?.Invoke(report);

        return LegacyGuardShieldTextCommandResult.Handled(report: report);
    }

    private static async ValueTask FlushPendingCommandsAsync(
        List<byte> pending,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> writeAsync,
        LegacyGuardShieldTextProtocolOptions options,
        bool force,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var newlineIndex = pending.IndexOf((byte)'\n');
            if (newlineIndex < 0)
            {
                break;
            }

            var commandBytes = pending.GetRange(0, newlineIndex).ToArray();
            pending.RemoveRange(0, newlineIndex + 1);
            await HandleCommandBytesAsync(commandBytes, writeAsync, options, cancellationToken)
                .ConfigureAwait(false);
        }

        if (pending.Count == 0)
        {
            return;
        }

        var result = HandleCommand(WireEncoding.GetString(pending.ToArray()), options);
        if (result.Status == LegacyGuardShieldTextCommandStatus.Incomplete && !force)
        {
            return;
        }

        pending.Clear();
        if (result.Status != LegacyGuardShieldTextCommandStatus.Incomplete)
        {
            await WriteResultAsync(result, writeAsync, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask HandleCommandBytesAsync(
        byte[] commandBytes,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> writeAsync,
        LegacyGuardShieldTextProtocolOptions options,
        CancellationToken cancellationToken)
    {
        var result = HandleCommand(WireEncoding.GetString(commandBytes), options);
        if (result.Status == LegacyGuardShieldTextCommandStatus.Incomplete)
        {
            return;
        }

        await WriteResultAsync(result, writeAsync, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask WriteResultAsync(
        LegacyGuardShieldTextCommandResult result,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> writeAsync,
        CancellationToken cancellationToken)
    {
        if (result.Response is not { Length: > 0 } response)
        {
            return;
        }

        await writeAsync(WireEncoding.GetBytes(response), cancellationToken).ConfigureAwait(false);
    }

    private static bool IsPartialCommand(string value, string command)
    {
        return value.Length > 0 &&
            value.Length < command.Length &&
            command.StartsWith(value, StringComparison.Ordinal);
    }

    private static bool IsUsageKind(string kind)
    {
        return string.Equals(kind, "Clicker", StringComparison.Ordinal) ||
            string.Equals(kind, "Program", StringComparison.Ordinal) ||
            string.Equals(kind, "Speed", StringComparison.Ordinal);
    }

    private static LegacyGuardShieldTextCommandResult ReportCommand(
        LegacyGuardShieldTextProtocolOptions options,
        string command,
        int fieldCount,
        LegacyGuardShieldTextCommandResult result)
    {
        options.ReportCommand?.Invoke(
            new LegacyGuardShieldCommandReport(
                command,
                result.Status,
                fieldCount,
                result.Response?.Length ?? 0,
                result.UsageReport?.Kind));
        return result;
    }
}

public sealed record LegacyGuardShieldCommandReport(
    string Command,
    LegacyGuardShieldTextCommandStatus Status,
    int FieldCount,
    int ResponseLength,
    string? UsageKind);

public sealed record LegacyGuardShieldUsageReport(
    string Kind,
    string Detail,
    string Username,
    string Server,
    LegacyGuardShieldBanDecision? BanDecision = null);

public sealed record LegacyGuardShieldTextCommandResult(
    LegacyGuardShieldTextCommandStatus Status,
    string? Response,
    LegacyGuardShieldUsageReport? UsageReport = null)
{
    public static LegacyGuardShieldTextCommandResult Incomplete { get; } =
        new(LegacyGuardShieldTextCommandStatus.Incomplete, null);

    public static LegacyGuardShieldTextCommandResult Ignored { get; } =
        new(LegacyGuardShieldTextCommandStatus.Ignored, null);

    public static LegacyGuardShieldTextCommandResult Handled(
        string? response = null,
        LegacyGuardShieldUsageReport? report = null)
    {
        return new LegacyGuardShieldTextCommandResult(
            LegacyGuardShieldTextCommandStatus.Handled,
            response,
            report);
    }
}

public enum LegacyGuardShieldTextCommandStatus
{
    Incomplete = 0,
    Ignored = 1,
    Handled = 2
}
