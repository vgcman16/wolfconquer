using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeGameItemRuntime
{
    public void SendItemLocked(LegacyNativeGameClientRuntime client, LegacyNativePacket stream)
    {
        // The real item remains the owner. Both the wire output and transition
        // prepare before any visible mutation; Send is a post-commit notification.
        lock (this)
        {
            if (Locked != 2) return;
            using var plan = PrepareLockedSend(client, stream);
            if (plan is null) return;
            ArgumentNullException.ThrowIfNull(stream);
            if (!plan.IsCurrent()) throw new InvalidOperationException("Locked item ownership changed before commitment.");
            plan.Output.PublishTo(stream);
            plan.Commit();
            plan.Output.Send(client);
        }
    }
}
