using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Typed data only; authority remains with the actual Store publication.
internal readonly record struct LegacyNativeAttributeState(
    ushort Strength, ushort Agility, ushort Vitality, ushort Spirit, ushort Atributes)
{
    internal static LegacyNativeAttributeState From(CharacterSnapshot character)
    {
        if (character.Strength is < 0 or > ushort.MaxValue || character.Agility is < 0 or > ushort.MaxValue ||
            character.Vitality is < 0 or > ushort.MaxValue || character.Spirit is < 0 or > ushort.MaxValue ||
            character.AttributePoints is < 0 or > ushort.MaxValue)
            throw new InvalidOperationException("ordinaryNativeAttributesOutOfRange");
        return new((ushort)character.Strength, (ushort)character.Agility, (ushort)character.Vitality,
            (ushort)character.Spirit, (ushort)character.AttributePoints);
    }
}
