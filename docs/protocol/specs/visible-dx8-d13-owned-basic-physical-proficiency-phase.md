# D13 owned basic-physical proficiency phase

## Identity and scope
- Revision1, 2026-10-31; Root-approved source implementation within Action/basic-physical260.
- Retained VisibleDx8D13 source-shaped behavior. No live client or7937 activation claim.
- Only the actual admitted basic-physical producer's private calculated hit may issue this phase. Other spell/monster/proficiency writers remain separate owners.

## Packet contract
- Keep the existing `LegacyProficiencyPacket` opcode, four UInt32 payload fields (ID, level, experience, needed experience) and successful ordering.
- Interaction and target HP/death notifications remain before proficiency output. Each proficiency packet follows its actual completed file/native/Store commit. Repeat arming follows all required output and proficiency completion.
- No new wire fields, fabricated accepted damage or caller-selected proficiency gain.

## Catalog/table contract
- Existing right/left active weapon selectors and `LegacyProficiencyPacket` progression are used unchanged.
- Source applies raw requested physical damage times the configured existing1000 rate with the existing UInt32 clamp, then the existing proficiency experience formula.
- Retained Role.Instance/Proficiency.cs66–119 explicitly creates a missing row at level0/experience0 without applying that first hit to the new row. Preserve this source-backed behavior; it is not independent client first-hit acceptance evidence.
- The physical file is a four-byte count followed by20-byte rows. Existing row bytes9–11 are opaque and survive by ID; new rows begin with canonical zero reserved bytes. All modeled fields, complete membership and native graph must match the actual before snapshot.

## Validation and transaction rules
- Resolve Profs root only from the same actual Frame snapshot loader/current Runtime and admitted binding; UID alone is not authority.
- Before the target HP writer, derive immutable proficiency outcome/packet bytes from the private prepared hit, capture complete bounded physical before image under the actual snapshot lock and preflight known I/O refusal.
- Genuine absent file within the existing configured Profs directory and with an actual empty bound snapshot may use the existing create-only coordinator. A missing/unreadable directory refuses before HP commitment; this phase does not mint a directory-creation owner. A present file must match the complete native/Store before graph and remain exact at final write.
- Only the same private committed basic-physical publication, after its base notifications completed and while both actual participants/generation remain current, may consume the prepared proficiency phase.
- Use the existing exact-before atomic replacement or create-only coordinator. Publish native proficiency COW and canonical Store snapshot only after the actual file outcome is proven. No unrestricted loader save, accepting callback or generic completion flag grants permission.
- Zero requested damage or no actual current weapon proficiency produces no file or proficiency packet; the closed producer still completes its required neutral phase before repeat arming.
- A post-HP proficiency failure fences the actual committed action and prevents repeat arming; it cannot refund/retry the target HP hit. The target HP and later proficiency file are separate durable phases. Crash recovery between them remains an OPEN reconciliation gap and this feature is not whole-attack crash atomicity.

## Expected wire behavior and fixtures
- Genuine owned weapon hit with an existing proficiency: exact independent packet and complete file before first progress output, native/Store agreement, then repeat eligibility.
- Missing row: actual new0/0 row, not invented first-hit XP.
- Real file read lease, stale before image, escaped native-row drift, transport/EOF and retired owner: preserve the relevant precommit graph or fence an already committed HP/proficiency outcome; never arm from send/handled alone.
- Preserve unrelated proficiency rows, row order and opaque reserved bytes; no item UID allocation or change.

## Evidence alignment
| Requirement | Source | Status |
| --- | --- | --- |
| Actual requested damage and weapon order | DirectPhysical and MonsterCombat.Proficiency existing call/helper | Source-shaped |
| Missing row starts0/0 | Retained Role.Instance/Proficiency.cs66–119 | Source-observed, Root read |
| Physical count/20-byte schema | PlayerProficiencySnapshot loader | Existing parser |
| Native COW owner | LegacyNativeProficiencyStorage.PreparedSessionMap | Actual admitted owner |
| Exact file outcome | Existing CharacterSnapshotFileCoordinator captured replace/create-only | Existing mechanism, no new global authority |

## Implementation and qualification
- New focused pure plan, prepared file writer, actual Store proficiency continuation and Frame configured-root join; actual basic-physical publication owns issuance/completion.
- SOURCE ONLY, UNBUILT/UNRUN. Root alone qualifies/integrates. General proficiency writers, all combat branches, HP→Profs crash reconciliation and full native/client parity remain active.
