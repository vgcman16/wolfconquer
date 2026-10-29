# Chinese7935-chain decoded Echo catalog ingestion

## Identity and scope

- Revision1, 2026-10-29; derived from recorded catalog/schema and native-consumer
  evidence. This spec precedes the new C# source in this isolated package.
- Selected official7931 DX8 engine carried by the7935 chain:30,085,432 bytes,
  SHA256a7e1aac88a999b1f4723e6a1f197b96924e24022fd681fb2c9cf79c1bb413422.
- Actual decoded inputs are private:7930,501bytes,
  SHA256f376205af43e319e54ef59a56f81e0a3dcbb9c1ec4d8e85ca73f060862999cbd;
  7931,9114bytes,SHA256753c1ebc56459c089819fd8ba8a8acaa53b276c18f6a0a60115a9baacf8511ff.
- Parse already-decoded UTF8 data only; no transform key/default/discovery,
  Lua/CRT/native execution, profile registration, startup binding or gameplay.
- Declared catalog version is caller metadata, not proof of official provenance.
  A caller-supplied expected hash is comparison evidence, not a signed authority.

## Packet contract

Not applicable: no packet/handler/transport is added. The existing2132 codec and
2350 item codec remain separate. This data parser grants no Echo activation,
currency/cost, fifth-slot or saved-state authority.

## Catalog/table contract

Recorded wrapper is exactly `local ret = {}; ret.cq_xuanbao_huying_extra_attr =
{...}; return ret;`. The pure strict literal subset supports optional UTF8 BOM,
ASCII whitespace/identifiers, bounded Lua line/long comments, literal nonnegative
decimal integers, numeric-keyed rows, named fields and optional trailing commas.
Every token/tail is consumed. Calls, strings, functions, expressions, aliases,
extra exports/statements, unsupported encoding and duplicate keys refuse.

Each row has the exact four named fields once; textual field order can vary
because the recorded native consumer looks them up by name. Source row order is
retained. Row IDs are unique positive signed32-bit indices; contiguity,1..85,
17groups and five-row grouping are observed data, not universal validation rules.

| Field | Typed accepted literal range | Evidence / finite limit |
| --- | --- | --- |
| row ID |1..Int32.MaxValue|Native loop/accessor uses signed dword indices; no zero/default row invented |
| attr_type |0..Int32.MaxValue|Native signed dword key comparisons00426468/10E77CE; negative literal grammar is unsupported |
| attr_value |0..UInt32.MaxValue|Retained dword threshold with unsigned compare0115AD07..34 |
| extra_attr_type |0..Int32.MaxValue|Existing typed pure selector and literal named-field read; type6 comparison0115AC39 |
| extra_attr_value |0..UInt32.MaxValue|Retained dword bonus and unsigned maximum0115AD36..67 |

These envelopes do not emulate native Lua/CRT overflow, fractional conversion or
all signed bit patterns. Values outside them refuse rather than wrap or guess.
Duplicate row IDs (including1/01) refuse; duplicate primary type/threshold rows
with different IDs remain, as proved by the actual duplicate-retaining cache.

The original strict private parser's inspection guards are retained:64KiB bytes,
8192tokens,128-character identifiers,19-digit literals,1024aggregate table members.
Each row consumes five members; at most204complete rows fit. These are sanitized
offline bounds, not native/gameplay limits. The specialized schema has only the
export table and one row-table level; deeper/nested values refuse.

## Validation and transaction rules

- Pure parsing owns its input copy and only returns a complete immutable typed
  snapshot after all syntax/schema/version/duplicate/width checks pass.
- Supported declared versions are7930 and7931 (the latter carried forward by the
  researched7935 chain). No invented7935 table version, row-count requirement or
  automatic conversion of other versions. Empty exports remain real empty data.
- Explicit file loading requires a caller-selected existing absolute ordinary
  path and a32-byte expected SHA256. Use a bounded read lease; reject reparse
  ancestors/leaf, Windows network/device spellings and byte/pin mismatch. Return owned data, no open file lease or
  implicit runtime owner. This is not hostile-namespace/multiprocess authority.
- No global reload, old snapshot mutation, callback loader, accepting handler or
  fallback table. A failed parse/load returns no replacement snapshot.
- A fresh typed projection into Chinese7935EchoType6Thresholds retains all rows,
  including repeated primary keys, and owns its output. It does not provide the
  actual live contributions or current catalog-cache/initialization owner.

## Expected wire behavior and fixtures

No new wire behavior. Synthetic tests cover valid/empty/comment/BOM modules,
complete named-field sets, source order, repeated primary keys, immutable ownership,
signed-key/unsigned-value width boundaries, unknown versions/exports/fields,
duplicate numeric identities, malformed/trailing/expression input, allocation
bounds, explicit file/pin refusal and unchanged previous successful snapshot.
Fixtures contain no proprietary rows, encrypted tables, keys or client assets.

## Evidence alignment

| Requirement | Recorded source | Status |
| --- | --- | --- |
| Exact literal wrapper/four fields |helper-chinese-echo-catalog-schema-next/{literal_module.py,literal_tokens.py,README.md}|Data grammar, not Lua execution |
| Private input identities |same/analyze_catalog.py and metadata summary|Exact recorded pins, real inputs stay ignored |
| Signed keys / unsigned threshold/bonus |helper-chinese-echo-selection-next reports007–014, README|Bounded static consumer evidence |
| Duplicate cache retention |helper-chinese-echo-selector-review-next reports001–011|Retain repeated type/threshold rows |
| Current implementation boundary |docs/CHINESE-7935-PROTOCOL.md + Chinese7935EchoType6Thresholds.cs|No runtime binding/activation proof |

## Implementation and qualification

New standalone Client namespace parser/snapshot/file loader only. Current
VisibleDx8D13/default startup, WorldDataCatalog, packet dispatch and persistent
schemas are unchanged. Root alone reviews/builds/runs after the sole full CI is
terminal; this author executes nothing. Private real-file parse can be separately
qualified without printing/publishing its rows. Live Echo state/contributions,
permissions/costs, operation13/14 semantics and activation/save transactions remain
missing prerequisites. No parity percentage or complete7935 compatibility claim.
