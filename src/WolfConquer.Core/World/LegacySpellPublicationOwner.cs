using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Pins a real entered Store/native lifetime, not a copied UID or a send callback.
internal sealed class LegacySpellPublicationOwner
{
    private readonly WorldSessionStore sessions;
    private readonly LegacyTradeStateStore trades;
    private readonly ConquerTcpConnection connection;
    private readonly object identity;
    private readonly LegacyNativeGameClientRuntime? native;
    private readonly LegacyNativePlayerRuntime? player;
    private readonly long uid, account;
    private readonly LegacySpellPublicationPlan plan;

    private LegacySpellPublicationOwner(WorldSessionStore sessions, LegacyTradeStateStore trades,
        ConquerTcpConnection connection, object identity, WorldSession expected, LegacySpellPublicationPlan plan)
    { this.sessions = sessions; this.trades = trades; this.connection = connection; this.identity = identity;
      uid = expected.Character.Uid; account = expected.AccountId; this.plan = plan;
      native = connection.NativeGameClient; player = native?.Player; }

    internal static bool TryBind(WorldSessionStore sessions, LegacyTradeStateStore trades,
        ConquerTcpConnection connection, WorldSession expected, LegacySpellPublicationPlan plan,
        out LegacySpellPublicationOwner? owner)
    {
        owner = null;
        if (expected.AccountId is <= 0 or > uint.MaxValue || expected.Character.Uid is <= 0 or > uint.MaxValue ||
            trades.HasConnectionEnded(connection) || !sessions.TryGet(connection, out var current) ||
            !ReferenceEquals(current, expected) ||
            !LegacyNativeClientPublication.TryCaptureIdentity(sessions, connection, expected.Character.Uid, out var identity)) return false;
        var captured = new LegacySpellPublicationOwner(sessions, trades, connection, identity, expected, plan);
        if (!captured.IsCurrent()) return false;
        owner = captured;
        return true;
    }

    internal bool IsCurrent()
    {
        if (trades.HasConnectionEnded(connection) || !NativeOwnerIsCurrent() ||
            !LegacyNativeClientPublication.TryCurrent(sessions, connection, uid, identity, out var current) ||
            current.AccountId != account || !ReferenceEquals(current.NativeGameClient, native) ||
            !plan.Matches(current.PlayerSpells)) return false;
        // Learned collections are caller-owned and may execute code while read.
        // Recheck the actual holder identity after those reads: remove/recreate
        // with the identical session record still retires this publication owner.
        return LegacyNativeClientPublication.TryCurrent(sessions, connection, uid, identity, out var final) &&
            ReferenceEquals(current, final) && final.AccountId == account &&
            !trades.HasConnectionEnded(connection) && ReferenceEquals(final.NativeGameClient, native) &&
            NativeOwnerIsCurrent();
    }

    private bool NativeOwnerIsCurrent() => ReferenceEquals(connection.NativeGameClient, native) &&
        (native is null || player is { } actualPlayer && ReferenceEquals(native.Player, actualPlayer) &&
            ReferenceEquals(actualPlayer.Owner, native) && actualPlayer.UID == uid);

    internal async ValueTask<bool> PublishAsync(byte[]? trailingFrame, CancellationToken cancellationToken)
    {
        foreach (var frame in plan.Frames)
        {
            if (!IsCurrent()) return false;
            await connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            if (!IsCurrent()) return false;
        }
        if (!IsCurrent()) return false;
        if (trailingFrame is not null)
        {
            await connection.SendAsync(trailingFrame, cancellationToken).ConfigureAwait(false);
            if (!IsCurrent()) return false;
        }
        return true;
    }
}
