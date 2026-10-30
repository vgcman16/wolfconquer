# Owned native training weapons

Revision 1, 2026-10-30. Source-only implementation scope approved by Root.

## Target and evidence
Current real VisibleDx8D13 ordinary admission owns canonical item snapshots and seven native containers. Joined NativeUpdateSpell currently reads RightWeapon/LeftWeapon combat caches, which ordinary item admission does not build. Existing native training/proficiency call sequence and frame codecs remain unchanged. This closes that real consumer seam without activating full native combat or QueryEquipment.

## Owned projection
The actual Store-issued NativeSpellMutationLease issues a graph-owned TrainingWeaponsLease. It validates the exact current Store holder/binding/canonical snapshot under the existing Store gate, then owns client/player, spell/proficiency, Warehouse/Rune/relic/gear gates in existing order through synchronous training preparation and CommitJoined. It accepts no caller permission/writer callback. All scopes end before notifications.

Use lease.ExpectedSession.AlternateEquipmentActive to select canonical equipment positions 4/5 or 54/55. A missing slot contributes weapon ID zero. Multiple rows for an active slot refuse before progression. Each selected row must agree with the genuine current native Equipment map by physical owner, map key, native reference, UID, position and item ID. Require the complete last-published graph and exact canonical snapshot before and after callback-bearing spell/proficiency preparation. Do not initialize RightWeapon/LeftWeapon caches or replace Equipment.

## Training and wire behavior
Joined TrainingPlan derives its proficiency class from the projected full item ID divided by 1000, retaining original spell-associated then physical progression passes, rate checks, overflow refusal, frame order, Store publication and existing save/notification semantics. The direct unbound component path retains its explicit equipment-cache contract. No new client opcode/layout, weapon-category policy or full native combat authority is introduced.

## Validation and limits
Use genuine admitted test fixtures with saved 410009/420009 equipped roots before issuer publication. Preserve the actual Equipment object; include alternate, missing, ambiguous and changed-owner/current-state controls where meaningful. Root alone builds/runs/integrates. Public noncooperating raw writes remain outside lock guarantees and must be detected rather than adopted as authority. This spec and authored source alone are not live-client, deployment, complete parity or durable training-save proof.
