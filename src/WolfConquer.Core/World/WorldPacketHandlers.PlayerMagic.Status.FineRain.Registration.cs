using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static WorldSession ApplyLegacyFineRainRegistration(
        WorldSession session, LegacyMagicRuntimeSpell spell, int flag, TimeProvider provider)
    {
        // Each source AddSpellFlag replaces ONLY this registration at its own call point.
        var character = WithLegacySourceAddSpellFlag(session.Character, flag, removeOnDead: true);
        DateTimeOffset expiresAt;
        if (flag == LegacyFineRain1Flag)
        {
            var invocation = LegacyFineRain212Invocation.Create(
                character.Uid, spell.Duration, provider, out expiresAt);
            character = invocation.Install(character);
        }
        else if (flag == LegacyFineRain2Flag)
        {
            expiresAt = provider.GetUtcNow().AddSeconds(unchecked((int)spell.Duration));
        }
        else throw new ArgumentOutOfRangeException(nameof(flag));

        character = WithCharacterAttribute(character, flag == LegacyFineRain1Flag
            ? RuntimeLegacyFineRain1RemoveOnDeadAttribute : RuntimeLegacyFineRain2RemoveOnDeadAttribute, "true");
        character = WithCharacterAttribute(character, flag == LegacyFineRain1Flag
            ? RuntimeLegacyFineRain1ExpiresAtUtcAttribute : RuntimeLegacyFineRain2ExpiresAtUtcAttribute,
            expiresAt.ToString("O", CultureInfo.InvariantCulture));
        return session with { Character = character };
    }
}
