# 7937 complete decoded-text column evidence

## Identity and scope

Revision1, 2026-11-03. Offline source/data evidence based on the retained7937
catalog investigation and clean27b8caaa's existing item-columns package. Inputs
are already-decoded local reference65/67 and current7937 strict67 catalogs.
An encoding difference is a question, not a conclusion. This tool neither names
fields nor produces a chosen mapping, translation, runtime adapter or profile.

## Packet contract

Not applicable: no wire, client/session or server operation is implemented.
No executable/archive decoding, key discovery, native inspection or client apply.

## Catalog and comparison contract

Reuse `catalog_rows.parse/read_pinned`:64MiB/file,100000 rows, exact65/67 fields,
literal `@@` delimiters including the trailing delimiter, canonical UInt32 ID
uniqueness with original spelling retained, exact byte reconstruction and strict
CRLF on current7937. Column0 is the numeric join identity, never a candidate.
The new core reparses every supplied Catalog's captured raw; supplied rows/by_id
are not independent authority. Its declared field_count still must be supported.

`analyze(reference,current,*,reference_codec,current_codec,reference_columns,
current_version,max_vector_bytes=MAX_VECTOR_BYTES)` requires exact codec strings
`utf-8` or `cp936`, strict integer version7937 and a finite nonempty list/tuple of
distinct strict integer reference indices1..field_count-1. No default encoding,
default columns, generators, booleans-as-indices or guessed text slots. Budget
is a strict positive integer at most256MiB; callers may request a smaller guard.

Join every shared canonical numeric ID in sorted order, regardless of physical
row order or spelling. Decode every shared cell of each requested reference
column and every current column1..66 strictly, then require encoding with the
same codec to reproduce its exact source bytes. A column with a decode/encode
failure is `Undecodable`; a nonidentical re-encode is `RoundtripMismatch`. No
partial prefix earns candidates. Unsupported columns remain explicit outcomes.
Cells belonging only to nonshared identities are not comparison evidence.

Canonical vectors contain, for each sorted shared ID, a4-byte little-endian
**UTF8 byte length** followed by its exact strictly encoded UTF8 text bytes.
No normalization, trimming, case/BOM conversion, replacement decoding, sampling,
scores or hash-only equality. Complete framed vectors must compare byte-equal.
ASCII/numeric/constant equality is permitted evidence, not proof of textual role.

Preflight all strict cells and lengths before allocating any canonical vector.
Sum framed sizes of complete supported requested-reference and all66 current
vectors, and refuse `vectorBounds` before vector allocation when the finite
budget is exceeded. Unsupported prefixes are discarded and not charged. The
budget bounds canonical vector bytes, not total Python/RSS/raw/string overhead.
Vectors are built from the same reparsed immutable bytes after this admission.

## Results and read-only consumer

Frozen TextAlignment/Column/CurrentColumn instances retain tuples of exact
source ordinals, every target candidate and every target status. No vector
payload or ID is exposed. Empty shared sets retain requested/all66 column
statuses as `NoSharedIdentities`, with zero vector bytes and no candidates.

Global status describes requested-column evidence only, in precedence order:
`NoSharedIdentities`, `UnsupportedReferenceColumns`,
`MissingExactDecodedTextMatches`, `InsufficientDistinctTargetColumns`,
`AmbiguousCompleteDecodedTextAlignments`, `UniqueCompleteDecodedTextCandidates`.
Equal-vector candidate sets are disjoint equivalence classes; a class with more
requested source columns than targets cannot support an injective correspondence.
Even unique candidates are never exported as a chosen/semantic mapping.

`aggregate()` returns counts/codecs/status and fixed false semantic/client/chosen
mapping flags. `column_evidence()` returns only bounded ordinal/status/candidate
records plus unsupported current-column ordinals/statuses. Fresh dictionaries/
lists do not alias the frozen result. CLI supplies actual pinned ordinary inputs,
explicit version/codecs/columns and generic refusal diagnostics, with no writes,
private paths, row IDs, values, field names or chosen map in reports.

## Controls, ownership and remaining evidence

Synthetic controls must cover independent UTF8/CP936 literals, ASCII, moved and
ambiguous/constant vectors, late complete-set mismatches, canonical ID join,
zero shared IDs, malformed/non-roundtripping columns, forged indexes/mutable
buffers, explicit options and exact finite-budget boundaries before allocation.
Injected codec refusal controls are not genuine CP936 positive observations.

A owns this spec and new decoded_text_alignment.py. B owns the new CLI/README;
C owns new synthetic core/CLI controls. Existing byte analysis/parsers stay
unchanged. Root reviewed the three isolated proposals and ran the composed
synthetic suite:30 passed,0failed/skipped in3.566s, including all13 existing byte
controls and17 new text/CLI methods. The existing byte baseline also passed13/13.
These Python tests use synthetic owned text files and no C#/SQL/native/client or
host probes. Retained large probes wait until the sole host gate is terminal
and Root schedules them. No semantic65/67 alignment, English field
placement, official translation, profile/gameplay activation, rendering or full
parity is established by a successful comparison. Original data and the active
client/native execution hold remain unchanged.

## Actual retained-data result

Root ran eight explicit utf-8/cp936 combinations after full53f was terminal and
the host had no active build/test/runner job. No native inspection or transforms
were invoked. All three requested reference columns1,52,53 were unsupported as
utf-8 in both Main/Sub. cp936 strictly roundtripped all compared shared cells;
complete Main vectors over47,184 shared IDs had no exact candidate for those
three columns. Sub over28 shared IDs had only52->52 with cp936 on both sides;
the other two columns remained unmatched. utf-8 current decoding also retained
three unsupported current columns. Full data/pins stayed unchanged.

These observations reject automatic transcoding as a sufficient complete text
mapping for these inputs. They do not assign current text meanings, establish
whole-file native encoding semantics, choose a mapping, translate catalogs or
activate a runtime adapter. Further sufficient client/schema evidence is required.
