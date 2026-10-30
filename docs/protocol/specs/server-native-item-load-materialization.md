# FSB-226: Genuine detached native item materialization and routing

## Identity and scope
Revision 1, 2026-10-29; baseline archive 84611bb1. This component consumes the shared complete ItemLoadImage and produces genuine detached GetDataItem objects/placements, with no real owner-map/item/quest publication. Root owns file/orchestrator/integration; the owner publisher owns actual admission and final map/marker publication. Existing raw observation scope stays frozen. Current confirmed live profile remains 7899/VisibleDx8D13; this is not 7937/live acceptance.

## File and native conversion contract
- Parse/API evidence is the shared immutable roots/deposits/perfections image, exact 184/80-byte records. Global serialized item UID and perfection target validity belongs to the shared parser.
- Invoke actual LegacyNativeClientItemRecord.GetDataItem, not fabricated domain items or DTO acceptance substitutes. Preserve actual date/text/extra/rune-setter conversion failures.
- Retain explicit existing OuterItemRecordSanitation on ROOT value copies only, labeled UNVERIFIED legacy normalization. Add no caps. Children stay raw conversion inputs. Existing Inscribed conversion omission is not silently expanded.
- Keep each parent's actual UID/WH_ID/Position separate from every immediate child. Children reside only in the genuine prepared parent's Deposite map; all newly created refs may be targets for prepared perfection hydration. No existing runtime/foreign item is a perfection fallback.

## Route contract
| Actual root location | Prepared destination |
| --- | --- |
| WH_ID100, actual ItemPosition9/17 | actual Wardrobe.GetItemType Garment/Mount; same item also Equipment for saved Position1..59 |
| Other nonzero WH_ID | Warehouse with that actual bank ID |
| WH_ID0, Position0 | Inventory |
| WH_ID0, Position1..63 | Equipment |
| WH_ID0, Position213..215 | Relic |
| WH_ID0, actual MythsoulBag slot | Gear; preserve/check actual mutable slot evidence |
| WH_ID0, Position211/212 or101..135 | Rune; actual job rule may change the actual prepared item Position to212 |
| Any other unmapped location | refuse before publication, never silently skip |

A 205901 outer root without children retains the UNVERIFIED legacy filter. A filtered root with children refuses unresolved migration. A perfection targeting a filtered/unmapped item refuses. Main perfections on pure Wardrobe/Rune/Relic/Gear destinations refuse as a safe current save/load format boundary: actual native save and old lookup do not support those roots. Equipment+Wardrobe alias main perfections and supported Inventory/Equipment/Warehouse main refs are hydrated; actual new child refs are hydrated. This is not an invented client gameplay prohibition or migration rule.

## Real evidence and ownership
- API: RequiredDestinations(image) returns shared Destination flags via pure physical/static route classification; Prepare(image,client,player) returns Plan.Placements (shared Placement[]), uint? QuestUid, RequireCurrent().
- Root takes the genuine owner lease after classification and before Prepare. The materializer never calls real Add/Free or mutates owner containers. Publisher owns capacity/identity/collision/admission and actual quest owner requirements.
- Capture actual client/player identity, UID, Class, GuildID and mutable MythsoulBag evidence. Required Rune constructor/job lookups capture the real Pool.ItemsBase owner, actual row reference, matching key/ID, and queried RuneSpellID/Class fields. Revalidate around actual conversion/job evaluation and before owner final publication. No default metadata or stand-in owner.
- Rune job fallback calls actual EquipPassJobReq and sets actual detached item.Position=212 when required. Actual ITEM_ID setter runs and its prepared SpellID/Effect are checked against the captured genuine metadata.
- Wardrobe classification uses pure actual ItemPosition for requirements, then calls the actual bound Wardrobe.GetItemType and retains the exact same genuine item in both alias placements.
- Demon item 750000 marker is nullable prepared UID, updated in deterministic source root-then-child encounter order; it is never ITEM_ID. Actual optional marker owner is the publisher's concern.

## Qualification and limits
New focused C# modules and real owned materialization controls only; no helper build/test/dotnet/native/process/SQL/Git/live or actual checkout changes. Root reviews, builds and records actual outcomes. Gates/witnesses require cooperating owners and exclusive metadata/input/prepared item lifetime; they do not make public item/player/catalog fields, ABA, raw dictionary mutation or cross-owner SQL/crash state atomic. Source normalization/filter routes are explicitly bounded evidence, not new-client proof.

## Final callback boundary
Real catalog key comparers may execute application code. RequireCurrent completes all such lookups, then makes one final callback-free verification of actual required mapping refs (catalog enumeration), queried row scalars, original metadata owner, player/Class/Guild/bag-slot/wardrobe bindings and prepared constructor results. The private uint-key witness table uses only its own framework comparer. No repeated bounded lookup rounds or validation after real owner publication is substituted. Owned real-comparer controls mutate player/Class/Guild or an earlier metadata row/mapping on the last lookup and must refuse.

## Authored outcome scope
Fifty new owned component outcomes are authored, all UNRUN: 19 valid/detached route, actual constructor/job, alias, marker and supported perfection controls; 31 refusal/evidence controls, including four real final-comparer mutation cases and three late actual-date repair cases. Tests use actual existing native Scope/owner leases, encoders/records/catalogs and source container classes. They do not publish a fake accepting owner or prove live admission merely because a detached placement can be built. Root owns the serialized build/focused/full gate and actual integration receipts.

Root local qualification for the combined40-path candidate: Release build01
passed with0errors/157existingwarnings and no new changed-path warnings. Actual
focused01 passed423 with7existing skips; native-broad01 passed13278/13278 with
0failures/skips. Selections overlap. Receipts are in root-native-item-load-next.
The old public7bb fullrun failed one timing-sensitive startup test; its actual
failedTRXs/logs remain. Exact-commit successor fullCI, SQL, deployment and live
new-client/fullparity acceptance are pending. This note supersedes authored/unrun
labels in this spec/entry without expanding the verification scope.
