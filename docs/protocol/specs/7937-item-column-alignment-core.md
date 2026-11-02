# 7937 managed item-column alignment

## Identity and scope

Revision1,2026-11-02, Root-approved offline ingestion primitive. Source baseline:
complete fe3e79a29ff6f377981465de0d78785d7105bf92 candidate. This consumes the
existing immutable strict historical65 and explicit Chinese7937/67 documents;
it does not adapt67 fields into server66, assign names, or activate a profile.

Read first: `docs/specs/auto-generated/chinese-7937-item-column-alignment.md`
and `chinese-7937-item-catalog-schema.md`. Their complete retained comparisons
reported Main47184 shared IDs with12/47/5 old columns having zero/one/multiple
exact candidates; Sub28 shared IDs with5/6/53. Both are
MissingExactColumnMatches. Main57093/Sub1693 are strict67. Older main47184 and
Sub301 are strict65. The English-heavy65 reference has one nonuniform canonical
duplicate and remains refused; no actual English67 reference is located.

Those are prior complete-data findings, not a fresh comparison by this proposal.
Bounded current read-only checks of the retained decoded files confirmed their
byte lengths, first-row65/67 trailing-delimiter shape and finalCRLF only. No
rows, values, actual mappings or private paths enter source/fixtures.

## Packet contract

Not applicable: no packet, handler, profile, gameplay or runtime owner is added.

## Catalog/table contract

| Input | Established grammar/owner | Non-ID columns | Authority boundary |
| --- | --- | --- | --- |
| Historical reference | Existing ClientItemCatalogDocument, exactly65, canonical unique uint32 ID0 | 1..64 opaque bytes for comparison | Its observed roles are not transferred |
| Current7937 | Existing Chinese7937ItemCatalogDocument, exactly67, CRLF/trailing@@, unique uint32 ID0 | 1..66 opaque bytes | No column meaning inferred |

Join the COMPLETE numeric shared-ID set in ascending order, independent of source
row order and decimal ID spelling. Exclude ID0 from column candidates. For each
column concatenate every joined field as uint32 little-endian byte length plus
exact bytes. No encoding/numeric normalization, sample, score or heuristic.
Hash indexing may narrow comparisons; complete byte equality must confirm every
candidate. Equal vectors create equivalence groups; group cardinality determines
injectivity and ambiguity. No map is chosen for missing/ambiguous/conflicting
evidence or zero shared IDs. A unique complete injective byte mapping still does
not prove text roles, price/currency meanings, permissions or client behavior.

Finite operational guards:64MiB per complete decoded document,100000 rows per
document,256MiB total framed vectors. A caller may lower the vector budget,
never raise that fixed ceiling. Preflight measures complete inputs and vectors
before vector allocation. These are tool limits, not native schema capacities.

## Validation and transaction rules

Use genuinely private immutable row storage through internal read-only spans;
public field APIs remain defensive copies. Results own their candidate/mapping
arrays and expose read-only collections/spans. No mutable input alias becomes
authoritative. This DATA comparison has no accepting runtime adapter or effects.
Parsed null/unsupported bounds return no result; malformed/duplicate sources
must refuse at existing complete strict parsers before analysis.

The DataTool consumer is read only:

```text
client-item-catalog7937-align 7937 <absolute-old65-decoded> <SHA256> <absolute-current67-decoded> <SHA256>
```

Require exact closed arguments, explicit7937, ordinary absolute nonlinked
single-link local inputs,64-hex pins and bounded held read leases. Reuse existing
ConfigDecode path/lease/capture guards. Verify fixed-time hashes before parsing;
recheck ordinary leases, EOF and length before output. Release both leases even
on failure. Report aggregate row/shared/column counts, finite status/reason and
hashes only. Never print candidate maps, IDs, rows, field values, paths, exception
messages or stack traces. No output files or write/apply mode. Pins prove captured
byte equality, not provenance or hostile concurrent path-replacement immunity.

## Examples and controls

Owned synthetic fixtures cover non-appended reversed unique columns; exact
length framing versus concatenation collisions; complete-set late mismatch;
unshared rows; numeric ID spellings/order; missing correspondence; equal-vector
ambiguity and insufficient distinct targets; zero shared IDs; duplicate input
refusal; strict malformed schema; byte/row/vector bounds; null inputs and public
ownership. Process controls run the actual command on pinned temporary decoded
files, verify aggregate-only output/no writes and input preservation, and refuse
bad pins/arguments/paths, malformed/duplicate catalogs and bound violations.

## Evidence alignment and unresolved behavior

The complete retained byte-vector findings do not establish the placement of two
additional fields or current text/stat/currency roles. Matching7937 native-reader
offsets or an authoritative table schema remain missing. Value correlation alone
cannot close that gap. Existing Python diagnostics remain unchanged; the new C#
consumer lets Root run the actual retained pair through the managed pipeline and
compare complete aggregate evidence without inventing an accepting adapter.

## Verification and source-bug status

Source-only, unbuilt/unrun; Root alone integrates and qualifies after the sole
host gate. This adds a missing managed ingestion primitive, not a verified source
bug fix or a semantic/server/live7937 completion claim.
