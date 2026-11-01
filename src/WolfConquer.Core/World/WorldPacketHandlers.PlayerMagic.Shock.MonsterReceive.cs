using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Prepared packet DATA is not a damage/death/reward capability. Publication
    // requires the actual registered Store phase and its one-use output claim.
    internal sealed class DragonhowlMonsterOutput
    {
        private readonly uint actorUid;
        private readonly LegacyMonsterInstance original;
        private readonly Monster family;
        private readonly LegacyMonsterInstance after;
        private readonly MonsterDamageResult expected;
        private readonly byte[][] frames;

        internal bool RequiresDeathContinuation => expected.Killed;

        private DragonhowlMonsterOutput(uint actorUid, LegacyMonsterInstance original, Monster family,
            LegacyMonsterInstance after, MonsterDamageResult expected, byte[][] frames)
        {
            this.actorUid = actorUid; this.original = original; this.family = family;
            this.after = after; this.expected = expected; this.frames = frames;
        }

        internal static bool TryPrepare(WorldSession actor, LegacyMonsterInstance original, Monster family,
            uint requestedDamage, out DragonhowlMonsterOutput? output)
        {
            output = null;
            if (actor is null || original is null || family is null || actor.Character.Uid is <= 0 or > uint.MaxValue ||
                original.Uid == 0 || original.MonsterId != family.Id || original.Hitpoints == 0 ||
                original.Hitpoints > original.MaxLife) return false;
            uint actorUid = checked((uint)actor.Character.Uid);
            uint damage = Math.Min(requestedDamage, original.Hitpoints);
            uint remaining = original.Hitpoints - damage;
            var result = new MonsterDamageResult(damage, remaining, Killed: remaining == 0,
                AlreadyDead: false, RequestedDamage: requestedDamage);
            var after = original with { Hitpoints = remaining };
            byte[][] frames;
            if (result.Killed)
            {
                frames = [new MsgAttackPacket(new MsgAttackQuery(actorUid, original.Uid, original.X, original.Y,
                    MsgAttackType.Death, Damage: 0, KilledMonster: 1, ResponseDamage: 0,
                    Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes()];
            }
            else frames = LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(original)
                ? LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(after).Select(row => row.ToFrameBytes()).ToArray()
                : [];
            output = new(actorUid, original, family, after, result, frames);
            return true;
        }

        internal async ValueTask<bool> PublishAsync(OwnedDragonhowlPreparation plan,
            WorldSessionStore.OwnedDragonhowlNonPlayer token, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrent(plan, token) || !plan.Store.TryBeginDragonhowlNonPlayerOutput(plan, token)) return false;
            foreach (var frame in frames)
            {
                // Source receive HP updates are self-only. Death is self first,
                // followed by peers around the monster, not around the caster.
                await SendCheckedAsync(plan, token, plan.Context.Connection, frame, cancellationToken).ConfigureAwait(false);
                if (!expected.Killed) continue;
                foreach (var peer in plan.Store.Snapshot())
                {
                    if (ReferenceEquals(peer.Connection, plan.Context.Connection) || peer.Session.Character.Uid == actorUid ||
                        !plan.Store.TryGet(peer.Connection, out var current) || !ReferenceEquals(current, peer.Session) ||
                        !CanSeeMonster(current.Character, original)) continue;
                    await SendCheckedAsync(plan, token, peer.Connection, frame, cancellationToken).ConfigureAwait(false);
                }
            }
            // Only the receive packet phase completed. The original death path
            // next requires drops, boss rewards and team XP through their own
            // real participants. The producer keeps lethal casts held until joined.
            return IsCurrent(plan, token);
        }

        private bool IsCurrent(OwnedDragonhowlPreparation plan, WorldSessionStore.OwnedDragonhowlNonPlayer token) =>
            plan.Store.TryReadDragonhowlMonsterPublication(plan, token, out var actor, out var actualOriginal,
                out var actualFamily, out var actualAfter, out var result) && actor.Character.Uid == actorUid &&
            actualOriginal == original && ReferenceEquals(actualFamily, family) && actualAfter == after && result == expected;

        private async ValueTask SendCheckedAsync(OwnedDragonhowlPreparation plan,
            WorldSessionStore.OwnedDragonhowlNonPlayer token, ConquerTcpConnection recipient,
            byte[] frame, CancellationToken cancellationToken)
        {
            if (!IsCurrent(plan, token)) throw new InvalidOperationException("ownedDragonhowlMonsterOutputOwnerChanged");
            await recipient.SendAsync(frame.ToArray(), cancellationToken).ConfigureAwait(false);
            if (!IsCurrent(plan, token)) throw new InvalidOperationException("ownedDragonhowlMonsterOutputOwnerChanged");
        }
    }

    private static async ValueTask<WorldSession> PublishLegacyShockMonsterReceiveAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyPlayerMonsterMagicTarget target,
        MonsterDamageResult result,
        CancellationToken cancellationToken)
    {
        if (!result.Killed)
        {
            if (LegacyMsgUpdatePacket.ShouldSendMonsterHitpointUpdates(target.Monster))
            {
                var damagedMonster = target.Monster with { Hitpoints = result.RemainingHitpoints };
                foreach (var update in LegacyMsgUpdatePacket.CreateMonsterHitpointUpdateSequence(damagedMonster))
                {
                    await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                }
            }

            return session;
        }

        var death = new MsgAttackPacket(new MsgAttackQuery(
            Uid: CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            OpponentUid: target.Monster.Uid,
            X: target.Monster.X,
            Y: target.Monster.Y,
            AttackType: MsgAttackType.Death,
            Damage: 0,
            KilledMonster: 1,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0));
        await SendMonsterCombatFrameToVisibleAsync(context, session, target.Monster, death, cancellationToken)
            .ConfigureAwait(false);
        await TrySendLegacyMonsterDeathDropAsync(
                context,
                session,
                target.Monster,
                target.MonsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        var updatedSession = await TryApplyLegacyBossDeathRewardsAsync(
                context,
                session,
                target.Monster,
                target.MonsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        context = context with { Session = updatedSession };
        await TryApplyLegacyMonsterTeamExperienceAsync(
                context,
                updatedSession,
                target.Monster,
                cancellationToken)
            .ConfigureAwait(false);
        return updatedSession;
    }
}
