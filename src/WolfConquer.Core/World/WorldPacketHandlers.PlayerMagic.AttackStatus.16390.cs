using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const int LegacyBonePulseStatusFlag = 0x11F;
    private const int LegacyBonePulseDurationSeconds = 10;
    private const string RuntimeBonePulsePowerAttribute = "BonePulsePower";
    private const string RuntimeLegacyBonePulsePowerAttribute = "LegacyBonePulsePower";
    private const string RuntimeBonePulseExpiresAtUtcAttribute = "BonePulseExpiresAtUtc";
    private const string RuntimeLegacyBonePulseExpiresAtUtcAttribute = "LegacyBonePulseExpiresAtUtc";
    private const string RuntimeLegacyBonePulseRemoveOnDeadAttribute = "LegacyBonePulseRemoveOnDead";
    private const string RuntimeLegacyTortoiseDamagePercentAttribute = "LegacyTortoiseDamagePercent";
    private const string RuntimeTortoiseDamagePercentAttribute = "TortoiseDamagePercent";
    private const string RuntimeTortoiseGemDamagePercentAttribute = "TortoiseGemDamagePercent";

    private static async ValueTask<bool> HandleLegacyPlayerBonePulseAttackStatusMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        _ = query;
        if (spell.Sort != LegacyMagicSort.AttackStatus ||
            spell.Id != LegacyBonePulseSpellId)
        {
            return false;
        }

        var resourceSession = await TryPrepareLegacyPlayerMagicCastAsync(
                context,
                session,
                spell,
                cancellationToken)
            .ConfigureAwait(false);
        if (resourceSession is null)
        {
            return true;
        }

        var updatedSession = ApplyLegacyBonePulseStatus(resourceSession, spell, DateTimeOffset.UtcNow);
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

        var casterUid = CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid));
        var animation = new LegacySpellAnimationPacket(
            casterUid,
            AttackedUid: casterUid,
            X: 0,
            Y: 0,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            NextSpell: 0,
            LevelHu: playerSpell.UseSpellSoul,
            SpellEffect: 0,
            Targets: []);
        await SendPlayerSpellAnimationToVisibleAsync(updatedContext, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy BonePulse AttackStatus magic " +
                $"{FormatSession(updatedContext)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} statusFlag={LegacyBonePulseStatusFlag} " +
                $"duration={LegacyBonePulseDurationSeconds} bonePulsePower={spell.DamageOnHuman}; " +
                "source=AttachStatus.cs case 16390.")
            .ConfigureAwait(false);
        return true;
    }

    private static WorldSession ApplyLegacyBonePulseStatus(
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        DateTimeOffset nowUtc)
    {
        var expiresAt = nowUtc
            .ToUniversalTime()
            .AddSeconds(LegacyBonePulseDurationSeconds)
            .ToString("O", CultureInfo.InvariantCulture);
        var power = spell.DamageOnHuman.ToString(CultureInfo.InvariantCulture);
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                WithCharacterAttribute(
                    WithCharacterAttribute(
                        WithCharacterAttribute(
                            WithLegacyStatusFlag(session.Character, LegacyBonePulseStatusFlag),
                            RuntimeBonePulsePowerAttribute,
                            power),
                        RuntimeLegacyBonePulsePowerAttribute,
                        power),
                    RuntimeBonePulseExpiresAtUtcAttribute,
                    expiresAt),
                RuntimeLegacyBonePulseExpiresAtUtcAttribute,
                expiresAt),
            RuntimeLegacyBonePulseRemoveOnDeadAttribute,
            "true");
        return session with { Character = updatedCharacter };
    }
}
