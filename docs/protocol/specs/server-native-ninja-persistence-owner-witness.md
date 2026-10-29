# server-native-ninja-persistence-owner-witness: retain the admitted registry

## Identity and scope
- Revision1, 2026-10-29; Root-authorized correction of a real compiled80-candidate failure, reserve FSB-203.
- Clean public233bd0d0ffcbdf131c638dd03b9f49fff76a83de plus exactly80 current Root candidate source paths, captured by identity before authoring. No actual checkout or Root candidate changes.
- Target profile: not applicable; native Ninja rank in-memory/file ownership only. No packet, catalog, SQL or live handler change.
- Root's neighbor run retained ActualLoadWaitingOnCapturedRegistryGateCannotOverwriteANewerWholeRegistry failing after a newer complete registry was overwritten. The strengthened dictionary's TryGetValue now exposes the early guard's blocking point; the persistence call must retain its first owner rather than recapturing.

## Packet contract
Not applicable. Existing successful rank rows, category order and user-facing response paths stay unchanged.

## Catalog/table contract
Retain the existing NinjaRank.txt Count header,21 fields/trailing delimiter, row/category/UID/sort/text bounds, strict decoder, location identity, owned file lease, prepared image verification and replace/move rules. Parsing and input code remain untouched.

## Validation and transaction rules
- The early reentry guard captures one actual registry and its actual stable gate before any potentially blocking registry lookup, then returns that witness. It still refuses registry/category gate reentry and verifies that the same owner remains current after the lookup pass.
- Retain this exact witness across file coordinator waiting, source reading/parsing, image capture and publication. Never recapture Ranks as a new authority in LoadLocked or CapturePersistenceImage.
- Revalidate the witness after acquiring the real file lock and captured registry gate, preserving the existing file -> registry -> category lock order. The early guard does not retain a graph lock while waiting for the file lock.
- Load prepares a complete replacement without changing the live graph. Under the captured gate it revalidates location/owner and uses real Interlocked.CompareExchange with the captured expected registry to consume the publication witness. A newer whole registry cannot be overwritten between a reference check and assignment.
- Save uses the captured registry for its existing category/row/image checks and keeps that captured registry gate held from image preparation through stage verification and file publication. Recheck the same witness before staging and immediately before destination replacement. A stale operation refuses without changing the destination; stage cleanup and original failure handling remain.
- No callback, publisher hook, accepting barrier or replacement runtime owner is introduced. Noncooperating public static registry, category-map/gate and mutable-row writers are outside this ownership guarantee; category gates remain the existing capture-time snapshot mechanism. Other processes and crash/power-loss windows are not covered. Save cannot make arbitrary unsynchronized public-field writes atomic with file I/O; its gate serializes cooperating owner publications and its final identity check refuses observed replacement.

## Expected wire behavior and fixtures
- Keep the original actual held-registry Load counterexample and all4 reentry controls unchanged.
- Add actual Save held-registry replacement, Load/Save file-coordinator wait replacement, and Save held-category replacement cases with complete real registry owners and owned existing files. No injected persistence provider, comparer callback or production phase hook.
- ThreadState may coordinate a blocked phase; it is not the final oracle. Require completed worker refusal, exact current newer registry and row graph preservation, escaped old graph preservation, unchanged saved bytes and no owned stage residue. Category-phase coordination additionally observes the real captured registry gate held by the worker.
- Retain successful roundtrip, invalid graph/file/row/location and cleanup controls.

## Evidence alignment
| Requirement | Evidence | Status |
| --- | --- | --- |
| Actual Load overwrite | Root results-neighbors01/neighbors01.trx, named counterexample | Real prior failed test |
| Lost initial identity | Reject guard local owner followed by LoadLocked recapture | Confirmed source race |
| Save retarget twin | guard local owner followed by CapturePersistenceImage recapture; write lacks owner check | Confirmed source race |
| Final Load TOCTOU | reference check then plain Ranks assignment despite public rebinding | Closed by compare-exchange design |
| Complete graph/file safety | existing Input/Parse/category row/stage verification paths | Preserve unchanged |

## Implementation and qualification
- Production paths: Data/LegacyNativeNinjaRank.Persistence.cs and Persistence.Save.cs only. Input/Parse and generic dictionary owner stay untouched.
- Root Release build02 passed with zero errors. All 6 targeted regression/witness outcomes passed, followed by all 2,657 combined focused/neighbor cases with zero failures/skips. The six are a subset. The original failed Load test and four reentry controls remain unchanged; the original failing TRX is retained.
- The new source publication batch still requires its full Windows gate. No SQL, client, live process or deployment was exercised. This qualifies the retained-owner correction under the documented cooperating-owner bounds, not arbitrary public-field writers, complete persistence or crash safety.
