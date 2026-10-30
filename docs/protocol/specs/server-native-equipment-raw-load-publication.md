# FSB-226 raw equipment observation publication

## Identity and scope
- Revision 1, 2026-10-29; helper-owned raw observation portion of [complete item load](server-native-item-load-publication.md), baseline archive 84611bb1.
- Current confirmed live profile remains 7899 / VisibleDx8D13. No 7937 support or native gameplay authority follows from successful physical parsing.
- Existing EquipmentRuntime and one focused raw-load partial; one existing public-enter raw test file plus one new focused partial. No NativeLoad routing, parser ownership, GameClient dispatch/profile, SQL, live client, original save or executable changes.

## Packet contract
Not applicable. This observer emits no packets, rentals, resource changes, FullLoading or weapon authorization. CanAuthorizeWeaponAdmission remains false.

## File and observation contract
- Consume the shared actual LegacyNativeItemLoadImage.Parse(ReadOnlySpan<byte>) API, complete 184-byte roots/immediate deposits and complete 80-byte perfection postlude before preparing/publishing any raw collection.
- Each root Record provides its own UID, Position and WH_ID routing. Deposited records create genuine distinct raw children under that root, keyed by each child's own UID; no last-child substitution of parent routing/key.
- A raw item's concrete identity is a selected-field observation, not a genuine native MsgGameItem or DTO alias. Retain the same selected raw parent/child objects across observation calls, including genuine Deposits aliases.
- Preserve the existing raw equipment selection: WH_ID100 selects positions1..59, other nonzero warehouses excluded; WH_ID0 selects positions1..63, with213..215 excluded before equipment. Children are never independent equipment map entries.
- Complete physical parsing does not satisfy the real rune ITEM_ID constructor/catalog dependency. Encountering an applicable rune row makes observation unavailable, not a valid empty fallback.
- Perfection rows are physically validated by the shared parser, not applied to a three-field raw projection or treated as another raw item generation.
- Root ruling: the shared parser rejects zero/duplicate serialized item UIDs globally across all roots/deposits, matching the real canonical save graph (genuine wardrobe/equipment aliases serialize once). No first-TryAdd duplicate keyspace is advertised as a valid raw generation.
- A 205901 root without children retains its existing UNVERIFIED legacy semantic filter after complete physical parsing. A filtered205901 root with children makes the entire observation unavailable: migration/placement policy is unresolved, so children may not be silently discarded or invented elsewhere. Original bytes remain preserved.

## Lifecycle and publication
1. The real configured captured read creates this actual client-owned observer. An absent configured loader creates no observer. Missing file (null captured bytes) is a valid completed empty observation; a present malformed or zero-byte file is not.
2. Prepare an entirely detached raw dictionary and each actual raw child graph after complete parsing. All throwing parser/conversion/collection work precedes a single field/state publication under a private observer lifecycle gate. No late malformed tail/rune constructor failure leaves private partial rows.
3. Successful complete capture publishes the prepared collection once. A second capture invalidates observation rather than replacing prior raw aliases. Explicit invalidation is monotonic: future valid bytes cannot revive an invalidated observer or turn failure into empty-valid equipment.
4. Parse/domain evidence failure marks unavailable with a reason and leaves prior published raw collection unchanged. Observation returns Available=false, Found=false, Item=null and no raw entries when unavailable; this differs from valid empty equipment.
5. The private gate serializes this observer's capture/observe/invalidation lifecycle only. It does not authorize native item mutation, undo escaped raw-object/Deposit-map aliases, or provide cross-owner/persistence/crash atomicity.

## Evidence and expected fixtures
- Actual callers: SessionStartup's single item-file captured read, and OrdinaryAdmissionPlan preparation; invalidators are public item mutation intent, target starter replacement and operator mail commitment.
- Existing public-enter tests preserve distinct primary/alternate selection, owner generation, configured missing/empty/absent loader meanings, mutation-log boundary, starter invalidation, rune evidence boundary and routing cases. Replace the explicit corrupted parent-key assertion.
- Add owned complete valid parent/child graphs with conflicting route fields, malformed late counts/rows/tails, late rune failure, input-byte preservation, no private partial publication, aliases, repeated loads and invalidation-before/after-capture. Do not execute original saves or unsafe native baseline.

## Qualification
Helper authors source/tests only. Root alone reviews/integrates/builds/tests. No helper build, restore, dotnet, native/process/live/SQL/Git execution or actual checkout mutation. The observer is never proof of complete native item hydration, Rune metadata, gameplay admission, new-client parity, persistence or live acceptance.

## Authored verification and limits

All 18 existing public raw-load outcomes remain, with two defect-preserving assertions corrected (parent key and duplicate identity). The new raw-publication partial authors 26 unrun outcomes: six conflicting parent/child routes, one complete graph/alias/perfection framing control, nine malformed physical inputs, two late rune failures with a fresh-generation repair control, three repeat/invalidation lifecycle controls, two 205901 policy controls, two canonical identity failures, and one real-owner null control.

The helper did not execute a test/build/native/process/SQL/Git action. Read-only parser inputs still require exclusive captured-byte ownership; bounded parsing cannot make concurrent caller buffer mutation a transaction. Exported raw parent/Deposit-map aliases remain existing observation objects, not implemented native mutation owners. Root records actual qualification; no test total is a gameplay or live-client claim.

Root local qualification for the combined40-path candidate: Release build01
passed with0errors/157existingwarnings and no new changed-path warnings. Actual
focused01 passed423 with7existing skips; native-broad01 passed13278/13278 with
0failures/skips. Selections overlap. Receipts are in root-native-item-load-next.
The old public7bb fullrun failed one timing-sensitive startup test; its actual
failedTRXs/logs remain. Exact-commit successor fullCI, SQL, deployment and live
new-client/fullparity acceptance are pending. This note supersedes authored/unrun
labels in this spec/entry without expanding the verification scope.
