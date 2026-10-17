using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> HandleLegacyFreshShieldXpBuffAsync(
        WorldPacketHandlerContext context, MsgAttackQuery query, PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell, WorldSession session, CancellationToken cancellationToken)
    {
        var uid = session.Character.Uid;
        bool Refresh() => TryRefreshLegacyPlayerMagicResourceSession(context, uid, ref session);
        if (!Refresh()) return true;
        if (!HasLegacyStatusFlag(session.Character, LegacyRuntimeUpdateTicker.XpListFlag))
        {
            await context.Log.WriteLineAsync(
                $"WorldServer skipped legacy Shield XP buff {FormatSession(context with { Session = session })} " +
                $"spellId={spell.Id}; XPList status flag is not active after source resource preparation.").ConfigureAwait(false);
            return true;
        }

        // AttachStatus657-667 constructs this before RemoveFlag(XPList), including its awaited scalar.
        var casterUid = CheckedUInt32(uid, nameof(uid));
        var frame = LegacySpellAnimationPacket.ForPlayerMagic(casterUid, query.X, query.Y, spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue), playerSpell.UseSpellSoul,
            [new LegacySpellAnimationTarget(casterUid, spell.Duration, true, MsgAttackEffect.None)]).ToFrameBytes();
        session = session with
        {
            Character = LegacyStatusFlagDeathRegistration.Forget(
                WithoutLegacyStatusFlag(session.Character, LegacyRuntimeUpdateTicker.XpListFlag),
                LegacyRuntimeUpdateTicker.XpListFlag),
        };
        context.Sessions?.Set(context.Connection, session);
        await context.Connection.SendAsync(
            LegacyMsgUpdatePacket.CreateLegacyRuntimeXPListUpdate(casterUid, value: 0).ToFrameBytes(),
            cancellationToken).ConfigureAwait(false);
        if (!Refresh()) return true;
        // Native UpdateFlagOffset reads CURRENT bits after the XP scalar callback.
        var removed = await SendLegacyArrowBladesVectorAsync(
            context with { Session = session }, session, uid, cancellationToken).ConfigureAwait(false);
        if (removed is null) return true;
        session = removed;

        // AddFlag, not AddSpellFlag: a CURRENT duplicate preserves its old timer and policy.
        var hadStatus = HasLegacyStatusFlag(session.Character, LegacyMagicShieldFlag);
        if (!hadStatus)
        {
            var character = LegacyRootItemEffectFlagClock.ForgetProjection(session.Character, LegacyMagicShieldFlag);
            character = WithLegacySourceAddFlag(character, LegacyMagicShieldFlag, removeOnDead: true);
            character = LegacyRootItemEffectFlagClock.Install(context.Sessions, context.Connection, character,
                LegacyMagicShieldFlag, unchecked((int)spell.Duration), context.TimeProvider ?? TimeProvider.System);
            character = WithCharacterAttribute(character, RuntimeLegacyMagicShieldRemoveOnDeadAttribute, bool.TrueString);
            session = session with { Character = character };
            context.Sessions?.Set(context.Connection, session);
            var added = await SendLegacyArrowBladesVectorAsync(
                context with { Session = session }, session, uid, cancellationToken).ConfigureAwait(false);
            if (added is null) return true;
            session = added;
        }
        var sent = await SendLegacyArrowBladesVisibleFrameAsync(
            context with { Session = session }, session, uid, frame, cancellationToken).ConfigureAwait(false);
        if (sent is null) return true;
        await context.Log.WriteLineAsync(
            $"WorldServer handled legacy Shield XP buff {FormatSession(context with { Session = sent })} " +
            $"spellId={spell.Id} statusFlag={LegacyMagicShieldFlag} duration={spell.Duration} " +
            $"consumedXpList=True hadStatus={hadStatus} refreshedStatus=False " +
            $"animationTargetDamage={spell.Duration}; source=AttachStatus.cs case 1020 packetOrder=" +
            (hadStatus ? "XPList,StatusWithoutXPList,SpellAnimation2239." :
                "XPList,StatusWithoutXPList,StatusWithBuff,SpellAnimation2239.")).ConfigureAwait(false);
        return true;
    }
}
