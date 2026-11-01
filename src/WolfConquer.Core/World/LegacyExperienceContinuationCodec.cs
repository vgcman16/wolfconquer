using System.Globalization;

namespace WolfConquer.Core.World;

internal static partial class LegacyExperienceContinuationCodec
{
    internal const string Key = "ExperienceContinuation";
    internal const int MaxEncodedLength = 256;

    internal static string Encode(LegacyExperienceContinuation state)
    {
        ArgumentNullException.ThrowIfNull(state);
        string result = string.Join('|', "1", state.AwardId.ToString("N"), Number((ulong)state.AccountId), Number(state.CharacterUid),
            Number(state.InitialLevel), Number(state.InitialExperience), Number(state.CreditedExperience), Number(state.RemainingExperience),
            Number(state.ConsumedExperience), Number(state.CappedExperience), Number(state.CurrentLevel), Number(state.StepIndex),
            Number((byte)state.Phase), state.RestoreMaximum is { } maximum ? Number(maximum) : "-");
        if (result.Length > MaxEncodedLength) throw LegacyExperienceContinuation.Invalid();
        return result;
    }

    internal static LegacyExperienceContinuation Decode(string encoded)
    {
        if (encoded is null || encoded.Length is 0 or > MaxEncodedLength) throw LegacyExperienceContinuation.Invalid();
        string[] values = encoded.Split('|');
        if (values.Length != 14 || values[0] != "1" || !Guid.TryParseExact(values[1], "N", out var award))
            throw LegacyExperienceContinuation.Invalid();
        try
        {
            var state = new LegacyExperienceContinuation(award, checked((long)Parse(values[2])), checked((uint)Parse(values[3])),
                checked((ushort)Parse(values[4])), Parse(values[5]), Parse(values[6]), Parse(values[7]), Parse(values[8]), Parse(values[9]),
                checked((ushort)Parse(values[10])), checked((ushort)Parse(values[11])),
                (LegacyExperienceContinuationPhase)checked((byte)Parse(values[12])), values[13] == "-" ? null : checked((uint)Parse(values[13])));
            if (!string.Equals(encoded, Encode(state), StringComparison.Ordinal)) throw LegacyExperienceContinuation.Invalid();
            return state;
        }
        catch (OverflowException error) { throw LegacyExperienceContinuation.Invalid(error); }
    }

    internal static void ValidateTransition(LegacyExperienceContinuation? before, LegacyExperienceContinuation? after)
    {
        if (before is null)
        {
            if (after is not null && after.StepIndex != 0) throw LegacyExperienceContinuation.Invalid();
            return;
        }
        if (after is null)
        {
            if (before.Phase != LegacyExperienceContinuationPhase.Ready) throw LegacyExperienceContinuation.Invalid();
            return; // Actual catalog/owner must separately prove that settlement is due.
        }
        if (before == after) return;
        LegacyExperienceContinuation? required = null;
        if (before.Phase == LegacyExperienceContinuationPhase.Ready && after.Phase == LegacyExperienceContinuationPhase.QueryPending &&
            after.ConsumedExperience > before.ConsumedExperience)
            required = before.AdvanceLevel(after.ConsumedExperience - before.ConsumedExperience);
        else if (before.Phase is LegacyExperienceContinuationPhase.QueryPending or LegacyExperienceContinuationPhase.RestorePending &&
            after.Phase == LegacyExperienceContinuationPhase.RestorePending && after.RestoreMaximum is { } maximum)
            required = before.MarkRestore(maximum);
        else if (before.Phase == LegacyExperienceContinuationPhase.RestorePending && after.Phase == LegacyExperienceContinuationPhase.Ready)
            required = before.MarkReady();
        if (required != after) throw LegacyExperienceContinuation.Invalid();
    }

    private static string Number(ulong value) => value.ToString(CultureInfo.InvariantCulture);
    private static ulong Parse(string value) => ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
        ? parsed : throw LegacyExperienceContinuation.Invalid();
}
