namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Source Player.FinRane is distinct from QueryEquipment's FineRaneSave.
    // Rejected bag requests do not alter this transient cooldown stamp.
    internal LegacyNativeTime32 FinRane;

    internal sealed class OwnedRuneBagPublication
    {
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly LegacyNativePlayerRuntime player;
        private readonly LegacyNativeTime32 before, after;
        private OwnedRuneBagPublication(LegacyOrdinarySpellBinding binding)
        {
            this.binding = binding; player = binding.Player; before = player.FinRane;
            after = LegacyNativeTime32.Now;
            if (!IsCurrentPlain()) throw new InvalidOperationException("ordinaryRuneBagNativeOwnerMismatch");
        }
        internal static OwnedRuneBagPublication PrepareBound(LegacyOrdinarySpellBinding binding) => new(binding);
        internal bool IsCurrentPlain() => !binding.IsRetired && !binding.Recovery.IsBlocked &&
            ReferenceEquals(binding.Client.OrdinarySpellBinding, binding) && ReferenceEquals(binding.Client.Player, player) &&
            player.FinRane == before;
        internal void PublishPrepared() => player.FinRane = after;
    }
}
