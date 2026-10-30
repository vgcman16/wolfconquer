# FSB-220: Complete native Quest, Team and Proficiency packet preparation

## Identity and scope

Revision 1, 2026-10-29. Existing native component codecs and retained old7899 /
VisibleDx8D13 source/fixture evidence; no new7937 profile or host registration.
Root's73-path candidate supplies the existing physical-bound and prepared-codec
APIs. This package changes four native source files and finite owned tests.

## Packet contract

| Codec | Existing successful representation |
| --- | --- |
| QuestData body reader | Three UInt32LE words: opaque combined word, UID, one allocated intent; reads exactly12 bytes regardless of the combined word's count bits |
| QuestList body reader | UInt16LE mode/count, then UInt32LE quest ID/status/timer; exactly16 bytes; unknown enum bits retained |
| Team body reader | Two UInt32LE words, type then UID; exactly8 bytes; unknown type bits retained |
| Proficiency body reader | Four UInt32LE words, ID/level/experience/needed; exactly16 bytes; fresh real result only after success |
| QuestData full builders2136 | UInt16zero + UInt16 intent count, UID, actual intent words; first overload retains trailing zero and the35024/35007 repeat/padding cases; owner overload retains its shorter tail |
| QuestList incremental2120 | Create writes its20-byte body at offset4 and ends at24 without changing Size; Add appends UID/status/time12-byte rows; Finalize writes the existing header/seal and rewinds to4 |

UnKnow and OwnerUID builder parameters remain ignored as the independent existing
goldens specify. No guessed field meaning or intent-count grammar is introduced.
QuestData's fixed reader does not fabricate an intent when a shorter writer's
frame lacks that word. Declared lengths still exclude the optional8-byte seal.

## Catalog/table contract

Not applicable. No catalog, threshold, quest progression or team admission changes.
Proficiency's existing builders and threshold lookup remain byte-exact.

## Validation and transaction rules

Required stream/item/intent-array arguments reject before target publication.
Each fixed reader uses the shared ReadPrepared helper to prepare local scalars,
arrays or a genuine result DTO, assigning caller out variables only after all
required input bytes and allocation succeed. Ordinary refusal preserves prior
caller references/values, packet cursor, Size and bytes. Supplied Size is range-
checked input metadata, not authentication or independent capture provenance.

QuestData snapshots the actual intent array, rejects an unrepresentable UInt16
count instead of wrapping it, and builds a complete frame via PrepareFrame2136
before any target bytes/Size/cursor change. QuestList header and row snapshots
use PrepareWriteAt/PrepareAppend, preserving its valid incremental API and Size
semantics. Intentions inside QuestListItem remain unencoded and may be null.

There are no production invocations of these native getters/append methods in
the captured source map beyond their definitions. Managed host paths are separate.
No permission, inventory, quest acceptance, persistence or transport owner is added.
Caller-exclusive packet lifetime remains required; concurrent public pointer/Size
changes, cross-process/crash atomicity and delivery are not established here.

## Expected wire behavior and fixtures

Keep all independent successful bytes, unusual tails, widths, enum bits and
incremental cursor/Size assertions. Correct false logical-Size fixtures using their
actual copied/completed initialized extents; never treat Capacity as input.
Replace zero-at-EOF, partial output, null-owner and dirty append acceptance with
whole refusal invariants. Corrected-only controls cover truncated later fields,
tiny exact input ends, null/disposed targets, short header/row capacity and
unrepresentable intent counts. Never execute unsafe old small-buffer controls.

## Evidence alignment

The actual native-broad02 TRX records Quest/Team/Proficiency failures after stricter
physical bounds; existing tests also expose passing bug-preserving partial-output
assertions. Successful source/fixture contracts are retained. No current client
or catalog capture resolves new semantics, so no new wire authority is claimed.

## Implementation and qualification

Four source and four existing test paths, one focused test file and this spec.
The isolated helper performs no restore/build/test/dotnet/native execution, live
operation or Git action. Root reviews, composes and records actual qualification.
Until then every new outcome and fixed-behavior statement is an unrun candidate.
