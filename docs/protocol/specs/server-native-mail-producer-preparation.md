# FSB-223: Complete native mail-list producer preparation

## Identity and scope
- Revision 1, 2026-10-29; Root assigned isolated source-only implementation.
- Target evidence: native community-server component contracts and independent mail fixtures, composed candidate pointer captured in DEPENDENCY.json. Current confirmed live profile remains 7899 / VisibleDx8D13. These mail component quirks are not new 7937 client acceptance evidence.
- Direction: server mail list, Loading and request Process only. No dispatcher, live request, claim, persistence, notification codec or original saves change.

## Packet contract
- Opcode 2183; actual CreateMailList and pinned protobuf serializer own encoding/framing.
- Outer fields 1 Type=0, 2 Count=1, repeated field 3 Mail. Row fields: 1 Id, 2 Money, 3 EMoney, 4 Expiration_Date, 5 Itemid, 6 Emoney_Record_Type, 7 Flag (ulong/varint); 8 Title and 9 Sender_Name (string). Existing required-field declarations and nullable string serializer semantics remain unchanged.
- Loading reverses the actual map entry/value snapshot. Request stably sorts that snapshot by descending row Id, then consumes it from the end; equal-Id groups therefore reverse original order. Opaque map keys need not equal row Id.
- Preserve ten rows per page and the nonempty count/10+1 sequence, including a final empty page for exact multiples of ten. Truly empty maps publish no pages.
- Each complete frame must satisfy actual physical bounds, UInt16 header length, captured seal, and NativeGameClient.Send's 1032-byte total limit. Do not shrink/drop rows to fit.

## Catalog/table contract
Not applicable: this bounded producer consumes actual LegacyNativeMailList rows. No new catalog schema, field meaning or game rule is inferred.

## Validation and transaction rules
1. Require the real client and its actual mail map. Hold that map's actual SyncRoot. Capture its reference/revision, actual key/row references, and all nine transmitted wire values. Null rows reject before any packet rental or output; nullable strings pass to the actual serializer.
2. Empty maps return before unused target or rental prerequisites. Request requires its supplied actual packet only for nonempty maps, captures its original target witness before frame preparation, and never rents.
3. Prepare every DTO page and complete native frame in owned 1032-byte packets, then retain immutable private frame bytes. All preparation/encoding/allocations and ordinary validation precede any caller output or Send.
4. Loading takes its actual recycled packet LAST, after all rows/pages/frames prepare. Preflight every prepared frame against the actual rental's capacity and lifetime/metadata. Preserve the actual successful rental identity. The existing pool Take dequeues and rewinds before returning, and Reuse only rewinds: a late incompatible-rental failure cannot promise allocator/cursor rollback. No invented peek/reservation or fake pool is introduced.
5. Revalidate the bound owner/map/revision/rows and actual target full-width cursor, Memory, Size and capacity before the first copy. Copy complete private prepared frame bytes into the same supplied/rented actual target and call real Send in page order, with no later producer allocation or refusal checks.
6. Ordinary preparation/refusal leaves target whole bytes/Size/cursor and send queue unchanged. Pool failures can occur only at the final rental frontier and have the explicit limits above. No resource, mail row/map revision or optional cached-values state changes. Prepared native buffers are owned and disposed after their complete managed frame snapshots are captured; no prepared pointer aliases the final target. Actual pool buffers still require exclusive ownership/lifetime because the existing pool does not prove alias freedom.

## Expected wire behavior and fixtures
- Retain independent existing 0/1/10/11/20 Loading and 0/1/10/11 request goldens, page quirks, actual different key/id fixtures and notification tests.
- Replace the unsafe partial-first-ten and empty-pool dependency assertions with complete rejection/optional-empty invariants. Add malformed late-row/page, oversize later-page, exact small/disposed/invalid target, complete prior buffer/queue preservation, repair/retry, equal-id ordering, and actual snapshot mutation controls.
- New controls execute only corrected source through Root's serialized gate; this helper does not execute the unsafe baseline or any tests.

## Evidence alignment
| Requirement | Source / bounded fixture | Status |
| --- | --- | --- |
| Fields and serializer | CMsgMailList DTO, LegacyNativeMail.CreateMailList, existing independent MailFrame | component evidence retained |
| Page geometry/order | Producers Loading/Process and existing ListAndNotify cases | retained source success sequence; client acceptance unproved |
| Sender limit/copy | GameClient.Send and actual Socket.Send synchronous queue copy | 1032-byte frontier; delivery acknowledgement absent |
| Owner snapshot | actual DbSafeDictionary SyncRoot/Revision/ToArray and real public mail rows | cooperating-owner evidence, not global row concurrency |
| Catalog/live client | unavailable for this bounded mail change | no new catalog or live claim |

## Implementation and qualification
- Production: CMsgMailList.Producers.cs, .PreparedPages.cs, .Publication.cs.
- Tests: existing ListAndNotify.cs plus one Preparation.cs partial.
- Root alone reviews/integrates/builds/tests. This package is source-only and unrun.
- Limits: exclusive target lifetime and cooperating map writers required. Public map rebinding, raw row mutation, ABA and global seal changes are not a linearizable transaction; bounded revalidation cannot make public fields atomic. Holding SyncRoot does not establish cross-owner/SQL/crash atomicity. Native Send filters/catches and can fail after first publication; no all-pages-delivered or rollback claim. Actual pool allocation/rental is not reserved or transactionally reversible. Decoder/request/claim and persistence seams remain outside scope.
- Source-bug ledger fragment: FSB223; Root merges after qualification.
