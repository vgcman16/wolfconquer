using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed record LegacyRuneBagRecordedProjection(CharacterSnapshot Character, PlayerItemSnapshot Items, PlayerSpellSnapshot Spells);

public static partial class WorldPacketHandlers
{
    internal static LegacyRuneBagRecordedProjection ProjectRecordedRuneBag(CharacterSnapshot character,
        PlayerItemSnapshot items, PlayerSpellSnapshot spells, LegacyRuneBagOperation operation)
    {
        operation.Validate();
        if (character.Reborn < 2 || character.Level < 15 || operation.Request is { } request && character.Uid != request.CharacterUid)
            throw InvalidSpellMutation();
        var recipe = LegacyRuneBagRecipe.Prepare(items, operation.SelectedUid, operation.IntoBag, operation.Metadata);
        if (recipe.Plan.NewUidCount != (operation.Allocation is null ? 0 : 1)) throw InvalidSpellMutation();
        var projected = recipe.Project(operation.Allocation?.First);
        // Position0 and211 are absent from WornObjects and Rune.Score. The bag
        // commit never grants/removes a spell, clamps HP, or rewrites origins.
        return new(character, projected.Items, spells);
    }
}
