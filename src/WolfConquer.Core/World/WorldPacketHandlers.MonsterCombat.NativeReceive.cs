using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private readonly record struct LegacyMonsterNativeReceiveResult(
        MonsterDamageResult Damage, WorldSession Session, bool NativeDeadHandled);

    // Only explicitly converted callers opt in. Unconverted synchronous callers are unchanged.
    private static async ValueTask<LegacyMonsterNativeReceiveResult> ApplyLegacyMonsterNativeReceiveAsync(
        WorldPacketHandlerContext context, WorldSession publicationSession, WorldSession rewardSession,
        LegacyMonsterInstance monster, Monster family, uint requestedDamage, DateTimeOffset now,
        CancellationToken cancellationToken, Func<double, bool>? rate = null,
        Func<int, int, int>? next = null)
    {
        var states = context.MonsterStates ??
            throw new InvalidOperationException("The receive phase requires the real monster state owner.");
        if (monster.IsFloor || family.Id != 8_418 || rewardSession.Character.MapId != 10_342)
        {
            var ordinary = ApplyLegacyPlayerMonsterDamage(
                states, monster, requestedDamage, now, rewardSession);
            return new(ordinary, rewardSession, NativeDeadHandled: false);
        }

        var name = !string.IsNullOrWhiteSpace(rewardSession.Character.Name)
            ? rewardSession.Character.Name : rewardSession.Username;
        uint? uid = string.IsNullOrWhiteSpace(name) ? null :
            CheckedUInt32(rewardSession.Character.Uid, nameof(rewardSession.Character.Uid));
        var damage = states.ApplyLegacyFamily8418ReceiveDamage(
            monster, requestedDamage, now, uid, name);
        if (damage.AlreadyDead || !damage.Killed)
            return new(damage, rewardSession, NativeDeadHandled: false);

        await PublishLegacyFamily8418DeathPrefixAsync(
            context, rewardSession, monster, cancellationToken).ConfigureAwait(false);
        var death = new MsgAttackPacket(new MsgAttackQuery(
            CheckedUInt32(rewardSession.Character.Uid, nameof(rewardSession.Character.Uid)),
            monster.Uid, monster.X, monster.Y, MsgAttackType.Death,
            Damage: 0, KilledMonster: 1, ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0));
        rewardSession = IncrementLegacyFamily8418Kills(context, rewardSession);
        // A live Pool receiver is read at EACH Rate and switch draw. No saved global receiver.
        var reward = SelectLegacyFamily8418RandomReward(
            rate ?? CheckLegacyFamily8418NativeRate,
            next ?? ((min, max) => LegacyNativePoolRandom.GetRandom.Next(min, max)));
        if (reward.Kind == LegacyFamily8418RewardKind.StopNativeDeath)
            return new(damage, rewardSession, NativeDeadHandled: true);

        rewardSession = await TryApplyLegacyFamily8418Map10342MonsterDeathRewardAsync(
            context, rewardSession, monster, family, cancellationToken, reward).ConfigureAwait(false);
        rewardSession = await TryApplyLegacyMonsterDeathMapSideEffectsAsync(
            context with { Session = rewardSession }, rewardSession, monster, family,
            cancellationToken, family8418RewardAlreadyApplied: true).ConfigureAwait(false);

        await SendMonsterCombatFrameToVisibleAsync(
            context with { Session = rewardSession }, publicationSession, monster, death,
            cancellationToken).ConfigureAwait(false);
        states.CompleteLegacyMonsterDeathRemoval(monster);
        // Keep existing economy amounts/policy; only the converted native death phase is reordered.
        rewardSession = await TryApplyLegacyBossDeathRewardsAsync(
            context with { Session = rewardSession }, rewardSession, monster, family,
            cancellationToken, mapSideEffectsAlreadyApplied: true).ConfigureAwait(false);
        await TrySendLegacyMonsterDeathDropAsync(
            context with { Session = rewardSession }, publicationSession, monster, family,
            cancellationToken).ConfigureAwait(false);
        return new(damage, rewardSession, NativeDeadHandled: true);
    }

    // Literal MrRayzo.Role.Core.Rate(double) + Rate(int,int), not NextDouble percentage.
    private static bool CheckLegacyFamily8418NativeRate(double percent)
    {
        ushort num = 0;
        if (percent == 0.0) return false;
        while ((int)percent > 0)
        {
            num++;
            percent /= 10.0;
            if (num > 300)
            {
                Console.WriteLine("Problem While in Kernel=======================");
                return true;
            }
        }
        int num2 = 1;
        percent = Math.Round(percent, 4);
        num = 0;
        while (percent != Math.Ceiling(percent))
        {
            percent *= 10.0;
            num2 *= 10;
            percent = Math.Round(percent, 4);
            num++;
            if (num > 300)
            {
                Console.WriteLine("Problem While in Kernel 2  ===============");
                return true;
            }
        }
        int value = (int)percent;
        int discriminant = num2;
        int draw = LegacyNativePoolRandom.GetRandom.Next() % discriminant;
        return value > draw;
    }
}
