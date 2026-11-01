using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool CanBeginInteractionInvitation(WorldPacketHandlerContext context, WorldActiveSession partner, MsgAttackPacket packet)
    {
        using var scope = InteractionPairScope.TryEnter(this, context, partner);
        return scope is not null && scope.Frame.CanBeginInteractionInvitationPlain(this, scope.Context, scope.Owner, partner.Session, packet);
    }

    internal bool TryCommitInteractionLinkPublication(WorldPacketHandlerContext context, WorldActiveSession partner,
        MsgAttackPacket packet, out InteractionInvitationReceipt? receipt)
    {
        receipt = null;
        using var scope = InteractionPairScope.TryEnter(this, context, partner);
        if (scope is null) return false;
        var transition = WorldServerFrameHandler.InteractionInvitationPublication.Prepare(scope.Frame, this,
            scope.Context, scope.Owner, partner.Session, packet);
        if (transition is null) return false;
        var own = ProjectInteractionLink(scope.Owner, partner.Session, transition);
        var peer = ProjectInteractionLink(partner.Session, scope.Owner, transition);
        if (!LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, scope.Owner, own) ||
            !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, partner.Session, peer)) return false;
        using var first = new PreparedSessionSpellPublication(this, scope.Context.Connection, scope.OwnHolder, scope.Owner, own);
        using var second = new PreparedSessionSpellPublication(this, partner.Connection, scope.PeerHolder, partner.Session, peer);
        RequireInteractionDataUnchanged(scope.Owner, first.FinalSession, first.HitpointsWrite);
        RequireInteractionDataUnchanged(partner.Session, second.FinalSession, second.HitpointsWrite);
        var prepared = new InteractionInvitationReceipt(this, transition, first.FinalSession, second.FinalSession, scope.Own, scope.Peer);
        first.RequireCurrentBeforeWriter(); second.RequireCurrentBeforeWriter();
        first.ArmFinalProof(); second.ArmFinalProof();
        if (!scope.IsCurrentPlain() || !transition.IsCurrentPlain() ||
            !first.ConsumeBeforeWriterPlain() || !second.ConsumeBeforeWriterPlain() ||
            !first.IsCurrentPlain() || !second.IsCurrentPlain() || !scope.IsCurrentPlain() || !transition.IsCurrentPlain()) return false;
        // No file/resource change, callback, packet or allocation follows this
        // final proof before both live holders and the exact private transition.
        first.PublishNative(); second.PublishNative();
        first.PublishHolder(); second.PublishHolder();
        transition.PublishPrepared();
        receipt = prepared;
        return true;
    }

    private static WorldSession ProjectInteractionLink(WorldSession before, WorldSession other,
        WorldServerFrameHandler.InteractionInvitationPublication transition) => transition.Kind switch
    {
        MsgAttackType.InteractionRequest => before with { ActiveInteractionDance = transition.Dance },
        MsgAttackType.InteractionAccept => before with { InteractionPartnerUid = checked((uint)other.Character.Uid), ActiveInteractionDance = transition.Dance },
        MsgAttackType.InteractionRefuse or MsgAttackType.InteractionStopEffect => before with { InteractionPartnerUid = null, ActiveInteractionDance = 0 },
        _ => throw new InvalidOperationException("interactionInvitationKindInvalid"),
    };

    private static void RequireInteractionDataUnchanged(WorldSession before, WorldSession after, LegacyBoundHitpointsScalarWrite? writer)
    {
        if (writer is not null || !WorldPacketHandlers.BoundHitpointsInputs.ScalarsEqual(before.Character, after.Character) ||
            CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(before.Character, after.Character) ||
            !WorldPacketHandlers.BoundHitpointsInputs.PaidEqual(before.Character, after.Character) ||
            !PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems).AsSpan().SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(after.PlayerItems)) ||
            !PlayerSpellSnapshotLoader.ToLegacyBytes(before.PlayerSpells).AsSpan().SequenceEqual(PlayerSpellSnapshotLoader.ToLegacyBytes(after.PlayerSpells)) ||
            !PlayerProficiencySnapshotLoader.ToLegacyBytes(before.PlayerProficiencies).AsSpan().SequenceEqual(PlayerProficiencySnapshotLoader.ToLegacyBytes(after.PlayerProficiencies)))
            throw new InvalidOperationException("interactionInvitationCannotWritePersistentOrResourceState");
    }
}
