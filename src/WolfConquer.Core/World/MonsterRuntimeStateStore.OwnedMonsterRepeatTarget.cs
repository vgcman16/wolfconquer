using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class MonsterRuntimeStateStore
{
    // Target witness only. It is not a hit receipt, repeat ticket or permission
    // to damage a monster. Dynamic births are anchored to this store's real
    // registration and the same incarnation used by the existing buff owner.
    internal sealed class OwnedMonsterRepeatTarget
    {
        private readonly MonsterRuntimeStateStore owner;
        private readonly WorldServerFrameHandler frame;
        private readonly WorldDataCatalog catalog;
        private readonly LegacyMonsterInstance birth;
        private readonly Monster family;
        private readonly MonsterRuntimeState state;
        private readonly object incarnation;
        private readonly MonsterSpawn? staticSpawn;
        private readonly KeyValuePair<string, string>[]? spawnAttributes;
        private readonly KeyValuePair<string, string>[] attributes;
        private readonly SpecialDrop[] drops;

        private OwnedMonsterRepeatTarget(MonsterRuntimeStateStore owner, WorldServerFrameHandler frame,
            WorldDataCatalog catalog, LegacyMonsterInstance birth, Monster family, MonsterRuntimeState state, MonsterSpawn? staticSpawn)
        {
            this.owner = owner; this.frame = frame; this.catalog = catalog;
            this.birth = birth; this.family = family; this.state = state;
            incarnation = state.ClassicBuffIncarnation;
            this.staticSpawn = staticSpawn;
            spawnAttributes = staticSpawn?.Attributes.OrderBy(row => row.Key, StringComparer.Ordinal).ToArray();
            attributes = family.Attributes.OrderBy(row => row.Key, StringComparer.Ordinal).ToArray();
            drops = family.SpecialDrops.ToArray();
        }

        internal static OwnedMonsterRepeatTarget? TryCapture(MonsterRuntimeStateStore owner,
            WorldServerFrameHandler frame, WorldDataCatalog catalog, LegacyMonsterInstance observed, Monster family)
        {
            lock (owner._gate)
            {
                if (!ReferenceEquals(owner.physicalRangeFrame, frame) || !ReferenceEquals(owner.physicalRangeCatalog, catalog) ||
                    !frame.IsMonsterPhysicalRangeStore(owner, catalog) ||
                    !owner._states.TryGetValue(MonsterRuntimeKey.From(observed), out var state) ||
                    state.DeadAt is not null || state.Hitpoints == 0 || state.RemovedFromMapOnDeath ||
                    state.PhysicalRange is not { } physical ||
                    !physical.IsCurrentPlain(catalog, observed, family)) return null;
                if (owner._dynamicMonstersByUid.TryGetValue(observed.Uid, out var registered))
                {
                    if (!ReferenceEquals(registered.MonsterFamily, family) || !ReferenceEquals(physical.Birth, registered.Instance)) return null;
                    return new(owner, frame, catalog, registered.Instance, family, state, null);
                }
                if (observed.DynamicId != 0 || !TryFindStatic(catalog, physical.Birth, family, out var spawn)) return null;
                return new(owner, frame, catalog, physical.Birth, family, state, spawn);
            }
        }

        internal bool TryReadCurrent(out LegacyMonsterInstance current, out Monster actualFamily, out int physicalRange)
        {
            current = null!; actualFamily = null!; physicalRange = 0;
            lock (owner._gate)
            {
                if (!ReferenceEquals(owner.physicalRangeFrame, frame) || !ReferenceEquals(owner.physicalRangeCatalog, catalog) ||
                    !frame.IsMonsterPhysicalRangeStore(owner, catalog) || !RegistrationCurrentPlain() ||
                    !owner._states.TryGetValue(MonsterRuntimeKey.From(birth), out var actual) || !ReferenceEquals(actual, state) ||
                    !ReferenceEquals(actual.ClassicBuffIncarnation, incarnation) || actual.DeadAt is not null ||
                    actual.Hitpoints == 0 || actual.RemovedFromMapOnDeath || actual.PhysicalRange is not { } physical ||
                    !physical.IsCurrentPlain(catalog, birth, family) ||
                    !attributes.SequenceEqual(family.Attributes.OrderBy(row => row.Key, StringComparer.Ordinal)) ||
                    !drops.SequenceEqual(family.SpecialDrops)) return false;
                current = birth with { X = actual.X, Y = actual.Y, Hitpoints = actual.Hitpoints };
                actualFamily = family; physicalRange = physical.Range;
                return true;
            }
        }

        private bool RegistrationCurrentPlain()
        {
            if (staticSpawn is null)
                return owner._dynamicMonstersByUid.TryGetValue(birth.Uid, out var registered) &&
                    ReferenceEquals(registered.Instance, birth) && ReferenceEquals(registered.MonsterFamily, family);
            return !owner._dynamicMonstersByUid.ContainsKey(birth.Uid) &&
                TryFindStatic(catalog, birth, family, out var current) && ReferenceEquals(current, staticSpawn) &&
                spawnAttributes is not null && spawnAttributes.SequenceEqual(staticSpawn.Attributes.OrderBy(row => row.Key, StringComparer.Ordinal));
        }

        private static bool TryFindStatic(WorldDataCatalog catalog, LegacyMonsterInstance birth, Monster family, out MonsterSpawn? spawn)
        {
            spawn = null;
            if (birth.DynamicId != 0 || !catalog.MonsterSpawnsByMapId.TryGetValue(birth.MapId, out var spawns)) return false;
            try
            {
                var matches = LegacyStaticMonsterMaterializer.Enumerate(catalog, spawns)
                    .Where(row => row.Instance.Uid == birth.Uid).Take(2).ToArray();
                if (matches.Length != 1 || !ReferenceEquals(matches[0].MonsterFamily, family) || matches[0].Instance != birth) return false;
                spawn = matches[0].Spawn; return true;
            }
            catch (Exception error) when (error is ArgumentException or InvalidDataException or InvalidOperationException or OverflowException)
            { return false; }
        }

        // The caller retains this only through the synchronous real damage
        // application. It cannot keep a Monitor across any output or XP await.
        internal DamageScope? TryEnterDamage()
        {
            var scope = new DamageScope(this);
            return scope.TryAcquire() ? scope : null;
        }

        internal sealed class DamageScope : IDisposable
        {
            private readonly OwnedMonsterRepeatTarget target;
            private bool held;
            internal DamageScope(OwnedMonsterRepeatTarget target) => this.target = target;
            internal bool TryAcquire()
            {
                if (held) return false;
                Monitor.Enter(target.owner._gate, ref held);
                try
                {
                    if (target.TryReadCurrent(out _, out _, out _)) return true;
                    Dispose(); return false;
                }
                catch { Dispose(); throw; }
            }
            internal bool TryReadCurrent(out LegacyMonsterInstance current, out Monster family, out int range)
            {
                if (held && Monitor.IsEntered(target.owner._gate))
                    return target.TryReadCurrent(out current, out family, out range);
                current = null!; family = null!; range = 0; return false;
            }
            public void Dispose() { if (held) { held = false; Monitor.Exit(target.owner._gate); } }
        }
    }
}
