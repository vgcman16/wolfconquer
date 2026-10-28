# Recoverable FullAccount class-reset saves

Qualified offline on 2026-10-27. The existing NPC465123 options1..11 retain their
class grants, eligibility, level/stat calculations and successful packet order.
The managed implementation now checks the current owner and both saved images
before a recoverable two-file commit, then publishes the prepared session.

## What changed

- Require the exact current Store holder, live identity, real configured recovery
  owner, resolved trade/escrow/PIN state and actual send/close routes.
- Prepare complete Character/spell changes and all frames before commitment.
  Newer saved data refuses the action rather than silently becoming its baseline.
- Preserve Character encoding, unrelated fields and opaque byte11 of unchanged
  retained spell rows. Reset/new spells retain the original zero-progress rules.
- Use distinct v8 Character(AccountId)+Spells(CharacterUid) journal participants.
  No Items participant or ordinary-admission authority is fabricated.
- Retain interrupted before/mixed/after images for fresh configured recovery.
  Validate the complete domain, image identities, membership and current bytes;
  clear every same-transaction stage before discarding the pending record.
- Recheck relevant native magic cache after collection callouts. Matched absolute
  routes support trailing separators; relative paths gain no new authority.
- Postcommit transport failure keeps committed state. Recovery-required outcomes
  do not claim that files were unchanged.

## Actual verification

- First old-source baseline:52 cases,37 passed/15 failed/0 skipped. All15 failures
  were fixture setup UID mismatches and are not business-defect evidence.
- Fixture-only correction uses the real decoder/full Character parser over owned
  bytes. The production LoadByAccountId account=UID invariant remains unchanged.
- Corrected-fixture old-source baseline:52 cases,39 passed/13 failed/0 skipped.
  Failures cover stale/retired/foreign holders, ended/trading connections, missing
  routes, newer disk state and erased retained spell padding.
- Corrected public:354/354 passed; corrected private:354/354 passed; no skips or
  failures. Both exact name sets match and retain all52 baseline names.
- This comprises53 FullAccount controls,49 direct v8 journal controls and252
  adjacent trade/quest/mailbox/admission/starter/promotion/Performer regressions.
- After an equivalent assertion cleanup, the two affected public staging cases
  passed2/2; the private354 run already includes that final assertion. Counts
  overlap and must not be added into project-wide coverage.
- Existing11-class goldens and test bodies remain. New direct-v8 APIs and the
  late-cache proposal regression were corrected-only controls, not old-source
  defect proof. Real failures/logs/TRXs remain retained locally.

The prior exact source8e8ff83f passed full Windows40569779073: Core28,974 passed/
164 existing NotExecuted; Updater434; Launcher57; synthetic parsers20+13; all eight
steps0. That result predates this class-reset batch. Its new exact-public full
check is pending. Existing framework/analyzer warnings remain; no zero-warning
or CodeFactor-completion claim follows.

## Limits and next work

Distinct account/character fixtures prove the direct class API's file-key contract,
not ordinary-login admission of distinct IDs. File fixtures exercise reconciliation
through fresh recovery owners; they do not prove OS power-loss or multi-process
durability, full SQL, or native player/spell/equipment graph adoption. Native owner
identity checks do not themselves initialize or synchronize that whole graph.

The source has not been deployed or accepted by the live client. Original saves,
routes, restore material and running sessions are preserved. Native/session spell
coherence, other learning/book/rune transactions, full English, History crash,
full-client distribution and broad gameplay parity remain open.

See [the protocol workflow](engineering/PROTOCOL-WORKFLOW.md),
[ordinary spell integration design](protocol/specs/visible-dx8-d13-ordinary-spell-coherence.md)
and [source-bug record FSB-158](../FIXED-SOURCE-BUGS.md).
