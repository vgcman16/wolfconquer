# D13 durable experience continuation

## Identity and scope

Revision 1, 2026-11-01; Root-directed implementation prerequisite. Target is
VisibleDx8D13. This is a new internal saved-state schema, not a client packet or
an interpretation of an undocumented client field. The typed codec is DATA
only. Its presence, parsed GUID or projection cannot grant XP, admission,
query completion, HP restoration or permission to write a file.

The goal is to retain an already authorized XP award and each unfinished
level/query/HP step across a process interruption. Actual private award,
writer, admission and resume owners are separate work. The existing
[level-up operation-owner spec](visible-dx8-d13-level-up-operation-owner.md)
records source ordering and the presently open multi-threshold boundary.

## Packet contract

No new packet or wire field. Original `GameClient.IncreaseExperience` performs
one `UpdateLevel` for each crossed threshold. That sends level/action92/stat
or point updates, runs full `QueryEquipment(CallItems:false)`, restores HP,
and eventually sends final EXP. This source observation is not live-client
qualification. Durable continuation does not promise exactly-once network
delivery, and a fresh request without a durable request ID is not deduplicated
by this marker.

## Catalog/table contract

One reserved key, `ExperienceContinuation`, is stored in the actual
`[Character]` section of `Users/{accountId}.ini`. Account ID and player UID are
separate positive UInt32 values, matching current ordinary-admission bounds.
The value is canonical ASCII, at most 256 characters, with exactly 14 `|`
separated fields. The ordinary Character image limit remains 16 MiB and its
actual encoding/BOM/other bytes are retained.

| Index | Field | Validation |
| --- | --- | --- |
| 0 | Version | Exactly `1` |
| 1 | AwardId | Nonempty GUID, lowercase 32-digit `N` spelling |
| 2 | AccountId | Canonical positive UInt32 decimal; matches actual account path owner |
| 3 | CharacterUid | Canonical positive UInt32 decimal; matches saved UID and actual owner |
| 4 | InitialLevel | 1..139 |
| 5 | InitialExperience | Nonnegative decimal |
| 6 | CreditedExperience | Positive decimal; initial plus credit <= Int64.MaxValue |
| 7 | RemainingExperience | Nonnegative decimal; equals saved Character.Experience |
| 8 | ConsumedExperience | XP already removed by committed threshold steps |
| 9 | CappedExperience | Explicit source level-140 discard, otherwise zero |
| 10 | CurrentLevel | 1..140; equals saved level and InitialLevel + StepIndex |
| 11 | StepIndex | 0..139; positive steps require positive consumed XP |
| 12 | Phase | 0 Ready, 1 QueryPending, 2 RestorePending |
| 13 | RestoreMaximum | `-` unless RestorePending; otherwise 0..Int32.MaxValue |

All XP components fit the checked signed managed Experience domain. Exact
conservation is `InitialExperience + CreditedExperience = RemainingExperience
+ ConsumedExperience + CappedExperience`. At level 140 remaining XP is zero;
the explicitly recorded capped amount preserves accounting of intentional
source discard. Step zero is Ready with no consumed/capped XP. Pending query
or restoration requires at least one committed level step.

Unknown versions, extra/missing fields, signs/whitespace/leading zeroes in
marker numbers, empty markers, duplicate owned keys or Character sections,
invalid GUIDs/enums, arithmetic overflow, mismatched saved UID/level/EXP, or
account mismatch are invalid. Absence is distinct from malformed presence.
Raw-image validation runs before `IniDocument` can collapse duplicates.
Only owned UID/Level/Experience/marker duplicates are rejected by this codec;
unrelated case-distinct keys and complete raw text remain intact.

## Validation and transaction rules

The proposed real transition sequence is:

1. Actual private combat/reward owner validates the award and current file.
   In one atomic Character replacement, add the complete credited XP and a
   Ready marker. This is the only credit step. No success output precedes it.
2. With the actual current threshold and stat metadata validated, consume one
   threshold and advance one level/stat step together with QueryPending in
   the same Character image. Never re-credit the original award on resume.
3. Run the actual owned full equipment query. Its real completed maximums
   owner may prepare RestorePending, recording the historical maximum. The
   number in the marker is not a current native cache or HP permission.
4. The actual HP writer restores from its real completed query and changes the
   marker to Ready in the same Character replacement. Then continue another
   threshold or settle the award. Remove a Ready marker only after the real
   owner proves no further threshold work is owed (or the source cap applies).

After reboot, either pending phase requires a fresh actual query under the
admitted Frame/Store/native operation owner. No persisted transient completion
token is accepted. Re-querying a RestorePending step may replace its historical
maximum without changing any XP counters. Repeated query/HP notifications are
not exactly-once delivery. A crash during a query can leave partial HP/query
files; actual resume must reconcile those owners before releasing gameplay.

The codec enforces the numeric and one-step transition shape only. It does not
verify a supplied threshold against a catalog, calculate source stats, check
reward entitlement or settle the award. The actual writer retains those duties.
The initial award ID must be issued by that real owner; a caller-selected GUID
is merely serializable data and grants nothing.

Generic Store publication, every Character writer and admission must respect
pending continuation custody. A startup marker cannot just be loaded and
ignored. Malformed presence refuses admission; valid pending presence remains
held until the actual captured image/native owner is resumed. Those production
fences and restart joins are not implemented by this codec.

## Expected wire behavior and fixtures

Use owned DATA images with distinct account/UID, mixed line endings, comments,
opaque fields, and exact bytes. Controls cover canonical round trips,
credit/threshold/query/restore/cap conservation, duplicate/malformed markers,
identity/scalar mismatches, overflow and unchanged unrelated bytes. Actual
writer tests must interrupt each persisted phase and resume through genuine
configured admission twice, proving no duplicate credit/threshold spend and
fresh completed query/HP state. A handwritten marker is a negative or DATA
fixture, never evidence of positive runtime award authority.

## Evidence alignment

The retained original `GameClient.cs` lines 976–1019 and 2131–2241 establish
per-threshold query ordering. Current `CharacterSnapshot` uses signed Int64
Experience. `LegacyOrdinaryAdmissionPlan.Prepare` bounds account and UID to
positive UInt32 independently. `CharacterSnapshotFileBaseline` bounds images
to 16 MiB. `IniDocument` merges duplicate keys, motivating raw marker checks.
The marker schema itself is new managed implementation design.

## Implementation and qualification

New `LegacyExperienceContinuation` and `LegacyExperienceContinuationCodec`
partials provide immutable validated DATA state, canonical value decoding,
raw Character-image projection and a raw-comparer-preserving holder projection.
No existing frozen production path is modified by this component. Team owns
the actual credit/step writer, mutation fences and admission/resume join; Root
reviews and qualifies the combined implementation. Source-only tests are not
executed by this helper. Live client/updater execution remains held.

An OPEN source-bug record is required for the current risk: multi-level XP
cannot be safely represented by collapsed modeled vitals or by partial
per-level commits without retaining the remaining award and pending query/HP
work. This new DATA schema alone is not a fixed claim, a durable outbox, a
global transaction, or proof of a complete restart path.
