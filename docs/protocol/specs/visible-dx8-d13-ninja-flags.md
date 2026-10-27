# D13-NINJA-FLAGS-1: Prepared flag notifications

## Identity and scope

Revision 1, 2026-10-27, Root-approved bounded encoder-caller correction. Target
is VisibleDx8D13. Evidence is the existing managed/reference Ninja flag source
and retained independent scalar protobuf goldens, not new traffic or client
execution. This extends D13-NINJA-LOAD-1's safe user-frame helpers for AddFlag and
RemoveFlag only. No dispatcher, gameplay eligibility, balance or persistence
schema is activated or changed.

## Packet contract

Server-to-owner opcode 2395 keeps the actual MsgNinjaUser model's 21 required
scalar fields. Type is 1, Flag contains the resulting UInt64 mask, and UID and
all other fields remain source-default zero. Packet UID zero is wire behavior;
it does not authorize a zero-UID runtime actor. Existing little-endian header,
required protobuf tags/defaults, captured optional seal and total 1032 send bound
are supplied by the reviewed prepared user-frame helpers. Successful cursor is
4 before rental disposal; disposal retains the existing cursor-zero convention.

## Catalog/table contract

Not applicable: flag mutations do not consult item, skill, rank or magic tables.
The bit mask is retained as the source UInt64 value, without inventing an enum
allowlist. A genuine current constructor-owned Ninja/client/player is required
for an emitting operation; the actor UID must be positive.

## Validation and transaction rules

ContainsFlag remains an any-overlap query. Add returns without owner/packet
access when any requested bit already overlaps; Remove returns when none does.
AddFlag(0) still emits Type 1 even though the value is unchanged. No-op branches
retain their original irrelevant-owner timing.

An emitting operation refuses actual active Ninja preparation or a held current
SpellGate before actor locks, then repeats the existing discipline under the
real client/player gates. Under the actual Ninja persistence gate it captures
the complete immutable state, validates the physical actor, allocates its private
replacement and prepares the complete frame before borrowing/writing storage.
Required storage and final owner/state identity are checked before packet copy.
No fallible lookup, callback or state allocation follows that copy before one
plain prepared-state publication. Send occurs after held gates are released.

Rejected preparation cannot publish a Flag change or send a success frame.
Invalid seal is refused before a loan. Failed real storage admission may consume
its source-shaped rental lifetime, but cannot write frame bytes or commit Flag.
Null/foreign/missing required owners and unavailable storage use controlled
InvalidOperationException; invalid frame/seal uses InvalidDataException.
Postcommit transport failure is not claimed to roll back a committed flag.

## Expected wire behavior and fixtures

Keep independent full-width/composite/zero/result-zero goldens and any-overlap
controls. Replace null-owner dirty-prefix acceptance with unchanged state and no
loan/send. Add real foreign/zero actor, malformed seal and actual preparation
reentry controls. Genuine tiny-buffer cases are corrected-source-only and must
never execute the unsafe old encoder.

## Evidence alignment

Observed: Flags.cs publishes before CreateNinjaUser; the DTO and independent
Frame writer establish the successful scalar layout. Derived correction:
private frame/state preparation and real owner/gate admission. Unresolved:
client delivery, normal host adoption, arbitrary external raw mutation/disposal,
durable flag persistence and concurrent notification ordering.

## Implementation and qualification

Two production postimages plus focused tests are isolated in
helper-ninja-encoder-callers-next. They depend on the reviewed prepared frame
and Ninja admission helpers; no parent Storage/Loading implementation is edited.
Root owns integration, source-bug ledger allocation and actual qualification.
No build/test/native/live/main/Git action was performed.
