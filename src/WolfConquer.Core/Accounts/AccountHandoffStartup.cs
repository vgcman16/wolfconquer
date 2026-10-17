namespace WolfConquer.Core.Accounts;

public static class AccountHandoffStartup
{
    public static async Task<AccountHandoffService?> TryCreateServiceAsync(TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(log);

        var options = AccountHandoffOptions.FromEnvironment();
        if (!options.IsConfigured)
        {
            await log.WriteLineAsync(
                    $"Account handoff not loaded; set {AccountHandoffOptions.StoreRootEnvironmentVariable} to share login tickets between AccountServer and WorldServer.")
                .ConfigureAwait(false);
            return null;
        }

        if (!options.IsSigningConfigured)
        {
            await log.WriteLineAsync(
                    $"Account handoff not loaded; set {AccountHandoffOptions.SigningSecretEnvironmentVariable} so shared tickets cannot be forged from editable JSON.")
                .ConfigureAwait(false);
            return null;
        }

        try
        {
            var storeRoot = Path.GetFullPath(options.StoreRoot!);
            var store = new FileAccountHandoffTicketStore(storeRoot, options.SigningSecret!);
            await log.WriteLineAsync($"Account handoff root: {store.RootPath}").ConfigureAwait(false);
            return new AccountHandoffService(store);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            await log.WriteLineAsync(
                    $"Account handoff not loaded; {ex.Message}. Set {AccountHandoffOptions.StoreRootEnvironmentVariable} to a writable directory.")
                .ConfigureAwait(false);
            return null;
        }
    }
}
