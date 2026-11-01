using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public readonly record struct LegacyTeleportDestination(
    long MapId,
    long X,
    long Y,
    ulong DynamicId = 0);

public sealed record LegacyTeleportDestinationTransition(
    LegacyTeleportDestination Destination,
    CharacterSnapshot Character);

public sealed record LegacyTeleportDestinationReduction(
    LegacyTeleportDestinationTransition FirstTransition,
    LegacyTeleportDestinationTransition? Map6000Correction)
{
    public int TransitionCount => Map6000Correction is null ? 1 : 2;

    public CharacterSnapshot FinalCharacter =>
        Map6000Correction?.Character ?? FirstTransition.Character;
}

public static class LegacyTeleportDestinationReducer
{
    private const long TwinCityMapId = 1_002;
    private const long MissingMapFallbackX = 410;
    private const long MissingMapFallbackY = 354;
    private const long PrisonMapId = 6_000;
    private const long PrisonCorrectionX = 32;
    private const long PrisonCorrectionY = 73;

    public static LegacyTeleportDestinationReduction Reduce(
        CharacterSnapshot source,
        LegacyTeleportDestination requestedDestination,
        bool requestedMapExists,
        bool firstDestinationLocationIsValid = true)
    {
        ArgumentNullException.ThrowIfNull(source);

        var firstDestination = requestedMapExists
            ? requestedDestination
            : new LegacyTeleportDestination(
                TwinCityMapId,
                MissingMapFallbackX,
                MissingMapFallbackY,
                requestedDestination.DynamicId);
        var firstCharacter = ApplyTransition(source, firstDestination);
        var firstTransition = new LegacyTeleportDestinationTransition(
            firstDestination,
            firstCharacter);

        if (firstDestination.MapId != PrisonMapId || firstDestinationLocationIsValid)
        {
            return new LegacyTeleportDestinationReduction(firstTransition, Map6000Correction: null);
        }

        var correctionDestination = new LegacyTeleportDestination(
            PrisonMapId,
            PrisonCorrectionX,
            PrisonCorrectionY);
        var correctionCharacter = ApplyTransition(firstCharacter, correctionDestination);
        return new LegacyTeleportDestinationReduction(
            firstTransition,
            new LegacyTeleportDestinationTransition(correctionDestination, correctionCharacter));
    }

    private static CharacterSnapshot ApplyTransition(
        CharacterSnapshot source,
        LegacyTeleportDestination destination)
    {
        // Frozen DATA only. Actual callers retain their existing publication
        // owners; unique dynamic aliases cannot be collapsed into a new scope.
        var attributes = LegacyCharacterAttributeMap.Copy(source.Attributes);
        var sourceDynamicId = ReadDynamicId(attributes);
        var currentDynamicId = destination.MapId == TwinCityMapId
            ? 0
            : sourceDynamicId;

        if (source.MapId == destination.MapId && currentDynamicId == destination.DynamicId)
        {
            if (sourceDynamicId == currentDynamicId)
            {
                return source with
                {
                    X = destination.X,
                    Y = destination.Y,
                };
            }

            SetOwnedAttribute(attributes, "DynamicID", currentDynamicId);
            return source with
            {
                X = destination.X,
                Y = destination.Y,
                Attributes = attributes,
            };
        }

        SetOwnedAttribute(attributes, "DynamicID", destination.DynamicId);
        SetOwnedAttribute(attributes, "PDinamycID", currentDynamicId);
        return source with
        {
            MapId = destination.MapId,
            X = destination.X,
            Y = destination.Y,
            PreviousMapId = source.MapId,
            PreviousMapX = source.X,
            PreviousMapY = source.Y,
            Attributes = attributes,
        };
    }

    private static ulong ReadDynamicId(Dictionary<string, string> attributes) =>
        LegacyCharacterAttributeMap.TryRead(attributes, "DynamicID", out var raw) &&
        ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static void SetOwnedAttribute(Dictionary<string, string> attributes, string key, ulong value) =>
        LegacyCharacterAttributeMap.Set(attributes, key, value.ToString(CultureInfo.InvariantCulture));
}
