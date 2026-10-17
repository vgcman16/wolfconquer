namespace WolfConquer.Core.Accounts;

public sealed record AccountHandoffOptions(string? StoreRoot, string? SigningSecret)
{
    public const string StoreRootEnvironmentVariable = "WOLFCONQUER_HANDOFF_ROOT";
    public const string SigningSecretEnvironmentVariable = "WOLFCONQUER_HANDOFF_SECRET";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(StoreRoot);

    public bool IsSigningConfigured => !string.IsNullOrWhiteSpace(SigningSecret);

    public static AccountHandoffOptions FromEnvironment()
    {
        return new AccountHandoffOptions(
            Environment.GetEnvironmentVariable(StoreRootEnvironmentVariable),
            Environment.GetEnvironmentVariable(SigningSecretEnvironmentVariable));
    }
}
