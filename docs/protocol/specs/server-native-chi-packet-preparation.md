# FSB-212: Chi message and query preparation

## Identity and scope

2026-10-29, existing native source-shaped Chi components, old7899/VisibleDx8D13
evidence; no new7937 activation or live acceptance. Opcode2276 ChiMessage and
opcode2165 single-row query retain their independent successful wire goldens.
Generic ranking's separate incremental APIs are unchanged.

## Packet contract

ChiMessage: UInt16LE declared length/opcode2276, byte power type, Int32LE packed
attribute, fixed32 name bytes with existing character-count UTF8 truncation.
Query: opcode2165, QueryCount, type60000000+power type, registered/page/finish0,
count1, reserved0, then actual rank narrowed to byte, score widened to Int64,
repeated UID/name fields and existing reserved padding. Optional eight-byte seal
retains the current native byte prefix and is excluded from declared length.

## Catalog/table contract

Not applicable. Existing actual power.Score computation remains the source of
the query value; no new score, cost, authorization or catalog meaning is inferred.

## Validation and transaction rules

Reject missing required stream/message name, query user/power or power name
before touching the target. Capture actual power scalars/name and calculate Score
before frame preparation. Shared FSB-209 PrepareFrame builds and bounds a genuine
scratch frame, then copies its complete bytes to the actual target and publishes
Size/cursor4. Ordinary preparation refusal preserves every target byte, cursor,
Size, power data and send queue. Never fabricate a usable player or transport.

Query sends only the completed frame. A real notification failure after frame
commit is not claimed to roll back the frame or prove delivery. No whole-power
concurrency, gameplay transaction, persistence or crash-safe atomicity is implied.

## Expected wire behavior and fixtures

Retain independent existing2276 and2165 success goldens. Replace earlier tests
requiring dirty output on null-name, null-user or Score failure with complete
preparation-refusal invariants. Check actual short-capacity refusal and exact
capacity success using initialized owned buffers, with and without the seal.
Never execute an unsafe old small-allocation control.

## Evidence alignment

Existing LegacyNativeChiTests.Packets.cs supplies independent positive bytes and
three actual source-defect controls. Shared FSB209 frame preparation is an offline
dependency. Source/captured metadata do not establish new-client compatibility.

## Implementation and qualification

Root owns the two Chi production paths and finite tests. New source is uncompiled
and unexecuted until the combined native packet qualification. The previously
retained neighbors01 run has the actual null-name exception mismatch; it is not
an unsafe old-buffer baseline. Full CI and live/SQL acceptance remain separate.
