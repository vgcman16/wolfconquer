namespace WolfConquer.Core.Accounts;

public sealed record LegacyAccServerLoginLogEntry(
    string Username,
    long AccountId,
    string IpAddress,
    string Password,
    string ClockText,
    string SourcePath = "",
    int LineNumber = 0);
