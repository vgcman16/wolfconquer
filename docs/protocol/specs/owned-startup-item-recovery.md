# Owned startup recovery for a single Character/Items journal

## Identity and scope

Revision1, 2026-10-30. Source-only successor to public2ff18f12. Root alone owns
execution and qualification. The existing full Windows run qualifies that base,
not this unrun change. Existing VisibleDx8D13/7899 behavior is the evidence
profile; this adds no7937 wire or catalog interpretation.

An explicitly configured owned runtime must be able to finish an already-published
version2 single-owner Character/Items journal before admitting new runtime or UID
producers. The current startup rejects the journal before acquiring the real UID
namespace lease, so raw journal recovery alone does not establish server restart.

This is not a general migration or replay service. Multi-owner trade, mailbox,
ordinary-admission and other journal versions remain refused by this path. A v2
record that introduces any new physical item UID remains refused.
The first authority supports existing version 1/2 UID states without retained
producer intents or archives. Active/archive intent rows do not encode work-store
completion, so they cannot establish safe coexistence with this journal. Ordinary
writer allocation metadata also refuses. This includes completed history until
separate real work-store reconciliation is proved; no-WAL open stays unchanged.

## Packet contract

Not applicable: no new packet, opcode, field, encoding or ordering. Existing
successful repair frames and business validation remain unchanged.

## Catalog/table contract

No new client catalog or lookup. Recovery consumes the complete existing
journal and raw saved formats, including184-byte item/deposit rows and80-byte
perfection rows, through their existing strict parsers. It does not allocate
items, infer unknown catalog fields or replay a gameplay handler.

## Validation and transaction rules

The normalized explicit configuration, complete loader routes and actual mailbox
route must match before startup acquisition. The dedicated internal factory opens
the existing `owner.lock` using FileShare.None; it never bootstraps a namespace.
Its private-constructor typed custody retains that actual handle, exact expected
domain/configuration/state bytes and validated archive membership. It has no UID
reservation, producer or live runtime API.

Before any journal or target mutation:

- Verify the existing state/domain/configuration and ordinary paths while holding
  the actual exclusive namespace lease. Missing/corrupt state, a pending UID
  reservation or archive stage refuses without changing those bytes.
- After those checks, bind the configured participant directories through the
  existing deny-only mutation fence. A private captured-record direct guard must
  prove exact target paths, kinds, IDs and complete images before permitting this
  recovery. No general writer inherits permission. Only this exact Users/domain
  may bypass its own pending-journal denial; other configured shared-root domains
  and all actual package-owner checks remain effective. A retained ordinary owner
  must match the complete domain, roots and mailbox configuration.
- Validate the complete current raw namespace and archive consistency. Current
  observed UID maximum must fit the pinned state high-water.
- Capture one exact pending record from the actual journal directory. Require
  version2, exactly one Character and one Items participant, and complete nonnull
  before/after images. Character file ID is the account key; it need not equal
  the character UID. Strict parsed before/after UIDs must be equal and match the
  Items file ID.
- Validate complete before/after raw physical item UID sets. Both maxima must fit
  the same pinned high-water, and the after set must be a subset of before.
  A high-water bound alone is not permission to introduce another UID.
- Keep exact record/route identity and current-before-or-after target checks under
  the actual complete target-file locks and journal domain gate.

Recover only that captured record using the real journal staging/replacement
path. Recheck the same typed custody at actual write boundaries. Changed or
missing captured record refuses; absence alone is never completion authority.
Retain actual journal evidence on a conflict or uncertain prefix.

After recovery, require no pending journal and rescan the actual namespace. The
pinned registry state and archive membership must remain unchanged. Only then
construct the ordinary registry and continue the existing real ordinary-owner
claim/reconciliation/readiness path. No producer or runtime is returned before
those prerequisites complete. Existing generic TryOpen/TryBootstrap and producer
pending-journal refusal contracts remain unchanged.

Known transaction staging files require a closed captured-record scan path;
arbitrary path exclusions or accepting callbacks are not allowed. The final
implementation and fixtures must distinguish known operation scratch from
unknown entries without deleting unrelated evidence.
Only the deterministic Character/Items stages derived from the actual captured
transaction may contain an exact AFTER image or a bounded prefix of that image
(including empty). Unknown names, nonprefix bytes, oversized stages and directory
or reparse substitutions refuse. The STOP-only internal StageFlushed control
retains stages produced and flushed by the actual commit engine; it supplies no
bytes, path, writer or success result. It is not process-kill or power-loss proof.

## Rejection, retry and lifetime

Refusals preserve journal, target, namespace and archive bytes. Every refused
factory attempt releases its actual OS lease so a legitimate later attempt can
acquire it. Primary and cleanup errors remain distinguishable. Recovery is a
startup-only operation, never an automatic replay by an already-running Frame.

Repeated startup after successful completion must observe the same postimages,
UID set and high-water without charging or replaying the action again. Conflicts
remain fenced instead of being repaired by overwriting unrelated current data.

The lease fences cooperating producers. It does not attest host trust, exclude
all raw writers/restore ABA or prove power-loss durability. SQL, full multi-owner
restart recovery and live client acceptance remain separate unfinished work.

## Expected wire behavior and fixtures

Use genuine interrupted version2 journals at publication/Character/Items prefixes,
then exercise the actual public Runtime.TryOpen lifecycle. Check exact postimage,
wallet, durability, UID set/high-water and one-time reopen. Include distinct
account-key/character-UID mapping and real Frame/native admission where available.
Use owned fixtures only.
Also exercise an earlier actual runtime, a genuine interrupted journal, disposal,
then a new runtime, so retained route/owner fence state is part of the test. Check
actual flushed stages and valid shortened prefixes plus tampered/foreign refusals.

Refusal controls include competing actual lease, domain/config mismatch, missing
or corrupt namespace state, UID pending/archive stages, malformed/unsupported
journal, changed or missing target image, malformed raw tail, below-high-water
UID birth and above-high-water membership. Existing generic-open and unconfigured
startup refusal tests remain. All source-authored outcomes are unrun until Root
executes the isolated and appropriate full gates after the current lane ends.

## Evidence alignment

| Requirement | Evidence | Status |
| --- | --- | --- |
| Existing runtime rejects pending work before ownership | `LegacyOwnedAdmissionRuntime.cs` at public 2ff18f12; prior pending-WAL opening test | Observed source gap |
| Complete raw physical UID grammar | `LegacyItemUidRawCatalog.PlayerItems.cs` and its existing partials | Existing parser reused; no guessed fields |
| Published v2 target custody and staging | `LegacyTradeJournal.Storage.cs`, `.RecoveryCapture.cs`, `.Recovery.cs` | Existing grammar and writer reused |
| Client/profile and native admission | `VisibleDx8D13`; genuine saved Character/Items and existing native metadata fixture | Source-authored owned proof only; no new live claim |

## Implementation and qualification

The finite delta is listed in the private `PATHS.txt` handoff. It includes focused
registry custody, journal recovery and mutation-fence partials, actual Runtime
routing, the UID-array parser projection and owned interruption/lifetime tests.
Root must qualify `LegacyOwnedStartupItemRecoveryTests` plus affected existing
runtime, journal, UID and admission controls, then the integration gate. No test
command has run for this candidate. The private source-bug note is proposed text
for `FIXED-SOURCE-BUGS.md`, not a verified fixed entry. SQL persistence, unsupported
recovery shapes and live client acceptance remain outside this slice.
