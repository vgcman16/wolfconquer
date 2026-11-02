using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class SobNpcRuntimeStateStore
{
    internal SobNpc ReadMagicPrimaryVisible(SobNpc source)
    {
        ArgumentNullException.ThrowIfNull(source);
        lock (_gate)
        {
            _states.TryGetValue(SobNpcRuntimeKey.From(source), out var current);
            return source with
            {
                Name = current?.NameOverride ?? source.Name,
                Life = ToSeedHitpoints(current?.Hitpoints ?? Math.Min(GetSeedHitpoints(source), GetSeedMaxHitpoints(source))),
                MaxLife = ToSeedHitpoints(current?.MaxHitpoints ?? GetSeedMaxHitpoints(source)),
                Lookface = current?.MeshOverride ?? source.Lookface,
            };
        }
    }

    // A view may be a record copy. Resolve the genuine catalog producer and
    // read without creating a state row from that copy. Invalid is not unowned.
    internal bool TryReadMagicPrimaryOwnerGuildId(WorldDataCatalog data, SobNpc observed, out uint ownerGuildId)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(observed);
        ownerGuildId = 0;
        if (!data.TryGetOwnedSobRowsById(observed.Id, out var rows)) return false;
        var key = SobNpcRuntimeKey.From(observed);
        var sources = rows.Where(row => SobNpcRuntimeKey.From(row) == key).ToArray();
        if (sources.Length != 1) return false;
        var source = sources[0];
        lock (_gate)
        {
            _states.TryGetValue(key, out var current);
            if (current is not null && !ReferenceEquals(current.Birth, source) ||
                ReadMagicPrimaryVisible(source) != observed) return false;
            // Zero is the genuine initial owner of an absent state, not the
            // fallback result of an invalid/stale query.
            ownerGuildId = current?.OwnerGuildId ?? 0;
            return true;
        }
    }

    // Read-only input observation. This neither creates a runtime row nor
    // authorizes a resource publication. The actual admission writer must bind it.
    internal bool TryCaptureMagicPrimaryTarget(SobNpc source, out MagicPrimaryTarget? target)
    {
        ArgumentNullException.ThrowIfNull(source);
        target = null;
        if (Monitor.IsEntered(_gate) || !Monitor.TryEnter(_gate)) return false;
        try
        {
            _states.TryGetValue(SobNpcRuntimeKey.From(source), out var current);
            if (current is not null && !ReferenceEquals(current.Birth, source)) return false;
            var visible = source with
            {
                Name = current?.NameOverride ?? source.Name,
                Life = ToSeedHitpoints(current?.Hitpoints ?? Math.Min(GetSeedHitpoints(source), GetSeedMaxHitpoints(source))),
                MaxLife = ToSeedHitpoints(current?.MaxHitpoints ?? GetSeedMaxHitpoints(source)),
                Lookface = current?.MeshOverride ?? source.Lookface,
            };
            target = new(this, source, current, current?.MutationVersion ?? 0, visible);
            return true;
        }
        finally { Monitor.Exit(_gate); }
    }

    internal sealed class MagicPrimaryTarget
    {
        private readonly SobNpcRuntimeStateStore owner;
        private readonly object? state;
        private readonly ulong version;
        internal SobNpc Source { get; }
        internal SobNpc Visible { get; }

        internal MagicPrimaryTarget(SobNpcRuntimeStateStore owner, SobNpc source,
            object? state, ulong version, SobNpc visible)
        {
            this.owner = owner; Source = source; this.state = state;
            this.version = version; Visible = visible;
        }

        internal bool TryEnter(out MagicPrimaryLease? lease)
        {
            lease = null;
            if (Monitor.IsEntered(owner._gate) || !Monitor.TryEnter(owner._gate)) return false;
            try
            {
                if (!IsCurrentHeld()) { Monitor.Exit(owner._gate); return false; }
                lease = new(this, owner._gate);
                return true;
            }
            catch { Monitor.Exit(owner._gate); throw; }
        }

        internal bool IsCurrentHeld()
        {
            if (!Monitor.IsEntered(owner._gate)) return false;
            if (!owner._states.TryGetValue(SobNpcRuntimeKey.From(Source), out var current))
                return state is null;
            return ReferenceEquals(current, state) && ReferenceEquals(current.Birth, Source) &&
                current.MutationVersion == version;
        }
    }

    internal sealed class MagicPrimaryLease(MagicPrimaryTarget target, object gate) : IDisposable
    {
        private bool disposed;
        private LegacyGuildWarRuntimeStateStore.PermissionLease? guildPermission;
        internal void AttachGuildPermission(LegacyGuildWarRuntimeStateStore.PermissionLease lease)
        {
            ArgumentNullException.ThrowIfNull(lease);
            if (disposed || guildPermission is not null || !target.IsCurrentHeld() || !lease.IsCurrentPlain())
                throw new InvalidOperationException("sobGuildPermissionLeaseInvalid");
            guildPermission = lease;
        }
        internal bool IsCurrentPlain() => !disposed && target.IsCurrentHeld() &&
            (guildPermission is null || guildPermission.IsCurrentPlain());
        public void Dispose()
        {
            if (disposed) return;
            if (!Monitor.IsEntered(gate))
                throw new SynchronizationLockException("SOB primary lease must be released by its owning thread.");
            disposed = true;
            try { guildPermission?.Dispose(); }
            finally { Monitor.Exit(gate); }
        }
    }
}
