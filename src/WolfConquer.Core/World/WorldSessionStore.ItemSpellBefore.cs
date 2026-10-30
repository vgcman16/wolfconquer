using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // A closed read/preparation entry, with the same Store-before-actor order
    // as publication. It accepts no operation, RNG, writer or acceptance callback.
    internal WorldPacketHandlers.ItemSpellBeforeImages CaptureItemSpellBeforeImages(WorldPacketHandlerContext context,
        WorldSession expected, WorldSession stable, LegacyPreparedSpellFileWrite writer, string[] paths)
    {
        if (!ReferenceEquals(context.Sessions, this)) throw InvalidSpellPublication();
        RefuseBoundSpellReentry(context.Connection);
        using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected);
        if (!_connectionGates.TryGetValue(context.Connection, out var gate)) throw InvalidSpellPublication();
        lock (gate)
        {
            using var scope = EnterNativeConnectionGate();
            var binding = RequireItemSpellBeforeHolder(context, expected, writer);
            using var actors = binding is null ? null : new SpellActorScope(binding);
            writer.RequireItemSpellBeforeOwnerPlain(context, expected);
            return SnapshotFileLockCoordinator.ExecuteLocked(paths,
                () => WorldPacketHandlers.ItemSpellBeforeImages.Capture(context, expected, stable, writer, paths));
        }
    }

    internal void RequireItemSpellBeforeOwner(WorldPacketHandlerContext context, WorldSession expected,
        LegacyPreparedSpellFileWrite writer)
    {
        if (!ReferenceEquals(context.Sessions, this)) throw InvalidSpellPublication();
        RefuseBoundSpellReentry(context.Connection);
        using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected);
        if (!_connectionGates.TryGetValue(context.Connection, out var gate)) throw InvalidSpellPublication();
        lock (gate)
        {
            using var scope = EnterNativeConnectionGate();
            var binding = RequireItemSpellBeforeHolder(context, expected, writer);
            using var actors = binding is null ? null : new SpellActorScope(binding);
            writer.RequireItemSpellBeforeOwnerPlain(context, expected);
        }
    }

    private LegacyOrdinarySpellBinding? RequireItemSpellBeforeHolder(WorldPacketHandlerContext context,
        WorldSession expected, LegacyPreparedSpellFileWrite writer)
    {
        if (!_sessions.TryGetValue(context.Connection, out var holder) || !ReferenceEquals(holder.Read(), expected) ||
            !ReferenceEquals(context.Connection.NativeGameClient, expected.NativeGameClient) ||
            !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, expected, expected)) throw InvalidSpellPublication();
        var binding = holder.SpellBinding;
        if (binding is not null && !BindingIsCurrentPlain(binding, holder, expected) || binding is null &&
            (context.Connection.NativeGameClient?.OrdinarySpellBinding is not null || expected.NativeGameClient?.OrdinarySpellBinding is not null))
            throw InvalidSpellPublication();
        writer.RequireItemSpellBeforeOwnerPlain(context, expected);
        return binding;
    }
}
