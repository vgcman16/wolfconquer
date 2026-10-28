using WolfConquer.Core.Networking;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // One actual Shock issuance, captured before admission/transport can call out.
    // A removed/reissued holder never inherits this cast from its copied UID.
    private sealed class LegacyShockCasterOwner
    {
        private readonly WorldSessionStore? store;
        private readonly ConquerTcpConnection connection;
        private readonly long accountId, uid;
        private readonly string? usersRoot;
        private readonly LegacyNativeGameClientRuntime? client;
        private readonly LegacyNativePlayerRuntime? player;
        private readonly LegacyOrdinarySpellBinding? binding;
        internal object? Identity { get; }

        private LegacyShockCasterOwner(WorldPacketHandlerContext context, WorldSession expected, object? identity)
        {
            store = context.Sessions; connection = context.Connection; Identity = identity;
            accountId = expected.AccountId; uid = expected.Character.Uid; usersRoot = expected.UsersRoot;
            client = expected.NativeGameClient; player = client?.Player; binding = client?.OrdinarySpellBinding;
        }

        internal static bool TryCapture(WorldPacketHandlerContext context, WorldSession expected,
            out LegacyShockCasterOwner owner)
        {
            owner = null!;
            object? identity = null;
            if (context.Sessions is { } world && !LegacyNativeClientPublication.TryCaptureIdentity(
                    world, context.Connection, expected.Character.Uid, out identity)) return false;
            var captured = new LegacyShockCasterOwner(context, expected, identity);
            if (!captured.IsCurrent(context, expected)) return false;
            owner = captured; return true;
        }

        internal bool TryCurrent(WorldPacketHandlerContext context, WorldSession local, out WorldSession current)
        {
            current = local;
            if (!ReferenceEquals(context.Sessions, store) || !ReferenceEquals(context.Connection, connection)) return false;
            if (store is not null && (Identity is null ||
                !LegacyNativeClientPublication.TryCurrent(store, connection, uid, Identity, out current))) return false;
            return current.AccountId == accountId && current.Character.Uid == uid && current.UsersRoot == usersRoot &&
                ReferenceEquals(connection.NativeGameClient, client) && ReferenceEquals(current.NativeGameClient, client) &&
                (client is null || player is not null && ReferenceEquals(client.Player, player) &&
                    ReferenceEquals(player.Owner, client) && player.UID == uid && ReferenceEquals(client.OrdinarySpellBinding, binding) &&
                    (binding is null || store is not null && store.IsBoundSpellOwnerCurrent(binding)));
        }

        internal bool IsCurrent(WorldPacketHandlerContext context, WorldSession expected) =>
            TryCurrent(context, expected, out var current) && ReferenceEquals(current, expected);

        internal bool MatchesCapturedFileOwner(LegacySpellFileOwner captured) =>
            ReferenceEquals(captured.Context.Sessions, store) && ReferenceEquals(captured.Context.Connection, connection) &&
            captured.Expected.AccountId == accountId && captured.Expected.Character.Uid == uid && captured.Expected.UsersRoot == usersRoot &&
            ReferenceEquals(captured.Native, client) && ReferenceEquals(captured.Player, player) &&
            ReferenceEquals(client?.OrdinarySpellBinding, binding);

        internal bool TryPublish(WorldPacketHandlerContext context, WorldSession expected,
            WorldSession proposed, out WorldSession final)
        {
            final = expected;
            if (!IsCurrent(context, expected)) return false;
            if (store is null) { final = proposed; return true; } // Existing local-record component boundary only.
            if (!store.TryCompareAndSetWithNativeIdentity(connection, expected, Identity!, proposed, out var published) ||
                published is null) return false;
            final = published;
            return IsCurrent(context, final);
        }
    }

    private static async ValueTask<WorldSession?> TrySendLegacyShockRetaliationAsync(
        WorldPacketHandlerContext context, WorldSession session, LegacyPlayerMonsterMagicTarget target,
        LegacyShockCasterOwner owner, CancellationToken cancellationToken)
    {
        if (!owner.TryCurrent(context, session, out session)) return null;
        if (IsLegacyAttackingTargetExtraBoss(target.Monster))
            await TrySendLegacyMagicMonsterRetaliationAsync(context with { Session = session }, session,
                target.Monster, target.MonsterFamily, GetLegacyExtraBossSpellId(target.Monster), cancellationToken, owner)
                .ConfigureAwait(false);
        else
            await TrySendLegacyMonsterRetaliationAsync(context with { Session = session }, session,
                target.Monster, target.MonsterFamily, cancellationToken).ConfigureAwait(false);
        // Return the actual canonical record published by retaliation, under the
        // retained original issuer; never create a new issuer from this UID.
        return owner.TryCurrent(context, session, out var final) ? final : null;
    }

    private static async ValueTask<bool> SendLegacyShockRetaliationAnimationAsync(
        WorldPacketHandlerContext context, WorldSession session, LegacyMonsterInstance monster,
        LegacySpellAnimationPacket packet, LegacyShockCasterOwner owner, CancellationToken cancellationToken)
    {
        var frames = packet.ToFrames();
        foreach (var frame in frames)
        {
            if (!owner.IsCurrent(context, session)) return false;
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            if (!owner.TryCurrent(context, session, out session)) return false;
        }
        if (context.Sessions is not { } world) return true;
        // Native recipient traversal starts after the owner's send. Each selected
        // actual recipient gets its own issued token before any transport call.
        foreach (var active in world.Snapshot())
        {
            if (ReferenceEquals(active.Connection, context.Connection) || active.Session.Character.Uid == session.Character.Uid ||
                !CanSeeMonster(active.Session.Character, monster) || !LegacyNativeClientPublication.TryCaptureIdentity(
                    world, active.Connection, active.Session.Character.Uid, out var identity)) continue;
            foreach (var frame in frames)
            {
                if (!owner.IsCurrent(context, session)) return false;
                if (!LegacyNativeClientPublication.TryCurrent(world, active.Connection, active.Session.Character.Uid,
                        identity, out var recipient) || !CanSeeMonster(recipient.Character, monster)) break;
                await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                if (!owner.TryCurrent(context, session, out session)) return false;
            }
        }
        return true;
    }
}
