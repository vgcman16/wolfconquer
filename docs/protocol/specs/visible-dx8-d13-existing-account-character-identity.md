# Existing account and character identity during ordinary admission

## Identity and scope

Revision1,2026-10-31; Chinese7899/VisibleDx8D13, Root-owned offline implementation.
An existing Character file belongs to an account; its saved UID names the player
and player-owned item/spell/proficiency/quest/gear files. These identities can
differ. The new-account creator retains its existing same-ID recipe.

## Packet contract

No packet change. Existing accepted-login key selects the account file. Native
player UID, world visibility and player-owned data use the validated saved UID.
No packet-supplied alternate UID or arbitrary filename selects another account.

## Catalog/table contract

Character: configured Users/<account>.ini, strict complete Character UID UInt32.
Other configured player files: <savedUID> with their existing kind/extension.
Existing version7 journal keeps its schema and explicit FileId per participant;
Character FileId is account, other target/input FileIds are the unchanged saved UID.
Version6 and authenticated new-character creation retain their previous rules.

## Validation and transaction rules

- Validate the exact account file, before/after UID equality and numeric domains.
- Scan configured canonical Character files for unique UID ownership; duplicate,
  malformed or foreign account mappings refuse. Recheck mapping/current images
  at preparation and every existing journal boundary, including startup recovery.
- Native prefix reads the actual account file while publishing its saved player UID.
- Capture and commit every player participant by the saved UID, including read-only
  inputs. The actual private admission plan/Store lease still owns publication.
- Preserve all existing character/item/spell normalization, no new UID allocation,
  and no output before the existing durable admission outcome. No placeholder owner.

## Expected wire behavior and fixtures

The genuine signed-creation/history plus preexisting second-character fixtures
deliberately use account != savedUID. They must reach actual ordinary admission,
then actual equipment query and death journal22/23/restart controls. Add negative
mapping/participant tests for duplicate UID, wrong account and foreign player file.
Existing same-ID admission and new-creation suites remain required neighbors.

## Evidence alignment

Observed failure: LegacyOrdinaryAdmissionPlan.TryPrepare explicitly rejects
raw.Uid != accountId; capture/permit/domain/native prefix also assume one ID.
Existing WorldSession, item/spell loaders, class mutation and22/23 already retain
separate AccountId/Character.Uid identities. File routing is an implementation
correction; no new client feature or7937 wire acceptance is inferred.

## Implementation and qualification

Source change is pending; no build/test/live acceptance or fixed claim. Actual
client/updater execution hold and all original data/routes/backups remain preserved.
