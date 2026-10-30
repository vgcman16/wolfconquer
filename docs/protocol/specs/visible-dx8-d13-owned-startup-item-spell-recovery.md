# Owned startup recovery of recorded v9 item/spell intent

## Identity and scope
- Revision1, 2026-10-30; Root requirement over exact public6cb2babc0fbd591372e5be440dae5d234dcf683f. The full English server/client and full v9/Rune recovery horizons remain ACTIVE.
- Target `VisibleDx8D13`. No packet ID/layout or WAL schema change. Existing v1/v2 startup behavior remains intact.
- Implement exact source-static skill-book consumption/learning and source-derived Rune item transitions whose Character and spell outcomes are unchanged. Changed Rune HP or metadata-dependent spell removal remains unresolved until its real dependency proof is joined; do not redefine full v9 completion around the proven slices.

## Packet contract
- No wire changes. Actual native Frame fixtures use current MsgItemUsage2137 and existing spell/update packets through the real Store and prepared writer. New actions retain their real catalog, ownership, permission, class, capacity and PIN validation.
- The closed failure-only ItemSpellJournalInterruption reaches the real producer and engine; it supplies no paths, bytes, callback or successful saver. Support replacement0..3 and actual flushed stages1..3 for v9.

## Catalog/table contract
- `WorldPacketHandlers.ItemUsage.SpellTransaction` -> `LegacyPreparedSpellFileWrite.Writers` -> `TryCommitCharacterItemsSpells` emits v9 with canonical Domain Users/Items/Spells and Character(account), Items(Character UID), Spells(same UID) order.
- Require actual configured roots/domain, existing Character and Items before-images, complete after-images and strict unchanged Character identity. Spells BEFORE may be null only as explicit captured physical absence; no empty before-file is invented. A missing configured Spells directory is valid only with that captured absent target: the actual producer publishes pending before creating the directory. Owned recovery rechecks the exact domain/paths/custody, uses the actual engine directory preparation, then rechecks before staging. Files, reparse/aliased paths and foreign evidence remain fenced.
- The actual raw item parser validates every physical row, deposit, perfection reference, duplicate and trailing byte. Parse/capture complete save graphs losslessly; use the actual producer's full raw-record comparison by UID/parent for original order normalization, then require exact expected after serialization.
- The actual native spell FreezeSessionSnapshot validates positive unique IDs, nonnegative experience and supported native fields. Preserve every existing full12-byte record, including byte11, in order. The new book row is the exact mapped default entry; no upgrade, unrelated grant/removal or field edit is inferred from well-formed bytes.

## Validation and transaction rules
- Retain actual exclusive owner.lock, expected domain/evidence/high water, pinned registry/state/archive/configuration, retained-work refusal, exact captured pending identity, exact current before/after target checks and bounded stage prefixes at write boundaries. Transfer the SAME lease only after ordinary full raw postscan and readiness.
- v9 matches the configured Spells root as well as Users/Items. Reject unrelated Character, Items or Spells staging evidence, aliases/reparse roots, tampered stages and unsupported records. No generic allow-pending authority or unrelated namespace exemptions.
- v9 is single-owner consumption, so scan physical Items unchanged; never apply the v1 pair logical overlay. Both captured raw UID sets must be at/below the actual pinned high water and AFTER must be a subset of BEFORE. A retiring UID duplicated elsewhere must remain detectable by the physical namespace scan.
- Strengthen v2 and v9 with a closed captured-BEFORE ownership guard: any captured BEFORE physical UID found in an unrelated actual physical input is a conflict, including when the owned target already equals AFTER and has retired that UID. Only the exact captured Items target with actual PlayerItems input kind is its owner; no caller-chosen exemptions/callbacks, per-row I/O or logical AFTER substitution. Mail/detained references remain permitted subject to ordinary high-water checks. This closes an exposed v2 ambiguity while retaining physical scanning and unrelated evidence.
- Character is byte-identical for books and the currently proven Rune identity forms. Validate strict relevant Character/native currency/escrow state without changing unrelated fields. Book intent must have no unresolved escrow.
- Captured Character images use the retained journal schema (`ValidateCharacter`), as v1/v2 do. Do not add fresh LoaderHeroInfo byte-level/Level acceptance to settlement; actual ordinary admission enforces its own wire widths later.
- Book: exactly one unlocked, positive-stack, deposit-free inventory root uses an existing `LegacySkillBookUse` source mapping; consume one unit (decrement stack only or remove its physical UID), preserve every other root/deposit/perfection/container and append exactly one previously absent mapped default spell. Bound/Inscribed restrictions from trade/vendor are NOT book rules and must not be imported.
- Rune identity forms: Character BEFORE==AFTER; existing spell image BEFORE==AFTER, or genuinely absent BEFORE -> canonical four-byte empty AFTER. Reproject exact equip/unequip item transitions with the actual pure helpers/static family/slot, lock, raw stack and capacity rules; require full expected item bytes and conserved UID set. Root permits harmless exact no-op intent only when an actual source transition can produce it. Do not use current generic metadata as a historical proof.
- Trusted configured durable intent is settlement authority, not fresh action authorization or cryptographic producer provenance. Books' static mapping proves the persisted grant shape; original mutable magic metadata/class/proficiency checks remain the producer's responsibility before intent. Missing Rune outcome dependencies stay explicit.

## Expected wire behavior and fixtures
- Genuine owned Runtime/native Frame/Store/CTS transport/catalog, actual book producer for stack1/stack2 and physical missing-before Spells; real engine interruption at all prefixes/stage flushes. Preserve reserved existing spell byte11 and exact complete after-images/UID registry; fresh native admission and second restart cannot replay.
- Actual source Rune identity positives and malformed/static transition negatives; metadata-dependent genuine Rune removal/HP change remains preserved pending evidence rather than silently accepted.
- Refuse UID birth/duplicate/high water, malformed/lossy items/spells, wrong mapping/grant/stack delta, Character edits, foreign/current target data, changed Domain/root, tampered/foreign stages, unavailable lease and retained producer work. Negative tests may corrupt genuine captured WAL; positive WAL must come from the real producer.
- Managed Program companions cover raw/encrypted startup after Items replacement or actual final stage flush, exact after-images before login and second restart without replay. Author only during current execution hold.

## Evidence alignment
| Requirement | Source evidence | Status |
| --- | --- | --- |
| Book mapping/default fields/one-unit consume | LegacySkillBookUse, ItemUsage.SkillBooks, InventoryHelpers | Exact static/source-derived |
| Reserved spell byte/order preservation | LegacyPreparedSpellFileWrite.PrepareImage and native FreezeSessionSnapshot | Exact raw bytes, not lossy roundtrip |
| v9 domain/order/custody | CharacterItemsSpellsDomain/Images/Owner, OwnedStartupCustody | Preserved and extended to configured Spells |
| Rune identity item projections | ItemUsage.RuneEquipment and actual pure helpers | Bounded observable outcome; changed HP/removal unresolved |
| Full Rune metadata/HP dependencies | Program WorldDataStartup adoption; catalog/gear/alternate/transform trace | ACTIVE owner frontier in RUNE-CATALOG-OWNER-FRONTIER.md |
| Runtime/full gate/live client | No execution by this source team | Unproven |

## Implementation and qualification
- Work only in this owned archive candidate. Draft FSB-243 remains unqualified until Root reviews/compiles/runs relevant focused and full gates. Full v9/Rune work stays active beyond the current proven projections.
- No runtime/build/API/Git or actual checkout/client/updater/AV/save/route/restore/retained-mail changes. Do not retry or mirror Root's rejected unrelated artifact update.
- Offline source/fixture evidence is not power-loss, multiprocess/SQL/global economy durability, deployment, host cleanliness or full English/live-client acceptance.
