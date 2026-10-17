namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<LegacyEquipmentQueryPublicationResult> ClearLegacyPhysicalMonsterThundercloudsAsync(
        WorldPacketHandlerContext context, WorldSession session, LegacyEquipmentQueryPublicationScope scope,
        CancellationToken cancellationToken)
    {
        var current = RefreshLegacyEquipmentQueryPublication(context, session, scope);
        if (!current.Completed) return current;
        var capturedOwner = current.Session!;
        var map = capturedOwner.Character.MapId;
        var captured = ReadLivePets(capturedOwner.Character).Where(IsLegacyThundercloudLivePet).ToArray();
        var removeViewerCount = 0;
        foreach (var pet in captured)
        {
            current = RefreshLegacyEquipmentQueryPublication(context, current.Session!, scope);
            if (!current.Completed) return current;
            var x = pet.X ?? CheckedUInt16(capturedOwner.Character.X, nameof(capturedOwner.Character.X));
            var y = pet.Y ?? CheckedUInt16(capturedOwner.Character.Y, nameof(capturedOwner.Character.Y));
            var frame = LegacyRemoveEntityPacket.FromEntity(pet.Uid).ToFrameBytes();
            IReadOnlyList<WorldActiveSession> viewers = context.Sessions?.Snapshot() ??
                [new WorldActiveSession(context.Connection, current.Session!)];
            foreach (var candidate in viewers)
            {
                current = RefreshLegacyEquipmentQueryPublication(context, current.Session!, scope);
                if (!current.Completed) return current;
                var viewer = candidate.Session;
                if (context.Sessions is { } sessions &&
                    (!sessions.TryGet(candidate.Connection, out viewer) || viewer is null ||
                     viewer.Character.Uid != candidate.Session.Character.Uid))
                    continue;
                if (viewer is null || !CanSeeFloorItem(viewer.Character, map, x, y)) continue;
                await candidate.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                removeViewerCount++;
                current = RefreshLegacyEquipmentQueryPublication(context, current.Session!, scope);
                if (!current.Completed) return current;
            }
            context.FloorItems?.TryRemoveVisible(pet.Uid, out _);
            // Do not restore an old whole pet list after a callback/new acquisition.
            session = current.Session!;
            session = session with
            {
                Character = WithLegacyLivePetProjections(session.Character,
                    ReadLivePets(session.Character).Where(item => item.Uid != pet.Uid).ToArray()),
            };
            context.Sessions?.Set(context.Connection, session);
            current = RefreshLegacyEquipmentQueryPublication(context, session, scope);
            if (!current.Completed) return current;
        }
        if (captured.Length > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer removed legacy Thundercloud floor projections on owner death " +
                    $"{FormatSession(context)} ownerUid={capturedOwner.Character.Uid} " +
                    $"removedCount={captured.Length} removeViewerCount={removeViewerCount}; " +
                    "source=Player.cs death Thundercloud action 135 cleanup.")
                .ConfigureAwait(false);
            current = RefreshLegacyEquipmentQueryPublication(context, current.Session!, scope);
            if (!current.Completed) return current;
        }
        return current;
    }
}
