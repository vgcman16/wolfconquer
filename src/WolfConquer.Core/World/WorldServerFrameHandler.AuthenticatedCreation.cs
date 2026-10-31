using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private readonly ConditionalWeakTable<ConquerTcpConnection, CreationConnectionLifetime> _creationLifetimes = new();
    private readonly ConditionalWeakTable<ConquerTcpConnection, AuthenticatedCreationRedemption> _creationRedemptions = new();

    private sealed class CreationConnectionLifetime
    {
        private int ended;
        internal Func<Task> CloseCleanup { get; }
        internal CreationConnectionLifetime(WorldServerFrameHandler owner, ConquerTcpConnection connection)
        {
            CloseCleanup = () =>
            {
                End();
                owner.RetireEndedAuthenticatedCreation(connection);
                return owner.CancelTradeAfterDisconnectAsync(owner.CreateTradeDisconnectContext(connection), transportClosed: true);
            };
        }
        internal bool IsCurrent(ConquerTcpConnection connection) => Volatile.Read(ref ended) == 0 &&
            connection.IsTransportCloseCleanupCurrent(CloseCleanup);
        internal bool IsClosed(ConquerTcpConnection connection) => Volatile.Read(ref ended) != 0 &&
            connection.IsTransportCloseCleanupClosed(CloseCleanup);
        internal void End() => Interlocked.Exchange(ref ended, 1);
    }

    // This object is useful only while installed by the actual successful
    // redemption branch in this Frame's private table. Public ticket/result
    // records, or an independently constructed object, cannot install it.
    internal sealed class AuthenticatedCreationRedemption
    {
        private readonly WorldServerFrameHandler owner;
        private readonly AccountHandoffService handoff;
        private readonly AccountHandoffTicket ticket;
        private readonly CreationConnectionLifetime lifetime;
        private readonly LegacyNativeGameClientRuntime native;
        private readonly LegacyNativePlayerRuntime player;
        internal ConquerTcpConnection Connection { get; }
        internal uint AccountId => checked((uint)ticket.AccountId);
        internal string Username => ticket.Username;
        internal string TicketDigest { get; }

        private AuthenticatedCreationRedemption(WorldServerFrameHandler owner, ConquerTcpConnection connection,
            AccountHandoffService handoff, AccountHandoffTicket ticket, CreationConnectionLifetime lifetime)
        {
            this.owner = owner; Connection = connection; this.handoff = handoff;
            this.ticket = ticket; this.lifetime = lifetime;
            native = connection.NativeGameClient!; player = native.Player;
            TicketDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ticket.Token))).ToLowerInvariant();
        }

        internal static AuthenticatedCreationRedemption Capture(WorldServerFrameHandler owner,
            ConquerTcpConnection connection, AccountHandoffService handoff, AccountHandoffTicket ticket)
        {
            if (!ReferenceEquals(owner._handoffService, handoff) || ticket.AccountId is <= 0 or > uint.MaxValue ||
                !ticket.IsRedeemed || string.IsNullOrWhiteSpace(ticket.Username) ||
                !Guid.TryParseExact(ticket.Token, "N", out _) ||
                !owner._creationLifetimes.TryGetValue(connection, out var lifetime) || !lifetime.IsCurrent(connection) ||
                !owner.IsPreCharacterConnection(connection))
                throw new InvalidDataException("Authenticated creation requires the actual redeemed pre-character connection.");
            return new(owner, connection, handoff, ticket, lifetime);
        }

        internal bool IsCurrent(LegacyOwnedAdmissionRuntime runtime, WorldServerFrameHandler actualFrame) =>
            ReferenceEquals(owner, actualFrame) && ReferenceEquals(owner._ownedAdmissionRuntime, runtime) &&
            ReferenceEquals(owner._handoffService, handoff) &&
            owner._creationRedemptions.TryGetValue(Connection, out var current) && ReferenceEquals(current, this) &&
            owner._creationLifetimes.TryGetValue(Connection, out var currentLifetime) && ReferenceEquals(currentLifetime, lifetime) &&
            lifetime.IsCurrent(Connection) && ReferenceEquals(Connection.NativeGameClient, native) &&
            ReferenceEquals(native.Player, player) && ReferenceEquals(player.Owner, native) && player.UID == 0 &&
            owner.IsPreCharacterConnection(Connection);

        internal bool IsClosed(LegacyOwnedAdmissionRuntime runtime, WorldServerFrameHandler actualFrame) =>
            ReferenceEquals(owner, actualFrame) && ReferenceEquals(owner._ownedAdmissionRuntime, runtime) &&
            owner._creationRedemptions.TryGetValue(Connection, out var current) && ReferenceEquals(current, this) &&
            owner._creationLifetimes.TryGetValue(Connection, out var currentLifetime) && ReferenceEquals(currentLifetime, lifetime) &&
            lifetime.IsClosed(Connection);
    }

    private bool IsPreCharacterConnection(ConquerTcpConnection connection) =>
        // Store.TryGet is the existing CWT lookup + SessionHolder.Volatile.Read;
        // it acquires no Store connection/native publication gate.
        !_sessions.TryGet(connection, out _) && connection.HasSendRoute && connection.HasDisconnectRoute &&
        connection.NativeGameClient is { } native && native.Player is { UID: 0 } player &&
        ReferenceEquals(player.Owner, native) && native.Inventory is null && native.Equipment is null && native.Rune is null;

    private bool TryRegisterAuthenticatedCreation(ConquerTcpConnection connection, AccountHandoffTicket ticket,
        out string reason)
    {
        reason = "authenticatedCreationRuntimeRequired";
        if (_ownedAdmissionRuntime is not { } runtime || _handoffService is not { } handoff ||
            _protocolProfile != ConquerProtocolProfile.VisibleDx8D13) return false;
        if (_creationRedemptions.TryGetValue(connection, out _))
        { reason = "authenticatedCreationPermitAlreadyRegistered"; return false; }
        var redemption = AuthenticatedCreationRedemption.Capture(this, connection, handoff, ticket);
        _creationRedemptions.Add(connection, redemption);
        if (!runtime.TryPrepareAuthenticatedCreation(this, redemption, out reason))
        { _creationRedemptions.Remove(connection); return false; }
        _pendingCreations.Remove(connection);
        _pendingCreations.Add(connection, new(ticket.AccountId, Authenticated: true));
        return true;
    }

    internal static bool TryNormalizeAuthenticatedCreationName(string raw, out string name) =>
        TryNormalizeLegacyNewRoleName(raw, out name);

    private void RetireEndedAuthenticatedCreation(ConquerTcpConnection connection)
    {
        if (_ownedAdmissionRuntime is { } runtime && _creationRedemptions.TryGetValue(connection, out var redemption))
            runtime.RetireEndedAuthenticatedCreation(this, redemption);
    }
}
