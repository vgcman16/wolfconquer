using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Core.World;

internal sealed partial class SqlOrdinaryAdmissionImagePlan
{
    private static bool PreservedImagesMatch(SqlWorldStateBundle before, SqlWorldStateBundle after)
    {
        if (before.AccountId != after.AccountId || before.CharacterUid != after.CharacterUid ||
            !before.Core.ItemBytes.SequenceEqual(after.Core.ItemBytes) || !before.ItemUids.SequenceEqual(after.ItemUids) ||
            before.Sidecars.Length != after.Sidecars.Length) return false;
        for (int index = 0; index < before.Sidecars.Length; index++)
        {
            var left = before.Sidecars[index]; var right = after.Sidecars[index];
            if (left.Kind != right.Kind || left.Kind != SqlWorldStateSidecarKind.Spells && !SidecarsEqual(left, right)) return false;
        }
        return true;
    }

    private static bool ImagesDiffer(SqlWorldStateBundle before, SqlWorldStateBundle after)
    {
        if (!before.Core.CharacterBytes.SequenceEqual(after.Core.CharacterBytes) ||
            !before.Core.ItemBytes.SequenceEqual(after.Core.ItemBytes) || before.Sidecars.Length != after.Sidecars.Length) return true;
        for (int index = 0; index < before.Sidecars.Length; index++)
            if (!SidecarsEqual(before.Sidecars[index], after.Sidecars[index])) return true;
        return false;
    }

    private static bool SidecarsEqual(SqlWorldStateSidecar left, SqlWorldStateSidecar right) =>
        left.Kind == right.Kind && left.Presence == right.Presence && left.Bytes.SequenceEqual(right.Bytes);
}
