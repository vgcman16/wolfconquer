# Ordinary Player and ranking persistence

2026-10-27. This batch prepares saved physical identity, complete Ninja rank-file
persistence and current-owner Performer learning. Focused public and private qualification passed.
FSB-154–157 record the corrected behavior and remaining limits.

Public corrected result: **524/524 passed, zero failures/skips**.
Private corrected result: **524/524 passed, zero failures/skips**.
New full-source check: **Pending for this composed source; no deployment or live-client acceptance**.
Both exact 524-case name sets match; every one of the 508 baseline names is retained.
The additional 16 corrected-only native/reader bounds were excluded from old-source execution.

The safe tests-only baseline ran 508 cases: 102 failed, 406 passed, zero skipped.
Three failures lacked the configured PowerShell 7 setup; their real rerun passed 3/3.
One existing high-login-count cleanup aborted; its real rerun passed 1/1.
That leaves 98 intended differences: 75 rank persistence, 15 physical identity and
8 Performer cases. These reruns are distinct evidence, not a rewritten baseline.

The previous full Windows run 40555473262 on 9e779d5d failed. Core had 28,861 passes,
164 existing NotExecuted and one stale null-item successful-empty-frame assertion;
Updater passed 434. Launcher/parsers were not reached. The actual failed receipt
is archived. Its failure is not a result for this new batch.
The stale null-item control now requires ArgumentNullException and preservation
of complete packet storage. Valid packet goldens remain unchanged.

The physical prefix publishes saved Class:uint and Level:ushort on the
same actual cold Player after its existing durable commitment. Complete before/
after witnesses include class/level and preserve Transformation and owner identity.
Fresh ordinary admission additionally checks Level 1..255 before durable work
because its existing LoaderHeroInfo field is a byte. Retained journal validation
keeps 1..65535; direct non-emitting prefix keeps 0..65535.

This plain mirror is not the original whole Level setter. Original
GameServer/MrRayzo.Role/Player.cs:1650..2475 performs FullLoading-gated rewards,
then an unconditional assignment/clamp of levels at least 150 to 150/Experience 0;
GameServer/MrRayzo.Database/ServerDatabase.cs:813 calls it; line 817 restores saved XP.
Managed saved-level preservation, the existing XP-gain cap 140 and byte wire width
are separate policies. Target maximum-level/cap behavior remains unresolved;
this batch adds no guessed 140/150 normalization or fixed-cap claim.

Rank Load prepares a bounded, strictly decoded complete NinjaRank.txt image and
six genuine detached category owners before one current-registry publication.
Malformed rows/counts/UIDs/encoding or unavailable/changed required owners retain
the prior graph. A missing file is a real failure, not permission for empty ranks.
Valid empty files, unsigned widths, category mirrors, ties and top-ten rules remain.

Rank Save validates the complete current graph and serializes before destination
mutation. Old Save already queued serialization before Execute; its defects were
stale cached/incomplete graphs, in-place I/O exposure and missing capture/write order.
The literal configured path and existing record format remain. The
same target-path coordinator orders capture/write with cooperating Load/Save;
the staged image is flushed/verified before per-file Move/Replace. Failures before
replacement retain the target; post-replacement verification faults are not rollback.
This is not a global database transaction or power-loss/crash-safety guarantee.

Performer learning prepares stable membership and all seven existing frames, then
uses the real expected-session Store transaction around actual spell persistence.
Stale/replaced/retired owners and changed saved modeled rows cannot overwrite the
file or publish success. Existing raw spell rows retain their opaque byte 11 when
the dance is appended; missing files retain real-session first-save behavior.
Review of the replacement found a late lookup gap; known row backing is now
rechecked without callbacks after the last SubProfInfo read. That review finding
is distinct from the original stale-save and padding defects.
Committed send failure retains learning; a retired publisher stops notifications.
The existing generic spell-file writer's uncertain-error boundary is unchanged.

Tests use owned files and genuine Store/loopback owners; Performer frame checks use
the existing test connection capture, not proof of live encrypted delivery.
These guarantees concern components/managed admission. Ordinary native MySpells,
physical Equipment, rank activation and the reverse native/session spell join
are still open. FullAccount, skill-book/rune transactions, raw external writers,
SQL ownership, delivery, live client behavior and whole-login crash safety are
not certified by focused results.

Contracts: [physical identity](protocol/specs/visible-dx8-d13-ordinary-native-hydration.md),
[rank persistence](protocol/specs/visible-dx8-d13-ninja-rank-persistence.md),
[Performer](protocol/specs/visible-dx8-d13-performer-durable-learning.md) and
[ordinary spell coherence](protocol/specs/visible-dx8-d13-ordinary-spell-coherence.md).
Root alone reviews, integrates, verifies, stages and publishes this checkpoint.
