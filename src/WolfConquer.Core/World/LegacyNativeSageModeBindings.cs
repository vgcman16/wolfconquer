namespace WolfConquer.Core.World;

// Immutable physical owner identity only; no accepting gameplay/transport callback.
internal sealed class LegacyNativeSageModeBindings
{
    private readonly LegacyNativeNinja ninja;
    private readonly LegacyNativeGameMap? map;
    private readonly LegacyNativeMapView? mapView;

    internal LegacyNativeGameClientRuntime Client { get; }
    internal LegacyNativePlayerRuntime Player { get; }
    internal LegacyNativeStatusFlagsBigVector32 Vector { get; }
    internal LegacyNativeRoleView View { get; }
    internal uint Uid { get; }

    internal LegacyNativeSageModeBindings(LegacyNativeNinja ninja, LegacyNativeGameClientRuntime client,
        LegacyNativePlayerRuntime player, LegacyNativeStatusFlagsBigVector32 vector, LegacyNativeRoleView view)
    {
        this.ninja = ninja;
        Client = client; Player = player; Vector = vector; View = view; Uid = player.UID;
        map = client.Map; mapView = map?.View;
    }

    // Deliberately pure after any dictionary comparer has returned. A null map is
    // the genuine no-peers path, but a present map needs its actual captured view.
    internal bool IsCurrent() => ReferenceEquals(ninja.user, Client) && ReferenceEquals(Client.Player, Player) &&
        ReferenceEquals(Player.Owner, Client) && ReferenceEquals(Player.BitVector, Vector) &&
        ReferenceEquals(Player.View, View) && ReferenceEquals(View.Owner, Client) && Player.UID == Uid &&
        ReferenceEquals(Client.Map, map) && (map is null || (mapView is not null && ReferenceEquals(map.View, mapView)));
}
