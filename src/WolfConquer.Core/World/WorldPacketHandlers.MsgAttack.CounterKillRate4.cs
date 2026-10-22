namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // CounterKill's Process phase only, not every attack/ProcescMagic/autoattack entry.
    // Resolve CURRENT Player AFTER prefix awaits from the originally captured physical CLIENT.
    private static bool TryAdvanceLegacyCounterKillProcessRate4(
        WorldPacketHandlerContext context, LegacyCounterKillCanUseOwner owner)
    {
        if (!IsLegacyCounterKillCanUseOwnerCurrent(context, owner) ||
            owner.PhysicalClient is not { } client ||
            !ReferenceEquals(context.Connection.NativeGameClient, client))
            return false;

        var advanced = false;
        void Consume(WorldSession current)
        {
            if (!ReferenceEquals(current.NativeGameClient, client))
                return;
            var player = client.Player;
            if (player is null || !ReferenceEquals(player.Owner, client))
                return; // A foreign/missing physical Player is not a replacement stream fallback.
            _ = player.Rate(4); // ONE parameterless MyRandom.Next; both native result branches empty.
            advanced = true;
        }

        if (owner.Store is { } world)
        {
            // SAME registration gate as Set/Remove; no await, send or late identity recapture.
            world.WithCurrentNativeClientIdentity(owner.Connection, owner.Uid, identity =>
            {
                if (ReferenceEquals(identity, owner.NativeClientIdentity) &&
                    world.TryGet(owner.Connection, out var current) && current is not null)
                    Consume(current);
            });
        }
        else if (context.Session is { } local && local.Character.Uid == owner.Uid)
        {
            Consume(local); // Explicit physical capsule only; no Store/UID/transport inference.
        }
        return advanced;
    }
}
