namespace WolfConquer.Core.World;

internal sealed class LegacyNativeDivineGuardBindings
{
    private readonly LegacyNativeGameMap? map;
    private readonly LegacyNativeMapView? mapView;
    private readonly uint mapId, dynamicId;
    private readonly ushort x, y;
    private readonly DateTime previousStamp;

    internal LegacyNativeGameClientRuntime Client { get; }
    internal LegacyNativePlayerRuntime Player { get; }
    internal LegacyNativeStatusFlagsBigVector32 Vector { get; }
    internal LegacyNativeRoleView View { get; }
    internal uint Uid { get; }
    internal DateTime Stamp { get; }

    internal LegacyNativeDivineGuardBindings(LegacyNativeGameClientRuntime client,
        LegacyNativePlayerRuntime player, LegacyNativeStatusFlagsBigVector32 vector, LegacyNativeRoleView view)
    {
        Client = client; Player = player; Vector = vector; View = view;
        Uid = player.UID; x = player.X; y = player.Y; mapId = player.Map; dynamicId = player.DynamicID;
        map = client.Map; mapView = map?.View;
        previousStamp = player.LastDivineGuard;
        // Genuine native local clock, prepared before any chance or physical write.
        Stamp = DateTime.Now.AddMinutes(10.0);
    }

    internal bool IsCurrent() => IdentityIsCurrent() && Player.LastDivineGuard == previousStamp;

    internal bool IsAcceptedCurrent() => IdentityIsCurrent() && Player.LastDivineGuard == Stamp;

    private bool IdentityIsCurrent() => ReferenceEquals(Client.Player, Player) && ReferenceEquals(Player.Owner, Client) &&
        ReferenceEquals(Player.BitVector, Vector) && ReferenceEquals(Player.View, View) && ReferenceEquals(View.Owner, Client) &&
        Player.UID == Uid && Player.X == x && Player.Y == y && Player.Map == mapId && Player.DynamicID == dynamicId &&
        ReferenceEquals(Client.Map, map) && (map is null || (mapView is not null && ReferenceEquals(map.View, mapView)));
}
