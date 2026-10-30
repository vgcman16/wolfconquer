using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeGameItemRuntime
{
    // Complete direct-pipe item/optional-extra sequence. This route does not
    // normalize items, unlock them or impose GameClient.Send's 1032-byte cap.
    public void Send(LegacyNativePipeClientRuntime user, LegacyNativePacket stream)
    {
        var socket = CaptureItemPipe(user);
        if (socket is null) return;
        lock (this)
        {
            if (!ItemPipeIsCurrent(user, socket)) return;
            var target = new PipeItemTarget(stream);
            using var plan = PreparePipeItem(stream.Capacity);
            if (!ItemPipeIsCurrent(user, socket)) return;
            plan.RequireCurrent();
            plan.RequireCapacity(stream.Capacity);
            target.RequireCurrent(stream);
            // Every preparation/refusal precedes the single caller write and
            // no-throw actual-extra commit. Later sends do not undo commitment.
            target.Publish(stream, plan.FinalBytes);
            plan.CommitExtras();
            SendCommittedPipeItem(user, socket, plan);
        }
    }
}
