using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record DragonSkinOwner(WorldSession Session, object Identity, LegacyDragonSkinBook Book,
        LegacyTradeRecoveryState? Recovery);

    private static bool TryCaptureDragonSkinOwner(WorldPacketHandlerContext context, WorldSession supplied,
        out DragonSkinOwner owner, out string reason)
    {
        owner = null!;
        reason = "dragonSkinCurrentOwnerUnavailable";
        if (context.Sessions is not { } sessions || supplied is null || supplied.Character is null ||
            supplied.AccountId is <= 0 or > uint.MaxValue || supplied.Character.Uid is <= 0 or > uint.MaxValue ||
            !ReferenceEquals(context.Session, supplied) || !sessions.TryGet(context.Connection, out var current) ||
            !ReferenceEquals(current, supplied) || !DragonSkinPhysicalOwnerMatches(context, supplied) ||
            !LegacyNativeClientPublication.TryCaptureIdentity(sessions, context.Connection, supplied.Character.Uid, out var identity)) return false;
        if (!LegacyDragonSkinBook.TryCapture(supplied.Character.Attributes, out var book, out reason)) return false;
        var captured = new DragonSkinOwner(supplied, identity, book, context.TradeRecovery);
        if (!IsCurrentDragonSkinOwner(context, captured, supplied))
        { reason = "dragonSkinCurrentOwnerChanged"; return false; }
        owner = captured;
        reason = string.Empty;
        return true;
    }

    private static bool IsCurrentDragonSkinOwner(WorldPacketHandlerContext context, DragonSkinOwner owner,
        WorldSession expected) => context.Sessions is { } sessions &&
        LegacyNativeClientPublication.TryCurrent(sessions, context.Connection, expected.Character.Uid, owner.Identity, out var current) &&
        ReferenceEquals(current, expected) && DragonSkinPhysicalOwnerMatches(context, expected);

    private static bool DragonSkinPhysicalOwnerMatches(WorldPacketHandlerContext context, WorldSession session)
    {
        var client = context.Connection.NativeGameClient;
        if (client is null) return session.NativeGameClient is null;
        return ReferenceEquals(session.NativeGameClient, client) && client.Player is { } player &&
            ReferenceEquals(player.Owner, client) && player.UID == session.Character.Uid;
    }

    private static bool TryCommitDragonSkinPresentation(WorldPacketHandlerContext context, DragonSkinOwner owner,
        WorldSession updated, out WorldSession finalSession, out string reason)
    {
        finalSession = null!;
        reason = "dragonSkinCurrentOwnerChanged";
        if (context.Sessions is not { } sessions || !IsCurrentDragonSkinOwner(context, owner, owner.Session) ||
            !owner.Book.IsCurrent(owner.Session.Character.Attributes)) return false;
        var changed = false;
        WorldSession? committed = null;
        try
        {
            changed = sessions.TryCompareAndSetWithNativeIdentity(context.Connection, owner.Session,
                owner.Identity, updated, out committed);
        }
        catch (WorldSessionPublicationException published)
        {
            // The actual Store published and attempted identity-scoped fencing.
            // Preserve its stage/cause/historical record even if an observer
            // replaced or reissued the current holder. Never recapture recovery.
            if (published.BelongsTo(sessions, context.Connection))
            {
                try { owner.Recovery?.Block("dragonSkinRecoveryRequired:presentationPostPublication"); }
                catch (Exception failure) { published.RecordFencingFailure(failure); }
            }
            throw;
        }
        catch (Exception error) when (LegacyDragonSkinPersistence.IsPreparationFailure(error))
        {
            if (committed is not null)
            {
                // Unexpected untyped failure after the actual out-final was
                // assigned is also not an unchanged preparation rejection.
                try { owner.Recovery?.Block("dragonSkinRecoveryRequired:presentationPostPublicationUntyped"); }
                catch (Exception failure)
                { throw new AggregateException("DragonSkin postpublication failure and recovery fencing both failed.", error, failure); }
                throw;
            }
            reason = "dragonSkinPresentationPreparation:" + error.GetType().Name;
            return false;
        }
        if (changed) { finalSession = committed!; reason = "committed"; }
        return changed;
    }

    private static Task LogDragonSkinRefusalAsync(WorldPacketHandlerContext context, string reason) =>
        context.Log.WriteLineAsync("WorldServer LegacyDragonSkin outcome=" +
            (reason.StartsWith(LegacyDragonSkinPersistence.RecoveryRequired, StringComparison.Ordinal)
                ? "indeterminate; recovery fenced"
                : "rejected without commit") + " reason=" + reason + ".");
}
