# VisibleDx8D13 Character, Items and Spells journal v9

2026-10-28, Root-owned prerequisite for genuine skill-book/rune transactions.
Current target Chinese7899 / VisibleDx8D13. No packet/schema/profile changes.

## Domain and source evidence
Three actual targets only: Character(AccountId).ini, Items(CharacterUid).bin,
Spells(CharacterUid).bin. The account filename may differ from embedded UID.
Reuse the real shared pending/stage/replace/recovery engine, not an admission
record or a fake Items participant. Existing v8 Character+Spells stays unchanged.
Actual skill books consume an inventory row and add a spell; rune unequip moves
an equipped rune, removes its granted spell, and can lower MinHitPoints.

## Contract
- API TryCommitCharacterItemsSpells(usersRoot,itemsRoot,spellsRoot,changes,out reason).
- Bounded enum LegacyCharacterItemsSpellsJournalInterruption: None, AfterPending,
  AfterCharacter, AfterItems, AfterSpells. No arbitrary external failpoint callback.
- Exactly one of each kind, canonical Character0 then Items1 then Spells5.
- Character and Items before required; all after required. Only Spells before
  may be absent for actual first-save. The Character before/after may be identical
  when unchanged; it remains a real identity/baseline participant, not invented state.
- Require complete strict Character images (existing v8 ambiguity checks/full
  parser); positive embedded UID <=UInt32.MaxValue in both equals Items/Spells
  filename identity. Items/Spells FileIds equal; AccountId positive.
- Items images use the real complete raw item catalog validation; positive/unique
  UIDs/complete record/perfection/trailing structure, no projecting malformed tails
  away. Spells use complete existing parser, positive/unique IDs and nonnegative XP.
- Canonical explicit absolute Users/Items/Spells roots must be logically/physically
  disjoint and ordinary (no reparse), separate from shared journal under Users.
  Users and Items directories must already exist. Missing configured Spells parent
  may be created only after pending publication, then all routes are revalidated.
- Existing per-target16MiB/shared192MiB bounds, current-byte preconditions, shared
  pending slot, complete copied images, ordered file locks and restart engine apply.
- JSON version9 exact root fields Version,TransactionId,Changes,
  CharacterItemsSpellsDomain; exact domain fields Users,Items,Spells. Unknown,
  duplicate/missing fields, wrong target order/kind/count/IDs/images refuse.
- Clear all same-transaction stages before deleting pending, including already-after
  targets. Retained interruption prefixes are reconciled by a fresh configured owner.
- Recovery/readiness requires actual seven-route RecoveryState configured Users,
  Items and Spells, plus completed real startup recovery and no pending/failure.
  Pair/v8-only or copied UID/path-adjacent state cannot grant v9 authority.
- Keep all v1..8 schema/property/order/behavior contracts. No multi-process or OS
  power-loss guarantee is inferred from process-restart file simulations.

## Consumption and qualification
Root also owns the real book/rune callers: validate actual Store/trade/escrow/PIN,
metadata, inventory ownership/location/capacity and saved before images; prepare
complete Character/Items/Spells images, native participant/final session and frames
before commit, execute v9 under real domain/Store/file ownership, then publish actual
final record and successful wire. Changed/invalid/missing required state refuses
without cost/success. Postcommit native/publication conflict is real recovery-required,
never an unchanged rejection. No saved file or native sender stand-in.

Direct owned file tests cover all four interruption prefixes, distinct account/UID,
unchanged Character, optional Spells first-save, fresh recovery/idempotence, wrong
roots/configuration/JSON/images/membership/current bytes and retained stage cleanup.
The corrected combined candidate passed2405 selected core cases, zero failures,
two existing skipped teleport cases. Final proficiency refinement passed81
overlapping focused cases, zero failures/skips. Full CI and live acceptance remain
required. Caller tests exercise real item consumption/rune movement and no debit/
success on rejection in owned fixtures. No full SQL, native
whole graph, live client, deployment or whole newer-client parity claim follows.
