using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

/// <summary>Retires only the owned15750/240 clock; never invents a DB timer.</summary>
public static class LegacyLightningShieldBaseLifetime
{
    public const int StatusFlag = 240;
    public const string ExpiresAtUtcAttribute = "LegacyLightningShieldExpiresAtUtc";

    public static CharacterSnapshot Forget(CharacterSnapshot character)
    {
        if (!character.Attributes.ContainsKey(ExpiresAtUtcAttribute)) return character;
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        Forget(attributes);
        return character with { Attributes = attributes };
    }

    public static void Forget(Dictionary<string, string> attributes) => attributes.Remove(ExpiresAtUtcAttribute);
}
