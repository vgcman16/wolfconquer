namespace WolfConquer.Core.Accounts;

public sealed record AccountPasswordOwnerDiagnostic(
    long Id,
    string Username,
    int PasswordLength);
