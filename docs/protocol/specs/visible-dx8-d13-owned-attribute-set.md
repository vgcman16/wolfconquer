# Visible DX8 D13: owned attribute allocation

## Identity and scope

Draft 2026-10-31; target Chinese 7899 / VisibleDx8D13. The admitted player's
client sends the legacy attribute allocation packet. This increment covers the
four attributes and remaining points, their Character file and native owner; it
does not claim general player persistence or later client acceptance.

## Packet contract

Opcode 2203 is `LegacyAttributeSetPacket`: a four-byte reserved word followed
by Strength, Agility, Vitality and Spirit UInt32 values in that order, for a
20-byte little-endian payload. The existing codec emits reserved zero; the
reader does not assert its value. Existing codec tests are local wire evidence.
The original `MrRayzo.Game.MsgServer/MsgAtributeSet.cs` echoes 2203 and then
calls the complete `Equipment.QueryEquipment(Alternante, CallItems:false)`.
Preserve that order: the actual query includes derived spell work, status,
source-ordered HP phases, refinement and its WindWalker postlude. `CallItems:false`
omits equipment refresh and ServerInfo/peer-spawn phases. The earlier local
HP/refinement-only description was incomplete. Unknown or malformed frames refuse.

## Catalog/table contract

Point allocation has no new catalog. The required full query consumes the actual
configured equipment, rune, gear, status and derived-spell inputs through their
existing owners. Preflight those required inputs and real serialization before
the allocation write; missing required state refuses without a debit.

## Ownership and transaction

Require the actual Frame, current connection/Store holder, admitted native
client/player binding, clear recovery and a safe absolute Character route.
Capture and revalidate the exact current Character before image and live input;
reject negative/insufficient points, overflow, or a resulting native stat above
UInt16. Prepare the echo, full-query prerequisites and the complete independently
validated Character after image before spending. Preserve unrelated bytes and encoding.
Atomically replace the one Character file under its real file/owner gates, then
publish the matching session and native four fields/points as one owned result.
Failures before a durable replacement leave state and output unchanged. If a
durable outcome cannot be proven or native/session publication fails after the
replacement, fence the owner and require recovery; do not report rollback.
The real 2203 Frame holds one exclusive world-operation lease throughout. Echo
only after publication, then invoke the actual full query in that same operation;
never enter a nested Frame or substitute accepting callbacks. Query admission,
native/item owners, derived spell rows, routes, numerical bounds and codecs are
preflighted before spending and actual query owners revalidate at each phase.
Numerical preflight replays current HP and maximum in the actual phase order:
Circle, Fury, ordinary contributions, VIP, SubClass/Rune storage, unconditional
retained Blood HP, Rampage, Sacrifice's conditional setter, FineRain/Rise, the
ordinary signed clamp, and finally Transform's absolute setter. A later clamp,
reduction or Transform cannot hide an earlier invalid signed addition. The same
checked setter/clamp helpers run in the real query. Vigor retains its valid
source clamp order but rejects values outside the intermediate/native widths.
HP projection edits only its owned persisted HP field and preserves the plain
attribute comparer and unrelated case-distinct keys; ambiguous owned HP refuses.

The four native magic fields are combined in actual query order during preflight,
including active gems, Witchery/Nature's Chant, selected-item prestige, gear,
profiles, the real retained interserver JiangHu cache, Rune storage and Rise.
Execution and preflight share checked field combinations and percentage rules;
query-specific provider sums/narrowings also refuse overflow before producing a
wrapped contribution. These DATA calculations grant no native/query ownership.
An echo failure, declined rebuild, owner loss or partial rebuild after commitment
fences the actual binding/recovery. It cannot refund points or report a clean
precommit refusal. This is ordered committed progression, not a crash-atomic
transaction covering every query write/output. Fresh admission rebuild after a
committed interruption remains a required regression and distinct proof boundary.

## Examples and validation

An admitted player with ten available points may allocate ten Spirit; the saved
Spirit, remaining points, native player, session and ordered success frames must
agree. An over-allocation, unrepresentable native result, stale file, blocked
recovery or inaccessible file must leave the preimage and success output intact.
Tests must use actual admission, packet dispatch and owner, including repeated
attempts and a real file refusal. Source-only work is unrun until Root verifies.

## Evidence and open questions

`WorldPacketHandlers.AttributeSet.cs` and `LegacyAttributeSetPacketTests.cs`
describe current legacy behavior; the community server is not client authority.
The previous generic Store mutation did not establish a native stat owner or
atomic durable publication. Client rendering, process-crash recovery, SQL and
the complete game are not established by a focused source test.

The earlier incremental HP/mana owner draft is held in
`.artifacts/helper-attribute-cache-hold/HANDOFF.md`; it is not the 2203 execution
contract. A full query must also rebuild Spirit's native magic attack contribution
and the other derived caches. New implementation and tests are SOURCE ONLY,
UNBUILT/UNRUN until Root qualifies them. Local source sequencing is not live-client
wire acceptance, and no multi-file/outbox crash-atomicity claim is made.

Arithmetic follow-up controls live in `LegacyEquipmentQueryBoundsTests.cs` and
`LegacyOwnedAttributeArithmeticTests.cs`. They include a native Witchery sum that
overflows after allocation although the ordinary status projection still fits,
retained Blood with no flag, Vigor narrowing, and a genuine HP-changing query
with opaque case variants. These new controls remain UNBUILT/UNRUN. This does
not qualify every legacy calculator, optional system, time-sensitive provider or
other caller's complete query transaction. Existing runtime ownership and
postcommit fencing remain necessary; process-crash/restart proof stays separate.
