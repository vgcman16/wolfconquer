using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // The exact captured holder and native connection identity, as well as the
    // real per-connection clock entry (or proven absence), are consumed together.
    internal bool TryCommitRootItemEffectExpiry(ConquerTcpConnection connection, object identity,
        WorldSession expected, WorldServerFrameHandler.RootExpiryAuthorization authorization,
        out WorldSession? final, out bool removed)
    {
        return TryCommitRootItemEffectExpiry(connection, identity, expected, authorization, out final, out removed, out _);
    }

    internal bool TryCommitRootItemEffectExpiry(ConquerTcpConnection connection, object identity,
        WorldSession expected, WorldServerFrameHandler.RootExpiryAuthorization authorization,
        out WorldSession? final, out bool removed, out RootExpiryOutputReceipt? output)
    {
        return TryCommitRootItemEffectExpiry(connection, identity, expected, authorization, out final, out removed, out output, out _);
    }

    internal bool TryCommitRootItemEffectExpiry(ConquerTcpConnection connection, object identity,
        WorldSession expected, WorldServerFrameHandler.RootExpiryAuthorization authorization,
        out WorldSession? final, out bool removed, out RootExpiryOutputReceipt? output,
        out LegacyNativeAttributeMapProof? successorMap)
    {
        final = null; removed = false; output = null; successorMap = null;
        if (authorization is null) return false;
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
                    !LegacyExperienceContinuationFence.AllowsOrdinaryPublication(expected, expected) ||
                    !WorldServerFrameHandler.TryReadRootExpiryAuthorization(authorization, this, connection, identity, expected,
                        out var capturedMap, out var observation) || observation.Flag is not 8 and not 9) return false;
                using var clock = LegacyRootItemEffectFlagClock.PreparedExpiry.TryCreate(this, connection, expected, identity, observation);
                if (clock is null || !WorldServerFrameHandler.TryProjectRootItemEffectExpiry(expected.Character, observation, out var character, out var projectedRemoved)) return false;
                var proposed = expected with { Character = character };
                if (!LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, expected, proposed)) return false;
                using var publication = new PreparedSessionSpellPublication(this, connection, holder, expected, proposed);
                if (publication.HitpointsWrite is not null || !capturedMap.IsCurrentPlain()) return false;
                bool matches;
                try { matches = WorldPacketHandlers.RootXpShieldProjectionMatches(proposed.Character, publication.FinalSession.Character); }
                catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or FormatException or OverflowException)
                { return false; } // Operated DATA failed before the writer; this subject is untouched.
                if (!matches) return false;
                // Bind the next ordered entry to the exact final map prepared by
                // this real Store writer, before it can be observed externally.
                LegacyNativeAttributeMapProof preparedSuccessorMap;
                try { preparedSuccessorMap = LegacyNativeAttributeMapProof.CaptureRaw(publication.FinalSession.Character.Attributes); }
                catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or FormatException or OverflowException)
                { return false; }
                if (!preparedSuccessorMap.IsCurrentPlain()) return false;
                var preparedOutput = projectedRemoved
                    ? PrepareRootExpiryOutput(connection, holder, publication.FinalSession, publication.PublishedIdentity)
                    : null;
                var clockPermit = RootClockPermit(this, connection, holder, expected, publication.FinalSession, publication.PublishedIdentity, clock);
                if (!WorldServerFrameHandler.ConsumeRootExpiryAuthorization(authorization, this, connection, identity, expected) ||
                    !TryPublishFrameOwnedHitpoints(publication, holder, out _)) return false;
                clock.Publish(clockPermit);
                if (preparedOutput is not null) { PublishRootExpiryOutput(preparedOutput); output = preparedOutput; }
                removed = projectedRemoved;
                successorMap = preparedSuccessorMap;
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
