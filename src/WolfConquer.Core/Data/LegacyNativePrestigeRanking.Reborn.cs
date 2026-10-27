using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativePrestigeRanking
{
    private static PrestigeRebornInput? CapturePrestigeRebornInput(LegacyNativeGameClientRuntime user)
    {
        var player = user.Player;
        if (player is null || !ReferenceEquals(player.Owner, user))
            throw new InvalidOperationException("Prestige reborn cleanup requires the client's physical player.");

        var reborn = player.Reborn;
        if (reborn is not 1 and not 2) return null;

        var uid = player.UID;
        if (uid == 0)
            throw new InvalidOperationException("Prestige reborn cleanup requires a positive character UID.");
        var previousClass = reborn == 1 ? player.FirstClass : player.SecoundeClass;
        // A world-only prior class has no class bucket to clean. It must never
        // acquire authority over Water merely because of a historical fallback.
        if (IsWorldOnlyClass(previousClass)) return null;
        if (!TryGetIndex(previousClass, out var previousCategory))
            throw new InvalidOperationException("Prestige reborn cleanup requires a supported prior class.");
        var currentClass = player.Class;
        var hasCurrentCategory = TryGetIndex(currentClass, out var currentCategory);
        if (!hasCurrentCategory && !IsWorldOnlyClass(currentClass))
            throw new InvalidOperationException("Prestige reborn cleanup requires a supported current class.");
        // A successful equipment rebuild can publish this current family first.
        // Keep that included family; only an obsolete prior bucket is removable.
        if (hasCurrentCategory && currentCategory == previousCategory)
            return null;
        return new PrestigeRebornInput(user, player, uid, reborn, previousClass, currentClass, previousCategory);
    }

    private static void RequireCurrentPrestigeRebornInput(PrestigeRebornInput input, object gate)
    {
        if (!ReferenceEquals(SynRoot, gate) || !ReferenceEquals(input.Client.Player, input.Player) ||
            !ReferenceEquals(input.Player.Owner, input.Client) || input.Player.UID != input.Uid ||
            input.Player.Reborn != input.Reborn || input.Player.Class != input.CurrentClass ||
            (input.Reborn == 1 ? input.Player.FirstClass : input.Player.SecoundeClass) != input.PreviousClass)
        {
            throw new InvalidOperationException("The captured prestige reborn player or selection changed.");
        }
    }

    private static PrestigeRankSource CapturePrestigeRebornRank(
        LegacyNativeDbSafeDictionary<Type, Rank> ranks, Type category)
    {
        var rank = FindPrestigeRebornRank(ranks, category);
        var source = CapturePrestigeRankSource(rank);
        if (source.Category != category)
            throw new InvalidOperationException("Prestige reborn cleanup found a mismatched selected rank.");
        return source;
    }

    private static Rank FindPrestigeRebornRank(
        LegacyNativeDbSafeDictionary<Type, Rank> ranks, Type category)
    {
        Rank? selected = null;
        var found = false;
        // Use actual raw category identity, not GetValues or a custom comparer.
        foreach (var pair in ranks)
        {
            if (pair.Key != category) continue;
            if (found)
                throw new InvalidOperationException("Prestige reborn cleanup found a duplicate selected bucket.");
            found = true;
            selected = pair.Value;
        }
        if (!found || selected is null)
            throw new InvalidOperationException("Prestige reborn cleanup requires its selected rank.");

        // Other owners and their rows are unrelated, but an alias of this exact
        // Rank would also publish into another bucket when its fields are stored.
        foreach (var pair in ranks)
        {
            if (pair.Key != category && ReferenceEquals(pair.Value, selected))
                throw new InvalidOperationException("Prestige reborn cleanup cannot mutate an aliased rank owner.");
        }
        return selected;
    }

    private static void RequireCurrentPrestigeRebornRank(
        LegacyNativeDbSafeDictionary<Type, Rank> ranks, PrestigeRankSource source)
    {
        if (!ReferenceEquals(Ranks, ranks) ||
            !ReferenceEquals(FindPrestigeRebornRank(ranks, source.Category), source.Owner) ||
            source.Owner._Type != source.Category || source.Owner.MaxItems != source.MaxItems ||
            !ReferenceEquals(source.Owner.Items, source.Items) || source.Items.Count != source.Rows.Length ||
            !ReferenceEquals(source.Owner.BestOfClass, source.Champion))
        {
            throw new InvalidOperationException("The selected prestige reborn rank changed before publication.");
        }
    }

    private sealed record PrestigeRebornInput(
        LegacyNativeGameClientRuntime Client,
        LegacyNativePlayerRuntime Player,
        uint Uid,
        byte Reborn,
        uint PreviousClass,
        uint CurrentClass,
        Type PreviousCategory);
}
