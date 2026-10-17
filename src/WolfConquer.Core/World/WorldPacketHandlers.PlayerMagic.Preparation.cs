using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyGraceOfHeavenSpellId = 12_560;
    private const int LegacyHeavensWrathFlag = 0xEC;

    private static ValueTask<WorldSession?> TryPrepareLegacyPlayerMagicCastAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        return TryPrepareLegacyPlayerMagicCastAsync(
            context,
            session,
            spell,
            ignoreAttackStamp: false,
            cancellationToken: cancellationToken);
    }

    private static async ValueTask<WorldSession?> TryPrepareLegacyPlayerMagicCastAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        bool ignoreAttackStamp,
        CancellationToken cancellationToken)
    {
        if (HasPreparedLegacyRootFirstFamilyInvocation(context, session, spell))
            return TryRefreshLegacyPlayerMagicResourceSession(context, session.Character.Uid, ref session) ? session : null;
        var cadenceSession = ignoreAttackStamp
            ? session
            : await TryStartLegacyPlayerMagicAttackStampCadenceAsync(
                    context,
                    session,
                    spell)
                .ConfigureAwait(false);
        if (cadenceSession is null)
        {
            return null;
        }

        var resourceResult = await TrySpendLegacyPlayerMagicResourcesAsync(
                context,
                cadenceSession,
                spell,
                cancellationToken)
            .ConfigureAwait(false);
        if (resourceResult.Session is null)
        {
            return null;
        }

        return await TryStartLegacyPlayerMagicCooldownAsync(
                context,
                resourceResult.Session,
                spell,
                resourceResult.GraceOfHeaven,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<WorldSession?> TryStartLegacyPlayerMagicAttackStampCadenceAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell)
    {
        if (!TryGetLegacyPlayerMagicAttackStampCadence(spell, out var cadence))
        {
            return session;
        }

        var now = DateTimeOffset.UtcNow;
        if (TryReadLegacyPlayerAttackStamp(session.Character, out var attackStamp))
        {
            var readyAtUtc = attackStamp.ToUniversalTime() + cadence;
            var remainingMilliseconds = LegacyMagicRuntime.GetLegacyCooldownRemainingMilliseconds(readyAtUtc, now);
            if (remainingMilliseconds > 0)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped legacy player magic " +
                        $"{FormatSession(context)} spellId={spell.Id} attackStampRemainingMs={remainingMilliseconds}.")
                    .ConfigureAwait(false);
                return null;
            }
        }

        var updatedSession = session with
        {
            Character = WithLegacyPlayerAttackStamp(session.Character, now),
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        return updatedSession;
    }

    private static bool TryGetLegacyPlayerMagicAttackStampCadence(
        LegacyMagicRuntimeSpell spell,
        out TimeSpan cadence)
    {
        if (spell.Id == 1_045)
        {
            cadence = TimeSpan.FromMilliseconds(500);
            return true;
        }

        if (spell.Id == 1_115)
        {
            cadence = TimeSpan.FromMilliseconds(spell.CoolDown == 0 ? 500 : spell.CoolDown);
            return true;
        }

        if (spell.Sort == LegacyMagicSort.WhirlwindKick)
        {
            var cooldownMilliseconds = spell.Id == 10_415
                ? 800
                : spell.CoolDown == 0
                    ? 500
                    : spell.CoolDown;
            if (cooldownMilliseconds is > 1_000 and < 3_000)
            {
                cooldownMilliseconds = 2_500;
            }

            cadence = TimeSpan.FromMilliseconds(cooldownMilliseconds);
            return true;
        }

        if (LegacyMagicSortCapabilities.IsSupportedPounceRuntimeSpell(spell.Sort, spell.Id))
        {
            cadence = TimeSpan.FromMilliseconds(spell.CoolDown);
            return true;
        }

        if (LegacyMagicSortCapabilities.IsSupportedThunderRampageRuntimeSpell(spell.Sort, spell.Id))
        {
            cadence = TimeSpan.FromMilliseconds(500);
            return true;
        }

        if (LegacyMagicSortCapabilities.IsSupportedRemoveBuffersRuntimeSpell(spell.Sort, spell.Id))
        {
            cadence = TimeSpan.FromMilliseconds(500);
            return true;
        }

        if (LegacyMagicSortCapabilities.IsSupportedPerimeterSpecialRuntimeSpell(spell.Sort, spell.Id))
        {
            cadence = TimeSpan.FromMilliseconds(500);
            return true;
        }

        cadence = default;
        return false;
    }

    private static async ValueTask<WorldSession?> TryStartLegacyPlayerMagicCooldownAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        bool graceOfHeaven,
        CancellationToken cancellationToken)
    {
        if (HasPreparedLegacyRootFirstFamilyInvocation(context, session, spell))
            return TryRefreshLegacyPlayerMagicResourceSession(context, session.Character.Uid, ref session) ? session : null;
        if (graceOfHeaven && LegacyMagicRuntime.UsesLegacyCooldown(spell))
        {
            return session;
        }

        if (LegacyMagicRuntime.UsesLegacyCooldown(spell) &&
            (HasLegacyStatusFlag(session.Character, LegacyCannonBarrageFlag) ||
                HasLegacyStatusFlag(session.Character, LegacyBlackbeardsRageFlag) ||
                HasLegacyStatusFlag(session.Character, LegacyHeavensWrathFlag)))
        {
            return session;
        }

        DateTimeOffset? currentReadyAtUtc = null;
        if (session.MagicCooldowns is not null &&
            session.MagicCooldowns.TryGetValue(spell.Id, out var readyAtUtc))
        {
            currentReadyAtUtc = readyAtUtc;
        }

        var cooldown = LegacyMagicRuntime.TryStartLegacyCooldown(
            spell,
            DateTimeOffset.UtcNow,
            currentReadyAtUtc);

        if (!cooldown.Ready)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy player magic " +
                    $"{FormatSession(context)} spellId={spell.Id} cooldownRemainingMs={cooldown.RemainingMilliseconds}.")
                .ConfigureAwait(false);
            return null;
        }

        if (!LegacyMagicRuntime.UsesLegacyCooldown(spell))
        {
            return session;
        }

        var cooldowns = session.MagicCooldowns is null
            ? new Dictionary<ushort, DateTimeOffset>()
            : new Dictionary<ushort, DateTimeOffset>(session.MagicCooldowns);
        cooldowns[spell.Id] = cooldown.ReadyAtUtc;

        var updatedSession = session with { MagicCooldowns = cooldowns };
        context.Sessions?.Set(context.Connection, updatedSession);
        await context.Connection.SendAsync(
                LegacyMagicColdTimePacket.FromStartedCooldown(spell).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<LegacyPlayerMagicResourcePreparationResult> TrySpendLegacyPlayerMagicResourcesAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (HasPreparedLegacyRootFirstFamilyInvocation(context, session, spell))
            return new LegacyPlayerMagicResourcePreparationResult(
                TryRefreshLegacyPlayerMagicResourceSession(context, session.Character.Uid, ref session) ? session : null,
                GraceOfHeaven: false);
        if (session.Character.MapId == 1_039)
        {
            return new LegacyPlayerMagicResourcePreparationResult(session, GraceOfHeaven: false);
        }

        var cost = LegacyMagicResourceCostCalculator.Calculate(
            spell,
            session.PlayerItems,
            context.WorldData,
            session.Character.Attributes,
            session.AlternateEquipmentActive,
            session.Character.Class);

        var stamina = ReadLegacyRuntimeStamina(session.Character);
        if (stamina < cost.UseStamina)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy player magic " +
                    $"{FormatSession(context)} spellId={spell.Id} stamina={stamina} requiredStamina={cost.UseStamina}.")
                .ConfigureAwait(false);
            return new LegacyPlayerMagicResourcePreparationResult(Session: null, GraceOfHeaven: false);
        }

        var acceptedAttackerUid = session.Character.Uid;
        var updatedCharacter = session.Character;
        if (cost.UseStamina > 0)
        {
            var remainingStamina = stamina - cost.UseStamina;
            updatedCharacter = WithCharacterAttribute(updatedCharacter, "Stamina", remainingStamina);
            session = session with { Character = updatedCharacter };
            context.Sessions?.Set(context.Connection, session);
            await context.Connection.SendAsync(
                    LegacyMsgUpdatePacket
                        .CreateLegacyRuntimeStaminaTickUpdate(
                            CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid)),
                            remainingStamina)
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!TryRefreshLegacyPlayerMagicResourceSession(context, acceptedAttackerUid, ref session))
            {
                return new LegacyPlayerMagicResourcePreparationResult(Session: null, GraceOfHeaven: false);
            }

            updatedCharacter = session.Character;
            context = context with { Session = session };
        }

        if (updatedCharacter.MinMana < cost.UseMana)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy player magic " +
                    $"{FormatSession(context)} spellId={spell.Id} mana={updatedCharacter.MinMana} requiredMana={cost.UseMana}.")
                .ConfigureAwait(false);
            return new LegacyPlayerMagicResourcePreparationResult(Session: null, GraceOfHeaven: false);
        }

        var graceOfHeaven = !LegacyMagicResourceCostCalculator.IsLegacyResourceFreeSpell(spell.Id) &&
            HasLegacyGraceOfHeavenResourceBypass(session);

        if (cost.UseMana > 0 && !graceOfHeaven)
        {
            var remainingMana = checked(updatedCharacter.MinMana - cost.UseMana);
            updatedCharacter = updatedCharacter with
            {
                MinMana = remainingMana,
            };
            session = session with { Character = updatedCharacter };
            context.Sessions?.Set(context.Connection, session);
            await SendScalarUpdateAsync(
                    context,
                    updatedCharacter,
                    LegacyMsgUpdateDataType.Mana,
                    CheckedUInt32(remainingMana, nameof(updatedCharacter.MinMana)),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!TryRefreshLegacyPlayerMagicResourceSession(context, acceptedAttackerUid, ref session))
            {
                return new LegacyPlayerMagicResourcePreparationResult(Session: null, GraceOfHeaven: false);
            }
        }

        return new LegacyPlayerMagicResourcePreparationResult(session, graceOfHeaven);
    }

    private static bool HasLegacyGraceOfHeavenResourceBypass(WorldSession session)
    {
        return session.PlayerSpells.ContainsSpell(LegacyGraceOfHeavenSpellId) &&
            HasLegacyEquippedMonkEpicWeapon(session.PlayerItems, position: 4) &&
            HasLegacyEquippedMonkEpicWeapon(session.PlayerItems, position: 5);
    }

    private static bool HasLegacyEquippedMonkEpicWeapon(PlayerItemSnapshot playerItems, ushort position)
    {
        return playerItems.EquipmentItems.Any(item =>
            item.Position == position &&
            item.ItemId is >= 622_000 and <= 622_439);
    }

    private readonly record struct LegacyPlayerMagicResourcePreparationResult(
        WorldSession? Session,
        bool GraceOfHeaven);
}
