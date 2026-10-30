# Owned Rune container lifecycle and spell provenance

## Identity and scope
- Revision1, 2026-10-30; Root continuation of the full active Rune/English server-client horizon. New isolated exact6cb archive plus frozen77 overlay; predecessors remain untouched.
- Target VisibleDx8D13. Implement actual RuneStorage types0/1 and DropRune through the real Character/Items/Spells owner, preserving legitimate source rules and cleaning up only proven temporary spell origins. No reward/UID birth pipeline is invented.
- Audit actual mappings and soul/reborn/progression callers before choosing wider ownership work. Full Rune compose/chance/decompose/bag/reborn/spell-conversion horizons remain ACTIVE where real owners are missing.

## Packet contract
- Existing RuneStorage opcode2286 and fields/type0/type1 response/HPAdd, MsgItemUsage2137 DropRune and RemoveInventory, existing action109 spell removal, and equipment refresh layouts remain unchanged.
- Prepare output only after validating the whole action. Proven owned spell removal precedes the actual storage response/drop removal packet. Storage retains its actual post-response ten-frame equipment query under the published current owner; Drop retains its RemoveInventory sequence without an unsolicited refresh. Do not invent UnequipRune echoes for a storage packet.
- Preserve startup/read-only/type3 and unrelated successful packet sequences unless separately joined; explicitly record unjoined mutation ownership instead of treating them as complete.

## Catalog/table contract
- Actual DBServer itemtype.txt audit:1219 distinct Rune401..403 ItemIDs;69 exact original no-skill cases;572 mapped rows across26 IDs with a single explicit nonzero IsRuneSkill;551 mapped rows across63 IDs missing their ID00 section;27 mapped rows all ID8140 with duplicate [814000] sections (one lacks flag, one has1). There are no explicitly false mapped Rune rows in this dataset.
- Missing551 comprise550 higher-level self-encoded rows whose adjusted display ID differs from ItemID, plus4035009→4011301. Do not infer those are no-skill or invent ordinary mapped Rune XP flows. Strict classification remains required for new affected operations; ambiguous/missing authority stays explicit.
- Reuse the actual adopted item catalog/classification, exact native Store/item graph and relevant metadata witnesses from the frozen Rune producer. No Beast availability owner is needed for container retirement; no catalog or permission fallback is accepted.

## Validation and transaction rules
- Original MsgRuneStorage.Process type0 moves selected Rune→212 only if no same-base212 row; type1 moves→211. It requires Reborn>=2, not Equip/Unequip's Level15, inventory-space, vending, Beast or job/level gates. Already-target position is a harmless no-change response. Type1 enforces the real finite bag100 capacity before moving an additional row; the original omitted that capacity check.
- Same-owner storage moves preserve the exact physical root/deposit/perfection closure, raw stack and lock fields; do not import trade/equip restrictions. DropRune destroys the requested Rune root without birth/cost; reject locked destruction and unproven deposited-child destruction before any publication, and retain bound/inscribed behavior. Its complete physical UID retirement must be exact and protected during restart.
- All live mutations require the actual current Frame/native Store/prepared file owner and strict relevant Character state. Shared remaining equipped providers retain their row/origin. Last-provider retirement deletes only an exact unchanged managed-created grant and its marker. Unmarked/permanent data is retained under the actual IsRuneSkill caster provider gate. Changed managed-owned rows fail safely pending their real writer join; no marker is cleared while leaving a usable temporary ability.
- New version13 carries explicit typed Store/Bag/Drop kind, selected UID, exact required Rune metadata/classification and alternate state. It is distinct from version12 Equip/Unequip and old v9 identity records. Character+Items+Spells before/after images, marker and exact output preparation share the actual writer/WAL; no per-file optimistic saves or accepting callbacks.
- Store/Bag conserve all physical UIDs; Drop AFTER is exactly BEFORE minus the selected deposit-free root. No UID allocation/birth, logical AFTER scan overlay or unrelated namespace exemption. Reuse existing exclusive custody/domain/high water, captured pending/current/stage identity, actual configured Spells absence/create proof, protected BEFORE physical ownership, SAME lease and ordinary postscan.
- Recovery derives exact three afterimages from captured operation/dependencies, not today's catalog or arbitrary accepted balances/HP. Persisted HP follows the frozen exact captured-Character clamp; response refresh uses actual current owners only at live preparation and is never replayed at startup.

## Expected wire behavior and fixtures
- Real owned Runtime/native Frame/Store/CTS, actual parser/catalog rows and real closed interruption. Never inject positive markers, fake owners/savers or hand-authored positive WAL.
- Exercise equip-created claim→storage/bag/drop retirement, shared/permanent preservation, actual storage-specific rules, bag capacity, source no-change, locked/deposit drop refusal, exact physical UID closure/registry and first reply after complete commitment.
- Interrupt real v13 at replacement0..3 and actual flush1..3, recover exact images and second restart, and refuse malformed kind/metadata/marker/UID/stage/current target/custody evidence. Include retired UID external physical conflict without banning valid references.
- Source-only during Root qualification. No build/test/runtime/API/Git/host/client/updater/AV/original-data action by this team.

## Evidence alignment
| Requirement | Source | Boundary |
| --- | --- | --- |
| Storage0/1 rules | Original MsgRuneStorage.Process40..100 | Source-derived; no imported Unequip gates |
| Drop retirement | Original MsgItemUsuagePacket2070 and Rune.Remove | Complete requested root only; no reward birth |
| Provenance/caster ownership | Frozen77 origin/classification/native binding/WAL | Preserve exact existing owner contracts |
| Actual mapped Rune behavior | Complete original itemtype/MagicEffect audit | No real false mapping; missing/duplicate classification not guessed |
| Other lifecycle writers | Soul/reborn/native mutations/container audit | Explicit remaining owners, not completion claims |

## Implementation and qualification
- Lead owns typed version13/evidence/transaction/recovery central plumbing. Container helper owns pure container projection/frames/adapters and genuine tests. Spell helper owns complete catalog/writer audit and source-backed spell ownership work only after agreement.
- Draft FSB247 and finite manifests follow source review (FSB246 is Root's separate native alternate publication correction); Root alone integrates, compiles, tests and publishes. Keep full horizon active after this finite no-birth lifecycle slice.

## Current Drop HP correction before publication

Original MsgItemUsuagePacket2070-73 and Rune.Remove307 do not assign HP or query
Equipment; the established Drop result has no HP frames. Current v15 RawSelf Drop
therefore preserves the complete current HP/native value and saved HP bytes.
Store/Bag retain their actual equipment-query projection. Old v13 replay keeps
its separately declared historical interpretation. Captured transform fields do
not authorize adding an HP effect to a different action. The actual transformed
Drop control retains HP100000 with only109/removal output and exact origin/item/
spell commitment; genuine flushed-stage recovery preserves that exact recorded
HP before a later independent normal-login projection. Rune unequip still clamps
100000 to3000 and publishes its existing HP frames. This is a source correction,
not live-client acceptance.
