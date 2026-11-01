using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // CaptureItemSpellBeforeImages already holds the package, connection and
    // file gates. This proof never acquires them in the opposite order.
    internal bool MatchesManagedItemSpellImage(WorldPacketHandlerContext context, WorldSession expected,
        WorldSession stable, string path, byte[] before, out bool applicable)
    {
        applicable = framePersistenceOwner?.RequiresUnboundCharacterOrigin(expected) == true;
        return applicable && ReferenceEquals(context.Sessions, this) &&
            framePersistenceOwner!.MatchesManagedItemSpellImage(this, context.Connection, expected, stable, path, before);
    }

    internal void CompleteManagedItemSpellImage(WorldPacketHandlerContext context, WorldSession expected,
        WorldSession published, LegacyPreparedSpellFileWrite writer)
    {
        if (framePersistenceOwner?.RequiresUnboundCharacterOrigin(expected) != true) return;
        RefuseBoundSpellReentry(context.Connection);
        using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected, published);
        if (!_connectionGates.TryGetValue(context.Connection, out var gate))
            throw new LegacyCommittedSpellPublicationException();
        lock (gate)
        {
            using var native = EnterNativeConnectionGate();
            SessionHolder? holder = null;
            try
            {
                if (!ReferenceEquals(context.Sessions, this) || !_sessions.TryGetValue(context.Connection, out holder) ||
                    !ReferenceEquals(holder.Read(), published) || holder.SpellBinding is not null)
                    throw new InvalidOperationException("managedItemSpellPublishedHolderChanged");
                framePersistenceOwner.CompleteManagedItemSpellImage(this, context.Connection, expected, published, writer);
            }
            catch (Exception primary)
            {
                var errors = new List<Exception> { primary };
                try { framePersistenceOwner.BlockUnboundQueryFailure(); } catch (Exception error) { errors.Add(error); }
                if (holder is not null && ReferenceEquals(holder.Read(), published))
                    try { holder.Clear(); } catch (Exception error) { errors.Add(error); }
                if (errors.Count > 1) throw new AggregateException("Managed item/spell publication and fencing failed.", errors);
                throw;
            }
        }
    }
}
