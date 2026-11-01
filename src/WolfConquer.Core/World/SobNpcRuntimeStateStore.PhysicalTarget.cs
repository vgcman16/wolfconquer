using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class SobNpcRuntimeStateStore
{
    internal LegacyPhysicalTargetPresence ReadRegisteredPhysicalTarget(uint uid, int mapId,
        IReadOnlyList<SobNpc> adoptedRows, out SobNpc current)
    {
        current = null!;
        lock (_gate)
        {
            var matches = _states.Where(row => row.Key.Id == uid && row.Key.MapId == mapId).ToArray();
            if (matches.Length == 0) return LegacyPhysicalTargetPresence.Absent;
            if (matches.Length != 1) return LegacyPhysicalTargetPresence.Ambiguous;
            var state = matches[0].Value;
            if (state.DeadAt is not null || state.Hitpoints == 0 || state.Birth is not { } candidate ||
                adoptedRows.Count(row => ReferenceEquals(row, candidate)) != 1 ||
                adoptedRows.Count(row => row.MapId == mapId && row.Id == uid) != 1)
                return LegacyPhysicalTargetPresence.Unavailable;
            current = candidate with { Name = state.NameOverride ?? candidate.Name, Life = ToSeedHitpoints(state.Hitpoints),
                MaxLife = ToSeedHitpoints(state.MaxHitpoints), Lookface = state.MeshOverride ?? candidate.Lookface };
            return LegacyPhysicalTargetPresence.Live;
        }
    }
}
