# Runtime global-worker join and postpublication retirement

## Controlled CrackStar expiry fixture follow-up

After exact public9769a9a8/full40732279561's sole one-second expiry wait timeout,
the test uses the existing manual clock to observe both actual player/global
Task.Delay registrations before advancing10ms. Seeded status/expiry fields and
absence of completion must be observed before advance. No expiry method is
invoked directly, and the one-second behavior deadline is unchanged.

Holder publication precedes the actual status send. Wait for the real expiry
log written after that send, supporting the synchronized writer's actual sync
and async call paths, then check the exact cleared status vector from a locked
frame snapshot. Cancel and join actual tracked workers before writer disposal;
retain both primary/cleanup failures and require timers drained. These are
test-fixture completion/lifetime controls, with no new protocol or product rule.

Release build passed; all46 selected runtime-expiry/CrackStar/worker-lifetime
cases passed without failures/skips. The original full-run timeout cause remains
unproven; a successor full gate and live acceptance remain required.

## Identity and scope

Source-derived test correction against exact public2811f796, 2026-10-28.
The recorded fullCI40718716578 failure is RuntimeGlobalJoin.cs119's old external
observer-lock assertion, reached after the existing global-clock join checks.
Chinese7899/VisibleDx8D13 remains the target. No production behavior is changed.

## Packet contract

No packet ID, payload, frame sequence, runtime tick or successful gameplay rule
changes. This concerns actual task completion and Store publication phases.

## Catalog/table contract

Not applicable. No catalog, saved schema or game data is changed.

## Validation and lifetime rules

1. Cancellation callback completion is not completion of a global runtime worker.
   A genuine entered tick held in TimeProvider.GetUtcNow must keep its actual
   returned/tracked Task incomplete after CancelAsync finishes.
2. Capture those actual worker Tasks while the controlled tick is entered. Join
   them before fixture cleanup; do not replace that join with a delay or seed a
   runtime counter. The global-loop finally marker is an additional diagnostic,
   not a universal application/registry-lease completion proof.
3. The real Frame persistence postlude retains its exact current Store gate and
   owning Frame identity. Arbitrary constructor sessionChanged observers are a
   separate phase; they are not synthetic stand-ins for an INI/save participant.
4. A public postpublication observer runs after package/Store/actor scopes end.
   Retirement may complete while that observer is still held. The original CAS
   keeps its actual historical committed result even if its current holder is
   removed before the observer returns.
5. A late stale expected-session CAS must not recreate the removed holder.
   Preparation-time reentry refusal and concrete Frame persistence ownership
   stay unchanged. Cleanup releases the test barrier and joins actual tasks;
   simultaneous primary/cleanup failures remain visible together.

## Expected behavior and fixtures

- HandlerRuntimeCancellationJoinWaitsForEnteredGlobalTickBeforeFixtureCleanup
  starts the actual global worker, holds its real clock entry, captures actual
  tracked Tasks, cancels, and requires their join to remain incomplete until
  release. Cleanup awaits the captured/current actual Tasks before clock disposal.
- HandlerSessionRetirementCompletesWhilePostPublicationObserverWaits uses a real
  Store/CAS/removal and an external observer barrier. The callback asserts no
  publishing connection gate is held. The actual committed out-result/current
  record are visible before removal; removal must finish before observer release,
  while the publishing call still waits. The returned committed result remains
  intact after release, and stale CAS cannot restore the owner.

These are separate tests because worker drain and observer retirement are
different contracts. No accepting writer, fake persistence callback, modified
Store gate, native owner or replacement worker queue is introduced.

## Evidence alignment and qualification

The old test incorrectly described its arbitrary observer as the same boundary
as synchronous Frame INI persistence and required removal to block there. Exact
public Store source instead calls PersistFrameOwnedSessionChange under its gate,
then NotifyPublishedSessionChange only after publication scopes end. The failure
does not prove the concrete Frame persistence/worker lifetime was broken.

Relevant unchanged controls: WorldSessionPublicationObserverTests (including
preparation-reentry and actual Frame persistence-failure phase),
WorldSessionStoreConcurrencyTests, and LegacyOwnedAdmissionRuntimeTests.Lifetime.
See [publication phases](world-session-publication-observers.md) and
[actual owned admission lifetime](../../OWNED-ADMISSION.md).

Root's affected selection passed207/207 with zero failures/skips, including both
named tests and the actual publication/owned-runtime lifetime controls. A full
successor gate remains required. This unbound Frame fixture does not itself prove
registry release, SQL/crash safety, every runtime worker, multiplayer behavior or
live-client shutdown.
