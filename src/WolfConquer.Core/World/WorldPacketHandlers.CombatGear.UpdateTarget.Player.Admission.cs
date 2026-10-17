using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Only the two nested UpdateTarget DAMAGE proc owners are admitted here.
    // Source CanUseSpell resource order is not the generic Grace/ColdTime wire helper.
    private static async ValueTask<(WorldSession DefenderSession, WorldSession AttackerSession,
        bool Admitted, LegacyPlayerMagicReceiveAbortReason AbortReason)>
        TryAdmitLegacyCombatGearNestedPlayerDamageProcAsync(
            WorldPacketHandlerContext defenderContext,
            ConquerTcpConnection attackerConnection,
            WorldSession attackerSession,
            WorldSession defenderSession,
            LegacyMagicRuntimeSpell spell,
            CancellationToken cancellationToken)
    {
        var incomingContext = defenderContext with
            { Connection = attackerConnection, Session = attackerSession };
        var target = new WorldActiveSession(defenderContext.Connection, defenderSession);
        var request = new LegacyPlayerReceiveCombatGearRequest(
            attackerSession, target, attackerSession.Character.Uid, defenderSession.Character.Uid);
        LegacyPlayerMagicReceiveAbortReason Refresh()
        {
            var reason = RefreshLegacyPlayerReceiveCombatGearParticipants(
                incomingContext, request, ref attackerSession, ref target);
            defenderSession = target.Session;
            return reason;
        }
        (WorldSession, WorldSession, bool, LegacyPlayerMagicReceiveAbortReason) Result(
            bool admitted, LegacyPlayerMagicReceiveAbortReason reason = LegacyPlayerMagicReceiveAbortReason.None) =>
            (defenderSession, attackerSession, admitted, reason);
        var reason = Refresh();
        if (reason != LegacyPlayerMagicReceiveAbortReason.None) return Result(false, reason);
        if (spell.Id is not (LegacyCombatGearMagneticLightSpellId or LegacyCombatGearAblazeBladeSpellId) ||
            defenderContext.WorldData is not { } worldData ||
            !LegacyMagicRuntime.TryResolveLearnedSpell(defenderSession.PlayerSpells, worldData,
                spell.Id, out _, out _))
            return Result(false);

        // CanUseSpell: the direct Attack/DirectAttack view-range check precedes
        // resources. The caller already supplies its original selected player.
        if ((spell.Sort is LegacyMagicSort.Attack or LegacyMagicSort.DirectAttack) &&
            LegacyGetDistance(defenderSession.Character.X, defenderSession.Character.Y,
                attackerSession.Character.X, attackerSession.Character.Y) > spell.Range)
            return Result(false);

        if (defenderSession.Character.MapId != 1_039)
        {
            // Source CanUseSpell uses ScurvyBomb +6, NOT the generic attack's
            // independent DragonPierce surcharge. MPMaster owns only this mana cost.
            var useStamina = (uint)spell.UseStamina +
                (HasLegacyStatusFlag(defenderSession.Character, LegacyScurvyBombFlag) ? 6U : 0U);
            var stamina = ReadLegacyRuntimeStamina(defenderSession.Character);
            if (stamina < useStamina) return Result(false);
            if (useStamina > 0)
            {
                var remaining = stamina - useStamina;
                defenderSession = defenderSession with
                {
                    Character = WithCharacterAttribute(defenderSession.Character, "Stamina", remaining),
                };
                target = target with { Session = defenderSession };
                defenderContext.Sessions?.Set(defenderContext.Connection, defenderSession);
                await defenderContext.Connection.SendAsync(
                        LegacyMsgUpdatePacket.CreateLegacyRuntimeStaminaTickUpdate(
                            CheckedUInt32(defenderSession.Character.Uid, nameof(defenderSession.Character.Uid)),
                            remaining).ToFrameBytes(), cancellationToken)
                    .ConfigureAwait(false);
                reason = Refresh();
                if (reason != LegacyPlayerMagicReceiveAbortReason.None) return Result(false, reason);
            }

            var cost = LegacyMagicResourceCostCalculator.Calculate(spell, defenderSession.PlayerItems,
                worldData, defenderSession.Character.Attributes, defenderSession.AlternateEquipmentActive,
                defenderSession.Character.Class);
            // Stamina is deliberately not rolled back when mana/coldtime rejects.
            // The source Mana property write is SILENT, with no Grace bypass.
            if (defenderSession.Character.MinMana < cost.UseMana) return Result(false);
            if (cost.UseMana > 0)
            {
                defenderSession = defenderSession with
                {
                    Character = defenderSession.Character with
                        { MinMana = defenderSession.Character.MinMana - cost.UseMana },
                };
                target = target with { Session = defenderSession };
                defenderContext.Sessions?.Set(defenderContext.Connection, defenderSession);
            }
        }

        // Map1039 skips only resources, never coldtime. Source excludes these
        // two flags, not HeavensWrath, and sends NO ColdTime packet here.
        if (LegacyMagicRuntime.UsesLegacyCooldown(spell) &&
            !HasLegacyStatusFlag(defenderSession.Character, LegacyCannonBarrageFlag) &&
            !HasLegacyStatusFlag(defenderSession.Character, LegacyBlackbeardsRageFlag))
        {
            var now = (defenderContext.TimeProvider ?? TimeProvider.System).GetUtcNow();
            if (defenderSession.MagicCooldowns is { } existing &&
                existing.TryGetValue(spell.Id, out var readyAt) && readyAt > now)
                return Result(false);
            var cooldowns = defenderSession.MagicCooldowns is null
                ? new Dictionary<ushort, DateTimeOffset>()
                : new Dictionary<ushort, DateTimeOffset>(defenderSession.MagicCooldowns);
            cooldowns[spell.Id] = now.AddMilliseconds(spell.ColdTime);
            defenderSession = defenderSession with { MagicCooldowns = cooldowns };
            target = target with { Session = defenderSession };
            defenderContext.Sessions?.Set(defenderContext.Connection, defenderSession);
        }

        // CombatGearAttack.Execute20 checks alive AFTER CanUseSpell. Ablaze
        // routes through Attack.Execute27, which has no identical initial check.
        return Result(spell.Id != LegacyCombatGearMagneticLightSpellId ||
            defenderSession.Character.MinHitPoints > 0);
    }
}
