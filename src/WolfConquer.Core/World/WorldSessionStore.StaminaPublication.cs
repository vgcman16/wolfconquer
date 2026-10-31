using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed class StaminaPublication
    {
        private readonly WorldSessionStore store;
        private readonly WorldPacketHandlerContext context;
        private readonly LegacyOwnedStaminaRuntime.Readiness readiness;
        private readonly LegacyOwnedStaminaRuntime.Readiness.Step step;
        private readonly WorldSession published;
        private readonly LegacyNativeStatusFlagsBigVector32.StaminaFlags flags;
        private readonly JoinedWindWalker? fan;
        private readonly byte[]? effect;
        private readonly byte[] scalar;
        private bool committed;

        private StaminaPublication(WorldSessionStore store, WorldPacketHandlerContext context,
            LegacyOwnedStaminaRuntime.Readiness readiness, LegacyOwnedStaminaRuntime.Readiness.Step step,
            WorldSession published, LegacyNativeStatusFlagsBigVector32.StaminaFlags flags)
        {
            this.store = store; this.context = context; this.readiness = readiness; this.step = step;
            this.published = published; this.flags = flags; fan = store.WindWalkerOwnerForStamina(readiness.Binding, flags);
            uint uid = checked((uint)readiness.Binding.Uid);
            effect = step.Effect ? LegacyStringPacket.PlayerEffect(uid, "TSM_SXJ_HPhf").ToFrameBytes() : null;
            scalar = LegacyMsgUpdatePacket.CreateLegacyRuntimeStaminaTickUpdate(uid, step.Stamina).ToFrameBytes();
        }

        // Only the actual prepared Store phase calls this factory; no caller
        // callback is accepted as either commit evidence or output authority.
        internal static StaminaPublication Create(WorldSessionStore store, WorldPacketHandlerContext context,
            LegacyOwnedStaminaRuntime.Readiness readiness, LegacyOwnedStaminaRuntime.Readiness.Step step,
            WorldSession published, LegacyNativeStatusFlagsBigVector32.StaminaFlags flags) =>
            new(store, context, readiness, step, published, flags);

        internal void ConfirmCommitted()
        {
            if (!ReferenceEquals(context.Connection, readiness.Connection) || !step.IsCommittedPlain() ||
                !store.TryGet(context.Connection, out var current) || !ReferenceEquals(current, published) ||
                !store.IsBoundSpellOwnerCurrent(readiness.Binding) || readiness.Binding.Player.Stamina != step.Stamina)
                throw new InvalidOperationException("staminaCommittedPublicationUnproven");
            committed = true;
        }

        private bool TryCurrent(out WorldSession current)
        {
            current = null!;
            try { return TryReadCurrent(out current); }
            catch (InvalidOperationException) { return false; }
        }

        private bool TryReadCurrent(out WorldSession current)
        {
            current = null!;
            if (!committed || !readiness.Frame.IsStaminaReadinessCurrent(readiness) ||
                !LegacyNativeClientPublication.TryCurrent(store, context.Connection, readiness.Binding.Uid,
                    readiness.Binding.NativeIdentity, out var actual)) return false;
            if (!ReferenceEquals(readiness.Binding.Player.BitVector, flags.Vector)) return false;
            lock (readiness.LifecycleGate)
            lock (flags.Vector.SyncRoot)
            {
                if (!step.IsCommittedPlain() || !store.IsBoundSpellOwnerCurrent(readiness.Binding) ||
                    !flags.IsCurrentPlain(readiness.Binding.Player.BitVector) || readiness.Binding.Player.Stamina != step.Stamina ||
                    LegacyStaminaState.RequireCurrent(actual.Character) != step.Stamina ||
                    fan is not null && !store.WindWalkerOwnerForStaminaPlain(readiness.Binding, flags, fan)) return false;
                current = actual; return true;
            }
        }

        internal async ValueTask<WorldSession?> PublishAsync(CancellationToken cancellationToken)
        {
            if (!TryCurrent(out var owner)) return null;
            if (effect is not null)
            {
                await context.Connection.SendAsync(effect, cancellationToken).ConfigureAwait(false);
                if (!TryCurrent(out owner)) return null;
                foreach (var viewer in store.Snapshot())
                {
                    if (ReferenceEquals(viewer.Connection, context.Connection) ||
                        !WorldPacketHandlers.WindWalkerViewerVisible(owner.Character, viewer.Session.Character) ||
                        !LegacyNativeClientPublication.TryCaptureIdentity(store, viewer.Connection, viewer.Session.Character.Uid, out var identity) ||
                        !LegacyNativeClientPublication.TryCurrent(store, viewer.Connection, viewer.Session.Character.Uid, identity, out var target)) continue;
                    if (!TryCurrent(out owner)) return null;
                    if (!WorldPacketHandlers.WindWalkerViewerVisible(owner.Character, target.Character)) continue;
                    await viewer.Connection.SendAsync(effect, cancellationToken).ConfigureAwait(false);
                    if (!TryCurrent(out owner)) return null;
                }
            }
            if (!TryCurrent(out owner)) return null;
            await context.Connection.SendAsync(scalar, cancellationToken).ConfigureAwait(false);
            return TryCurrent(out owner) ? owner : null;
        }
    }
}
