# VisibleDx8D13 v9 fixture: genuine active transform maximum

## Identity and scope
- Revision 1, 2026-10-28. Root retained test02's actual early prerequisite failure:
  the previous expectedHp InRange check received 100000. It did not reproduce
  the conditional v9 Character afterimage mismatch.
- New source-only fixture successor; original v9 package and production sources
  remain frozen. Root alone executes the genuine old-producer red/green run.

## Source cause
- CalculateRuntimeMaxHitpoints first checks actual active LegacyTransformState.
  Otherwise it returns max(currentHitpoints, computed, stored). The rune clamp
  calls that function with current HP and then Math.Min(current, maximum).
  Changing vitality/strength/class-level/gear metadata cannot produce a lower
  ordinary maximum at current HP 100000. The failure is not missing class data.
- Existing CalculateEquipmentRebuildMaxHitpoints omits that floor, but this
  follow-up does not change or reroute the frozen rune clamp to use it.

## Packet and catalog contract
- No production packet/schema changes. Preserve the existing real UnequipRune
  removal/echo/max/current/status/refine order and all prior exact assertions.
- The owned fixture gains an optional TransformInfo list. Default remains empty
  for every existing caller. Its real WorldDataCatalog receives the list through
  the existing transformInfo input and exposes the actual indexed row.
- The regression provides one explicit synthetic owned TransformInfo row using
  the existing supported DivineHare form: spell 1350, level 0, lookface 207,
  MaxLife 3000. This is fixture metadata, not client evidence or a new game rule.

## Ownership and precondition
- Preserve genuine Login, Frame/Store/binding/registry/PIN ownership. The existing
  actual Store.Set first durably sets and physically hydrates HP 100000.
- Read the actual catalog's indexed TransformInfo row, validate identity and
  positive values, and use the SAME LegacyTransformState.Apply routine consumed
  by the production transform handler. As that handler does for its metadata
  phase, retain prior current HP when publishing the legitimate transform state
  through the actual typed Store. No callback, accepting saver or native HP seed.
- Require the original binding current/unretired, HP still 100000 in Store,
  native and saved bytes, a live unexpired transform, catalog/state maximum 3000
  and actual CalculateRuntimeMaxHitpoints result 3000 before issuing the rune.
- Existing sparse optional ExpBallUsed=0 image, opaque comment/attribute, exact
  real three-file afterimages before first output, physical/Store/saved HP,
  original binding/recovery, resources and all six packet assertions remain.

## Qualification and limits
- New postimages replace only the regression and two shared test fixture files;
  new optional inputs preserve existing default fixture behavior. No production
  file is included. Root must first capture the actual regression against the old
  producer, then qualify the existing separate producer fix.
- Source-derived reachability is not an executed failure or pass. This fixture
  exercises the real rune v9 caller with an owned transform state; it does not
  claim a complete actual transform cast/client journey. Potion HP+Items,
  Team/death/revive, SQL/power loss and wider gameplay remain OPEN.

## Root ordinary-admission input correction
Actual test03 reached the Rune route but retained an earlier ordinary-login
EqualException: the test body decorated Character text after OrdinaryLoginFixture
had already captured the original bytes. This is not a clean producer failure.
Supply optional owned input lines during NativeItemSpellCallerFixture initialization
BEFORE constructing the real ordinary issuer/fixture; preserve default callers.
Require successful Login without runtime-exception/PARITY-BUG before testing Rune.
The source writer, valid wire assertions and original sparse/transform guards
remain unchanged. Root retains both earlier actual failed runs.
