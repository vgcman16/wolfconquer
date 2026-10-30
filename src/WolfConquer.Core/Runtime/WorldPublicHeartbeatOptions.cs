namespace WolfConquer.Core.Runtime;

public sealed record WorldPublicHeartbeatOptions
{
    public const string FileVariable = "WOLFCONQUER_PUBLIC_STATUS_FILE";
    public const string InstanceVariable = "WOLFCONQUER_PUBLIC_STATUS_INSTANCE_ID";
    public const string EnvironmentVariable = "WOLFCONQUER_PUBLIC_STATUS_ENVIRONMENT";
    public const string ClientBuildVariable = "WOLFCONQUER_PUBLIC_STATUS_CLIENT_BUILD";
    public string FilePath { get; }
    public Guid InstanceId { get; }
    public string Environment { get; }
    public string? ClientBuild { get; }

    public WorldPublicHeartbeatOptions(string filePath, Guid instanceId, string environment, string? clientBuild = null)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !Path.IsPathFullyQualified(filePath) ||
            filePath.StartsWith("\\\\", StringComparison.Ordinal) || filePath.StartsWith("//", StringComparison.Ordinal) ||
            !string.Equals(Path.GetFileName(filePath), "live-game-status.json", StringComparison.Ordinal))
            throw new ArgumentException("Public heartbeat requires its explicit local JSON path.", nameof(filePath));
        if (instanceId == Guid.Empty) throw new ArgumentException("Public heartbeat requires a nonzero instance.", nameof(instanceId));
        if (environment is not ("development" or "test" or "production"))
            throw new ArgumentException("Public heartbeat environment is invalid.", nameof(environment));
        if (clientBuild is not null && (clientBuild.Length is < 1 or > 48 ||
            clientBuild.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '_' or '-'))))
            throw new ArgumentException("Public heartbeat client target label is invalid.", nameof(clientBuild));
        FilePath = Path.GetFullPath(filePath); InstanceId = instanceId; Environment = environment; ClientBuild = clientBuild;
    }

    public static bool TryFromEnvironment(out WorldPublicHeartbeatOptions? options, out string reason,
        Func<string, string?>? readEnvironment = null)
    {
        options = null; reason = string.Empty; readEnvironment ??= System.Environment.GetEnvironmentVariable;
        string? file = readEnvironment(FileVariable);
        if (string.IsNullOrWhiteSpace(file)) return true;
        try
        {
            if (!Guid.TryParseExact(readEnvironment(InstanceVariable), "D", out var instance) || instance == Guid.Empty)
            { reason = "publicHeartbeatInstanceInvalid"; return false; }
            options = new(file, instance, readEnvironment(EnvironmentVariable) ?? "", readEnvironment(ClientBuildVariable));
            return true;
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or System.Security.SecurityException)
        { reason = "publicHeartbeatConfigurationInvalid"; return false; }
    }
}
