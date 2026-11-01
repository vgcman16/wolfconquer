# Ordinary admission fence dispatch proposal

Status: source implementation and 19 test outcomes authored after Root approval;
UNBUILT / UNRUN. No builds, tests or Git operations have been performed by this
helper. Base is the local, unqualified `cfbaad27` source checkpoint. Root reviews
and intakes explicit files into its separate actual-failure repair workspace.
The failed earlier gate remains failed; this source change does not qualify it.

## Observed evidence

The frozen `94006b64` full Release gate's first owned-testhost stack capture
recorded its active thread at `PathInternal.NormalizeDirectorySeparators`,
called by `LegacyOrdinaryAdmissionMutationFence.RequireFileMutation` through the
actual journal/NPC pickup writer. The current test was
`PickupItemLockLifecycleSavesFinalFullImageBeforeInitialLockedFrameAndKeepsPhysicalHolder`.
Evidence: `.artifacts/root-action260-public-checkpoint-next/testhost-stacks-01.log`,
lines 112-144. The gate subsequently ended FAILED at
`2026-11-01T03:06:42.725Z`, solution-test exit 1. Restore/build passed and later
stages did not run. The second stack capture found an ended testhost; this is
not evidence of a hang or tooling block. Root is preserving and inspecting the
actual failed results separately.

The inspected `94006b64` and `cfbaad27` fence implementations have the same
dispatch structure: every file write snapshots all registered owners, enumerates
each owner's `Roots.Kinds` (including Enum.GetValues/LINQ work), and recomputes
the target parent directory inside that owner/kind loop. Session and
authenticated-creation checks also scan every registered owner. Owner entries
are intentionally retained after registry disposal because ended leases must
continue denying unsafe writes. This creates a growing unrelated-world lookup
cost. One stack sample establishes a real hot-path observation, not an aggregate
CPU attribution, a diagnosis of the test failure, or a measured speedup.

`LegacyOrdinaryAdmissionMutationFence.Domains.cs` already dispatches configured
journal checks by participant directory and freshly inspects matching pending
journals. That existing historical domain protection must remain unchanged.

## Implementation boundary

The implementation retains the existing actual registry keyed by Users root and its exact
`StringComparer.OrdinalIgnoreCase` behavior, including on non-Windows systems.
Add a private directory index containing immutable ordered entries of actual
owner reference and participant kind. The index is only lookup data. An entry,
root string or recorded kind must never grant mutation or recovery authority.

Only genuine `Register(owner)` updates membership under the existing `Gate`.
Compute each owner's immutable participant roots/kinds once per registration.
Retain original owner insertion order and kind order in matching buckets so
short-circuit/refusal behavior remains equivalent. Same-Users re-registration
replaces precisely that existing registry slot and its participant memberships,
matching today's `Owners[owner.Roots.Users] = owner`; it does not delete unrelated
owners or prior configured-domain knowledge. Distinct Users roots sharing any
participant directory must all remain in that directory bucket. Do not prune
disposed owners, weak-reference them, filter blocked owners or infer no debt
from a retired registry. Re-registering the same owner is idempotent membership.

Build replacement bucket arrays before publishing registration changes. Under
the one Gate, registry and affected bucket membership become visible together.
Readers retain an immutable matching-bucket snapshot after releasing the Gate;
concurrent re-registration cannot mutate that snapshot. A reader that retained
the older owner still evaluates its actual current/pending state, as the current
all-owner Snapshot implementation does. No owner call or filesystem inspection
occurs while holding the registration Gate. Preserve the existing separate
DomainGate ordering; do not introduce a cross-gate owner callback.

Private dispatch helpers in `LegacyOrdinaryAdmissionMutationFence.Dispatch.cs`:

- `SnapshotParticipants(directory)` returns only the immutable ordered bucket
  for that exact directory using the existing fence's case comparison.
- `SnapshotSessionOwners(previousUsers, nextUsers)` selects at most the two
  current registry slots under one Gate, deduplicates a shared slot and retains
  original owner order. It does not return cached pending/current verdicts.

`RequireFileMutation` still calls `Path.GetFullPath` and the unchanged configured
journal fence before owner decisions. Extract directory and leaf once per input
path. For each matching participant entry, preserve the current extension and
unsigned leaf parsing exactly: Character/CombatGear `.ini`, other kinds `.bin`,
case-insensitive extensions, `NumberStyles.None`, and current account parsing.
If one owner maps multiple kinds to a shared directory, retain each kind entry
and its current behavior rather than treating a directory as one generic file
type. Check `owner.IsPending(account)` freshly, followed by the existing exact
prepared-plan permission. Owned startup still calls its real
`PermitsOrdinaryOwner(owner, absolute)` for every applicable owner; no clean-debt
shortcut may permit a foreign owner on a shared participant path.

Authenticated creation uses the same directory dispatch but retains its stricter
invalid-leaf exception, its exact producer/account bypass, foreign pending-owner
refusal, and both producer target proofs. Session checks retain normalization,
configured journal checks, account bounds and exact pending-owner permission,
then inspect only the actual previous/next Users slots. Do not cache journal
existence, pending account sets, registry liveness or prepared-plan authority.

Lookup complexity is proportional to owners actually sharing the addressed
directory (or the two relevant session roots), rather than all owners retained
by the process. This follows from the authored lookup mechanism; no speedup has
been measured. Registration prepares replacement arrays and dictionary capacity
before publishing any membership and does not scan all registry entries.

The immutable-roots premise is evidenced by the existing sealed positional
`LegacyOrdinaryAdmissionRoots` record containing only strings and the actual
owner's constructor-assigned, get-only `Roots` property. A `with` expression
creates a different record. No runtime debt, registry lease state or filesystem
state is cached with those routes. Source validation currently refuses aliases
between kinds within one owner before registration; different genuine owners
can share a directory with different kinds, and dispatch preserves both entries.

## Authored test coverage

New `LegacyOrdinaryAdmissionFenceDispatchTests` partials use actual bootstrapped
registry leases and actual `LegacyOrdinaryAdmissionUidOwner.TryCreate` issuance.
Private dispatch reflection observes counts and entries only; it neither sets
owners nor supplies permission, counters or accepting persistence callbacks.
Coverage includes eight unrelated live/disposed domains with a constant matching
bucket, fresh blocked debt after a real interrupted registry reservation,
retained disposed owners, genuine reopen/re-registration order and old immutable
snapshots, shared directories with same/different file kinds, case/leaf bounds,
previous/next session roots, immutable route-copy behavior, and actual rejection
of within-owner aliased roots. A real pending journal after reconfiguration
proves that the old configured route remains protected.

The scoping test records 20 identical real `SaveByCharacterUid` operations on
the same owner/path/image before and after adding unrelated domains. It asserts
unchanged complete files and lookup membership, but makes no timing threshold
or speedup claim. The source is unrun; Root can collect the reported timings.
Separate existing-fixture partials exercise genuine startup recovery against a
clean or disposed foreign shared-root owner and authenticated creation/replay
with an unrelated retained disposed owner.

## Required independent verification

Before acceptance, preserve existing actual admission/startup/journal tests and
add discriminating real-owner coverage for: unrelated registered roots; shared
participant directories across distinct Users; a disposed or blocked retained
owner; debt introduced after a prior clean lookup; genuine same-Users owner
replacement; repeated registration; case variants; alias kinds sharing one
directory; malformed/wrong-extension leaves; previous/next session roots; and
foreign-owner startup/creation refusals. Verify that old configured participant
routes remain protected by pending journals after route reconfiguration.

Use genuine registry/owner issuance for positive and negative authority tests.
An internal dispatch-shape test may inspect bucket membership as DATA only;
it must not fabricate an accepting owner or mutation permission. A small test
reference of the old dispatch decisions can compare selected owner/kind order,
including overlapping roots, without becoming production authorization logic.
Do not remove failures or lower protection to get a timing result.

Root must run the appropriate focused checks and full gate after reviewing the
actual current failures. Later performance evidence should compare the same
owned saved-write operation with increasing unrelated registered domains,
retaining refusal/bytes/output assertions; elapsed time alone is not correctness.
No current speedup, fixed gate failure, live-client, SQL, crash-atomicity or broader
parity claim is made. Frozen checkpoints, original data and client/AV holds are
unchanged.
