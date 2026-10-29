using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> HandleLegacyPlayerClassicBuffAttachStatusMagicAsync(
        WorldPacketHandlerContext context, MsgAttackQuery query, PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell, WorldSession session, CancellationToken cancellationToken)
    {
        if (!TryGetLegacyClassicBuffDefinition(spell.Id, out var statusFlag, out var expiresAt,
                out var removeOnDead, out var suppressWhenActive)) return false;
        var target = new LegacyClassicBuffAdmissionRequest(query.OpponentUid);
        var resource = await TryPrepareLegacyPlayerMagicCastObservedAsync(context, session, spell, ignoreAttackStamp: false,
            cancellationToken, expectedNativeClientIdentity: null, classicTarget: target).ConfigureAwait(false);
        if (resource is null) return true;
        var resourceContext = context with { Session = resource };
        var casterUid = CheckedUInt32(resource.Character.Uid, nameof(resource.Character.Uid));
        var animationTargets = new List<LegacySpellAnimationTarget>(1);
        var progress = resource; var now = DateTimeOffset.UtcNow;
        var alreadyActive = false; var applied = false;
        if (target.Monster is { } monster)
        {
            if (resource.Character.MapId != target.ActorMap || resource.Character.X != target.ActorX ||
                resource.Character.Y != target.ActorY || ReadCharacterDynamicId(resource.Character) != target.ActorDynamic)
                throw new LegacyClassicBuffContinuationStoppedException("classicCommittedActorPositionChanged");
            if (!context.Sessions!.TryApplyClassicMonsterBuff(new(context.Connection, resource, target.ActorIdentity!),
                    monster, statusFlag, TimeSpan.FromSeconds(spell.Duration), now, !suppressWhenActive,
                    out var result, out var visible, out var vector, out var reason))
                throw new LegacyClassicBuffContinuationStoppedException(reason);
            alreadyActive = result.AlreadyActive;
            if (result.Applied)
            {
                await SendMonsterStatusFlagVectorToVisibleAsync(resourceContext, resource, visible,
                    vector, cancellationToken).ConfigureAwait(false);
                animationTargets.Add(new(visible.Uid, Damage: 0, Hit: true, MsgAttackEffect.None)); applied = true;
            }
        }
        else
        {
            var peer = target.Peer;
            var recipient = peer?.Expected ?? resource;
            var connection = peer?.Connection ?? context.Connection;
            var identity = peer?.Identity ?? target.ActorIdentity;
            RequireClassicTargetCurrent(context.Sessions, context.Connection, resource, target.ActorIdentity);
            RequireClassicTargetCurrent(context.Sessions, connection, recipient, identity);
            if (peer is not null && !CanSeePlayer(resource.Character, recipient.Character))
                throw new LegacyClassicBuffContinuationStoppedException("classicCommittedTargetNotVisible");
            alreadyActive = suppressWhenActive && HasLegacyStatusFlag(recipient.Character, statusFlag);
            RequireClassicTargetCurrent(context.Sessions, connection, recipient, identity);
            if (!alreadyActive)
            {
                var candidate = ApplyLegacyClassicBuffToPlayer(recipient, statusFlag, expiresAt,
                    removeOnDead, spell.Duration, now);
                if (context.Sessions is { } store)
                {
                    if (peer is { } selected)
                    {
                        if (!store.TryCommitProc(selected, candidate,
                                new WorldSessionStore.ProcParticipant(context.Connection, resource, target.ActorIdentity!), out var committed))
                            throw new LegacyClassicBuffContinuationStoppedException("classicCommittedTargetChanged");
                        candidate = committed!.Published;
                    }
                    else
                    {
                        if (!store.TryCompareAndSetWithNativeIdentity(connection, recipient, identity!, candidate, out var final))
                            throw new LegacyClassicBuffContinuationStoppedException("classicCommittedTargetChanged");
                        candidate = final!;
                    }
                }
                RequireClassicTargetCurrent(context.Sessions, connection, candidate, identity);
                if (peer is null) progress = candidate;
                await SendPlayerStatusFlagVectorToVisibleAsync(resourceContext with { Session = progress },
                    connection, candidate, cancellationToken).ConfigureAwait(false);
                animationTargets.Add(new(CheckedUInt32(candidate.Character.Uid, nameof(candidate.Character.Uid)),
                    Damage: 0, Hit: true, MsgAttackEffect.None)); applied = true;
            }
        }
        RequireClassicTargetCurrent(context.Sessions, context.Connection, progress, target.ActorIdentity);
        var progressContext = resourceContext with { Session = progress };
        progress = await TryApplyLegacyPlayerMagicSpellExperienceAsync(progressContext, progress,
            playerSpell, spell, spell.Duration, cancellationToken).ConfigureAwait(false);
        RequireClassicTargetCurrent(context.Sessions, context.Connection, progress, target.ActorIdentity);
        progressContext = progressContext with { Session = progress };
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(casterUid, query.X, query.Y, spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue), playerSpell.UseSpellSoul, animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(progressContext, progress, animation, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync("WorldServer handled legacy classic AttachStatus buff " +
            $"{FormatSession(progressContext)} spellId={spell.Id} statusFlag={statusFlag} targetUid={query.OpponentUid} " +
            $"targetKind={target.Kind.ToString().ToLowerInvariant()} targetMatched=True statusApplied={applied} " +
            $"alreadyActive={alreadyActive} duration={spell.Duration} progressDamage={spell.Duration} " +
            $"animationTargets={animationTargets.Count} statusOpcode={LegacyMsgUpdatePacket.Opcode} " +
            $"statusType={(uint)LegacyMsgUpdateDataType.StatusFlag} animationOpcode={LegacySpellAnimationPacket.Opcode}; " +
            $"source=AttachStatus.cs case {spell.Id}.").ConfigureAwait(false);
        return true;
    }
}
