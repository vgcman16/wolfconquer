# Full Chinese7937 selected catalog intake

## Identity and scope

- Revision 1, 2026-10-29, authored before this adapter's implementation.
- Actual reference: the separately obtained official7917 full client, updated
  to7937; `version.dat` contains ASCII `7937\r\n`. This is a data reference,
  not the active D13 client, a runtime profile or a granted gameplay owner.
- Intake is a fixed five-file selection from an explicit ordinary directory.
  It does not traverse archives, enumerate the whole client or execute anything.
- Existing managed packed-item codec and strict client65-field document are
  reused. This is distinct from the server's ItemType/66-field seed projection.
- Existing strict already-decoded Echo parser is reused only for an explicit,
  separately pinned supplement declared7930/7931. A client7937 version marker
  does not relabel an unknown catalog as a new7937 schema.

## Packet contract

Not applicable: no packet, dispatcher, transport, profile, handler or gameplay
registration. Existing2132/2350 codecs and Echo threshold selection are unchanged.

## Catalog/table contract

| Fixed relative path | Observed bytes | Handling |
| --- | ---: | --- |
| version.dat | 6 | Four ASCII digits, optional LF/CRLF; nonzero, exact expected version |
| ini/itemtype.dat | 17279558 | Existing packed codec; strict65-field byte document and exact inverse checks |
| ini/ItemtypeSub.dat | 554243 | Same independently validated grammar; never merge into main implicitly |
| ini/luacfg/XuanBaoEcho.dat | 9114 | Opaque captured identity; no invented transform key/default |
| ini/luacfg/XuanBaoInfo.dat | 47345 | Opaque configuration identity; no guessed currency/cost fields |

The observed reference hashes are private input metadata in the isolated
handoff. A hash comparison is evidence of bytes, not a signed publisher result,
current runtime ownership or proof that supplied decoded text derives from raw.

The item document retains all65 fields, trailing `@@`, original row order and
line endings. IDs retain the existing UInt32 parser contract, including zero,
leading-zero spellings and UInt32.MaxValue; numeric duplicate IDs refuse. Other
field bytes are opaque: no automatic GBK/UTF8 transcode or runtime field mapping.
Decode, reconstruct and re-encode must all be exact. Unknown item formats yield
an explicit unsupported finding with no document, not a fallback table.

The optional Echo supplement consumes the entire already-decoded literal module
through Chinese7935DecodedEchoCatalog. Its established four-field widths,
duplicate/version/grammar refusals and ownership remain unchanged. Both raw and
decoded SHA256 comparisons are explicit. Parsing a supplement does not prove its
derivation, native cache binding, activation transaction or the full7937 grammar.

## Validation and transaction rules

- Absolute local ordinary directory and files only; reject reparse ancestors,
  leaves, network/device paths, missing files and changing read lengths.
- Hold all five ordinary read leases through capture/parse. This is a bounded
  reader, not hostile-namespace or multiprocess atomic-snapshot authority.
- Version64-byte, each item64-MiB and each opaque catalog1-MiB guards are offline
  tool bounds. Copy caller buffers before inspection; return immutable entries
  and existing immutable parsed documents. A failed supplement never changes the
  previous successful snapshot.
- No live mutation, generic callback, command execution, arbitrary file list,
  key discovery, download, archive expansion or reference write.
- DataTool's optional Markdown output is create-only, outside the reference
  directory with an existing ordinary parent. No plaintext tables are exported.
- Missing/unsupported formats remain explicit findings. File/row totals are
  observations only and never a gameplay completion percentage or acceptance.

## Expected wire behavior and fixtures

No wire behavior. Synthetic tests must cover both item documents, UInt32 ID
envelope and duplicates, field-count mismatch, malformed/truncated payloads,
strict version/mismatch, snapshot ownership, unknown Echo versions, explicit
supplement pins, malformed supplement, previous-snapshot preservation, actual
owned-directory reading, missing required files and safe path refusal.

All fixture rows are synthetic. No proprietary tables, keys, assets or game
executables are bundled. The helper authors tests but does not build/run them;
Root qualifies the complete source later. Existing codec/Echo suites remain
required regression controls, not replaced by this adapter's roundtrip checks.

## Evidence alignment

| Requirement | Existing evidence | Limit |
| --- | --- | --- |
| Packed item transform | ClientItemCatalogCodec and its existing byte-golden tests | Previously pinned files, not every7937 input |
| Strict65-field document | ClientItemCatalogDocument/Row and existing byte goldens | No server66-field or name/encoding inference |
| Echo literal data | Chinese7935DecodedEchoCatalog and its generated spec | Declared7930/7931 only; optional supplement |
| Full reference selected paths | Actual ordinary file/version reads2026-10-29 | No full-client runtime/launch proof |

## Open integration owners

Root must first run the new managed adapter on the actual selected reference and
retain actual parsed/refused results. A compatible item document is not a loaded
WorldDataCatalog or live item/save owner. Echo needs a proven decoded/raw relation,
current cache/contribution/item ownership, permissions, cost meanings, operation
13/14 semantics, durable activation/save and profile transport/startup. No current
client rendering, English acceptance, SQL, gameplay activation or parity claim.
