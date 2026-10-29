# MANAGED-MANA-1: owned Mana-consumable Character/Items transaction

## Identity and scope

Revision1, 2026-10-29, Root director decision. Target the active managed
VisibleDx8D13 MsgItemUsage Mana-consumable route in the current public candidate,
using FSB182 health's real Frame/Store/ordinary/HP owner and v2 journal mechanisms.
The current full40826474342 is Root-owned and in progress; no execution is
authorized for this source package. Previous native-Mana design remains separate.

This slice changes managed MinMana, exactly one selected item unit and the accepted
potion timestamp in one validated transaction. HP, MaxMana, currencies, other item
rows/identities, paid state and unrelated sidecars stay unchanged. It does not
hydrate, mutate, validate against or claim ownership of physical Player.Mana.
The actual HP participant must still validate/publish its unchanged physical HP.
Native Mana, native item containers, raw setter/cast ownership, SQL, power loss
and complete native/managed tuple consistency remain OPEN.

## Packet contract

No opcode/layout changes. Preserve the actual supported MsgItemUsage item-use
actions and legacy successful sequence: selected stack update/removal, then one
opcode2176 Mana(type2) update carrying the committed managed value. No new HP or
MaxMana update is introduced. Existing serializer/goldens define framing and
protobuf fields. Rejected/no-change use emits no success packet.

## Catalog/table contract

Use existing Classify/IsLegacyManaConsumable, ItemType.ItemMP(field19), stack cap
(field46), current saved/managed MaxMana and the existing Calculate rule. Mana
amount must be positive uint and stack metadata a valid ushort; stack0 means one
physical unit, matching existing consumption. Keep the150ms rule and actual
full-resource no-change behavior; do not apply the health boost to Mana. Missing,
duplicate, stale or malformed required metadata refuses before cooldown changes.

## Validation and transaction rules

Resolve the supplied exact current holder under actual packet operation, trade,
PIN-consistency and Store/native identity owners. No equipment-only checked-PIN
requirement or blanket trade prohibition is added. Offered potion refuses; an
unoffered potion may succeed while retaining all unchanged actual offered row
identities and the valid trade closure. Reject disconnected/retired/foreign or
recovery-blocked owners and unresolved escrow without inventing an owner.

Capture bounded complete managed item graph and immutable Character/catalog
inputs. Validate alive state, exact UID/location, uniqueness/deposits, stack/cap,
current cooldown, amount and resulting managed Mana with the existing calculator.
Prepare detached complete Character and exact opaque-preserving raw Items images
without live mutation. Publish only the consumed row mutation; retain all other
live roots/children. Canonicalize the final Character attributes with the existing
pure full scalar projector so a later legitimate use sees the exact saved state.

The focused managed Mana writer permits ONLY MinMana scalar delta and the selected
one-unit Items transition. It binds Character by account ID and Items by character
UID, preserves encoding/comments/unmodeled bytes, rechecks both preimages under
actual file locks, invokes the real v2 TryCommitSingleOwner journal, then verifies
both exact afterimages and no pending journal. No accepting saver or fake queue.
Reuse the existing pure item-image logic through a resource-kind-restricted common
serializer while preserving the old health helper's contract.

The existing real Store.TryCommitSession owns publication. Since MinHitPoints is
unchanged, HP's typed scalar write is absent; its physical baseline proof and plain
unchanged publication still run. Do not create a fake physical-Mana participant.
Only after the journal's proven result may holder/cooldown publish, followed by
notifications outside mutation gates. Definite refusal leaves saved/managed/native
HP, actual prior physical Mana, items, currencies, cooldown, PIN state and output unchanged.
Uncertain/postcommit failure fences/retires the exact owner, with no automatic
retry/refund. Committed notification failure remains committed and cannot consume
again through the retired owner.

## Expected wire behavior and fixtures

Genuine ordinary-admitted Frame fixtures with real metadata/UID registry and files;
no reflected HP/Mana seeds. Cover last-unit/stack/native-zero-stack, exact durable
images and managed Mana before first output, unchanged physical HP/managed MaxMana,
literal consume+Mana packet order, opaque bytes and all unchanged item identities.
Require second accepted use after150ms to prove canonical saved/holder continuity.
Missing/stale metadata, cooldown/dead/full-resource state, overflow stack, stale
Character, duplicate UID, retired/disconnected owner and real file replacement failures
must preserve rejection invariants. Exercise actual v2 pending/Character/Items
interruptions, fresh recovery and old-owner replay, notification faults, and real
two-client offered/unoffered trade through both confirmations.

Successful tests do not require a stale/zero physical Mana value or absent native
containers. Those are current integration limits, not desired gameplay contracts;
future genuine hydration/ownership may agree. Rejection compares any actual prior
physical Mana value without imposing an invented value.

## Evidence alignment

| Requirement | Actual source mechanism | Status |
| --- | --- | --- |
| No early cooldown/resource publication | Old ItemUsage publishes timestamp before metadata and later Set | Confirmed managed bug; route to prepared transaction |
| Real saved owner independent of physical Mana | HP PrepareBound accepts unchanged HP; no HP write when unchanged | Supported current API; retain all owner checks |
| Complete Character+Items durability | Health write/v2 journal/exact before+after images | Reuse pattern with only MinMana delta |
| Preserve actual trade offer identities | Health live-item projection after genuine trade regression | Required same identity-preserving construction |
| No native tuple claim | Physical Mana is unhydrated/direct and untouched here | Explicit OPEN boundary |

## Implementation and qualification

New focused managed-Mana owner/plan/handler/write modules, a common pure consumable
item-image module retaining the health wrapper, and one narrow dispatch branch
before the old cooldown path. New genuine fixture/success/refusal/recovery/trade
tests. Exact pre/post images and manifests accompany the isolated package.
Root alone integrates/builds/tests after the active full gate and binary
preservation boundary. Source review/green focused tests would not establish live
client, deployment, SQL/power-loss or physical Mana consistency. Root assigns and
updates the eventual source-bug verification entry; this spec is not a fixed claim.

Root qualification2026-10-29: actual build08 zeroerrors and combined selection
205passed/2existing external-health skips/0failed in1m18s. Original FullRestore
refusals2/2 and restoration/Mana controls now pass. This includes genuine journal,
recovery, trade, reload and mailbox-kit child/delivery/claim checks. The full
Windows publication gate and live-client acceptance are still separate/pending.
