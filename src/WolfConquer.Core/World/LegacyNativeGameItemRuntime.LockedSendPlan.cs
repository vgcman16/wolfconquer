using WolfConquer.Core.Networking;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeGameItemRuntime
{
    internal readonly record struct LockedWitness(uint Uid, byte Locked, Flags.ItemMode Mode,
        int Timer, uint ServerId, uint PlayerServerId);

    internal sealed class LockedSendPlan : IDisposable
    {
        private readonly LegacyNativeGameItemRuntime source;
        private readonly LegacyNativeItemPacketSnapshot? image;
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyNativePlayerRuntime player;
        private readonly LegacyNativeGroupServerList.Server server;
        private readonly LockedWitness witness;
        private readonly bool unlock;
        internal LegacyNativePreparedItemOutput Output { get; }

        internal LockedSendPlan(LegacyNativeGameItemRuntime source, LegacyNativeItemPacketSnapshot? image, LegacyNativeGameClientRuntime client,
            LegacyNativePlayerRuntime player, LegacyNativeGroupServerList.Server server,
            LockedWitness witness, bool unlock, LegacyNativePreparedItemOutput output)
        {
            this.source = source; this.image = image; this.client = client; this.player = player;
            this.server = server; this.unlock = unlock; Output = output;
            this.witness = witness;
        }

        internal bool IsCurrent() => Output.IsUsable && (image is null || image.IsCurrent()) && source.Uid == witness.Uid &&
            source.Locked == witness.Locked && source.Mode == witness.Mode && source.UnLockTimer == witness.Timer &&
            ReferenceEquals(client.Player, player) &&
            ReferenceEquals(player.Owner, client) && ReferenceEquals(LegacyNativeGroupServerList.MyServerInfo, server) &&
            player.ServerID == witness.PlayerServerId && server.ID == witness.ServerId;

        internal void Commit()
        {
            if (!unlock) return;
            source.Locked = 0;
            source.Mode = Flags.ItemMode.Update;
        }

        public void Dispose() => Output.Dispose();
    }

    internal LockedSendPlan? PrepareLockedSend(LegacyNativeGameClientRuntime client, LegacyNativePacket? target,
        LegacyNativeItemPacketSnapshot? captured = null)
    {
        // No unrelated required owner is consulted for a genuinely inactive branch.
        if (Locked != 2) return null;
        ArgumentNullException.ThrowIfNull(client);
        var player = client.Player ?? throw new InvalidOperationException("Locked item has no actual player owner.");
        if (!ReferenceEquals(player.Owner, client)) throw new InvalidOperationException("Locked item player owner is foreign.");
        var server = LegacyNativeGroupServerList.MyServerInfo
            ?? throw new InvalidOperationException("Locked item requires the configured server owner.");
        var witness = new LockedWitness(Uid, Locked, Mode, UnLockTimer, server.ID, player.ServerID);
        if (witness.Locked != 2 || witness.PlayerServerId != witness.ServerId) return null;
        ArgumentNullException.ThrowIfNull(target);
        uint uid = witness.Uid;
        int timer = witness.Timer;
        bool unlock = timer == 0 || DateTime.Now > LegacyNativeRoleCore.GetTimer(timer);
        var image = captured ?? (unlock ? new LegacyNativeItemPacketSnapshot(this) : null);
        if (image is not null && (!ReferenceEquals(image.Source, this) || !image.IsCurrent()))
            throw new InvalidOperationException("Locked item changed before output preparation.");
        uint remaining = unlock ? RemainingTime : 0;
        uint minutes = unlock ? TimeLeftInMinutes : 0;
        var output = LegacyNativePreparedItemOutput.Prepare(target.Capacity, prepared =>
        {
            if (unlock) WriteCapturedItem(prepared, image!, remaining, minutes, Flags.ItemMode.Update, 0);
            else prepared.ItemLockCreate(uid, LegacyNativeItemLockPacket.TypeLock.UnlockDate, 0, (uint)timer);
        });
        try { return new(this, image, client, player, server, witness, unlock, output); }
        catch { output.Dispose(); throw; }
    }
}
