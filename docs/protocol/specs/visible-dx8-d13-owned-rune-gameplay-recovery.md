# Owned Rune gameplay and recorded-operation recovery

## Identity and scope
- Revision1, 2026-10-30, Root requirement; isolated successor of frozen29-path item/spell candidate over public6cb2babc. Both predecessors remain unchanged. Full English client/server and historical Rune recovery horizons remain ACTIVE.
- Target VisibleDx8D13, real ordinary native admission, actual Store/catalog/Beast owners. This increment restores source-proven equip grants, ordered displacement spell effects and real Beast slot admission, with sanitized spell ownership and crash-recoverable recorded operations.
- Existing v9 lacks action, historical metadata and spell provenance. Do not reinterpret changed old-v9 outcomes using today's catalog. New Rune operations use a distinct bounded record; old v9 book/identity behavior remains as frozen unless a separately justified source safety correction is required.

## Packet contract
- Existing MsgItemUsage2137, EquipRune/UnequipRune actions and wire fields remain unchanged. Existing native spell packet and action109 removal are used without schema changes.
- Original MrRayzo.Game.MsgServer/MsgItemUsuagePacket.cs2190..2242 and MrRayzo.Role.Instance/Rune.cs271..300 establish same-ID displacement, then requested-slot displacement, then selected equip. Each displacement echo precedes its spell removal. Absent mapped spell grant precedes equip echo and equipment refresh. Publish all prepared frames only after durable Character/Items/Spells commitment.
- Existing spell row is never overwritten/upgraded while retained. At each actual displacement stage, a still-equipped shared provider retains the row/claim. Exact managed-owned last-provider retirement emits removal; a later selected equip can then grant its mapped default row in the original order. A prospective selected Rune is not counted as an already-equipped provider. Unmarked/permanent rows stay retained. Preserve full saved12-byte chunks and opaque byte11 through the actual prepared writer.

## Catalog/table contract
- Use the actual Store-issued native item's catalog adoption, matching the Frame context; no catalog fallback or fake Beast. RuneSpellId is uint decimal ID*100+level, with encoded0 as no skill; nonzero quotient must be1..65535, remainder0..99. Preserve Root's numeric/null safety correction.
- Use the actual literal native ItemType Rune family/slot predicate:401 at101/121;402 at102/122;403 at103..114/123..134 inclusive. The previous managed nine-yellow-slot restriction contradicts the native twelve-slot source and must be corrected. Availability additionally requires actual admitted Beast.YellowRuneSlots using current real row plus hammer flags, with checked arithmetic rather than byte wrap.
- Native Beast must be the target actually published by the successful admission prefix, not a newly constructed same-client replacement. Current relevant Beast fields, exact table/row and YellowRuneSlots value must stay pinned through pre-WAL revalidation. Missing/misbound/malformed/stale required owner or metadata refuses the action.

Bounded data alignment, read from the original server DBServer/itemtype.txt (66 `@@` fields) and MagicEffect.ini, without launching or changing originals:

| Item | RequiredClass/Level | Encoded RuneSpellId | IsRuneSkill section |
| --- | --- | --- | --- |
| 4020515 | 6000/0 | 1416014 | [1416000]=1 |
| 4020517 | 6000/0 | 1416016 | [1416000]=1 |
| 4020518 | 6000/0 | 1416017 | [1416000]=1 |
| 4022001 | 6000/0 | 1547000 | [1547000]=1 |
| 4030101 | 0/0 | 4030101 | Actual self-ID display/no-skill source branch |

The retained official Chinese patch INIs under .artifacts/official-chinese-patches/20261026/extracted/7892-7930 and7931 also carry [1416000] IsRuneSkill=1. This confirms the classification field/data exists across those retained inputs, not that their packet/build contracts replace VisibleDx8D13 authority. Higher-level self-effect Rune rows without actual spell classification do not receive an invented exemption.

## Validation and transaction rules
- Keep real PIN/Store/native spell and complete item binding, second reborn/level15, family/class/level, stack raw0-or1, lock/deposit, unequip inventory capacity and vending restrictions. Rejections publish no success, mutate no live state/files/authorization and create no WAL.
- Preserve the original/native no-Rune-XP rule in the actual managed progression path using this same real classification before a spell writer; stale or malformed explicit classification cannot advance XP. Ordinary mapped false-classification and soul/reborn/other origin conversions require their own complete owner joins and remain an active frontier; changed managed-owned rows still refuse Rune movement.
- Managed Rune spell provenance is a canonical Character-owned marker, created only when the real current Rune producer appends an absent mapped default spell and persisted atomically with Items and Spells. Legacy unmarked rows are unknown/permanent/shared, never silently adopted as temporary.
- A marked row must remain exactly equal to its recorded default ID/level/zero fields before a Rune action may alter it; changed managed-owned rows refuse the entire action pending a real progression-owner join. No moving a Rune or clearing a marker while leaving an unproven temporary ability. Complete actual metadata for remaining equipped contributors is required before removing an owned spell; another equipped Rune mapping to that ID retains the existing row/claim exactly.
- Implement the historical caster-metadata join where actual source/data supports it: WorldDataCatalog.MagicEffects and loaded MagicEffect.ini IsRuneSkill must supply real managed descriptor classification, then current genuine equipped-provider eligibility precedes costs/cooldowns/publication. No guessed Rune-ID map or accepting fallback. Preserve ordinary/unrelated unknown spell behavior. With this real gate, unmarked rows remain preserved on last-provider removal; without its actual metadata/owner proof that action refuses. Historical row ownership is never retroactively claimed.
- The new closed Rune operation records equip/unequip, selected physical UID, requested slot, exact necessary typed metadata dependencies and actual Beast/alternate inputs issued by current real owners. It carries no arbitrary accepted after-HP, saver, callback or caller-chosen ignored path. Recovery reconstructs exact Character marker/HP, item projection and spell outcomes from the complete captured BEFORE images and recorded dependencies; compare every AFTER byte, with no UID births or unrelated changes.
- New journal version12 retains the ordered Character/Items/Spells targets and configured domain, adding RuneOperation. Typed metadata is ItemId/RuneSpellId/RequiredClass/RequiredLevel/IsRuneSkill (actual classification, null only the real no-skill branch); operation includes Equip/SelectedUid/RequestedPosition/Metadata/AlternateEquipmentActive/BeastLevel/BeastFlags/BeastYellowSlots (actual native row extra slots, null only real level0 absence). No arbitrary current-catalog replacement during replay.
- Historical v9 identity-Rune settlement keeps its exact old producer slot predicate, including the old nine-yellow-slot admission bound. Correcting new v12 gameplay to twelve slots must not silently widen which older intents the v9 validator can derive. Its decoder/accepted exact identity forms remain supported; changed metadata-dependent old-v9 outcomes remain fenced.
- Persisted HP clamp follows actual LegacyMaxHitpointsCalculator: nontransform result is at least currentHP, hence identity; active-transform cap comes from captured Character state and yields min(currentHP,cap). CombatGear/alternate and broader HP catalogs affect response maximum/refresh, which recovery does not replay. Any future change to that algorithm requires its full real dependencies before widening recovery.
- Preserve existing exclusive UID lease/domain/state/archive/config custody, exact pending identity/stage prefixes/current target before-or-after checks, same-lease transfer and ordinary raw postscan. New operation remains a single-owner physical scan with complete UID conservation, protected captured-BEFORE physical ownership and high-water checks; no logical AFTER overlay.
- Trusted configured durable intent is recovery authority, not producer authentication or fresh action permission. New actions still require the real live catalog/Beast and other owners; old records cannot obtain new historical authority by being syntactically valid.

## Expected wire behavior and fixtures
- Real owned Runtime/native Frame/Store/CTS transport and actual producers; no handcrafted positive journals, accepting callbacks or fabricated permission owners. Independent protocol goldens assert grant/displacement/remove/echo/refresh ordering and unchanged item-usage metadata/tails.
- Cover absent grant, exact current-row no-overwrite, shared-ID contributors, changed marker refusal, historical unmarked boundary, raw0/Bound/Inscribed rules, normal/alternate slot limits and full twelve-slot family bounds. Missing/replaced/stale Beast or catalog dependency must refuse without WAL/dirty state/output.
- Interrupt actual new record at every replacement prefix0..3 and actual flushed stages1..3; owned restart must produce exact Character/Items/Spells afterimages and preserve physical UID set/registry, then second restart must not replay. Malformed operation/marker/metadata, stale target/pending/stage, foreign UID evidence and retained work remain refused with evidence preserved.

## Evidence alignment
| Requirement | Evidence | Boundary |
| --- | --- | --- |
| Grant/no-overwrite/removal ordering | Original Rune.cs253..301, MsgItemUsuagePacket.cs2190..2242 | Source-derived, no new client execution |
| Twelve yellow family slots | Original ItemType.cs406..420 and literal native RuneCheck.cs | Source conflict corrected; Beast availability still required |
| Genuine Beast adoption | ExistingCharacterPrefix.SpellAdmission/PrefixLoad | Must join published target to binding before new actions |
| Spell origin | No original/save-row provenance exists | New explicit managed claim; historical ambiguity retained |
| Persisted HP | LegacyMaxHitpointsCalculator.CalculateRuntimeMaxHitpoints/LegacyTransformState | Exact captured Character clamp only; no refresh replay |
| Runtime/live/full parity | Current team source-only | UNRUN/unproven |

## Implementation and qualification
- Lead owns live Rune transition/provenance/Character projection; helper owns disjoint recorded-operation and dependency adoption/recovery code; independent helper owns real behavior/durability tests. Agree APIs before overlapping changes.
- Draft FSB245+ and finite delta/full manifests follow review. Root alone integrates/compiles/tests/Git/publishes after its current gate and frozen29 qualification. No build/runtime/test/API/Git/current-checkout/client/updater/AV/save/route/ZIP action by this team; no retry of rejected unrelated artifact update.
