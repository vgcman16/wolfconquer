namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    internal bool TryPrepareMailboxClassExperience(LegacyNativeGameClientRuntime client,
        uint expected, uint increment, out MailboxClassExperienceProjection? projection)
    {
        projection = null;
        if (client is null || !ReferenceEquals(Owner, client) || !ReferenceEquals(client.Player, this) ||
            UID == 0 || _classexp != expected || increment == 0) return false;
        try { projection = new(this, client, UID, Class, expected, checked(expected + increment)); return true; }
        catch (OverflowException) { return false; }
    }

    internal sealed class MailboxClassExperienceProjection
    {
        private readonly LegacyNativePlayerRuntime player;
        private readonly LegacyNativeGameClientRuntime client;
        private readonly uint uid;
        private readonly uint classCode;
        private readonly uint before;
        private readonly uint after;
        private bool published;
        internal bool SendsUpdate { get; }
        internal MailboxClassExperienceProjection(LegacyNativePlayerRuntime player, LegacyNativeGameClientRuntime client,
            uint uid, uint classCode, uint before, uint after)
        { this.player = player; this.client = client; this.uid = uid; this.classCode = classCode; this.before = before; this.after = after; SendsUpdate = client.FullLoading; }
        private bool Same(uint value) => ReferenceEquals(player.Owner, client) && ReferenceEquals(client.Player, player) &&
            player.UID == uid && player.Class == classCode && player._classexp == value && client.FullLoading == SendsUpdate;
        internal bool IsCurrent() => !published && Same(before);
        internal bool IsPublishedCurrent() => published && Same(after);
        internal bool TryPublishCommitted()
        {
            if (!IsCurrent()) return false;
            player._classexp = after; // Never invoke the sending property setter under transaction gates.
            published = true;
            return IsPublishedCurrent();
        }
    }
}
