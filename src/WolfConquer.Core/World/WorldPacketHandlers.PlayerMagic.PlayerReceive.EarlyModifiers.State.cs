using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private readonly record struct LegacyPlayerReceiveEarlyModifierResult(
        WorldActiveSession Target, CharacterSnapshot Attacker, WorldSession? AttackerSession,
        uint Damage, bool AttackerChanged, bool HawksAmbitionConsumed,
        uint? LightningShieldActivationLeft, uint? LightningShieldDrainLeft,
        uint LightningShieldSpellLevel, LegacyPlayerReceiveInfiniteMistResult InfiniteMistResult,
        LegacyPlayerMagicReceiveAbortReason AbortReason);

    private sealed partial class LegacyPlayerReceiveEarlyModifierExecution
    {
        private readonly WorldPacketHandlerContext context;
        private readonly long attackerUid;
        private readonly long targetUid;
        private readonly WorldSession? originalAttacker;
        private CharacterSnapshot unownedAttacker;

        public LegacyPlayerReceiveEarlyModifierExecution(WorldPacketHandlerContext context,
            WorldActiveSession target, CharacterSnapshot attacker, uint damage)
        {
            this.context = context;
            Target = target;
            attackerUid = attacker.Uid;
            targetUid = target.Session.Character.Uid;
            originalAttacker = context.Session?.Character.Uid == attackerUid ? context.Session : null;
            AttackerSession = originalAttacker;
            unownedAttacker = attacker;
            Damage = damage;
        }

        public WorldActiveSession Target { get; private set; }
        public WorldSession? AttackerSession { get; private set; }
        public CharacterSnapshot Attacker => AttackerSession?.Character ?? unownedAttacker;
        public uint Damage { get; set; }
        public bool HawksConsumed { get; set; }
        public uint? ActivationLeft { get; set; }
        public uint? DrainLeft { get; set; }
        public uint ShieldLevel { get; set; }
        public LegacyPlayerReceiveInfiniteMistResult InfiniteMist { get; set; }
        public LegacyPlayerMagicReceiveAbortReason AbortReason { get; private set; }

        public bool Refresh()
        {
            if (context.Sessions is not { } sessions) return true;
            if (!sessions.TryGet(context.Connection, out var attacker) ||
                attacker is null || attacker.Character.Uid != attackerUid)
            {
                AbortReason = LegacyPlayerMagicReceiveAbortReason.AttackerLost;
                return false;
            }
            AttackerSession = attacker;
            if (!sessions.TryGet(Target.Connection, out var target) ||
                target is null || target.Character.Uid != targetUid)
            {
                AbortReason = LegacyPlayerMagicReceiveAbortReason.TargetLost;
                return false;
            }
            Target = Target with { Session = target };
            return true;
        }

        public bool CommitAttacker(Func<CharacterSnapshot, CharacterSnapshot> mutation)
        {
            if (!Refresh()) return false;
            var character = mutation(Attacker);
            if (AttackerSession is { } session)
            {
                AttackerSession = session with { Character = character };
                context.Sessions?.Set(context.Connection, AttackerSession);
            }
            else unownedAttacker = character;
            return true;
        }

        public bool CommitTarget(Func<CharacterSnapshot, CharacterSnapshot> mutation)
        {
            if (!Refresh()) return false;
            Target = Target with { Session = Target.Session with { Character = mutation(Target.Session.Character) } };
            context.Sessions?.Set(Target.Connection, Target.Session);
            return true;
        }

        public LegacyPlayerReceiveEarlyModifierResult Complete() =>
            new(Target, Attacker, AttackerSession, Damage, !Equals(originalAttacker, AttackerSession),
                HawksConsumed, ActivationLeft, DrainLeft, ShieldLevel, InfiniteMist, AbortReason);
    }
}
