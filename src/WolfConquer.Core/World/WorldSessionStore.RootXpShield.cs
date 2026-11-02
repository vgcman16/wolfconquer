using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // Only the real XP Shield handler enters this concrete continuation. The
    // accepted session and identity are compared under the actual holder gate;
    // clock preparation owns no registration until the holder commits.
    internal bool TryCommitRootXpShield(ConquerTcpConnection connection, object identity,
        WorldSession expected, WorldPacketHandlers.RootXpShieldAdmission admission,
        uint duration, TimeProvider provider, out WorldSession? final, out bool added)
    {
        final = null; added = false;
        RefuseBoundSpellReentry(connection);
        object publishedIdentity; LegacyOrdinarySpellBinding? publishedBinding;
        using (LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected))
        {
            if (!_connectionGates.TryGetValue(connection, out var gate)) return false;
            lock (gate)
            {
                using var native = EnterNativeConnectionGate();
                if (!_sessions.TryGetValue(connection, out var holder) || !ReferenceEquals(holder.Read(), expected) ||
                    !ReferenceEquals(holder.NativeClientIdentity, identity) || expected.Character.MinHitPoints <= 0 ||
                    LegacyPlayerDeathState.HasStatusFlag(expected.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag) ||
                    LegacyPlayerDeathState.HasStatusFlag(expected.Character.Attributes, LegacyRuntimeUpdateTicker.XpListFlag) ||
                    !LegacyExperienceContinuationFence.AllowsOrdinaryPublication(expected, expected) ||
                    !WorldPacketHandlers.ConsumeRootXpShieldAdmission(admission, this, connection, identity, expected, duration)) return false;
                if (LegacyPlayerDeathState.HasStatusFlag(expected.Character.Attributes, 8))
                { final = expected; return true; }
                if (!LegacyRootItemEffectFlagClock.CanRepresentDuration(duration, provider)) return false;
                var character = WorldPacketHandlers.ProjectRootXpShieldFlag(expected.Character);
                using var clock = LegacyRootItemEffectFlagClock.PreparedInstall.Create(this, connection, character,
                    8, checked((int)duration), provider);
                var proposed = expected with { Character = clock.Character };
                if (!LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, expected, proposed)) return false;
                using var publication = new PreparedSessionSpellPublication(this, connection, holder, expected, proposed);
                // This transient flag/clock transition cannot acquire a scalar
                // saver or silently alter the prepared field image.
                if (publication.HitpointsWrite is not null ||
                    !WorldPacketHandlers.RootXpShieldProjectionMatches(proposed.Character, publication.FinalSession.Character)) return false;
                if (!TryPublishFrameOwnedHitpoints(publication, holder, out _)) return false;
                clock.Publish(); // preallocated, while both actual gates remain held
                final = publication.FinalSession; publishedIdentity = publication.PublishedIdentity;
                publishedBinding = publication.Binding; added = true;
                publication.Dispose();
                PersistFrameOwnedSessionChange(connection, expected, final, publishedIdentity, publishedBinding, publication.HitpointsWrite);
            }
        }
        NotifyPublishedSessionChange(connection, expected, final!, publishedIdentity, publishedBinding);
        return true;
    }
}
