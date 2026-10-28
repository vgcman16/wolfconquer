namespace WolfConquer.Core.World;

public sealed record LegacyOperatorGearMailItem(uint ItemId, ushort StackSize = 1,
    byte Plus = 0, bool Bound = false, bool FirstSocket = false, bool SecondSocket = false);

public sealed record LegacyOperatorGearMailRequest(Guid OperationId, Guid ExpectedDomain,
    uint AccountId, uint CharacterUid, string Title, string SenderName, string Content,
    IReadOnlyList<LegacyOperatorGearMailItem> Items);

public enum LegacyOperatorGearMailStatus
{
    Rejected,
    PreviouslySubmittedOutcomeUnproven,
    CommittedNotificationCompleted,
    CommittedNotificationStopped,
    RecoveryRequired,
}

public sealed record LegacyOperatorGearMailResult(Guid OperationId, LegacyOperatorGearMailStatus Status,
    string Reason, uint[] MailIds, uint[] ItemUids);
