using System.Collections.ObjectModel;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyExperienceContinuationCodec
{
    internal static LegacyExperienceContinuation? ReadCharacter(CharacterSnapshot character, long accountId)
    {
        var captured = Capture(character);
        var state = ReadMap(captured);
        state?.RequireCharacter(accountId, character.Uid, character.Level, character.Experience);
        return state;
    }

    internal static CharacterSnapshot WithState(CharacterSnapshot character, long accountId, LegacyExperienceContinuation? state)
    {
        var captured = Capture(character);
        var old = ReadMap(captured);
        // Scalar projection may already have changed Level/Experience; only
        // original marker identity is checked here. The raw two-image codec
        // separately proves its baseline and complete transition shape.
        if (accountId is <= 0 or > uint.MaxValue || character.Uid is <= 0 or > uint.MaxValue ||
            old is not null && (old.AccountId != accountId || old.CharacterUid != character.Uid))
            throw LegacyExperienceContinuation.Invalid();
        state?.RequireCharacter(accountId, character.Uid, character.Level, character.Experience);
        var values = new Dictionary<string, string>(captured.CapturedComparer);
        foreach (var pair in captured.Fields)
            if (!pair.Key.Equals(Key, StringComparison.OrdinalIgnoreCase)) values.Add(pair.Key, pair.Value);
        if (state is not null) values.Add(Key, Encode(state));
        if (!captured.IsCurrentPlain()) throw LegacyExperienceContinuation.Invalid();
        return character with { Attributes = new ReadOnlyDictionary<string, string>(values) };
    }

    private static LegacyNativeAttributeMapProof Capture(CharacterSnapshot character)
    {
        ArgumentNullException.ThrowIfNull(character);
        try { return LegacyNativeAttributeMapProof.CaptureRaw(character.Attributes); }
        catch (InvalidOperationException error) { throw LegacyExperienceContinuation.Invalid(error); }
    }

    private static LegacyExperienceContinuation? ReadMap(LegacyNativeAttributeMapProof captured)
    {
        string? encoded = null;
        foreach (var pair in captured.Fields)
            if (pair.Key.Equals(Key, StringComparison.OrdinalIgnoreCase))
            {
                if (encoded is not null) throw LegacyExperienceContinuation.Invalid();
                encoded = pair.Value;
            }
        if (!captured.IsCurrentPlain()) throw LegacyExperienceContinuation.Invalid();
        return encoded is null ? null : Decode(encoded);
    }
}
