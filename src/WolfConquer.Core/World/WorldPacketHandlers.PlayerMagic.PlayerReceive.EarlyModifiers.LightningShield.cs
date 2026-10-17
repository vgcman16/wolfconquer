namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> ApplyLegacyPlayerReceiveLightningShieldPrefixAsync(
        WorldPacketHandlerContext context, LegacyPlayerReceiveEarlyModifierExecution phase,
        CancellationToken cancellationToken)
    {
        var shieldSpell = phase.Target.Session.PlayerSpells.Spells.FirstOrDefault(spell => spell.Id == 15_750);
        if (HasLegacyStatusFlag(phase.Target.Session.Character, LegacyLightningShieldFlag) &&
            shieldSpell is not null && CheckLegacyLightningShieldRateRoll(phase.Target.Session.Character))
        {
            var level = shieldSpell.Level; // Source selected DB row remains captured through AddFlag's send.
            var left = HasLegacyStatusFlag(phase.Target.Session.Character, LegacyThunderRampageFlag)
                ? checked((uint)(20_000 + level + 1_000)) : 20_000U;
            left = checked(left + GetLegacyFlashShieldBonus(phase.Target.Session.Character));
            var alreadyActive = HasLegacyStatusFlag(phase.Target.Session.Character, LegacyLightningShieldActivatedFlag);
            if (!phase.CommitTarget(current => WithCharacterAttribute(
                    LegacyLightningShieldActivationLifetime.CaptureFirstInsertion(current,
                        WithLegacySourceAddFlag(current, LegacyLightningShieldActivatedFlag, removeOnDead: true),
                        (context.TimeProvider ?? TimeProvider.System).GetLocalNow().DateTime),
                    RuntimeLegacyLightningShieldLeftAttribute, left))) return false;
            // AddFlag is first-insertion only: no duplicate registration vector.
            if (!alreadyActive && !await phase.SendTargetVectorAsync(cancellationToken).ConfigureAwait(false))
                return false;
            phase.ActivationLeft = ReadAttributeUInt32(
                phase.Target.Session.Character.Attributes, RuntimeLegacyLightningShieldLeftAttribute);
            phase.ShieldLevel = level;
            if (!await phase.SendAzureAsync(phase.ActivationLeft.Value, level, cancellationToken)
                    .ConfigureAwait(false)) return false;
        }

        // Drain is selected from CURRENT state after activation/vector/Azure callbacks.
        var remaining = ReadAttributeUInt32(phase.Target.Session.Character.Attributes,
            RuntimeLegacyLightningShieldLeftAttribute);
        if (HasLegacyStatusFlag(phase.Target.Session.Character, LegacyLightningShieldActivatedFlag) && remaining != 0)
        {
            if (phase.Damage >= remaining)
            {
                phase.Damage -= remaining;
                remaining = 0;
            }
            else
            {
                remaining -= phase.Damage;
                phase.Damage = 0;
            }
            if (!phase.CommitTarget(current => WithCharacterAttribute(current,
                    RuntimeLegacyLightningShieldLeftAttribute, remaining))) return false;
            phase.DrainLeft = remaining;
            shieldSpell = phase.Target.Session.PlayerSpells.Spells.FirstOrDefault(spell => spell.Id == 15_750);
            if (shieldSpell is not null) phase.ShieldLevel = shieldSpell.Level;
            if (!await phase.SendAzureAsync(remaining, phase.ShieldLevel, cancellationToken, remainingTime: true)
                    .ConfigureAwait(false)) return false;
        }
        return phase.Refresh();
    }
}
