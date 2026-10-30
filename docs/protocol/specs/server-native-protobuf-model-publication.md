# FSB-216: Publish outer protobuf models only after complete decoding

## Identity and scope
- Revision1, 2026-10-29; Root native packet qualification batch.
- Base: public91040398 plus the Root overlays captured in DEPENDENCY.json, including actual generic protobuf bounds from FSB-206.
- Target remains the existing native component and retained 7899/VisibleDx8D13 wire fixtures. No new7937 profile, receiver, schema or opcode admission is introduced.
- Three owners: GetItemRefineRecord, GetTitleStorage and GetRankMemberShow. Their creators/serializers, model classes and protobuf attributes are unchanged.

## Packet contract
| Getter | Existing model/creator opcode | Preserved behavior |
| --- | --- | --- |
| GetItemRefineRecord | ItemRefineRecord /2266 | Existing required/optional tags and nested RefineRecord graph |
| GetTitleStorage | TitleStorage /2313 | Existing action enum, scalar values and nested Title graph |
| GetRankMemberShow | MsgRankMemberShowProto /2054 | Existing Type/Items and all21 nested entry fields |

The generic decoder reads the declared frame from absolute offset0. Caller cursor is not a header locator; opcode matching is not invented. FSB-206 requires actual supplied Size within Capacity, a complete four-byte header and declared length in4..Size. Only the declared managed payload reaches the pinned protobuf-net implementation. Successful cursor becomes declared length; parse/framing failure retains the original raw cursor and Size.

## Catalog/table contract
Not applicable: no catalog or protobuf schema changes.

## Validation and transaction rules
- Reject a null required packet explicitly before assigning any caller output.
- Allocate the actual local protobuf DTO, call the actual generic bounded deserializer, and assign the outer out parameter only after complete success.
- Refusal preserves the caller's prior out reference, including null, and every prior model/nested alias/value. No default DTO is published on a parse, framing, disposed-storage or null-packet failure.
- Do not add required-field admission, normalize missing arrays/nested DTOs or change pinned protobuf default/unknown-field behavior. Known valid empty/null serializer semantics remain intact.
- Direct-copy test fixtures set Size=bytes.Length immediately after the actual copy. Spare allocation, poison/reset Size, or untrusted header length must not define the input window.
- Mutable Size is still caller metadata rather than independent capture/authentication proof. Public memory/lifetime changes have no common concurrency guard. No gameplay, queue, SQL or transport authority is added by decoding.

## Expected wire behavior and fixtures
- Keep every existing independent successful frame, creator default/null behavior, protobuf attribute/schema assertion and nested data assertion.
- Replace default-model/dirty cursor/OverflowException failure expectations with the original out reference, original model/nested values, actual input bytes/Size, original cursor and empty queue.
- New corrected-only controls cover short headers, declared tails beyond actual captured input, truncated scalar/nested payloads, null packets, null prior outputs and disposed storage. Every native allocation is owned/initialized; no old unsafe baseline is executed.

## Evidence alignment
| Requirement | Existing source/tests | Status |
| --- | --- | --- |
| Local decoding before native cursor commit | FSB-206 generic ProtoBufferDeserialize | Already implemented dependency |
| Outer output must remain original on failure | Three getters assign a new DTO before calling Deserialize | Confirmed source defect |
| Genuine captured input length | Title.Input, Ninja.PlaceBytes and direct refinement copies retain reset Size321/333 | Confirmed fixture defect |
| Valid model/default behavior | Existing pinned goldens, field/tag contracts and Ninja empty-payload control | Preserve; do not invent new admission |

## Implementation and qualification
- Production edits are limited to the three getter bodies; creator regions and generic codec/model/schema sources are byte-exact.
- Existing tests: ItemTransport.RefinementRecord, NinjaRank.Codec, Title.Decoder and Title.Fixture.Input. A new CorrectedOnlyNativeProtobufModelPublicationTests file covers outer failure publication.
- Source-only package; no restore/build/test/dotnet/native/client/process/SQL/live configuration or Git mutation. Root must compose/review and run the actual combined gate before a verified fixed claim.
- FSB-216 ledger fragment and HANDOFF retain qualification limits. No client acceptance, profile activation, complete protocol or full parity claim.
