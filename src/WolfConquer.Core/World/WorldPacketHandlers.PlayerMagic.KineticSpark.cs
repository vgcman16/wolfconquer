using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyKineticSparkSpellId = 11_590;
    private const int LegacyKineticSparkFlag = 0x93;
    private const string RuntimeLegacyKineticSparkExpiresAtUtcAttribute = "LegacyKineticSparkExpiresAtUtc";
    private static readonly TimeSpan LegacyKineticSparkDuration = TimeSpan.FromSeconds(2_592_000);

    private static async ValueTask<bool> HandleLegacyPlayerKineticSparkMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.KineticSpark ||
            spell.Id != LegacyKineticSparkSpellId)
        {
            return false;
        }

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

        var hadKineticSpark = HasLegacyStatusFlag(resourceSession.Character, LegacyKineticSparkFlag);
        var toggledCharacter = hadKineticSpark
            ? WithoutCharacterAttribute(
                WithoutLegacyStatusFlag(resourceSession.Character, LegacyKineticSparkFlag),
                RuntimeLegacyKineticSparkExpiresAtUtcAttribute)
            : WithCharacterAttribute(
                WithLegacyStatusFlag(resourceSession.Character, LegacyKineticSparkFlag),
                RuntimeLegacyKineticSparkExpiresAtUtcAttribute,
                (DateTimeOffset.UtcNow.ToUniversalTime() + LegacyKineticSparkDuration)
                    .ToString("O", CultureInfo.InvariantCulture));
        var updatedSession = resourceSession with
        {
            Character = toggledCharacter,
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

        var casterUid = CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid));
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            casterUid,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            [new LegacySpellAnimationTarget(casterUid, 0, Hit: true, MsgAttackEffect.None)]);
        await SendPlayerSpellAnimationToVisibleAsync(updatedContext, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy KineticSpark direct cast " +
                $"{FormatSession(updatedContext)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} statusFlag={LegacyKineticSparkFlag} " +
                $"toggledOn={!hadKineticSpark} animationTargets=1; " +
                "source=KineticSpark.cs Execute case 11590.")
            .ConfigureAwait(false);
        return true;
    }
}
