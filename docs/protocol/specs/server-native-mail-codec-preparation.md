# FSB-215: complete native Mail codec preparation

## Identity and scope

- Revision2026-10-29, Root-authorized offline source work after FSB206–213.
- Native community-source Mail component; existing historical7899/D13 evidence
  does not qualify these native codecs or imply7937/live acceptance.
- Shared actual packet dependencies: ReadPrepared, PrepareFrame and bounded
  native protobuf/primitive methods in Root's70-path candidate from91040398.
- No dispatcher, operator-mail tooling, retained nine-mail request, real mailbox
  persistence, authentication, costs or transport changes are included.

## Packet contract

| Field | Wire contract | Evidence |
| --- | --- | --- |
| Mail list frame | Opcode2183; genuine existing required-field ProtoBuf schema | LegacyNativeCMsgMailList and existing independent MailFrame fixtures |
| List Type/Count | Required int fields1/2 | Preserve values independently; Count is not inferred from row count |
| Mail rows | Repeated field3; seven required ulong fields then Title/Sender_Name strings8/9 | Existing nested Mail schema; no narrowed values |
| Mailbox mode frame | Opcode2264; mode UInt16LE, ID UInt32LE, count UInt32LE, selected count UInt64LE items | Existing primitive literal |
| Mode input | Native byte enum narrows UInt16; unknown bits preserved according to existing cast | Existing positive high-bit fixture |
| Header/seal | Actual shared Finalize rules, optional eight UTF8 seal-prefix bytes | Existing packet dependency |

Mode parsing starts at the current body cursor; it does not validate opcode/header
or invent a new profile. Genuine captured Size limits input; spare allocation does
not extend it. Size remains caller metadata, not proof of receive provenance.

## Catalog/table contract

Not applicable. No catalog or item ownership semantics are derived by this codec.

## Validation and transaction rules

- Explicitly reject null stream and required list message. Do not serialize a
  missing message into a successful empty frame.
- Snapshot list Type/Count, clone the actual row array and capture each actual
  row's exact scalar/string state before scratch serialization. Null array remains
  an absent repeated field; nonnull arrays reject null rows rather than silently
  omitting malformed entries. Title/Sender_Name keep their existing required
  annotations and supplied nullable values. The pinned serializer determines their
  representation or refusal; no null-string omission or fallback is invented.
- Use an internal copy constructor on the actual existing Mail DTO, preserving
  every ulong field without narrowing. Its existing public LegacyNativeMailList
  projection constructor rejects a null required source explicitly; no schema
  replica or placeholder record is introduced.
- Mode count0 requires no item rows and preserves existing optional-null Items
  behavior. Positive count requires a real array with at least count elements;
  preserve extra source elements but serialize only the selected prefix.
- Refuse count/length/capacity before copying or allocating a result array. Use
  arithmetic bounded by actual supplied remaining bytes (reader) or real target
  Capacity/header/seal overhead (builder); do not clamp count or fabricate data.
- Prepare complete list and mode frames using actual owned scratch packets;
  normal rejection preserves every target byte, raw cursor and Size.
- Parse mode into a complete local tuple under ReadPrepared; assign every out
  parameter only after complete success. Reject malformed/truncated/count-excess
  input with unchanged original out values/array reference and whole cursor.
- No concurrent packet lifetime/foreign mutation/crash-atomicity guarantee follows.

## Expected wire behavior and fixtures

- Retain independent nested required-field list bytes and literal2264 mode frame,
  count0/null-items, unknown enum narrowing and selected-prefix semantics.
- Replace old partial-prefix writes, default-at-EOF and null-dereference tests
  with explicit validation and complete unchanged-state assertions, retaining
  existing case counts and model/queue checks.
- Correct the positive copied-byte fixture's false Size5 to its actual copied
  extent. The separately approved roulette fixture captures actual copied frame
  length immediately after Marshal.Copy; all its other assertions remain.
- Add corrected-only truncated later-item, excessive count, exact/small capacity,
  disposed and oversized list/required-null controls with real owned storage.
- Do not execute unsafe older code as a failure baseline.

## Evidence alignment

| Requirement | Evidence | Status |
| --- | --- | --- |
| Existing successful wire output | LegacyNativeMailTests.Codecs + MailFrame | Retained fixture evidence |
| Partial publication/prefix defect | LegacyNativeMail.cs and old dirty-output/EOF tests | Source defect; correction to qualify |
| Genuine copied input extent | Scope.Input(bytes), direct roulette Marshal.Copy | Fixture correction; not authentication authority |
| Actual client/dispatcher/mail transaction | No new live capture | Unproven and unchanged |

## Implementation and qualification

World/LegacyNativeMail.cs and the approved LegacyNativeCMsgMailList.cs DTO support,
existing Mail codec/construction tests, approved roulette input fixture and two
focused corrected-only test partials form the finite8-path patch. The new controls
author18 outcomes:11 reader cases and7 writer/copy cases, all unbuilt/unrun.
Helper does no restore/build/test/live execution. Root reviews and qualifies the
combined source batch, retaining any actual old-code Mail failures separately.
FSB215 records bounded source repair rather than full mailbox functionality.
Existing LegacyNativeMailList runtime constructor/persistence/transaction behavior
and unrelated roulette writer defects remain outside this change.
