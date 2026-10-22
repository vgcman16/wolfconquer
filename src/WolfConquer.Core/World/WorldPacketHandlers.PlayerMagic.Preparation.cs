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
        CancellationToken cancellationToken,
        object? expectedNativeClientIdentity = null)
    {
        return TryPrepareLegacyPlayerMagicCastAsync(
            context,
            session,
            spell,
            ignoreAttackStamp: false,
            cancellationToken: cancellationToken,
            expectedNativeClientIdentity: expectedNativeClientIdentity);
    }

    private static async ValueTask<WorldSession?> TryPrepareLegacyPlayerMagicCastAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        bool ignoreAttackStamp,
        CancellationToken cancellationToken,
        object? expectedNativeClientIdentity = null)
    {
        if (expectedNativeClientIdentity is not null &&
            !TryRefreshLegacyPlayerMagicResourceSession(context, session.Character.Uid, ref session,
                expectedNativeClientIdentity)) return null;
        if (HasPreparedLegacyRootFirstFamilyInvocation(context, session, spell))
            return TryRefreshLegacyPlayerMagicResourceSession(context, session.Character.Uid, ref session,
                expectedNativeClientIdentity) ? session : null;
        var cadenceSession = ignoreAttackStamp
            ? session
            : await TryStartLegacyPlayerMagicAttackStampCadenceAsync(
                    context,
                    session,
                    spell,
                    expectedNativeClientIdentity)
                .ConfigureAwait(false);
        if (cadenceSession is null)
        {
            return null;
        }

        var resourceResult = await TrySpendLegacyPlayerMagicResourcesAsync(
                context,
                cadenceSession,
                spell,
                cancellationToken,
                expectedNativeClientIdentity)
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
                cancellationToken,
                expectedNativeClientIdentity)
            .ConfigureAwait(false);
    }

    private static async ValueTask<WorldSession?> TryStartLegacyPlayerMagicAttackStampCadenceAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        object? expectedNativeClientIdentity = null)
    {
        if (expectedNativeClientIdentity is not null &&
            !TryRefreshLegacyPlayerMagicResourceSession(context, session.Character.Uid, ref session,
                expectedNativeClientIdentity)) return null;
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

        if (!TryCommitLegacyPlayerMagicPreparation(context, ref session,
                owner => owner with { Character = WithLegacyPlayerAttackStamp(owner.Character, now) },
                expectedNativeClientIdentity)) return null;
        return session;
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
        CancellationToken cancellationToken,
        object? expectedNativeClientIdentity = null)
    {
        if (expectedNativeClientIdentity is not null &&
            !TryRefreshLegacyPlayerMagicResourceSession(context, session.Character.Uid, ref session,
                expectedNativeClientIdentity)) return null;
        if (HasPreparedLegacyRootFirstFamilyInvocation(context, session, spell))
            return TryRefreshLegacyPlayerMagicResourceSession(context, session.Character.Uid, ref session,
                expectedNativeClientIdentity) ? session : null;
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

        if (!TryCommitLegacyPlayerMagicPreparation(context, ref session,
                owner => owner with { MagicCooldowns = cooldowns }, expectedNativeClientIdentity)) return null;
        await context.Connection.SendAsync(
                LegacyMagicColdTimePacket.FromStartedCooldown(spell).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        if (expectedNativeClientIdentity is not null &&
            !TryRefreshLegacyPlayerMagicResourceSession(context, session.Character.Uid, ref session,
                expectedNativeClientIdentity)) return null;
        return session;
    }

    private static async ValueTask<LegacyPlayerMagicResourcePreparationResult> TrySpendLegacyPlayerMagicResourcesAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken,
        object? expectedNativeClientIdentity = null)
    {
        if (expectedNativeClientIdentity is not null &&
            !TryRefreshLegacyPlayerMagicResourceSession(context, session.Character.Uid, ref session,
                expectedNativeClientIdentity)) return new(Session: null, GraceOfHeaven: false);
        if (HasPreparedLegacyRootFirstFamilyInvocation(context, session, spell))
            return new LegacyPlayerMagicResourcePreparationResult(
                TryRefreshLegacyPlayerMagicResourceSession(context, session.Character.Uid, ref session,
                    expectedNativeClientIdentity) ? session : null,
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
            if (!TryCommitLegacyPlayerMagicPreparation(context, ref session,
                    owner => owner with { Character = WithCharacterAttribute(owner.Character, "Stamina", remainingStamina) },
                    expectedNativeClientIdentity)) return new(Session: null, GraceOfHeaven: false);
            updatedCharacter = session.Character;
            await context.Connection.SendAsync(
                    LegacyMsgUpdatePacket
                        .CreateLegacyRuntimeStaminaTickUpdate(
                            CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid)),
                            remainingStamina)
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!TryRefreshLegacyPlayerMagicResourceSession(context, acceptedAttackerUid, ref session,
                    expectedNativeClientIdentity))
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
            if (!TryCommitLegacyPlayerMagicPreparation(context, ref session,
                    owner => owner with { Character = owner.Character with { MinMana = remainingMana } },
                    expectedNativeClientIdentity)) return new(Session: null, GraceOfHeaven: false);
            updatedCharacter = session.Character;
            await SendScalarUpdateAsync(
                    context,
                    updatedCharacter,
                    LegacyMsgUpdateDataType.Mana,
                    CheckedUInt32(remainingMana, nameof(updatedCharacter.MinMana)),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!TryRefreshLegacyPlayerMagicResourceSession(context, acceptedAttackerUid, ref session,
                    expectedNativeClientIdentity))
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
