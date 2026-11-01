using WolfConquer.Core.Networking;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private enum BasicPhysicalAudience { ActorAndVisible, Target, TargetAndVisible }

    internal sealed partial class BasicPhysicalPublication
    {
        internal ValueTask<bool> PublishAsync(CancellationToken cancellationToken) => store.PublishBasicPhysicalAsync(this, cancellationToken);
    }

    private async ValueTask<bool> PublishBasicPhysicalAsync(BasicPhysicalPublication token, CancellationToken cancellationToken)
    {
        if (!basicPhysicalPublications.TryGetValue(token, out var actual)) return false;
        actual.OutputPhase = "initial-currentness";
        lock (actual.Frame.RequireActionLifecycleGate(this))
        {
            if (actual.OutputStarted || !IsBasicPhysicalPublicationCurrent(actual)) return false;
            actual.OutputStarted = true;
        }
        try
        {
            actual.OutputPhase = "interaction";
            if (!await SendBasicPhysicalFrameAsync(actual, actual.Plan.InteractionFrame,
                    BasicPhysicalAudience.ActorAndVisible, cancellationToken).ConfigureAwait(false)) return false;
            actual.OutputPhase = "hp-phases";
            foreach (var phase in actual.Plan.Phases)
                if (!await SendBasicPhysicalFrameAsync(actual, phase.Frame,
                    phase.Audience == WorldPacketHandlers.OwnedBasicPhysicalReceiveAudience.Target
                        ? BasicPhysicalAudience.Target : BasicPhysicalAudience.TargetAndVisible,
                    cancellationToken).ConfigureAwait(false)) return false;
            if (actual.DeathNotifications is { } death)
            {
                actual.OutputPhase = "death-notifications";
                lock (actual.Frame.RequireActionLifecycleGate(this))
                {
                    if (!IsBasicPhysicalPublicationCurrent(actual)) return false;
                    actual.DeathOutputStarted = true;
                }
                if (!await death.PublishAsync(cancellationToken).ConfigureAwait(false)) return false;
            }
            actual.OutputPhase = "base-completion";
            lock (actual.Frame.RequireActionLifecycleGate(this))
            {
                if (!IsBasicPhysicalPublicationCurrent(actual)) return false;
                actual.BaseOutputCompleted = true;
            }
            actual.OutputPhase = "proficiency-commit";
            if (!TryCommitBasicPhysicalProficiency(token)) return false;
            actual.OutputPhase = "proficiency-output";
            foreach (var frame in actual.ProficiencyWrite.Plan.Frames)
            {
                if (!IsBasicPhysicalPublicationCurrent(actual)) return false;
                await actual.ActorBinding.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                if (!IsBasicPhysicalPublicationCurrent(actual)) return false;
            }
            lock (actual.Frame.RequireActionLifecycleGate(this))
            {
                if (!actual.ProficiencyCompleted || !actual.ProficiencyWrite.IsCommittedFor(token) ||
                    !IsBasicPhysicalPublicationCurrent(actual)) return false;
                actual.CompletedOutput = true; // This private path alone completed every required send.
            }
            actual.OutputPhase = "repeat-arm";
            bool armed = TryArmCompletedBasicPhysical(token);
            if (armed) actual.OutputPhase = "completed";
            return armed;
        }
        catch (Exception error)
        {
            // The actual HP/proficiency pointer remains committed. A lost
            // notification cannot authorize an inverse save or another hit.
            throw FenceBasicPhysicalPersistence(actual, "basicPhysicalCommittedOutputFailed", error);
        }
    }

    private async ValueTask<bool> SendBasicPhysicalFrameAsync(BasicPhysicalPublicationData actual,
        ReadOnlyMemory<byte> frame, BasicPhysicalAudience audience, CancellationToken cancellationToken)
    {
        if (!IsBasicPhysicalPublicationCurrent(actual)) return false;
        bool actorSubject = audience == BasicPhysicalAudience.ActorAndVisible;
        var subject = actorSubject ? actual.Actor : actual.Target;
        var connection = actorSubject ? actual.ActorBinding.Connection : actual.TargetBinding.Connection;
        await connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        if (!IsBasicPhysicalPublicationCurrent(actual)) return false;
        if (audience == BasicPhysicalAudience.Target) return true;
        // Source viewers are captured after owner delivery. A later holder or
        // native identity cannot inherit a previously selected recipient.
        var viewers = new List<(ConquerTcpConnection Connection, long Uid, object Identity)>();
        foreach (var candidate in Snapshot())
            if (!ReferenceEquals(candidate.Connection, connection) && candidate.Session.Character.Uid != subject.Character.Uid &&
                WorldPacketHandlers.BasicPhysicalViewerVisible(subject.Character, candidate.Session.Character) &&
                LegacyNativeClientPublication.TryCaptureIdentity(this, candidate.Connection, candidate.Session.Character.Uid, out var identity))
                viewers.Add((candidate.Connection, candidate.Session.Character.Uid, identity));
        foreach (var viewer in viewers)
        {
            if (!IsBasicPhysicalPublicationCurrent(actual)) return false;
            if (!LegacyNativeClientPublication.TryCurrent(this, viewer.Connection, viewer.Uid, viewer.Identity, out var current) ||
                !WorldPacketHandlers.BasicPhysicalViewerVisible(subject.Character, current.Character)) continue;
            await viewer.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            if (!IsBasicPhysicalPublicationCurrent(actual)) return false;
        }
        return true;
    }
}

public static partial class WorldPacketHandlers
{
    internal static bool BasicPhysicalViewerVisible(CharacterSnapshot owner, CharacterSnapshot viewer) => CanSeePlayer(owner, viewer);
}
