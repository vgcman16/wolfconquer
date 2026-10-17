namespace WolfConquer.Core.Accounts;

public enum AccountPasswordMatchKind
{
    None,
    Exact,
    LegacyTrailingNoise,
    LegacySavedPasswordOwnerRebind,
}
