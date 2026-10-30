# All-round passive: bounded existing-runtime implementation design

Revision2,2026-10-30. Root-requested separate source-only milestone; FSB253
reserved. The exact selected7899 resource join below closes the table decision.
Implementation and tests remain unexecuted. No7937 runtime activation.

## Identity and scope

Actual original DBServer item4020201..4020227 is Warrior All-round, required
class2000/level0, raw RuneSpellID equal to actual ItemID, stack field46 zero.
This is an item passive, never a learned PlayerSpell or managed spell origin.
The adjusted item display remains base4020201/Effect1 at every level.

Implement the missing bounded passive contribution through current owned runtime
status/combat projections, retaining existing complete item/UID/native ownership.
Do not import held full native Equip.UpdateStats or activate Chinese7937 decoded
catalogs. Keep unrelated passives, FlashShield and native full-graph debt separate.

## Packet contract

No opcode/layout/count change. Existing item2116 display/Effect1 is unchanged.
The existing player-status fields affected by the source branch are MinAttack,
MaxAttack, Defence and flat MagicDefence (not percentage MDefence). Existing
Equip/Unequip/equipment-query successful sequences remain in place. No phantom
spell grant, action109, marker or new HP event comes from All-round itself.

## Catalog/table contract and conflict

Read-only sources:

- Original DecompiledSource/GameServer/MrRayzo.Role.Instance/Equip.cs1516..1522:
  alive + actual equipped All-round + actual left shield adds level*111 to each
  physical attack bound, level*370 Defence and level*185 flat MagicDefence.
- Original Rune.cs138..158 and328..364: normal/alternate/fallback worn selection,
  Reborn2/Level15 and actual item job eligibility before the first matching Rune.
- Actual lqtxfwd/GameServer/DBServer/itemtype.txt47857..47883: exact27 IDs, class,
  raw-self mapping and nonstack metadata. Descriptions conflict with the code:
  level1 says attack300/defence3000/magic-defence500; level27 says3000/10000/5000.
  Intermediate descriptions form an explicit non-linear progression.

The official [2024 Rune Details](https://co.99.com/guide/items/2024/1114/rune_details.shtml)
gives all27 Warrior/shield triples. Each matches the exact selected7899 client
at `C:/Users/skull/Downloads/Conquer Online - English Staged 7896`: its current
version.dat says7899 (SHA256
`65B0260EE626CCA2B478DA875A7AFA3B6869B85EF40986982C6207CEBFB815A6`),
RuneMagic.lua33..59 has unique exact keys4020201..4020227 (SHA256
`922263766348A2A8DDA6EF870AC87C169CDA3CCB802599A0BC6E99530CCD3C96`),
and RuneDesc.ini112..190 has the same current triples plus the next-level
triples (SHA256
`100652C2C6A767E9DC7CC50B7E68CC25119FF788685FBA2FA471839FEDF4A775`).
This is the retained selected7899/D13 stage documented by the branding and live
validation handoffs; no executable was launched or requalified here. The retained
65-field main descriptions and original server rows agree on all27 triples.
The implementation uses an explicit immutable numeric table, never parses
localized text or derives approximate111/370/185 multiples.

|Level|Attack added to each bound|Physical defense|Flat magic defense|
|---|---|---|---|
|1|300|3000|500|
|2|350|3100|550|
|3|400|3200|600|
|4|450|3300|650|
|5|500|3400|700|
|6|550|3500|750|
|7|600|3600|800|
|8|650|3700|850|
|9|700|3800|900|
|10|750|3900|950|
|11|800|4000|1000|
|12|850|4100|1050|
|13|900|4200|1100|
|14|950|4300|1150|
|15|1000|4400|1200|
|16|1100|4500|1300|
|17|1200|5000|1400|
|18|1300|5500|1500|
|19|1400|6000|1600|
|20|1500|6500|1800|
|21|1600|7000|2000|
|22|1800|7500|2500|
|23|2000|8000|3000|
|24|2200|8500|3500|
|25|2500|9000|4000|
|26|2700|9500|4500|
|27|3000|10000|5000|

The [2018 official preview](https://co.99.com/news/2018-02-01/allrunes.shtml)
has maximum physical defense8000. Fresh publisher-matching7931 Cn_Res and the
separate current7937 reference contain BOTH the newer status-skill triples and
RuneDesc triples with physical defense2000 lower at every level. That later
role/version conflict is preserved as evidence, not blended into7899 gameplay.

Chinese7937 typed decoded envelopes and their activation transaction remain
separate. Reading those documents does not attach them to WorldDataCatalog or
enable this legacy gameplay path through an inferred catalog identity.

## Validation and current ownership

Use exact All-round IDs/levels1..27 and actual adopted raw-self metadata, unique
raw/index row and complete required numeric fields. Names/attributes supplied by
a character must not create a passive. Missing/malformed required metadata is
failure, not a bonus-zero/default acceptance. No independent decoded catalog or
caller-supplied accepting owner/callback is introduced.

Alive means current HP>0, matching actual native Player.Alive. Rune eligibility
uses source Reborn/Level/job requirements. Rune selection must match actual
normal/alternate/fallback WornObjects behavior; stored/bag/inactive rows cannot
grant the effect and two sets cannot stack it.
Before selecting by exact All-round ID, require global uniqueness of every
non0/211/212 Rune position, matching LegacyNativeRuneItemStorage's complete
TryCaptureEquipped admission. Another Rune type in the same active position
cannot be hidden by the exact-ID filter or by current item-graph membership.
For the selected active All-round only, the proven metadata cap0 means one
physical nonstackable Rune. Preserve actual source raw quantities0/1 and refuse
raw>1 before a reply. Do not normalize/remove the invalid saved input or impose
this policy on unrelated Runes/materials. The negative fixture seeds raw2 before
bootstrap and proves its actual native/current owner; it never equips invalid
quantity through a fabricated successful action.

The qualifying shield is the actual current durable left-hand selection, using
the established equipment query weapon/alternate rules (including learned10311
when a two-hand weapon would otherwise exclude a shield), not merely any900xxx
item elsewhere in inventory. Broken, stored and inactive shields do not qualify.

Current real owner path: Frame/Runtime ordinary binding owns Items/catalog and
native graph; Store publication checks complete native/current/saved evidence;
EquipmentQuery and Rune frames create status through
CalculateLegacyRuntimeStatusFields/LegacyPlayerStatusCalculator and
CreateNativeMagicCachedPlayerStatusResponse. The native cached override replaces
only four magic attack/percent/critical/increase fields, leaving these four
All-round projected fields intact. Current managed combat uses the same status
projection. Full native Equip.QueryEquipment/UpdateStats is explicitly held in
LegacyNativeEquipItemStorage.cs and cannot be represented as already adopted.

## Proposed finite implementation and fixtures

Seed only necessary current Root130 source files into this partial candidate.
The calculator gains a trailing optional protocol profile; omitted/other profiles
retain prior behavior. The one shared runtime status caller passes the profile
from the actual current nonretired Store/native item binding and same adopted
catalog. A profile enum is a pure projection input, never publication authority.
Existing prepared Rune/item/native/Store writers retain all currentness checks.
The equipment query selector gains a pure parameter form so the passive reuses
the exact current right/eligible-left policy without constructing a fake session.

Original Equip1516..1570 applies All-round BEFORE MagicAttack++/gem flat adds,
Beasts/Profession/Chi/Perfection/CombatGear. Insert the new contribution immediately
after existing active Rune attack additions and before MagicAttack++ and gems.
Do not append it after arbitrary late percentage or aura phases.

New owned regression partials should use actual Runtime/native Frame/Store and
real prebootstrap catalog/gear/Rune input; exercise levels1/27, dead/alive,
shield/no shield/broken, normal/alternate/fallback, Rune Equip/Unequip and shield
removal, repeat query no cumulative bonus, actual eligibility and malformed
metadata. Check exact affected and unaffected status fields, successful wire
sequences, complete saved bytes/UID ledger/native owner and absent phantom
PlayerSpell/origin. Pure table arithmetic cases may complement real-owner cases,
but cannot replace them or be presented as current-client acceptance.

## Qualification

All implementation/fixtures are source-only until Root review and execution.
No builds, tests, runtime children, APIs, Git, host, legacy client/updater,
antivirus or original-data action. No Compose, active Root or frozen predecessor
edits. Full English/server/client/SQL/Rune/current7937 horizon remains ACTIVE.
