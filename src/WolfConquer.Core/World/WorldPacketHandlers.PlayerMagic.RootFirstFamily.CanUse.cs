using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession?> TryPrepareLegacyRootFirstFamilyCanUseAsync(
        WorldPacketHandlerContext context, WorldSession session, LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken,
        object? expectedNativeClientIdentity = null)
    {
        var uid = session.Character.Uid;
        bool Refresh() => TryRefreshLegacyPlayerMagicResourceSession(context, uid, ref session, expectedNativeClientIdentity);
        if (!Refresh()) return null;
        if (session.Character.MapId != 1_039)
        {
            var useStamina = (uint)spell.UseStamina + (HasLegacyStatusFlag(session.Character, LegacyScurvyBombFlag) ? 6U : 0U);
            var stamina = ReadLegacyRuntimeStamina(session.Character);
            if (stamina < useStamina)
                return await RejectLegacyRootFirstFamilyWithDiagnosticAsync(
                        context, session, spell, $"stamina={stamina} requiredStamina={useStamina}").ConfigureAwait(false);
            if (useStamina > 0)
            {
                var remaining = stamina - useStamina;
                if (!TryCommitLegacyPlayerMagicPreparation(context, ref session,
                        owner => owner with { Character = WithCharacterAttribute(owner.Character, "Stamina", remaining) },
                        expectedNativeClientIdentity)) return null;
                await context.Connection.SendAsync(LegacyMsgUpdatePacket.CreateLegacyRuntimeStaminaTickUpdate(
                        CheckedUInt32(uid, nameof(uid)), remaining).ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                if (!Refresh()) return null;
            }
            // Native lookup happens AFTER the stamina publication, on the CURRENT rune/equipment state.
            var cost = LegacyMagicResourceCostCalculator.Calculate(spell, session.PlayerItems,
                context.WorldData, session.Character.Attributes, session.AlternateEquipmentActive, session.Character.Class);
            if (session.Character.MinMana < cost.UseMana)
                return await RejectLegacyRootFirstFamilyWithDiagnosticAsync(
                        context, session, spell, $"mana={session.Character.MinMana} requiredMana={cost.UseMana}").ConfigureAwait(false);
            if (cost.UseMana > 0)
            {
                var remaining = session.Character.MinMana - cost.UseMana;
                if (!TryCommitLegacyPlayerMagicPreparation(context, ref session,
                        owner => owner with { Character = owner.Character with { MinMana = remaining } },
                        expectedNativeClientIdentity)) return null;
                await SendScalarUpdateAsync(context, session.Character, LegacyMsgUpdateDataType.Mana,
                        CheckedUInt32(session.Character.MinMana, nameof(session.Character.MinMana)), cancellationToken).ConfigureAwait(false);
                if (!Refresh()) return null;
            }
        }
        // Native CanUse has no Grace/HeavensWrath bypass and emits no ColdTime packet.
        if (LegacyMagicRuntime.UsesLegacyCooldown(spell) &&
            !HasLegacyStatusFlag(session.Character, LegacyCannonBarrageFlag) &&
            !HasLegacyStatusFlag(session.Character, LegacyBlackbeardsRageFlag))
        {
            var provider = context.TimeProvider ?? TimeProvider.System;
            var now = provider.GetUtcNow();
            if (session.MagicCooldowns is { } existing && existing.TryGetValue(spell.Id, out var readyAt) &&
                LegacyArrowBladesFlagRegistration.LocalTime(provider, readyAt) >
                    LegacyArrowBladesFlagRegistration.LocalTime(provider, now))
                return await RejectLegacyRootFirstFamilyWithDiagnosticAsync(
                        context, session, spell,
                        $"cooldownRemainingMs={LegacyMagicRuntime.GetLegacyCooldownRemainingMilliseconds(readyAt, now)}").ConfigureAwait(false);
            var cooldowns = session.MagicCooldowns is null ? new Dictionary<ushort, DateTimeOffset>() :
                new Dictionary<ushort, DateTimeOffset>(session.MagicCooldowns);
            var duration = unchecked(spell.ColdTime - ReadLegacyRootColdSigilPower(context, session, spell.Id));
            var localDeadline = LegacyArrowBladesFlagRegistration.LocalTime(provider, now).AddMilliseconds(duration);
            cooldowns[spell.Id] = new DateTimeOffset(localDeadline, provider.LocalTimeZone.GetUtcOffset(localDeadline));
            if (!TryCommitLegacyPlayerMagicPreparation(context, ref session,
                    owner => owner with { MagicCooldowns = cooldowns }, expectedNativeClientIdentity)) return null;
            if (!Refresh()) return null;
        }
        return session; // No HP/Alive check here; it belongs to the native handler where present.
    }

    private static int ReadLegacyRootColdSigilPower(WorldPacketHandlerContext context, WorldSession session, ushort spellId)
    {
        var type = spellId switch { 16_520 => 102, 16_540 => 105, 16_460 => 304, 16_500 => 402, _ => 0 };
        if (type == 0 || !IsLegacyNinjaClass(session.Character.Class) || context.WorldData is not { } worldData ||
            !session.Character.Attributes.TryGetValue("MsgNinja", out var raw) ||
            !TryReadLegacyMsgNinjaEquippedItemLevel(raw, type, session.AlternateEquipmentActive, out var level) ||
            !worldData.GouyuTypesByType.TryGetValue(type, out var rows)) return 0;
        foreach (var row in rows)
            if (row.Level == level && row.Values.Count > 5) return row.Values[5];
        return 0;
    }
}
