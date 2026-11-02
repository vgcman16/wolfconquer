using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyRelicFuseReturnOwnedEvidence
{
    internal bool TryCreateRetainedProjection(out LegacyRelicFuseReturnProjection? projection, out string reason)
    {
        projection = null; reason = "relicFuseReturnPreparedOwnerChanged";
        if (!DependenciesCurrent() || !preparation.TryProject(stable.PlayerItems, out var candidate, out reason) || candidate is null)
            return false;
        if (!PlayerItemSnapshotLoader.ToLegacyBytes(candidate.Items).AsSpan().SequenceEqual(afterImage))
        { reason = "relicFuseReturnProjectionChanged"; return false; }
        projection = candidate; reason = string.Empty; return true;
    }

    internal byte[][] CopyPreparedFrames()
    {
        if (!DependenciesCurrent()) throw Invalid();
        return preparedFrames.Select(row => row.ToArray()).ToArray();
    }

    internal LegacyRelicFuseReturnJournalOperation CreateJournalData()
    {
        _ = HighWater;
        return new(accountId, characterUid, packet.RelicID1);
    }

    internal void RequireJournalWriter(LegacyPreparedSpellFileWrite writer)
    {
        if (!before.IsRelicFuseWriter(writer) || !DependenciesCurrent()) throw Invalid();
        before.RequireRelicFuseOwnerHeld(context, stable);
        _ = HighWater;
    }

    internal void RequireJournalTargets(string usersRoot, string itemsRoot, string spellsRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes)
    {
        before.RequireRelicFuseOwnerHeld(context, stable);
        _ = HighWater;
        if (!TryCreateRetainedProjection(out var projection, out _) || projection is null || changes.Count != 3 ||
            !SamePath(before.CharacterPath, Path.Combine(usersRoot, accountId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".ini")) ||
            !SamePath(before.ItemPath, Path.Combine(itemsRoot, characterUid.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".bin")) ||
            !SamePath(before.SpellsPath, Path.Combine(spellsRoot, characterUid.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".bin"))) throw Invalid();
        var character = changes.SingleOrDefault(row => row.Kind == LegacyTradeJournalFileKind.Character);
        var items = changes.SingleOrDefault(row => row.Kind == LegacyTradeJournalFileKind.Items);
        var spells = changes.SingleOrDefault(row => row.Kind == LegacyTradeJournalFileKind.Spells);
        var savedCharacter = before.CopyCharacter(); var savedItems = before.CopyItems(); var savedSpells = before.CopySpells();
        if (character is null || items is null || spells is null || character.FileId != accountId ||
            items.FileId != characterUid || spells.FileId != characterUid || character.Before is null || items.Before is null ||
            !character.Before.AsSpan().SequenceEqual(savedCharacter) || !character.After.AsSpan().SequenceEqual(savedCharacter) ||
            !items.Before.AsSpan().SequenceEqual(savedItems) ||
            !items.After.AsSpan().SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(projection.Items)) ||
            (savedSpells is null ? spells.Before is not null || !spells.After.AsSpan().SequenceEqual(new byte[sizeof(int)]) :
                spells.Before is null || !spells.Before.AsSpan().SequenceEqual(savedSpells) || !spells.After.AsSpan().SequenceEqual(savedSpells)))
            throw Invalid();
    }

    internal bool ConfirmPublishedHeld(WorldSession actual, LegacyOrdinarySpellBinding actualBinding,
        LegacyPreparedSpellFileWrite writer)
    {
        if (published || !HasActualFrameLifecycle() || !ReferenceEquals(actualBinding, binding) || !before.IsRelicFuseWriter(writer) ||
            !before.HasCommittedRelicFuseReturn(this) || !Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) ||
            !Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) || !ReferenceEquals(actual.NativeGameClient, binding.Client) ||
            !ReferenceEquals(binding.Items.Image.FinalSnapshot, actual.PlayerItems) || !binding.Items.Image.IsCurrentPlain() ||
            !PlayerItemSnapshotLoader.ToLegacyBytes(actual.PlayerItems).AsSpan().SequenceEqual(afterImage)) return false;
        var highwater = runtime.RequireRelicFuseReturnHighWater(frame);
        if (physicalUids.Any(uid => uid > highwater)) return false;
        published = true; return true;
    }

    private static bool SamePath(string left, string right) => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
