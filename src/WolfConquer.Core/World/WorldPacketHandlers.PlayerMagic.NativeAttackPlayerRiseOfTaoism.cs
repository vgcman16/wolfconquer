using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static uint CompleteLegacyNativeAttackPlayerPostBackDamage(
        CharacterSnapshot currentCaster, ushort spellId, uint damage)
    {
        // Magic.OnPlayer654: late CASTER flag/field, after BackDmg and before
        // Infinity/IronShield. Read the refreshed post-BackDmg participant,
        // not the entry caster, cached MA, row alias, or target flag.
        // Native shield-block must precede this seam when implemented;
        // native Infinity/IronShield follow it. Those phases remain required.
        if (IsLegacyNativeAttackPlayerCompletion(spellId) &&
            HasLegacyStatusFlag(currentCaster, LegacyRiseOfTaoismFlag))
        {
            damage = unchecked(damage + ReadAttributeUInt32(
                currentCaster.Attributes, RuntimeLegacyRiseOfTaoismExtraMagicDamageAttribute));
        }

        // Attack.cs caller half follows the completed Magic.OnPlayer scalar.
        // WildPhoenix/ManiacDance/Vortex/MagicDefender early exits bypass this helper.
        return CompleteLegacyNativeAttackPlayerDamage(spellId, damage);
    }
}
