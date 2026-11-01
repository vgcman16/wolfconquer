namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed partial class OwnedPhysicalDeathNotifications
    {
        internal async ValueTask<bool> PublishAsync(CancellationToken cancellationToken)
        {
            if (!preparationReleased || !published || Interlocked.CompareExchange(ref started, 1, 0) != 0) return false;
            if (!TryCurrent()) return false;
            foreach (var delivery in deliveries)
            {
                foreach (var recipient in delivery.Recipients)
                {
                    if (!TryCurrent()) return false;
                    if (!RecipientCurrent(recipient, delivery.RequireVisibility)) continue;
                    // Preserve the immutable plan for subsequent recipients
                    // even when a transport observer owns its supplied buffer.
                    await recipient.Connection.SendAsync(delivery.Frame.ToArray(), cancellationToken).ConfigureAwait(false);
                    if (!TryCurrent()) return false;
                }
            }
            return TryCurrent();
        }

        private bool TryCurrent() => IsPublishedCurrentPlain() &&
            actual.TryReadCommittedDeathPublication(preparation, actor, target, out _);

        private bool RecipientCurrent(Recipient recipient, bool visible)
        {
            var store = preparation.TargetBinding.Store;
            if (!store.TryGet(recipient.Connection, out var current) || current is null || current.Character.Uid != recipient.Uid)
                return false;
            if (recipient.Client is { } original)
            {
                if (!ReferenceEquals(current.NativeGameClient, original) || original.OrdinarySpellBinding is not { } binding ||
                    !ReferenceEquals(binding.Store, store) || !ReferenceEquals(binding.Connection, recipient.Connection) ||
                    binding.Uid != recipient.Uid || binding.IsRetired || !store.IsBoundSpellOwnerCurrent(binding)) return false;
            }
            else if (!ReferenceEquals(current, recipient.Original)) return false;
            return !visible || CanSeePlayer(target.Character, current.Character);
        }
    }
}
