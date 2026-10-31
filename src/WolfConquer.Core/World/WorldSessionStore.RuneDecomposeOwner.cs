namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal WorldServerFrameHandler RequireRuneDecomposeFrame(WorldPacketHandlerContext context, WorldSession expected)
    {
        if (!ReferenceEquals(context.Sessions, this)) throw InvalidSpellPublication();
        RefuseBoundSpellReentry(context.Connection);
        using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected);
        if (!_connectionGates.TryGetValue(context.Connection, out var gate)) throw InvalidSpellPublication();
        lock (gate)
        {
            using var scope = EnterNativeConnectionGate();
            if (!_sessions.TryGetValue(context.Connection, out var holder) || !ReferenceEquals(holder.Read(), expected) ||
                holder.SpellBinding is not { } binding || !BindingIsCurrentPlain(binding, holder, expected) ||
                !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, expected, expected)) throw InvalidSpellPublication();
            using var actors = new SpellActorScope(binding);
            if (!BindingIsCurrentPlain(binding, holder, expected)) throw InvalidSpellPublication();
            var actual = framePersistenceOwner ?? throw InvalidSpellPublication();
            _ = actual.RequireRuneDecomposeRuntime(this, context);
            return actual;
        }
    }
}
