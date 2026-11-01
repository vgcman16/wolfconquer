# FSB260-WAL22: owned physical death Character and Spells settlement

## Identity and scope
- Revision5, 2026-10-31, Root/lead-owned physical attack continuation. Exact HP0/derived-spells22 schema and configured custody are source-authored with bounded peer review. **Initial260 compilation exposed integration errors; behavior remains UNRUN and unqualified. This document activates no runtime.**
- Target: VisibleDx8D13; existing successful packet codecs remain unchanged. Source/reference behavior is not live-client proof.
- Actual admitted two-owner physical receive → ordinary death calculation → target Character+Spells durable settlement. No v8/v9 fallback, caller-chosen spell levels, native cache recovery or output replay.

## Packet contract
- This adds no packet or wire field. Actual source death/query notifications remain prepared by the genuine death producer and are published only from its committed receipt.
- Derived spell Set uses existing full spell packet; Remove uses existing Action109. Effective order21930 then20000. Absent removal emits neither transition nor frame.
- Unknown extension data is not a record permission. Strict Version22 record unknown/duplicate keys refuse.

| Field/path | Number/offset | Wire type/width | Meaning | Presence/repetition | Bounds/default | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| Saved spell row | offset4+12*n | 12bytes LE | Existing saved fields and opaque byte11 | Complete unique membership | Existing parser bounds | PlayerSpellSnapshotLoader / prepared writer |
| Spell fields |0/2/4/8/9/10| ushort/ushort/int/byte/byte/byte | Id/Level/Experience/PreviousLevel/SoulLevel/UseSpellSoul | Six modeled fields | NonzeroID, nonnegativeXP, existing soul bounds | Data/PlayerSpellSnapshot.cs |
| Opaque byte |11| byte | Unmodeled existing row data | Retain byID for existing rows | Never invent semantics | LegacyPreparedSpellFileWrite.PrepareImage |

## Catalog/table contract
- Actual selected equipment / alternate / Class / learned10311 feed the same `LegacyEquipmentQuerySelector`; MythSoul on all selected rows determines20000.
- Actual complete ordered loaded CombatGear rows feed kind40/42's incremental top10 calculation; invalid/inapplicable rows retain their exact skipped behavior.
- Root/B's live complete used catalog proof remains mandatory. This recipe persists no unrelated HP/magic tables. Replay uses fixed selector rules, complete source Items/MythSoul, Before Class/10311, ordered rankings and at-most2 exact granted Magic rows for effective Set transitions. A generic catalog, arbitrary rank or caller level is not an allowed substitute.

| Column/offset | Name | Type/width/encoding | Validation | Packet/behavior relation | Evidence |
| --- | --- | --- | --- | --- | --- |
| Ranking Fields0/2/3 | CharacterId/kind/points | int/byte/uint invariant | Exact source skip and per-row stable top10 |21930 level derivation| DerivedSpells.CalculatePirateRank |
| Item.MythSoul | typed lossless item field | uint | Actual selected source roots; checked byte level sum |20000 level derivation| DerivedSpells.CalculateDerivedSpells |

## Validation and transaction rules
- Live permission comes only from the actual decision/attempt-bound `OwnedPhysicalDeathPreparation` and held real Store/actors, not its exported DATA transitions.
- Version22 uses exactly Character(AccountId) then Spells(CharacterUID), complete images, actual configured Users/Spells roots and read-only Items proof. Both Character images bind the same UID. No UID mutation/birth or Items write.
- Exact PhysicalDeath keys: `TargetUid:uint`, `QueryAdmitted:bool`, `Alternate:bool`, `ItemsBefore:byte[]`, `RankingRows:array`, `DerivedSpells:array`, `GrantedRows:array`. All arrays are non-null. Ranking rows are exactly `{CharacterId:int,Fields:string[]}` in full loaded source order, with typed CharacterId matching raw Fields0. QueryAdmitted and Before Class establish whether the ranking branch was read; when it was not read, RankingRows must be empty. A genuinely read empty ranking also remains empty, never an invented provider.
- Transition rows are exactly `{Id:ushort,After:null-or-six-field-entry}`, maximum2, unique source order21930/20000. No effective transition means an empty array. GrantedRows0..2 are exactly `{Id:int,TypeId:int,Name:string,Fields:string[]}`, in exact effective-Set membership/source order and validated by the existing pure row decoder; no fake live catalog. Existing16MiB image/192MiB record/MaxDepth8 bounds apply. Rank count is bounded by the physical UInt16 source domain; strings/field arrays and total metadata have separate finite allocation bounds. Root-level exact22 keys are Version/TransactionId/Changes/CharacterItemsSpellsDomain/PhysicalDeath.
- Recompute21930 and20000 from those dependencies through the recorded-origin reconciler. Preserve every unmarked existing row, including its progress, soul, order and opaque byte; ID/default fields alone never establish ownership. Known owned21930 retires when its qualifying class/rank disappears, including outside the old source outer branch. Zero venom removes only a genuinely owned20000. Effective owned Set resets all four nonidentity/nonlevel modeled fields to0; a missing actual eligible row receives its origin in the same commit.
- 21930 prefers valid weekly rank, else valid overall; level11-rank is1..10. Root corrected inherited unsupported overall-only0. Each Set first resolves its actual adopted raw Magic row; captured row raw0/1/3/8 must match its Id/TypeId/Name/derived Level and successful existing decoder. Actual20000 levels0..56 are evidence, not a guessed hardcoded cap.
- Reconstruct Spells through the actual origin reconciler/serializer, preserving current source order and byte11 byID. Actual complete managed Before Spells must agree with physical Before's modeled rows; no alternate source-spells payload. Complete lossless ItemsBefore must agree with the actual managed source graph and configured physical target throughout replay. Character After combines the exact HP0 scalar projection with only the reconciled ManagedDerivedSpellOrigins field. All other persisted scalars and unrelated bytes equal Before. Recorded and proposed origins must match the actual complete proposed spell graph; no arbitrary bytes/HP/transient aliases.
- Prepare/validate images and stages before mutation; commit both through distinct typed22; advance the actual saved-spell anchor and native/holder outcomes only after commit. Rejection leaves owners/resources/output/UID state unchanged. Postcommit output failure cannot refund or replay death.
- Configured owned startup custody validates strict schema/domain, unique Account→UID/current image/stage/namespace evidence, exact read-only Items and complete physical item UID custody before replay. Spells-after/Character-before refuses when Spells actually changes. Unchanged Spells do not create an artificial replacement-order failure. Recovery restores files, never combat/grants/native timers/notifications.
- Existing completed ordinary UID-producer history remains mandatory through the actual registry path. Completed WAL records are deleted; there is no new22 archive or UID allocation. Do not invent a history owner or require later mutable Items to equal a completed death's old reference.
- Preservation/origin reconciliation is authored; its composed live writer/query/class/progression integration and runtime verification remain required. No retroactive claim is minted for an existing unmarked21930/20000 row.

## Expected wire behavior and fixtures
- Genuine actual physical death producer creates positive22 and source-order frames. No handmade positive records or accepting owner callbacks.
- Required tests: actual21930 Set/Remove/preserve,20000 Set/Remove, retained opaque byte11 and full unrelated spell chunks; ordered ranking ties/replacements; current metadata drift; real prefixes0..2 and flushed2; second restart stable; actual recorded malformed keys/levels/target roots/foreign stages/UID graph refusal.
- Root's18 DATA outcomes are authored for complete scalar/origin projection, literal rank-spell bytes, unmarked retention, known-owned retirement, opaque byte11/row order, membership/native bounds and duplicate Character authority. These create no damage/writer/recovery permission. A's six actual creation/history→Equip→different-character attack interruption/restart controls and Root's eight genuine-pending refusal controls are authored. **None has run for22.** Existing real physical tests do not qualify this new journal.

## Evidence alignment
| Requirement | Spec clause | Decoded catalog reference | Bounded wire fixture | Status/conflict |
| --- | --- | --- | --- | --- |
| Genuine derived transitions | Validation | Root actual builder and loaded ranking source | Existing spell/Action codecs | Source traced |
| Live catalog / historical selection | Catalog | Actual used live metadata +complete Items/ordered ranking source | Not a packet | Purpose-specific witness and defensive history export authored, unrun |
| Saved complete image/byte11 | Validation | PreparedSpellFileWrite real saved baseline |12byte saved row | Physical/modeled source equivalence and exact projection authored, unrun |
| Typed recovery22 | Validation | Configured owned startup custody | Genuine issued pending22 fixtures in progress | Source/API peer clear after exact flushed-stage hook correction; unrun |

## Implementation and qualification
- Finite source/API proposal and exact remaining owner inputs: `../../../../OWNED-PHYSICAL-DEATH-WAL22-PROPOSAL.md` in the containing artifact (outside candidate).
- Root's ROOT-22-PATHS manifest lists20production and3DATA-test paths. Pure DTO/validator/format/live producer/configured recovery are authored. Lead owns actual writer/Store/native publication. Root21 scopes remain separate. The specific known scalar-projection refusal is normalized to InvalidDataException, keeping the journal's fail-closed exception contract.
- All260 evidence is source-only / UNRUN. Real prefix/flushed-stage/second-restart/history controls, membership reconciliation and composed qualification remain required. No build/test/live-client/deployment/full combat/full parity or verified fixed-source-bug claim.
