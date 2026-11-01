# Actual monster physical range lifetime

## Identity and scope

Revision1, 2026-10-31; existing7899/VisibleDx8D13 Action260 continuation.
This joins the strict adopted family DATA to the existing Frame-owned monster
store. It is source-only, unbuilt and unrun; no7937 or live acceptance.

## Packet contract

No wire change. A physical target/range read cannot create, respawn or expire a
monster. Unknown, ambiguous, dead or unproven required targets refuse before
combat prefixes, output, cost, cooldown or replacement of an existing repeat.

## Catalog/table contract

The companion owned-physical-attack-range specification defines signed raw
attack_range, Boss byte, life Int32, loader Boss override3 and Copy override12.
The actual Frame configures its existing store once. Any already existing
component state remains untouched and receives no retrospective range proof.
Static state birth must match the complete configured materialized spawn.
Dynamic registration must retain the exact adopted family and actual complete
registered instance; synthetic fallback/changed family rows confer no proof.
GameMap.AddMapMonster1054 calls the adopted family's Copy before MobCollection
Add; normal MobCollection also uses a new family per role. This store owns the
resulting per-instance range, never the shared catalog row.

## Validation and transaction rules

Range evidence is captured only at actual state birth/registration. Failed or
unconfigured births remain usable by existing component APIs but gain no new
physical admission authority. A later reader never upgrades them.

The real Frame's private loop issues a current-loop token and single-use exact
instance/family/target phase; its time comes privately from that Frame's actual
configured TimeProvider. Beginning/ending a loop and consuming its phase share
the monster gate. Supplied timestamps, targets or constructor DTOs cannot issue
the phase. The real Frame's target-lock phase may enter AttackingTarget only for its
current target holder in the same map/dynamic scope and the existing retained
target UID. Under the monster gate, Boss exactly1 and current HP at least2m
sets this instance's range18, before attack cadence/range decisions. This
matches ActionHandler143-151. The value persists after HP decreases and across
the same role's respawn; death/respawn does not reconstruct the family.

Catalog replacement, family/raw-field drift, changed birth graph or a foreign
Frame/store refuses the new range owner. No reader uses the permissive uint
family parser as a substitute. Other mutable spawn/AI/persistence phases remain
separate; this does not claim global combat atomicity.

## Expected wire behavior and fixtures

Use actual Frame/catalog/spawn registration for birth, live HP/target-lock AI
for18, and independent signed range expectations. Cover initial12, Boss2 not18,
Boss1 transition18, retained18 below2m/respawn, read-only no state birth and
catalog/instance drift. No supplied accepted event, range callback or fake cache.

## Evidence alignment

| Requirement | Evidence | Status |
| --- | --- | --- |
| Per-role Copy | GameMap1054; normal MobCollection59 | Source observed |
| Exact18 mutation | ActionHandler.AttackingTarget143-151 | Source observed |
| Range reader | Actual existing registered state only | New closed join |
| Other dynamic overrides | Synthetic Instance fallback is not adopted metadata | Explicit remaining owner gap |

## Implementation and qualification

Lead owns MonsterRuntimeStateStore.PhysicalRange, actual Frame startup/AI joins
and real entity routing. Root owns strict family metadata and query range cache.
All new source/tests require Root compilation and execution; no passing claim.
