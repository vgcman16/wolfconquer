# Internal handoff connection lifetime

## Identity and scope

2026-11-03. Server engineering invariant for the existing internal world-enter
route, not a Chinese7937 login specification. Source authority is the actual
AccountHandoffService redemption and WorldServerFrameHandler dispatch. The full
SQL/native migration remains required; this closes its authenticated-connection
prerequisite without inventing a new client key meaning.

## Packet contract

Keep InternalWorldEnterRequestPacket and all successful response bytes/order.
No opcode, enum value, layout or encoding changes. A repeated enter on an already
bound/occupied connection is ignored and logged without a success response.
Existing invalid, expired and replayed-ticket responses on fresh connections
remain unchanged. No ticket token or password is logged.

## Catalog/table contract

Not applicable: no client catalog or SQL schema changes.

## Validation and transaction rules

- Before redeeming, reject observed transport closure, an existing Store session,
  an existing successful handoff binding or an already initialized native player.
- Require this Frame's actual installed connection-close lifetime. Another Frame
  cannot redeem on a connection whose close callback belongs to the first Frame.
  A native UID of zero alone does not establish empty state: its actual owner and
  uninitialized inventory/equipment/rune participants must also match.
- Dispatch already owns the real async exclusive world-operation lease. Reuse it;
  do not add a thread-affine Monitor across awaits or a second accepting owner.
- Only the actual successful redemption branch installs the private per-connection
  binding. A public ticket record, parsed key or failed redemption cannot install it.
- Once redemption commits, retain that binding even if loading or output later
  fails. A second ticket cannot change identities on that connection or consume
  another authorization. A new connection is required for a new enter attempt.
- Closure after redemption is a committed authentication outcome, not an undo of
  the ticket. Stop output/publication when closure is observed; retain the binding.
- Invalid/unknown/expired tokens do not bind an otherwise eligible connection.
- This provides no SQL revision, UID allocation, native-save or 7937 authority.

## Expected wire behavior and fixtures

Retain existing internal handoff workflow positives and file-store cold replay.
Add real-store/handler controls for a second valid account ticket on the same
connection, including pending creation and snapshot failure; verify the second
ticket remains unredeemed, first state is unchanged and no second success appears.
Include actual Store occupancy without a handoff binding, closed-before-enter,
unknown-then-valid, concurrent enters serialized by actual dispatch, and a new
connection positive. Use owned saves and real issued tickets, no accepting store
or callback substitute. Actual loopback cases must use the real TCP producer.

## Evidence alignment

WorldServerFrameHandler.Dispatch.cs already gives InternalWorldEnterRequest an
exclusive world-operation lease. HandleWorldEnterRequestAsync currently redeems
before checking its connection's existing session and has no general successful
handoff binding. StartWorldSessionAsync can replace that connection's Store state.
AuthenticatedCreationRedemption protects creation only, not every redeemed enter.

## Implementation and qualification

Authored: WorldServerFrameHandler.cs, its InternalHandoff/SessionStartup partials,
and AccountHandoffConnectionLifetimeTests.cs. Root reviewed all production hunks,
the real existing async operation lease and response callers. Thirteen authored
regression rows include actual two-Frame callback ownership, real TCP EOF and
encrypted requests against three initialized TCP-born native refusal states.
They built and all13 passed in the384-case affected run, which finished
382passed/2failed/0skipped. Two older creation controls expected a snapshot
reply after actual EOF or foreign-Frame ownership. Their reviewed repair checks
the real issued ticket remains identical/unredeemed, with no output/session/
permit, unchanged native/player, saved files and UID ledger, including a later
actual creation refusal. Production guards remain unchanged. Focused repaired
controls/neighbors passed19/19,0failed/skipped, including all13 new rows.
The earlier failures stay recorded. All44 actual SQL provider Facts separately
passed, including genuine ACK loss. The marker grants no SQL/native publication.
See the [actual results and limits](../../checklists/HANDOFF-SQL-RECOVERY-20261103.md).
