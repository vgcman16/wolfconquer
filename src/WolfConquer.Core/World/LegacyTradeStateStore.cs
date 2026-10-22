using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

// Handler-lifetime managed trade ownership, not a native Trade/GameClient identity facade.
// Currency claims are also saved atomically with the debit; this owner is only
// the live pair/window state, never authority for an unrecorded restart refund.
internal sealed class LegacyTradeStateStore
{
    internal object SyncRoot { get; } = new();
    private readonly Dictionary<ConquerTcpConnection, LegacyTradeParty> _parties =
        new(ReferenceEqualityComparer.Instance);
    private readonly ConditionalWeakTable<ConquerTcpConnection, object> _endedConnections = new();

    internal void MarkConnectionEnded(ConquerTcpConnection connection) =>
        _endedConnections.GetValue(connection, static _ => new object());

    internal bool HasConnectionEnded(ConquerTcpConnection connection) =>
        _endedConnections.TryGetValue(connection, out _);

    internal bool TryGet(ConquerTcpConnection connection, out LegacyTradeParty party) =>
        _parties.TryGetValue(connection, out party!);

    internal bool Contains(ConquerTcpConnection connection) => _parties.ContainsKey(connection);

    internal LegacyTradeParty Create(ConquerTcpConnection first, uint firstUid,
        ConquerTcpConnection second, uint secondUid)
    {
        var tradeId = Guid.NewGuid();
        var a = new LegacyTradeParty(first, firstUid, tradeId);
        var b = new LegacyTradeParty(second, secondUid, tradeId);
        a.IsRequester = true;
        a.Other = b;
        b.Other = a;
        _parties.Add(first, a);
        _parties.Add(second, b);
        return a;
    }

    internal void Remove(LegacyTradeParty party)
    {
        _parties.Remove(party.Connection);
        _parties.Remove(party.Other.Connection);
    }
}

internal sealed class LegacyTradeParty(ConquerTcpConnection connection, uint uid, Guid tradeId)
{
    internal ConquerTcpConnection Connection { get; } = connection;
    internal uint Uid { get; } = uid;
    internal Guid TradeId { get; } = tradeId;
    internal LegacyTradeParty Other { get; set; } = null!;
    internal bool IsRequester { get; set; }
    internal bool WindowOpen { get; set; }
    internal bool Confirmed { get; set; }
    internal long Money { get; set; }
    internal uint ConquerPoints { get; set; }
    internal Dictionary<uint, LegacyTradeOffer> Items { get; } = new();

    internal void ResetConfirmations()
    {
        Confirmed = false;
        Other.Confirmed = false;
    }
}

internal sealed record LegacyTradeOffer(PlayerItemSnapshotItem Item, byte[] CapturedBytes);
internal sealed record LegacyTradeSend(ConquerTcpConnection Connection, uint ExpectedUid, byte[] Frame);
