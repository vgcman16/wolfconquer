namespace WolfConquer.Core.Runtime;

public enum ConquerProtocolProfile
{
    RootD11_7899,
    VisibleDx8D13,
}

public static class ConquerProtocolProfileConfiguration
{
    public const string EnvironmentVariable = "WOLFCONQUER_PROTOCOL_PROFILE";

    public static ConquerProtocolProfile FromEnvironment(
        Func<string, string?>? readEnvironmentVariable = null)
    {
        readEnvironmentVariable ??= Environment.GetEnvironmentVariable;
        return Parse(readEnvironmentVariable(EnvironmentVariable));
    }

    public static ConquerProtocolProfile Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"{EnvironmentVariable} is required and must be set to " +
                $"{nameof(ConquerProtocolProfile.RootD11_7899)} or " +
                $"{nameof(ConquerProtocolProfile.VisibleDx8D13)}.");
        }

        var normalized = value.Trim();
        if (string.Equals(
                normalized,
                nameof(ConquerProtocolProfile.RootD11_7899),
                StringComparison.OrdinalIgnoreCase))
        {
            return ConquerProtocolProfile.RootD11_7899;
        }

        if (string.Equals(
                normalized,
                nameof(ConquerProtocolProfile.VisibleDx8D13),
                StringComparison.OrdinalIgnoreCase))
        {
            return ConquerProtocolProfile.VisibleDx8D13;
        }

        throw new InvalidOperationException(
            $"Unknown {EnvironmentVariable} value '{normalized}'. Expected " +
            $"{nameof(ConquerProtocolProfile.RootD11_7899)} or " +
            $"{nameof(ConquerProtocolProfile.VisibleDx8D13)}.");
    }
}
