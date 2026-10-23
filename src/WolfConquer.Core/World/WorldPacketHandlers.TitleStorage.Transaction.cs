using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record LegacyTitleStoragePublication(
        byte[] Acknowledgement, byte[]? UseTitle, byte[] Spawn, LegacyTitleStoragePeer[] Peers,
        object? ClientIdentity);

    private sealed record LegacyTitleStoragePeer(WorldActiveSession Active, object ClientIdentity);

    private static bool IsRepresentableLegacyRuntimeTitle(LegacyRuntimeTitleType title) =>
        title.Id > 0 && title.SubId < 10_000 &&
        ((ulong)title.Id * 10_000UL) + title.SubId <= uint.MaxValue;

    private static CharacterSnapshot CanonicalizeLegacyTitleNumericAttributes(CharacterSnapshot character)
    {
        foreach (var key in new[] { "SpecialTitleID", "SpecialWingID", "SpecialTitleScore" })
            character = WithCharacterAttribute(character, key, ReadAttributeUInt64(character.Attributes, key));
        return character;
    }

    private static LegacyTitleStoragePublication PrepareLegacyTitleStoragePublication(
        WorldPacketHandlerContext context, WorldSession updated,
        LegacyTitleStoragePacket acknowledgement, LegacyTitleStoragePacket? useTitle)
    {
        var ack = acknowledgement.ToFrameBytes();
        var use = useTitle?.ToFrameBytes();
        var spawn = CreatePlayerSpawnPacket(updated, context.WorldData).ToFrameBytes();
        var peers = new List<LegacyTitleStoragePeer>();
        foreach (var active in context.Sessions!.Snapshot())
        {
            var uid = active.Session.Character.Uid;
            if (ReferenceEquals(active.Connection, context.Connection) || uid == updated.Character.Uid ||
                !CanSeePlayer(active.Session.Character, updated.Character) ||
                !LegacyNativeClientPublication.TryCaptureIdentity(context.Sessions, active.Connection, uid, out var peerIdentity) ||
                !LegacyNativeClientPublication.TryCurrent(context.Sessions, active.Connection, uid, peerIdentity, out var current) ||
                !ReferenceEquals(current, active.Session)) continue;
            peers.Add(new(active, peerIdentity));
        }
        object? identity = null;
        context.Sessions.WithCurrentNativeClientIdentity(context.Connection,
            updated.Character.Uid, current => identity = current);
        return new(ack, use, spawn, peers.ToArray(), identity);
    }

    private static bool TryCommitLegacyTitleStorage(WorldPacketHandlerContext context,
        WorldSession previous, WorldSession updated, LegacyTitleStoragePublication publication,
        CancellationToken cancellationToken,
        out string reason)
    {
        reason = "sessionStoreUnavailable";
        if (context.Sessions is not { } sessions) return false;
        if (context.TradeRecovery is not { } recovery || recovery.IsBlocked)
        { reason = "recoveryOwnerUnavailableOrBlocked"; return false; }
        if (publication.ClientIdentity is null) { reason = "sessionIdentityUnavailable"; return false; }
        if (string.IsNullOrWhiteSpace(updated.UsersRoot) || updated.AccountId <= 0 ||
            updated.Character.Uid != updated.AccountId)
        { reason = "characterStorageUnavailable"; return false; }
        var attributes = updated.Character.Attributes;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SpecialTitleID"] = ReadAttributeUInt64(attributes, "SpecialTitleID").ToString(CultureInfo.InvariantCulture),
            ["SpecialWingID"] = ReadAttributeUInt64(attributes, "SpecialWingID").ToString(CultureInfo.InvariantCulture),
            ["SpecialTitleScore"] = ReadAttributeUInt64(attributes, "SpecialTitleScore").ToString(CultureInfo.InvariantCulture),
        };
        if (attributes.TryGetValue("SpecialTitles", out var titles)) values["SpecialTitles"] = titles;
        var failure = "sessionChanged";
        bool Persist()
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (recovery.IsBlocked) { failure = "recoveryRequired"; return false; }
            bool sameIdentity = false;
            sessions.WithCurrentNativeClientIdentity(context.Connection, previous.Character.Uid,
                identity => sameIdentity = ReferenceEquals(identity, publication.ClientIdentity));
            if (!sameIdentity) { failure = "sessionIdentityChanged"; return false; }
            var saved = LegacyTitleStoragePersistence.TryPersist(previous, updated, values, out failure);
            if (!saved && failure.StartsWith("titleStorageRecoveryRequired", StringComparison.Ordinal))
                recovery.Block(failure);
            return saved;
        }
        bool committed = sessions.TryCommitSession(context.Connection, previous, updated, Persist);
        reason = committed ? "committed" : failure;
        return committed;
    }

    private static Task LogLegacyTitleStorageCommitFailureAsync(
        WorldPacketHandlerContext context, string reason)
    {
        var outcome = reason.StartsWith("titleStorageRecoveryRequired", StringComparison.Ordinal)
            ? "indeterminate; recovery fence blocked"
            : "rejected without commit";
        return context.Log.WriteLineAsync(
            $"WorldServer LegacyMsgTitleStorage persistence {outcome} {FormatSession(context)} reason={reason}.");
    }

    private static async ValueTask PublishLegacyTitleStorageAsync(WorldPacketHandlerContext context,
        LegacyTitleStoragePublication publication, CancellationToken cancellationToken)
    {
        // Disk and session already committed. Delivery errors never roll back or report rejection.
        if (!IsCurrentLegacyTitleStoragePublication(context, publication)) return;
        await context.Connection.SendAsync(publication.Acknowledgement, cancellationToken).ConfigureAwait(false);
        if (publication.UseTitle is { } useTitle)
        {
            if (!IsCurrentLegacyTitleStoragePublication(context, publication)) return;
            await context.Connection.SendAsync(useTitle, cancellationToken).ConfigureAwait(false);
        }
        foreach (var peer in publication.Peers)
        {
            if (!IsCurrentLegacyTitleStoragePublication(context, publication)) return;
            if (!LegacyNativeClientPublication.TryCurrent(context.Sessions!, peer.Active.Connection,
                peer.Active.Session.Character.Uid, peer.ClientIdentity, out var current) ||
                !ReferenceEquals(current, peer.Active.Session) ||
                !CanSeePlayer(current.Character, context.Session!.Character)) continue;
            await SendLegacyTeleportFrameAsync(context, peer.Active.Connection, publication.Spawn, cancellationToken).ConfigureAwait(false);
        }
        await context.Log.WriteLineAsync($"WorldServer persisted LegacyMsgTitleStorage {FormatSession(context)} keys=SpecialTitles,SpecialTitleID,SpecialWingID,SpecialTitleScore.")
            .ConfigureAwait(false);
    }

    private static bool IsCurrentLegacyTitleStoragePublication(WorldPacketHandlerContext context,
        LegacyTitleStoragePublication publication)
    {
        if (context.Sessions is not { } sessions || context.Session is not { } committed ||
            !sessions.TryGet(context.Connection, out var current) || !ReferenceEquals(current, committed)) return false;
        bool sameIdentity = false;
        sessions.WithCurrentNativeClientIdentity(context.Connection, committed.Character.Uid,
            identity => sameIdentity = ReferenceEquals(identity, publication.ClientIdentity));
        return sameIdentity && sessions.TryGet(context.Connection, out current) && ReferenceEquals(current, committed);
    }
}
