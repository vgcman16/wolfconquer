# owned-gm-startup: explicit local GM policy snapshot

## Identity and scope
- Revision 1, 2026-10-30; user-authorized disposable-character testing tools.
- Target: `VisibleDx8D13`. This startup policy does not verify Chinese7937 chat.
- No policy is generated from a character name, Role field, Discord rank or XP.
- Source authored in the isolated GM candidate; tests remain unrun by the author.

## Packet contract
Not applicable to startup configuration. Existing D13 chat remains the separate
ingress contract; loading JSON neither sends a packet nor admits an actor.

## Catalog/table contract
Not applicable. The policy is strict UTF-8 JSON as defined in
[owned-gm-policy-parser.md](owned-gm-policy-parser.md), not a client catalog.

## Validation and transaction rules
- `WOLFCONQUER_GM_POLICY_FILE` absent/empty disables the policy. Explicit whitespace,
  relative/network/device/alternate-stream paths and unreadable files refuse
  startup. Windows paths require a drive-rooted ordinary local path.
- Read at most 16 KiB once. Reject an observed reparse leaf or ancestor, empty or
  oversized file, changed length, malformed UTF-8/JSON or invalid policy. Preserve
  the input; do not create a file, normalize grants or supply a default domain.
- Existing ordinary-file pre/post checks do not attest the host or prove exclusion
  of an uncooperative path race. The retained immutable byte-derived policy is a
  configuration snapshot, not a proof of continuing file identity.
- Program parses once before opening the owned runtime. A configured policy needs
  explicit owned-admission options with the same domain and the D13 profile.
- Frame construction also checks the actual nonnull runtime's domain/profile
  before claiming its Frame slot. After the real claim, actual operator-domain
  configuration/readiness must succeed before retaining `_gmPolicy`.
- A policy grant is data only. Every command still needs actual Frame, connection,
  Store, binding, lifetime, PIN and transaction admission at invocation time.

## Expected wire behavior and fixtures
Startup refusal sends no game response and starts no GM listener. Existing local
OS-operator pipes remain separate. Owned temporary-file tests cover disabled,
valid immutable snapshot, explicit invalid/missing/directory paths, exact byte
limit, bad encoding/document and real reparse paths. The file-symlink case skips
only when the OS denies symbolic-link creation; no settings are changed. The
Windows ancestor case uses an owned directory junction without symlink privilege.
All cleanup is confined to the generated temporary root and preserves both test
and cleanup failures.

## Evidence alignment
| Requirement | Source/fixture | Bound |
| --- | --- | --- |
| Actual domain before Frame claim | `ownedAdmission.Ordinary.Domain` | Required configuration match, not actor admission |
| Real current Frame/readiness | `TryGetOperatorGearMailConfiguration` | Rechecked by commands; startup is not a continuing lease |
| File/reparse refusal | `CharacterSnapshotFileBaseline.TryValidateSafePath` | Ordinary path observation; no hostile race or host-trust claim |
| Bounded immutable policy | `LegacyGmPolicy.Parse` | Policy data only |

## Implementation and qualification
- `LegacyGmPolicyConfiguration.cs`, optional Frame constructor argument and
  WorldServer `Program.cs` startup seam.
- `LegacyGmPolicyConfigurationTests.cs` and `.Paths.cs`: 24 authored outcomes,
  unrun. Root owns execution, integration and constructor/registered-ingress proof.
- No live policy/configuration, game/client launch, runtime restart, permission
  grant, item creation, Git action or deployment was performed by this author.
