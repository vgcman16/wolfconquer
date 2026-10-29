# Chinese7937 item catalog schema investigation

## Identity and scope

- Revision 2, 2026-10-29, complete data audit before schema successor implementation.
- Actual official full7917 reference updated to7937; selected main/Sub bytes
  are pinned by the preceding actual intake. No running client/native inspection.
- Main17279558 bytes, SHA256780a2ebd826655766c0e51c6e44af975334caaf6612f4f75f0b7f428887899f4.
- Sub554243 bytes, SHA25628f9a5528b6b5ba5503e71240b579cee67fa017352670bdfad70840f31719f01.
- Existing historical65-field item parser/transform are retained as established
  contracts. This investigation may establish a separately named schema, never
  relax the old parser or infer server fields/currencies/permissions from names.

## Packet contract

Not applicable: data-only decode/schema work; no profile, handler or packet change.

## Catalog/table contract

Raw headers are opaque and contain repeated12-byte blocks. The two actual
managed codec commands both report `Item catalog row 1 has more than 65 fields`.
This identifies the immediate strict-document refusal, not yet the full schema.

| Property | Current evidence | Status before complete data audit |
| --- | --- | --- |
| Packed transform | Existing fixed12-byte ThreeWay/XOR54 pipeline | Full decoded data coherent; exact packed inverse in both files |
| Plain header | Both first rows are ordinary numeric-ID rows; all bytes consumed as rows | No header bytes/lines skipped or supported |
| Row field count | Main57093/Sub1693; every row exactly67 fields | Separate strict7937 envelope; old65 unchanged |
| Separator | Every field ends `@@`; every row ends CRLF, including last | Exactly that shape; no LF/CR-only or unterminated-row extension |
| Field0 ID width | Complete UInt32-decimal identity validation; no duplicates | Existing offline identity envelope retained; no native conversion generalization |
| Other fields | Opaque bytes | No numeric/semantic width guessed |
| Encoding | Both full decoded files strict CP936 roundtrip; strict UTF8 fails | CP936-compatible observed bytes, not unique encoding or text-slot ownership; no transcode |

Exact observed decoded hashes: main
`875cc337245f102b15302e230972d0d92db95dcaade600a869a73a9ef623fe2c`, Sub
`72ae7536d232011443cff432f2ea8752f00998250020dcbe7868778ac78090df`.
Neither file has an empty row, NUL, missing trailing delimiter, invalid/colliding
canonical ID or unaccounted byte. Main IDs span50000..40708406; Sub194245..10100108.
Every field's observed byte-length range is retained as metadata in the private
audit, not imposed as a native maximum or used to guess numeric/string meanings.
The additional two-field relationship to the historical65 columns is UNKNOWN;
do not assume appended fields or reuse the old1/52/53 text-role mapping.

Accepted new API identity envelope is the existing offline decimal UInt32 parser
contract, including leading-zero spellings, zero and UInt32.MaxValue. Actual IDs
fit that envelope; out-of-envelope values refuse. This is not a claim about all
native numeric conversions. Fields1..66 remain opaque non-NUL byte slices.

## Validation and transaction rules

Read only the two pinned ordinary data files and preserve them byte-for-byte.
The complete preimplementation audit covered every decoded row's shape,
trailing delimiters, line endings, NULs, IDs, duplicates and exact reconstruction/
re-encoding. It emitted metadata/counts/hashes only; no proprietary row/name payloads.
Unsupported/ambiguous results remain refused. Byte bounds are offline guards.
No key discovery, native executable/disassembly/hook, installer or game execution.

## Expected wire behavior and fixtures

No wire change. Any justified successor needs synthetic positive/malformed,
exact width/header/ID/ownership/byte preservation/refusal controls. Existing65
tests/goldens remain unchanged. Actual input row payloads never enter public tests.

## Evidence alignment

| Requirement | Source | Status |
| --- | --- | --- |
| Historical65 profile | Existing codec/document/tests and recorded independent native outputs | Retain unchanged |
| Actual7937 refusal | helper-full7937-schema-next/codec-diagnosis.json | Both row1 exceed65 |
| Exact observed envelope | Complete bounded managed metadata audit | All rows67/CRLF; successor code qualification pending |

## Implementation and qualification

The notified owned net9 metadata probe built0errors/0warnings, references only
the already compiled Core codec and ran once per pinned data file, terminal0/0.
It did not rebuild Core/tests, expose rows or run client/native inspection tools.
The full audit justifies a separately named immutable Chinese7937ItemCatalogDocument
with explicit declared7937, exact67/trailing@@/CRLF, complete input consumption,
canonical ID uniqueness, opaque field ownership and exact packed inverse.

The intake adapter may retain an explicitly typed7937 document and report
ParsedChinese7937ItemEnvelope. It must not fabricate a historical65 document,
change server66 fields or activate a runtime catalog. Existing historical65
parsing remains unchanged. Root reviews/qualifies the supported successor;
runtime/catalog ownership and gameplay remain separate and unproved.

### Root source/data qualification, 2026-10-29

The combined37-path candidate passed Build05 with0 errors. Catalog02 passed
294/294 cases, including the19 new7937 outcomes and unchanged historical65
controls. Actual managed `client-reference-intake` against the official updated
7937 reference returned0: both main57093 and Sub1693 rows are separately typed
ParsedChinese7937ItemEnvelope/67fields; their original packed hashes match the
recorded observations. Echo and Info remain OpaqueRequiresExplicitDecoder.
The complete input byte envelope is retained; no column meanings, old/server
field mapping, runtime registration, routing, costs, persistence or live gameplay
is inferred. The full Windows publication gate remains pending for this batch.
Actual local findings: root-classic-target-integration-next/actual7937-findings.md.
