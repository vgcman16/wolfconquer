# JOURNAL-DOMAIN-1: one process-local canonical Users transaction slot

## Identity and scope
- 2026-10-29 isolated source on current shared5daa-line source plus frozen C25 journal postimages from `helper-caprice-pair-implementation-next`. Root alone integrates/executes; the current full gate remains untouched.
- Serialize the common `.managed-trade-journal/pending.json` lifecycle for every actual engine writer and recovery, independent of optional Trades/package locks. Keep journal schemas1..9 and C25v11 unchanged; heldv10 remains unsupported/untouched.
- This is process-local canonical-path serialization and restart recovery, not global gameplay ordering, physical-alias attestation, live-owner replay, SQL, multi-process or power-loss atomicity.

## Packet and catalog contract
- No network packet, successful wire sequence, game cost, eligibility, table or saved schema change. Existing engine image/domain/version validation remains authoritative.

| Field/path | Number/offset | Type/encoding | Meaning | Presence/repetition | Bounds/default | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| Shared pending slot | existing Users/.managed-trade-journal/pending.json | existing JSON | one domain transaction | schemas1..9/11 | existing192MiB journal/16MiB target bounds | existing Format/Storage/C25 |
| TransactionId/images | unchanged | existing GUID/byte encoding | exact recovery identity/preimages/afterimages | existing schema membership | unchanged | existing journal fixtures |
| Domain gate | internal only | OS-comparer canonical FullPath/trimmed separator | in-process exclusion | per canonical Users root | no new ownership authority | actual caller lock audit |

## Validation and lock order
- All commits keep their COMPLETE existing ordered target/input `SnapshotFileLockCoordinator` monitors, then enter common Users J. In particular v7 includes unchanged admission inputs; v11 includes both Character files. No J->target-monitor path exists. Do not insert a common J key ahead of existing file keys.
- The existing journal permission checks can inspect package/ordinary owners and therefore run before J while complete file gates and real caller scopes remain held. Inside J retain only direct engine IO, same-root stable pending probes, and closed failure decisions. No acquisition of Trades, Store, actors, recovery, new snapshot monitors, arbitrary delegates or output sends under J.
- Hold J through pending inspection/publication, staging/replacement, verification, rollback/record deletion and finally stage cleanup. Every commit variant funnels through this common protected core. Outcomes remain true only on complete verified cleanup; definite unchanged refusal and uncertain/pending outcomes keep their current distinctions.
- `HasPending` takes the same J for a stable directory observation, so live readiness checks do not see a healthy sibling transaction's transient preparing/pending. It stays fail-closed on invalid/unreadable state. Same-thread nested mutations/recovery refuse BEFORE snapshot acquisition; cross-domain probes from a mutation may not wait on a second J.

## Recovery
- Under a short J, capture the exact pending bytes and directory membership, parse the same bounded schema and release J. Resolve declared roots/targets and acquire COMPLETE target/input file gates without J.
- Reenter J, require exact captured pending bytes and expected directory membership before any roll-forward/delete. If another recovery completed the original and the slot is stably clear, return true/recovered0. If the slot contains another record or unexpected evidence, refuse/preserve; never replay/delete using a stale record. A matching original retains existing validation/staging/cleanup behavior.
- Keep `LegacyTradeRecoveryState`'s startup-only contract. Its held startup lock is not permission to replay an active World's files, and this patch does not widen that authority.

## Closed interruption/fault contract
- Replace ALL exposed `Func<int,bool>` journal/persistence hook forwarding with a sealed engine-owned plan: fixed validated boundary plus closed enum action, no caller code/path/result/saver callback. Production null/default remains no-fault. Preserve each old boundary count and exact source outcomes.
- Stop-after-boundary models the existing managed interruption. Closed synthetic IO/security/invalid-operation exceptions retain the explicitly synthetic error controls. A real sharing lease can open only a declared target ordinal or the current pending path at its validated fixed boundary; no arbitrary path is accepted.
- Capture detached actual visited counts, pending existence and target-prefix images for assertions. Real read-share/exclusive handles remain alive through original rollback/delete/recovery-refusal assertions and truthful disposal. Dispose never manufactures successful cleanup or masks the primary outcome. No lease grants commit/persistence authority.
- Preserve real tests: target1 read-share after first replacement permits exact rollback; target1 exclusive makes rollback unreadable; pending read-share after last replacement retains completed bytes on delete failure; NpcSale's declared Character0 lease at boundary1 blocks rollback while an external Items lease causes replacement failure. Quest's external target2 lease and trace, and DragonSkin's uncaught InvalidOperation boundaries, retain their meaningful distinctions.

## Evidence and fixtures
| Requirement | Actual source seam | Test evidence to author/retain | Status |
| --- | --- | --- | --- |
| All writers share slot | v1/v2/v3/v4/v5/v7/v8/v9/v11 -> CommitLocked | real barrier-start disjoint same-Users transactions | unqualified source |
| No transient false pending | live HasPending/readiness prechecks | real concurrent pending probes without callback pause | unqualified source |
| Exact recovery identity | capture -> files -> J recheck | actual private capture followed by genuine completion/new record and stale-phase refusal | unqualified source |
| IO/failure classifications | existing real OS sharing tests | closed leased target/pending controls with exact before/after/pending assertions | preserved via migration |
| Caller order | Store/actor autosavev8 vs Trades/Store/actor paidv8/itemv9 | source map in helper-journal-order-review-next/DESIGN.md | source-only; no invented lock owner |

## Implementation and qualification
- NativeMap owns C continuation and NativeStore independently reviews. This package owns only the shared journal engine/gate/closed hooks and their necessary migrations/tests; no C gameplay/packet change.
- Root retains prior failures and runs actual mixed-version concurrency, all journal/persistence/recovery/failure neighbors and relevant Frame/owned callers before any publication claim. No helper execution/build/parse/test/Git/host/network/DB/UI action is authorized or claimed.

## Root local qualification - 2026-10-29, combined 108 candidate

This appended execution record supersedes earlier source-authored/pending wording
for the locally qualified composition; those historical findings remain above.
Root build04 completed with 0 errors / 114 historical warnings. Corrected focused02
passed 51/51 and neighbors01 passed 3614/3614, with 0 failures/skips in each selection.
These are local source/fixture checks, not a new full Windows gate or live proof.

The prior full Windows40867334704 passed exact531a9aa3 (Core 29,833 passed / 152 existing
fixture skips, Updates 446, Launcher 57). That separate published source predates
this Caprice/shared-journal/status composition and does not qualify this candidate.
No new full gate has run yet. Live/client, whole-cast/SQL/native-physical-Stamina,
multiprocess and power-loss guarantees remain outside this qualification.
The final composition includes the closed interruption-plan migrations, actual
engine serialization/recovery controls and the Victory startup forwarding overlay.
The recovery test path correction above the actual v11 afterimage checks did not
alter the journal engine or authorize unrelated sidecar reads. The evidence is
bounded to these in-process engine/file tests and existing neighbor behavior;
physical aliases, active-world replay and cross-process ordering remain unproven.

## Full Windows qualification, superseding pending notes above

Full Windows [40886558198](https://github.com/vgcman16/wolfconquer/actions/runs/40886558198)
passed on exact50cf80fa8c5d1c334d7ee6bfea0715040c66fc56, containing source30e793f0
and the preceding decoded-Echo loader dd976947. Core29,929 passed/152 existing
NotExecuted, Updates446/446, Launcher57/57; all eight stages passed with0failures.
Actual local20261029T135633667Z-e23c3f08 finished2026-10-29T14:44:06.9964313Z.
All51 new combined focused outcomes and46 decoded-Echo catalog outcomes are
present as Passed in the full core TRX. Actual logs/receipts/all3TRXs and matching
complete World/DataTool runtime files are preserved outside the runner.
This qualifies the Caprice pair, shared journal, XP/Fly preflight and Echo loader
source. Whole-cast/SQL/native physical Stamina, power-loss/multiprocess safety,
deployment, newer-client/English/full gameplay and live acceptance remain open.
No original-save/route edit, legacy client relaunch or retained gear resend followed.
