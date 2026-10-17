namespace WolfConquer.Core.Accounts;

public sealed record AccountLoginResult(
    AccountLoginStatus Status,
    AccountRecord? Account = null,
    AccountPasswordMatchKind PasswordMatchKind = AccountPasswordMatchKind.None,
    AccountLoginPasswordDiagnostic? PasswordDiagnostic = null)
{
    public bool IsSuccess => Status == AccountLoginStatus.Success;
}
