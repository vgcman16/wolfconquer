# D13-NINJA-POINTS-1: Coherent checked point credit

## Identity and scope

Revision 1, 2026-10-27, Root-approved correction of the actual AddPoints lost
credit race. Target is VisibleDx8D13. Evidence is current managed/reference
source plus the retained independent MsgNinjaUser scalar goldens, not new client
traffic. This only changes the component AddPoints operation; no host binding,
paid AirPower debit, reward authority, balance or persistence schema is added.

## Packet contract

Opcode 2395 retains the real 21 required MsgNinjaUser fields. Type is 2, Points
is the complete updated UInt32 total, UID and unrelated fields remain zero.
Packet UID zero is source wire behavior, not runtime actor authority. Prepared
user framing retains the existing little-endian header, protobuf required/default
fields, captured optional seal and total 1032 byte send bound. Valid zero credit
still produces one notice, including at UInt32.MaxValue. No retry or message
chunking policy is invented.

## Catalog/table contract

Not applicable: point credit does not consult item, rank or magic tables. The
existing UInt32 storage width is retained. The real current constructor-owned
Ninja, physical player/client binding and positive actor UID are required.

## Validation and transaction rules

The previous code reads/checks Exp before the separately locked property setter,
so two concurrent credits can pass the old-value check and store the same total.
The corrected operation refuses active real Ninja/Spell preparation entry before
actor locks and repeats that discipline under actual client/player gates.
It then holds the actual Ninja persistence gate across reading the current
immutable state, checked addition and complete private state/frame preparation.
Overflow uses OverflowException before any rental and cannot wrap or credit.

Real storage is admitted before any copy or state store. A last plain actor and
state-identity fence precedes packet copy and one prepared-state reference store;
no callback, lookup or state allocation separates those operations. Other fields
and physical item-map references are retained in the copied aggregate. Thus
cooperating point callers credit from the preceding committed state, while
required-state/frame/storage rejection cannot publish Exp or send success.
Zero credit does not bypass owner, entry, frame or storage requirements.

Invalid required ownership/gate/storage uses InvalidOperationException; invalid
seal/frame uses InvalidDataException. Send occurs after all held gates release.
The source rental lifetime may be consumed on storage refusal but cannot write
frame bytes or credit. A postcommit transport failure is not a rolled-back credit.

## Expected wire behavior and fixtures

Retain the independent Type 2/UID zero full-width and zero-credit goldens, checked
overflow and null-owner assertions. Only the existing missing-pool exception
expectations change to controlled InvalidOperationException. A separate test
lane supplies genuine-gate concurrent callers and physical owner/storage controls.
Tiny/disposed native cases are corrected-only and must not run old writers.
No fake serializer callback, accepting owner or retry-until-success is permitted.

## Evidence alignment

Observed: Points.cs has a read/check followed by a separately locked Exp setter;
Persistence.State.cs owns the real immutable aggregate and gate. Derived
correction: serialize complete credit preparation/publication under those actual
gates. Existing independent scalar goldens retain successful wire behavior.
Unresolved: concurrent notification order after gate release, arbitrary raw
external field changes/disposal, paid outer operations and durable persistence.

## Implementation and qualification

Source and the minimal existing-test adaptation are isolated in
helper-ninja-points-safety-next. Focused new tests are in
helper-ninja-points-tests-next. This consumes the prepared user frame/storage and
Ninja admission helpers, without editing frozen Flags or parent core files.
Root alone integrates, allocates the source-bug ledger entry and executes checks.
No builds/tests/native/live/main/Git actions were performed.
