using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record LegacyVendorParticipant(ConquerTcpConnection Connection, WorldSession Session,
        object Identity, LegacyNativeGameClientRuntime? Client, LegacyNativePlayerRuntime? Player,
        IReadOnlyDictionary<string, string> Attributes, KeyValuePair<string, string>[] Fields,
        Dictionary<string, string> OwnedFields);

    private static bool TryCaptureLegacyVendorParticipant(WorldPacketHandlerContext context,
        ConquerTcpConnection connection, WorldSession session, out LegacyVendorParticipant owner)
    {
        owner = null!;
        if (session is null || session.Character is null || session.AccountId <= 0 ||
            session.Character.Uid is <= 0 or > uint.MaxValue || session.Character.Money < 0 ||
            session.Character.ConquerPoints is < 0 or > uint.MaxValue ||
            context.Sessions is not { } sessions || session.Character.Attributes is not { } attributes ||
            !sessions.TryGet(connection, out var current) || !ReferenceEquals(current, session) ||
            !LegacyNativeClientPublication.TryCaptureIdentity(sessions, connection, session.Character.Uid, out var identity))
            return false;
        try
        {
            var fields = attributes.ToArray();
            var owned = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in fields)
                if (string.IsNullOrEmpty(field.Key) || field.Value is null || !owned.TryAdd(field.Key, field.Value)) return false;
            var client = connection.NativeGameClient;
            owner = new(connection, session, identity, client, client?.Player, attributes, fields, owned);
            if (LegacyVendorParticipantCurrent(context, owner) && LegacyVendorAttributesCurrent(owner)) return true;
        }
        catch (InvalidOperationException) { }
        owner = null!;
        return false;
    }

    private static bool LegacyVendorParticipantCurrent(WorldPacketHandlerContext context, LegacyVendorParticipant owner) =>
        context.Trades is { } trades && !trades.HasConnectionEnded(owner.Connection) &&
        !trades.Contains(owner.Connection) && !owner.Session.LegacyTradeActive &&
        context.Sessions is { } sessions &&
        LegacyNativeClientPublication.TryCurrent(sessions, owner.Connection, owner.Session.Character.Uid,
            owner.Identity, out var current) && ReferenceEquals(current, owner.Session) &&
        ReferenceEquals(owner.Session.NativeGameClient, owner.Client) &&
        ReferenceEquals(owner.Connection.NativeGameClient, owner.Client) &&
        (owner.Client is null || owner.Player is not null && ReferenceEquals(owner.Client.Player, owner.Player) &&
            ReferenceEquals(owner.Player.Owner, owner.Client) && owner.Player.UID == owner.Session.Character.Uid);

    private static bool LegacyVendorParticipantHasNoEscrow(LegacyVendorParticipant owner) =>
        LegacyTradeEscrowClaims.TryRead(owner.OwnedFields, out var claim, out _) && claim is null;

    private static bool LegacyVendorAttributesCurrent(LegacyVendorParticipant owner)
    {
        if (!ReferenceEquals(owner.Session.Character.Attributes, owner.Attributes) || owner.Attributes.Count != owner.Fields.Length)
            return false;
        var index = 0;
        try
        {
            foreach (var pair in owner.Attributes)
            {
                if (index == owner.Fields.Length || !string.Equals(pair.Key, owner.Fields[index].Key, StringComparison.Ordinal) ||
                    !string.Equals(pair.Value, owner.Fields[index].Value, StringComparison.Ordinal)) return false;
                index++;
            }
        }
        catch (InvalidOperationException) { return false; }
        return index == owner.Fields.Length;
    }
}
