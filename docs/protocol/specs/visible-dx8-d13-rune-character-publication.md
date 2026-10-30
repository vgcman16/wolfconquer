# VisibleDx8D13: exact Rune Character publication

## Identity and scope

- Revision 1, 2026-10-30; Root-authorized source correction, draft FSB-250.
- Target: existing VisibleDx8D13 owned Frame/Store Rune and container actions.
- Source input: current Root integration candidate, including FSB-246. This is a
  finite source overlay, not another checkout or an execution environment.
- Correct final live publication after the genuine three-file Rune journal has
  committed. Preserve ordinary HP persistence and existing v12-v15 recovery.

## Packet contract

No packet changes. Existing Rune action echoes, action109 retirement, grant,
equipment refresh and container response sequences remain authoritative. Publish
those prepared frames only after the actual Store/native state is coherent.

## Catalog/table contract

No new catalog. The existing actual Rune/Container evidence supplies recorded
operation dependencies and rechecks currentness at the existing writer boundary.

## Validation and transaction rules

The observed failure is a disagreement between two serializers: Rune journals
project only HP and managed origins onto exact captured Character bytes, whereas
the ordinary native HP proof inserts every scalar (including absent ExpBallUsed)
and preserves the old origins. Their complete images differ after a valid commit.

1. The actual prepared spell file writer issues a sealed typed publication proof
   before the writer. Capture the same actual Store, connection, expected session,
   candidate Character, native binding, saved spell baseline and prepared rows.
2. Require exactly one actual Rune or Container evidence capability. Validate the
   complete ordered Character/Items/Spells changes and derive their exact outcome
   from the current operation. Clone all images. Reject a foreign or wrong image.
3. A dedicated Store entry uses this proof only for that exact expected/candidate
   transaction. Require the exact proposed Items afterimage and losslessly
   projected Spells afterimage each time Store/HP preparation consumes it, including
   after native canonicalization. The actual native freeze copies groups, rows
   and deposits in order and preserves fields; spell freeze preserves row order.
   No WorldSession reference shortcut or membership-only comparison suffices.
   The ordinary Store/HP entry keeps its scalar serializer unchanged.
4. HP preparation uses the proof's exact Character before/after images, proving
   actual owner/path, current complete beforeimage and scalar/paid correspondence.
   It still captures the usual closed native and input witnesses.
5. The actual three-file writer requires the same proof, operation and complete
   changes before its existing journal. Only actual success marks that proof
   committed; interrupted/unproven outcomes keep the existing recovery fence.
6. Final publication requires that actual writer's matching commit, unchanged
   owner, journal-clear state and complete physical Character afterimage. Retain
   all native map/item/currency/alternate/input checks and publication fencing.

No HP-only acceptance, general boolean, accepting saver, second Character write,
normalization of the sparse input, fabricated owner, old-record reinterpretation
or new UID authority is introduced.

## Expected wire behavior and fixtures

- Existing sparse HP test must keep HP3000, omitted ExpBallUsed, full opaque text,
  old spell byte11 A5, coherent native ownership and six original output frames.
- Existing TransformIntent live Unequip/Drop and genuine interruption/restart
  cases retain exact HP/origin/Items/Spells images and UID ledger.
- Add actual owned native Frame setup followed by a real prepared spell
  writer negative: a wrong Character afterimage cannot obtain publication proof;
  no writer, journal, file/state mutation or success output follows.
- A second negative prepares a genuine typed proof, then supplies false success
  testimony without invoking its real journal writer. Even when HP is unchanged,
  the Store must fence and retire the holder before any native/holder stores;
  files, native graph and success outputs remain unchanged. This is deliberately
  invalid test input, not a positive persistence callback or permission owner.
- Two genuine-proof negative cases substitute Items or Spells while retaining
  the identical proposed Character. Both must refuse before the writer callback,
  native publication, WAL, files, recovery state or success outputs change.

## Evidence alignment

| Requirement | Evidence | Status |
| --- | --- | --- |
| Mismatched exact afterimages | lifecycle04.log; LegacyBoundHitpointsScalarWrite.Images; LegacyRuneSpellOrigins.ProjectCharacterImage | observed log and source derivation |
| Final fence remains required | Store.PersistedTransactions and SpellPublication | source contract |
| Sparse/opaque HP golden | LegacyOrdinarySpellCallerTests.NativeHitpoints | retained |
| Real transform current/restart intent | LegacyOwnedRuneGameplayTests.TransformIntent | retained, execution owned by Root |

## Implementation and qualification

Author only necessary isolated source copies and the focused new test. Root owns
review, integration and all execution. No builds/tests/runtime/API/Git/host or
legacy-client actions by the author. This source correction is unqualified until
Root runs the affected and appropriate integration gates; no deployment, SQL,
live-client or full reconstruction completion claim follows.
