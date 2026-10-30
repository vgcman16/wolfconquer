namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // This closed capability prepares only. It accepts no grant/writer callback.
    internal ItemRepairPreparationLease PrepareItemRepair(WorldPacketHandlerContext context, WorldSession expected) =>
        new(this, context, expected);

    internal sealed class ItemRepairPreparationLease : IDisposable
    {
        private readonly WorldSessionStore store;
        private readonly WorldPacketHandlerContext context;
        private readonly SessionHolder holder;
        private readonly object gate;
        private readonly IDisposable package;
        private SpellActorScope? actors;
        private bool gateHeld, entered, disposed;
        private readonly int thread = Environment.CurrentManagedThreadId;
        internal WorldSession Expected { get; }
        internal object Identity { get; }
        internal LegacyOrdinarySpellBinding? Binding { get; }
        internal WorldServerFrameHandler Frame { get; }
        internal string UsersRoot { get; }
        internal string ItemsRoot { get; }

        internal ItemRepairPreparationLease(
            WorldSessionStore store, WorldPacketHandlerContext context, WorldSession expected)
        {
            this.store = store;
            this.context = context;
            Expected = expected;
            Frame = store.framePersistenceOwner ?? throw InvalidRepairPreparation();
            RefuseBoundSpellReentry(context.Connection);
            package = LegacyMailboxPackageMutationFence.EnterStoreScope(store.MailboxPackages, expected);
            try
            {
                if (!store._connectionGates.TryGetValue(context.Connection, out gate!))
                    throw InvalidRepairPreparation();
                Monitor.Enter(gate, ref gateHeld);
                _enteredConnectionGateDepth = checked(_enteredConnectionGateDepth + 1);
                entered = true;
                if (!store._sessions.TryGetValue(context.Connection, out holder!) ||
                    !ReferenceEquals(holder.Read(), expected) || expected.Character is null ||
                    expected.PlayerItems is null || expected.Character.Attributes is null ||
                    expected.Character.PaidNobility is null ||
                    !LegacyMailboxPackageMutationFence.CanMutateSession(store.MailboxPackages, expected, expected))
                    throw InvalidRepairPreparation();
                Identity = holder.NativeClientIdentity;
                Binding = holder.SpellBinding;
                if (Binding is { } binding)
                {
                    if (!store.BindingIsCurrentPlain(binding, holder, expected) ||
                        !ReferenceEquals(binding.Recovery, context.TradeRecovery))
                        throw InvalidRepairPreparation();
                    actors = new SpellActorScope(binding);
                    binding.Items.RequireCurrentBeforePreparation();
                }
                else if (context.Connection.NativeGameClient is not null || expected.NativeGameClient is not null)
                {
                    throw InvalidRepairPreparation();
                }
                var roots = Frame.RequireItemRepairContext(store, context, expected);
                UsersRoot = roots.UsersRoot;
                ItemsRoot = roots.ItemsRoot;
                RequireCurrent();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal void RequireCurrent()
        {
            if (disposed || Environment.CurrentManagedThreadId != thread || !gateHeld ||
                !ReferenceEquals(store.framePersistenceOwner, Frame) ||
                !store._sessions.TryGetValue(context.Connection, out var current) ||
                !ReferenceEquals(current, holder) || !ReferenceEquals(holder.Read(), Expected) ||
                !ReferenceEquals(holder.NativeClientIdentity, Identity) || !ReferenceEquals(holder.SpellBinding, Binding))
                throw InvalidRepairPreparation();
            if (Binding is { } binding)
            {
                if (!store.BindingIsCurrentPlain(binding, holder, Expected))
                    throw InvalidRepairPreparation();
                binding.Items.RequireCurrentBeforePreparation();
            }
            else if (context.Connection.NativeGameClient is not null || Expected.NativeGameClient is not null)
            {
                throw InvalidRepairPreparation();
            }
            var roots = Frame.RequireItemRepairContext(store, context, Expected);
            if (roots.UsersRoot != UsersRoot || roots.ItemsRoot != ItemsRoot)
                throw InvalidRepairPreparation();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            if (Environment.CurrentManagedThreadId != thread)
                throw InvalidRepairPreparation();
            disposed = true;
            try { actors?.Dispose(); }
            finally
            {
                if (entered)
                {
                    entered = false;
                    _enteredConnectionGateDepth--;
                }
                if (gateHeld)
                {
                    gateHeld = false;
                    Monitor.Exit(gate);
                }
                package.Dispose();
            }
        }
    }

    private static InvalidOperationException InvalidRepairPreparation() =>
        new("itemRepairRequiresCurrentFrameIssuedPreparation");
}
