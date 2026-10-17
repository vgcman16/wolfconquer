using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal const string RuntimeLegacyLightningShieldExpiresAtUtcAttribute = "LegacyLightningShieldExpiresAtUtc";

    private const ushort LegacyLightningShieldAttachStatusSpellId = 15_750;
    private const int LegacyLightningShieldAttachStatusDurationSeconds = 2_592_000;

    private static async ValueTask<bool> HandleLegacyPlayerLightningShieldAttachStatusMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.AttachStatus ||
            spell.Id != LegacyLightningShieldAttachStatusSpellId)
        {
            return false;
        }

        var uid = session.Character.Uid;
        var resourceSession = await TryPrepareLegacyPlayerMagicCastAsync(
                context, session, spell, cancellationToken)
            .ConfigureAwait(false);
        if (resourceSession is null ||
            !TryRefreshLegacyLightningShieldFamilySubject(context, resourceSession, uid, out var current))
            return true;

        var hadLightningShield = HasLegacyStatusFlag(current.Character, LegacyLightningShieldFlag);
        if (hadLightningShield)
        {
            // AttachStatus343-360: two separate RemoveFlag publications, then
            // the owner reset. Neither registration owns LightningShieldLeft.
            foreach (var flag in new[] { LegacyLightningShieldFlag, LegacyLightningShieldActivatedFlag })
            {
                if (!HasLegacyStatusFlag(current.Character, flag)) continue;
                var character = WithoutLegacyStatusFlag(current.Character, flag);
                if (flag == LegacyLightningShieldFlag)
                    character = WithoutCharacterAttribute(character, RuntimeLegacyLightningShieldExpiresAtUtcAttribute);
                current = current with { Character = character };
                context.Sessions?.Set(context.Connection, current);
                var afterVector = await SendLegacyLightningShieldFamilyVectorAsync(
                        context, current, uid, cancellationToken)
                    .ConfigureAwait(false);
                if (afterVector is null) return true;
                current = afterVector;
            }
            await SendLegacyLightningShieldAzureShieldResetAsync(
                    context, current, cancellationToken)
                .ConfigureAwait(false);
            if (!TryRefreshLegacyLightningShieldFamilySubject(context, current, uid, out current)) return true;
        }
        else
        {
            current = EnableLegacyLightningShield(current, DateTimeOffset.UtcNow);
            context.Sessions?.Set(context.Connection, current);
            var afterVector = await SendLegacyLightningShieldFamilyVectorAsync(
                    context, current, uid, cancellationToken)
                .ConfigureAwait(false);
            if (afterVector is null) return true;
            current = afterVector;
        }

        var casterUid = CheckedUInt32(uid, nameof(uid));
        var animation = new LegacySpellAnimationPacket(
            casterUid,
            AttackedUid: casterUid,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            NextSpell: 0,
            LevelHu: playerSpell.UseSpellSoul,
            SpellEffect: 0,
            [new LegacySpellAnimationTarget(casterUid, 0, Hit: true, MsgAttackEffect.None)]);
        foreach (var frame in animation.ToFrames())
        {
            var afterAnimation = await SendLegacyLightningShieldFamilyFrameAsync(
                    context, current, uid, frame, cancellationToken)
                .ConfigureAwait(false);
            if (afterAnimation is null) return true;
            current = afterAnimation;
        }
        var updatedContext = context with { Session = current };

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy LightningShield AttachStatus magic " +
                $"{FormatSession(updatedContext)} spellId={spell.Id} spellLevel={playerSpell.Level} " +
                $"statusFlag={LegacyLightningShieldFlag} toggledOn={!hadLightningShield} " +
                $"duration={LegacyLightningShieldAttachStatusDurationSeconds} removeOnDead=False " +
                $"clearedActivated={hadLightningShield}; source=AttachStatus.cs case 15750.")
            .ConfigureAwait(false);
        return true;
    }

    private static WorldSession EnableLegacyLightningShield(
        WorldSession session,
        DateTimeOffset nowUtc)
    {

        var updatedCharacter = WithCharacterAttribute(
            WithLegacyStatusFlag(session.Character, LegacyLightningShieldFlag),
            RuntimeLegacyLightningShieldExpiresAtUtcAttribute,
            nowUtc
                .ToUniversalTime()
                .AddSeconds(LegacyLightningShieldAttachStatusDurationSeconds)
                .ToString("O", CultureInfo.InvariantCulture));
        return session with { Character = updatedCharacter };
    }

    private static async ValueTask SendLegacyLightningShieldAzureShieldResetAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var packet = new LegacyMsgUpdatePacket(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            [
                new LegacyMsgUpdateEntry(
                    LegacyMsgUpdateDataType.AzureShield,
                    Value: 0,
                    Flag: checked((uint)LegacyLightningShieldActivatedFlag),
                    Time: 0,
                    Damage: 0,
                    SpellLevel: 0),
            ]);
        await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
    }
}
