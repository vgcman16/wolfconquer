using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Core.World;

// Immutable storage preparation only. This is not a current SQL revision,
// authenticated account, Store lifetime, native publisher or UID authority.
internal sealed partial class SqlOrdinaryAdmissionImagePlan
{
    private SqlOrdinaryAdmissionImagePlan(SqlWorldStateBundle before, SqlWorldStateBundle after,
        LegacyCapturedCharacterPrefix preparedPrefix, LegacySecondaryPasswordState pin, DateTime preparedAt, bool hasChanges)
    { Before = before; After = after; PreparedPrefix = preparedPrefix; Pin = pin; PreparedAt = preparedAt; HasChanges = hasChanges; }

    internal SqlWorldStateBundle Before { get; }
    internal SqlWorldStateBundle After { get; }
    internal LegacyCapturedCharacterPrefix PreparedPrefix { get; }
    internal LegacySecondaryPasswordState Pin { get; }
    internal DateTime PreparedAt { get; }
    internal bool HasChanges { get; }

    internal static bool TryPrepare(SqlWorldStateBundle? before, DateTime localNow,
        out SqlOrdinaryAdmissionImagePlan? plan, out string reason)
    {
        plan = null; reason = "sqlAdmissionImageMissing";
        if (before is null) return false;
        try
        {
            if (!LegacyCapturedCharacterPrefix.TryCreate(before.Core, out var original, out reason)) return false;
            if (!TryProject(before, original!, localNow, out var after, out var pin, out reason)) return false;
            if (after is null || pin is null) { reason = "sqlAdmissionProjectionMissing"; return false; }
            if (!PreservedImagesMatch(before, after))
            { reason = "sqlAdmissionPreservedImagesChanged"; return false; }
            if (!LegacyCapturedCharacterPrefix.TryCreate(after.Core, out var preparedPrefix, out reason)) return false;
            plan = new(before, after, preparedPrefix!, pin, localNow, ImagesDiffer(before, after));
            reason = string.Empty; return true;
        }
        catch (Exception error) when (error is FormatException or InvalidDataException or ArgumentException or
            InvalidOperationException or OverflowException or KeyNotFoundException or IndexOutOfRangeException)
        { reason = "sqlAdmissionImageInvalidData"; return false; }
    }
}
