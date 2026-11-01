using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Pure projections, never a live pair/permission or arbitrary after-session
    // acceptance. The real Store validates both actual participants separately.
    internal static WorldSession ProjectPairMovementActor(WorldSession before, LegacyPairMovementOperation operation)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(operation);
        operation.Validate();
        var character = operation.Kind == LegacyPairMovementKind.Walk
            ? ProjectPairWalkActor(before.Character, operation)
            : ProjectPairJumpActor(before.Character, operation);
        return before with { Character = character };
    }

    internal static WorldSession ProjectPairMovementPartner(WorldSession before, LegacyPairMovementOperation operation)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(operation);
        operation.Validate();
        var character = before.Character with
        {
            X = operation.ToX,
            Y = operation.ToY,
            Attributes = WithCharacterAttribute(before.Character, "Angle", operation.DirectionOrFacing).Attributes,
        };
        if (operation.Kind == LegacyPairMovementKind.Jump)
            character = WithCharacterAttribute(character, "Action", 100U);
        return before with { Character = character };
    }

    private static CharacterSnapshot ProjectPairWalkActor(CharacterSnapshot before, LegacyPairMovementOperation operation)
    {
        var character = LegacyMovementSideEffects.ApplyMsgMovement(before).Character;
        var attributes = WithCharacterAttribute(character, "Angle", operation.DirectionOrFacing).Attributes;
        TryCancelLegacyIntensifyArm(attributes, out var focusAttributes);
        TryRemoveLegacyMovementStatusFlags(focusAttributes, out var movementAttributes);
        character = character with { X = operation.ToX, Y = operation.ToY, Attributes = movementAttributes };
        // Both original and current paired walk return before ActivePick's
        // ordinary tail. Do not silently remove it here.
        return WithCharacterAttribute(character, "Action", 0U);
    }

    private static CharacterSnapshot ProjectPairJumpActor(CharacterSnapshot before, LegacyPairMovementOperation operation)
    {
        var character = LegacyMovementSideEffects.ApplyMsgDataPlayerJump(before).Character;
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["Angle"] = operation.DirectionOrFacing.ToString(CultureInfo.InvariantCulture),
            ["Action"] = "100",
        };
        TryCancelLegacyIntensifyArm(attributes, out var focusAttributes);
        TryRemoveLegacyMovementStatusFlags(focusAttributes, out var movementAttributes);
        // The actual paired-jump branch returns before ordinary Ride vigor and
        // ActivePick tails. Neither is a price for the pair transaction.
        return character with { X = operation.ToX, Y = operation.ToY, Attributes = movementAttributes };
    }
}
