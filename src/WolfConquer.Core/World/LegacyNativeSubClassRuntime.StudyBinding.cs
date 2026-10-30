using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeSubClassRuntime
{
    private sealed class StudyBinding
    {
        private readonly LegacyNativeSubClassRuntime runtime;
        internal readonly LegacyNativeGameClientRuntime Client;
        private readonly LegacyNativePlayerRuntime player;
        private readonly LegacyNativeRoleView view;
        private readonly LegacyNativeGameMap? map;
        private readonly LegacyNativeMapView? mapView;
        private readonly LegacyNativeSecuritySocket? socket;
        internal readonly uint Uid;
        private readonly uint playerMap, dynamicId;
        private readonly ushort x, y;
        internal readonly ILegacyNativeMapObj[] Recipients;

        internal StudyBinding(LegacyNativeSubClassRuntime runtime,
            LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player)
        {
            this.runtime = runtime;
            Client = client;
            this.player = player;
            view = player.View ?? throw new InvalidOperationException("Study requires the actual view.");
            map = client.Map;
            mapView = map?.View;
            socket = client.Socket;
            Uid = player.UID;
            playerMap = player.Map;
            dynamicId = player.DynamicID;
            x = player.X;
            y = player.Y;
            RequireCurrent();
            Recipients = view.Roles(LegacyNativeMapObjectType.Player).ToArray();
            if (Recipients.Any(role => role is null))
                throw new InvalidOperationException("Study visibility contains an absent recipient.");
            RequireCurrent();
        }

        internal void RequireCurrent()
        {
            if (!ReferenceEquals(Client.Player, player) || !ReferenceEquals(player.Owner, Client) ||
                !ReferenceEquals(player.SubClass, runtime) || !ReferenceEquals(player.View, view) ||
                !ReferenceEquals(view.Owner, Client) || !ReferenceEquals(Client.Map, map) ||
                !ReferenceEquals(Client.Socket, socket) || (map is not null &&
                    (mapView is null || !ReferenceEquals(map.View, mapView))) ||
                player.UID != Uid || player.Map != playerMap || player.DynamicID != dynamicId ||
                player.X != x || player.Y != y)
                throw new InvalidOperationException("Study owner or visibility binding is not current.");
        }
    }
}
