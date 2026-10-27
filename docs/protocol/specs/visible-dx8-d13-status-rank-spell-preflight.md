# Status rank-spell preflight

## Identity and scope

- Revision 1, 2026-10-27; requirement owner: Root.
- Canonical public source baseline: 4b9762ccbcd362a1af208b3bb5b627e11dd7a90f.
- Existing typed MyCombatGear.Status component; no dispatcher or runtime adoption.
- Prevent rank bonuses preceding a required SpellStorage mutation refusal. Preserve
  existing arithmetic and valid spell notices. This is not a whole Status transaction.

## Packet contract

No new schema. Reuse real SpellStorage Add/Remove and their reviewed prepared
writers: learned spell opcode 2101 and spell-removal action 109 in opcode 2046.
Existing independent byte goldens remain unchanged. Actual delivery remains
unobservable through the current void Send interface. No client activation claim.

## Catalog/table contract

Existing CombatGearRankTable effect snapshot selects VRANK. Existing class checks,
rank selection, score/cap ordering, metadata and optional flag behavior remain.
The six rank A calls consume only CoreStrike, Parry, PAttack, PDefense and MDefense.
No new gameplay cap, coefficient, metadata owner or required catalog is introduced.

## Validation and transaction rules

1. Preserve existing selected-flag preparation and complete real rank query.
2. Resolve the genuine intended client's current SpellStorage and require its
   existing IsOwnedBy(client) identity check. Resolve actual Status and
   PerfectionStatus; capture client/player identity, UID/class and the seven scalar
   values consumed by the six rank A calls.
3. Prepare precisely the existing numeric results, including integer /100 and
   unchecked uint addition. Do not write fields or invent temporary runtime owners.
4. Perform the existing single rank-spell Add or conditional Remove first. Its
   preparation/storage refusal must precede any rank bonus store.
5. After that call and its notification callouts, recheck captured bindings,
   physical target references and consumed scalar values using plain reads only.
   If stale, return without subsequent Status work. A committed spell stays
   committed; this return is not an uncommitted refusal or rollback.
6. Store the seven prepared values directly into captured real owners in the
   original A field order, then continue existing Status work unchanged.

## Expected wire behavior and fixtures

- Use existing NativeSpellTestFixture, NativeCombatGearQueryScope and Status
  witnesses. Do not execute tiny native-buffer controls against old code.
- A genuine Archer weekly first-place rank supplies nonzero 500/500/300 bonuses.
  A malformed learned-spell tail or foreign current storage must not change those
  target fields, learned rows or queues on refusal.
- A real map comparer can replace/null a target or change a consumed scalar while
  the spell removal runs. The spell may commit, but stale target bonuses and all
  later Status work must stop. Preserve the external callback's own change.
- Valid removal retains an independent literal action frame and exact bonuses;
  existing Pirate add and equipment/formula goldens remain authoritative controls.

## Evidence alignment

| Requirement | Evidence | Status |
| --- | --- | --- |
| No fallible work in prepared rank stores | A switch and StatusConversions bodies | Observed source: primitive conversions/stores only |
| Current intended storage | SpellStorage.Preparation.IsOwnedBy | Existing genuine owner/slot API in composed Ninja package |
| Rank effects before old spell call | MyCombatGear.Status | Observed unsafe ordering |
| Notification may call out | SpellStorage.Mutation.Publication and GameClient.Send | Captured targets must be rechecked |
| Broader caller transactions | Callers.md | Explicitly unresolved; not fixed by this slice |

## Implementation and qualification

Production: MyCombatGear.Status.cs plus focused StatusRankBonus.cs partial.
Tests: additive StatusTests.SpellAdmission and StatusTests.SpellTargets partials.
Root alone reviews/composes with the frozen Ninja package and runs qualification.
No build/test/process/Git/live activity is authorized for this helper. Full run
40537900359 concerns source 4b9762cc, not these unintegrated postimages.

Limits: no whole concurrent snapshot, global crash safety, later equipment/18160/
selected-flag transaction, AirPower spending, HundredWeapons mutation/persistence
or multi-call Pirate skill transaction is certified. The typed component has no
clean production receiver/owner adoption proven by this bounded caller trace.
