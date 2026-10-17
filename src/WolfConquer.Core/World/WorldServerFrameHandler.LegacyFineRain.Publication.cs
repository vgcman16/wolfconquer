using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private readonly record struct LegacyFineRainVectorState(
        bool FineRain1, bool FineRain2, string? Deadline1, string? Deadline2)
    {
        public static LegacyFineRainVectorState Capture(CharacterSnapshot character)
        {
            character.Attributes.TryGetValue(
                WorldPacketHandlers.RuntimeLegacyFineRain1ExpiresAtUtcAttribute, out var deadline1);
            character.Attributes.TryGetValue(
                WorldPacketHandlers.RuntimeLegacyFineRain2ExpiresAtUtcAttribute, out var deadline2);
            return new(
                LegacyPlayerDeathState.HasStatusFlag(character.Attributes, LegacyFineRain1StatusFlag),
                LegacyPlayerDeathState.HasStatusFlag(character.Attributes, LegacyFineRain2StatusFlag),
                deadline1, deadline2);
        }
    }

    private async ValueTask<(WorldSession? Session, int ViewerCount)> SendLegacyFineRainExpiryVectorAsync(
        ConquerTcpConnection connection,
        long ownerUid,
        WorldSession expired,
        CancellationToken cancellationToken)
    {
        var expected = LegacyFineRainVectorState.Capture(expired.Character);
        var viewers = 0;
        if (!TryRefreshLegacyPhysicalStatusRuntimeSubject(connection, ownerUid, out var current))
            return (null, viewers);
        if (LegacyFineRainVectorState.Capture(current.Character) != expected)
            return (current, viewers);

        var frame = LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
                CheckedUInt32(ownerUid, nameof(ownerUid)),
                LegacyPlayerDeathState.CreateStatusFlagVector(
                    expired.Character.Attributes, LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength))
            .ToFrameBytes();
        await connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        viewers++;
        if (!TryRefreshLegacyPhysicalStatusRuntimeSubject(connection, ownerUid, out current))
            return (null, viewers);
        if (LegacyFineRainVectorState.Capture(current.Character) != expected)
            return (current, viewers);

        foreach (var candidate in _sessions.Snapshot())
        {
            if (!TryRefreshLegacyPhysicalStatusRuntimeSubject(connection, ownerUid, out current))
                return (null, viewers);
            if (LegacyFineRainVectorState.Capture(current.Character) != expected)
                return (current, viewers);
            if (ReferenceEquals(candidate.Connection, connection) ||
                !_sessions.TryGet(candidate.Connection, out var viewer) || viewer is null ||
                viewer.Character.Uid != candidate.Session.Character.Uid || viewer.Character.Uid == ownerUid ||
                !CanSeePlayer(viewer.Character, current.Character))
                continue;

            await candidate.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            viewers++;
            if (!TryRefreshLegacyPhysicalStatusRuntimeSubject(connection, ownerUid, out current))
                return (null, viewers);
            if (LegacyFineRainVectorState.Capture(current.Character) != expected)
                return (current, viewers);
        }

        return (current, viewers);
    }
}
