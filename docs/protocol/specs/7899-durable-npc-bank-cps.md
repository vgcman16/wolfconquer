# 7899-durable-npc-bank-cps: owned CP bank confirmation

## Identity and scope
- Revision 1, 2026-10-30, source-only successor; proposed FSB-239.
- Chinese 7899 `VisibleDx8D13`, actual NPC 102177 / `NpcHandler.BankCps`.
- Preserve established request/dialog/update bytes. Close the real confirmation
  path's publish-before-save and premature pending-consumption defects.
- No client launch, host change, global parser/cap change, fake owner/queue/saver,
  SQL/global economy durability or current-patch qualification.

## Packet contract
- Registered `LegacyNpcServerRequestPacket`, script/cursor routing as already
  accepted by the NPC handler. NPC 102177 input options 10/20 stage a positive
  UInt32 amount; confirmation options 2/3 apply deposit/withdraw respectively.
- The actual registered transports are NPC server 2084 and NPC reply 2255.
  Reply NPC0 resolves through the captured ActiveNpcId routing hint and is
  revalidated against the actual current banker under Exclusive.
- Existing valid confirmation output: one MsgUpdate CP field 29; dialog text
  `Done. You now have <CP> CPs with you and <bank> CPs in the bank.`; Thanks option
  255; avatar 183; finish. Existing input/confirmation/rejection texts remain.
- No new packet/opcode or request transaction nonce is inferred.

| Field/path | Number/offset | Wire type/width | Meaning | Presence/repetition | Bounds/default | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| input options | 10 / 20 | existing NPC option byte + input text | stage deposit/withdraw | request | invariant uint > 0 | BankingServices.BankCps.cs |
| confirm options | 2 / 3 | existing NPC option byte | confirm current pending amount | request | exact current amount/direction required | same |
| CP update | 29 | existing MsgUpdate UInt64 scalar encoding | inventory CP after commit | one | current native slot uint where bound | OwnedCps / MsgUpdate |
| PendingBankConquerPoints | session field | nullable UInt32 | current confirmation authority | transient | cleared only with successful transfer | WorldSession.cs |
| BankCps | saved INI key | nonnegative signed-64 text | current saved bank CP baseline | required on routed write | no invented absent default | existing pickup BankCps baseline |

## Catalog/table contract
- No new binary catalog. NPC visibility is revalidated through the existing real
  catalog/click gate under the actual operation owner, before any pending change.
- Character writer requires exact identity, raw nonnegative CP and BankCps
  baselines, coherent warehouse aliases and unchanged other currency state.
  Duplicate fields, malformed/negative/overflow/missing required bank fields and
  stale beforeimages refuse. Preserve unrelated text/line endings and item bytes.

## Validation and transaction rules
- Parsed bank mutation requests own the actual Exclusive. The registered NPC
  path must route these before its generic ActiveNpc Set and Frame must not hold
  Shared while the bank handler acquires Exclusive. Resolve the actual current
  holder/identity, current pending amount/direction, PIN, ended connection,
  trade/escrow and recovery state under that operation.
- Confirmation requires the exact supplied holder to remain current after
  Exclusive acquisition; a queued confirmation must not consume a newer pending
  amount from a replacement holder. Input staging may refresh the same identity.
- Actual Frame and invocation each classify bank routing. If an earlier bank hint
  resolves to a foreign NPC in the subsequently captured context, invocation must
  acquire its own Shared lease before foreign dispatch. If an earlier nonbank
  hint resolves to bank after Frame acquired Shared, the bank wrapper refuses the
  retained actual Frame lease before attempting Exclusive. Neither direction
  grants a global numeric-option dispatch bypass.
- A valid input stages the real transient pending amount via current Store CAS;
  it does not debit or persist wallet state. Failed input or owner/physical proof
  must leave any existing pending confirmation unchanged.
- Confirmation validates current funds and checked final nonnegative signed-64
  CP/bank values. Native bound CP/BoundCP/bank representability is verified by
  the existing closed actual Store participant before a writer; do not spill
  through an emitting native setter or invent a login queue.
- Prepare the complete updated immutable session and all success bytes first.
  The proposal changes only CP, bank CP and their two attribute projections,
  and clears the matching pending amount/direction on successful publication.
- Derive stable BCL writer maps from the existing raw NativeValueProof capture,
  keeping its source-row witness. Permission/escrow reads inside the writer use
  prepared maps; a pure source proof follows before the real WAL. No caller
  attribute-map wrapper callback executes inside this bank writer after Store
  preflight. Existing actual canonical item graph/file proofs remain separate;
  this does not exclude noncooperating raw writers or prove global isolation.
- Use actual Store.TryCommitSession plus a closed real
  `LegacyNpcSalePersistence.TryPersistBankCps(original, updated, itemsRoot,
  deposit, amount, [interruption,] out reason)` API. It validates exact conserved
  CP/bank delta and unchanged other character/session/item state and commits
  actual Character + unchanged Items images through the existing v2 WAL.
- Refuse missing real routed/native roots, Store, operation/recovery owners.
  Entirely in-memory/no-native/no-route fixture compatibility may remain without
  inventing disk durability. No generic Set plus best-effort scalar saver on the
  actual routed confirmation path.
- Before-writer drift/refusal preserves resources, files and pending state.
  Failed final native proofs after genuine durable commitment retire/fence;
  uncertain WAL work blocks recovery and emits no success. Replies occur only
  after committed native/canonical publication and release of Exclusive.
- Repeated confirmation after success has no pending amount and cannot repeat
  that confirmation. A separately restaged request is a new transfer against
  current funds; this is not network request-ID deduplication.

## Expected wire behavior and fixtures
- Real owned Frame fixture with the real visible BankCps NPC and current
  admission/native binding. Exact input/confirm/update/Done goldens; saved/current/
  native tuple before first success reply, no extra native setter output.
- Current-funds/capacity/representation/negative/malformed-bank refusal preserves
  pending. Prewriter raw tuple drift and actual catalog/input callback drift must
  not reach the writer. Genuine postwriter drift must fence without refund.
- Real Windows leaf-sharing refusal/retry, exact stale/delta/route validation,
  v2 WAL bounded interruption and independent-owner recovery/replay controls.
- Existing direct managed tests that required success before failed persistence
  must become safe refusal invariants; retain meaningful established wire goldens.

## Evidence alignment
| Requirement | Spec clause | Catalog reference | Wire fixture | Status/conflict |
| --- | --- | --- | --- | --- |
| options / pending authority | packet/transaction | NPC 102177 | existing banking tests | observed source |
| CP native physical tuple | validation | no new table | owned CP publication fixtures | existing actual source, not live |
| bank persistence before reply | transaction | raw Character/Items | new owned WAL fixtures | focused source verified; full gate pending |
| latest-client support | scope | unavailable here | unavailable | not qualified |

## Implementation and qualification
- Modular BankCps plan/transaction partials, scoped NPC early route/Frame operation
  classification and strict CP-bank persistence mode in existing WAL machinery.
- Root integrated and compiled production/tests with zero errors. The combined
  currency/CI batch passed387/387 focused outcomes, zero failures/skips, including
  all97 new CP-bank outcomes and the sibling readonly-bank input control. Its
  own public-commit full Windows gate remains pending. Historical a17 run
  41082514020 failed; its4 names now pass in this focused integration batch.
- Preserve frozen CP/warehouse source and all held/live/original data boundaries.
