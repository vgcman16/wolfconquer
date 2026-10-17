using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyAxeShadowStatusSetterSpellId = 15_940;
    private const ushort LegacyCelestialDanceStatusSetterSpellId = 7_030;
    private const string RuntimeAxeShadowPowerAttribute = "AxeShadowPower";
    private const string RuntimeLegacyAxeShadowPowerAttribute = "LegacyAxeShadowPower";
    private const string RuntimeAxeShadowExpiresAtUtcAttribute = "AxeShadowExpiresAtUtc";
    private const string RuntimeLegacyAxeShadowExpiresAtUtcAttribute = "LegacyAxeShadowExpiresAtUtc";
    private const string RuntimeCelestialDancePowerAttribute = "CelestialDancePower";
    private const string RuntimeLegacyCelestialDancePowerAttribute = "LegacyCelestialDancePower";
    private const string RuntimeCelestialDanceExpiresAtUtcAttribute = "CelestialDanceExpiresAtUtc";
    private const string RuntimeLegacyCelestialDanceExpiresAtUtcAttribute = "LegacyCelestialDanceExpiresAtUtc";

    private static async ValueTask<bool> HandleLegacyPlayerAxeShadowCelestialDanceStatusMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell is { Sort: LegacyMagicSort.Wildwind, Id: LegacyAxeShadowStatusSetterSpellId })
        {
            return await HandleLegacyPlayerAxeShadowStatusMagicAsync(
                    context,
                    session,
                    query,
                    playerSpell,
                    spell,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (spell is { Sort: LegacyMagicSort.Bomb, Id: LegacyCelestialDanceStatusSetterSpellId })
        {
            return await HandleLegacyPlayerCelestialDanceStatusMagicAsync(
                    context,
                    session,
                    query,
                    playerSpell,
                    spell,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return false;
    }

    private static async ValueTask<bool> HandleLegacyPlayerAxeShadowStatusMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
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

        var casterUid = CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid));
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            casterUid,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            []);
        await SendPlayerSpellAnimationToVisibleAsync(context with { Session = resourceSession }, resourceSession, animation, cancellationToken)
            .ConfigureAwait(false);

        var expiresAt = AddLegacyAxeShadowCelestialDanceDuration(DateTimeOffset.UtcNow, spell.Duration);
        var updatedSession = resourceSession with
        {
            Character = WithLegacyAxeShadowStatus(resourceSession.Character, spell.Damage3, expiresAt),
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyCombatGearArchiveSkillUpdateAsync(
                updatedContext,
                updatedSession,
                LegacyAxeShadowFlag,
                spell.Duration,
                damage: 0,
                spellLevel: 0,
                visible: true,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy AxeShadow Wildwind status magic " +
                $"{FormatSession(updatedContext)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"statusFlag={LegacyAxeShadowFlag} axeShadowPower={spell.Damage3} duration={spell.Duration} " +
                $"expiresAtUtc={expiresAt:O}; source=Wildwind.cs case 15940.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> HandleLegacyPlayerCelestialDanceStatusMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
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

        var casterUid = CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid));
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            casterUid,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            []);
        await SendPlayerSpellAnimationToVisibleAsync(context with { Session = resourceSession }, resourceSession, animation, cancellationToken)
            .ConfigureAwait(false);

        var power = spell.DamageOnHuman % 1_000;
        var expiresAt = AddLegacyAxeShadowCelestialDanceDuration(DateTimeOffset.UtcNow, spell.Duration);
        var updatedSession = await ApplyLegacyPlayerCelestialDanceSelfStatusAsync(
                context,
                resourceSession,
                spell,
                power,
                expiresAt,
                casterUid,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy CelestialDance Bomb status magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"statusFlag={LegacyCelestialDanceFlag} celestialDancePower={power} duration={spell.Duration} " +
                $"expiresAtUtc={expiresAt:O}; source=Bomb.cs case 7030.")
            .ConfigureAwait(false);
        return true;
    }

    private static ValueTask<WorldSession> ApplyLegacyPlayerCelestialDanceSelfStatusAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        uint casterUid,
        CancellationToken cancellationToken)
    {
        var power = spell.DamageOnHuman % 1_000;
        var expiresAt = AddLegacyAxeShadowCelestialDanceDuration(DateTimeOffset.UtcNow, spell.Duration);
        return ApplyLegacyPlayerCelestialDanceSelfStatusAsync(
            context,
            session,
            spell,
            power,
            expiresAt,
            casterUid,
            cancellationToken);
    }

    private static async ValueTask<WorldSession> ApplyLegacyPlayerCelestialDanceSelfStatusAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        uint power,
        DateTimeOffset expiresAt,
        uint casterUid,
        CancellationToken cancellationToken)
    {
        var updatedSession = session with
        {
            Character = WithLegacyCelestialDanceStatus(session.Character, power, expiresAt),
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        var dancePacket = new LegacyMsgUpdatePacket(
            casterUid,
            [
                new LegacyMsgUpdateEntry(
                    LegacyMsgUpdateDataType.CelestialDance,
                    Value: 0,
                    Flag: LegacyCelestialDanceFlag,
                    Time: spell.Duration,
                    Damage: 0,
                    SpellLevel: 0),
            ]);
        await SendFrameToPlayerAndVisiblePeersAsync(
                updatedContext,
                context.Connection,
                updatedSession,
                dancePacket.ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static CharacterSnapshot WithLegacyAxeShadowStatus(
        CharacterSnapshot character,
        uint power,
        DateTimeOffset expiresAtUtc)
    {
        var formattedExpiresAt = expiresAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        return WithCharacterAttribute(
            WithCharacterAttribute(
                WithCharacterAttribute(
                    WithCharacterAttribute(
                        WithLegacyStatusFlag(character, LegacyAxeShadowFlag),
                        RuntimeAxeShadowPowerAttribute,
                        power),
                    RuntimeLegacyAxeShadowPowerAttribute,
                    power),
                RuntimeAxeShadowExpiresAtUtcAttribute,
                formattedExpiresAt),
            RuntimeLegacyAxeShadowExpiresAtUtcAttribute,
            formattedExpiresAt);
    }

    private static CharacterSnapshot WithLegacyCelestialDanceStatus(
        CharacterSnapshot character,
        uint power,
        DateTimeOffset expiresAtUtc)
    {
        var formattedExpiresAt = expiresAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        return WithCharacterAttribute(
            WithCharacterAttribute(
                WithCharacterAttribute(
                    WithCharacterAttribute(
                        WithLegacyStatusFlag(character, LegacyCelestialDanceFlag),
                        RuntimeCelestialDancePowerAttribute,
                        power),
                    RuntimeLegacyCelestialDancePowerAttribute,
                    power),
                RuntimeCelestialDanceExpiresAtUtcAttribute,
                formattedExpiresAt),
            RuntimeLegacyCelestialDanceExpiresAtUtcAttribute,
            formattedExpiresAt);
    }

    private static DateTimeOffset AddLegacyAxeShadowCelestialDanceDuration(DateTimeOffset nowUtc, uint durationSeconds)
    {
        var normalizedNow = nowUtc.ToUniversalTime();
        if (durationSeconds == 0)
        {
            return normalizedNow;
        }

        var maximumDuration = DateTimeOffset.MaxValue - normalizedNow;
        return durationSeconds >= maximumDuration.TotalSeconds
            ? DateTimeOffset.MaxValue
            : normalizedNow.AddSeconds(durationSeconds);
    }
}
