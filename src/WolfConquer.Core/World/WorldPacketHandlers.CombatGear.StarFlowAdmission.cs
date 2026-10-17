using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<(WorldSession Session, bool Admitted, LegacyPlayerMagicReceiveAbortReason AbortReason)>
        TryAdmitLegacyStoneCrackerStarFlowSpellAsync(
            WorldPacketHandlerContext context, WorldSession session, LegacyMagicRuntimeSpell spell,
            CancellationToken cancellationToken)
    {
        var uid = session.Character.Uid;
        (WorldSession, bool, LegacyPlayerMagicReceiveAbortReason) Result(
            bool admitted, LegacyPlayerMagicReceiveAbortReason reason = LegacyPlayerMagicReceiveAbortReason.None) =>
            (session, admitted, reason);
        bool Refresh() => TryRefreshLegacyPlayerMagicResourceSession(context, uid, ref session);
        if (!Refresh()) return Result(false, LegacyPlayerMagicReceiveAbortReason.AttackerLost);

        // MyCombatGear's10950 query has OpponentUID0. Source IsInSpellRange
        // cannot resolve a valid view object0, so Attack/DirectAttack reject BEFORE costs.
        if (spell.Sort is LegacyMagicSort.Attack or LegacyMagicSort.DirectAttack) return Result(false);
        if (session.Character.MapId != 1_039)
        {
            var useStamina = (uint)spell.UseStamina +
                (HasLegacyStatusFlag(session.Character, LegacyScurvyBombFlag) ? 6U : 0U);
            var stamina = ReadLegacyRuntimeStamina(session.Character);
            if (stamina < useStamina) return Result(false);
            if (useStamina > 0)
            {
                var remaining = stamina - useStamina;
                session = session with { Character = WithCharacterAttribute(session.Character, "Stamina", remaining) };
                context.Sessions?.Set(context.Connection, session);
                await context.Connection.SendAsync(
                        LegacyMsgUpdatePacket.CreateLegacyRuntimeStaminaTickUpdate(
                            CheckedUInt32(uid, nameof(uid)), remaining).ToFrameBytes(), cancellationToken)
                    .ConfigureAwait(false);
                if (!Refresh()) return Result(false, LegacyPlayerMagicReceiveAbortReason.AttackerLost);
            }

            // Rune lookup is AFTER the stamina setter's publication and refresh.
            var cost = LegacyMagicResourceCostCalculator.Calculate(spell, session.PlayerItems,
                context.WorldData, session.Character.Attributes, session.AlternateEquipmentActive, session.Character.Class);
            if (session.Character.MinMana < cost.UseMana) return Result(false); // Stamina is not rolled back.
            if (cost.UseMana > 0)
            {
                session = session with { Character = session.Character with
                    { MinMana = session.Character.MinMana - cost.UseMana } };
                context.Sessions?.Set(context.Connection, session);
                // Player.Mana2633–2650 emits its owner update after FullLoading;
                // runtime WorldSession is that modeled post-loading boundary.
                await SendScalarUpdateAsync(context, session.Character, LegacyMsgUpdateDataType.Mana,
                        CheckedUInt32(session.Character.MinMana, nameof(session.Character.MinMana)), cancellationToken)
                    .ConfigureAwait(false);
                if (!Refresh()) return Result(false, LegacyPlayerMagicReceiveAbortReason.AttackerLost);
            }
        }

        // Source checks coldtime AFTER both costs. No Grace/HeavensWrath bypass,
        // no ColdTime packet, and map1039 skips costs only.10950 has no sigil adjustment.
        if (LegacyMagicRuntime.UsesLegacyCooldown(spell) &&
            !HasLegacyStatusFlag(session.Character, LegacyCannonBarrageFlag) &&
            !HasLegacyStatusFlag(session.Character, LegacyBlackbeardsRageFlag))
        {
            var now = (context.TimeProvider ?? TimeProvider.System).GetUtcNow();
            if (session.MagicCooldowns is { } existing &&
                existing.TryGetValue(spell.Id, out var readyAt) && readyAt > now) return Result(false);
            var cooldowns = session.MagicCooldowns is null
                ? new Dictionary<ushort, DateTimeOffset>()
                : new Dictionary<ushort, DateTimeOffset>(session.MagicCooldowns);
            cooldowns[spell.Id] = now.AddMilliseconds(spell.ColdTime);
            session = session with { MagicCooldowns = cooldowns };
            context.Sessions?.Set(context.Connection, session);
        }
        // CombatGearAttack20 uses source HP-only Alive AFTER CanUseSpell.
        return Result(session.Character.MinHitPoints > 0);
    }
}
