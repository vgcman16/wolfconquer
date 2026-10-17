using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class TeamSessionStateStore
{
    internal bool TryGetLegacyMemberInfo(
        ConquerTcpConnection connection,
        long ownerUid,
        out LegacyTeamMemberInfoEntry? entry)
    {
        ArgumentNullException.ThrowIfNull(connection);
        entry = null;
        lock (_gate)
        {
            if (!_states.TryGetValue(connection, out var state)) return false;
            var member = state.Team.Members.FirstOrDefault(candidate =>
                ReferenceEquals(candidate.Connection, connection) && candidate.Uid == ownerUid);
            if (member?.LegacyInfo is not { } captured || captured.Uid != ownerUid) return false;
            entry = captured;
            return true;
        }
    }

    internal bool InitializeLegacyMemberInfo(
        ConquerTcpConnection connection,
        long ownerUid,
        LegacyTeamMemberInfoEntry entry)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Uid != ownerUid) return false;
        lock (_gate)
        {
            if (!_states.TryGetValue(connection, out var state)) return false;
            var member = state.Team.Members.FirstOrDefault(candidate =>
                ReferenceEquals(candidate.Connection, connection) && candidate.Uid == ownerUid);
            if (member is null) return false;
            member.LegacyInfo = entry;
            return true;
        }
    }

    internal IReadOnlyList<(ConquerTcpConnection Connection, long Uid)> SnapshotLegacyMemberRecipients(
        ConquerTcpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        lock (_gate)
        {
            return _states.TryGetValue(connection, out var state)
                ? state.Team.Members.Select(member => (member.Connection, member.Uid)).ToArray()
                : Array.Empty<(ConquerTcpConnection Connection, long Uid)>();
        }
    }

    internal bool TryUpdateLegacyMemberHitpoints(
        ConquerTcpConnection connection,
        long ownerUid,
        uint maximum,
        int value,
        out LegacyTeamMemberInfoEntry? entry)
    {
        ArgumentNullException.ThrowIfNull(connection);
        entry = null;
        lock (_gate)
        {
            if (!_states.TryGetValue(connection, out var state)) return false;
            var member = state.Team.Members.FirstOrDefault(candidate =>
                ReferenceEquals(candidate.Connection, connection) && candidate.Uid == ownerUid);
            if (member?.LegacyInfo is not { } prior) return false;
            entry = prior with
            {
                MaxHitpoints = unchecked((ushort)maximum),
                MinHitpoints = unchecked((ushort)value),
            };
            member.LegacyInfo = entry;
            return true;
        }
    }
}
