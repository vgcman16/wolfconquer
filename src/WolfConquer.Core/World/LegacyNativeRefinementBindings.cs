namespace WolfConquer.Core.World;

internal sealed class LegacyNativeRefinementBindings
{
    private readonly LegacyNativeGameMap? map;
    private readonly LegacyNativeMapView? mapView;
    private readonly uint mapId, dynamicId;
    private readonly ushort x, y;

    internal LegacyNativeGameClientRuntime Client { get; }
    internal LegacyNativePlayerRuntime Player { get; }
    internal LegacyNativeStatusFlagsBigVector32 Vector { get; }
    internal LegacyNativeRoleView View { get; }
    internal LegacyNativePerfectionEffects.RefinementKind Kind { get; }
    internal uint Uid { get; }

    internal LegacyNativeRefinementBindings(LegacyNativeGameClientRuntime client,
        LegacyNativePlayerRuntime player, LegacyNativeStatusFlagsBigVector32 vector,
        LegacyNativeRoleView view, LegacyNativePerfectionEffects.RefinementKind kind)
    {
        Client = client; Player = player; Vector = vector; View = view; Kind = kind;
        Uid = player.UID; x = player.X; y = player.Y; mapId = player.Map; dynamicId = player.DynamicID;
        map = client.Map; mapView = map?.View;
    }

    internal bool IsCurrent() => ReferenceEquals(Client.Player, Player) && ReferenceEquals(Player.Owner, Client) &&
        ReferenceEquals(Player.BitVector, Vector) && ReferenceEquals(Player.View, View) && ReferenceEquals(View.Owner, Client) &&
        Player.UID == Uid && Player.X == x && Player.Y == y && Player.Map == mapId && Player.DynamicID == dynamicId &&
        ReferenceEquals(Client.Map, map) && (map is null || mapView is not null && ReferenceEquals(map.View, mapView));
}
