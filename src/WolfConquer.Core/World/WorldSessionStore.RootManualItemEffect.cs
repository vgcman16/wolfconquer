using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // Real manual Root item proc: insert its flag and clock in the same
    // holder/clock transaction, with no speculative clock publication.
    internal bool TryCommitRootManualItemEffect(ConquerTcpConnection connection, object identity,
        WorldSession expected, WorldPacketHandlers.RootManualItemEffectAdmission admission,
        int flag, int seconds, string removeOnDead, TimeProvider provider,
        out WorldSession? final)
    {
        final = null;
        if (admission is null || flag is not 8 and not 9 || seconds < 0 ||
            removeOnDead != (flag == 8 ? WorldPacketHandlers.RuntimeLegacyMagicShieldRemoveOnDeadAttribute :
                WorldPacketHandlers.RuntimeLegacyStigmaRemoveOnDeadAttribute)) return false;
        RefuseBoundSpellReentry(connection);
        object publishedIdentity; LegacyOrdinarySpellBinding? publishedBinding;
        using (LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected))
        {
            if (!_connectionGates.TryGetValue(connection, out var gate)) return false;
            lock (gate)
            {
                using var native = EnterNativeConnectionGate();
                if (!_sessions.TryGetValue(connection, out var holder) || !ReferenceEquals(holder.Read(), expected) ||
                    !ReferenceEquals(holder.NativeClientIdentity, identity) ||
                    !WorldPacketHandlers.RootManualItemEffectAdmissionIsCurrent(admission, this, connection, expected,
                        identity, flag, seconds, removeOnDead, provider) ||
                    LegacyPlayerDeathState.HasStatusFlag(expected.Character.Attributes, flag) ||
                    !LegacyExperienceContinuationFence.AllowsOrdinaryPublication(expected, expected)) return false;
                if (!LegacyRootItemEffectFlagClock.CanRepresentDuration((uint)seconds, provider)) return false;
                var character = WorldPacketHandlers.ProjectRootManualItemEffect(expected.Character, flag, removeOnDead);
                using var clock = LegacyRootItemEffectFlagClock.PreparedInstall.Create(this, connection, character, flag, seconds, provider);
                var proposed = expected with { Character = clock.Character };
                if (!LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, expected, proposed)) return false;
                using var publication = new PreparedSessionSpellPublication(this, connection, holder, expected, proposed);
                if (publication.HitpointsWrite is not null ||
                    !WorldPacketHandlers.RootXpShieldProjectionMatches(proposed.Character, publication.FinalSession.Character)) return false;
                clock.BindPrepared(publication.FinalSession, publication.PublishedIdentity);
                publication.AttachRootClockInstall(clock);
                var clockPermit = RootClockPermit(this, connection, holder, expected, publication.FinalSession, publication.PublishedIdentity, clock);
                if (!WorldPacketHandlers.ConsumeRootManualItemEffectAdmission(admission, this, connection, expected,
                        identity, flag, seconds, removeOnDead, provider) ||
                    !TryPublishFrameOwnedHitpoints(publication, holder, out _)) return false;
                clock.Publish(clockPermit);
                final = publication.FinalSession; publishedIdentity = publication.PublishedIdentity;
                publishedBinding = publication.Binding;
                publication.Dispose();
                PersistFrameOwnedSessionChange(connection, expected, final, publishedIdentity, publishedBinding, publication.HitpointsWrite);
            }
        }
        NotifyPublishedSessionChange(connection, expected, final!, publishedIdentity, publishedBinding);
        return true;
    }
}
