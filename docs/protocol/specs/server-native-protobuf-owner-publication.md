# FSB-218: Publish message/Ninja owners only after complete protobuf decoding

## Identity and scope
- Revision1,2026-10-29; Root-owned offline repair after actual binary70 broad02.
- Base91040398 plus73 captured Root overlays. Existing7899/VisibleDx8D13 component evidence; no7937/profile/dispatcher activation.
- Exact production map: MsgMessage.Serialization.cs Deserialize; MsgNinja.InfoCodec.cs GetNinjaInfo; MsgNinja.ItemCodec.cs GetNinjaItem. Other writer/receiver regions stay byte-exact.
- Actual broad02 records9 relevant failed outcomes:4 short NinjaInfo lengths,1 MsgMessage false-input Size,1 MsgMessage early default publication,1 NinjaItem early default publication and2 malformed receiver decode boundaries. Root's183 missing-PS7 environment failures are a different category, not evidence for these changes.

## Packet contract
- Keep genuine schemas and pinned protobuf-net2.0.0.668. MsgMessage2179 required scalars/Count/raw enum bits and repeated Content are unchanged; NinjaInfo2394 and NinjaItem2393 keep their full existing widths/enums/repeated structures.
- Generic FSB-206 decoder starts at the absolute header, validates declared length against actual Size/Capacity and returns a new real DTO. Caller cursor may be beyond Size but within the actual allocation before that reset. Do not wrap these getters in ReadPrepared, which imposes a different initial input-cursor restriction.
- Null stream rejects explicitly; declared lengths below4/outside Size retain InvalidDataException, malformed actual payload retains pinned parse failure, and failures preserve the original generic cursor/input metadata.
- MsgMessage only replaces outer string slots present at decoded Content indexes0/1/3/6. Absent repeated Content (including serialized empty arrays) is valid and retains prior outer strings; the actual decoded Content remains null when the pinned serializer omits it. Never invent an empty array/string or additional required field. Count remains independent of Content length, and opcode is not newly validated.

## Catalog/table contract
Not applicable; no catalog or server-name lookup is added to decoding.

## Validation and transaction rules
- Required packet validation precedes publication. Deserialize into a local instance of the real existing DTO, never the currently published owner or a stand-in model.
- MsgMessage prepares every scalar/enum and present-or-retained string value before assigning its proto reference or outer fields. Ordinary failure retains the complete old proto reference/contents and all outer scalar/string values. Successful absent Content still publishes the genuine decoded scalars/proto and retains old strings.
- Ninja getters assign the out reference only after local generic decoding succeeds, preserving the original caller object/reference and nested aliases on failure.
- Existing NinjaItem Process order is unchanged: decoding precedes pool/requester lookup and every response packet take. Malformed request tests must retain real pool identity/nullness, original cursor, queue and recycle counts.
- Supplied Size is range-checked caller metadata, not independent capture provenance. Correct positive fixture Size to the actual written/copied extent; do not extend it from Capacity or a desired request length.
- This is exclusive-caller ordinary publication safety, not a concurrent object/lifetime gate, persistent transaction or complete receiver validation.

## Expected wire behavior and fixtures
- Keep all independent MsgMessage/Ninja Info/Item byte goldens, count/raw enum behavior, present-string-only mapping and pinned omission-induced string index shifts.
- Replace prior tests enforcing early default proto/out references and dirty failure cursors with prior-reference/outer-field/input preservation. Preserve existing case counts and real queue/model/recycle assertions.
- Source-authored corrected-only controls cover malformed varint after otherwise valid fields, invalid declared windows, null/disposed storage and actual header-reset success from a cursor beyond supplied Size. No unsafe old-code/OOB baseline.

## Evidence alignment
| Requirement | Evidence | Status |
| --- | --- | --- |
| Early owner/reference publication | The three old methods assign new DTOs before generic decode | Confirmed source defect |
| False Size and stale failure expectations | Actual broad02 TRX9 selected outcomes and current tests | Repair finite fixtures/defect assertions |
| Valid schema/wire behavior | Existing independent pinned message/Ninja controls | Preserve |
| Live receiver/7937 behavior | No new capture/runtime evidence | Unproved and unchanged |

## Implementation and qualification
- Root alone integrates/builds/tests. Helper writes the finite3production,4existing-test,3focused-test paths and this spec/FSB218 fragment only.
- The3 corrected-only partials author16 outcomes (MsgMessage6, NinjaInfo5, NinjaItem5); they remain unbuilt/unrun. Existing test outcome counts and independent valid goldens are retained.
- No restore/build/test/dotnet/live/native/process/SQL/Git execution or actual checkout mutation. Corrected-only controls remain unbuilt/unrun.
- Other MsgMessage send/build behavior, Ninja costs/owners/ranking flows and broader SQL/client/parity gaps stay separate.
