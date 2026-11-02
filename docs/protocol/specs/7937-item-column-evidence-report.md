# 7937 item-column evidence report

## Identity and scope

Revision1,2026-11-02, Root-requested source-only follow-on to the managed65/67
alignment primitive. Base is Root's ccb567e36c835b662f5dd7b39d512874a04cf2d3
source with unrelated import fixes under qualification. Existing candidates and
all earlier frozen scopes remain unchanged; Root alone integrates and executes.

Current Result/API already owns complete exact candidate sets, but the CLI emits
only aggregate counts. Add an explicit machine-readable ordinal evidence mode;
do not repeat comparison, sample rows, decode catalogs again or assign meanings.

## Packet contract

Not applicable. No runtime profile, handler, packet or gameplay action changes.

## Catalog/table contract

Reuse the actual `ClientItemCatalog7937Alignment` result on strict historical65
and current7937/67 parsed documents. Existing full numeric shared-ID join,
length-framed vectors, exact equality, input/pin/lease/resource guards and
global statuses remain authoritative. No raw table row is serialized.

New immutable DATA projection per reference column:

| Property | Contract |
| --- | --- |
| ReferenceColumn | Zero-based historical ordinal1..64; identity0 excluded |
| CurrentColumns | Complete ascending exact target candidates1..66; empty if missing |
| SharingReferenceColumns | Ascending reference ordinals with the same NONEMPTY candidate set, including self; empty for missing |
| Status | MissingExactCandidate, SingleExactCandidate, AmbiguousCandidates or InsufficientDistinctTargetColumns |

For a nonempty set, more sharing reference columns than target candidates is an
injectivity conflict. Otherwise exactly one reference/one target is a single
exact candidate; all other nonempty groups are ambiguous. Missing columns never
get grouped as equivalent vectors just because both have empty candidate sets.
Per-column conflicts remain visible even when a missing column takes precedence
in the unchanged global status. These are column-evidence names, not field roles.

Results come only from the existing closed immutable owner. New projection arrays
are privately owned, with read-only public collections/spans; no writable alias.
No chosen ambiguous map is introduced. Even global byte uniqueness remains DATA,
not text/stat/currency/permission meaning or acceptance of a runtime adapter.

## Read-only command and output

```text
client-item-catalog7937-align 7937 <absolute-old65-decoded> <SHA256> <absolute-current67-decoded> <SHA256> --column-evidence
```

Existing six-argument aggregate FormatVersion1 behavior stays unchanged. The
sole optional flag is `--column-evidence`, in final position. Extra/duplicate/
unknown options refuse through the existing static Arguments diagnostic.

The new mode uses the same single pinned capture/analysis and emits FormatVersion2
JSON: existing aggregate metadata and captured hashes, explicit zero index base/
excluded identity ordinal, plus ColumnEvidence records with the four properties
above. Maximum64 records,66 candidate ordinals and64 sharing ordinals per record.
The serialized UTF8 JSON must fit65536bytes before any successful output; the
finite bound covers the worst supported schema. Refusals contain no partial
column report. SemanticRolesProved/RuntimeBinding/GameplayActivated/InputsWritten/
OutputFilesWritten remain false. No raw row IDs/values, private paths, exception
messages or mapping/asset files are emitted. The intentional ordinal arrays must
not weaken existing default-mode redaction controls.

## Validation and controls

API controls use real complete parsed fixtures/result creation, covering unique,
missing/late mismatch, no shared IDs, ambiguity, injectivity and mixed missing +
conflicting groups, ordering and defensive copies. Actual process controls use
owned pinned decoded fixtures and the real route/reader, independently assert
ordinal arrays/statuses, worst-case finite report bound, redaction, refusal/no
partial output, unchanged sources/no files, and retained FormatVersion1 output.
No fabricated accepting owner, allocator, callback or runtime state is needed.

## Unresolved evidence and qualification

This makes the actual complete65-to67 candidate evidence consumable; it does not
resolve the real main/Sub missing matches or semantic mapping. Matching7937
reader/schema evidence remains necessary. All new C# and controls are unbuilt/
unrun in this helper scope. No source-bug fix or runtime/live support is claimed.
