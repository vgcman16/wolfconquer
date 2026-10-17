using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static CharacterSnapshot WithLegacySourceAddFlag(
        CharacterSnapshot character, int flag, bool removeOnDead) =>
        LegacyStatusFlagDeathRegistration.CaptureAddFlag(character,
            WithLegacyStatusFlag(character, flag), flag, removeOnDead);

    private static CharacterSnapshot WithLegacySourceAddSpellFlag(
        CharacterSnapshot character, int flag, bool removeOnDead) =>
        LegacyStatusFlagDeathRegistration.CaptureAddSpellFlag(
            WithLegacyStatusFlag(flag == 212 ? LegacyFineRain212Invocation.Forget(character)
                : flag == LegacyLightningShieldActivationLifetime.StatusFlag
                ? LegacyLightningShieldActivationLifetime.Forget(character)
                : flag == LegacyLightningShieldBaseLifetime.StatusFlag
                    ? LegacyLightningShieldBaseLifetime.Forget(character)
                    : LegacyArrowBladesFlagRegistration.Forget(character, flag), flag),
            flag, removeOnDead);
}
