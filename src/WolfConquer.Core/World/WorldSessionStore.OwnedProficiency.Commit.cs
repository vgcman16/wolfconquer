using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed class OwnedProficiencyPublication { }
    private readonly ConditionalWeakTable<OwnedProficiencyPublication, OwnedProficiencyPublicationData> ownedProficiencyPublications = new();

    internal bool TryCommitOwnedProficiency(WorldPacketHandlerContext context, WorldSession expected,
        WorldPacketHandlers.BasicPhysicalProficiencyCalculation calculation, out OwnedProficiencyPublication? receipt,
        out WorldSession? final, out string reason)
    {
        receipt = null; final = null; reason = "ownedProficiencyOwnerUnavailable";
        RefuseBoundSpellReentry(context.Connection);
        if (framePersistenceOwner is not { } frame || !calculation.HasChanges ||
            !ReferenceEquals(calculation.Before, expected.PlayerProficiencies) ||
            !frame.TryPrepareActionContext(this, context, expected, out var actual)) return false;
        context = actual;
        OwnedProficiencyPublication issued; OwnedProficiencyPublicationData data;
        lock (frame.RequireActionLifecycleGate(this))
        {
            var proposed = expected with { PlayerProficiencies = calculation.After };
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected, proposed);
            if (!_connectionGates.TryGetValue(context.Connection, out var gate)) return false;
            lock (gate)
            {
                using var nativeGate = EnterNativeConnectionGate();
                if (!_sessions.TryGetValue(context.Connection, out var holder) || !ReferenceEquals(holder.Read(), expected) ||
                    holder.SpellBinding is not { } binding || !BindingIsCurrentPlain(binding, holder, expected) ||
                    !frame.IsActionLifecycleCurrent(this, context) ||
                    !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, expected, proposed) ||
                    !LegacyExperienceContinuationFence.AllowsOrdinaryPublication(expected, proposed)) return false;
                using var publication = new PreparedSessionSpellPublication(this, context.Connection, holder, expected, proposed);
                if (publication.HitpointsWrite is not null || !ReferenceEquals(publication.FinalSession.Character, expected.Character) ||
                    !ReferenceEquals(publication.FinalSession.PlayerItems, expected.PlayerItems) ||
                    !ReferenceEquals(publication.FinalSession.PlayerSpells, expected.PlayerSpells)) return false;
                data = new(this, frame, context, binding, publication.PublishedIdentity, expected, publication.FinalSession,
                    calculation, publication.PreparedProficiencyGraph);
                try { data.PrepareFile(); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
                { reason = "ownedProficiencyPreflight:" + error.GetType().Name; return false; }
                issued = new(); ownedProficiencyPublications.Add(issued, data);
                publication.ArmFinalProof();
                if (!publication.ConsumeBeforeWriterPlain() || !data.BeforeIsCurrentPlain()) return false;
                if (!data.TryWrite(out reason)) return false;
                try
                {
                    if (!publication.IsCurrentPlain() || !data.AfterWriteIsCurrentPlain()) throw InvalidOwnedProficiency();
                    binding.PublishSavedProficiencies(data.AfterImage);
                    publication.PublishNative(); publication.PublishHolder(); data.Committed = true;
                    final = publication.FinalSession;
                    publication.Dispose();
                    PersistFrameOwnedSessionChange(context.Connection, expected, final, publication.PublishedIdentity, binding,
                        completedOwnedProficiencies: issued);
                }
                catch (Exception error) { FenceOwnedProficiencyFailure(data, error); throw; }
            }
        }
        NotifyPublishedSessionChange(context.Connection, expected, final!, data.Identity, data.Binding);
        receipt = issued; reason = string.Empty; return true;
    }

    private static InvalidOperationException InvalidOwnedProficiency() => new("ownedProficiencyPublicationChanged");
}
