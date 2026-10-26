using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Native ClientItems retains an empty bank until the warehouse owner is rebuilt.
// This is transient presentation state, never permission or an item-file field.
internal sealed partial class LegacyWarehouseBankPresence
{
    private readonly ConquerTcpConnection _connection;
    private readonly object _identity;
    private readonly long _accountId;
    private readonly long _characterUid;
    private readonly LegacyNativeGameClientRuntime? _client;
    private readonly LegacyNativePlayerRuntime? _player;
    private readonly uint[] _banks;

    private LegacyWarehouseBankPresence(ConquerTcpConnection connection, object identity,
        WorldSession session, uint[] banks)
    {
        _connection = connection;
        _identity = identity;
        _accountId = session.AccountId;
        _characterUid = session.Character.Uid;
        _client = session.NativeGameClient;
        _player = _client?.Player;
        _banks = banks;
    }

    internal bool Contains(ConquerTcpConnection connection, object identity, WorldSession session, uint bank) =>
        Matches(connection, identity, session) && Array.IndexOf(_banks, bank) >= 0;

    private bool Matches(ConquerTcpConnection connection, object identity, WorldSession session) =>
        ReferenceEquals(_connection, connection) && ReferenceEquals(_identity, identity) &&
        _accountId == session.AccountId && _characterUid == session.Character.Uid &&
        ReferenceEquals(_client, connection.NativeGameClient) && ReferenceEquals(_client, session.NativeGameClient) &&
        ReferenceEquals(_player, _client?.Player) &&
        (_client is null || _player is not null && ReferenceEquals(_player.Owner, _client) && _player.UID == _characterUid);

    internal static bool TryPrepareTransfer(WorldSessionStore store, ConquerTcpConnection connection,
        object identity, WorldSession expected, WorldSession candidate, LegacyItemSaveGraph before,
        LegacyItemSaveGraph after, uint bank, uint itemUid, bool deposit, out Transfer transfer)
    {
        transfer = null!;
        if (bank == 0 || bank == 100 || expected.AccountId <= 0 || expected.Character.Uid is <= 0 or > uint.MaxValue ||
            !LegacyNativeClientPublication.TryCurrent(store, connection, expected.Character.Uid, identity, out var current) ||
            !ReferenceEquals(current, expected) || !before.IsCurrent(expected.PlayerItems) ||
            !after.IsCurrent(candidate.PlayerItems)) return false;
        // Actual captured rows prove the bank was used; eligibility/catalog data do not.
        var moved = deposit
            ? before.TryGetInventory(itemUid, out _) && after.TryGetWarehouse(bank, itemUid, out _)
            : before.TryGetWarehouse(bank, itemUid, out _) && after.TryGetInventory(itemUid, out _);
        if (!moved) return false;
        var prior = expected.WarehouseBankPresence;
        var retained = prior is not null && prior.Matches(connection, identity, expected);
        var presence = retained && Array.IndexOf(prior!._banks, bank) >= 0 ? prior!
            : new LegacyWarehouseBankPresence(connection, identity, expected,
                retained ? prior!._banks.Append(bank).ToArray() : [bank]);
        if (!presence.Matches(connection, identity, candidate)) return false;
        transfer = new Transfer(store, connection, identity, expected, candidate.PlayerItems, presence);
        return true;
    }
}
