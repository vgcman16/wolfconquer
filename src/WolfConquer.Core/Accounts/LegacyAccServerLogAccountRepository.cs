namespace WolfConquer.Core.Accounts;

public sealed class LegacyAccServerLogAccountRepository : IAccountRepository
{
    private readonly IReadOnlyList<AccountRecord> _accounts;

    public LegacyAccServerLogAccountRepository(
        IEnumerable<LegacyAccServerLoginLogEntry> entries,
        IEnumerable<long>? bannedAccountIds = null)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var bannedIds = bannedAccountIds?.ToHashSet() ?? [];
        var latestByAccountId = new Dictionary<long, LegacyAccServerLoginLogEntry>();
        foreach (var entry in entries)
        {
            if (entry.AccountId <= 0 || string.IsNullOrWhiteSpace(entry.Username))
            {
                continue;
            }

            latestByAccountId[entry.AccountId] = entry;
        }

        _accounts = latestByAccountId.Values
            .OrderBy(entry => entry.AccountId)
            .Select(entry => new AccountRecord(
                entry.AccountId,
                entry.Username,
                entry.Password,
                entry.AccountId,
                bannedIds.Contains(entry.AccountId)
                    ? 1
                    : AccountLoginService.AllowedBannedId,
                IsOnline: false))
            .ToArray();
    }

    public static LegacyAccServerLogAccountRepository FromLogFiles(
        IEnumerable<string> paths,
        string? banUidPath = null,
        DateTime? now = null)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var entries = new List<LegacyAccServerLoginLogEntry>();
        foreach (var path in paths.OrderBy(GetLogFileSortKey, StringComparer.OrdinalIgnoreCase))
        {
            entries.AddRange(LegacyAccServerLoginLogParser.ParseFile(path));
        }

        IReadOnlySet<long> bannedAccountIds = new HashSet<long>();
        if (!string.IsNullOrWhiteSpace(banUidPath) && File.Exists(banUidPath))
        {
            bannedAccountIds = LegacyBanUidFileParser.ParseActiveAccountIds(
                File.ReadLines(banUidPath),
                now ?? DateTime.Now);
        }

        return new LegacyAccServerLogAccountRepository(entries, bannedAccountIds);
    }

    public ValueTask<IReadOnlyList<AccountRecord>> FindByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(username);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<AccountRecord> matches = _accounts
            .Where(account => string.Equals(account.Username, username, StringComparison.Ordinal))
            .ToArray();
        return ValueTask.FromResult(matches);
    }

    private static string GetLogFileSortKey(string path)
    {
        return Path.GetFullPath(path);
    }
}
