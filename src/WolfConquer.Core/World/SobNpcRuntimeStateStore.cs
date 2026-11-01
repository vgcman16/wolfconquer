using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class SobNpcRuntimeStateStore
{
    private readonly object _gate = new();
    private readonly Dictionary<SobNpcRuntimeKey, SobNpcRuntimeState> _states = new();

    public SobNpc GetVisible(SobNpc sobNpc)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        lock (_gate)
        {
            var state = GetOrCreateNoLock(sobNpc);
            return sobNpc with
            {
                Name = state.NameOverride ?? sobNpc.Name,
                Life = ToSeedHitpoints(state.Hitpoints),
                MaxLife = ToSeedHitpoints(state.MaxHitpoints),
                Lookface = state.MeshOverride ?? sobNpc.Lookface,
            };
        }
    }

    public uint GetOwnerGuildId(SobNpc sobNpc)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        lock (_gate)
        {
            return GetOrCreateNoLock(sobNpc).OwnerGuildId;
        }
    }

    public bool TryGetOwnerGuildId(SobNpc sobNpc, out uint ownerGuildId)
    {
        ownerGuildId = GetOwnerGuildId(sobNpc);
        return ownerGuildId != 0;
    }

    public void SetOwnerGuildId(SobNpc sobNpc, uint ownerGuildId)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        lock (_gate)
        {
            var state = GetOrCreateNoLock(sobNpc);
            state.AdvanceMutationVersion();
            state.OwnerGuildId = ownerGuildId;
        }
    }

    public void ResetGuildWarGate(
        SobNpc sobNpc,
        uint ownerGuildId,
        uint? maxHitpoints = null,
        int? closeMeshOverride = null,
        int? demolitionMeshOverride = null)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        lock (_gate)
        {
            var state = GetOrCreateNoLock(sobNpc);
            state.AdvanceMutationVersion();
            state.MaxHitpoints = maxHitpoints ?? state.SeedMaxHitpoints;
            state.Hitpoints = state.MaxHitpoints;
            state.DeadAt = null;
            state.MeshOverride = closeMeshOverride;
            state.NameOverride = null;
            state.OwnerGuildId = ownerGuildId;
            state.GuildWarGateDestroyedNotified = false;
            state.GuildWarGateDemolitionMeshOverride = demolitionMeshOverride;
        }
    }

    public bool TryMarkGuildWarGateDestroyed(SobNpc sobNpc)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        lock (_gate)
        {
            var state = GetOrCreateNoLock(sobNpc);
            if (state.GuildWarGateDestroyedNotified)
            {
                return false;
            }

            state.AdvanceMutationVersion();
            state.GuildWarGateDestroyedNotified = true;
            return true;
        }
    }

    public SobNpcDamageResult ApplyDamage(
        SobNpc sobNpc,
        uint requestedDamage,
        DateTimeOffset now)
    {
        return ApplyDamage(sobNpc, requestedDamage, now, deathMeshOverride: null);
    }

    public SobNpcDamageResult ApplyDamage(
        SobNpc sobNpc,
        uint requestedDamage,
        DateTimeOffset now,
        LegacySobNpcDeathBehavior deathBehavior)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        var deathMeshOverride = deathBehavior == LegacySobNpcDeathBehavior.GateDemolition
            ? LegacySobNpcEventRules.GetDefaultDemolitionMesh(sobNpc)
            : null;

        return ApplyDamage(sobNpc, requestedDamage, now, deathBehavior, deathMeshOverride);
    }

    public SobNpcDamageResult ApplyDamage(
        SobNpc sobNpc,
        uint requestedDamage,
        DateTimeOffset now,
        int? deathMeshOverride)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        var deathBehavior = IsLegacyTrainingObject(sobNpc)
            ? LegacySobNpcDeathBehavior.ResetHitpointsAndBroadcastDeathUpdates
            : LegacySobNpcDeathBehavior.PersistentDeath;

        return ApplyDamage(sobNpc, requestedDamage, now, deathBehavior, deathMeshOverride);
    }

    private SobNpcDamageResult ApplyDamage(
        SobNpc sobNpc,
        uint requestedDamage,
        DateTimeOffset now,
        LegacySobNpcDeathBehavior deathBehavior,
        int? deathMeshOverride)
    {
        lock (_gate)
        {
            var state = GetOrCreateNoLock(sobNpc);
            if (deathBehavior == LegacySobNpcDeathBehavior.GateDemolition &&
                state.GuildWarGateDemolitionMeshOverride is { } configuredDeathMeshOverride)
            {
                deathMeshOverride = configuredDeathMeshOverride;
            }

            if (state.DeadAt is not null)
            {
                if (deathMeshOverride is { } meshOverride)
                {
                    state.AdvanceMutationVersion();
                    state.MeshOverride = meshOverride;
                }

                return new SobNpcDamageResult(
                    DamageApplied: 0,
                    RemainingHitpoints: 0,
                    Killed: false,
                    AlreadyDead: true);
            }

            if (deathBehavior == LegacySobNpcDeathBehavior.NotDamageable)
            {
                return new SobNpcDamageResult(
                    DamageApplied: 0,
                    RemainingHitpoints: state.Hitpoints,
                    Killed: false,
                    AlreadyDead: false);
            }

            if (requestedDamage == 0)
            {
                return new SobNpcDamageResult(
                    DamageApplied: 0,
                    RemainingHitpoints: state.Hitpoints,
                    Killed: false,
                    AlreadyDead: false);
            }

            var damage = Math.Min(requestedDamage, state.Hitpoints);
            // Reject an unsupported state transition before changing HP. The
            // checked version also fails before any part of this mutation.
            if (deathBehavior is not (LegacySobNpcDeathBehavior.ResetHitpointsAndBroadcastDeathUpdates or
                LegacySobNpcDeathBehavior.GateDemolition or LegacySobNpcDeathBehavior.PersistentDeath))
                throw new ArgumentOutOfRangeException(nameof(deathBehavior), deathBehavior, "Unsupported SOB NPC death behavior.");
            state.AdvanceMutationVersion();
            state.Hitpoints -= damage;
            var killed = state.Hitpoints == 0;
            if (killed)
            {
                switch (deathBehavior)
                {
                    case LegacySobNpcDeathBehavior.ResetHitpointsAndBroadcastDeathUpdates:
                        state.Hitpoints = state.MaxHitpoints;
                        break;
                    case LegacySobNpcDeathBehavior.GateDemolition:
                        state.Hitpoints = state.MaxHitpoints;
                        state.MeshOverride = deathMeshOverride;
                        break;
                    case LegacySobNpcDeathBehavior.PersistentDeath:
                        state.DeadAt = now;
                        state.MeshOverride = deathMeshOverride;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(deathBehavior),
                            deathBehavior,
                            "Unsupported SOB NPC death behavior.");
                }
            }

            return new SobNpcDamageResult(
                damage,
                state.Hitpoints,
                killed,
                AlreadyDead: false);
        }
    }

    public void ResetHitpointsToMax(
        SobNpc sobNpc,
        string? visibleNameOverride = null,
        uint? ownerGuildId = null)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        lock (_gate)
        {
            var state = GetOrCreateNoLock(sobNpc);
            state.AdvanceMutationVersion();
            state.Hitpoints = state.MaxHitpoints;
            state.DeadAt = null;
            state.NameOverride = visibleNameOverride;
            state.GuildWarGateDestroyedNotified = false;
            if (ownerGuildId is { } guildId)
            {
                state.OwnerGuildId = guildId;
            }
        }
    }

    public bool ApplyDeathMesh(SobNpc sobNpc, int meshOverride)
    {
        ArgumentNullException.ThrowIfNull(sobNpc);

        lock (_gate)
        {
            var state = GetOrCreateNoLock(sobNpc);
            if (state.DeadAt is null)
            {
                return false;
            }

            state.AdvanceMutationVersion();
            state.MeshOverride = meshOverride;
            return true;
        }
    }

    private SobNpcRuntimeState GetOrCreateNoLock(SobNpc sobNpc)
    {
        var key = SobNpcRuntimeKey.From(sobNpc);
        if (!_states.TryGetValue(key, out var state))
        {
            var maxHitpoints = GetSeedMaxHitpoints(sobNpc);
            var hitpoints = Math.Min(GetSeedHitpoints(sobNpc), maxHitpoints);
            state = new SobNpcRuntimeState(hitpoints, maxHitpoints) { Birth = sobNpc };
            _states.Add(key, state);
        }

        return state;
    }

    private static bool IsLegacyTrainingObject(SobNpc sobNpc)
    {
        return sobNpc.MapId == 1039 && sobNpc.Kind is 21 or 22;
    }

    private static uint GetSeedHitpoints(SobNpc sobNpc)
    {
        return sobNpc.MapId == 1039 ? 10_000 : ToRuntimeHitpoints(sobNpc.Life);
    }

    private static uint GetSeedMaxHitpoints(SobNpc sobNpc)
    {
        return sobNpc.MapId == 1039 ? 10_000 : ToRuntimeHitpoints(sobNpc.MaxLife);
    }

    private static uint ToRuntimeHitpoints(int value)
    {
        return value <= 0 ? 0U : (uint)value;
    }

    private static int ToSeedHitpoints(uint value)
    {
        return value > int.MaxValue ? int.MaxValue : (int)value;
    }

    private sealed class SobNpcRuntimeState(uint hitpoints, uint maxHitpoints)
    {
        internal SobNpc? Birth { get; init; }

        // Actual mutations/reset operations advance this before touching any
        // field. A return to equal values cannot revive an earlier proc input.
        internal ulong MutationVersion { get; private set; }
        internal void AdvanceMutationVersion() => MutationVersion = checked(MutationVersion + 1);

        public uint Hitpoints { get; set; } = hitpoints;

        public uint SeedMaxHitpoints { get; } = maxHitpoints;

        public uint MaxHitpoints { get; set; } = maxHitpoints;

        public DateTimeOffset? DeadAt { get; set; }

        public int? MeshOverride { get; set; }

        public string? NameOverride { get; set; }

        public uint OwnerGuildId { get; set; }

        public bool GuildWarGateDestroyedNotified { get; set; }

        public int? GuildWarGateDemolitionMeshOverride { get; set; }
    }

    private readonly record struct SobNpcRuntimeKey(int Id, int MapId, int X, int Y, int SourceLine)
    {
        public static SobNpcRuntimeKey From(SobNpc sobNpc)
        {
            return new SobNpcRuntimeKey(
                sobNpc.Id,
                sobNpc.MapId,
                sobNpc.X,
                sobNpc.Y,
                sobNpc.SourceLine);
        }
    }
}

public readonly record struct SobNpcDamageResult(
    uint DamageApplied,
    uint RemainingHitpoints,
    bool Killed,
    bool AlreadyDead);
