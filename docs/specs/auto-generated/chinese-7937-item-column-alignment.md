# Chinese7937 item decode and column-alignment diagnostics

## Identity and scope

Revision1,2026-10-29. Root's reviewed62-path candidate is the explicit source base,
with declared public baseline233bd0d0ffcbdf131c638dd03b9f49fff76a83de. Preserve
unrelated/held work. The source/data tooling has now undergone Root's offline
qualification below. Native/client execution remains outside this scope; Root
alone integrates and qualifies the successor batch.

The existing strict historical65 document and English67 text reader establish
their own observed fields1/52/53. Current7937 main/Sub are separately strict67:
57093/1693 rows, complete trailing@@/CRLF, unique decimal UInt32 identities and
exact reconstruction/packed inverse. Their fields1..66 remain opaque. The two
additional-field relationship is unknown; never assume appended fields, old
text-slot continuity, server-stat mapping, currency or permissions.

## Packet contract

Not applicable. Offline catalog bytes and diagnostic column relationships only.
No handler, runtime catalog, profile, prices, permissions or gameplay activation.

## Catalog/table contract and evidence

The7937 schema spec records packed/decoded identities and complete metadata audit.
The initial audit intentionally saved metadata only; subsequent qualification
produced fresh private decoded main/Sub files. Retained older
main/sub decoded65 inputs and English-modified65 outputs exist; those modified
outputs are not the separate original English67 reference. Identify/pin every
actual source and verify schema before comparisons. No private key access or new
transform is needed: use the already established fixed item-catalog codec.

Bounded follow-up located an English-heavy database reference with49894 rows,
all65 semantic fields plus trailing@@. It is not the strict67 EnglishTextSource;
never pad or relabel it. No actual67 English reference has been located. A
separately declared65 opaque reference is supported by the diagnostic reader,
without adding an English-role reader or current text replacement.

Independent complete older-reference audit found47184 unique65 main rows and301
unique65 Sub rows. The pinned English-heavy65 reference has49894 rows but49893
canonical identities: one nonuniform duplicate identity group differs in six
fields. The diagnostic reader refuses that complete source. Do not silently
drop/select/merge the duplicate, or turn partial anchor agreement into source
acceptance. Its exact resolution and the missing English67 input remain open.

Fresh7937 plaintext is a required comparison input, not a guessed snapshot.
The implemented separately named data-only command is:

```text
client-item-catalog7937-decode <absolute-input> --expected-sha256 <64-hex-packed-pin> [--output-new <absolute-fresh-decoded-path>]
```

The command name closes the explicit schema to7937; it supports decoding only.
The original `client-item-catalog-codec`65 behavior is unchanged. Add only small
dispatch/known-command/help glue to the exact Program.cs preimage and a focused
new partial. Existing fixed codec, strict7937 document and ordinary local-path /
create-only decoder publication safeguards are reused, with no key discovery,
native probe, transcode, implicit width fallback or old-document coercion.

Require explicit absolute ordinary paths and existing ordinary nonlinked parents;
refuse UNC/device/reparse paths, missing/empty/>64MiB input, malformed pin and
existing/aliased output. Hold a read lease, capture exactly, recheck EOF/length/
ordinary path, compare the packed pin in fixed time, and call the complete strict
`Chinese7937ItemCatalogDocument.ParsePacked(...,7937)`. It must verify67 fields,
CRLF, IDs/duplicates/NULs and exact reconstruction/inverse before publication.

Default is metadata only: version, row/field/byte counts, packed/decoded hashes,
roundtrip and publication flag; never print item names/rows. Optional output is
the complete decoded private data file, never a packed client file. Publish only
after all validation via flushed owned temporary + no-overwrite promotion. Keep
primary/cleanup diagnostics and publication state truthful on I/O failure. No
output directory creation, target overwrite or install/application operation.

## Alignment diagnostic contract

Actual comparisons do not establish complete unique alignment, so retain
diagnostics instead of a text patch API. Accept pinned already-decoded ordinary
reference65 or67 plus explicit current7937/67. Parse complete unique decimal IDs,
exact widths/trailing@@, no NUL/empty rows. Source ending forms are preserved as
observed by historical/English readers; current requires CRLF including last.
All non-ID bytes remain opaque; no encoding normalization or value conversion.

Join exact numeric shared IDs in ascending order, retaining original ID spellings
and untouched bytes in their captured inputs. Field0 is the independently known
identity join, not a candidate text field. For every non-ID source/target column,
compare its COMPLETE shared-ID byte vector with length framing; no samples,
scores, majority votes, edit distance, ASCII/CJK heuristics or guessed offsets.

For each source column report sorted exact target candidates. Equal byte vectors
form equivalence groups. No shared IDs means no evidence; any empty candidate set
means missing exact correspondence; too few target columns in an equal-vector
group means no injective alignment; multiple equivalent choices mean ambiguity.
Only a complete injective unique mapping may be reported as unique BYTE alignment.
That still does not prove native text semantics or authorize replacement. Unknown
or ambiguous results contain no chosen mapping. Always report aggregate counts
and finite status, never names, row payloads or implied text-role authority.

Only65/67 source widths and explicit7937 target are supported, with64MiB/file,
100000 rows and256MiB total framed vectors as offline operational guards. Read
ordinary pinned inputs only; create no actual maps/plans/proposals in public
source. Original resources remain unchanged. Future anchor review may compare
source-established roles and independently observed item IDs, including the old
owned consumable/equipment evidence; older live samples are not current7937 rendering.

## Validation and transaction rules

Prepare complete data/diagnostics before output. Rejection changes no input or
existing output and publishes no successful metadata. Source hashes are explicit
comparison evidence, not runtime owners or trusted gameplay/catalog activation.
Diagnostic tooling has no mutation mode. All actual inputs/maps remain private.

## Expected behavior and synthetic fixtures

Author synthetic command-process tests before implementation: metadata-only;
fresh decoded publication and exact byte inverse; old65 command still refuses67;
bad widths/endings/IDs/pin/arguments, existing output, missing/unsafe paths and
oversized input never publish. No actual catalog rows, item names, key or asset.

Synthetic alignment tests must cover non-appended/reversed unique mapping,
duplicate-vector ambiguity, missing correspondence, insufficient distinct target
columns, no shared IDs, strict malformed sources/targets, immutable raw inputs,
length framing, canonical-ID duplicate refusal and deterministic source/target
order. Synthetic Python checks passed13/13; the20 C# command outcomes passed within
Root's combined direct556. A synthetic result is not actual column proof.

## Evidence alignment and qualification

| Requirement | Evidence | Status |
| --- | --- | --- |
| Historical65 and English67 roles | Existing document/text-source code and old native/rendered samples | Valid only for those sources |
| Current67 byte envelope | Complete main/Sub audits and typed intake | Observed; columns opaque |
| Current plaintext for full comparison | Four actual metadata/create-only decoder executions | Expected67-field envelopes and decoded hashes verified privately |
| Complete shared-ID alignment / role continuity | Complete vectors on47184 main/28 Sub shared identities | MissingExactColumnMatches; no chosen map or replacement API |
| Native text owners/rendering/runtime/prices | No new observation/owner | Unresolved; no activation |

### Actual qualification

Root Release build01 completed with0errors; combined direct556/556 passed,
including all20 new decoder outcomes (plus15 account and41 Info outcomes). The
portable Python diagnostics passed13/13 synthetic tests. Initial neighbors01
completed2095passed/2failed and remains pre-fix evidence. After Root's cache-test
correction and separate Ninja FSB-203 repair, Release build02 completed with
0errors (157warnings); combined02 passed2657/2657 with0failures/skips, covering
direct556 plus corrected neighbors2101. Targeted6/6 are a subset. Integration is reviewed; the new full Windows gate remains pending at this qualification checkpoint.

The actual adopted DLL ran metadata-only and fresh create-only output for both
pinned official7937 inputs, four executions with exit0. Main57093/Sub1693 rows
were strict67/CRLF with exact reconstruction/inverse. Decoded hashes matched:
main `875cc337245f102b15302e230972d0d92db95dcaade600a869a73a9ef623fe2c`,
Sub `72ae7536d232011443cff432f2ea8752f00998250020dcbe7868778ac78090df`.
Official version/raw pins, older input pins and input timestamps were unchanged
after all data work. Outputs were new private files; no original was replaced.

Complete actual shared-ID vector comparisons then reported:

| Pair | Shared IDs | No exact column candidate | Single candidate | Multiple candidates |
| --- | ---: | ---: | ---: | ---: |
| Older main65/current main67 |47184|12|47|5|
| Older Sub65/current Sub67 |28|5|6|53|

Both returned MissingExactColumnMatches. No complete/unique map was chosen;
individual matches do not transfer historical text roles or prove the placement
of two additional fields. Current semantic text roles and native capacities
remain UNKNOWN. The actual English-heavy65 input was explicitly refused for
duplicateCanonicalIdentity; its nonuniform duplicate remains quarantined without
deduplication or first/last precedence. No actual English67 reference is located.

The private qualification summary records aggregate data only. No real item
names/rows/maps, full private paths or keys are bundled in public source/tests.
No client application, rendered English, runtime catalog, price/permission rule,
real SQL ownership, full gameplay parity or new parity percentage is established.
