namespace WolfConquer.Core.Accounts;

public sealed record AccountLoginPasswordDiagnostic(
    int RequestLength,
    int CandidateCount,
    int BestStoredLength,
    int BestCommonPrefixLength,
    int BestLengthDelta,
    string TailShape,
    int ExactPasswordOwnerCount = 0,
    long? FirstExactPasswordOwnerId = null,
    string? FirstExactPasswordOwnerUsername = null);
