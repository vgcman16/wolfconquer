namespace WolfConquer.Core.World;

internal enum LegacyExperienceContinuationPhase : byte { Ready, QueryPending, RestorePending }

// Immutable saved DATA. Neither an award nor a persistence/query capability.
internal sealed record LegacyExperienceContinuation
{
    internal Guid AwardId { get; }
    internal long AccountId { get; }
    internal uint CharacterUid { get; }
    internal ushort InitialLevel { get; }
    internal ulong InitialExperience { get; }
    internal ulong CreditedExperience { get; }
    internal ulong RemainingExperience { get; }
    internal ulong ConsumedExperience { get; }
    internal ulong CappedExperience { get; }
    internal ushort CurrentLevel { get; }
    internal ushort StepIndex { get; }
    internal LegacyExperienceContinuationPhase Phase { get; }
    internal uint? RestoreMaximum { get; }

    internal LegacyExperienceContinuation(Guid awardId, long accountId, uint characterUid,
        ushort initialLevel, ulong initialExperience, ulong creditedExperience, ulong remainingExperience,
        ulong consumedExperience, ulong cappedExperience, ushort currentLevel, ushort stepIndex,
        LegacyExperienceContinuationPhase phase, uint? restoreMaximum)
    {
        AwardId = awardId; AccountId = accountId; CharacterUid = characterUid; InitialLevel = initialLevel;
        InitialExperience = initialExperience; CreditedExperience = creditedExperience; RemainingExperience = remainingExperience;
        ConsumedExperience = consumedExperience; CappedExperience = cappedExperience; CurrentLevel = currentLevel;
        StepIndex = stepIndex; Phase = phase; RestoreMaximum = restoreMaximum;
        Validate();
    }

    internal static LegacyExperienceContinuation CreateCredit(long accountId, uint uid, Guid awardId,
        ushort initialLevel, ulong initialExperience, ulong creditedExperience)
    {
        try
        {
            return new(awardId, accountId, uid, initialLevel, initialExperience, creditedExperience,
                checked(initialExperience + creditedExperience), 0, 0, initialLevel, 0,
                LegacyExperienceContinuationPhase.Ready, null);
        }
        catch (OverflowException error) { throw Invalid(error); }
    }

    internal LegacyExperienceContinuation AdvanceLevel(ulong threshold)
    {
        if (Phase != LegacyExperienceContinuationPhase.Ready || CurrentLevel >= 140 || threshold == 0 || threshold > RemainingExperience)
            throw Invalid();
        ushort level = checked((ushort)(CurrentLevel + 1));
        ulong remaining = RemainingExperience - threshold;
        return new(AwardId, AccountId, CharacterUid, InitialLevel, InitialExperience, CreditedExperience,
            level == 140 ? 0 : remaining, checked(ConsumedExperience + threshold), level == 140 ? remaining : 0,
            level, checked((ushort)(StepIndex + 1)), LegacyExperienceContinuationPhase.QueryPending, null);
    }

    internal LegacyExperienceContinuation MarkRestore(uint maximum)
    {
        if (Phase is not (LegacyExperienceContinuationPhase.QueryPending or LegacyExperienceContinuationPhase.RestorePending)) throw Invalid();
        return Copy(LegacyExperienceContinuationPhase.RestorePending, maximum);
    }

    internal LegacyExperienceContinuation MarkReady()
    {
        if (Phase != LegacyExperienceContinuationPhase.RestorePending) throw Invalid();
        return Copy(LegacyExperienceContinuationPhase.Ready, null);
    }

    private LegacyExperienceContinuation Copy(LegacyExperienceContinuationPhase phase, uint? maximum) =>
        new(AwardId, AccountId, CharacterUid, InitialLevel, InitialExperience, CreditedExperience, RemainingExperience,
            ConsumedExperience, CappedExperience, CurrentLevel, StepIndex, phase, maximum);

    internal void RequireCharacter(long accountId, long uid, long level, long experience)
    {
        if (AccountId != accountId || CharacterUid != uid || CurrentLevel != level || experience < 0 ||
            RemainingExperience != (ulong)experience) throw Invalid();
    }

    private void Validate()
    {
        if (AwardId == Guid.Empty || AccountId is <= 0 or > uint.MaxValue || CharacterUid == 0 ||
            InitialLevel is < 1 or >= 140 || CurrentLevel is < 1 or > 140 || StepIndex > 139 ||
            CurrentLevel != InitialLevel + StepIndex || CreditedExperience == 0 ||
            Phase is not (LegacyExperienceContinuationPhase.Ready or LegacyExperienceContinuationPhase.QueryPending or
                LegacyExperienceContinuationPhase.RestorePending)) throw Invalid();
        try
        {
            ulong total = checked(InitialExperience + CreditedExperience);
            if (total > long.MaxValue || checked(RemainingExperience + ConsumedExperience + CappedExperience) != total ||
                RemainingExperience > total || ConsumedExperience > total || CappedExperience > total ||
                (CurrentLevel == 140 ? RemainingExperience != 0 : CappedExperience != 0) ||
                (StepIndex == 0 ? ConsumedExperience != 0 || CappedExperience != 0 || Phase != LegacyExperienceContinuationPhase.Ready
                    : ConsumedExperience < StepIndex) ||
                (Phase == LegacyExperienceContinuationPhase.RestorePending ? RestoreMaximum is null or > int.MaxValue : RestoreMaximum is not null))
                throw Invalid();
        }
        catch (OverflowException error) { throw Invalid(error); }
    }

    internal static InvalidDataException Invalid(Exception? inner = null) => new("experienceContinuationInvalid", inner);
}
