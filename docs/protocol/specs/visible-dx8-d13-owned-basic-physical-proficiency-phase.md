# D13 owned basic-physical proficiency phase

## Identity and scope
- Revision2, 2026-11-01; source-only journal24 rebase onto Root checkpoint `cda6b65ba3ee3fcef3fbef42a0662c62dbe5ab3a`, within Action/basic-physical260. The held check01 candidate is a donor, not current execution evidence.
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
- Non-neutral actor proficiency and target HP, including actual optional death-derived Spells, participate in the same closed journal24 intent. Validate the actual actor/target, native graphs, configured roots, complete before images, current lease and continuation/marker guards before commit. Publish the canonical holder and native proficiency/HP participants only after the genuine combined durable outcome is proven. The live committed publication retains its existing ordered output phase; no unrestricted loader save, accepting callback or generic completion flag grants permission.
- Zero requested damage or no actual current weapon proficiency produces no file or proficiency packet; the closed producer still completes its required neutral phase before repeat arming.
- A pre-intent refusal leaves HP and proficiency unchanged; a post-intent uncertain outcome fences the actual owners and recovery completes only the retained validated transaction. A post-commit output failure preserves the durable outcome and cannot reapply it. This journal does not make XP, rewards, SQL, fresh requests or transport delivery atomic with the hit.

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
- The actual basic-physical hit owns issuance; the closed combined writer and recovery path, not a DATA calculation, own journal24 persistence. Neutral hits retain their existing path. Preserve the current real receipt pointers, managed origins, native committed-generation publication and proc closed-purpose union; this basic-physical spec does not confer Dragonhowl/proc authority.
- The rebased tests include exact joint images before the first Interaction output, ordinary successful 2041 progression, Hawk zero-applied-damage, actual producer-issued recovery intents and first-Interaction/2041 transport loss. Their declared source cases have not been executed here. Missing-before Profs cannot be positively exercised by deleting an admitted bound file; the genuine lifecycle remains open.
- SOURCE ONLY, UNBUILT/UNRUN for this rebase. Root alone qualifies/integrates. General proficiency writers, other combat branches, fresh-request deduplication, full native/client parity and actual client acceptance remain open.
