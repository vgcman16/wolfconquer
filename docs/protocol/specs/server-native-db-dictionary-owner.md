# server-native-db-dictionary-owner: synchronized collection and value snapshots

## Identity and scope
- Revision1, 2026-10-29. Root-authorized collection-owner implementation; FSB-201 qualification remains bounded below.
- Baseline: public233bd0d0ffcbdf131c638dd03b9f49fff76a83de (sourcea4380ba1), committed source only.
- Client/profile: not applicable. No packet or data-table layout changes.
- Correct LegacyNativeDbSafeDictionary's real ownership/cache seam and the two required HeroRewards consumers. Keep derived activity/hero tables and successful ranking, mail, group-server and event uses compatible. Separate ItemBag caches and held Guild/Union edits remain outside this change.

## Packet contract
Not applicable: collection ownership only. Existing enumeration order and successful callers' serialized values remain unchanged.

## Catalog/table contract
Not applicable: preserve existing keys, values, comparer identity and insertion-order enumeration from the internal Dictionary. Keys/values are shallow references to real existing objects; no invented row or accepting owner is introduced.

## Validation and transaction rules
- Replace Dictionary inheritance with one private Dictionary implementing IDictionary<TKey,TValue>, IReadOnlyDictionary<TKey,TValue>, IDictionary and their collection/enumeration contracts. No cast may bypass synchronization or snapshot invalidation.
- One nonreplaceable nonnull SyncRoot guards all collection reads/mutations and cache metadata. External callers may retain and lock that same gate. Keep comparer identity; negative capacity/null reference keys reject through real Dictionary validation.
- Concrete Add remains the existing native upsert. Explicit IDictionary/ICollection Add retains BCL duplicate-add rejection; TryAdd returns false for a duplicate. Rejected calls leave entries, mutation revision and cache metadata unchanged. The concrete missing indexer returns generic default without insertion; standard dictionary-interface getters retain missing-key exceptions.
- Indexer assignments, successful additions/removals and nonempty Clear invalidate cached values and advance a checked monotonic Revision under the same gate. Compute the next revision before changing entries; no overflow may leave a partial change. Missing Remove, duplicate TryAdd and empty Clear do not report a mutation.
- Update becomes a read-only dirty diagnostic. MyArray becomes a read-only defensive copy of the last materialized cache, never an external write path. GetValues materializes a current value array under the gate then returns a defensive copy. External array element replacement cannot poison the owner or its future snapshots. The concrete ToArray takes one entry snapshot under the gate, avoiding LINQ Count/CopyTo races. Keys, Values and enumeration use independent read-only/snapshot projections; Values still has Count. Enumeration preserves the underlying Dictionary's existing order and keeps element references (no deep cloning).
- HeroRewards captures actual entry rows, comparer, stable gate and monotonic Revision under its existing lock order. Revalidation checks real row identity/fields and revision, never cloned-array reference identity. Staged goal books use interface duplicate Add and the real GetValues materialization rather than physical base casts or public cache writes.
- Comparer/value-equality calls may reenter a monitor. Reject nested collection mutations before changing entries while a Dictionary operation is active; restore that guard in finally/Dispose on both success and failure. Ordinary custom comparers and read-only owner diagnostics remain supported.
- Per-operation collection locking does not make multi-call gameplay operations transactional, protect mutable value internals, or establish file/SQL persistence or crash safety.

## Expected wire behavior and fixtures
- Preserve successful ranking Create upserts, ban replacement, mail Values.Count/enumeration, elite pairing order, activity/hero derived catalogs, missing-native indexing and comparer behavior.
- Replace tests that enforce stale indexer/inherited mutations, exposed cache arrays and replaceable/null gates with owner/snapshot invariants, retaining the unrelated SQL helper/enum controls.
- Add meaningful generic/nongeneric interface mutation, duplicate rejection, comparer, failed/null input, defensive array, snapshot enumeration and parallel writer/reader controls. Include actual goal admission, ranking/mail/group-server callers and existing rollback suites.
- Adapt existing rollback fixture checks from escaped cache-array identity to contents and owner revision. Replace impossible public gate/cache corruption fixtures with direct rejection/defensive-copy and successful caller checks; retain actual malformed owner/row and comparer/rebinding coverage.

## Evidence alignment
| Requirement | Observed source/caller | Status |
| --- | --- | --- |
| Stale indexer cache | DbSafeDictionary indexer does not dirty cache | Confirmed source defect |
| Bypass mutation | inherited Dictionary mutators skip dirty state and gate | Confirmed source defect |
| Exposed metadata | public SyncRoot/Update/MyArray permit lock and cache replacement | Confirmed source defect |
| Concrete upsert | SystemBanned AddBan; repeated HWRank/Ninja/Prestige Create | Required successful behavior |
| Hero caller adaptation | Admission array identity checks; Writes base cast/cache assignments | Required two-file integration |
| Snapshot consumers | mail Values.Count; rank/view/event/group-server value iteration | Preserve shape, order and real row aliases |
| Other physical casts/field writes | no additional production DbSafeDictionary cast/write found; ItemBag is a different owner | Mapped, no extra production edits planned |

## Implementation and qualification
- Planned files: focused DbSafeDictionary partials; HeroRewards.Admission.cs and Writes.cs; meaningful affected collection/caller tests; this spec and FSB-201.
- Root Release build01 completed with0errors; combined direct556/556 passed, including15 account,41 Info and20 new catalog-decoder outcomes.
- Initial neighbors01 completed2095passed/2failed and remains pre-fix evidence. After the cache-test correction and separate Ninja FSB-203 repair, Release build02 completed with0errors (157warnings). Combined02 passed2657/2657,0failures/skips: direct556 plus corrected neighbors2101. Targeted6/6 are a subset, not an additional total.
- Integration is reviewed; the new full Windows gate remains pending at this qualification checkpoint. Live SQL, compound gameplay transactions, persistence/crash safety and deployment remain unproved.- Root corrected the failing BruteForce cache test with complete valid GroupServers rows and revision/defensive-snapshot/unchanged-owner checks. AllowAddress still returns false for valid inputs and has no installed production caller; catalog metadata grants no trusted-network policy. Other malformed-row/null-trap policy tests remain a separate unresolved audit, not fixes claimed by this collection batch.
