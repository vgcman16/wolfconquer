# server-native-item-bag-owner: reuse the real synchronized collection owner

## Identity and scope
- Revision1, 2026-10-29; Root-authorized source-only successor, reserve FSB-202.
- Clean public233bd0d0ffcbdf131c638dd03b9f49fff76a83de plus exactly62 reviewed paths copied from Root's batch2 candidate pointer. The prerequisite and specialization have since passed the Release build and focused selection described below; successor integration/full-gate qualification remains pending.
- Specialize the new LegacyNativeDbSafeDictionary<uint, LegacyNativeGameItemRuntime> for RelicFuse and GearMythsoulBag. Remove the duplicated unsafe inheritance/cache code. Do not modify the generic owner, held Guild/Union paths, or Root's candidate.
- No item issuance, UID normalization, ownership/tradability rules, routing, gameplay handler activation or replacement item owner is introduced.

## Packet contract
Not applicable: collection ownership only. Existing caller packet layouts, order, resource results and complete item serialization remain unchanged.

## Catalog/table contract
Not applicable: retain actual item object references and existing uint bag keys. Bag keys may be native indexes rather than item UIDs. The collection does not synthesize items, rewrite UIDs or validate gameplay ownership; the actual consuming owners retain their current validation.

## Validation and transaction rules
- Thin derived specialization uses the prerequisite's one private Dictionary, nonreplaceable gate, read-only dirty/cache diagnostics, mutation revision, interface contracts and independent ordered snapshots.
- Keep default and capacity constructors, negative-capacity refusal, native upsert Add, native missing-indexer null/no insertion, and actual item aliases including null storage. Standard dictionary/collection interface Add remains duplicate rejecting and interface missing lookup throws.
- Indexer/concrete/interface mutations all participate in the same dirty/revision ownership. No Dictionary base cast, mutable public gate/dirty/cache field, or escaped cached-array replacement may bypass it. Snapshots remain shallow: replacing array slots cannot change membership, while legitimate mutations to the actual item object remain visible.
- Existing client item save captures concrete ToArray entries and independently validates real owners, UID uniqueness, actual rows and graph membership. Preserve that validation and rejection bytes; null/malformed item values do not become accepted saved rows merely because the collection can hold them.
- Membership snapshots and individual collection operations are synchronized. This is not durable persistence, a compound inventory transaction, a native Store synchronization proof or a change to row-level gameplay admission.

## Expected wire behavior and fixtures
- Map all production and test casts/interfaces/array identity/direct field access before the replacement. Existing production calls use Add, Values and concrete ToArray; no required production Dictionary cast or metadata write has been found beyond the duplicated class itself.
- Replace stale/indexer/cache/base-cast/null-gate defect assertions with owner, interface, immutable-gate, defensive-array and current-value invariants. Keep real item alias, missing lookup, valid constructors and duplicate upsert coverage.
- Adapt saved-item, member-birth and Mythsoul stat tests only where they relied on cached-array identity or the stale dirty bit. Preserve full serialized byte, packet/stat/resource/UID assertions.
- Add actual-item parallel interface/indexer/snapshot controls and real owned save positives/refusals. Include slot-key/item-UID distinction and null-row rejection through the genuine save owner; no fake accepting callbacks or runtime owners.

## Evidence alignment
| Requirement | Source/caller | Status |
| --- | --- | --- |
| Duplicated stale/indexer/inherited bypass | LegacyNativeItemBagRuntime plus its explicit defect tests | Confirmed source defect |
| Shared real owner | reviewed DbSafeDictionary in Root's exact62-path prerequisite | Release build and combined2657 passed; successor integration/full gate pending |
| Existing consumers | ClientItemNonWarehouseRouting, ClientItemSave.Membership/Graph, PlayerRuntime.GetMyAllPerfectionItems, EquipItemStorage.Mythsoul | Preserve actual rows, order and consuming validation |
| Cache metadata writes | only ItemBag implementation/tests found in current map | No extra production adaptation planned |
| Actual runtime/client ownership | not established by this collection refactor | Existing boundaries stay in force |

## Implementation and qualification
- Intended production path: World/LegacyNativeItemBagRuntime.cs only; thin specialization, no generic owner changes.
- New spec and FSB-202 plus bounded focused/caller test updates. Exact prerequisite overlaps and authored manifest are retained separately; the62 copied prerequisites are not reauthored postimages.
- Root Release build01 completed with0errors; combined direct556/556 passed, including15 account,41 Info and20 new catalog-decoder outcomes.
- Initial neighbors01 completed2095passed/2failed and remains pre-fix evidence. Root corrected the cache test and separate Ninja FSB-203 defect. Release build02 completed with0errors (157warnings); combined02 passed2657/2657,0failures/skips, covering direct556 plus corrected neighbors2101. Targeted6/6 are included, not added.
- Integration is reviewed; the new full Windows gate remains pending at this qualification checkpoint. Collection evidence does not prove native/runtime ownership, real SQL, durable inventory transactions, client behavior or deployment.