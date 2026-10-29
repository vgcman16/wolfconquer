# VisibleDx8D13: owned health potion Character/Items transaction

Root qualification update: the initial health selection passed 57/57 and broader
callers passed 350/350 with no skips. A subsequent genuine two-player test
reproduced an unrelated trade offer being invalidated by detached live rows.
The identity-preserving follow-up passed both cases without changing assertions;
see [the follow-up spec](visible-dx8-d13-health-consumable-trade-identity.md).
The final combined native/ordinary/equipment/health/trade suite passed587/587,
zero skips. Full CI remains pending. Authored-cut qualification
notes below are historical; none of these tests establishes live-client acceptance.

## Identity and scope
- Revision 1, 2026-10-28. Root-assigned next Sanitized Parity milestone; source
  baseline is root-native-owned-hitpoints-next/candidate plus its prospective
  typed HP participant. Those packages are independently unqualified.
- Only LegacyConsumableItemKind.Hitpoints is migrated. Mana, FullRestore,
  experience balls, packs, teleports and other item effects retain their owners.
- Existing defect: ItemUsage publishes a cooldown before metadata validation,
  publishes HP/Items through Set, then separately attempts a best-effort item
  save. A rejected/failed save can leave a debit/HP/cooldown split.

## Packet contract
- Preserve the existing successful D13 order: selected item update/removal, max
  HP update, max/current HP update. No new opcode/schema or owner notification.
- Existing ItemHP, 150 ms cooldown and 200 percent ShieldBlock/HawksAmbition boost
  are unchanged. Existing Calculate/ShouldConsumeItem supplies current full-HP
  behavior; no new rule is inferred from fixture needs. The reference health
  branch returns when current==Status.MaxHitpoints (MsgItemUsuagePacket.cs2829),
  but full client/duel/MiracleTouch parity is outside this transaction milestone.

## Catalog/table contract
- Require exactly one real ItemType row, matching selected type, explicit
  positive uint ItemHP field18 and parsed ushort stack cap field46 (zero means
  no merging/one physical unit, preserving existing Inventory semantics). Freeze
  every row field and recheck the real catalog identity/contents before writing.
- Raw item ABI uses 184-byte DBItem records, UID offset0, stack ushort offset52,
  deposit count offset136, and the existing full raw catalog validator. Preserve
  physical order, unrelated records/perfections and every opaque byte. A consumed
  last unit removes its physical record and matching perfection row; stacked
  consumption changes only that stack word.

## Ownership, prepare and commit
- Resolve the actual current admitted native Frame/Store/binding/recovery and
  owned configured Users/Items routes under the existing trade gate and caller's
  packet operation lease. Do not nest operation leases. Require send route,
  current PIN binding/baseline (checked=true is NOT required), recovery clear,
  valid alive character, elapsed cooldown and complete unique item graph.
- The old health route has no verified-PIN or blanket active-trade restriction.
  Do not reuse equipment admission permissions. Preserve unverified-PIN use and
  unoffered health use during a managed trade. Check selected UID is not offered,
  retain the actual trade party identity and escrow state. An unmatched native
  trade owner or escrow without its real managed owner refuses as unknown item
  ownership; do not consume an item owned by another transfer operation.
- Select exactly one top-level inventory UID; require position/warehouse zero,
  no deposits, bounded physical inventory and logical stack max(1,rawStack)
  within actual metadata cap. Bound/lock bytes are retained, not guessed trade
  permissions for a health use. No new UID is minted or registry state mutated;
  genuine admission's existing registry/domain and mutation fences remain real.
- Prepare immutable item/character witnesses, typed result, response frames and
  exact two-file before/after images before Store/native preparation. Use SAME
  LegacyBoundHitpointsScalarWrite.ProjectAfterImage for the complete HP-changing
  Character image, keeping opaque text/encoding. Character target is AccountId;
  Items target is character UID. No cooldown/HP/item change is published yet.
- Actual Store.TryCommitSession calls the real v2 TryCommitSingleOwner writer.
  Revalidate current owner/PIN/trade/recovery/catalog/witnesses and exact file
  beforeimages under ordered locks. Commit Character+Items via the real WAL,
  prove both exact afterimages, then Store's external typed-HP proof publishes
  native HP and final holder. Publish cooldown only in that same final session.
- Definite failure requires both exact preimages and leaves all live state,
  cooldown, UID/stack, files and success outputs unchanged. Unknown/partial write
  or postcommit proof conflict blocks actual recovery and retires the old Store
  owner through the existing committed-publication exception path. No retry,
  refund, live replay or second saver is introduced.
- Send prepared frames only after all locks end, checking the same current owner
  before/after each send. Notification faults remain committed and are fenced;
  they never become an unchanged rejection or refund authorization.

## Fixtures and qualification
- Actual ordinary Frame fixture supplies HP10, real owned item UID/stack and
  ItemHP70/cap10 before OrdinaryLoginFixture captures original bytes. Opaque
  Character/item bytes are installed before admission. Login must have no runtime
  exception or PARITY-BUG diagnostics. No reflective HP seed/accepting saver.
- Actual Frame success covers raw stacks0/1/3, HP80 at FIRST output, exact durable
  Character+Items, native/Store agreement, conserved UIDs, unchanged unrelated
  sidecars and valid wire order. Controls cover missing metadata, active trade,
  cooldown, stale saved images/duplicate physical UID and real Windows deny-replace on
  either Character or Items. Real WAL interruption boundaries exercise pending,
  Character and Items recovery with fresh recovery, no stale-owner replay.
  A genuine protected-but-unverified PIN positive guards the permission boundary.
  The companion two-client regression now covers offered-potion refusal and
  unoffered-potion use through both actual confirmations. Distinct AccountId/UID
  runtime fixtures and live-client trade acceptance remain open; no fake
  participant or accepting trade owner is substituted.
- No helper executes code. Root reviews, composes, captures appropriate red/green
  results and qualifies focused/full gates. Record verified source bug/checklist
  entries only after actual validation.

## Dependencies and limits
- Prospective native HP base: LegacyBoundHitpointsScalarWrite.ProjectAfterImage,
  Store.TryCommitSession external full-afterimage acceptance and physical owned
  HP publication. Also current EquipmentMoveInputs and raw-item spell
  section comparators are source dependencies; no fake substitute exists.
- v2 WAL supports process-restart recovery, not SQL/global sidecar/power-loss or
  cross-process atomicity. Cooldown is the existing session-scoped value, not a
  new durable timer. Full transform/Team/death/revive/client acceptance and
  other consumable transactions remain OPEN.
