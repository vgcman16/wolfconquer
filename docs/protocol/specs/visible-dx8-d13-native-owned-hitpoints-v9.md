# VisibleDx8D13: native HP and the real v9 rune producer

## Identity and scope
- Revision 1, 2026-10-28; Root-assigned follow-up to the frozen native-owned-HP
  source and source-review packages. Root's composed candidate contains the
  existing 16 production paths and four companion test partials; they remain
  untouched by this follow-up.
- Target is the existing VisibleDx8D13 real admitted item/spell/rune handler and
  Character/Items/Spells v9 journal, not an additional persistence owner.
- Source-derived composition defect: the HP participant requires the complete
  pure scalar afterimage, while the real rune producer currently replaces only
  MinHitPoints. A missing default-valued scalar line can preserve the parsed
  baseline but cause the two valid afterimages to differ after durable success.

## Packet contract
- No packet layout, opcode or field changes. Preserve actual UnequipRune success
  ordering: spell removal action 109, original request echo, maximum/current HP
  updates, player-status and total-refine refresh. Use existing packet parsers
  and independent fixed request bytes from the ordinary caller tests.
- Source/profile evidence is the existing handler/fixture and native health
  goldens. New live-client or wire evidence is not claimed.

## Catalog/table contract
- No new catalogs. Use the genuine NativeItemSpellCallerFixture's existing
  second-reborn rune, item metadata, learned rune spell and ordinary admission.
- Saved Character text may omit a scalar whose parser default equals the actual
  admitted value. The full scalar projection must insert its proper value and
  preserve opaque comments/attributes and the original encoding.

## Validation and transaction rules
- Only the HP-changing branch of PrepareItemSpellImages changes: generate its
  Character image through LegacyBoundHitpointsScalarWrite.ProjectAfterImage
  using the original bounded bytes and actual updated Character before the
  existing real v9 writer. Keep the existing independent scalar equality guard.
- Preserve exact external-afterimage proof, genuine binding/recovery/Store
  ownership, all existing beforeimage checks and the actual three-file journal.
  Do not add a saver callback, relax equality or introduce a second HP write.
- HP-unchanged producer behavior remains byte-for-byte baseline Character image.
  Existing item/spell image validation and all success publication order remain.

## Expected wire behavior and fixtures
- New regression calls actual WithNativeItemSpellCallerAsync(true), real Login,
  then actual Store.Set to durably set and physically hydrate HP 100000. It does
  not reflectively seed physical HP or create an accepting writer.
- Before Login retain an opaque Character comment and attribute. After the real
  high-HP publication, remove exactly one ExpBallUsed=0 line from owned test text;
  assert ExpBallUsed really is zero and the parsed scalar state remains equal.
  Face/Haire/FirstClass use required parser reads and cannot serve as this
  omitted-default fixture; ExpBallUsed uses actual GetInt64OrDefault.
- Actual UnequipRune must clamp HP below 100000, remove rune spell 14160 and move
  its genuine item to rune storage. At the first real output require durable
  exact Character/Items/Spells afterimages, saved/Store/native HP agreement,
  original live binding, recovery clear and no pending journal. Retain opaque
  text and prove ExpBallUsed=0 was projected back into the Character image.
- Preserve successful removal/echo/HP/status/refine packet order. Existing
  non-HP book/rune regressions remain separate controls.

## Evidence alignment
- Review evidence: helper-native-owned-hitpoints-review-next/HANDOFF.md.
- Producer: WorldPacketHandlers.ItemUsage.SpellTransaction.Images.cs and real
  TryCommitLegacyItemSpell -> TryCommitCharacterItemsSpells -> TryCommitSession.
- Pure image/proof: LegacyBoundHitpointsScalarWrite.Images.cs and
  RequireExternallyCommitted; neither file is changed by this follow-up.

## Implementation and qualification
- Production postimage and new regression are separately listed in manifests,
  allowing Root to run only the new test against the old producer for a genuine
  red baseline before composing the production fix.
- This helper authors source only. No parser/build/test/runtime result is
  claimed. Root owns review, red/green reproduction and serialized qualification.
- Potion HP+Items atomicity, Team/death/revive, unrelated domains, SQL, power
  loss, external writers and client acceptance remain OPEN.
