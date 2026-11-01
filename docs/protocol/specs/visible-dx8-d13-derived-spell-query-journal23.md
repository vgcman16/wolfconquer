# Query-derived spell Character/Spells journal23

## Identity and scope

Revision1,2026-10-31. Root/B-owned source implementation for existing7899/D13.
No new client packet/profile, deployment or live claim. Fix unmarked spell loss
under the separately documented preservation/real-origin policy (FSB264 OPEN).

## Packet contract

Existing spell Set and Action109 Remove bytes stay unchanged. One actual query
source phase commits before its own notification: rank21930 precedes later
MythSoul20000. Unmarked rows remain untouched and emit no destructive replacement.
One successful source Set at the same owned level may have identical file images;
an image no-op is not a detached writer/notification permission.

## Catalog/table contract

Exact operation keys: TargetUid UInt32, Source numeric enum(Rank1/MythSoul2),
Alternate bool, ItemsBefore non-null complete raw bytes, RankingRows non-null
ordered array of {CharacterId Int32,Fields string[]}, GrantedRow null or exact
{Id Int32,TypeId Int32,Name string,Fields string[]}. Rank uses its actual loaded
ordered ranking; MythSoul uses no ranking rows. The existing recorded selector,
rank rules and actual raw Magic decoder derive entitlement; caller levels are
never accepted as source evidence. GrantedRow is required only for actual Set.
Full read-only Items remains anchored to the configured current player file.
Finite existing image/record/depth and metadata/array/string bounds apply.

## Validation and transaction rules

- Only a private actual query-phase producer and its prepared writer issue the
  JournalOwner. Real current binding/query/provider/full images must match at
  every live commit boundary. The operation is historical DATA, not permission.
- Two ordered targets: Character(accountID), Spells(characterUID). Roots use
  the exact configured CharacterItemsSpellsDomain; Items is read-only. No UID
  allocation or item mutation. Unknown/duplicate/missing properties refuse.
- Reconstruct both complete afterimages from beforeimages through the strict
  preserved-unmarked/owned-origin reconciler. No HP or arbitrary scalar change.
- Root keys are Version/TransactionId/Changes/CharacterItemsSpellsDomain/
  DerivedSpellQuery, with Version23. Version8 retains its schema, with strict
  recorded-origin validation: no new/releveled claim, no retained row after its
  claim is dropped, and exact retained owned rows. Joint claim/row retirement is
  valid journal DATA; live retirement still requires the actual class/clear owner.
- Reuse the existing flushed preparing/pending/stage/replacement engine. Owned
  configured startup alone may recover23 with complete UID custody, source
  Items, account-to-UID mapping, ordered prefix, stage and exact pending proofs.
  Generic recovery refuses. Preserve existing completed ordinary UID history;
  recovery replays captured files, never a fresh grant/query/output/native event.
- All ordinary spell/admission/native publication paths must validate retained
  origins. A provider-owned temporary level cannot become permanent by silently
  dropping its origin. Unmarked progression stays available. An actual future
  permanent/admin supersession requires its own transaction capability.

## Expected wire behavior and fixtures

Actual query-issued positive and unmarked-preservation fixtures, prefix0/1/2,
flushed stages, stable second restart, history/UID invariance, metadata/owner
drift, exact opaque rows and malformed pending evidence are required. A pure
operation, marker, hash, frame adjacency or handmade positive pending record is
not a runtime owner. Preserve source wire order and postcommit no-refund behavior.
Version8 DATA checks also cover exact preservation/joint retirement, six invalid
claim transitions, and a retained pending record that drops only the claim. These
owned-file journal fixtures never create a live grant or class-reset capability.

## Evidence alignment

| Requirement | Evidence | Status |
| --- | --- | --- |
| Existing unmarked ownership ambiguity | Real GM/generic Spell.Add/save/XP/soul source | Confirmed source finding |
| Complete image reconciliation | Shared derived-origin model/raw projection | Authored, unrun |
| Real query issuer | Private query execution and actual prepared writer | B implementing |
| Typed durability/custody | Existing journal kernel and configured registry | Root implementing23 |

## Implementation and qualification

Root owns strict record/format/live boundary/configured recovery hooks. B owns
operation/provider derivation, real query writer/Store/native/reporting and
generic/admission/progression integration with genuine tests. Entire260 remains
SOURCE ONLY, UNBUILT/UNRUN; no parity, SQL, live or completed-fix claim.
