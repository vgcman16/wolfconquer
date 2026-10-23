using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    public void SageMode(uint Timer)
    {
        var client = user;
        var player = client?.Player;
        if (client is null || player is null) return;
        lock (client.SpellAdmissionSyncRoot)
        lock (player.SpellAdmissionSyncRoot)
        {
            var vector = player.BitVector;
            var view = player.View;
            uint uid = player.UID;
            if (!ReferenceEquals(user, client) || !ReferenceEquals(client.Player, player) ||
                !ReferenceEquals(player.Owner, client) || view is null ||
                !ReferenceEquals(view.Owner, client) || client.Map is { View: null } || vector is null ||
                !vector.TryContainFlag((int)LegacyNativeMsgUpdate.Flags.SageSpeedMode, out bool present) ||
                !SageBindingsCurrent(client, player, vector, view, uid)) return;

            // Valid duplicates still publish the requested archive without renewing the row.
            // A refused absent grant must never be represented as an accepted archive.
            if (!present && !player.AddFlag(LegacyNativeMsgUpdate.Flags.SageSpeedMode,
                unchecked((int)Timer), RemoveOnDead: true)) return;
            if (!SageBindingsCurrent(client, player, vector, view, uid)) return;
            using var rental = new LegacyNativeRecycledPacket();
            player.SendUpdate(rental.GetStream(), LegacyNativeMsgUpdate.Flags.SageSpeedMode,
                Timer, 0u, 0u, LegacyNativeMsgUpdate.DataType.ArchiveSkill, scren: true);
        }
    }

    private bool SageBindingsCurrent(LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player,
        LegacyNativeStatusFlagsBigVector32 vector, LegacyNativeRoleView view, uint uid) =>
        ReferenceEquals(user, client) && ReferenceEquals(client.Player, player) &&
        ReferenceEquals(player.Owner, client) && ReferenceEquals(player.BitVector, vector) &&
        ReferenceEquals(player.View, view) && ReferenceEquals(view.Owner, client) &&
        player.UID == uid && client.Map is not { View: null };
}
