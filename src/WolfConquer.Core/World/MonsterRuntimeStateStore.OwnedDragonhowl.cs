using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class MonsterRuntimeStateStore
{
    // Captures an existing registration. Only the Store's registered, current
    // Dragonhowl phase can turn this witness into one actual damage operation.
    internal sealed class OwnedDragonhowlMonsterTarget
    {
        private readonly MonsterRuntimeStateStore owner;
        private readonly WorldServerFrameHandler frame;
        private readonly WorldDataCatalog catalog;
        private readonly OwnedMonsterRepeatTarget before;
        private readonly LegacyMonsterInstance birth;
        private readonly Monster family;
        private readonly MonsterRuntimeState state;
        private readonly object incarnation;
        private readonly MonsterPhysicalRangeState physical;
        private readonly MonsterSpawn? staticSpawn;
        private readonly IReadOnlyDictionary<string, string>? spawnAttributeSource;
        private readonly LegacyNativeAttributeMapProof? spawnAttributes;
        private readonly IReadOnlyList<SpecialDrop> dropSource;
        private readonly SpecialDrop[] drops;
        private bool attempted;
        private MonsterDamageResult? committedResult;
        private DamageAfter? committedAfter;

        private OwnedDragonhowlMonsterTarget(MonsterRuntimeStateStore owner,
            WorldServerFrameHandler frame, WorldDataCatalog catalog, OwnedMonsterRepeatTarget before,
            MonsterRuntimeState state, MonsterPhysicalRangeState physical, MonsterSpawn? staticSpawn)
        {
            this.owner = owner; this.frame = frame; this.catalog = catalog; this.before = before;
            this.state = state; this.physical = physical; this.staticSpawn = staticSpawn;
            birth = physical.Birth; family = physical.Family; incarnation = state.ClassicBuffIncarnation;
            spawnAttributeSource = staticSpawn?.Attributes;
            spawnAttributes = spawnAttributeSource is null ? null : LegacyNativeAttributeMapProof.CaptureRaw(spawnAttributeSource);
            dropSource = family.SpecialDrops; drops = dropSource.ToArray();
        }

        internal static OwnedDragonhowlMonsterTarget? TryCapture(MonsterRuntimeStateStore owner,
            WorldServerFrameHandler frame, WorldDataCatalog catalog, LegacyMonsterInstance observed, Monster family)
        {
            if (owner is null || frame is null || catalog is null || observed is null || family is null) return null;
            lock (owner._gate)
            {
                var before = OwnedMonsterRepeatTarget.TryCapture(owner, frame, catalog, observed, family);
                if (before is null || !before.TryReadCurrent(out var current, out var actualFamily, out _) ||
                    current != observed || !ReferenceEquals(actualFamily, family) ||
                    !owner._states.TryGetValue(MonsterRuntimeKey.From(current), out var state) ||
                    state.PhysicalRange is not { } physical) return null;
                MonsterSpawn? spawn = null;
                if (owner._dynamicMonstersByUid.TryGetValue(current.Uid, out var registered))
                {
                    if (!ReferenceEquals(registered.Instance, physical.Birth) ||
                        !ReferenceEquals(registered.MonsterFamily, family)) return null;
                }
                else if (!TryFindStaticBirth(catalog, physical.Birth, family, out spawn)) return null;
                var target = new OwnedDragonhowlMonsterTarget(owner, frame, catalog, before, state, physical, spawn);
                return target.RegistrationCurrentPlain() ? target : null;
            }
        }

        internal bool TryReadCurrent(out LegacyMonsterInstance current, out Monster actualFamily)
        {
            current = null!; actualFamily = null!;
            lock (owner._gate)
            {
                if (attempted || !RegistrationCurrentPlain() ||
                    !before.TryReadCurrent(out var observed, out var observedFamily, out _)) return false;
                current = observed; actualFamily = observedFamily; return true;
            }
        }

        internal bool TryCommit(WorldSessionStore.OwnedDragonhowlNonPlayer purpose, out MonsterDamageResult result)
        {
            result = default;
            // The token checks the real lifecycle/actor/phase gates. Never call
            // back into that owner while holding the monster gate in reverse order.
            if (purpose is null || Monitor.IsEntered(owner._gate) ||
                !purpose.TryReadMonsterBefore(this, out var expected, out var damage, out var now, out var actor) ||
                actor.Character.Uid is <= 0 or > uint.MaxValue) return false;
            string name = !string.IsNullOrWhiteSpace(actor.Character.Name) ? actor.Character.Name : actor.Username;
            if (string.IsNullOrWhiteSpace(name)) return false;
            uint actorUid = checked((uint)actor.Character.Uid);
            using var scope = before.TryEnterDamage();
            if (scope is null || attempted || !RegistrationCurrentPlain() ||
                !scope.TryReadCurrent(out var current, out var actualFamily, out _) || current != expected ||
                !ReferenceEquals(actualFamily, family)) return false;

            // Consume before the real mutation. A throwing mutation is uncertain,
            // never retryable through this target; the outer owner must fence it.
            attempted = true;
            var applied = owner.ApplyDamage(current, damage, now, actorUid, name);
            var after = DamageAfter.Capture(state);
            uint expectedDamage = Math.Min(damage, current.Hitpoints);
            if (applied.AlreadyDead || applied.RequestedDamage != damage || applied.DamageApplied != expectedDamage ||
                applied.RemainingHitpoints != current.Hitpoints - expectedDamage ||
                applied.Killed != (expectedDamage == current.Hitpoints) ||
                after.Hitpoints != applied.RemainingHitpoints ||
                after.DeadAt != (applied.Killed ? now : null))
                throw new InvalidOperationException("ownedDragonhowlMonsterDamageOutcomeUnproven");
            committedAfter = after;
            committedResult = applied;
            result = applied;
            return true;
        }

        internal bool IsCommittedCurrent()
        {
            lock (owner._gate)
            {
                return attempted && committedResult is { AlreadyDead: false } result &&
                    committedAfter is { } after && RegistrationCurrentPlain() &&
                    after == DamageAfter.Capture(state) && result.RemainingHitpoints == after.Hitpoints &&
                    result.Killed == (after.Hitpoints == 0 && after.DeadAt is not null);
            }
        }

        internal bool TryReadCommitted(out LegacyMonsterInstance after, out MonsterDamageResult result)
        {
            after = null!; result = default;
            lock (owner._gate)
            {
                if (!IsCommittedCurrent() || committedAfter is not { } image || committedResult is not { } actual) return false;
                after = birth with { X = image.X, Y = image.Y, Hitpoints = image.Hitpoints };
                result = actual; return true;
            }
        }

        internal bool TryCaptureRepeatSuccessor(out OwnedDragonhowlMonsterTarget? next,
            out LegacyMonsterInstance current, out Monster actualFamily)
        {
            next = null; current = null!; actualFamily = null!;
            lock (owner._gate)
            {
                // This reads a new alive image from the same registered birth,
                // incarnation and physical owner; it never reuses old hit HP as
                // permission or treats a replacement/respawn as its successor.
                if (!RegistrationCurrentPlain() || !before.TryReadCurrent(out var observed, out var foundFamily, out _) ||
                    !ReferenceEquals(foundFamily, family)) return false;
                var captured = TryCapture(owner, frame, catalog, observed, foundFamily);
                if (captured is null) return false;
                next = captured; current = observed; actualFamily = foundFamily; return true;
            }
        }

        internal static bool TryCaptureRepeatSuccessor(MonsterRuntimeStateStore owner,
            WorldServerFrameHandler frame, WorldDataCatalog catalog, OwnedMonsterRepeatTarget original,
            out OwnedDragonhowlMonsterTarget? next, out LegacyMonsterInstance current, out Monster actualFamily)
        {
            next = null; current = null!; actualFamily = null!;
            if (owner is null || frame is null || catalog is null || original is null) return false;
            lock (owner._gate)
            {
                // Keep the ordinary repeat's original incarnation alive through
                // capture. A new registration at the same numeric UID is not a
                // continuation of that repeat, even when its model is identical.
                if (!original.TryReadCurrent(out var observed, out var family, out _)) return false;
                var captured = TryCapture(owner, frame, catalog, observed, family);
                if (captured is null || !original.TryReadCurrent(out var rechecked, out var recheckedFamily, out _) ||
                    rechecked != observed || !ReferenceEquals(recheckedFamily, family)) return false;
                next = captured; current = observed; actualFamily = family; return true;
            }
        }

        // Unlike the alive target witness, this remains meaningful after lethal
        // ApplyDamage. Replacement births and even equal-valued respawns fail.
        private bool RegistrationCurrentPlain()
        {
            if (!ReferenceEquals(owner.physicalRangeFrame, frame) || !ReferenceEquals(owner.physicalRangeCatalog, catalog) ||
                !frame.IsMonsterPhysicalRangeStore(owner, catalog) ||
                !owner._states.TryGetValue(MonsterRuntimeKey.From(birth), out var actual) || !ReferenceEquals(actual, state) ||
                !ReferenceEquals(state.ClassicBuffIncarnation, incarnation) || !ReferenceEquals(state.PhysicalRange, physical) ||
                !physical.IsCurrentPlain(catalog, birth, family) || !ReferenceEquals(family.SpecialDrops, dropSource) ||
                !drops.SequenceEqual(dropSource)) return false;
            if (staticSpawn is null)
                return owner._dynamicMonstersByUid.TryGetValue(birth.Uid, out var registered) &&
                    ReferenceEquals(registered.Instance, birth) && ReferenceEquals(registered.MonsterFamily, family);
            return !owner._dynamicMonstersByUid.ContainsKey(birth.Uid) && spawnAttributes is not null &&
                ReferenceEquals(staticSpawn.Attributes, spawnAttributeSource) && spawnAttributes.IsCurrentPlain() &&
                TryFindStaticBirth(catalog, birth, family, out var current) &&
                ReferenceEquals(current, staticSpawn);
        }

        private static bool TryFindStaticBirth(WorldDataCatalog catalog, LegacyMonsterInstance birth,
            Monster family, out MonsterSpawn? spawn)
        {
            spawn = null;
            if (birth.DynamicId != 0 || !catalog.MonsterSpawnsByMapId.TryGetValue(birth.MapId, out var rows)) return false;
            try
            {
                var matches = LegacyStaticMonsterMaterializer.Enumerate(catalog, rows)
                    .Where(row => row.Instance.Uid == birth.Uid).Take(2).ToArray();
                if (matches.Length != 1 || matches[0].Instance != birth || !ReferenceEquals(matches[0].MonsterFamily, family))
                    return false;
                spawn = matches[0].Spawn; return true;
            }
            catch (Exception error) when (error is ArgumentException or InvalidDataException or InvalidOperationException or OverflowException)
            { return false; }
        }

        // Damage/lifecycle outcome only. This does not grant or acknowledge
        // ranking consumption, drops, rewards, status outputs, or persistence.
        private readonly record struct DamageAfter(ushort X, ushort Y, uint Hitpoints,
            DateTimeOffset? DeadAt, DateTimeOffset? NativeRespawnReadyAt, bool PendingRespawnBroadcast,
            bool DeathRemovalBroadcastSent, bool RespawnSuppressed, bool RemovedFromMapOnDeath)
        {
            internal static DamageAfter Capture(MonsterRuntimeState state) => new(state.X, state.Y, state.Hitpoints,
                state.DeadAt, state.NativeDeathRespawnReadyAt, state.PendingRespawnBroadcast,
                state.DeathRemovalBroadcastSent, state.RespawnSuppressed, state.RemovedFromMapOnDeath);
        }
    }
}
