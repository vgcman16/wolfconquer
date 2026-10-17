using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask TrySendLegacyPlayerReceiveEagleEyeCountdownAsync(
        WorldPacketHandlerContext context,
        WorldActiveSession target,
        CancellationToken cancellationToken)
    {
        var targetSession = target.Session;
        if (context.WorldData is not { } worldData ||
            !IsLegacyEagleEyeCountDownActive(targetSession.Character) ||
            !LegacyMagicRuntime.TryResolveLearnedSpell(
                targetSession.PlayerSpells,
                worldData,
                LegacyEagleEyeCountdownSpellId,
                out var playerSpell,
                out _) ||
            !TryReadLegacyMagicByteField(
                worldData,
                LegacyEagleEyeCountdownSpellId,
                playerSpell.Level,
                LegacyMagicRateField,
                out var rate) ||
            !CheckLegacyEagleEyeResponseRateRoll(targetSession.Character, rate))
        {
            return;
        }

        var targetUid = CheckedUInt32(targetSession.Character.Uid, nameof(targetSession.Character.Uid));
        var animation = new LegacySpellAnimationPacket(
            targetUid,
            AttackedUid: targetUid,
            X: 0,
            Y: 0,
            LegacyEagleEyeCountdownSpellId,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            NextSpell: 0,
            LevelHu: playerSpell.UseSpellSoul,
            SpellEffect: 0,
            [new LegacySpellAnimationTarget(
                targetUid,
                LegacyEagleEyeCountdownSpellId,
                Hit: false,
                MsgAttackEffect.None)]);
        await target.Connection.SendAsync(animation.ToFrameBytes(), cancellationToken).ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer sent legacy player EagleEye countdown response " +
                $"{FormatSession(context with { Session = targetSession })} spellId={LegacyEagleEyeCountdownSpellId} " +
                $"rate={rate}; source=ReceiveAttack.Player.Execute EagleEyeCountDown.")
            .ConfigureAwait(false);
    }
}
