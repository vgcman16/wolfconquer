# VisibleDx8D13: teleport attribute preservation

## Identity and scope

Revision2026-11-01, Root requirement. Source base is the current
root-hit-proficiency-check02/candidate; the frozen character-attribute-scope
intake supplies LegacyCharacterAttributeMap as an explicit dependency. This
bounded correction concerns proposed teleport DATA, its previous-scope persistence
values, and the necessary WorldPacketHandlers player/monster scope readers. No native admission, receipt, callback authority,
packet schema, catalog row or SQL transaction is added.

The reducer currently forces OrdinalIgnoreCase and removes/re-adds canonical
owned keys. An Ordinal map containing unrelated Other/other loses a field;
ambiguous DynamicID/PDinamycID aliases can be collapsed and accepted. Preserve
supported raw map comparers, opaque fields and unique owned spelling instead.

## Packet contract

No wire changes. Existing teleport, prison correction, owner/view ordering and
byte goldens remain authoritative. This patch changes internal attribute keys,
not client fields. Existing scalar map/coordinate/history behavior, requested
missing-map fallback and one/two-transition prison behavior remain unchanged.

## Catalog/table contract

No catalog layout change. The existing real map/terrain lookup and target
profile continue to decide map existence and prison tile validity. The reducer
receives those booleans as historical DATA and cannot grant a teleport.

## Validation and transaction rules

- Capture raw attributes through the common exact-BCL proof. Accepted shapes
  remain exact Dictionary or ReadOnlyDictionary backed by exact Dictionary,
  using Ordinal, OrdinalIgnoreCase or EqualityComparer<string>.Default. Never
  enumerate an arbitrary map or invoke an arbitrary comparer fallback.
- Read DynamicID through unique case-insensitive owned-key resolution. Missing
  or malformed numeric text retains the existing reducer compatibility value0;
  this is not a new safety claim. Actual native owned scalar proofs remain strict.
- Update DynamicID and, on changed scope, PDinamycID via the same unique-owned
  Set operation. A duplicate operated alias refuses even when values agree.
  Untouched opaque keys, including case-distinct pairs, remain unchanged.
- Preserve the original map reference for a same-map/same-dynamic coordinate-only
  result. A PDinamycID field not operated on by that branch remains opaque;
  if a later prison correction operates on it, the entire Reduce call must refuse
  before ExecuteLegacyTeleportDestinationAsync publishes its first transition.
- Shared WorldPacketHandlers dynamic-scope reads must recognize a unique retained
  lowercase alias, refuse ambiguous owned aliases and preserve numeric fallback.
  The player-view comparator routes through that reader and retains its existing
  UInt32-overflow-to0 policy. Separate Frame/support-target readers are outside
  this bounded change.
- CreateLegacyCommittedTeleportPersistenceValues reads the unique retained
  PDinamycID spelling and still emits its canonical persisted key/value. The
  value is not dropped merely because its runtime spelling is lowercase.
- Reducer results and copied maps are DATA only. Actual Store/Frame/lease/file/
  native currentness proofs, expected-session rejection and successful output
  order remain unchanged. No equal-state adoption or accepting callback is added.

## Expected wire behavior and fixtures

Pure controls cover the three real comparers and direct/wrapped maps; changed
scope, coordinate-only identity, Twin City normalization, missing-map fallback,
prison correction, unique alias spelling, duplicate operated aliases and denied
callback-shaped map/comparer inputs. Existing canonicalization-enforcing reducer
fixture is revised to a valid unique-alias positive; its ambiguous input becomes
an explicit refusal rather than a successful normalization requirement.

Actual component controls use the existing destination executor with a real
WorldSessionStore and transport observer. Valid lowercase scope must survive
teleport and affect actual view/chat scope selection correctly. Ambiguous equal
values must refuse before destination publication/leave/output, including when
only the second prison transition would operate on PDinamycID. Existing actual floor-NPC controls also exercise token/file custody and the real
persistence-value generator, canonical saved previous-scope value, original
item/observer-leave/teleport order and ambiguity refusal before token/file/output
changes. No private DATA method is invoked via reflection and no production
seam is introduced for tests. Component state is not native ordinary-admission
or live client acceptance evidence.

## Evidence alignment

| Requirement | Evidence | Limit |
| --- | --- | --- |
| Attribute loss and owned alias collapse | LegacyTeleportDestinationReducer.CopyAttributes/SetCanonicalAttribute | Server DATA defect; no new client capture |
| Safe raw map/comparer and unique-key contract | Frozen LegacyCharacterAttributeMap plus actual raw proof | DATA only; not a commit owner |
| Pre-publication full reduction | ExecuteLegacyTeleportDestinationAsync calls Reduce before TryPublish | Does not close every NPC preflight transaction |
| Dynamic view alignment | WorldPacketHandlers.Visibility.Monsters private reader and callers | Separate Frame/support readers remain outside scope |

## Implementation and qualification

SOURCE ONLY, UNBUILT/UNRUN. Root alone integrates, compiles, tests and publishes.
Existing packet/history goldens and real lifetime/currentness guards must remain
unchanged. Record actual outcomes and the fixed-source bug in Root's integration
ledger; this specification is not a verified fix claim. The stricter reader's
map-capture allocation/performance is unmeasured. Broader late-effect callers,
whole NPC transactions, real SQL, host trust, client stability and full parity
remain separate frontiers. No original data, routes, live programs, saves,
backups, held mail or AV settings are changed.

## Root focused qualification - 2026-11-01

Build16 passed0errors/163warnings1m07s. Focused17 actual268P/1F/0skip over269
includes all21new teleport cases passed. The sole gear-fixture slot assertion was
corrected separately and passes in19/21. Root added the required Runtime import
to the component fixture. These remain component/public-NPC checks, not native
teleport/client/global transaction or allocation-performance acceptance.
