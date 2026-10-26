using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyWarehouseBankPresence
{
    // Short-lived proof held by the existing transfer plan, not by the retained
    // presence marker. It must pass again inside the persisted Store callback.
    internal sealed class Transfer
    {
        private readonly WorldSessionStore _store;
        private readonly ConquerTcpConnection _connection;
        private readonly object _identity;
        private readonly WorldSession _expected;
        private readonly PlayerItemSnapshot _candidateItems;
        internal LegacyWarehouseBankPresence Presence { get; }

        internal Transfer(WorldSessionStore store, ConquerTcpConnection connection, object identity,
            WorldSession expected, PlayerItemSnapshot candidateItems, LegacyWarehouseBankPresence presence)
        {
            _store = store;
            _connection = connection;
            _identity = identity;
            _expected = expected;
            _candidateItems = candidateItems;
            Presence = presence;
        }

        internal bool IsCurrent(WorldSession expected, WorldSession updated) =>
            ReferenceEquals(expected, _expected) && ReferenceEquals(updated.PlayerItems, _candidateItems) &&
            ReferenceEquals(updated.WarehouseBankPresence, Presence) &&
            Presence.Matches(_connection, _identity, expected) && Presence.Matches(_connection, _identity, updated) &&
            LegacyNativeClientPublication.TryCurrent(_store, _connection, expected.Character.Uid, _identity, out var current) &&
            ReferenceEquals(current, expected);
    }
}
