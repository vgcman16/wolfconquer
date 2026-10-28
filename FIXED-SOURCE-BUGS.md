# Source bug fixes and audit

Engineering policy: [Sanitized Parity](docs/engineering/SANITIZED-PARITY.md).
Started 2026-10-23. **Open items below are not fixed or verified claims.**
Earlier passing tests may encode defects; test totals are not a safety certificate.

## 2026-10-28 operator transport and testing tools - focused verification

### FSB-180: Keep an operator response open until a matching peer receipt

Actual publica62bb717 / Windows40781201208 failed only the operator pipe's
second, duplicate-operation response-body read with EndOfStreamException.
Core29334passed/1failed/164existing skips; Updater446passed. Restore/build
passed; launcher/parsers were not reached. The FSB179 StarVolcano case passed.

The server wrote/flushed then disconnected without a peer-consumption boundary.
That is a supported missing lifetime contract consistent with the EOF, not proof
of the sole captured Windows cause. Transport revision2 prepares one bounded
response, then reads exact WCM2+returned-operation-GUID receipt before normal
disconnect under the SAME existing10s token. The actual DataTool acknowledges
only a completely read, strictly parsed response matching its expected GUID.
Storage reservation/journal revision1, gameplay packets and no-regrant behavior
are unchanged; wrong/missing receipts retain unknown transport outcome without
rollback, automatic retry or a new grant. No blocking WaitForPipeDrain is used.

Root's isolated actuala62-based batch builds0errors, parses7 selected PS files,
and passes60/60 focused cases with0fail/skip. Controls include actual DataTool
first/same-operation commands against the real owned endpoint, delayed/fragmented
body consumption, wrong-operation receipt, actual worker shutdown join and saved
UID/mail conservation. The first59/60 run found a status-table dictionary display
bug, corrected without weakening its assertion. Results are retained locally.
The new full Windows gate and actual game-client hot mail/claim/equip acceptance
remain pending. This is not SQL, power-loss, full English/parity or deployment
proof; the older qualified live stack is preserved.


## 2026-10-28 StarVolcano fixture correction - focused verification

### FSB-179: Drive repeat/expiry tests through real workers with controlled time

Full public8e84946e / Windows40771334759 failed the single StarVolcano
repeat/expiry case (Core29,334 passed, one failure,164 existing skips; Updater446
passed). Launcher/parsers were not reached. The fixture used a real advancing
600ms lifetime and80ms repeat; the actual consumer checks expiry before pulses,
so delayed scheduling can remove before two hits. The specific full-run timeout
cause remains unproven; no production timer defect is attributed.

Only the failing test moved to a focused partial. Its existing manual
TimeProvider dispatches real scheduled workers at virtual10,90 and600ms;
assertions retain two damaging Hit pulses, no early removal, exactly one original
RemoveEffect/item ID and removed floor state. All observations share the original
three-second bound. Synchronized frames/logs and captured/current worker joins
retain primary/cleanup errors, the existing five-second cleanup bound and drained
timers/fault checks. No production source, packet, formula, timeout, exclusion,
blanket serialization or neighboring test changed.

Release build: zero errors. Affected/neighbors selection15/15 passed without
failures/skips; three separate repeat runs of the repaired case each passed1/1.
These overlap and are not additive coverage. Full successor CI and actual-client
tools acceptance remain pending. Actual failed run evidence remains retained.


## 2026-10-28 latest full and local boundary

Exact public `be2740aea8f3295b57901fcd64d58a5e9cfc11c8` passed Windows
[40742984664](https://github.com/vgcman16/wolfconquer/actions/runs/40742984664),
all eight steps: Core 29,280 passes/164 existing skips, Updater 446, Launcher 57,
and synthetic parser cases 20+13 with no network probes. The unrebuilt runner
Release build also received the bounded owned local file-backed login, movement,
graceful World restart and fresh-login observation described in
[the live checkpoint](docs/LIVE-CLIENT-VALIDATION-20261028.md). The copied
client exited with code 18; this is not a clean-exit or crash-free claim.
SQL, crash durability, complete parity and full English remain unproven. A
subsequent private one-file resource trial displayed the native title
`WolfConquer` and entered Twin City after login; it is not a complete client
rebrand or a distributed asset update. Equipment/inventory presentation and ten
top-level forge categories were observed; real gear transactions remain unproven.
Subclass mojibake and Chinese labels remain. A requested local operator mail
sender is under development, outside this qualified build.

## Historical 2026-10-28 CrackStar fixture follow-up

### FSB-177: Observe runtime expiry completion and drain the test workers

This is a replacement test-fixture defect, not an attributed decompiled gameplay
bug. LegacyCrackStarAttachStatusRuntimeTests.cs waited for holder mutation, which
precedes the actual status send/log, then canceled without joining worker Tasks
before disposing its writer. It also depended on a real timer's dispatch within
a one-second polling deadline. Full public9769a9a8 failed that wait; the unchanged
test passed one isolated rerun. The original full-run timeout cause is unproven.

The test now uses the existing manual TimeProvider to observe both actual worker
timer registrations, checks unchanged seeded state, and advances their real
Task.Delay callbacks by10ms. Its synchronized log observer handles the actual
sync/async writer path and signals after the real expiry send. Packet assertions
use a locked snapshot. Cleanup cancels and joins the actual captured/current
worker Tasks, retains primary and cleanup failures, and checks drained timers.
The one-second behavior bound and status/wire assertions remain; no production
handler, packet, owner, timeout, exclusion or suppression was changed.

The first new fixture attempt exposed an async-only log-observer mistake despite
the actual expiry line being written; it was corrected before qualification.
Release build has zero errors; the final affected/surrounding selection passes
46/46, no failures/skips. Its successor full gate and bounded local observation
are recorded above. This does not prove the original timeout cause or universal
runtime safety.

## Historical 2026-10-28 observer follow-up - focused verification

### FSB-176: Preserve DragonSkin's published observer failure

The replacement implementation's DragonSkin presentation path caught the typed
`WorldSessionPublicationException` as a preparation `InvalidOperationException`,
then used mutable current ownership to return an unproven/refused outcome after
publication. This is an implementation regression after FSB-169, not a newly
attributed defect in the decompiled reference.

The handler now retains the original typed stage/cause/historical session, blocks
only its captured real Recovery when present and matching the Store/connection,
and rethrows the same exception. Secondary fencing failures remain inspectable;
an unexpected untyped assigned-final failure preserves both errors if fencing
also fails. No owner is fabricated, newer issuance erased, entitlement refunded,
or post-fault success output sent. Ordinary observers remain outside Store gates.

All six new fault cases failed against unchanged public2811f796 because the
typed outcome was swallowed. The repaired selection passed207/207 with zero
failures/skips, including the full DragonSkin family, real worker-task joins,
observer retirement, Store/Frame publication and owned-runtime lifetime controls.
The last full2811f796 run failed7 cases; this successor still needs its own full
gate. No SQL, crash durability, bound-native/live-client or full-parity claim.
See [the contract](docs/engineering/DRAGON-SKIN-PUBLICATION-OUTCOMES.md).

## Historical 2026-10-28 integration regression repairs - focused verification

The isolated exact-public4d9280fe repair candidate built with zero errors. Its
final regression selection passed520/520; the wider combat, native dispatch,
trap, Labyrinth, DarkHorn and boss-reward selection passed3557/3557. Both ended0
with no skipped cases; the selections overlap and are not additive coverage.
The full public4d9280fe run failed444 core tests. This successor still requires
its own full Windows CI and staged live acceptance. Scope and limits are in
[the repair checkpoint](docs/CI-REGRESSION-REPAIR-20261028.md).

### FSB-165: Validate the complete proficiency map before CheckProf
Reject null/foreign/malformed rows explicitly under the real owner gate instead
of treating corrupt state as a missing key. Preserve full-width native levels.
Null-client spell tests now require the existing explicit ArgumentNullException.
No live-client or SQL claim.

### FSB-166: Retain legitimate unbound training rows and skip unused owners
Preflight complete spell/proficiency inputs and output frames before scalar
publication, retain existing producer-held rows/maps when membership is unchanged,
and avoid inspecting an unused proficiency owner. Bound canonical publication
remains separate. This proves no new proficiency-file or full cast transaction.

### FSB-167: Preflight Pirate children before the first spell mutation
Read the whole required child list/table and reject zero IDs before any Jade
spell prefix. Valid ordering and repeated additions remain. Later sequential
native writer/transport failures are not a globally atomic batch.

### FSB-168: Reject duplicate paid learning before debit
Ninja/Monk epic learning uses the actual Character+Spells journal and validates
duplicates before the2150CP debit. A missing required Monk item route refuses
before grants. Real-root WhirlwindKick UID reservation/joint item+spell creation
remains OPEN; the missing-route guard does not close that reward transaction.

### FSB-169: Preserve published outcomes across notification failures
Concrete Frame persistence retains its actual current Store gate; external
observers run after their publication scopes. Typed stage/cause/issued identity
separates historical publication from preparation rejection. Seven NPC rollback
sites and free/paid/item spell writers suppress compensation/success and fence
actual recovery after a published failure. Secondary fencing errors remain
inspectable. A new holder may share a blocked world Recovery. Historical memory
publication does not prove every sidecar save or global crash-safe atomicity.

### FSB-170: Keep HP and its saved-field projection coherent
On actual HP changes, prepare the scalar saver's complete field map and apply
the existing EpicTrojan composite-to-runtime projection. Preserve opaque fields,
existing composite slots and no-change semantics; keep strict file comparison.
Other scalar producers and their save-failure semantics remain separate work.

### FSB-171: Preserve valid physical Rune single-unit encodings
A physical Rune slot accepts native raw StackSize0 or1 without rewriting that
field and refuses larger stacks pending a separate split transaction. Ownership,
UID/slot/class/level/lock and journal guards remain. No general zero-stack rule.

### FSB-172: Retain actual recovery owners in runtime equipment queries
Propagate the Frame's real Trade/Recovery objects into its runtime context so
rank/Venom spell persistence can complete before later stats/startup output.
Missing/blocked owners still refuse with diagnostics. This is source/runtime
context alignment, not a new client-binary or full login acceptance claim.

Fixture-only corrections are not additional source-bug claims: complete saved
Character fixtures, real loader array representations, nonoverlapping saved
roots, correct mutation options and correct preparation/observer phase controls.

### FSB-173: Accept valid raw item ordering without weakening the journal
The loader sorts Rune/child data in memory while ordinary admission retains the
original raw saved image. Compare the validated parsed projection and complete
physical item/deposit/perfection records by identity and parent, permitting only
representable order differences. Preserve exact original bytes for the journal
and its final before-image check. Unsupported opaque representations still refuse;
a direct checker test is not a live concurrent-writer interposition.

### FSB-174: Publish converted8418 EagleEye countdown before spell progression
The countdown produced a new record that had not entered the Store, so the exact
current-owner spell writer refused legitimate XP. Capture the genuine original
issuance before callbacks, publish countdown through exact identity+record CAS,
and use its actual final record. Stale/reissued owners suppress continuation.
This is scoped to the converted Monster8418 path, not every retaliation/XP caller.

### FSB-175: Carry the actual Shock caster through retaliation and spell saving
Retain the original issued identity and physical owner before admission awaits.
The extra-boss magic retaliation path publishes HP by exact CAS and returns the
actual canonical record to spell progression. The real free writer validates
that original issuer and physical pins at capture and retains them through its
existing commit checks. Preserve committed progress on later delivery loss.
Verified controls cover actual HP4999/EXP160/saved level1, same-UID and same-record
reissue, a legitimate same-issuance change and postcommit sender loss. Other
retaliation branches, nested helpers, proficiency/final-animation internals,
whole-cast atomicity, SQL and live client acceptance remain OPEN.

## Native spell/proficiency integration — focused qualification

2026-10-28: isolated clean-public-base candidate **2405 passed, zero failures,
two existing skipped teleport cases**. Final opened-file refinement **81 passed,
zero failures/skips**. These checks overlap; full CI and live acceptance remain
required. See [scope](docs/NATIVE-SPELL-PROFICIENCY-INTEGRATION.md).

### FSB-159: Managed/native spell owners diverged and retired storage regained writers

**Source:** replacement WorldSessionStore and native Spell/Proficiency producers.
**Correction:** actual ordinary-admission binding, immutable canonical snapshots,
retained retirement pins, Store-first actor gates, original/candidate raw-map
proofs and actual final records. Committed conflicts fence real Recovery. Genuine
prefix UID0 is retained until journal commitment; canonical login frames are
prepared before the writer and preserve their original bytes.
**Verification:** shared selection includes58 ordinary native coherence cases.
**Boundary:** not universal protection from uncooperative raw writes, SQL,
complete native SaveClient, transport delivery or client rendering proof.

### FSB-160: Spell costs and inventory changes used separate persistence phases

**Source:** replacement paid learning, skill books/runes and other spell writers.
**Defect:** funds/items and spells could commit independently; a native save could
overwrite newer disk state or discard legitimate unsaved memory changes.
**Correction:** real v8 Character+Spells and v9 Character+Items+Spells journals,
validated complete inputs/images/frames, retained byte11, current ownership and
a separate last-proven saved image advanced only by an actual successful writer.
Generic post-memory save failure remains an honest unsaved state.
**Verification:**9 paid-handler,19 admitted caller and49 direct v9/domain/path
cases are included in the shared selection. **Boundary:** interruption fixtures
do not prove power-loss/multiple-process/SQL atomicity; unsupported item images
are refused. Whole cast and promotional transactions remain separate work.

### FSB-161: Proficiency files admitted short records and dirtied rejected saves

**Source:** native LoadClientProfs ignored read results/reused its row buffer;
SaveClientProfs wrote a count before discovering a null row. Older tests enforced
default/reused-row admission and partial dirty output.
**Correction:** bounded exact-length20-byte records, nonzero unique IDs, genuine
physical owners and prepared map merge. Rooted save prepares its complete image
before real atomic replacement and revalidates its configured path. Opened save
validates before writing, seeks0, writes the full image, sets length and flushes.
**Verification:**81 final focused cases include real file/ABI/cursor/length,
malformed-input, before-image and path controls. **Boundary:** opened I/O failures
after writing starts have no rollback guarantee. Bound native proficiency file
I/O remains unavailable without a real configured saved-image participant.

### FSB-162: Proficiency arithmetic wrapped and compound updates had dirty prefixes

**Source:** native proficiency/UpdateSpell unsigned gains and ordered mutations.
**Correction:** widened arithmetic and complete spell/proficiency maps and frames
prepared before either publication. Valid two-pass/message order remains.
Null owners and malformed/negative inputs reject without dirty output; earlier
bug-preserving tests now enforce safe invariants and positive numerical/goldens.
**Verification:**2405 selection plus81 overlapping final focused cases, including
independently calculated widened remainders. **Boundary:** every progression
rule, wider cast transactions and live gameplay remain unproven.

## FullAccount class-reset transaction — focused qualification

### FSB-158: Class reset saved before current ownership and erased untouched spell bytes

**Source:** the managed replacement's AccountSecurity.FullAccount.Class.cs and
.Class.Persistence.cs. These are not automatically original Rayzo defect claims.

**Defect:** two files were replaced before unconditional Store.Set, allowing a
stale caller to replace a newer holder. Saved spell progress was not reconciled;
unknown byte11 of retained rows was zeroed. Sender/serialization faults could
follow disk changes, and in-process rollback had no persistent restart plan.

**Correction:** real expected-session/trade/PIN/file ownership; complete prepared
inputs, frames and images; unchanged padding; strict distinct v8 Users+Spells
journal/recovery; current metadata and final cache proof; truthful committed/
recovery-required outcomes. The existing11 grants/positive wire order remain.

**Verification:**354/354 public and354/354 private, same exact names, zero failures/
skips; all52 baseline names retained. Old source with corrected fixture:39passed/
13failed. Earlier37passed/15fixture-setup failures are retained and do not count
as defects. The two final equivalent assertion-cleanup cases pass2/2 publicly;
the private354 includes that final assertion. See
[scope and limits](docs/FULLACCOUNT-CLASS-TRANSACTION.md).

**Boundary:** direct distinct-key fixtures use the actual full parser and preserve
the ordinary loader's account=UID requirement. Fresh-owner journal fixtures do not
prove OS crash/power-loss, multi-process or SQL safety. Physical native spell/
equipment adoption, other producers and live acceptance remain open. Exact public `224f1eb5` passed full run `40646084998`: Core 29,039 Passed /
164 existing NotExecuted, Updater 434, Launcher 57, synthetic parsers 20+13;
all eight steps exited 0. This remains source evidence within the stated limits.

## Ordinary physical identity and persistence — focused qualification

FSB-154–157 share the same focused batch qualification; these are not four
independent runs. Public corrected: **524/524 passed, zero failures/skips**.
Private corrected: **524/524 passed, zero failures/skips**. Exact-public full
Windows40569779073 on source8e8ff83f completed SUCCESS: Core28,974 passed/164
existing NotExecuted, Updater434, Launcher57, synthetic receipt parsers20+13;
all eight steps exited0. This full result predates FullAccount v8, whose separate successful full gate is recorded in FSB-158 above.
See [scope and limits](docs/ORDINARY-PLAYER-PERSISTENCE.md).

The tests-only baseline was 508: 102 failed/406 passed/zero skipped. Three actual
PowerShell 7 prerequisite reruns passed 3/3; one aborted high-login cleanup rerun
passed 1/1. The remaining 98 intended differences are rank 75, physical identity 15
and Performer 8. Preserve the original receipt and these distinct rerun outcomes.
Prior full 40555473262 on 9e779d5d failed one stale null-item frame assertion;
Core 28,861 passed/164 NotExecuted, Updater 434 passed, launcher/parsers not reached.
That null control now expects ArgumentNullException and complete storage retention;
valid wire goldens remain. No SQL/live/client/crash-safe completion is claimed.

### FSB-154: Ordinary prefix omitted saved physical Class and Level

**Source:** `LegacyNativePlayerRuntime.ExistingPrefixLoad.cs`,
`LegacyNativeExistingPrefixBefore.cs`, `LegacyNativeExistingCharacterPrefix.Admission.cs`
and ordinary admission validation/preparation.

**Defect:** admission could publish a physical Player with unloaded class/level;
fresh admission also accepted a level too wide for its later LoaderHeroInfo byte.

**Correction:** prepare/check complete before/after values, publish saved
physical widths only after the actual committed prefix, preserve transformation
and reciprocal owners, and fence the full published image. Apply the byte limit
only to fresh emitting admission; preserve direct ushort and retained journal domains.

**Verification:** 524/524 public and 524/524 private, same shared selection; genuine ordinary/direct-prefix,
callback/change/refusal and owner controls. Original Level's 150 clamp/reward
transition, managed XP cap 140 and saved-level policy remain distinct/unresolved.
No global level-cap normalization or complete native owner adoption is claimed.

### FSB-155: Ninja rank Load published incomplete or defaulted state

**Source:** `LegacyNativeNinjaRank.Persistence.cs`, `.Persistence.Input.cs` and
`.Persistence.Parse.cs`.

**Defect:** destructive/incremental loading and tolerant fields could replace a
valid graph with partial, malformed or invented values.

**Correction:** read a bounded strictly decoded complete file, validate
all rows/counts/UID/category/width/text state, prepare six genuine detached owners,
then recheck the current owner/location and publish once. Missing/unreadable input
propagates its real failure; invalid content cannot authorize empty defaults.

**Verification:** 524/524 public and 524/524 private, same shared selection; malformed tails/decoding/counts,
owner/gate changes, exact valid rows, empty/tie/cap and escaped-graph controls.
This prepares persistence; ordinary host rank activation remains separate.

### FSB-156: Ninja rank Save accepted stale graphs and wrote targets in place

**Source:** `LegacyNativeNinjaRank.Persistence.cs` / `.Persistence.Save.cs`.

**Defect:** Save could use a stale cached registry array and accept incomplete or
corrupt graphs. It wrote the target in place, exposing it to I/O failures, without
cooperating capture-to-write ordering. Old Save queued row serialization before
Execute; a serialization fault did not necessarily mutate the destination.

**Correction:** validate/capture the complete current six-owner graph,
prepare exact serialized bytes, retain the real target-path coordinator through
capture/write, flush/verify a same-directory stage and replace one file only after
preparation. Preserve the literal path, successful layout and real ranking rules.

**Verification:** 524/524 public and 524/524 private, same shared selection; malformed graph/text/owners and
unchanged-target controls, valid serialization and real gate ordering. Failures
before replacement retain the target; post-replacement uncertainty, raw writers,
multi-file/SQL transactions and process/power-loss durability remain outside it.

### FSB-157: Performer learning saved a stale session and erased opaque row bytes

**Source:** `WorldPacketHandlers.NpcEntrypoints.Performer.Dance*.cs` and extracted
method in `WorldPacketHandlers.NpcEntrypoints.SubclassMasters.cs`.

**Defect:** saving before unconditional Set could overwrite newer membership;
modeled reserialization zeroed existing opaque spell byte 11. During review of
the proposed replacement, a late InputsCurrent attribute lookup was also found
able to alter row backing after its earlier check. That replacement gap is not
claimed as a demonstrated original emulator mechanism.

**Correction:** prepare stable rows/seven genuine frames, commit actual
persistence through expected-session TryCommitSession, reconcile modeled disk
membership and append while preserving existing raw bytes. Recheck known backing
without callbacks after the final attribute read; reject unsupported shapes.
Preserve eight dance IDs, phase/duplicate dialogs and valid sequence bytes.

**Verification:** 524/524 public and 524/524 private, same shared selection; stale/retired/foreign Store contexts,
real file failure, padding/conflict, late lookup and repeated-learning controls.
Committed delivery failure does not roll back learning. Generic writer uncertain
errors, other learning producers and native/session spell authority remain open.

## Ninja and spell component safety

FSB-147–153 share one focused public qualification: **1,082 passed, zero failures
or skips**, comprising 1,067 Ninja/caller cases plus 15 LocalDevelopmentTools cases.
Private v2 passed the same **1,067 common Ninja cases**, zero failures/skips.
All 814 public baseline names remain; that tests-only baseline was 279 failed /
535 passed / zero skipped. These are shared suites, not seven separate runs.
See [qualification and limits](docs/NINJA-SPELL-SAFETY.md).

The initial private compilation lacked the configured ignored MySql dependency;
the existing original DLL was restored only into its owned snapshot before v2.
The prior full `4b9762cc` check failed only two stale plan-argument assertions;
its actual failed receipt remains. The new composed source needs a new full gate.
No live/SQL/crash-safety or host-adoption claim is made by these entries.

### FSB-147: Ninja rank refresh and AddItem published partial or invalid state

**Source:** `LegacyNativeNinja.Ranking.*`, `AddItem*`,
`LegacyNativeNinjaRank.Refresh*` and `LegacyNativeMsgNinja.ItemCodec.cs`.

**Defect:** Wind used Overall category; stale low/ineligible memberships remained.
Unchecked scores, malformed item identities/appearance and late rank faults could
follow item stores or notices. The writer checked storage too late.

**Correction:** validate all six real rank owners/tails and canonical item/metadata
identities; prepare checked scores, all affected maps, candidate item state and
bounded exact bytes before stores. Preserve legitimate thresholds/ties/caps and
old escaped graphs. Final plain fences follow comparer-capable appearance reads.

**Verification:** shared suites above include genuine refresh, AddItem, comparer,
late-fault and independent wire controls. Tiny unsafe old writers were not run.
Standalone rank persistence/mutation APIs and outer costs remain separate.

### FSB-148: Loading and typed spell mutations retained dirty failure prefixes

**Source:** `LegacyNativeNinja.Loading*`, `Alternate.cs`, `GetLevel.cs`,
`LegacyNativeSpellStorage*` and prepared spell/user/action/update frame helpers.

**Defect:** later owner/metadata/frame/comparer failures could leave earlier ranks,
skills or badge updates; sequential map writes and native encoders could fail
after publication or write beyond actual capacity.

**Correction:** prepare complete real maps, badge and ordered frames; retain and
check actual comparers against raw expected identities. Reject typed recursive or
inverted entry before actor locks. Admit all real storage before commit/copy and
publish only committed outcomes, skipping stale notifications.

**Verification:** shared suites cover original identities/outputs on refusal,
real comparer reentry, successful sequence goldens and corrected-only bounds.
Raw external writers, recycler retention and delivery/durability remain unproved.

### FSB-149: Direct spell writers could modify escaped copy-on-write rows

**Source:** `LegacyNativeMsgSpell.Handler.cs` / `SoulPreparation.cs`,
`LegacyNativeUpdateSpell*`, `SpellStorage.DirectWriters.cs` and proficiency ownership.

**Defect:** soul/XP writers could resolve a row before acquiring its actual gate
or continue using it after legitimate replacement; late frame/owner faults could
follow a store.

**Correction:** capture current physical rows under the real spell gate, prepare
responses/progress and revalidate bindings/metadata/rows after callouts before
plain stores. Require actual equipment/proficiency owners only when used.

**Verification:** shared current-row, owner, reentry and byte controls above.
Earlier proficiency effects are not rolled back. XP checks are not universal:
capped/rune/level-gated paths and full proficiency transactions remain open.

### FSB-150: Spell files accepted incomplete rows and incoherent save graphs

**Source:** `LegacyNativeServerDatabasePersistence.Spells*` and
`LegacyNativeItemRecordFile.SpellLength.cs`.

**Defect:** ignored read results invented spell zero or reused preceding fields;
incremental publication, missing required metadata and count/row mismatch could
leave partial graphs. Save could truncate before required graph validation.

**Correction:** read/close a complete bounded image, validate required known-spell
and rune metadata, prepare the merged current map and publish once. Preserve
existing/duplicate-first and actual rune-denial policy. Snapshot valid save rows
before public create/truncate; propagate incomplete writes and close handles.

**Verification:** shared malformed/read/metadata/comparer/closure/save controls.
Unknown saved IDs remain opaque membership, not cast permission. IO may still
leave a partial file; opened-file callers may already have truncated it. No
recoverable replacement, SQL or whole-login transaction is claimed.

### FSB-151: Flags and Points could publish before notice admission or lose credits

**Source:** `LegacyNativeNinja.Flags.cs` / `FlagPreparation.cs` and
`Points.cs` / `PointPreparation.cs`.

**Defect:** flags could change before owner/frame failure. Points read/checks
preceded a separately locked setter, allowing concurrent credits to overwrite.

**Correction:** prepare complete state/frame under genuine actor/persistence
gates, admit real storage, then copy and publish one aggregate. Preserve any-overlap
flag no-ops, AddFlag(0), zero-credit notices and full widths; check point addition.

**Verification:** shared full-width/zero/refusal and corrected-only storage cases;
real joined workers require final 79 from 71+3+5 and unordered valid notices.
Delivery, concurrent notification order and paid outer spending remain open.

### FSB-152: Status applied rank bonuses before required spell refusal

**Source:** `LegacyNativeMyCombatGear.Status.cs` / `StatusRankBonus.cs`.

**Defect:** six bonus calls changed status/perfection before a required spell
owner/map/frame check; callouts could replace the later write targets.

**Correction:** validate intended spell ownership and prepare the exact seven
scalar results against captured real targets. Apply after the spell call only
while bindings/values remain current; otherwise stop later Status work without
misreporting a committed spell as an uncommitted rejection.

**Verification:** shared malformed/foreign storage, real target-change comparer,
500/500/300 arithmetic and literal removal-frame controls. Later Status and other
CombatGear/Pirate/HundredWeapons outer prefixes are not certified.

### FSB-153: Training trusted a consumed metadata key over the actual row identity

**Source:** `LegacyNativeUpdateSpell.DirectPreparation.cs` / `DirectCommit.cs`.

**Defect:** a keyed row declaring another spell ID or level could supply training
rules; a later callout could change those identities before progress publication.

**Correction:** when consumed, require declared ID/level to match the selected
spell/captured level before either proficiency pass and recheck after callouts.
Missing/null/mismatched consumed metadata retains the fail-soft return.

**Verification:** shared actual metadata/proficiency caller and independent wire
controls. Count-based early-cap behavior, unused/next-level metadata, previous-level
restoration and prior proficiency side effects remain explicit open boundaries.

## CI regression-control follow-up

FSB-145's1057 full-suite test still required the old missing-grant bug; it now
expects the exact1110 level0 record and retains all stat/save/repeat assertions.
The poisoned-runtime fixture's wall-clock/log race and unjoined worker cleanup
are replaced by real timer/rearm and joined-worker controls. Public focused47Core
and3Updater cases pass. No production expiry defect or full-suite pass is claimed.
See [CI qualification](docs/CI-QUALIFICATION-20261027.md) for actual failed counts,
retained process/updater diagnostic limits and pending full scheduling verification.

## Current opt-in world diagnostics

### FSB-146: Default world frame diagnostics exposed raw payload prefixes

**Source:** replacement `WorldPacketHandlers.Diagnostics.cs` and
`WorldServerFrameHandler.Diagnostics.cs`, including the pre-session exception
fallback. This is a replacement-server diagnostic defect, not a claimed
client-confirmed game rule or newly discovered decompiled behavior.

**Defect:** unknown/malformed packet logs sampled up to 32 payload bytes by
default; the old PIN-only exception did not protect other arbitrary payloads.

**Correction:** both ordinary formatters emit only opcode and payload length.
The separate logger defaults off, creates an exclusive private output, and
bounds records, bytes, rate, signatures, fields and capture lengths. Structural
scanning and raw prefixes each require an explicit matching-profile allowlist;
raw also requires the actual current admitted session. Auth/PIN/text families
are excluded. It never grants an operation or produces a gameplay response.

**Verification:** public focused 229/229 passed, zero failures/skips. Every one
of the 138 baseline cases is retained; baseline16 Failed/122 Passed. Ninety-one
additional cases exercise the real new API, file ownership/failure, bounded
scanning and genuine-session raw gating/dedupe. Private focused qualification also passed 229/229, zero failures/skips.
The new exact-source full gate remains pending. A subsequent mechanical ACL-test loop
cleanup retains the same assertions; it awaits that full gate.

**Limits:** no live capture, runtime restart, gameplay activation, reliable log
delivery or complete logging/privacy audit. Write failure can leave a partial
last line; synchronous filesystem latency remains. Earlier stored logs and
other diagnostics are not retroactively sanitized. See
[operator guidance](docs/WORLD-WIRE-DIAGNOSTICS.md).

## Current target-class qualification

Nine production files and37 test files correct confirmed target stages52–57 in
shared class/stat selection, HP/mana, guild permission queries, configured boss
package rewards and Trojan-lineage login spells (FSB-141–145). Public and private
focused selections each pass **2,006/2,006**, zero failures/skips. Every exact
public baseline name is retained; that baseline was335 failures/1,671 passes.
See [the qualification guide](docs/JOB-FAMILY-TARGET-QUALIFICATION.md) for actual
consumer coverage, retained-journal compatibility and incomplete runtime limits.

Prior exact source252bb950 passed Windows40501867920: core27,926 passed/164 existing
NotExecuted, updater434, launcher57; all8 steps exit0, synthetic parsers20+13.
The new46-path C# batch still needs its own full gate. No deployment, live-client
acceptance, full English, SQL ownership, distribution or full-parity claim follows.

### FSB-141: Recognize confirmed target stages in shared families and stat selection

**Source:** `LegacyNativeAtributesStatus.JobFamilies.cs` and
`LegacyNativeAtributesStatus.State.cs` PositionArray.

**Defect:** family cutoffs at suffix 51 rejected confirmed target stages 52–57;
duplicated stat selectors could route them to fallback despite corrected callers.

**Correction:** extend the eleven existing families through confirmed suffix 57
and reuse their predicates in the original stat-selection order. Preserve row
keys, fallback, broad Taoist compatibility and the separate Lee 8049 gate. No
mapping or permission is invented for unmodeled jobs.

**Verification:** shared public baseline 335 failed/1,671 passed/0 skipped;
corrected 2,006/2,006, retaining every exact baseline name. Private selection40466 also passed2,006/2,006, zero failures/skips. This shared result also supports FSB-142–145; it is not five separate
2,006-case runs. State table APIs have direct coverage, without a production
adoption claim. Other direct bounds and the byte-class InfoStr defect remain open.

### FSB-142: Apply existing HP and Mana formulas to confirmed target stages

**Source:** physical `CalculateHitPoint.cs`/`CalculateMana.cs`, ordinary
`LegacyFirstCharacterStarterProfiles.cs` and `LegacyEquipmentNativeMana.cs`.

**Defect:** independent suffix-51 limits selected fallback HP/Mana contributions
for known later stages even when class-family admission succeeded.

**Correction:** extend only the confirmed Trojan HP and Water/Fire mana bounds.
Preserve coefficients, truncation, UInt16 physical/class contributions, ordinary
long arithmetic and packet fields. Equipment's Pirate bound is unchanged.

**Verification:** the shared result in FSB-141 includes genuine managed
RefreshVitals and EquipmentQuery.Prefix consumers plus physical calculator
controls and suffix-58 fallback. Private selection40466 also passed2,006/2,006, zero failures/skips. Whole level-up,
equipment rebuild, overflow-policy changes and native pipeline adoption are not
claimed.

### FSB-143: Honor existing guild class permissions through target stage 57

**Source:** `LegacyNativeGuildRuntime.AdmissionPolicy.cs` Profession.

**Defect:** a guild carrying the correct existing class bit still rejected
confirmed stages 52–57 because the classifier stopped at 51.

**Correction:** extend the eleven known bands only. Preserve the flag enum,
Full mask, missing-Info/missing-bit refusals, funds and membership/success gates.
World-only, unmodeled and unknown inputs receive no recruitment permission.

**Verification:** the shared public result includes genuine loaded-guild API
controls and unchanged owner/Info/funds/collections. Private selection40466 also passed2,006/2,006, zero failures/skips.
No clean-baseline managed production Profession caller was found; this is a
permission-query fix, not guild-join or economy qualification. Held Guild/Union
work is outside the change.

### FSB-144: Select existing boss-package rewards for confirmed target stages

**Source:** `LegacyBossHarmRankPackagePolicy.cs` GetJobFromClassCode, consumed by
ClaimPlan, PackageCatalog and PackageNative.

**Defect:** known later Trojan/Warrior/Archer/Ninja/Pirate stages became Other;
Ninja/Pirate claims could omit their configured points/native projection.

**Correction:** extend those five configured bands through 57. Preserve every
amount, item, rank rule, capacity check and owner/commit gate. Pirate ranks 8/9
retain no job bonus; no reward family is invented for other classes.

**Verification:** the shared public result includes pure plans and genuine cold
Ninja/Pirate recovery, PIN, journal, reward and replay controls. Private selection40466 also passed2,006/2,006, zero failures/skips. Old v5 journals retain their pinned old result. Before-only retries
whose recomputed recipe differs refuse and retain debt; migration and completed
claim compensation remain open. See the [qualification guide](docs/JOB-FAMILY-TARGET-QUALIFICATION.md).

### FSB-145: Recognize target Trojan lineage in the existing login spell grant

**Source:** `LegacyClassOwnedSpellGrantProfile.cs` IsTrojanFamily and its existing
ordinary-admission, spell-image, normalization and startup consumers.

**Defect:** current/first/second Trojan stages 52–57 omitted the existing
missing-only Cyclone grant.

**Correction:** extend only the Trojan upper bound to 1057. Preserve lineage OR,
grant 1110, sequence, flags and saved spell bytes; no magic metadata is fabricated.

**Verification:** the shared public result includes real transactional ordinary
login, exact twelve-byte record preservation and repeat idempotence. Private selection40466 also passed2,006/2,006, zero failures/skips. Version-6/7 pending records may fail recomputed grant
validation; no journal migration is supplied. The older reachable best-effort
save helper can still return learned state after failed persistence. Whole grant
durability, CLI/startup adoption and live-client behavior remain open.

## Earlier class/reborn and D13 raw-codec qualification

FSB-139..140 correct stale reborn cleanup and prestige class resolution through
D13 suffix57. Public932/932 class/reborn/Map cases pass, including all843 baseline
names (previously290 failed/553 passed). Rawcodec27/27 and private combined959/959
pass, zero failures/skips. The actual D13 action6 row is113 bytes; its query stays
inactive pending an authoritative extra field, text conversion and lifecycle.
See [class/reborn](docs/PRESTIGE-CLASS-AND-REBORN.md) and
[wire contract](docs/D13-PRESTIGE-WIRE-CONTRACT.md).

Accepted prior source99447f89 passed Windows40489896224: core27,549 passed/
164 existing exclusions, updater434 and launcher57; all8 steps exit0. Receipt
20261027T103908463Z-08177f55 finished2026-10-27T11:24:44.8247513Z. The newer
31-path C# batch needs its own full gate and has not been deployed.

## Earlier retained-rank qualification and prior full source

Prestige refresh and standalone/global rank mutations now retire obsolete same-UID
memberships and prepare admitted dictionaries/champions before publication
(FSB-137..138). Public-derived843/843 focused checks pass, including every one of
the489 tests-only baseline names (previously133 failed/356 passed). The three
shorter startup messages preserve all11 frames. See [rank refresh](docs/PRESTIGE-RANK-REFRESH.md).

Accepted prior sourcee7d85b70063d698fad3ea15c77bbe441bf383850 passed
Windows40474236546: core27,404 passed/164 existing exclusions/0 failed,
updater434, launcher57, synthetic parsers20+13; all eight stages exit0.
Receipt20261027T083328942Z-73c6dd50 finished2026-10-27T09:16:49.7448393Z.
This newer rank/wording source needs its own full gate. Runtime adoption,
CheckReborn/remaining queries, crash recovery and live-client stability remain open.
## Historical client/effect focused qualification

Prestige client/effect preparation plus wardrobe-save ownership passed735/735
public-derived and740/740 private focused checks, zero failures/skips. Baselines
were35failed/207passed and two failed/six passed. FSB-135..136 record exact limits.
The prior loader/Save source42a1116b passed Windows40466127994: core27,367 passed/
164 existing exclusions, updater434, launcher57 and synthetic parsers20+13; all
eight stages exit0. This new source still needs its own exact-public full gate.
Older status sections retain their historical outcomes.

### FSB-140: Resolve prestige classes without granting Water fallback authority

**Source:** `LegacyNativePrestigeRanking.Queries.cs` GetIndex,
`ClassResolution.cs`, rebuild preparation/publication and the two Player.GetArray
badge consumers.

**Defects:** global family predicates stopped at suffix 51 although the selected
D13 primary contains suffixes 52–57 for all eleven existing prestige families.
Unrecognized classes defaulted to Water, giving unsupported IDs incorrect class
publication, badge lookup and destructive historical-cleanup authority.

**Correction:** a prestige-specific resolver covers the 634 confirmed IDs through
57. Strict GetIndex refuses nonclass/unsupported inputs; TryGetIndex supplies no
category. The 17 observed unmodeled IDs receive no invented prestige mapping.
Only exact 10000–10002 retains World-only behavior; 10002 is an absent-table
compatibility literal. Rebuild and publisher validate the real Class/category/
includeClass disposition before outputs. Spawn class badges use rank zero when
there is no class category. Unknown IDs cannot select Water for cleanup.

**Verification:** shared tests-only baseline: 290 failed/553 passed/0 skipped,
843 total. Root's corrected focused selection passed 932/932, zero failures/skips,
including every baseline case. Coverage includes target 51/52/57 boundaries,
unmodeled/current/prior refusals, genuine rebuild outputs, world-only controls
and both actual-UID badge paths. This is shared batch evidence with FSB-139,
not a separate 932-case result for each entry.

The combined private selection passed959/959, zero failures/skips; it includes
the same class/reborn coverage plus27 raw-codec cases. Source intake follows
Root's review of all31 C# paths. A new exact-source full gate remains required.

Global Atributes predicates, other class systems and loader/catalog admission
are unchanged. No old saved-row repair, new class category, packet-layout change,
live-client proof or exact-source full-CI acceptance is claimed.

### FSB-139: Prepare obsolete reborn-rank cleanup and preserve the current family

**Source:** `LegacyNativePrestigeRanking.Queries.cs` CheckReborn and the focused
`Reborn.cs` helper. Reference RebornInfomations stores historical/new classes,
refreshes equipment at 447 and calls CheckReborn at 448. Equipment's
QueryEquipment → OnDequeue → UpdateStats → CreatePrestigePoints chain can
publish a fresh current-family entry immediately before cleanup.

**Defects:** raw removal could delete that freshly rebuilt same-family entry,
mutate escaped Items before a later sort failed, retain a stale champion, and
consume a foreign Player or malformed selected owner without complete admission.

**Correction:** require the real physical Player and positive active UInt32 UID;
preserve genuine non-reborn early no-ops. Select FirstClass/SecoundeClass using
reborn 1/2. A prior World-only class has nothing to remove; known same-current-
family membership is preserved. Required unsupported prior/current IDs refuse. Obsolete
cleanup admits only the required selected owner and every matching positive
unique raw UID row, then prepares a new map and next/null champion before stores.
Recheck captured player/selection/owner identity under the cooperating gate.
Preserve escaped maps/entries, valid shared Items, caps, stable ties and genuine
absence; do not invent backfill or repair unrelated owners.

**Verification:** the shared 843-case baseline and corrected 932/932 result above
include genuine CreatePrestigePoints → CheckReborn preservation at class 1001 and
1052, selected malformed tails, aliases, current/prior class admission, full
UInt32 UID, cached-only target champions and unchanged client/rank outputs.

Private combined qualification passed959/959 as recorded above. No managed production rebirth caller is added.
The whole rebirth transaction, unrelated query/cache defects, arbitrary external
writers, crash recovery and exact-source full CI remain open.

### FSB-138: Prepare standalone and global prestige rank mutations before stores

**Source:** `LegacyNativePrestigeRanking.Rank.cs` AddItem/Remove,
`PreparedRanks.cs`, and only global Remove in `Queries.cs`.

**Defects:** AddItem/Remove changed escaped dictionaries before fallible sorting,
could leave an insertion/deletion prefix on malformed tails, and retained stale
champions. AddItem's finally could invoke a native diagnostic after dirty output.
Global removal changed earlier owners before a later owner failed. Low scores
could bypass the shared gate and complete owner admission.

**Correction:** admit complete physical owners and rows, including capped tails,
positive matching raw UID keys and usable caps. Detached ranks require no global
registry; global removal scans actual present owners, not cached GetValues.
Validate every affected global owner and prepare every dictionary/champion before
reference stores. Preserve escaped maps, Entry/point identities, stable ties,
positive configured caps and valid sparse World/class mirrors/shared maps. Refresh
the targeted champion even when old raw deletion left it absent from Items;
genuine absence remains a no-op without healing unrelated stale champions.
Nonpositive caps refuse without native logging; low and eligible operations both
require the real shared gate. No new Entry.Class/text/Points matching rule is added.

**Verification:** shared tests-only E7 baseline was 133 failed/356 passed/0 skipped
(489 total). The corrected public-derived selection passed 843/843, zero failures/
skips, including every one of those 489 baseline names. Tests retain exact threshold,
cap/tie/Entry aliases, detached and sparse owners, malformed late owners/tails,
historical champion-only state, immutable escaped maps, actual cache identity,
null-gate and owned native-diagnostic controls. Other ranking families' successful
removal/hash metrics and CheckReborn assertions remain unchanged.

This is retained-cache preparation safety under cooperating SynRoot users, not
an uncapped leaderboard, whole concurrent snapshot or crash/persistence transaction.
Create, CheckReborn and other query/diagnostic paths remain unchanged/open. The
current correction has no exact-source full CI yet; accepted E7 Windows40474236546
predates it. No dynamic immutable-catalog refresh, client or deployment proof.

### FSB-137: Retire obsolete prestige memberships during coherent refresh

**Source:** `LegacyNativePrestigeRanking.Publication.cs` and
`Publication.Registry.cs`, consumed by LegacyNativeGameClientRuntime.CreatePrestigePoints.

**Defects:** the below-20,000 early return retained an old eligible same-UID Entry
and champion after its score fell. Eligible class migration or the existing
World-only exclusion left the UID in its former class, exposing stale ranks and
potentially persisting the obsolete class row.

**Correction:** under SynRoot, admit the actual present registry before treating
absence as permission. Require World and an included target class; validate real
owners, caps, complete UID maps and cross-class uniqueness without inventing
missing buckets. At 20,000 or more, prepare World/current included class and remove
that UID from other present classes. Below threshold, prepare removal everywhere.
A target UID retained only by BestOfClass is still affected; select actual survivors
or null. Publish all prepared rank references before the caller commits client
points, Entry, physical perfection and genuine guild-member fields. Keep source
GetIndex/exclusion decisions, stable replacement ties and real Entry aliases.

**Verification:** the combined tests-only E7 baseline was 133 failed/356 passed/
0 skipped (489 total); corrected public-derived qualification passed 843/843,
zero failures/skips, and all 489 baseline names pass. Genuine client controls
cover exact 0/19,999 decline, next/null champions, eligible class and literal
10000..10002 World-only migration, sparse/shared-map mirrors, exact threshold,
stable ties/caps, no invented backfill and corrupt late-state refusal before any
client/rank/perfection/guild output changes. This is shared batch evidence, not
a separate 843-case count for each defect entry.

The immutable saved WorldDataCatalog.PrestigeRanks projection is separate and is
not dynamically refreshed here; GenericRanking/Viewer and other query/reborn paths
remain open. Discarded candidates cannot be backfilled from an uncapped owner
that does not exist. No new packet layout or class-eligibility rule, crash-safe
transaction, runtime adoption, live-client or full-parity claim is made. Current
source still needs its own full CI; accepted E7 Windows40474236546 is prior evidence.

### FSB-136: Require physical wardrobe ownership at item-save consumption

**Source:** ClientItemSave.SaveGraph.HasCurrentOwners and mutable Wardrobe.Owner.

**Defect:** a foreign-born or null-born wardrobe could be installed in the current
client slot and have public Owner rewritten, then be accepted for a real item save.

**Correction:** retain the actual wardrobe constructor binding and require it,
public Owner and the current client slot to match. The save graph consumes this
same physical-owner guard. Public Owner remains mutable for existing query APIs;
no placeholder owner or automatic adoption is introduced.

**Verification:** existing real-file authority theory expanded by two cases.
Before the graph guard, both incorrectly committed (two failed/six passed).
Both now reject with prior file/graph unchanged in public735/private740 selections.
Valid genuine wardrobe/equipment alias serialization remains covered. This does not
establish a whole inventory transaction or crash/hostile namespace safety.

### FSB-135: Prepare prestige and perfection results before publishing

**Source:** GameClientRuntime.CreatePrestigePoints and PerfectionEffect.Update.

**Defects:** failed calculations could reset or partially replace client points,
Entry, physical effects or guild totals before a required owner or rank failed.
Unchecked arithmetic/narrowing could publish wrapped scores. Foreign optional UID
owners, rewritten wardrobe owners and unregistered guild members could contribute.
Malformed saved-row text or out-of-range Chi values could also be published.

**Correction:** prepare real component scores, total, Entry/point copy and effects
privately; validate physical constructor/slot/UID ownership, configured identity,
actual guild registration, complete equipment and required metadata. Recheck captured
owners and equipment values, publish prepared ranks under the shared gate, then
apply plain field stores to client, the existing physical effect and genuine member.
Foreign-server calculations still avoid rank/gate consultation. Checked arithmetic,
actual Chi bounds and strict text admission reject invalid data before publication.
Standalone effect Update also prepares before changing its existing sixteen fields.

**Verification:** tests-only old42a baseline35failed/207passed/zero skipped,242total.
First correction733/733; final coherent public735/735 and private740/740 pass with
zero failures/skips (five extra private compact-English controls). All242 baseline
names and eight item-save authority rows pass. Includes late-world failures, real
owner/guild mismatches, score overflow,39/44-slot valid arrays versus38 rejection,
Chi bounds, Unicode, effect identity and valid source formulas/thresholds.

This is managed preparation/rejection safety, not runtime adoption, a complete
concurrent snapshot, atomic observation of all sixteen fields, crash/database
transactions, deployment, client rendering or full parity. Double equipment and
wardrobe Items[1] formulas remain source-shaped observations pending client evidence.
Low-score same-UID retention and other ranking APIs remain open.
[Calculation contract](docs/PRESTIGE-CLIENT-CALCULATION.md).

## Historical session-root fix and D13 codec qualification

Owned public-derived selection 15047 passed 250/250, zero failures/skips, including
30 new root cases and 71 D13 RandomDailyTask codec cases plus retained controls.
The prior new 25-case baseline exposed 20 failures / 5 passing controls. Broader 74510
stays failed 556/1/0 (557 total, 24m36s); final raw-alias validation and five added
navigation cases correct its sole failure. See FSB-129 and [validation](VALIDATION.md).

Exact public `5ec4e1f4` Windows40438330239/job121972342308 remains FAILED:
26,776 core passed / 6 failed / 164 excluded; updater 434/434 passed, build passed,
launcher/parsers not reached. All six failed cases pass in the focused successor,
but no newer full-source acceptance follows. Last accepted full source remains
`f21e2a11`; a new exact-public full gate is pending. The codec is a protocol
prerequisite, not a new gameplay authority or separately numbered source-bug fix.
Private English catalogs/tests are not included. Historical sections below do
not establish a newer live, distribution or full-parity result.

## Current update - focused verification, public full gate pending

Clean public-derived source now passes250/250 focused controls after complete
networking dependency restoration and native saved collection defaults. A new
local disposable client session also reaches world entry and movement. Full
public CI remains pending; neither compilation-only failure is reclassified.

Public ef868fa8 Windows40415429365 failed compilation only because the native network birth tests referenced an omitted helper (three CS0246 errors; no tests executed). The existing tested bounded capture helper is now included unchanged. This is publication dependency repair, not a new gameplay fix or a passing full-suite claim.

The source successor contains FSB114-123 and the explicit saved-state startup /
synthetic-fixture tools. Relevant owned selections pass591/591,64/64,102/102 and
corrected47/47. The earlier focused228 run remains225passed/3failed; its CP50,
promotion39, Chi33+caller, CLI25 and26 measured-PS7 child cases pass. No failed
run is reclassified as green. Exact-source public full checks remain pending;
see [validation](VALIDATION.md). Native authentication, SQL, actual client,
English/distribution and whole-server parity remain open.

## Previous failed source attempt and focused corrections

Exact publiccb49be1da4ead0e6a9aea78ab7fb00bcf839b431 failed
[Windows40373707806](https://github.com/vgcman16/wolfconquer/actions/runs/40373707806),
job121750963988. Its terminal receipt ended2026-10-26T18:32:20.4338084Z:
core26,153passed/6failed/165existing exclusions, updater434/434. Restore/build
passed; launcher/parsers were not reached. This is a failed full run.

Four prefix Facts required unsafe counter wrapping, UID-only failure publication
or foreign Beast adoption; two duplicate Character-field rows required successful
login. The replacements retain every input row and valid control, requiring
unchanged-state rejection and genuine ownership. Actual focused225/225 pass,
0fail/skip: prefix24, PIN55, retained Hairface71, existing Chinese packet31,
independent Echo request1 and explicit-key catalog decoder43. The catalog cases
include four primary published known answers and every native tail length.

Earlier separate component proofs remain290decoder/CLI/packet,135prefix and
32readiness checks; private owned-input UI decoding is not runtime rendering.
The current target stays Chinese7899 / VisibleDx8D13. Newer7935-chain research
uses an explicit profile without game assets/keys, default routing or a business
handler. See [protocol and catalog scope](docs/CHINESE-7935-PROTOCOL.md).

Combined corrected-source verification passes330/330,0fail/skip: the prior225
plus48AccountManager VIP transaction cases,55guild recruitment policy cases and
two retained Frame VIP wire goldens. The controlled unchanged baseline had
72failures/33valid controls (VIP40/8, guild32/23, Frame2pass); the preceding
fixture-import attempt was compile-only. FSB-112/113 record behavior and limits.
Two actual private Echo catalogs decode to UTF8-text candidates (501/9114bytes),
without Lua execution, runtime binding or gameplay/schema acceptance.

This reviewed successor needs its own exact-public full Windows gate. Earlier
a3cb full CI and focused selections do not validate the complete successor or
prove actual-client acceptance, full English, distribution, deployment or parity.
Game/client processes, original saves, routes and restore points are preserved.

## Latest accepted public CI — recoverable mailbox owners

Exact publica3cb2624592efdc8a17ae7f01e814c442c124fd7 passed
[Windows40321922059](https://github.com/vgcman16/wolfconquer/actions/runs/40321922059),
job121568740813: all8stages0. Actual receipt20261026T122322395Z-6de9e39e ended
2026-10-26T12:34:05.4328605Z: core25473Passed+165existing NotExecuted,
updater434/434, launcher57/57, synthetic parsers20+13, build157warnings/0errors
(103.14s). Actual terminal receipt/TRX and GitHub conclusion were read. This
supersedes the pending checkpoint status, not the client/Host/parity limits.

## Earlier accepted public CI — Ninja safety and import tooling

Exact public42b0f6e29244ae2b5b1237d9487931643e254b01 passed
[Windows40313587063](https://github.com/vgcman16/wolfconquer/actions/runs/40313587063):
all8stages0, actual TRX25362core Passed+165existing NotExecuted, updater434/434,
launcher57/57, synthetic parsers20+13, build157warnings/0errors. Receipt
20261026T111637780Z-b26d2ef1 ended2026-10-26T11:21:56.683474Z. The28import-tool
Python tests ran standalone. The later mailbox transaction source below has focused
proof; its full successor CI is pending. No client/deployment/parity claim follows.

## Earlier accepted public CI — exact native asset reviews

Exact public816f3a20d872aa6af4a877e6eb2145ff2b9577d7 passed
[Windows40304697861](https://github.com/vgcman16/wolfconquer/actions/runs/40304697861):
all8stages0, actual TRX25299core passed+165unchanged NotExecuted,
updater434/434 and launcher57/57. Synthetic receipt parsers20+13 pass;
Release157warnings/0errors. Receipt20261026T100636154Z-88b9948d finished
2026-10-26T10:12:05.6248431Z. The standalone PS planner/exclusion/stager/native
review/pipeline checks were measured separately, not invoked by this CI.
No actual assets are approved; full-client distribution, current-public client
acceptance, complete English and gameplay parity remain unfinished.

## Earlier accepted public CI — publisher filename admission

Exact public98be3f410a68272b53c170abe25d5fb8c182b4a3 passed Windows40297219885:
all8stages0, actual TRX25299core passed+165unchanged NotExecuted, updater357/357,
launcher57/57, synthetic parsers20+13, build157warnings/0errors. Receipt
20261026T090840597Z-d2f7acd6 finished2026-10-26T09:13:49.6704534Z. All52new publisher
outcomes pass in the full run. This supersedes the older CI summaries, not their
historical provenance. Package payout, actual-client acceptance and deployment
remain OPEN; no live process/data changed.

## Latest accepted LOCAL integration gate — standalone package admission GREEN

Tracked-only public base6bde6187 plus the eight reviewed package source/test paths
passed all8sequential LOCAL stages, receipt20261026T064543721Z-2f26492d,
finished2026-10-26T06:51:38.3601402Z. Core25193passed/0failed plus the same165
existing NotExecuted rows; updater305/305 and launcher57/57. Build157warnings,
0errors. All10new/migrated package checks pass; the only removed prior outcomes
are the two explicitly replaced unsafe grant oracles. Full package payout and
current actual-client acceptance remain OPEN. No live processes/data were changed.

## Earlier accepted LOCAL integration gate — equipped sigil ownership GREEN

Exact public `1e042149367ae8f784bb88825844d31884b07d7a` was extracted from a
tracked-only git archive (5434files) into a fresh owned Temp workspace. Local
session53716/receipt20261026T043853717Z-31626f9e finished2026-10-26T04:44:05.2731019Z;
all8sequential stages exited0. Root read terminal receipt, actual TRX counters/
outcome groups and stage logs: core **25071passed/0failed**,165existing NotExecuted
rows (25236total); updater305/305, launcher57/57, synthetic parsers20+13 (no HTTP/
services). Build190warnings/0errors,72.90s. All39new sigil cases, the corrected
missing-metadata fact, four retained cooldown clamp controls and all43mailbox
envelope cases pass. Public source was normal-fast-forward pushed and exact
remote verified after this gate passed. This is Windows LOCAL proof, NOT GitHub
CI, current actual-client acceptance, full Ninja/cast/mailbox transactions,
complete English, deployment or full parity. Running sessions, original saves,
protected clients, endpoints and60held Guild/Union paths remain unchanged.

## Previous accepted LOCAL integration gate — coherent mailbox parsing GREEN

Exact public `e9f472f338400f6d501146e664a32a253d8bcd09` was extracted from a
tracked-only git archive (5427files) into a fresh owned Temp workspace. Local
session7563/receipt20261026T042713959Z-10d749b3 finished2026-10-26T04:32:24.4981243Z;
all8sequential stages exited0. Root read the terminal receipt, actual TRX counters/
outcome groups and logs: core **25032passed/0failed**,165existing NotExecuted
rows (25197total); updater305/305, launcher57/57, synthetic parsers20+13 (no HTTP/
services). Build190warnings/0errors,72.12s. All43new mailbox envelope cases pass;
retained account/mailbox cases and wire goldens also pass. Public source was
normal-fast-forward pushed and exact remote verified after this gate passed.
This is Windows LOCAL proof, NOT GitHub CI, current actual-client acceptance,
mailbox business-transaction completion, complete English, deployment or full
parity. Original saves, running sessions, protected clients, endpoints and the
60held Guild/Union paths remain unchanged.

## Previous accepted LOCAL integration gate — combat owners and empty warehouse GREEN

Exact public `f3bfae4aae83cf2409e86827d7a55ce6685d9fa3` was extracted from a
tracked-only git archive (5424files) into a fresh owned Temp workspace. Local
session61648/receipt20261026T041147705Z-eb1c0faa finished2026-10-26T04:16:55.4688490Z;
all8sequential stages exited0. Actual receipt, TRX counters/outcome groups and
stage logs were inspected: core **24989passed/0failed**,165existing NotExecuted
rows (25154total); updater305/305, launcher57/57, synthetic parsers20+13 (no HTTP/
services). Build190warnings/0errors,71.51s. All38minimum/sentinel,35required-owner
and10bank-presence new cases pass, alongside the corrected Frame withdrawal and
all20native counter fixture cases. The two earlier full failures and narrow
test-only corrections remain documented in FSB089. Source was normal-fast-forward
pushed and exact remote verified after acceptance. This is Windows LOCAL proof,
NOT GitHub CI, current game-client acceptance, complete English, deployment, full
cast/warehouse transactions or full parity. Running sessions, original saves,
protected clients, endpoints and the60held Guild/Union paths remain unchanged.

## Previous accepted LOCAL integration gate — house saves and account rejection GREEN

Exact public `c1ee382d7669189037fd7d22618c283e7537ee38` was extracted from a
tracked-only git archive (5410files) into a fresh owned Temp workspace. Local
session14425/receipt20261026T025716423Z-2a4bf698 finished2026-10-26T03:02:13.3892318Z;
all8sequential stages exited0. Actual terminal receipt, TRX counters/outcome groups
and stage logs were inspected: core **24903passed/0failed**,165existing NotExecuted
rows (25068total); updater305/305, launcher57/57, synthetic parsers20+13 (no HTTP/
services). Build191warnings/0errors,52.09s. All157new account rows,54house rows and
both corrected malformed-auth tests pass. Public source was normal-fast-forward
pushed and exact remote verified only after this clean-source gate passed.
This is Windows LOCAL proof, NOT GitHub CI, actual game-client login, complete
English, housing/authenticated world-handoff completion, deployment or full parity.
No running process/configuration, original save, protected client or endpoint changed.

## Previous accepted LOCAL integration gate — durable warehouse locks GREEN

Exact public `0dda74102c9ebdba590eee0823227922961d2bf1` was extracted from a
tracked-only git archive (5398files) into a fresh owned Temp workspace. Local
session93031/receipt20261026T020222654Z-a7ab0d5d finished2026-10-26T02:06:08.3792132Z;
all8sequential stages exited0. Root inspected terminal receipt, actual TRX counters/
outcome groups and stage logs: core **24720passed/0failed**,165 existing NotExecuted
rows (24885total); updater305/305, launcher57/57, synthetic parsers20+13 (no HTTP/
services). Build191warnings/0errors,52.13s. All9new expiry cases pass. Public source
was normal-fast-forward pushed only after this clean-source gate passed.
This is Windows LOCAL verification, NOT GitHub CI, running-game acceptance,
complete English, full warehouse/game parity, deployment or a cleared backlog.
No replacement listener/manual GitHub status was used to claim CI success.

## Previous accepted LOCAL integration gate — warehouse Show GREEN

Exact public `5d78a448cb18e4eef20eab3688691b77c1e5ba50` was extracted from a
tracked-only git archive (5393files) into a fresh owned Temp workspace. Local
session72001/receipt20261026T014456139Z-3534d9d4 finished2026-10-26T01:49:14.552409Z;
all8sequential stages exited0. Root inspected actual terminal receipt, TRX
counters/outcome groups and stage logs: core **24711passed/0failed**,165 existing
NotExecuted rows (24876total); updater305/305, launcher57/57, synthetic parsers20+13
(no HTTP/services). Build191warnings/0errors,85.23s. All19new Show cases are included.
This is Windows LOCAL verification, NOT GitHub CI, running-game acceptance,
complete English, deployment, full warehouse/game parity or a cleared backlog.
Runner2 remained offline/idle at the preflight; no Windows job or replacement
listener was launched and no manual green GitHub status was written.

## Previous accepted LOCAL integration gate — warehouse and house-load GREEN

Exact public `50d1a22b27d6d65329ba020d9cd6235e247b8400` was extracted from a
tracked-only git archive into a fresh owned Temp workspace outside the original
server-source ancestors. Local session60498/receipt20261026T011029802Z-536dd321
finished2026-10-26T01:14:20.7259432Z; all8sequential stages exited0. Root inspected
the terminal receipt, actual TRX counters/outcome groups and stage logs:
core **24692passed/0failed**,165 existing NotExecuted rows (24857total);
updater305/305, launcher57/57, synthetic receipt parsers20+13 (no HTTP/services).
Build191warnings/0errors,55.16s. All41new warehouse/Trade rows and16new house rows
are included. The earlier11native-INI fixture failures are corrected without
changing the adapter or skipping assertions; the full clean-source gate now passes.
This is Windows LOCAL verification, NOT GitHub CI, running-game acceptance,
complete English, deployment, global UID safety or full gameplay parity.
The trusted runner remains unavailable; no replacement launcher or manual green
GitHub status was used to misrepresent a CI result.

## Latest accepted GitHub CI gate — player-booth full Windows CI GREEN

[Windows40051408447](https://github.com/vgcman16/wolfconquer/actions/runs/40051408447)
passed exact public `b8cc5cfa12e1fa60c8a61b4478d12e5631309341`; job120744692469
completed2026-10-24T01:52:36Z. Receipt20261024T014836141Z-8091590e finished
01:52:26.9077483Z; all8 sequential stages exited0. Root read actual runnerHEAD,
receipt, terminal TRX counters/outcome groups and build/test/parser logs, then
verified completed GitHub success: core **24569passed/0failed**,165 existing
NotExecuted rows (24734 total), updater305/305, launcher57/57, synthetic parsers20+13
(no HTTP/service probes). Build191warnings/0errors,56.23s. All41new booth controls
pass alongside the retained24528core baseline. This is offline source integration,
not all-transfer/global UID safety, live-client acceptance, complete English,
deployment, full gameplay parity or a cleared CodeFactor backlog.

## Previous accepted gate — Trade closure full Windows CI GREEN

[Windows40048716539](https://github.com/vgcman16/wolfconquer/actions/runs/40048716539)
passed exact public `3f7a5a9f2d915e282b11cac0ff5e50f0fdef1634`; job120736279115
completed2026-10-24T01:18:09Z. Receipt20261024T011404291Z-3bd038af finished
01:17:59.7428817Z; all8 sequential stages exited0. Root read actual runnerHEAD,
receipt, terminal TRX counters/outcome groups, build/test/parser logs and completed
GitHub success before accepting: core **24528passed/0failed**,165 existing
NotExecuted rows (24693 total), updater305/305, launcher57/57, synthetic parsers20+13
(no HTTP/service probes). Build191warnings/0errors,54.51s. All58new Trade controls
are retained alongside the earlier24470core passes. This is offline source
integration proof, not all-transfer safety, live-client, complete English,
deployment, full gameplay parity or a cleared CodeFactor backlog.

## Previous accepted gate — magic admission full Windows CI GREEN

[Windows40046944516](https://github.com/vgcman16/wolfconquer/actions/runs/40046944516)
passed exact public `781cdf227e249744b068f06bab1adec1e6dad7ed`; job120730744913
completed2026-10-24T00:55:10Z. Receipt20261024T005058389Z-3e862afb finished
00:55:01.5416317Z; all8 sequential stages exited0.
Root read actual runner HEAD, receipt, TRX counters/outcome groups, terminal
build/test/parser logs and completed GitHub success before cleanup:
core **24470passed/0failed**,165 existing NotExecuted rows (24635 total);
updater305/305; launcher57/57; synthetic parser20+13 (no HTTP/service probes).
Build191warnings/0errors. This accepts shared/root/basic eligibility and the
generic/Recruit/Pounce behavioral batch offline, not live-client, deployment,
complete English, full gameplay parity or a cleared CodeFactor backlog.

## Previous accepted gate — quest/updater full Windows CI GREEN

[Windows40041857100](https://github.com/vgcman16/wolfconquer/actions/runs/40041857100)
passed exact public `999ce3262dcf5d6ac9c365af3670b542a7339952`; job120714544998
completed2026-10-23T23:55:01Z. Receipt20261023T235053615Z-3475fa2f finished
23:54:52.1499601Z; all8 sequential stages exited0.
Actual runner HEAD, receipt, TRXs and terminal logs were read before cleanup:
core **24340passed/0failed**,165 existing NotExecuted rows (24505 total);
updater305/305; launcher57/57; synthetic parser20+13 (no HTTP/service probes).
Build190warnings/0errors. This verifies the quest-loader/Frame and updater-pin
batch in offline CI, not live-client, deployment, complete English or full parity.

## Previous integration gate — runtime full Windows CI GREEN

[Windows40036685838](https://github.com/vgcman16/wolfconquer/actions/runs/40036685838)
passed exact public `c90122abc1961cef8c8f4a90f6dec623a3ddbaf8`; job120697981313
completed2026-10-23T23:01:24Z in4m21s. Receipt20261023T225718764Z-c6c587e8
finished23:01:13.5433267Z; all8 sequential stages exited0.
Actual terminal TRXs: core **24183passed/0failed**,165 existing NotExecuted
rows (24348 total); updater301/301; launcher57/57. Build190warnings/0errors.
Synthetic updater-status/parser controls20+13 passed, with no HTTP/service
probes. Root read actual runnerHEAD, receipt, build/parser stdout, TRXs and
GitHub terminal state before report cleanup. This is public source verification,
not live-client, deployment, complete English or full-parity acceptance.

FSB068 private focused proof is556/556,0fail/skip,5s, including both controlled
D11/D13 race cases now retaining stamina100 and unchanged old packet assertions.
The first public transfer5c4948d0 failed build40036236341 (190warnings/4CS0103,
no tests ran): root placed its manual-clock declaration in another test.
Corrected in publicd218770e; c90122ab merges only reviewed generated parity
assets from534c26ab. No force/reset/stash. An accidental pre-correction workflow
40036614525 is terminal canceled; its source is not the accepted checkpoint.
Earlier full40032574805 on0deb1e46 failed the old Reviver assertion; its exact
historical stamina value/interleaving remains unknown. Those failures are
superseded by this exact accepted run, not silently reclassified as passes.
No live,SQL,original-save,routing or deployment action.

## Verified fixes

### FSB-134: Validate prestige saves before replacing complete file images

**Source:** PrestigeRanking.Save and the truncate-before-append DbWrite.Open port.

**Defect:** Save could persist stale cached owners, silently narrow a category,
substitute zero components for missing Points, or serialize malformed identities
and delimiter-bearing text. The generic writer truncated the destination before
appending the prepared rows.

**Correction:** capture complete valid current class rows under SynRoot, using real
owners and matching unique UID keys/categories. Reject missing Points and invalid
text/Unicode; preserve valid sparse graphs, World exclusion, full UInt32 values,
variable component arrays, literal path prefixes and exact valid output format.
Prepare strict UTF8 bytes before filesystem mutation. Write/flush/close an exclusive
same-directory stage, then use real Replace with an owned backup or create-only
Move. Verify outcomes; retain still-owned artifacts and fence uncertain paths.
Known failures retain their primary exception. Verified commitment cannot become
a reported rejection solely because cleanup failed.

**Verification:** old Save produced15 failing/seven passing checks (22 total).
A review regression used actual File.Replace and real file handles, exposing the
prior-first observer's identical-image classification error: one failed/two passed.
The corrected observer checks consumed-stage commitment before prior equality.
Final public765/765 and private770/770 pass, zero failures/skips; private includes
five compact-English controls. Cases cover exact create/replacement bytes, Unicode,
actual Windows sharing refusal, missing parent, directory targets, no ordinary
sidecar residue, graph/cache/point preservation and retained loader/query controls.

The observer tests select an unacknowledged state after a real successful Replace;
they do not prove Replace itself threw. The recovery fence is process-local.
Directory durability, power loss, automatic startup recovery, hostile namespace
races, cross-process compare-and-swap, multi-file transactions and production
adoption remain unproven. This does not fix the generic DbWrite for other systems.
[Storage contract](docs/PRESTIGE-FILE-STORAGE.md).

### FSB-133: Reject control-bearing saved numeric fields

**Source:** managed prestige catalog projection and legacy file parser.

**Defect:** numeric parsing alone can accept trailing NUL characters, allowing
malformed saved fields to pass otherwise consistent metadata mirrors.

**Correction:** require nonempty ASCII decimal digits before UInt32/Int32 parsing.
Keep overflow, category, component-count and source-mirror checks. Valid wire bytes
and numeric widths are unchanged.

**Verification:** all 63 catalog NUL refusals and five loader NUL refusals pass in
the qualified selections below. The prior projection failed all 63 catalog cases.
This does not audit every numeric parser in the repository.

### FSB-132: Prepare both prestige rank publications before assigning either

**Source:** managed CreatePrestigePoints final class/world publication.

**Defect:** the class rank could change before a missing or corrupt World owner
failed. Separate per-rank locks could also straddle a loader graph replacement.

**Correction:** under the shared gate, resolve real required owners, validate
complete UID maps including capped tails, and prepare both sorted/capped maps and
champions before reference assignment. Preserve excluded-class world-only behavior,
valid ordering and entry identity. Shared-gate queries/removal observe one graph.

**Verification:** nine publication cases plus retained formula/ranking/caller
controls pass. Five new publication checks failed against prior production.
Client-field mutation windows are addressed by FSB-135. Stale-low retention and
other global Rank APIs remain open; no crash-safe transaction is claimed.

### FSB-131: Validate complete prestige files before replacing the live graph

**Source:** decompiled PrestigeRanking.Load and its managed port.

**Defect:** Create ran before file admission, rows used tolerant numeric defaults,
and incremental insertion could erase prior state or publish a valid prefix of a
malformed file. Earlier tests explicitly required several of those defects.

**Correction:** read, strictly decode and validate the complete counted file into
private rows, then publish one fresh twelve-bucket graph under SynRoot. Missing
input and actual I/O failures propagate; malformed data throws FormatException.
Refusal preserves old graph/cache/row/point identities and file bytes. Variable
point counts, full positive UInt32 UIDs, valid BOM formats, threshold 20,000,
world 100/class 30 and deterministic saved-order ties are retained.

**Verification:** baseline 576 cases executed: 177 failed / 399 passed, zero skips
(109 loader-family, five publication and 63 projection failures). All 576 now pass.
The broader public-derived selection passed 1,295 with 129 existing exclusions;
private passed 698/698, including five English controls. Existing valid formats,
literal path prefixes, Unicode, malformed tails, duplicates, caches and consumers
are covered. Save was subsequently corrected by FSB-134. No production Load caller,
native execution, deployed refresh, power-loss recovery or live-client proof is
established. [Storage contract](docs/PRESTIGE-FILE-STORAGE.md).

### FSB-130: Validate complete saved prestige query data before publication

**Source:** ordinary `WorldPacketHandlers.RankMemberShow` and the plain managed
`PrestigeRanking.Load/Save` / `MsgRankMemberShow.Process` reference.

**Defect:** the ordinary Show 0 / first item type 1 path returns an empty result
even when a genuine complete saved champion exists. Legacy source loader defaults
and incremental rank publication are unsafe models for accepting malformed data;
the candidate does not repair or activate that separate legacy loader.

**Correction:** construct an immutable projection only after validating
all rows, numeric widths, identities, mirrors, category and component counts,
including ineligible/capped tails. Require the actual connection's same current
session and explicit matching manifest count. Preserve saved appearance and the
existing 2054 schema; serialize the complete response before sending. Corruption
or serialization rejection produces no frame; valid empty/ineligible snapshots
retain the known empty response. No fabricated owner, default row, live mutation,
or reward/progression authority is introduced.

**Verification:** prior handler baseline52 executed51 failures/one guard pass;
three failures were diagnostics and48 functional differences. Corrected public-
derived437/437 passed,0fail/skip:52 handler,233 projection,152 retained packet/
parser/dispatch cases. Full literal ASCII/UTF8 frames, same-count corrupt tails,
duplicates, ownership refusals, oversized output, Unicode, cancellation and valid
controls are covered. Exact adcae028 passed full Windows40456938513: core27,168
passed/164 existing exclusions, updater434, launcher57, parsers20+13 and all eight
stages exit0. This full result predates FSB-131..133. Goldens are not live D13 proof.
GenericRanking, Viewer, other families, dynamic refresh, rewards and full parity
remain open. [Query contract](docs/PRESTIGE-QUERIES.md).

### FSB-129: Validate supplied session roots before fences and recovery binding

**Source/location:** `LegacyOrdinaryAdmissionMutationFence` and configured-domain
checks; `LegacyMailboxPackageMutationFence`/StoreScope; `WorldSessionStore` mailbox
recovery admission; shared `LegacySessionStorageRoot`.

**Defect:** empty/whitespace/malformed UsersRoot could be classified as no storage,
then normalized only after retained owner or bound-producer short circuits. This
made rejection order-dependent and caused Path.GetFullPath(empty) exceptions.
Recovery could bind a producer before refusing missing/mismatched saved routes.
The first correction also checked only normalized Windows components, allowing
normalization to erase a supplied trailing-dot/space alias.

**Corrected invariant:** null alone retains ordinary storage-free semantics.
Invalid supplied syntax rejects before callbacks, persistence or publication;
both bool fences return `false` and Store scope rejects its invalid precondition.
Recovery requires a nonnull, fully-qualified normalized match to the genuine
producer's UsersRoot before binding. Windows raw and normalized components are
checked while preserving real '.'/'..', relative/drive-relative and UNC volume
semantics. No owner registry is cleared, fake authority supplied or journal fault
converted to allowed absence. The six memory-only legacy outcomes now use
explicit `null`; successful packets and behavioral assertions remain unchanged.

**Verification:** new 25-case prior-source baseline 20 failed / 5 controls passed.
Broader 74510 remains failed 556/1/0; final owned public-derived 15047 passes 250/250,
zero failures/skips, including all 30 new root cases and all six former full-CI
failures. The total includes 71 separate codec cases and retained controls; it is
not 250 root tests. New exact-public full CI remains pending. This proves the
focused Store/admission invariants, not live gameplay, full parity or crash safety.

### FSB-128: Explicit D13 synthetic fixtures select the catalogued class

The owned fixture creator inherited Class10 from its legacy compatibility input.
Exact D13 ProfessionalName data contains1000/InternTrojan, not10; the native
family calculation `(class % 100000) / 1000` also distinguishes them. Add a closed,
explicit creator profile selecting1000 only for new `VisibleDx8D13` fixtures.
Default/RootD11 calls retain10; ambient settings do not select it and existing
saves are never rewritten. Current hero codecs and economic/item/UID values stay.

The final public-derived focused selection passes314/314, zero failures/skips,
including20 new profile cases: API/CLI rejection before filesystem work, both
hero class widths, genuine ordinary Character/Spells1110 commit before success,
Action78/relogin without regrant, actual file-lock refusal and raw/encrypted Host
restart. This corrects fixture input selection, not a global class migration or
new casting implementation. Live class/skill UI, casting, full Windows CI,
complete English and7935 gameplay remain unverified.

### FSB-126: Native-probe cleanup requires retained process identity

The diagnostic probe previously reopened historical observed PIDs and terminated
them, even after the original descendant could have exited and its ID been reused.
Cleanup now pins the actual spawned root handle, captures a fresh OS parent graph,
and retains every proved descendant handle before terminating anything. Child
birth must fall within the exact retained parent's lifetime and snapshot boundary.
Only those handles are terminated individually; old integers never grant authority.
Missing intermediate-parent witnesses and post-snapshot descendants remain explicit
cleanup uncertainty. This does not prove complete descendant drain or all snapshot
attribution, and it does not establish the cause of a game-client exit or freeze.

A public-derived candidate passes11/11 including the unchanged direct-orphan
positive control. A fresh private committed-source copy then passes the final
11 cleanup cases within17/17 total, zero failures/skips. Both actual sibling
regressions require real stdin/stdout READY and immediate liveness before cleanup;
the intentional exited-root case retains its real exit0 witness. Eight lifetime
guards also pass. Primary assertions and all independent actor-cleanup failures
are preserved. Original RED2 and nominal GREEN9/11 remain recorded; the latter
used an older binary because a copied source timestamp predated its output. An
explicit rebuild corrected that integration artifact; fresh-source17 is the final
focused evidence. Exact new public full Windows acceptance remains pending.

### FSB-125: Select a supported D13 body for new synthetic fixtures

The offline creator inherited Body1003 from an older loot fixture. Exact
VisibleDx8D13 CMsgUserInfo2221 parses Mesh as field3/tag18 at00DD5360 into
wrapper+434; handler00DD61FD-00DD6214 passes Mesh modulo1000 to the native look
setter. Body1003/Face154 therefore requests look3. The staged Look table and
c3 index support look8/1008, while look3/1003 is absent. Both existing hero
packet producers already encode the correct Mesh field and packing.

Only future synthetic owned fixtures now select Body1008, retaining Face154,
Haire27, class, economy, items and real UID history. The independent expected
Mesh is1541008, field3 bytes18 90 87 5E. No existing saved body is rewritten,
no global player remap is added and no armor, skin or native owner is invented.

Root's clean public-derived focused run passes41/41, zero failures/skips,
including all5 new appearance cases: creator/CLI through actual public Frame,
raw/encrypted real WorldServer startup and an existing Body1003 preservation
control. Root then observed actual manual login in the staged D13 client: LootFixture's complete body was visibly rendered on
map1002/Twin City and actual movement reached409/356. The user independently confirmed that the body is visible. This is one isolated
local client rendering/movement result. It does not prove reliable restart, general English,
full gameplay/parity, public full CI or distribution readiness.

### FSB-124: Seed the native saved collection defaults in new test fixtures

The offline creator omitted CollectionID/CollectionStorage. The existing safe
snapshot packet API refused missing required ID, so real startup skipped2450
before22210. Native ServerDatabase1102/1104 reads default0/empty, Save661/663
persists the real owner as0 and canonical0/0/, and MsgLoginHandler32 always
loads/sends that state before hero. Exact D13 binary2450/type0 also handles its
zero/empty lists and final refresh. No general packet fallback or native owner
was fabricated: only future fresh fixtures gain these two saved fields.

Clean public-derived run1005 passes250/250, zero failures/skips, including4 new
creator/CLI/public Frame/raw/encrypted Host goldens for2450 immediately after
config and before22210. Existing collection5, native-birth5, Hairface164,
fixture29, API40 and Host7 all pass. The four omitted networking partials and
all11 PIN/ended-owner cases are restored as their original complete dependency
scope. Both earlier public attempts failed compilation only; neither is green.

A new disposable live fixture then sent2450, accepted actual manual account
login, progressed through77/78/132, rendered map1002 and accepted observed
movement. The previous fixture/processes were retained/retired deliberately;
original saves/routes/client assets were not repaired. This is a bounded local
client result, not full public CI, all gameplay/SQL/English/distribution parity,
or proof of the old font-thread stall's internal mechanism. Character-model
visibility is still being checked separately; keep the working session open.

### FSB-123: Bind explicit saved-state ownership in real WorldServer startup

Production Program used the unbound Frame constructor and could not open the safe
ordinary saved-admission transaction. The new explicit bounded configuration
binds its expected UID domain and all snapshot/journal routes to a genuine leased
runtime. Domain checking precedes retained registry recovery writes. Startup never
bootstraps; missing/conflicting state, a second owner and pending joint journals
refuse before listening. One runtime binds one actual handler, retains full
runtime/auth/profile settings and releases its registry only after actual worker
and world-operation drain.

Actual corrected owned run87908 passes47/47, zero failures/skips:40 public API,
configuration/ownership/actor controls and7 actual WorldServer process controls,
including raw/encrypted saved entry, abrupt restart, second-process contention and
normal non-listening disposal. The earlier228-case run remains225/3; its three
new test errors were a BOM-unaware assertion and incoherent HP/mana fixture,
corrected with real decoding/source literals while retaining complete byte checks.
All26 previously setup-blocked children pass after the measured PS7 binding.

Separate offline fixture CLI25/25 passes in21122. It creates new GUID-owned
synthetic snapshots and explicit UID history, publishes configuration last, never
imports existing saves/accounts and retains interrupted roots. These checks do
not prove account-password authentication, full native Loading/Host lifecycle,
mailbox payout activation, SQL, client rendering, deployment or full parity.

### FSB-122: Save ChargeChi and its donation debit before publication

AccountManager option2 updated Store before its two-field save and could repeatedly
charge100 points for an already-active permanent flag. Native Player defines a
byte; load defaults to0 and the Chi study consumer repeatedly reads1 without
consuming it (NpcHandler8164-8186, Player1097, ServerDatabase474/783,
MsgChiInfo193-195; managed LegacyChiStudyState116-119).

The dedicated transaction validates genuine current ownership, strict donation/
byte fields, persisted baseline and real recovery participants. It commits the
complete DonatePoints/ChargeChi file image and Store before diagnostics. Already1
refuses without another debit; malformed values cannot default or wrap. Valid
success retains its zero-packet behavior and insufficient funds retain the four
existing dialog frames. A failed postcommit diagnostic cannot refund or replay.

Actual owned run21122: all33 new ChargeChi cases and the retained source-backed
caller pass. The complete228-case run is FAILED225/3: three startup test assertion/fixture issues were subsequently corrected in
the47/47 API/Host rerun. Chi evidence does not
prove native mirrors, complete Chi lifecycle/ranking, SQL, client rendering or
cross-process/power-loss atomicity. No false success saver or native owner added.

### FSB-121: Commit donation conversion with the source wallet cap and bank spill

AccountManager option3 previously published/debited Store before its financial
file save. It also omitted the native ConquerPoints setter's2B cap and BankCps
spillover (NpcHandler8201-8206 -> Player2520-2557; saved bank is
GameClient.ConquerPointsSave/ServerDatabase489-490). The new actual-owner
transaction prepares one Character image before Store publication: donation and
wallet only without spill, all three fields with spill. Native UInt32 sum wrap
and an unrepresentable managed Int64 bank reject before payment. Existing cost100,
grant1B and spill MessageBox -> wallet update order remain. Delivery failure or
retirement after commitment cannot refund or replay a captured purchase.

Verification: all50 independent CP/bank cases pass in the broader5766 result:
2530Passed/26Failed/3NotExecuted. All26 failures stopped before child checks
because Root omitted the measured PowerShell7 path; that broader gate remains failed. All26 then pass in21122 with the measured
PowerShell7 binding, together with all50 CP cases. Tests cover exact file/Store/wire, width, bank defaults/overflow, real
locks, ownership, concurrency and delivery faults. This is managed one-file
transaction proof, not native financial mirrors, SQL, live client or full parity.

### FSB-120: Retire a live trade owner when a real pending journal appears

An earlier ordinary mutation fence could reject before the trade recovery owner
observed its pending journal, allowing that old owner to become usable again
after another owner reconciled files. Registered, direct, queued and disconnect
paths now observe the real pending journal under their actual lease and retain
the sticky recovery refusal. No accepting recovery callback or automatic replay
is introduced. A legitimate fresh owner is separate from the retired owner.

Verification: all30 TradeRestart controls, including four new sticky-owner cases,
passed in48996 and again within591/591. This does not establish every economic
handler, cross-process authority, SQL or crash-safe global transactions.

### FSB-119: Commit promotion payment, class and counter before publication

AccountManager option36 previously published Store/Class before the complete
DonatePoints/Class/ChargePromotion save and could log success after save failure.
It now validates the real owner and exact economic baseline, prepares one complete
Character image, replaces it before Store publication, and holds unknown outcomes
for the real recovery owner. The native byte counter cannot wrap. Postcommit log
failure cannot refund or turn commitment into rejection. Existing cost30, eleven
class ranges, insufficient-dialog order and zero success purchase frames remain.

Verification:39 new real Frame/owner/fault cases and11 retained mappings pass in
591/591. Scope is one cooperating Character-file transaction and managed Store;
SQL, native class synchronization, abrupt power loss and live gameplay remain open.

### FSB-118: Prepare spell responses and retain their actual publication owner

Ordinary admission saved a missing Cyclone but omitted its source2101 notice;
Action78 serialized rows during sends without complete validation or retirement
checks. The new path prebuilds valid frames before effects, publishes only the
committed new1110 at its source startup slot, and serves saved spells in stored
order followed by the existing Action78 echo. Real holder/native/lifetime and
learned-set checks surround sends, including a final identity check after
callback-bearing collection reads; same-record holder recreation cannot pass.

Verification:591/591 includes12 new spell cases; final owner correction passes
64/64 including all15 spell cases and retained login/Action78 controls. Independent
wire goldens remain. No whole-startup fencing, native loader or rendering claim.

### FSB-117: Preserve existing characters through coherent ordinary admission

The replacement's existing-character startup could apply creation tuning or
starter repair to saved progression and publish partial admission state. The
ordinary path now validates captured Character/sidecar images, preserves saved
progression and raw records, grants only a genuinely missing confirmed1110, and
commits through the real configured UID owner and recoverable admission journal
before Store/native/PIN publication and ANSWER_OK. Pending or ambiguous retained
work refuses mutation rather than inventing ownership or replaying unsafev6 work.

Verification: owned591/591 admission/promotion/spell selection passed, retaining
pre-ACK preservation, rejection, PIN, expiry and recovery controls. Earlier
fresh-process ordinary restart/retained-journal controls also passed in48699;
that broader run failed elsewhere and is not accepted as a whole. This does not
prove full native Loading, SQL, power-loss durability or live-client acceptance.

### FSB-116: Reject negative or overflowing eligible spell XP before local credit

- The same source multiplied UInt32 damage/rate, cast the wrapped product to
  signed Int32 and added it with signed overflow. An older test required XP to
  fall from Int32.MaxValue to MaxValue-2 on a positive award. This is not a wire
  requirement: the actual stored/2101experience field is signed Int32.
- Prepare the full UInt64 product and remaining nonnegative signed capacity
  before proficiency effects; reject invalid currentXP/gain/sum unchanged.
  Commit a valid prepared value in the original spell phase, retaining strict
  threshold, single-level/reset, prior-level and packet order. Ineligible Rune/
  NeedLevel/max-level branches do not invoke an unrelated XP preflight.
- Replace the unsafe wrap oracle and add11numeric/eligibility cases, within
  actual48996's31training/163dispatch passes. Refusals preserve real prof/spell
  identity/levels/XP, packet bytes/size/cursor and output; exact signed maximum,
  zero-rate and independent physical controls pass. No saturation or wire change.
- Limits: proficiency-internal arithmetic/double-pass behavior, concurrent
  mutation, caller-before-update effects, persistence and whole-cast transactions
  remain open. The broader48996run still has1unresolved combined-ledger oracle.

### FSB-115: Resolve required spell-training metadata before proficiency credit

- LegacyNativeUpdateSpell.CheckUpdate credited equipped proficiencies before
  indexing the learned spell's required current-level metadata. A sparse/null
  row could then fault after real XP/2199output. Capture the learned record once,
  resolve the required row and real required owners before either local credit.
  Missing required state refuses unchanged. NullDB physical training retains
  its independent path without requiring unrelated MySpells; zero damage is
  still an immediate no-op. No owner replacement or blanket catch is added.
- Actual48996 passes163/163native attack-dispatch outcomes, including31training
  outcomes covering missing/null rows, required/optional owners, real proficiency
  controls and retained2101/2199/system-message ordering. Source-only earlier
  drafts are not executed RED claims. Root intook the exact tested component.
- Limits: real callers may already have spent resources or changed pets before
  this update. Publicly mutable owners/catalogs, concurrency, downstream prof
  storage/serialization and transport failure remain separate. This is local
  preflight, not complete cast atomicity, native Host or live-client proof.

### FSB-114: Retain duplicate native Echo threshold rows in the new projection

- New Chinese7935EchoType6Thresholds is an explicit caller-data component for
  the inspected7931DX8 engine carried by the7935chain. An unpublished draft
  incorrectly refused equal attribute/threshold rows. Physical cache insertion
  compares signed primary keys only, inserts equal keys to the right and retains
  every row. Every qualifying type6row must participate in each key's maximum.
- Root reviewed the corrected3paths and native cache/body evidence. Actual
  candidate48699 passes29/29selector cases, including qualified/unqualified
  equal-threshold rows with same/different bonuses and reversed input order,
  threshold boundaries, signed keys, missing contributions and ownership/bounds.
  Root intook the exact tested3paths privately; broader candidate199failures
  remain unresolved. No passing baseline of the rejected draft is claimed.
- Checked contribution/result overflow and4096element guards are explicit
  sanitized offline limits; native dword wrapping is not copied. Numeric meaning
  of type6/result owner+5930 is not guessed. No game catalogs/keys, accepting
  handler, inventory/state/persistence owner, Host or live-client proof. This is
  a prepublication projection correction, not a claimed old-emulator feature fix.

### FSB-112: Respect actual guild recruitment class permissions

- Source Guild.cs286â€“377 and local AdmissionPolicy used a complement comparison
  that admitted unselected classes; Windwalker queried Fire, and missing Info
  threw. Require every requested known nonzero class bit, reject missing state,
  and preserve all eleven class ranges and existing bit values.
- Controlled baseline27699:32failed/23passed policy cases. Corrected94769:
  55/55, within330/330 with0fail/skip. Genuine load-only owners and independent
  class-boundary/mask/no-effect controls are used; no fake membership callbacks.
- Limits: pure recruitment policy only. Managed membership mutations still refuse;
  broader held Guild/Union, donation economy, persistence/recovery and live client
  remain unfinished. The original held policy is preserved in an ignored backup.

### FSB-113: Commit AccountManager VIP debit and grant before success

- Options35/197 previously published the new session before saving and sent
  success even after save failure; captured stale requests could overwrite state.
  Prepare the complete three-field Character image and response frames, recheck
  actual Store/recovery/lifetime ownership and exact saved bytes, then commit
  through the real compare/replace writer before Store/success publication.
- Keep donation costs30/100, VIP6/7,30-day extension and valid packet ordering.
  Reject invalid/missing/stale owners, balances, files and date overflow unchanged.
  A committed transport failure cannot refund/replay; uncertain publication holds
  actual recovery rather than claiming an unchanged rejection.
- Actual baseline27699:40failed/8passed new VIP cases plus2retained Frame goldens
  passing. Corrected94769 passes48/48 plus both retained goldens, within330/330,
  0fail/skip. Real Windows file locks, concurrent requests, captured-owner changes,
  exact full-file images and post-commit transport failure/retirement are covered.
  The earlier19367 was compile-only (missing fixture Runtime import), not RED.
- Limits: one Character-file transaction, not SQL/multi-file/hardware power-loss
  proof, complete unknown-write fault injection, native financial/VIP adoption,
  production Host or actual-client acceptance. Other purchases remain separate.

### FSB-111: Preserve absent bytes in the new Chinese packet codec

- Location: Chinese7935XuanbaoOperationPayload.TryParseKnownPayload, packet 2132
  for the explicitly selected official 7935-chain DX8 engine. This is a new
  implementation defect caught before publication, not a claimed emulator bug.
- The conditional combining null with ReadOnlyMemory<byte> selected a nonnullable
  conversion. Parsed absent field 8 became present empty and added wire bytes
  42 00. Use an explicit nullable-memory null to retain real protobuf presence.
- Actual combined run 61403 had 225 passes / 1 failure: the independent duplicate
  child golden exposed the extra field. Two new parsed absence/empty goldens
  retain their distinct images. Corrected run 17613 passes all 290 selected
  outcomes: 31 packet, 62 CLI and 197 configuration/retained controls, no skips.
- Limits: known-schema codec only, no default registration, business handler,
  Host activation or live-client proof. Unknown fields deliberately refuse.

### FSB-110: Prepare the existing-character native prefix before publication

- Owners: the actual existing-character Frame prefix and sourceLoadCharacter
  lines758–788. The old path wrote Online and native fields before validating
  Hairface/Beast state; malformed input could leave partial saved/native state.
- Correction: capture the genuine native owners before snapshot/INI callbacks,
  validate the complete Character image and prepare every prefix participant.
  Commit an exact Online afterimage, then publish only prepared plain fields.
  Known unchanged failure retains the prior file and native owners; uncertain
  replacement is reported as recovery-required, not an unchanged rejection.
- Beast prefix loads now use absolute fresh-state arithmetic with complete
  positive progression metadata and byte bounds. Constructor null ownership
  rejects; foreign/relabelled Beast and foreign Hairface owners cannot authorize
  publication. Ordinary direct Beast mutation remains a separate audit horizon.
- Hairface prefix loads reject incomplete, duplicate/ambiguous, overflowing or
  truncated rows, including a valid254-character prefix hiding a longer tail.
  They replace obsolete rows with owned metadata copies. Native quoted boolean,
  numeric/string buffer behavior and valid source fields remain covered.
- Verification: actual unchanged-source88952 had14failures/1valid control.
  Corrected65772 passed135/135,0fail/skip:71actual encrypted-loopback/prefix,
  16Beast state,30complete INI and18INI prerequisite cases. All37new prefix
  outcomes and retained valid controls are present in the actual terminal TRX.
- Later fullcb49 CI exposed six remaining bug-preserving prefix/PIN outcomes.
  Corrected expectations retain all input rows and valid controls. Focused225/225
  includes24prefix+55PIN+71retainedHairface; counter overflow, denied writes,
  foreign Beast and duplicate Character fields cannot publish login authority.
- Limits: owned offline source/component proof, not successor full-public CI,
  native Host, SQL or live client. Paid-expiry, startup normalization, Store
  admission and persistent uncertain-write reconciliation remain outside this
  prefix; the full ordinary-admission successor is still isolated/unexecuted.

### FSB-001: Reject spell admissions before spending resources

- Owners: `LegacyNativeCanUseSpell` and the typed physical admission commit.
  Old validation consumed weapon authorization/soul, spent stamina and sometimes
  mana, or exposed output rows before a later rejection.
- Corrected invariant: read-only lookup/range/resource/cooldown validation and
  complete real 2176 packet preparation precede a client/player-serialized commit.
  False means null outputs and unchanged resources, authorization, soul, cooldown
  and queued success packets. Zero mana skips Rune; only a matching sigil is read.
- Metadata identity and level must agree, except the genuine 6009-to-6010 database
  alias. Negative cooldowns reject; rune/sigil reductions cannot create negative
  costs or backdated deadlines. Missing, disposed, undersized or aliased update
  rentals reject before commitment. Notification failure cannot undo acceptance.
- Verification: both private and independent public Release passed **510/510**,
  zero failures/skips, including 87 direct admission cases, 20 allocation cases,
  retained resource wire goldens, concurrency controls and 123 dispatch controls.
  A stale level-two test fixture was corrected to populate its actual metadata
  level; existing expected damage/packet assertions were retained.
- Limits: this fixes admission, not all executor-specific eligibility/effect
  transactions. Other resource/lifecycle writers, delivery recovery and durable
  replay/idempotency remain unfinished. No client, original save or SQL was used.

### FSB-011: Bound unmanaged packet constructor allocations

- Owner: `LegacyNativePacket` constructors. The source copied arbitrary byte
  arrays into a fixed 1,024-byte allocation and checked stack depth after allocating.
- Corrected invariant: validate first, allocate at least the full byte-array size,
  expose immutable truthful Capacity, and let admission check capacity before
  unsafe serialization. Existing small-buffer bytes and cursor conventions remain.
- Verification: 20 allocation cases within the **510/510** Release selections in
  both trees cover byte copies through 16,384 bytes, capacity, null/nonpositive
  inputs, retained deep-stack rejection and disposal. The unsafe oversized
  constructor baseline was not executed.
- Limits: general unsafe readers/writers still need a full bounds/lifetime audit;
  the retained stack guard is not a measured unmanaged-allocation counter.

### FSB-002: Require a real Hairface storage owner

- Source/local owner: `LegacyNativeHairfaceStorageRuntime` constructor; the old
  implementation accepted null and `Add` later faulted after publishing an item.
- Corrected invariant: reject null immediately with `ArgumentNullException(client)`;
  retain the actual client as a readonly owner. Tests use real native client owners.
- Verification: Release focused Hairface storage/login and character-read tests:
  **173 passed, 0 failed, 0 skipped** (2026-10-23, local offline run).
- Limits: does not yet fix loader partial publication, exposed mutable rows,
  foreign-owner login reuse, equipment validation or all mutation/send failures.
  No live client, original save or SQL was used. Commit is recorded in Git history.

### FSB-015: Safe single-pass ranking and rune-slot queries

- Owners: CombatGearRank, HWRank, NinjaRank, PrestigeRanking query methods and
  Rune.Free. These are eight observed CA1827 findings across five source files.
- Corrected invariant: preserve physical UID/value order and returned Entry aliases
  with one current-owner scan under existing writer gates. Missing buckets and
  invalid rows no longer crash the affected queries. A corrupt rune cache fails
  closed; a wrapped byte Count cannot reopen an occupied position-211 bag.
- Verification: private and independent public Release each passed **754/754**,
  zero failures/skips, including 24 new controls and the retained 510 selection.
  Three directly affected old crash assertions were replaced by safe outcomes.
- Limits: no benchmark or general concurrent catalog/lifecycle proof. Public
  mutable arrays/rows, Rune.Count/Add, BestOf and other writers still need audit.

### FSB-014: Persist active title changes before publishing session or packets

- Owner: registered opcode2313 `WorldPacketHandlers.TitleStorage`. The old route
  changed memory and sent success even when saving failed, treated wing4001 as a
  normal title on removal and echoed untrusted subtype metadata.
- Corrected invariant: validate the complete owned-title list and authoritative
  representable catalog identity; prepare real acknowledgement/UseTitle/spawn
  bytes; commit the four title fields through the genuine current-session gate
  and file coordinator before publishing the updated session. Canonical numeric
  fields are identical in memory and disk, including initially absent zeros.
- A bounded shared snapshot reader proves the expected UID/title baseline and
  validates the written title/non-title Character fields. A failed replacement
  is ordinary rejection only when the complete preimage is unchanged; otherwise
  the genuine recovery owner is blocked. The PIN reader now reuses this bounded
  reader without changing its exact-value/reason-prefix contract.
- Prepared owner and peer delivery revalidates captured native identity and exact
  session references. Changed/retired recipients do not receive stale packets.
  The real @@ catalog columns preserve spaced-name title2149 score150.
- Verification: private and independent public Release each passed **1008/1008**,
  zero failures/skips, including 80 active-route guards, 55 persistence/baseline
  controls, retained PIN/title goldens and the prior754 selection.
- Limits: offline owned files only; no live client or SQL. Real sharing-failure
  and postcondition/classifier controls do not prove every OS rollback fault or
  arbitrary transport race. The frame-admission fence does not prove every
  background writer honors it. It is not a durable title journal,
  all-crash recovery, multi-file atomicity or hostile filesystem-race immunity.
  Classic opcode2152 has its separate FSB-017 correction below; native title
  lifecycle and durable recovery remain separate unfinished work.

### FSB-016: Retain real read-only process image attribution

- Owner: `ConfiguredExecutableProcessGuard`. CI39926199302 failed closed when
  MainModule returned null; that API can be transient during process startup.
  The historical TRX does not prove the exact process or startup/exit timing.
- Corrected invariant: matching names enter one retained noninheritable Windows
  handle with limited-query and synchronization rights only. Query the image
  directly; ignore image-query failure only when that same handle proves exit.
  Missing handles, unknown/live-uninspectable state and protected exact paths
  still refuse. No preliminary PID-reopening HasExited escape remains.
- Both generic and layout never-launched pipeline fixtures now use independent
  per-root signed/configured/protected executable names. Intentional real running
  exact-path, attributable namesake and legacy-shared-name controls remain.
- Verification: the complete updater suite passed **301/301** in private and
  independent public Release, zero failures/skips. Includes25 new controls and
  14 quality-parser controls, actual Win32 image reads and retained owned-command
  lifecycle tests. Fixture cleanup boundaries were strengthened, not relaxed.
- Limits: a point-in-time read-only scan is not prevention of later process
  startup, filesystem-alias resolution, process birth identity before opening
  the handle, deployment or a claim that every possible race was reproduced.

### FSB-017: Persist classic title selection before acknowledging it

- Owner: registered managed opcode2152 List/Change. Replace stale-context Set
  with the genuine current-session commit and store-issued native identity.
  Validate the complete owned-byte list, prepare replies, persist MyTitle only,
  then publish the session and lifecycle-bound acknowledgment. Failed writes
  require exact unchanged preimage evidence; unknown outcomes block recovery.
- Compatibility: retain fixed2152 bytes/order and valid clear0. Initial file
  admission compares the exact IniDocument-trimmed session projection; after
  writing, raw ownership, complete lines and encoding must still match. Without
  a load-time raw token, whitespace-only historical edits are not detectable.
- Local evidence: all68 new classic controls and retained11 codec/handler cases
  passed within private and independent public Release2164/2164 selections.
  Clean committed-tree CI is pending below. Tests use genuine temporary files and commit owners,
  not accepting saver callbacks. The first fixture failed strict parsing and was
  corrected to provide mandatory fields; the parser was not weakened.
- Limits: no durable crash journal, hostile filesystem/external-writer proof,
  complete native title granting/login/save lifecycle or new live-client proof.

### FSB-018: Admit complete weapon slots and prepare bounded responses first

- Owners: native HundredWeapons slot queries and complete Equip/Unequip/Swap/
  Show/Hide methods. Reject corrupt tails, duplicate physical rows/appearances,
  aliased bitmaps/word arrays and overlapping occupied bits before mutation.
  Check the genuine current client/player/weapon owner, not matching IDs alone.
- Prepare the real2353 response before changing bits. Capture the seal once and
  construct complete private bytes before renting/checking actual capacity;
  mutable global seal activation cannot grow a later unsafe Finalize write.
  Transport or cleanup failure after commitment cannot turn it into a refusal.
- Local evidence: complete Hundred families, all12 stationary seal bounds and
  all14 captured-seal controls passed within private/public2164/2164. Prior-frame
  references AND cloned bytes are checked; existing protobuf goldens remain.
- Limits: whole2353/2354 multi-step receivers remain uninstalled/held. Add/remove,
  ranking/loading and raw public writers are not made globally transactional.

### FSB-019: Reject corrupt or foreign equipped-rune ownership safely

- Owners: WornObjects, named effects and equipped-slot/count queries. Require
  constructor/current client binding, genuine Player/equipment ownership,
  complete authoritative/cache membership, nonzero unique UIDs and unique
  equipped physical positions. Inactive0/211/212 may legitimately repeat.
- Missing/null/wrong-ID metadata grants no named effect and cannot crash the caller;
  valid other runes still work. Keyword matching is ordinal-ignore-case, not
  host-culture dependent. Invalid queries clear out/ref results. Equipped count
  does not wrap. Old tests demanding crashes, foreign-owner effects or duplicate
  slot bonuses were replaced with refusal/no-repair controls and valid fixtures.
- Local evidence: full Rune/query/perfection/XP/related admission families and
  new ownership, metadata, culture, cache/slot controls passed within2164/2164 in both trees.
  The small AddFlag null-Rune guard only prevents that dereference/stale bonus.
- Limits: full AddFlag transaction, Rune Add/Count/Score, family-to-slot policy,
  raw-writer races, inventory transfers and persistence remain separate work.

### FSB-020: Stage bounded shop refresh, counters and saved-book loading

- Owner: native ExchangeShop component. Complete book/catalog validation and
  finite uniform ordered sampling replace the unbounded rejection loop and
  prefix removal. Refresh/Reset, checked counters and strict append loading
  publish one prepared dictionary under the cooperating method gate; escaped
  previous rows/books remain unchanged. Valid zero-count retirement is retained.
- All cooperating writers/save enforce explicit4096-row/1MiB-text engineering
  limits; these are not client protocol constants. Open requires the genuine
  current shop/player, prepares complete2317 bytes and checks real allocation
  and native send bounds before output. True means publication attempted, never
  delivered; false precedes caller-buffer writes.
- Local evidence: full shop family including143 authored refreshed/new controls
  passed in private/public2164/2164. Real comparer materialization faults preserve the
  old book; sealed wire goldens and prior-frame bytes remain covered.
- Limits: sampling distribution/order is preserved, not the old PRNG draw trace.
  No PRNG rollback, whole paid refresh/purchase receiver, wallet/item grant,
  SQL/file crash recovery or globally synchronized raw fields is claimed.

### FSB-021: Count down to the actual local daily boundary

- Owner: ExchangeShop.GetTime. Original arithmetic targeted midnight+60seconds
  using three Now samples and large doubles, while Server.Reset follows the
  local DayOfYear boundary. Use one Now sample and bounded integer ceiling of
  remaining day ticks:1..86400 seconds. Keep UInt32 protobuf field4/opcode2317.
- Local evidence:26 deterministic literal calendar/subsecond/kind/extrema cases
  and retained real-clock/wire/shop tests passed in private/public2164/2164. Old tests
  requiring the extra minute were corrected, not retained as gameplay rules.
- Limits: calendar-wall-time countdown, not monotonic/DST elapsed-time accuracy,
  scheduler completion or an atomic daily-reward/reset transaction.

### FSB-022: Validate every character in the existing name repertoire

- Owner: NameStrCheck, used by the genuine Clan creation query. The source regex
  examined only the final character, accepting invalid interiors such as A B/A@B.
  Remove only its trailing anchor: retain exact ASCII/Arabic/CJK ranges, allowed
  punctuation, case-sensitive reserved tokens, UTF16 size bound and size opt-out.
- Verification: private/public Release2596/2596,0fail/0skip includes40 new
  controls, all262144 bounded UTF16-unit/position observations, existing name/API
  controls and the full Clan family. Old invalid-interior acceptance assertions
  now require refusal. Valid names and real Clan dictionaries/counter stay intact.
- Limits: no stored-name migration/deletion, normalized uniqueness, rename-queue
  validation or admin-authority claim. Those portions of FSB022 remain open.

### FSB-025: Widen inclusive RNG range arithmetic before taking a draw

- Owner: RandomLite bounded Next overloads. Full signed width2^32 previously
  overflowed to zero and threw after advancing the RNG. Compute width and final
  offset in Int64; retain guards, constant-range no-draw behavior and the genuine
  unbounded generator. Current Chi type/stat bands keep their seeded sequences.
- Verification: private/public Release2646/2646,0fail/0skip, including44 complete
  RNG controls (31 new), six seeds, signed width boundaries through2^32 and an
  independent BigInteger mapping. The old crash-enforcing test now requires the
  literal corrected seed97 result -1874220851 and next draw512644276.
- Limits: bounds safety only. The existing31-bit domain cannot cover a full-width
  interval and modulo remains biased. Uniform/full-domain sampling, fairness,
  concurrency and cryptographic security are not fixed or certified.

### FSB-023: Prepare real flag state and packets before commitment

- Owners: Player AddFlag/AddSpellFlag and the physical status-vector family.
  Validate the complete real dictionary/bitmap/row graph, current client/View,
  bounds and timer arithmetic before changes. Prepare COW state and genuine
  bounded2176/2215 frames; duplicates cannot reset XP or consult optional Rune.
- Opaque one-use plans keep staged mutable arrays private. Final typed commitment
  checks player/client/vector/View/UID AFTER comparer-dependent validation.
  Defensive cache arrays are not authority for death-clear. Invalid membership
  is explicit unavailability, never ordinary absence granting permission.
- Verification: Private/public Release1931/1931 each,0failed/0skipped.
  Independent native status/timed goldens, all XP flag branches, real pool/seal/
  alias faults, cache/timer/index/plan replay and late-owner controls retained.
- Limits: cooperating gates, not tuple hardware atomicity or arbitrary raw-writer
  exclusion. Remove/Clear/HP/death/XP workers, full cast/economy transactions,
  nonplayer postcommit sends, durable recovery and host adoption remain open.

### FSB-026: Potion rejection must not grant bonuses or leave dirty timers

- Original Player3145..3158 enabled a potion before date/flag/message preparation
  could fail and always claimed30 minutes. Both real methods now prepare their
  positive duration, flag state and bounded genuine SYSTEM talk before commitment.
  Message text reports the supplied duration; existing valid flag rows stay intact.
- Verification: Private/public Release1931/1931 each,0failed/0skipped.
  Includes missing/null/small/disposed/aliased real rentals, malformed state,
  no-output/no-bonus rejection, largest positive int duration, genuine full codec
  comparison and final-commit ownership changes. Old dirty-prefix tests corrected.
- Limits: no item-consumption caller is installed; expiry workers, raw races,
  durable potion state and delivery acknowledgement remain unproven. A delivery
  failure after commitment does not undo accepted potion state.

### FSB-027: Checked current-owner XP-duration adjustment

- Original IncreaseXPDuration reselected mutable state and added unchecked into
  an escaped row. Capture one full genuine graph, select the existing14-priority/
  Normal policy once, check signed sum/deadline, recapture after comparer work
  and verify exact owner/UID/vector before the single duration assignment.
- Verification: Private/public Release1931/1931 each,0failed/0skipped.
  Retains valid signed reductions/row/timer identity and adds44 boundary/ownership/
  malformed/replay controls. The old successful overflow expectation is replaced.
- Limits: full receive/XP opening/counter-goal/death/worker transactions remain
  separate; raw escaped rows and concurrent writers are not made globally atomic.

### FSB-028: Physical damage cannot dirty outputs or shared spell data on admission

- Whole OnMonster now validates real current status and all transitive query
  bounds before out assignment, RNG, procs or calculation. The floor branch
  retains its genuine player/status-free Damage2 behavior.
- The effective spell minimum10 is local; never write a caller's shared Magic
  catalog row. Valid float/NaN/infinity, gear branches, damage/RNG and2319 wire
  controls are retained; two players can reuse the same unchanged catalog row.
- Verification: Private/public Release1931/1931 each,0failed/0skipped.
  Includes44 new physical/call-site controls plus corrected old mutation assertions.
- Limits: admission-time protection and catalog integrity, not a whole cast/HP/
  death/resource transaction. Later owner faults and raw/lifecycle races stay open.

### FSB-029: Refused status is not a target, partial ranking or success animation

- AI stages target selection and clears stale Target/sets Idle when required flight
  membership is unavailable, before movement/stamps/RNG. Rank appearance resolves
  full flag priority before copying any Entry field. Existing successful order stays.
- Known-absent Shield/Stigma must pass real AddFlag before success animation.
  Sage distinguishes valid duplicates from refused grants, checks current bindings
  after membership and disposes the genuine archive rental synchronously.
- Verification: Private/public Release1931/1931 each,0failed/0skipped.
  Real malformed-tail callers, prior frames/buffers and existing valid packet/
  appearance/selection goldens remain; corrupt fixtures are not silently repaired.
- Limits: full rank publication, map enumeration/AI movement,
  complete refinement procs/casts/host recovery remain open. Prepared Sage and
  Shield/Stigma successors are recorded separately below.

### FSB-030: Commit required XP goals and load complete goal records safely

- Original Player.XPCount assigned20 before required HeroRewards goal103 could
  fail; the native goal loader also published valid prefixes/default-zero rows.
  Stage a complete bounded goal book, preserve valid row/claim aliases, revalidate
  actual current owners and commit the required goal with the counter under the
  genuine client/player/goal/book gates. Other XP values do not read goal owners.
- Reject malformed counted records before merge; preserve valid order/duplicate
  rules, full uint IDs/byte claims and genuine before-Player constructor timing.
  Widen stage-count arithmetic so uint.MaxValue cannot wrap into a free completion.
- Verification: private/public Release **2392/2392** each,0failed/0skipped;
  includes the186-case lifecycle/goal selection and70 new real-owner controls.
- Limits: in-process cooperating publication, not durable reward claims, SQL,
  complete XP/death workers, arbitrary raw writers or host adoption. Active reward
  grant safety still needs the real shared item-UID authority described in FSB-009.

### FSB-031: Prepare complete Sage status/archive before committing its flag

- Original Ninja.SageMode added its flag before archive preparation could fail.
  Prepare both genuine2176 frames and distinct bounded real loans first. Typed
  commitment binds the exact current capture, opaque token, staged words, Sage273
  policy and full uint archive timer. Valid duplicates retain their prior row.
- Final checks reject retired owners, disposed loans and changed Size/cursor/
  bytes. Failed delivery after commitment is not an uncommitted rejection.
- Verification: private/public Release **2392/2392** each,0failed/0skipped,
  including104 Sage controls, independent wire/seal/full-uint goldens,12 real
  final-buffer drift cases and17 mismatched-plan cases.
- Limits: no complete cast/host/durable replay or delivery acknowledgement;
  raw asynchronous disposal/field changes after final checks remain separate.

### FSB-032: Prepare Shield/Stigma animations before status acceptance

- Original effects could grant status before animation lookup/serialization or
  caller-buffer validation failed. Prepare the complete genuine animation and
  status frames, distinct real loans and caller preimage before opaque typed
  acceptance. New Stigma90/MagicShield40 and duplicate empty-target behavior stay.
- Successful operations retain caller2239 bytes/Size/cursor4 and2176→2239 order.
  Rejection preserves complete caller/state/XP/prior queues; accepted transport
  loss never refunds the grant or creates a retryable false result.
- Verification: private/public Release **2392/2392** each,0failed/0skipped;
  109 new cases plus retained valid effect and full generic animation goldens.
- Limits: these effects, not resource admission/full casts/HP/death, durable
  persistence, arbitrary raw races or per-recipient delivery transactions.

### FSB-033: Revalidate physical state and prepared loans after comparer work

- The sanitized layer still checked some data before its last real dictionary
  comparer call. A normal-returning comparer could then change an earlier status
  row/bitmap or a retained packet's Size, allowing stale authority or native send
  overread. Pure full-graph checks now follow final lookups; membership answers
  must agree with literal captured keys. Timer permission/renewal is guarded too.
- AddFlag/AddSpellFlag and potion typed commits also validate captured allocation,
  actual capacity, exact prepared Size/cursor/bytes after comparer work. Duplicate
  potions validate their message after the final query. Existing valid packets stay.
- Verification: private/public Release **2392/2392** each,0failed/0skipped,
  including118 final-state and54 ordinary flag/potion loan-drift controls.
  The unsafe unmanaged overread baseline was not executed.
- Limits: scoped genuine component operations, not all native packet IO, hostile
  comparers mutating new staged books, raw concurrent lifetime/ABA or full host
  recovery. General reader/writer audit FSB-012 remains open.

### FSB-034: Publish complete owned rune storage without count wrap or cache escape

- Original Rune.Add inserted before allocating its cache; null/UID/cache faults
  and escaped array edits could leave contradictory owners. Count wrapped at256.
  Stage the genuine comparer-preserving dictionary/cache, validate complete row
  identity and current assigned client/Player/UID, then publish both references.
- Count is exact int (no native production/wire consumer); Objects returns fresh
  defensive arrays, including empty arrays, retaining valid item aliases/order.
  Malformed raw queries are explicitly unavailable; they cannot grant free slots.
  Equipped readers join the same cooperating gate order and whole-graph checks.
- Verification: private/public Release **2896/2896** each,0failed/0skipped;
  includes56 new storage controls, retained source-valid calculations/position
  policies,32 concurrent Adds and broader item-loading/save consumers. The first
  local run exposed an empty-array reuse regression; implementation was fixed,
  keeping the original valid constructor assertion unchanged.
- Limits: no new Equip/Unequip/economy/SQL/host path, full loader transaction,
  global item-UID ownership or arbitrary raw-writer isolation. The unsafe native
  save truncation/partial-write owner is being replaced separately, not certified.

### FSB-035: Prepare complete refinement procs before status, chance or stamina effects

- Owners: native MirrorOfSin, KillingFlash and LightOfStamina. Old methods could
  emit a success refinement before invalid status was discovered, consume chance
  before known preparation failure, or change stamina before its scalar existed.
- Complete genuine captured status/owner state and bounded native2319/2176
  refinement/status/scalar frames now precede acceptance. XPList remains20seconds
  without XPCount reset; Light still restores only values below100. Successful
  native packet order, predicate priority and one genuine eligible Killing draw
  are retained. Invalid state cannot contribute a grant, stamina change or prefix.
- The eligible Killing loss now borrows three potential-success packets rather
  than one: an explicit bounded-allocation tradeoff, not a speedup. Post-draw raw
  mutation may still refuse; no RNG rollback is claimed. Accepted transport loss
  does not turn a committed grant into a retryable rejection.
- Verification: private/public Release **3065/3065** each,0failed/0skipped, including116 new
  controls, retained proc-rate/wire/visible-peer goldens and the corrected old
  test that required a refinement prefix before a NullReferenceException.
  The selection also includes53 genuine owned-file UID registry controls.
  FullWindows39958461749 on exact256d7a18 passed: core23176/0failed (165excluded),
  updater301/301, launcher49/49; this is offline proof, not host activation.
- Limits: no general CheckEffects activation, full HP/death/cast transaction,
  raw-writer isolation, live client or packet delivery atomicity. Other refinement
  procs and host recovery remain unfinished.

### FSB-037: Prepare a complete native item image before atomic destination replacement

- Owner: complete native SaveClientItems. Original FileMode.Create truncated
  before row enumeration/conversion, ignored failed writes and could leak its
  open handle after a later owner/row failure. Nested deposits were silently lost.
- Capture the real current client/Player/UID/configured root and complete owned
  containers/rows. Prepare checked184-byte item and80-byte perfection image first;
  stage in the same directory, flush/close/verify, recheck the exact baseline and
  owners under the genuine file coordinator, then Replace or no-overwrite Move.
- Distinct duplicate UIDs and unrelated repeated physical owners reject. Only
  the source-proven exact wardrobe/equipment indexes of one physical row emit
  once. Reject unsupported nested deposits instead of discarding grandchildren.
  Preserve native flat ordering, ownerless deposited-perfection predicate and
  independent successful bytes. The obsolete unsafe SaveOpenedFile is retired.
- Ordinary rejection requires unchanged destination proof; unknown replace
  outcome fences that canonical path in this owner. The void entry cannot
  silently acknowledge a failed save. Own validation InvalidDataException is
  explicitly classified; it does not derive from IOException.
- Verification: private/public Release **3116/3116** each,0failed/0skipped,
  including51 new save/text controls, genuine Windows replace-sharing denial,
  late shared-file-gate retirement, retained native ABI/reader/roundtrip goldens.
  The initial local run exposed the validation-exception omission; fixed in
  production without weakening rejection/no-prefix assertions.
  FullWindows39959714814 on exact01431fe3 passes: core23227/0failed (165excluded),
  updater301/301, launcher49/49, all8stageexit0. This remains offline component proof.
- Limits: one-file component, process-local fence only. Unknown post-replace
  fault classification is source-reviewed, not a fabricated runtime proof.
  Full loader/host/SQL/multi-file recovery, raw writers, hostile path ABA and
  power-loss/directory durability remain open. No original save/client was used.

### FSB-038: Bound native fixed32 text and reject null records explicitly

- Owner: native item perfection record. Unterminated getters could read beyond
  their buffers; oversized setters overran them and shorter text kept stale tails.
- Getters stop within32 bytes. Setters reject null/oversize before any write,
  accept full32, retain genuine low-eight-bit/Windows ANSI behavior and clear
  unused field bytes. No UTF8 guess, truncation or struct-layout change.
  Factory/ApplyTo reject null arguments explicitly before stores.
- Verification: all34 perfection-record cases pass within both **3116/3116**
  selections, including13 new boundary/field-isolation cases. Valid80-byte
  layout/offset/raw/header/ANSI/factory assertions remain; unsafe tail/NRE
  expectations and the dependent null-recipient assertion are corrected.
- Limits: whole reader/postlude/load transaction remains separate audit work;
  bounded accessors alone do not authorize an item or make partial loads safe.

### FSB-039: Prepare selected DivineGuard before cooldown or success publication

- Owner: complete native selected DivineGuard proc. The source reset its stamp
  and sent refinement11 before fallible status/view work; an old test required
  that dirty prefix. Invalid preflight now preserves stamp/status/resources,
  consumes no chance draw and sends no success frames.
- Capture the genuine client/player/vector/map/view and prepare all bounded,
  disjoint real frames before the genuine absent-flag5% draw. Final pure owner,
  status and loan checks precede plain commitment. Preserve flag177/15seconds,
  selected-trigger now+10minute stamp even on loss/present, and exact owner
  refine/status/Azure versus peer refine/status order. Present skips RNG.
- Verification: private/public Release **3214/3214** each,0failed/0skipped,
  including98new controls and14 retained ProcRates cases. Seeded win/loss,
  malformed owners/status, actual loan corruption/disposal, stale state,
  token replay, independent wire goldens and accepted send failure are checked.
  The previously deferred testconstant parentheses joins this behavior batch.
  FullWindows39962905914 on exactaf3ca964 passes: core23325/0failed
  (165excluded), updater301/301, launcher49/49, all8stageexit0.
- Limits: no full outer CheckEffects authorization, combat/HP/Team/revive or
  host activation. Eligible loss prepares three potential-success loans; this
  is a safety tradeoff, not a speed claim. No RNG rollback, raw-writer global
  atomicity or confirmed network delivery is claimed. Offline only.

### FSB-040: Stage complete native quest files and canonical indexes

- Owner: complete RoleQuests Save/Load. The source truncated before validating,
  ignored short I/O, leaked load handles and published partial/default/split-alias
  rows. Unsafe opened-file entry points are retired after migrating every caller.
- Save captures the genuine constructor-bound book/player/UID/absolute root,
  validates complete physical rows and Accepted subset, prepares bounded native
  bytes and uses real same-directory flushed atomic replacement with baseline
  checks. Unproven write outcomes fence this path locally; void wrappers cannot
  silently acknowledge rejected saves. No new quest owner is fabricated.
- Load validates the entire counted image and tail before detached merge. Keep
  nonconflicting rows and exact identical overlaps as canonical physical aliases;
  reject duplicate/conflicting UIDs and invalid indexes without publication.
  Preserve raw status bits, native enumeration, UID0 rows and more-than-five
  accepted persistence; no new gameplay cap. Validate the merged64MiB bound and
  physical indexes independently of caller comparers before reference commitment.
- Verification: private/public Release **3465/3465** each,0failed/0skipped.
  Retained byte goldens, first-save/reload, real Windows sharing denial, released
  handles, truncation/tail/count faults, changing-hash/collapsing comparers, owner
  drift and oversized file/merged-book controls pass. The first run exposed
  DirectoryInfo's missing sentinel; production now reads actual attributes and
  reports genuine missing directories as NotFound without hiding access errors.
  FullWindows39965399561 on exact0b0da909 passes: core23360/0failed
  (165excluded), updater301/301, launcher49/49,8stageexit0.
- Limits: offline one-file component and cooperating gates, not host/login/SQL,
  all quest/objective/award transactions, multifile/crash recovery or atomic raw
  observation of two public maps. Raw writers/path ABA remain open. Unknown
  post-replace classification is source-reviewed, not invented runtime proof.

### FSB-041: Debit only the admitted bound type of rune fragments

- Owners: direct six-fragment UseItem and NPC RuneFragments combine. Native
  Inventory.Contain checks ItemId+Bound, but Remove loses the binding predicate;
  the replacement inherited that mismatch and one test required the wrong debit.
- Both debit paths now select exactly the binding admitted by their count. The
  direct path retains per-unit updates; the NPC retains per-stack batch order
  and uses the selected row's staged auto-binding. Opposite-bound stock stays
  untouched; nineteen matching fragments cannot borrow a twentieth from it.
- Verification: private Release **236 passed/0 failed/0 skipped**; independent
  sanitized public **229 passed/0 failed/5 existing external-fixture skips** of234.
  All12 new controls pass in both trees. Registered direct ingress and real NPC
  public-owner fixtures check complete item images, disk reload, refusal identity,
  unchanged original rows, exact20-fragment cost and retained reward/wire order.
  The corrected old assertion genuinely failed before the production change.
- Full [Windows39970166492](https://github.com/vgcman16/wolfconquer/actions/runs/39970166492)
  passes4m44s on exact27e2ae9b: core23372/0failed (165externalfixtureexcluded),
  updater301/301, launcher49/49,8stageexit0,204build warnings. CodeFactor1NEW
  duplication/0FIXED,total2294; defer that cleanup to a coherent behavior batch.
- Limits: matching debit selection only, not whole reward/persistence atomicity,
  globally durable UID allocation, registered NPC ingress or live-client proof.
  The12 controls are additional to retained old cases; no balance values changed.

### FSB-042: Prepare complete quest counters and caller frames before mutation

- Owners: all three native Set/Increase objective operations. The source changed
  live arrays before unsafe serialization, wrapped unsigned counters and trusted
  split source/Accepted aliases; older tests explicitly accepted these defects.
- Require the genuine current constructor client/player/book and cooperating
  gates. Capture the complete canonical physical graph independently of custom
  comparer callbacks; reject shared nonempty mutable arrays, permit harmless
  shared empty arrays. Copy input and retained values, check every addition and
  prepare complete2136 bytes plus actual caller allocation/preimage before writes.
  Commit one canonical row array and the bounded caller image, then publish.
- Verification: private/public Release **292/292** each,0failed/0skipped.
  Includes70 new cases and19 retained/repaired objective cases, plus neighboring
  native item/quest/persistence controls. Exact native UID35024/35007 tails,
  stable ASCII/multibyte seals,1032-byte send/actual allocation boundaries, late
  overflow, null/disposed callers, input alias isolation and full-book faults pass.
  Legitimate missing/finished/inactive no-ops and nonshrinking tails remain.
- Full [Windows39971090546](https://github.com/vgcman16/wolfconquer/actions/runs/39971090546)
  passes4m50s on exact8e5ff255: core23442/0failed (165externalfixtureexcluded),
  updater301/301, launcher49/49,8stageexit0,204build warnings. CodeFactor4NEW
  (3complexity+1precedence)/1FIXED,total2297. The precedence-only parentheses fix
  is batched with the shop-capacity change; complex validation stays unchanged.
- Limits: actual native objective callers are not installed; this is component
  proof, not login/host/client activation or full quest award/save transactions.
  Raw public graph/packet writers, disposal races and delivery recovery remain
  open. A failed send after commitment is not an uncommitted refusal or refund.

### FSB-043: Refuse modeled shop purchases that cannot fit in inventory

- Owners: normal BuyItem and Race/Honor/Champion point shops. Current routes
  appended new rows and debited wallets without required capacity admission.
- A shop-only bounded check admits the complete modeled row count before costs,
  session, packets or persistence: normal uses its normalized full count;
  point shops require one row. Reject full40/already-overcap41 bags and do not
  truncate a requested normal purchase. Shared creators/returned awards are not capped.
- Verification: genuine registered-handler baseline **18 failed/15 passed** of33
  reproduced spending/changed sessions on overcapacity. After the four guards,
  private Release **334 passed/0 failed/0 skipped**; independent public
  **325 passed/0 failed/9 existing external-fixture skips** of334. All33 new
  controls pass; old shop checks and292 quest/component controls are retained.
  Verify exact wallets, untouched refusal files/rows, complete saved/reloaded
  positive images and packet order across gold/CP/boundCP/Honor/Race/Champion.
- One observed CodeFactor precedence finding is fixed mechanically in the
  objective length formula and checked by the retained292 cases in this batch.
- Full [Windows39972176836](https://github.com/vgcman16/wolfconquer/actions/runs/39972176836)
  passes4m17s on exacta92b1fb0: core23475/0failed (165externalfixtureexcluded),
  updater301/301, launcher49/49,8stageexit0,205build warnings. CodeFactor2NEW
  complexity/1FIXED (the observed precedence),total2298. The new xUnit2017
  assertion-style warning is corrected with the FSB044 behavior batch, not its own CI.
- Limits: current normal owner remains all-or-none/one-row-per-unit; original
  stacking/partial affordability, plus/activation/quest hooks and full wallet/
  file/session transactions remain open. No global UID/host/client proof.

### FSB-044: Prepare canonical quest acceptance before publication

- Owners: native `Accept` and `AcceptKingDomMission`. The reference modifies
  shared quest metadata for1469/409, publishes ordinary indexes separately, and
  constructs/sends the packet after those writes. A diagnostic failure can also
  escape a successful acceptance and invite an incorrect retry.
- Validate the genuine current client/player/book and complete canonical graph;
  use local effective intention counts without changing the catalog. Bound the
  projected persisted image before allocation. Prepare the real2120 packet and
  comparer-preserving private maps, then recheck physical state after the final
  comparer before publishing. Ordinary maps share one canonical row; Kingdom
  retains its source-only policy and existing finished/out semantics.
- Only exceptions escaping the real postcommit Send are contained. Accepted
  maps/out remain committed and return true; precommit errors remain errors.
  No rollback, retry, fabricated successful delivery or recursive logging occurs.
- Verification: private and independent public Release each **418/418 passed**,
  zero failures/skips. This includes75 new acceptance cases, two genuine Windows
  child-process delivery faults, and341 retained quest/packet/console/shop cases.
  Tests-first unguarded delivery baseline failed **2/2** specifically on the
  escaped TypeInitializationException after all committed-state assertions passed.
  Child OS handles are restored; cached real console initialization failure is
  proved afterward. No main process handle/logger or game session is changed.
- One xUnit2017 Shop fixture assertion is corrected mechanically in this batch.
- Full [Windows39975270475](https://github.com/vgcman16/wolfconquer/actions/runs/39975270475)
  passes4m11s on exact39eaaf458e4526555de2e682298b94c80ff172c2: core23552/
  0failed of23717 (165externalfixtureexcluded), updater301/301, launcher49/49,
  8stageexit0,204build warnings. Receipt20261023T150622623Z-73fd3de4 finished
  15:10:11.1538337Z. CodeFactor3NEW complexity/0FIXED,total2301; no score-only rewrite.
- Limits: native acceptance callers/host are not activated. FinishQuest, full GUI,
  awards, automatic disk/SQL saves, raw competing writers, delivery recovery and
  whole quest lifecycle remain open. No live client or original save was used.

### FSB-045: Aggregate exchange material costs before preparing a purchase

- Owner: registered Goods exchange route. Repeated material IDs checked the same
  stock independently; the shared consumer could silently pay only a prefix and
  still award the reward. Checked quantity multiplication could escape as an error.
- Check UInt32 products and repeated-ID sums, then validate complete captured
  stock before preparing any debit/reward. Consume only private rows and frames
  in original definition/physical-stack order; any unpaid remainder discards the
  entire plan. Leave the shared helper's25+ unrelated callers unchanged.
- Tests-first baseline: **10 failed/15 passed** of25. Three failures demonstrate
  genuine changed sessions on unpaid duplicate costs, two are escaped product
  overflows, and five are diagnostic-classification differences rather than
  additional economic exploit proofs.
- Verification: private Release **446 passed/0 failed/0 skipped**; independent
  public **442 passed/0 failed/4 existing external-fixture skips** of446. All25
  new finite registered-handler cases pass, including7 success/order cases with
  complete saved/reloaded native-image comparisons and18 refusal cases. Retained
  exchange, shop and native quest controls remain. No old test was removed.
- Existing count/shop/amount/reward/capacity guards, active exact-ID/mixed-binding/
  zero-stack-as-one policy, reward values, per-definition update/remove frames
  and reward/echo order remain. The handler extraction is a semantic material
  phase boundary, not a complexity-score rewrite.
- Full [Windows39976822207](https://github.com/vgcman16/wolfconquer/actions/runs/39976822207)
  passes4m05s on exact8abeec78727a476675861f4853f87e3a1cd4c444, completed
  15:21:24Z: core23577/0failed of23742 (165externalfixtureexcluded), updater301/301,
  launcher49/49,8stageexit0,204warnings. Receipt20261023T151734524Z-050a9ec1.
  CodeFactor3NEW (complexity,blank-line,precedence)/1FIXED old complexity,total2303.
  The2 mechanical corrections join the next behavior batch, not a separate CI.
- Limits: current creator UID policy and unconditional session publication are
  unchanged. Global UID activation, raw/cross-await writers, duplicate physical
  ownership, save-before-publication and durable multifile economy remain open.
  GoodsEx donation/selection and native interchangeable-material/binding parity
  are separate work; no live client, SQL or original save was used.

### FSB-046: Drain late-accepted TCP clients before shutdown completes

- Owner: local ConquerTcpServer.DisposeAsync, not a decompiled gameplay rule.
  The client-task snapshot preceded cancellation and accept-loop completion.
  A client accepted concurrently could be registered after that snapshot, so
  shutdown returned while its real frame handler/transport cleanup still ran.
- Cancel and stop listener admission, await the actual accept producer, then
  capture client tasks under the existing gate and await their real completion.
  Wire formats, handling order and awaited transport-close ownership are unchanged.
- Tests-first baseline:3 passed/1 failed of4; the genuine late-accepted handler
  was still held when Dispose had already completed. Scheduling uses the actual
  registration gate and real loopback socket, not injected task-list entries.
- Verification: private and independent public Release each25/25, zero failures
  or skips, including all4 new shutdown controls and21 retained networking/
  disconnect cases. Held handler and asynchronous transport cleanup both delay
  shutdown; peer EOF follows cleanup. Never-started/empty listeners are covered.
- Limits: normal single-owner shutdown only; concurrent/repeated disposal,
  failing cancellation/log callbacks, all detached runtime producers and full
  borrowed-Host lifetime are separate work. This is not global quiescence,
  UID activation, deployment or actual-client proof. FullWindows39986796881
  passes4m41s on exact93e7259168ad0a4d2eb0718f8c0db6af41261200, completed
  16:31:18Z:23593corepassed/0failed of23758 (165externalfixtureexcluded),
  updater301/301, launcher49/49,8stageexit0,204warnings. Receipt
  20261023T162652064Z-8218bc29 finished16:31:10.4777032Z.

### FSB-047: Spend the pack row that actually passed admission

- Owners: SacredRefinery, P7Soul, Steed and P7SacredTreasures pack consumers.
  Admission required a positive stack in the selected binding class, but debit
  selected an earlier matching ID regardless of binding and/or positive stock.
  This could convert the wrong binding class or falsely free inventory capacity.
- Debit now selects the first positive eligible row in original inventory order.
  Colored Steed retains its existing nonzero-bound class; other listed costs
  remain unbound. Reward values, CP costs, RNG, update/remove/reward frame order,
  shared consumption helpers and post-debit capacity policy are unchanged.
- Tests-first baseline:239 total,213 passed/26 failed. Ten corrected old unsafe
  expectations and16 new zero-stock/false-capacity controls fail on old code.
  The initial fixture-only compile failure executed no tests; setup/import fixes
  preserved every assertion before that real baseline. No tests were removed.
- Verification: private and independent public Release each239/239, zero failures/
  skips:205 retained cases
  (including10 repaired unsafe expectations) plus34 new controls. Full item
  metadata, saved/reloaded images, exact debit frames, first-eligible order and
  full40-slot rejection are checked.
- Limits: bounded selector repair, not whole economy atomicity, duplicate-UID
  validation, concurrent raw-writer/session safety, multi-file recovery, global
  UID activation or live-client proof. FullWindows39989920933 passes4m37s on
  exact8411bb619ec53477a6eb035aa96db29c45c0e4d9, completed16:54:13Z:
  23627corepassed/0failed of23792 (165externalfixtureexcluded), updater301/301,
  launcher49/49;8stageexit0,204warnings. Receipt20261023T164951218Z-143565ae
  finished16:54:06.1546457Z.


### FSB-048: Keep adjacent pack debits in their admitted binding class

- Owners: required-pack Black Friday catalog grants, Dazzling Diamond Box and
  the five-Spirit Phoenix exchange. Their unbound-stock gate was followed by
  ID-only debit. Phoenix also included empty rows in its per-pass targets.
- Capture positive unbound costs in original physical order. Phoenix filters
  both pass snapshots and current targets, preserving eligible round-robin order
  and exactly five cost frames. Black Friday/Diamond rewards, CP branches, RNG,
  capacity policy, UID API and debit/message/reward ordering are unchanged.
- Tests-first baseline:135 total,119 passed/16 failed. Five old unsafe debit
  expectations and11 new preserved-row cases reproduce the bug on unchanged
  production. All105 retained intentions remain, plus30 modular new controls.
- Verification: private and independent public Release each374/374,0failed/
  0skipped across all seven pack
  families (this135 plus retained FSB047239). Full item preimages, genuine saved/
  reloaded images, exact frame sequences, first-eligible/round-robin order and
  insufficient eligible stock are covered.
  Phoenix's old max-plus-one fixture reward UID changes21 to11 when the correct
  eligible20 is consumed and foreign10 survives; no payout or allocator tuning.
- Limits: bounded selectors, not duplicate-UID/raw-writer/current-session safety,
  multi-file/economy/outbox recovery or canonical UID activation. Future UID
  producer predicates are authored separately; genuine legacy fixture migration
  remains open. FullWindows39991776324 passes4m57s on exact
  918899075bce5d8ddc1d0e8127d8c1cb87c9d810, completed17:08:04Z:
  23657corepassed/0failed of23822 (165externalfixtureexcluded), updater301/301,
  launcher49/49;8stageexit0,204warnings. Receipt20261023T170347775Z-bb22ad8a
  finished17:07:57.7038917Z. No live client, SQL or original-save action.


### FSB-009 (partial): Prepare Super Guild War rewards before consuming claims

- Owner: the sole registered SuperGuildWar TryClaimReward caller. The in-memory
  claim pool/UID marker changed before item creation, checked wallet arithmetic,
  attribute copies and complete save/frame preparation. A later fault could lose
  the claim or expose partial rewards; retrying the same UID could not recover it.
- Prepare the private complete candidate, persistence values/item image and exact
  owner/dialog/broadcast frames before the unchanged real claim decision. Expected
  preparation faults leave claim, reward state, files and output unchanged. Grant
  reuses prepared values/bytes; no speculative claim/refund or accepting callback.
- Genuine registered2084 baseline **8 failed/4 passed** of12: every failure is a
  repaired SAME UID unable to claim after an earlier late fault, with no event
  reset/reseed. Four leader2-slot/deputy4-slot boundary controls already passed.
- Verification: private Release **468 passed/0 failed/0 skipped**; independent
  public **464 passed/0 failed/4 existing external-exchange skips** of468. All12
  new controls pass, plus the old5 reward/5 backend and446 retained cases. Full
  physical files, wallets/flags, claim-pool exhaustion and exact owner/broadcast
  frames are checked. Two exchange mechanical style corrections join this batch.
- Existing active payouts remain leader300000CP + tuned500000silver + one2100065
  item/flag151, deputy flag152 only. Source7770 documents the older model with
  50000000source silver; neither that amount nor the newer decompile's mail branch
  replaces the current approved tuning. No wire format or live routing changed.
- FullWindows39978653171 ran all12 guild cases successfully but failed one old
  RiseOfTaoism timer/log race (23588corepassed/1failed/165excluded). The fixture
  now waits for actual async completion; private/public focused15/15each pass.
  FullWindows39980179395 now passes4m15s on exact16763a82:23589core/0failed,
  165excluded, updater301/301, launcher49/49,8stageexit0,204warnings.
  This is NOT full FSB009 closure:
  Lottery cadence, unconditional session publication, claim+memory+two-file crash
  atomicity, durable outbox/reconciliation, raw writers and global UID activation
  remain open. No live client, original saves or SQL were used.

### FSB-049: Prepare Plume collection progress and its item together

- Owner: map1002/family2 quest1007 Turtledove Plume. The real mission requires
  collecting item711236, not independent kill credit. Old code published/saved
  objective progress before validating or preparing the item; missing metadata
  advanced2 to3 without granting a plume.
- Prepare checked private quest/item snapshots and complete objective/item frames
  first, then publish one combined in-memory candidate through the actual current
  session CAS. Missing definitions, invalid durability, objective/frame overflow
  and item creation overflow refuse before either half is published.
- Valid one-item reward and objective-before-item wire order remain. The existing
  quest save runs after CAS. Its ordinary I/O failure remains a committed coherent
  memory reward with an explicit persistence gap, not rollback or file atomicity.
- Tests-only baseline13:7passed/6failed. One unsafe old expectation corrected;
  three old valid/no-op controls retained, nine new controls added. Private Release
  complete boss/reward class now439/439, zero failed/skipped, including all13.
  Clean public Windows39998007520 passes4m39s on exact7c9e9a11a47fda490f43124b71587198ee9725d0:
  23687core/0failed of23852 (165externalfixtureexcluded), updater301/301,
  launcher49/49;8stageexit0,204warnings. Completed2026-10-23T17:52:55Z.
- Limits: existing local max+1 UID API, postcommit observer/cancellation/delivery
  failures, raw writers, cross-file recovery and whole-combat atomicity remain
  open. The isolated canonical UID equivalent separately passed17/17 within
  791/791 focused integration checks; that family is not activated in main.

### FSB-050: Stop false monster reward receipts and undefined luck charges

- Owners: Earth Force3646 full-bag response, Cloud Riding Scroll1337 reward and
  Deityland WarDevil reward notice. The recovered WarDevil branch never grants
  its announced treasure; it now refuses itemRewardDefinitionUnavailable before
  luck debit, reward RNG, treasure broadcast or effect. No payout was invented.
- Earth full inventory reports the existing capacity wording in the same
  MessageBox layout. Valid fragments/conversion stay unchanged. Cloud prepares
  the genuine grant and both complete frames before exact-current CAS, then
  retains MessageBox-before-item order and rechecks the committed owner before
  each send. No-owned-scroll remains a no-add branch without a false receipt.
- Corrected tests-only baseline25:4passed/21failed. Four unsafe old facts corrected,
  21 new cases added; complete private boss/reward class439/439, zero failed/skipped.
  Existing generic kill-CP no-award controls isolate the new scenes; assertions
  were not weakened. The unregistered connection stops at the real earlier
  ArrowBlades session refresh: its control requires no frames/no reward branch,
  not a fabricated later diagnostic. The passing full Windows checkpoint above
  includes all25 message controls as well as the retained boss/reward family.
- Limits: WarDevil grant enablement still needs an authoritative definition and
  real entitlement transaction. Current item creation, durable rewards/outbox,
  whole-kill atomicity, unrelated later writers and check/send races remain open.
  Observer retirement after Cloud CAS suppresses stale output, not claimed rollback.

### FSB-051: Prepare Earth Force conversion before fragment debit output

- Owner: map3998/family40906/quest3646, native MonsterRole2131..2160.
  Previously the fragment update and five-fragment removal frames could precede
  final Earth Force preparation; missing/malformed metadata or checked UID/
  durability failure could leave a prefix without the earned reward.
- Prepare the complete fragment/conversion inventory and all frames privately.
  Validate each required physical metadata row and bounded durability, require
  the exact current session, then CAS only the final inventory. Publish the same
  fragment/update/removal/Earth frames in order, checking the committed owner.
  After-commit notification failure is not a rejection or rollback.
- Actual tests-only32:10passed/22failed. One unreachable unregistered diagnostic
  was corrected to the real earlier ArrowBlades refusal, retaining all state/file
  checks and requiring EMPTY all frames. All32 then pass. Full private focused
  reward/rank/Status batch592/592, zero failed/skipped; all471 boss/reward cases
  pass. Valid0/3/4/5/6 and split-row conversions retain quantities, binding,
  independent full2116 bytes and consume order; FSB050 capacity wording remains.
- Limits: operation-local memory/output boundary, not whole-kill, durable game-file
  transactions, entitlement/outbox, delivery or global UID closure. Current
  max+1 remains until the separate canonical registry cutover; its equivalent
  overlay/real-ledger fixtures are isolated and not yet executed or activated.
  Clean full Windows40002265954 passes4m44s on exact549e9829c1a9095fd986fe5cce38c5be6106d5ee:
  23753core/0failed of23918 (165existing externalfixtureexcluded), updater301/301,
  launcher49/49;8stageexit0,212warnings/0buildErrors. Completed18:24:11Z.

### FSB-052: Unavailable rankings cannot authorize bonuses or spell removal

- Owners: complete combat-gear rank effects and the real MyCombatGear.Status
  caller. The prior query returned ordinary rank0 for absent/null owners/lists
  and could accept a winning prefix before a malformed tail. In particular an
  unavailable Pirate family could authorize removal of existing spell21930.
- Capture and validate every class-required physical family, including complete
  tails, under its real rank gates before producing detached UID ordinals or any
  Status rank mutation. Observed unavailable/drifting state throws explicit
  InvalidDataException; valid empty/nonmembership remains rank0. No repair,
  dictionary callback, new rank order, numeric formula or wire format is used.
- Independent real Status baseline8:2valid controls passed/6unsafe cases failed;
  after correction all45 Status cases and46 rank-effect cases pass within592/592.
  Seven old private exception-oriented cases now assert safe unavailability/no
  publication;20 other effect controls stay. Private/public export shapes differ:
  public retains its RankBonuses owner and all6 existing bonus tests, adds34
  controls, and does not import duplicate private mapping/query methods.
- Limits: prepared rank reads, not whole-Status/global rank transactions. Raw
  mutable owners/ABA, later non-rank failures, spell persistence, concurrent
  lifecycle and Host activation remain open. The clean full public CI above
  includes all34 new public rank/Status controls and6 retained bonus controls.

### FSB-053: Preflight selected status values before applying effects

- MyCombatGear.Status previously read selected named coefficients after rank,
  spell and earlier flag effects; a missing second bless row was explicitly
  tested to retain the dirty prefix. Now capture the same 22 predicates and
  their required scalar slots before any Status effects or packet rental.
  Missing/null/short active values refuse with InvalidDataException; inactive
  optional metadata stays unconsulted. Valid formulas, conversions, per-addition
  clamps, composite indices and output order are unchanged.
- Tests-only Status79:47passed/32failed. After repair, surrounding rank/query/
  Status176/176 and combined reward/status721/721 pass, zero failed/skipped.
  One unsafe old bless fact is corrected;34 additional controls compare complete
  stat/perfection/spell/queue/caller-packet/flag preimages.
- Limits: selected-input preparation, not a whole Status transaction, immutable
  raw owners, global lifecycle authorization, spell persistence or delivery.

### FSB-054: Prepare five nature-force grants before session publication

- Map3998 families40900/40902/40905/40907/40908 previously Set inventory before
  complete item-frame preparation and could overwrite stale owners. Prepare
  strict required metadata, items and full frames privately, CAS the exact
  current session once, and check the committed owner before item output.
  Loaded Effect256, malformed durability and UID overflow refuse unchanged.
  A committed observer retirement/send failure is not misreported as rollback.
- Restore the native eight-Rune-Fragment ceiling; retain quest/rate/capacity
  ordering, one unbound grant, stack UIDs, quantities and complete2116 goldens.
  Tests-only74:39passed/35failed; all74 pass within combined721/721, including
  all545 boss/reward cases. New predicate assertions were batched on intake,
  without exclusions, suppressions or protocol refactoring.
- Limits: in-memory grant boundary, not durable files/rewards, whole-kill atomicity,
  auto-route implementation, outbox/delivery or global UID safety. The separately
  composed canonical UID successor remains isolated and unactivated.

### FSB-055: Retain primary launcher-test failures during dispatcher cleanup

- Current CodeFactor CA2219 identified the launcher test helper throwing a join
  timeout from finally, hiding assertion/skip/operation failures. Capture the
  primary error, independently attempt shutdown/join, retain distinct observed
  cleanup errors, and rethrow one original error with its stack or aggregate
  primary-first. Already-safe updater aggregation is unchanged.
- Real STA tests-only8:5passed/3failed; repaired full launcher57/57 passes, zero
  failed/skipped. Held shutdown threads are always released and actually joined
  in fixture cleanup. No game/client execution or accepting fake dispatcher.
- Limits: bounded test-harness diagnostics, not gameplay or production shutdown
  completion; work still live beyond the helper deadlines is not certified.

Full FSB053/054/055 checkpoint: clean Windows40008366439 passes4m14s on exact
8843a6384f167b9e16a19b30e8d6b93c6a7efc74, completed2026-10-23T19:09:18Z.
Core23861passed/0failed of24026 (165existing externalfixtureexcluded), updater
301/301, launcher57/57; all8stages exit0,204warnings/0buildErrors. Parser20+13
controls are synthetic only. This does not close the explicit gameplay limits.

### FSB-056: Admit only positive unbound DragonSkin unlock costs

- `src/WolfConquer.Core/World/WorldPacketHandlers.AnimaSkin.cs` selected a mapped
  Unlock item without checking binding or quantity. Retrieve creates an unbound
  item, permitting bound-to-unbound conversion; an empty stack could also supply
  a skin. Select the first mapped row with `Bound == 0 && StackSize > 0`, retaining
  inventory order, the one-unit cost, other item fields and existing frame order.
  Native `DragonSkin.Process` uses `Inventory.Contain(mappedId, 1, 0)`; this is
  behavioral reference, not new client authority. Opcode 2415 is unchanged.
- Real registered-frame tests-first baseline: all 16 new cases failed before the
  repair. Repaired focused suite: private 70/70 and public 70/70 passed. Refusals
  cover bound-only/zero-only rows and nonzero binding 255, with truthful actual-UID
  `OK=false` Unlock/Retrieve replies, no debit/item frames, no skin and unchanged
  session, full item/character state, file bytes and write times. Mixed-order
  controls cover all three mappings and valid stacks of one/three after earlier
  bound/zero rows, preserving later eligible rows, metadata and exact success
  frames; saved item/skin results are checked.
- Historical FSB056 admission-only boundary (before FSB057): the real
  `WorldServerFrameHandler` session callback does
  autosave changed inventory. The explicit character skin save is separate;
  successful individual saves are not a coherent item-plus-skin transaction.
  Crash/replay recovery, concurrent ownership, persistence/send-failure atomicity
  and delivery remain open for the separate transaction work. This is admission
  proof, not full durability, a new full-CI checkpoint or live-client/deployment
  proof.

### FSB-057: Commit DragonSkin items and skin through the shared journal

- The managed Unlock/Retrieve path previously saved items and character skin
  separately. `WorldPacketHandlers.AnimaSkin.*` and `LegacyDragonSkinPersistence.*`
  now use the shared `LegacyTradeJournal` v2 single-owner transaction: Character
  is keyed by account (fixture 100), Items by character (1293600). Strict complete
  item-graph/skin-book and persisted baselines, current-owner persisted CAS,
  exclusive admission and recovery readiness for the exact roots precede commit.
  FSB056 positive unbound costs and existing successful frames remain. Retrieve
  retains zero durability for absent optional metadata; present malformed
  metadata refuses. Presentation changes also use current-owner CAS.
- Registered tests-only baseline: 89 total, 8 passed/81 failed. First admitted
  run: 186 total, 161 passed/25 failed; all 89 registered and 70 retained cases
  passed. The 25 of 27 component failures were raw `IReadOnlyList` collection-
  expression fixture wrappers correctly rejected by strict validation. Only
  `Fixture.Seed` changed to reload its just-written file with the real
  `PlayerItemSnapshotLoader`, plus a raw-byte preservation assertion; production
  validation was not weakened. Then 186/186 passed; broader actual Trade/barrier/
  session selection 368/368 passed, zero failed/skipped. The same 26 paths (15
  production, 11 new test) transferred to public, which passed 368/368 with zero
  failed/skipped in two seconds: 116 new + 70 retained + 182 adjacent cases.
- Controls cover interrupted 0/1/2-replacement prefixes rolling forward, sharing
  failure rollback/retry, stale owners, postcommit retirement/replay/transport
  close and presentation-CAS observers. Recovery is process-restart reconciliation,
  not power-loss, multiprocess, raw external-writer or ABA proof. Durable outbox/
  delivery and global UID safety remain open: the current creator is still max+1;
  the canonical alternative is not installed. No SQL/live/original-save action,
  routing change or Host activation.
- Full Windows [40013043779](https://github.com/vgcman16/wolfconquer/actions/runs/40013043779)
  passed on exact public `f2f122f2a9f337ec0bcb80332f1d93579c2868ca` in 4m11s:
  core 23993 passed/0 failed of24158 (165 existing external-fixture exclusions),
  updater301/301, launcher57/57, all8 stages exit0, build204 warnings/0errors.
  Receipt20261023T194003090Z-230b1736 finished19:43:51.8908154Z; job completed
  2026-10-23T19:43:59Z. Parsers20+13 are synthetic, no network/service probes.
  This is offline source acceptance, not deployment or complete gameplay parity.

### FSB-058: Debit only admitted positive unbound NPC exchange costs

- Owners: Arthur VIP quest rewards, EventUtility flower/Stardust/Buddha
  exchanges and Labyrinth Simon diamond rewards. Native NPC handlers authorize
  ordinary costs with `Inventory.Contain(id, count, 0)`, but native `Remove`
  ignores binding. The old managed debit copied that flaw after an unbound gate.
  Simon additionally counted empty rows as one diamond; Buddha selected an empty
  necklace. These are server defects, not packet compatibility requirements.
- Debit now selects only matching `Bound == 0 && StackSize > 0` rows, retaining
  lowest eligible UID order. Simon authorization and debit count actual positive
  stack units. All four Arthur costs/rewards, four-red/four-white flower costs,
  30-percent branches, one-Stardust choices, one-necklace cost and exact17-diamond
  rewards remain unchanged. Diagnostics no longer claim any-binding removal.
- Combined tests-first batch with FSB059:108 cases,82 passed/26 failed. Six old
  facts that required wrong binding/lock behavior were corrected;39 new cases
  were added, including seventeen empty diamond rows and16 positive+one empty.
  The wider private Release NPC/reward selection passed338/338,zero failed/skipped.
  Independent public Release also passed338/338,zero failed/skipped.
- Controls retain full saved item comparisons, valid economics, metadata and
  successful packet order. Arthur covers all four chains and binding255;
  Event/Simon cover noncanonical bindings2/7 and empty rows before real costs.
  These are admitted-cost repairs, not whole current-owner NPC transactions,
  crash recovery, global UID safety, SQL or live-client/deployment acceptance.

### FSB-059: Preserve existing item locks when granting RedBodhi stack units

- Owner: FullAccount option16 RedBodhiBean grant. Updating a compatible existing
  stack called `WithLocked(0)`, silently unlocking player stock. Remove only that
  existing-row reset; genuinely new rows retain the original unlocked defaults.
  Ten1000-unit calls, capacity policy, metadata, binding and frame order remain.
- Two unsafe old facts now require the existing lock to survive. Three new
  Locked0/1/2 cases check every saved-file byte (only StackSize changes), retained
  UnlockTimer/perfection/owner metadata, unchanged source/character files and
  all ten independent206-byte opcode2116 goldens. The Locked1/2 controls and both
  unsafe old facts failed before repair; Locked0 already passed.
- Included in private/public338/338 above. This is metadata preservation,
  not a whole grant journal,
  recovery/delivery guarantee, global allocator activation or live proof.
- Full Windows [40017540883](https://github.com/vgcman16/wolfconquer/actions/runs/40017540883)
  passed on exact public06210e3ad195366b3e052fabb6d67e82cf24f762 in5m26s:
  core24032 passed/0failed of24197,165 existing external-fixture exclusions;
  updater301/301,launcher57/57,all8 stages exit0,build204 warnings/0errors.
  Receipt20261023T201551325Z-371218ea finished20:20:17.8510965Z;
  GitHub job completed2026-10-23T20:20:24Z. Root read exact runnerHEAD,
  receipt/stdout and all three terminal TRXs. Parser20+13 controls remain
  synthetic only; no HTTP/service,SQL,live-client or deployment proof.

### FSB-060: Refuse full-bag Vote item claims before spending points

- Owner: `RealmTransferServices.MiscServices` Vote item rewards, options43–46.
  The old path could deduct VotePoints without granting an item when the bag was
  full. Capacity now rejects before persistence, session mutation or success.
- Preserve all four costs/rewards, metadata validation and successful frames.
  Full-bag controls require unchanged points, items, saved files and outputs.
- Verification: included in the shared admission batch below. This does not
  establish whole Vote claim recovery or a durable economy transaction.

### FSB-061: Debit only eligible Lottery exchange costs before allocating rewards

- Owner: `JailGuildServices.Rewards`, Collector Wong and Market exchanges.
  Authorization could accept unbound stock while debit selected an earlier bound
  or empty row. Select only matching `Bound == 0 && StackSize > 0` rows, retaining
  original eligible order and existing costs/payouts.
- Market checks capacity against the exact staged ticket removal before reward
  allocation; a full bag cannot reach UID overflow/allocation first. Refusal
  preserves costs, economy, files and success output. Valid slot-freeing and
  noncanonical-binding controls remain.
- Verification: shared batch below; no global allocator/Host activation or
  complete exchange transaction/replay claim.

### FSB-062: Check Deityland capacity against the actual planned key debit

- Owner: `Deityland.TryUnlockLegacyDeitylandChestAsync`. The old ANY-short-key
  prediction let a full40 bag with ordered stacks `[2,1]` append item41 after
  charging the first stack2 without freeing a slot.
- Prepare the exact existing debit/frames privately, then check the resulting
  inventory for item payouts. Refusal discards both; CP-only rewards need no slot.
  Preserve binding/enumeration policy, costs, rewards/rates and debit-first frames.
- Verification: shared batch below. Pre-persistence session publication,
  shared zero-stack admission and NPC respawn/claim lifecycle remain open.

### FSB-063: Require the Epic chest's own Epic key

- Owners: Epic chest dialog/unlock constants and registered caller. Native code
  and the copied test consumed Rare3303973 while the Epic dialog requested
  Epic3303974. Unlock now uses the existing Epic dialog key constant.
- Registered Epic-only success, Rare-only unchanged refusal and mixed-key
  controls preserve Rare stock and consume Epic stock for either genuine random
  item payout or200,000CP. The old unsafe mixed-key fact is corrected; wire
  layouts, rates and successful packet ordering are unchanged.
- Catalog provenance: `.setup_work/seed-json/items.json` rows2373051–55 and
  2373123–27 name RareChestKey/EpicChestKey. Its `manifest.json` identifies a
  2026-07-19 export from original `lqtxfwd/GameServer/DBServer`: SERVER catalog
  evidence, not independent client-table/binary proof.
- Shared tests-first evidence for FSB060–063:50 cases=37pass/13fail and26 cases=
  15pass/11fail; combined76=52pass/24fail. Added43 net new cases and corrected
  four unsafe old test methods. Private widened Release selection passed
  **326/326**, zero failed/skipped (`claim-chest-green.trx`, terminal exit0).
  Independent public Release also passed326/326,zero failed/skipped. Full
  Windows40022478743 on exactpublic306ec9ec failed one native-readiness test:
  core24074passed/1failed/165existing exclusions(total24240),updater301/301;
  launcher/later checks did not run. Build190warnings/0errors. The saved failure
  lacks the full probe report, so historical cause is unconfirmed. A test-only
  diagnostic successor retains all14assertions and unchanged5000/1500/1700
  bounds while including complete actual stdout/stderr on failure. This is not
  a runtime repair, fixed-flake claim or full-CI acceptance.
  Private diagnostic/readiness selection passed23/23,zero failed/skipped,8s;
  the original failure did not reproduce. Full successor acceptance follows.
- D13 offline registered/component fixtures and owned files only. Private-method
  post-roll controls are not registered Epic/Frame/client proof. No live client,
  SQL, crash safety, coherent multi-file atomicity or full-parity acceptance.
  Fourteen equivalent xUnit2031 substitutions in eight test files are local
  hygiene, not a verified CodeFactor portal finding reduction.

- Full Windows [40024325826](https://github.com/vgcman16/wolfconquer/actions/runs/40024325826)
  passed on exactpublicfdec04667add46095956374371ab5faddd3f6af4 in4m40s,
  jobcompleted2026-10-23T21:12:50Z. ActualterminalTRXs:core24075passed/0failed,
  165existing external-fixture exclusions(total24240);updater301/301;
  launcher57/57. All8stagesexit0,build190warnings/0errors. Receipt
  20261023T210825276Z-b839ee31 finished21:12:43.7712151Z. Root read actual
  runnerHEAD,receipt/stdouts/all3terminalTRXs andGitHubjobstate. Parsers20+13
  are synthetic,noHTTP/serviceprobes. The readiness case passed4.897785s;
  earlier40022478743 cause remains unconfirmed,not a proven fixed flake.
  This accepts the coherent offline source batch,notSQL/live/deployment,
  globalUIDactivation,whole transactions or fullgameplayparity.

### FSB-064: Require a real positive Deityland chest key

- Owners: chest-local dialog/admission/debit in Deityland and ChestKeys.
  Shared legacy helpers treated zero stock as one, allowing empty-key rewards
  or charging an empty row instead of later real stock, falsely freeing a slot.
- Dialog and unlock now select the same first matching positive row. Stage
  exactly one unit and its existing removal/update frame from that physical row;
  zero rows remain unchanged and provide neither payment nor capacity.
  Preserve any-binding policy, enumeration order, Rare3303973/Epic3303974,
  one-key cost, random rates/rewards,200000CP and debit-before-reward frames.
- Native Inventory.Contain sums actual stack quantities; community defects are
  not client wire requirements. This is chest-local: other shared-helper callers
  and global graph/UID policy are not changed.

### FSB-065: Validate chest CP saves against the actual character UID

- Owner: chest call to the existing TryPersistLegacyCharacterAttributes overload.
  The convenience overload selected Users/{AccountId}.ini correctly but also
  treated AccountId as the expected character UID. Distinct identities caused
  uidMismatch after memory/output publication, leaving the old CP value on disk.
- Pass both the real account ID and session.Character.Uid to the existing
  explicit-identity owner. No filename convention, loader/auth rule, payout,
  parser, save coordinator or protocol layout changes.
- Shared verification for FSB064/065: tests-first67=31pass/36fail, then the
  disjoint8account cases=4pass/4fail; combined75=35pass/40fail. All25 retained
  chest cases remain;50 new cases added. Repaired private Release **184/184**,
  zero failed/skipped (75chest+95NPC-handler+14packet cases),
  .artifacts/positive-key-safety/results/chest-payment-green.trx, terminal exit0.
  Four distinct-ID positives verify complete actual account301/character1293600
  files and unrelated UID-named-file preservation; four negatives exercise the
  real lower persistence owner only, not whole-handler save-failure atomicity.
  Independent full [Windows40027179762](https://github.com/vgcman16/wolfconquer/actions/runs/40027179762)
  passed exactpublic05e0feb186495b77871c487e8918dda4c7001dd7 in4m55s,
  jobcompleted2026-10-23T21:34:45Z. Root read terminalreceipt all8stagesexit0,
  finished21:34:38.6025377Z (20261023T213005152Z-2aa361a9),actualrunnerHEAD,
  GitHubterminaljob/log andbuild190warnings/0errors. The next tracker checkout
  cleared local reports before terminalTRX/stdout counts were read; exact full
  core/updater/launcher counts are unavailable for this run,not extrapolated.
  This is independent offline CI acceptance,not live/deployment/fullparity.
- Explicit VisibleDx8D13 offline registered/component fixtures and owned files
  only. Post-roll CP calls are component proof, not registered RNG/client proof.
  English key-name evidence remains a SERVER export, not client-table proof.
  At this admission-only checkpoint, separate best-effort saves and whole reward
  commit/recovery remained open. FSB066 below supersedes that debt for Rare/Epic
  only; max+1/global UID, Victory/broader claims, delivery, SQL/live/full parity
  and canonical UID/Host activation remain open.


### FSB-066: Commit chest key/reward saves before publishing success

- Owners: Deityland chest transaction/preparation/publication partials and
  LegacyDeityChestPersistence. The old route published a charged key/reward
  before separate best-effort account/item saves; invalid or locked required
  files still produced success. Consuming a parent key could discard deposits.
- Corrected invariant: validate actual account-keyed Character and character-
  keyed complete Items preimages, native/current identity, positive key,
  deposits, capacity, metadata and wallet bounds before changes. Prepare packets,
  serialize under real Trades/current-session/file gates, commit both files
  through the existing v2 journal, prove afterimages, then publish. Unproven or
  pending outcomes fence real recovery. Keep valid any-binding keys, one-unit
  costs, random rates, rewards and packet order.
- Recheck captured current/native owner and connection-ended state before each
  send. Post-commit send/cancellation/log failures cannot refund or make payment
  retryable. No nested Shared-to-Exclusive world-barrier upgrade.
- Verification: tests-first91=75passed/16failed, then wider202=184passed/
  18failed (two deposited-parent controls added), zero skipped. Private Release
  **530/530 passed**, zero failed/skipped,5s, deity-transaction-green.trx:
  123 chest +95 NPC +14 codec +298 Trade/session/barrier/DragonSkin neighbors.
  All202 baseline names are retained and pass. Forty-eight new chest cases:
  16 required-file +2 deposited-cost +6 journal-boundary +4 changed-file +8
  recovery-owner +4 stale-owner +8 publication controls. Overflow and malformed
  metadata guards are source-reviewed, not separately tested here. Two retained Rare/Epic
  registered Script/Reply facts now use complete real files and ready recovery.
- Successor private Release **534/534 passed**, zero failed/skipped,4s:
  all530 retained plus four actual D13 Frame controls (chest127). Rare/Epic
  success and missing-item-file refusal complete under an independently owned
  shared reader, followed by an actual exclusive probe proving lease release.
  Complete dialog/reward/file/session checks remain; Epic tests its real chosen
  random branch, not exhaustive CP/item Frame outcomes. No accepting owner.
- Public Windows40031267716 FAILED exact14450da2 in4m28s: core24171pass/
  2fail/165NotExecuted, updater301pass; launcher/parsers did not run. Build190
  warnings/0errors. Receipt20261023T220650082Z-7d363ee5 finished22:10:53.9312898Z;
  jobcompleted22:11:02Z. Both failures precede handlers: the raw fixture used a
  private name instead of public's sanitized seed name. It now serializes the
  supplied name; full equality/assertions remain. Successor full CI pending;
  this failed run is not relabeled green.
- Limits: owned-file/registered/Frame component proof, not authenticated login,
  a real TCP listener/client, enabled runtime-loop lifecycle or deployment.
  Recovery covers fresh-owner replay of controlled journal prefixes, not an
  actual killed process, power loss, multi-process
  or uncooperative writers, durable outbox, ABA or every crash point. Current
  max+1 reward UID remains unsafe globally; canonical UID/Host activation HOLD.
  Victory/broader NPC claims, SQL, live/deployment/full parity remain open.
  No original saves, live client/server or routing were changed.

### FSB-068: Serialize player-tick and Reviver publication

- Owners: `WorldServerFrameHandler.RuntimeTicks`, `GuardReviverRuntime` and
  the new physical-connection publication lease. Both loops previously held
  Shared world leases: the player tick could prepare stamina5, pause its send,
  then merge5 after the global Reviver had published100.
- Tests-first: actual Frame login/SetLocation, real runtime delay callbacks and
  one held physical send reproduced final alive/stamina5 in BOTH D11 and D13:
  0passed/2failed/0skipped (834ms behavior). This is a new controlled trace;
  the prior full-CI failure's exact stamina value/interleaving was not recorded.
- Corrected invariant: one per-physical-connection async gate covers fresh tick
  capture through sends/merges and fresh Reviver eligibility through its whole
  revive/animation/publication phase. A skip-only healthy prefilter precedes
  waiting; authoritative checks remain after entry. No Exclusive upgrade,
  world-wide exclusive loop, peer-gate nesting or packet-layout change.
- Real startup workers are registered by their actual returned Tasks. Bounded
  active-task/last-fault diagnostics let fixtures retain both initial pending
  workers and join those same Tasks before disposing their logger/files.
  This diagnostic registry is not a general stop/start fence or Host drain.
- Private Release focused proof: **556/556 passed, 0 failed/skipped, 5s**:
  retained534 chest/NPC/Trade/session/barrier cases,2 controlled race cases,
  4 actual lease cancellation/double-dispose/connection-isolation controls,
  12 ticker,2 merge and2 old Reviver facts. The old failing fact now advances
  actual registered timers after SetLocation; every original packet-order,
  animation, state and no-generic-attack assertion is retained.
- Limits: offline owned fixtures only; saved positive HP is checked, NOT saved
  stamina (not a current scalar-save key). Other runtime/Frame/raw writers,
  recipient delivery recovery, fairness and stalled dead/invisible candidates
  remain open. No general shutdown, live-client or full-parity proof.
  Full Windows successor40036685838 on c90122ab is accepted; see exact proof above.
  No live server/client, SQL, original saves, routing or deployment changed.

### FSB-067: Commit Victory chest quest claim and reward together (scoped verified)

Full Windows [40040302782](https://github.com/vgcman16/wolfconquer/actions/runs/40040302782)
passed on exact `df2881ce5911bab1ad6da9fc6a9400ec31ffc820` in4m19s. Actual
terminal results: core24299 passed/0failed,165 existing NotExecuted rows;
updater301/301; launcher57/57; synthetic parser20+13; build190warnings/0errors.
All8 local-check stages exited0. Receipt20261023T233344597Z-56e69ade finished
2026-10-23T23:37:39.5183865Z; job120709561180 completed23:37:48Z. Root read
exact runner HEAD, receipt, all TRX outcomes and terminal logs before cleanup.
This verifies offline Victory integration, not deployment or complete gameplay.

- Source-confirmed owner: TryUnlockLegacyVictoryChestAsync in
  WorldPacketHandlers.NpcEntrypoints.Deityland.cs. It publishes Finished quest
  3944 and a reward before separate best-effort item/quest saves, then sends
  success even after an ordinary save refusal. One saved half can lose a reward
  or retain a reloadable Accepted claim. Frame sidecar autosave is also separate.
- Required invariant: validate complete current account/item/quest owners,
  prepare exact claim/reward images and output, journal item+quest commitment
  before session/output, and reconcile interrupted claims without duplication.
  Retain valid objective100 threshold, random rewards, capacity and wire order.
- The prior shared journal supported Character/Items only. The private successor
  adds genuine v3 Character(no-op)+Items+Quests with an explicit configured triple,
  one shared pending slot, strict record/domain shape and retained v1/v2 bytes.
  No quests-as-items disguise, second journal, accepting saver or guessed root.
- Tests-first registered D13 proof: **2passed/4failed/0skipped**,822ms behavior.
  Both healthy NPCs passed; actual Items/Quests leaf locks, missing explicit
  Quest authority and durable-Finished/live-Accepted faults all failed the first
  exact-current-session unchanged assertion. Later file/output/retry assertions
  were not reached on those RED rows; no fixture/cleanup failure was reported.
- Private focused Release **690/690 passed,0failed/0skipped,5s**: prior556,
  6 registered Victory cases,41 real v3 journal/recovery,42 strict quest capture,
  36 retained v2 journal and9 retained quest-loader controls. All four fault
  cases now reach unchanged state/files/output assertions, repair/retry and
  same-session/file-reloaded replay. Generic journal controls cover all four
  durable prefixes, fresh recovery, unknown images, shared pending slot,
  malformed domains and a real late Quest replacement failure/rollback/retry.
- The coordinator validates complete account/items/quest captures, prepares
  every image/output, then uses real Trades -> Store persisted CAS -> ordered
  file locks -> v3 journal. It proves full postimages before Store/output.
  Complete malformed quest capture precedes permissive claim lookup.
- One retained old NPC positive now uses genuine full files/triple recovery,
  with all19 original assertions plus complete send-time saved-image controls.
  The two read-only Victory facts remain unchanged; default Rare/Epic fixtures
  retain their pair-only roots and Name-aware seeds. No reward amount/layout
  or daily reset rule was invented.
- Private successor Release: **717/717 passed,0failed/0skipped,5s**. This retains
  all690 cases above, plus6 postcommit/malformed-tail,15 native directory identity
  and6 actual D13 Frame startup/recovery controls. Send failure or owner retirement
  cannot reopen a committed claim; malformed complete quest tails reject unchanged.
- Actual Frame now receives precisely the loader's explicit PlayerQuestsRoot.
  Real handoff/login/SetLocation/NPC controls cover both NPCs, quest-file replacement
  refusal and same-login retry, omitted-root/no-sibling-inference refusal, recovery
  of a genuine interrupted v3 prefix before login, and unknown-image admission
  refusal preserving files/journal. No test-created session or recovery readiness.
- V3 physically rejects same/ancestor configured directories, including actual
  DOS/Volume-GUID aliases. Inspection is scoped to trusted ordinary directories
  and cooperating writers, not retained target/namespace ownership. Initial
 714/715 exposed metadata-only handles not pinning rename; adding directory-read
  access then exposed blocked child replacement (714/715). Final read+attributes,
  read/write sharing and no delete sharing passes BOTH original controls unchanged.
  Required list-directory access fails closed; no write/delete access is requested.
- Status: ordinary/cooperating-writer registered and Frame paths verified offline;
  public source and full Windows CI are verified above. This is not full Victory, hostile
  filesystem, actual-client, outbox, power-loss, all-raw-writer or global UID proof.
  Existing max+1 remains globally unsafe; canonical UID/Host activation stays HOLD.
  Hostile namespace retargeting, handle-relative replacement and whole-domain
  lifetime hardening are separate explicit debt, not silently claimed solved.
  No live/SQL/original-save/routing/deployment action.

## Active audit

### Open: Ordinary login can persist paid expiry before rejecting admission

Root's independent encrypted-loopback run 52563 has 5 failures / 2 controls passing,
no skips. Malformed Hairface, foreign Hairface/Beast owners, missing Beast metadata
and Online overflow reject after changing the Character paid-nobility image.
Denied-write and valid accepted/repeated-login controls pass. This is measured
open behavior, not repaired by the verified local-prefix slice. The separate ordinary-admission transaction is still under review.
These new audit regressions are not included in the accepted component counts.


### Distribution continuation: exact reviewed native assets (FSB-099 retained)

- Schema2 now permits only two typed naming collisions with one exact review per
  payload file: native mapobj/.../obj resources and exact data/main/LogS.BMP.
  Every other risky segment still refuses. Explicit membership, ordinary exact
  spelling/type, bounds and source length/SHA survive planning, staging/rebase
  and the actual publisher. No inherited recursive approval or generic waiver.
- Schema1 behavior and the original four-argument Publish CLR signature remain;
  review-aware inputs are local metadata, not new signed-manifest wire fields.
  baseSha256 still describes prior installed bytes. CLI shape admission precedes
  key reads; actual source and copied-byte digests are checked before promotion.
- Actual C#89580 passed434/434,0fail/skip,1m41s:357retained+75new+2root controls.
  The two root cases call genuine preflight then actual guarded copy, proving
  same-length/restored-time mutation refusal and the unchanged-byte positive.
  A case-alias seed was refined to retain a valid native prefix; assertions stay.
- Actual PS83615 passed retained9planner/9exclusion/10stage groups, no skipped
  groups, plus9new groups/51fixtures. The real PS->stage->compiled CLI pipeline
  passes2cases, verifies both P256 signatures/exact blobs and refuses changed
  reviewed stage bytes without creating the publication root. No missing-type
  compile failure is called behavioral RED; these are new API/feature tests.
- Limits: ordinary cooperating filesystem, not hostile namespace or a multi-file
  transaction. Late failures can retain owned partial stages or earlier immutable
  blobs; the discovery feed follows full content readiness. Raster/content purpose,
  actual native reviews, portable config and full-client release remain OPEN.
  Exact public816f3a20 passed Windows40304697861, updater434/434 and all8stages0.
  PS/pipeline proofs are standalone, not CI steps. No original data or game process changed.

### FSB-107: Publish complete native Gear metadata and owned wire contracts together

- Owners: native CombatGear/Daoqi/Lottery/Collection/Rank loaders and private
  registrar. The old loader enabled its raw gate and published partial caches
  before later input/registration failures; retries could also trust foreign or
  incomplete model state. Standalone reloads discarded valid prior data early.
- Correction: stage all five actual bounded inputs and publish one complete
  graph; standalone readers prepare before replacement. Complete proof binds
  actual owners/row values and privately registered twelve DTO/six enum contracts,
  required fields/options and genuine serializer preparation. Foreign known
  owners refuse; unrelated Ninja contracts remain permitted. Compatibility setter
  writes invalidate complete proof, including same-reference assignments.
- Cold Pirate storage restoration now uses that genuine completed load and the
  entire canonical saved gear on a real detached owner. Final Store/connection/
  player/scalar/slot checks follow borrowed reads before one-way adoption. No fake
  loaded gate, substitute owner or ordinary Loading callback is used.
- Verification: actual combined94691 passed1268/1268,0fail/skip,6m52s; includes
  20cold Pirate,4nonzero/foreign enum,6registrar provenance/factory/surrogate/
  packed-mutation,51staged/proof,48metadata and19rank outcomes. Earlier wide51174
  had643passes and1old null-success oracle, corrected under FSB-108. Actual TRX read.
- Limits: private source/component proof, not public CI, normal native Host/
  Loading/equipment/skill/ranking, live client or SQL. Public field-shaped caches
  become same-name properties requiring rebuild. Metadata checks scan retained
  state and are not benchmarked; mutable runtime ranks remain separate from load
  metadata. Complete load does not prove files stay unchanged forever afterward.

### FSB-108: Refuse absent native messages and invalid frame lengths

- Owner: LegacyNativeCombatGearWire actual object entry, six typed projections
  and shared frame helpers. Null messages emitted empty success frames; unchecked
  UInt16 casts wrapped oversized lengths, and raw0..3byte inputs let the seal
  overlap a missing header.
- Correction: reject active missing outer DTOs before serialization. Gate-off
  refusal still precedes required-state/model access. Raw streams require4..65535
  bytes; object payloads0..65531, with checked header/allocation arithmetic and a
  copied-length recheck. Preserve positions, input bytes, opcode and eight-byte seal.
- Verification: actual null7140 had1failure/9controls; actual frame3345 had
  10failures/6controls. Corrected1268-case combined94691 includes all16new boundary
  outcomes,13actual direct/implicit null-route checks and migrated old null-success
  rows, retaining valid literal goldens. Exact maximum is independently verified
  through the actual pinned serializer and fixed Opt projection; one more element
  refuses. Actual terminal TRX inspected,0fail/skip.
- Limits: serialization prepares its bytes before framing can bound them; upstream
  object/count admission remains required. Caller-owned streams must not mutate
  concurrently. These bounds prove header representability, not native transport
  acceptance of every maximum-size packet, live-client behavior or Host activation.

### FSB-109: Prepare both Ninja metadata files before replacing either cache

- Owner: LegacyNativeNinjaFile.Load. The old path cleared/published its first
  dictionary before checking later files/rows, so missing, locked, malformed or
  duplicate input could erase or partially replace otherwise valid metadata.
- Correction: capture the real literal prefix/current directory/culture, hold both
  read-only file leases, parse complete bounded tables and publish one private pair.
  Failure preserves both prior dictionaries, row identities and values. Minimum
  source columns, opaque fields/extra tails, Score/UK5 mapping and insertion order
  remain; no unsupported schema or new Host caller is invented.
- Verification: actual new-case70799 had13failures/9controls. Corrected94691's
  1268/1268selection includes all43loader outcomes (21retained+22new), real sharing
  failures, full reloads/aliases, UTF8/16/32 BOMs, newline forms and genuine DBItem/
  Score consumption. One reflection test retains exact two-cache property types
  and all original row-field checks. No failures/skips; actual TRX read.
- Limits: same-name properties require rebuilt consumers. Public dictionaries/
  rows remain mutable and separate getters are not an operation-wide concurrent
  snapshot. The existing stricter equipped-consumer bound remains. No normal
  startup/rank/skill/equipment lifecycle, live client or full parity is proved.

### FSB-106: Prepare complete Ninja saved state before replacing the live graph

- Owner: LegacyNativeNinja.Persistence.Load; decompiled Ninja.Load immediately
  overwrote scalars with fallback defaults and merged rows into existing storage.
  Malformed input could partially overwrite state, publish zero/duplicate rows
  and leave obsolete or matching stale rows behind on later valid loads.
- Correction: validate complete canonical bounded input, copy all scalar/item
  data into a detached real graph, then publish one aggregate reference under
  the private state gate. Refusal leaves every prior scalar/row/owner unchanged.
  Null/empty optional data does nothing; exact0 retains the genuine database
  missing-field sentinel, and canonical empty state explicitly clears the graph.
  Missing required item storage refuses instead of inventing a replacement owner.
- Verification: actual owned-candidate RED48187 had20failures and2valid controls.
  Corrected91187 passed545/545,0fail/skip,1m34s. All22new outcomes are present:
  18malformed/bounded cases,2explicit empty states,4096valid rows and actual
  Load-to-Save/native User producer with independent52-byte opcode2395 golden.
  Thirteen prior profile facts retain valid/wide/derived-Score behavior while
  unsafe oracles are replaced. Three reflection tests retain all18scalar checks.
- Limits: five source/six test paths are intaken privately, not public CI or
  native Host/client/SQL proof. Public implementation fields become same-name
  properties: source compatibility requires rebuild, not compiled field ABI.
  Gameplay/ToString still read multiple properties and mutable rows separately;
  their whole-operation concurrency and general Save graph validation remain open.

### FSB-103: Retain completed mailbox history without losing active writer fences

- Owners: LegacyMailboxPackageWorkStore, LegacyMailboxPackageProducer and
  LegacyItemUidRegistry. The flat lifetime work cap and bounded active UID ledger
  could exhaust despite genuine completed claims; completed no-allocation work
  could also disappear from a writer fence before actual Store publication.
- Correction: bounded pending metadata plus immutable state-anchored completed
  history/direct lineages; retire only genuine completed mailbox UID pairs with
  a private proof, unchanged high-water and no reissued range/token. Preserve the
  actual awaiting-publication account/operation fence through completion. Legacy
  custody matches the current pending or anchored-completed record after valid
  retirement; missing pending operation identity is a safe schema refusal.
- Verification: actual owned-candidate63858 passed888/888,0fail/skip,21m26s.
  Includes300successive no-UID claims with exact unchanged UID bytes, allocating
  pressure, legacy migration/replay/retirement/fresh reopen,4publication-gap
  writer controls,4UID interruption cuts and4malformed pending identities.
  Earlier84375 failed compilation before any test ran. Root read actual TRX.
- Limits: local focused source/component proof, not successor public CI, native
  Host activation, live client/SQL or complete reward parity. Startup/retirement
  scans and retained identity memory grow with history; no benchmark, pruning or
  protection against coherent administrator rewriting is claimed. Other producers
  need their own real completion owners. Active bounds and shared RNG limits remain.

### FSB-104: Reject Trade mutations after either real endpoint ends

- Owner: WorldPacketHandlers.Trade.Lifecycle. An existing pending/open pair could
  accept, add items, debit gold/CP or confirm after the actual ended marker but
  before FIFO disconnect cleanup acquired the world operation lease.
- Correction: reject non-cancel actions from an ended sender or a pair with an
  ended peer under the existing real lease/trade lock. Explicit cancellation and
  genuine disconnect escrow refund retain their ordinary ownership path.
- Verification: actual combined RED22521 had11Trade failures plus2Ninja failures
  and6passing controls. Corrected79428 passed699/699,0fail/skip,1m34s, including
  all15new Trade cases and176Trade outcomes overall. Actual FIFO final-confirm
  and request controls pin complete files, sessions, escrow and packet order.
- Limits: an exchange committed before the marker remains committed. Existing
  journal/escrow reconciliation owns IO uncertainty; no SQL/native Trade/Host,
  live UI, global raw-writer quiescence or complete trade safety is claimed.

### FSB-105: Fence Ninja adoption after the last caller-owned attribute read

- Owner: LegacyNativeNinja.MailboxRecoveryOwner. A same-value MsgNinja or final
  ClassExperience read could remove the actual Store holder after its earlier
  check, then still attach MyNinja and report success.
- Correction: after all borrowed reads, recheck callback-free Store/transport/
  native/player/UID/class/experience/cold-phase/empty-slot identity, then attach
  once and truthfully report that one-way publication. No accepting substitute
  owner, queue, EOF authority or post-attach rejection is introduced.
- Verification: actual RED22521 exposed both retired-holder cases; all4new
  unchanged-value controls pass in corrected79428's699/699 selection, including
  523Ninja outcomes. Exact source points bytes and files remain pinned.
- Limits: focused owned runtime proof, not native authenticated login/Host,
  complete Ninja loading, live UI or general raw-field concurrency safety.

### FSB-102: Reject incomplete mailbox work before copying or publication

- Replacement integration defect: both real work-preparation paths dereferenced
  an absent change row while cloning images. A malformed internal request threw
  NullReferenceException instead of refusing with its real UID intent or pending
  no-allocation work intact. This is not a claim about an observed client packet.
- Both paths now capture exactly four nonnull rows and before/after arrays in one
  indexed pass. Only a complete private copy reaches durable preparation. Existing
  retained admission, current ownership, target and image checks remain required.
- Actual regression58408 reproduced2null-row faults with6passing controls.
  Corrected13961 passed39/39, including all8input cases and31stack/recovery cases,
  zero failures/skips. Rejection pins all files, resources and outputs; valid
  original requests subsequently commit. Both actual TRX were read.
- The stack successor also preserves complete existing item state during merges,
  allocates only genuinely new rows and retains explicit no-allocation work.
  Eleven warm-vs-cold restart oracles were corrected using complete saved graphs,
  pinned resources/files and the explicit zero-Vigor normal-startup phase. They
  do not fabricate the previous session's derived HP or physical overlay.
- Limits: owned C# component/registered Frame proof, not native executable,
  client/SQL/Host or successor full CI. Exact shared native RNG, history scaling,
  flags10-12, complete native loading and whole English parity remain open.

### FSB-101: Preserve pending mailbox work and stop stale postcommit delivery

- The replacement mailbox join originally allowed ordinary claims/world actions
  and saves to invalidate retained reward beforeimages. Restart normalization could
  strand that work; a catalog callback could also change native state after the
  final delivery checks. Complete resource and ownership admission is required.
- Actual initial route tests failed2/2, retained-work mutation cases failed6/6,
  then combined admission/transaction tests passed8/8. Same-value catalog callbacks
  exposed10fail/2unchanged-control passes (extra packet after commit).
- The complete inventory/mailbox class passed263cases. Its combined run also had
  3parent-registrar setup failures before Pirate test bodies; putting genuine
  Ninja use in the existing native prerequisite collection preserved all real
  registrar assertions. The ordered native/callback/Pirate group then passed28/28,
  zero failures/skips, including genuine table.Load and missing-wire controls.
- Explicit UID producer, immutable intent/range, closed prepared/completed work
  metadata and actual four-file v5 journal precede native/session/output changes.
  Exact requests alone may replay pending work. Store/cache/file/packet owners
  fence ordinary mutations; fresh startup reconciles exact retained images.
- Packet-specific real leases recheck queued requests without imposing inherited
  packet admission on transport cleanup. Delivery performs callback-bearing reads
  first, then current Store/PIN/native checks immediately before each send.
- Early native recovery skips the mutating Online prefix. Pure cold preparation
  publishes only saved fields under the actual Store identity; PIN verification
  does not rewrite saved PIN state. Malformed PIN refuses before cold publication
  or login success. Native recovery/cold-load/PIN controls pass9/9, zero skips.
- Additional corrected integration defects: full bags get only the established
  STR_BAG_FULL error, and InvalidDataException is a truthful refusal. Five earlier
  raw-vs-derived test oracles were corrected to assert complete actual raw loader
  state plus pinned resources/files, without inventing a prior physical overlay.
- Limits: owned C# components/Frame dispatch and capture transports, not native
  executable/client/SQL/Host proof. Reviewed source passed exacta3cb full Windows
  CI40321922059 above. Stack/color fidelity, no-allocation claims, flags10-12, native
  special-owner loading, inventory/wallet/save adoption and live acceptance remain
  open. Existing native cold loading does not fabricate Ninja/Pirate reward owners.

### FSB-100: Reject unsafe Ninja point credits before mutation

- Source: native Ninja.AddPoints unchecked uint addition wraps a credit to zero;
  it also mutates Exp before dereferencing an absent owner. Older tests required
  those defects and were replaced with unchanged-state refusal assertions.
- Corrected direct API: require the genuine current constructor-owned client and
  reciprocal Player before a packet loan; compute checked points, serialize the
  complete native packet before changing Exp, and recheck owner/counter state.
  Valid zero/full-width credits retain their source-golden Type2/UID-zero bytes.
- Actual tests-first overflow baseline:2failed/0passed, both counters became0
  instead of retaining4294967295/4294967246. The separate missing-owner baseline
  failed with71->74. After correction the widened Ninja suite passed118/118,
  zero failed/skipped:59new saved-line/capture/bounds cases,2new overflow rows,
  and57retained or deliberately migrated native mutation/contract cases.
  A subsequent review exposed callback-bearing source dictionary lookups in the
  new capture: actual2/2 RED changed25->26 and75->76 after prior checks. Bounded
  enumeration/private uint-key maps now avoid those callbacks and recheck the
  captured owner/scalars last. The widened successor passed120/120,0fail/skip.
- The mailbox Ninja projection additionally prepares complete Character.MsgNinja
  text and source-golden2395bytes without sending or mutating; it publishes Exp
  only at the caller's explicit postcommit boundary. The full mailbox package
  producer/journal/registered-handler join is still being implemented.
- Limits: owned offline component fixtures, not full CI, durable standalone
  AddPoints, authenticated Host birth, real client rendering or complete Ninja
  transactions. Existing post-credit native send/log behavior, unrelated APIs
  and broader raw-writer concurrency still need their own audit.

### FSB-099: Enforce filename quarantine at the actual publisher boundary

- Distribution tooling defect: planner/stager rejected private/generated/control
  names, but a direct ReleasePublisher plan could publish those same files. CLI
  publish also read the external signing-key path before rejecting such a plan.
- New PublicationPathPolicy retains structural Windows-path checks and the
  existing ordered filename categories, with bounded culture-invariant matching.
  Publish validates the entire bounded plan before root/source/signer/output work;
  CLI publish validates before opening key bytes. Errors contain static reasons,
  not the rejected filename. No blanket exemption or native-asset waiver exists.
- Current-API59740 RED:75total,37passed/38failed,0skips,2s. All36unsafe existing
  synthetic files were accepted; exclusive-first preflight reached the wrong
  source read, and CLI reached its absent key. All failure messages were read.
- Corrected91767 GREEN: full updater357/357,0failed/skipped,1m28s, including all
  305prior rows and52new outcomes (36refusal,14valid signed near-name controls,
  one Windows exclusive-source and one actual CLI pre-key case). No compiler
  warning/error text. Root reviewed/intook3production and2test paths in both roots.
- Limits: future publishing admission only. Existing signed-manifest/repoint
  compatibility and prior-install baseSha256 semantics stay intact. Names cannot
  detect renamed secrets or certify content. Exact reviewed BMP/DDS membership,
  source-image binding through staging/publishing, ambiguous INIs, portable client
  configuration and full-client release remain OPEN. Exact public98be3f41 passed
  Windows40297219885/all8stages with all52new outcomes, updater357/357, core25299
  plus165existing exclusions, launcher57/57. Receipt20261026T090840597Z-d2f7acd6
  ended09:13:49.6704534Z; build157warnings/0errors. No real client/configuration/
  key/feed or running service changed.

### FSB-098: Select the client-confirmed D13 mailbox text fields explicitly

- Source: LegacyMailListPacket used title8/sender9 for every profile. Cached DX8
  D13 client SHA256 D13B45BD2681DB7D3D8548EA575D89E0D05613BA00E8F9763DBF3F89030402A5
  has numeric optional8 and strings9/10 (CMailInfoPB serializerD539b3). Its parser
  tag40 branch and actual initialization check permit omitting opaque numeric8.
- Explicit D13 serialization/parsing now uses strings9/10 without inventing8.
  Listing, committed ClaimAll refreshes and NewMail select the known host profile.
  Default RootD11 bytes remain unchanged; this is not a new D11 native-schema claim.
  Unknown profiles refuse before listing-cache or boss-ranking/mail mutations.
- Current-API Frame baseline72346 failed5/5 solely on literal8/9 versus9/10 tags.
  Final42305 passed1593/1593,0skips,20s: all1027prior mailbox/UID/journal rows,
  545existing boss rows and21new D13 rows (5Frame,14literal codec,2producer).
  Golden bytes, optional8, cross-profile refusal, all three senders, postcommit
  ordering/replay and full-bag error-only behavior are covered.
- Initial28538 stopped before tests for a missing test System.Buffers import.
  Next61138 passed1591/1593; two producer cases stopped before invocation because
  lethal-only damage creates no harm-ranking row. Root seeded a real nonlethal
  hit before the kill, retaining every state/output assertion. No product rule
  or test coverage was weakened to obtain the final result.
- GitHub Windows40288903718 on exact public9ce02119 completed success. Actual
  receipt20261026T075718937Z-26791360 finished2026-10-26T08:02:11.8500648Z:
  all8stages0, core25299passed/0failed+165unchanged NotExecuted outcomes,
  updater305/305, launcher57/57, parsers20+13, build157warnings/0errors.
  All1593focused outcomes are included and pass. Static binary and owned handler
  proof are not current client rendering or authenticated native ownership;
  opaque8 semantics, broader schema, package payout and live acceptance stay OPEN.

### FSB-097: Do not acknowledge mailbox claims rejected for a full inventory

- Source: the managed capacity branch retained the community server's mode4
  echo; ClaimAll also emitted a mail refresh despite rejecting the whole move.
- Capacity rejection now sends only STR_BAG_FULL@@, preserving session, wallet,
  complete files and exact cached rows. Successful claim bytes/order stay intact.
  The ack diagnostic checks actual prepared mailbox-mode frames, not frame count.
- Tests-first49404:4failed/0passed/0skipped, all excess-output failures. Corrected
  34238:944passed/0failed/0skipped,18s. Two new actual D13 Frame rows and two
  explicit unsafe-oracle migrations pass. Repeated failures leave state unchanged;
  a genuine bank8 deposit frees a slot, then the same attachment claims and safely
  replays through the real handler with persistence checked before each reply.
- D13 static evidence shows a mode4 reply posts0xf0e and its conditional consumer
  clears cached row+18; protobuf numeric field5 maps to that word. This makes an
  error echo unsafe to assume harmless. Runtime HWND binding and actual client
  rendering remain unproved; no specific rendered-loss claim is made.
- GitHub Windows40283769355 on public9a1a72bb passed all8stages; actual runner
  receipt20261026T070721633Z-6635ce0c ended2026-10-26T07:12:16.8894775Z.
  Core25195pass+165existing exclusions, updater305/305, launcher57/57,
  build157warnings/0errors. All14targeted package/full-bag outcomes pass. Package payout,
  shared native graph, SQL, outbox/delivery and current client acceptance stay OPEN.

### FSB-096: Refuse unowned standalone mailbox packages before rewards

- Source: managed ClaimRecordType used session-local new-item UIDs, a missing
  CombatGear-to-Empty fallback, ordinary Store.Set and separate mail/sidecar saves.
  It could publish partial rewards or bypass main-PIN admission before clearing Flag.
- Mode5 now joins real exclusive/current Store, recovery and complete-baseline
  admission, including main PIN and native binding when present. Missing/zero-Flag mail keeps the no-ack rule;
  nonzero Flag refuses the whole unsupported action with PackageOwnersUnresolved.
  No items, progression, cache, files, PIN admission or success frames change.
  Remove the unreachable unsafe private grant helper; retain pure reward policies,
  plans, native primitive codecs and valid packet goldens.
- Tests-first:10failed rows (8new plus2 deliberate unsafe-oracle migrations).
  Two missing/zero-Flag failures were diagnostic-only; they do not prove new
  mutation defects. Corrected focused12300 terminal0:942passed/0failed/0skipped,
  17s. Real Frame class/gear/full-bag/replay controls, exact current-session/file
  invariants, supported ClaimMoney follow-up and genuine main-PIN issuer are covered.
- Package payout remains OPEN until a real live UID producer, stable durable
  reward intent/receipt reconciliation and joint gear/Ninja/package journal owners
  exist. Snapshot presence or a component reservation is not that authority.
  FSB097 removes the full-bag success echo; runtime client rendering remains open.
  Fresh tracked-source LOCAL passed25193core+165existing exclusions, updater305,
  launcher57, all8stages0; receipt20261026T064543721Z-2f26492d. GitHub full Windows
  on the later full-bag successor9a1a72bb also passed40283769355. Actual-client acceptance remains separate;
  no successful package transaction, Host activation or full parity is claimed.

### FSB-094: Commit mailbox deletion batches before cache removal or echoes

- Source: CMsgMailboxMode400..407/459..470 and managed Mail Delete/DeleteClaim.
  The managed handler removed cached rows before independent Save; batch echoes
  preceded that save, so write/send failures could publish an uncommitted result.
- Both modes now reuse the actual exclusive-operation/Store/PIN/Trade/recovery
  claim owners and typed v4 Character+Items+Mailbox transaction. Validate the
  complete graph and exact baselines first. Character/items are exact no-op
  participants. Remove only unique selected live rows with all reward fields
  empty; preserve every retained740-byte raw record/order, including valid
  uninterpreted string padding. Prepare the whole batch/cache/echoes first.
- Commit/prove the entire batch before cache/Store publication or first echo;
  a send fault cannot undo deletion or make replay delete/echo again. Missing,
  nonempty, duplicate, expired and out-of-uint IDs retain their safe decisions.
  Inventory capacity is not a deletion prerequisite. Last-row deletion writes
  the real four-byte count-zero image.
- Tests-first38371:16rows total,11failed/5passed/0skipped across deletion+Save;
  all six new failing deletion rows reproduced dirty/early-published behavior.
  Corrected57811:796/796,0failed/skipped,15s, retains all780prior outcomes and
  adds all16new rows, including8deletion cases and two retained setup migrations.
  Actual full-bag, raw-padding, exact-before-every-echo, sharing/retry, replay,
  first-send-fault and count-zero outcomes were read. No assertion was weakened.
- Limits: managed cooperating-owner controlled failures, not process-kill/power
  loss, raw/multiprocess writers, outbox/guaranteed delivery, native economic
  adoption, package rewards, full expiry/producer lifecycle or live-client proof.
  Clean public91501b34 archive5469files:62788 terminal0/all8Windows LOCAL stages,
  core25185pass/0fail+165unchanged existing NotExecuted; all81new successor rows
  and all914focused mailbox/UID outcomes pass. Updater305/305, launcher57/57,
  synthetic parsers20+13; receipt20261026T055841147Z-8d6e7b2c ended06:03:26.0363585Z.
  Normal-FF source push verified. GitHub Windows40277978050 on exact91501b34
  completed SUCCESS/all8stages; actual25185/305/57TRX outcomes read.

### FSB-095: Prepare and validate ordinary mailbox saves before replacing files

- Source: LegacyMailboxStore.SaveCore called File.Create before serializing
  required text/encoding, so a later null/encoder fault truncated a valid file.
  It could also publish duplicate attachment identities rejected by strict load.
- Serialize a bounded complete managed image in owned memory with the existing
  encoding/order, then validate it with the complete-image parser before file
  or directory mutation. Reuse the shared snapshot file gate and existing
  atomic writer. Failures preserve the prior complete file; count-zero writes
  remain truthful. No second save engine or accepting persistence callback.
- Five new current-API cases genuinely failed in38371 (three null fields, a
  later-row UTF8 encoder fault and duplicate attachments). All8Save cases pass
  in57811's796/796, together with retained parser/claim/journal coverage.
  Real Windows replacement sharing refusal/release/retry, independent UTF8
  bytes/numeric offsets and empty-image controls remain enabled.
- Limits: ordinary single-file atomic replacement, not rollback of cache edits
  made by callers before Save, ranking consumption or a multi-file reward
  transaction. Existing writer cleanup/retry is reused; process-kill, power
  loss, hostile namespace and uncooperative writers remain unproved. The full
  clean public91501b34 LOCAL/GitHub gates above pass; actual-client acceptance
  remains pending.

### Durable UID component continuation (FSB-036 remains open)

- Registry-owned v3 producer intents now persist bounded immutable payload bytes
  before range allocation in the real pending/state owner. Exact binding and
  range remain discoverable after restart; this API returns reconciliation data,
  never a replacement materialization token or permission to publish items.
  Old token operations cannot adopt intent-owned IDs, and intents cannot adopt
  an earlier legacy reservation. V1/v2 bytes and unrelated token lifetimes remain.
- Root reviewed8production files and3test partials;16143 passed169/169 with no
  skips,10s: all118prior outcomes retained plus51new intent/recovery/bounds cases.
  The new API had no compilable behavioral baseline; no measured RED is claimed.
  Limits:128permanent intents,8192bytes each,256KiB aggregate; no compaction or
  completion protocol. Canonical business payload schema, live player authority,
  reward-journal composition/publication and Host/global UID adoption stay OPEN.

- The existing genuine leased registry now binds a stable operation GUID,
  expected domain, writer/account/character, canonical digest and range count to
  its durable high-water transaction. An identical replay returns its receipt
  and no second materialization token; conflicts reject without advancing.
  Strict v2 receipts preserve v1 serialization and the immutable receipt prefix.
- Root reviewed all6production files/all3test partials. Measured96606:
 118/118passed,0failed/skipped,6s = all53retained controls plus65new operation,
  codec, controlled interruption/reopen, structural readback and concurrency rows.
  A direct Assert.Single predicate avoids introducing an xUnit2031 warning.
- This component has no Host/producer activation. The2048receipt limit refuses
  new operations when full. Trusted migration/domain handoff, long-account-ID
  adaptation, all-writer adoption, scalable retention and durable reward intent/
  journal reconciliation remain required. A crash after reservation but before
  the producer journal does not gain a replacement token. No global UID or
  package-grant completion is claimed; FSB-036 stays open. Full public91501b34
  LOCAL/GitHub gates above pass without activating a producer or Host.

### Joint mailbox/gear component continuation (package payout remains open)

- Typed journal v5 adds exactly Character/Items/Mailbox/CombatGear in canonical
  order to the real shared pending/replacement/recovery engine. Four explicit
  disjoint roots and exact before/after images are required; current Host does
  not supply or infer the Gear recovery root. V1-v4 shapes/order remain unchanged.
- Gear images must match the existing canonical UTF8 writer after counts are
  bounded by actual sections/keys. Missing/corrupt images cannot become Empty
  owner evidence. This strict component is not a migration or public-loader fix.
- Root reviewed3new production partials,5glue diffs and3test partials. Candidate
  42353 passed1027/1027 with0skips,14s: retained944 plus51intent and32gear cases.
  Actual outcomes include all five recovery prefixes, fourth-target Windows
  sharing failure/rollback/retry, wrong roots, malformed records and v4 controls.
  New API baseline lacks compilation; no behavioral RED is claimed.
- No handler/Host activation, live payout, shared native/Ninja authority, atomic
  UID/business commit, outbox, power-loss or raw-writer proof follows. Canonical
  business intent, genuine participants, reconciliation/publication and current
  actual-client acceptance stay OPEN. Full package claims remain refused.

### FSB-092: Commit mailbox item and currency claims as one recoverable outcome

- Source: CMsgMailboxMode/MailList and managed WorldPacketHandlers.Mail mutated
  wallet/items/cache and sent notifications before separate file saves. Sharing
  faults, send faults, overflow and retries could lose or duplicate rewards; main
  PIN was bypassed. Old package ClaimAll tests certified incomplete reward state.
- ClaimItem, ClaimMoney and Flag-zero ClaimAll now resolve the actual current
  Store owner under the real exclusive operation, Trade and mailbox gates.
  Validate current identity/native Player when present, main PIN, ended marker,
  complete seven-store item graph, exact character/item/mail baselines, explicit
  recovery roots, ownership, escrow refund headroom, bounds and capacity first.
  Unbound managed sessions retain only the existing zero-PIN path; this is not
  universal physical-Player authority or a completed native login handoff.
- Extend the existing shared journal with typed v4 Character+Items+Mailbox,
  explicit disjoint roots and account-file/character-file identities. Prepare
  detached items, currency fields, cache and all packets before the persisted
  Store CAS. Flush journal/replacements, prove all postimages, then publish
  cache/Store and transport. Retained/unknown durable outcomes fence recovery;
  send/close faults after commitment cannot refund or duplicate a retry.
- ClaimMoney keeps attachment/Flag and works with a full bag. Mixed Flag-zero
  claims commit wallet, attachment and mail together. Selected nonzero package
  flags refuse the whole ClaimAll: real package state and new-UID authority are
  unresolved. Successful bytes/order remain; FSB097 supersedes the earlier
  source-shaped bag-full echo with an error-only reply. Actual UI acceptance
  remains UNVERIFIED.
- Tests-first29727:748pass/15fail of763,0skip. Money78628:0pass/10fail,0skip.
  Failures reproduced dirty files/cache, PIN bypass, overflow/stale admission or
  notification-before-durability. Corrected77436: **780/780**,0fail/skip,13s,
  including all33new rows:26current-API claim/load cases and7new-v4 recovery rows.
  Prefixes0/1/2/3, unknown images, wrong/missing mailbox roots, sharing failures,
  real PIN issuer, transport close, send faults and replay are covered offline.
- Integration50074 stopped at3fixture constructor-argument errors before tests;
  use existing Context init properties, no new accepting API. Candidate26528
  passed773/780; all7 failures were journal Seed's noncanonical warehouse order
  before any journal call. Seed now follows the existing loader bank/UID order,
  retaining every roundtrip/byte/recovery assertion. Failed logs remain intact.
  Five retained positive fixtures gain genuine files/recovery/owners without
  weakening assertions; the unsafe package oracle becomes exact healthy-owner
  whole-request refusal. Public synthetic fixture identities remain sanitized.
- Limits: cooperating in-process owners/process-restart prefixes, not killed
  processes, power loss, hostile namespace, multiprocess/raw writers, outbox,
  actual-client acceptance or full mailbox parity. Direct stale-context/native
  generation windows, native/managed graph adoption, mailbox1000 presence/UID
  aliases, package claims and producer/expiry lifecycle remain open. Deletion
  and ordinary Save have separate scoped successor evidence in FSB-094/095.
  Clean publice64349a4 archive5455files: session87182 terminal0, all8Windows LOCAL
  stages pass; core25104pass/0fail plus unchanged165existing NotExecuted outcomes,
  all33new rows passed, updater305/305, launcher57/57, synthetic parsers20+13.
  Receipt20261026T052657357Z-4ef51c47 finished2026-10-26T05:32:45.3245849Z.
  Normal-FF source push verified; GitHub Windows run40274944480 completed SUCCESS
  on exacte64349a4, all8stages and the same25104/305/57terminal TRX passes read.
  No live/SQL/original-save,
  protected client, canonical UID/Host, endpoint or held Guild/Union changes.

### FSB-093: Validate complete mailbox images before allocation and cache replacement

- Source: LegacyMailboxStore.Load allocated from an untrusted signed count and
  accepted partial/trailing/ambiguous rows before replacing a prior valid cache.
- Validate bounded complete4+count*740 managed images before row allocation.
  Reject negative/inconsistent counts, duplicate mail IDs/attachment UIDs, null
  or invalid required state and invalid timers, including expired tails. Prepare
  the full live list/returned array before cache publication. A rejected file
  leaves prior cache and every injected byte intact. Native unsafe struct padding
  is not inferred or silently converted to this accepted managed file format.
- Four new cache-preservation cases failed in29727 and pass in77436's780/780,
  0fail/skip, together with retained loader/codec cases and complete Frame claims.
  No fixture or parser assertion was skipped, suppressed or relaxed.
- Limits: complete managed-file admission, not native binary-save migration,
  rollback of earlier login effects, atomic ordinary Save/producers or full mail
  lifecycle. Current client target stays Chinese7899/cachedDX8D13. The full clean
  publice64349a4 LOCAL and GitHub CI gates above pass; actual-client acceptance
  remains separate and pending.

### FSB-091: Capture real equipped sigil metadata before combat and cooldown commitment

- Source: Ninja.TryGetValueEquip and Item.DBItem were queried late by Physical.
  OnMonster and cooldown admission. Missing metadata fabricated a zero row;
  invalid/foreign owners could be treated as absence, or fail after output/RNG.
  Late alias changes could alter this hit's damage or cause null faults.
- Add a factory-issued capture bound to the actual constructor/current Ninja,
  Player/class, Equipment and item Values order. Validate all five item fields
  and actual metadata Values. Keep first matching Values row within a slot,
  last matching slot, alternate9..16 preference with paired1..8 fallback, and
  first exact ItemID+Level metadata. Dictionary keys do not become authority.
  Missing selected metadata/null rows are invalid; genuine absence and matching
  zero-valued metadata remain distinct valid states. Non-Ninja absence does not
  read unrelated Equipment/items/catalogs.
- Physical admission rechecks the capture after real status traversal and before
  output/RNG, then uses immutable captured values at the existing late formula
  point. Cooldown preparation retains the capture and checks it again after
  PreparedUpdates, before resource/authorization/cooldown commitment. Existing
  signed-power clamp and all four cooldown-ID positives remain unchanged.
- Add39rows (9invalid physical,8selection/absence/budgets,12admission/late-alias
  drift,10cooldown). Replace one old missing-metadata success oracle with exact
  rejection/state preservation. Tests-first34552: **1089passed/28failed of1117**,
  0skip (27new failures plus that migrated fact). Root read every actual message:
  unsafe acceptance, late null faults, missing preflight and changed damage.
  Corrected28715: **1117/1117**,0fail/skip,3s. All39new outcomes, migrated fact,
  four retained clamp controls and actual TRX groups were read. Independent
  static review found no production behavioral defect; it is not runtime proof.
- Limits: logical work budgets4096item/16384metadata rows bound accepted captures
  and helper arrays. ConcurrentDictionary.Values snapshots internally after the
  Count gate; an uncooperating growing raw writer can allocate beyond that gate.
  No hard allocation/race bound is claimed. The post-PreparedUpdates recheck is
  present but has no new forced-window test. Other DBItem placeholder consumers,
  full Ninja loading/profile repair, packet lifetime, complete cast/economy
  transactions, actual-client and native dispatch adoption remain open. Full
  clean-public LOCAL1e042149 passes above; GitHub CI/live remain unproved. No
  formula, wire layout, original data, live process or endpoint was changed.

### FSB-090: Bound mailbox counts and validate their physical packet envelope

- Source: LegacyMailboxModePacket.TryParse copied an independently supplied
  payload and multiplied its untrusted count in checked int arithmetic before
  rejecting its length. Hostile counts threw OverflowException; incoherent outer/
  embedded headers, missing physical frames and altered payloads could be accepted.
- Reuse the existing account envelope algorithm through PacketFrameEnvelope:
  require declared/embedded header, actual frame bytes and independent payload
  bytes to agree. Copy a bounded4..65535-byte snapshot, compare actual enumerated
  lengths and retain independent4096-segment visit caps. The account wrapper
  keeps its existing472-byte cap and exact fixed-length callers.
- Mailbox mode parsing requires the complete10-byte payload header, whole
  eight-byte remaining values and count exactly equal to that quotient before
  casting/allocating. At most8190values fit. Invalid input returns false with
  null output. All7mode values, uint IDs, full ulong values and outbound bytes
  remain unchanged. Retained mailbox/account goldens and handler cases pass.
- Add43parser rows: count/overflow boundaries, physical count/alignment, invalid
  modes/short payloads, all valid modes, maximum aligned direct frame,14envelope
  inconsistencies, every split/independent backing, and4096/4097visit boundaries.
  Tests-first49916: **249passed/17failed of266**,0skip. Root read every failure:
  actual overflows, lying-metadata copy exception or unsafe acceptance assertions.
  Corrected39126: **266/266**,0fail/skip,710ms; all43new outcomes and TRX groups read.
  No old assertion was deleted, weakened or skipped. A separate static helper
  review found no actionable defect; it is not execution evidence.
- Full clean-public LOCAL e9f472f3 passes above; GitHub CI/live remain unproved.
  Limits: focused offline parser proof. Current target remains
  Chinese7899/cachedDX8D13, with no new receiver
  or actual-client evidence. The65534-byte positive constructs PacketFrame
  directly; transport/serializer default6844 is unchanged. Arbitrary throwing
  MemoryManager/concurrent backing mutation, other mailbox parsers, authorization,
  claim transactions/replay and UI lifecycle remain separate. No live process,
  SQL, original save, protected client, endpoint or held Guild/Union path changed.

### FSB-088: Preflight required physical combat owners and monster metadata

- Source: Physical.OnMonster could discover missing Rune, CombatGear, conditional
  Ninja or monster Family/Name only after damage output, RNG, Kylin updates or
  Lucky sends. Earlier tests explicitly preserved late Rune/Ninja/name failures
  and dirty prefixes.
- Non-floor admission now captures the actual Rune and CombatGear owners, Ninja
  only for spell16420/16430, and required Family/Name. Rune reuses its existing
  physical ownership check; Gear/Ninja retain their genuine constructor owner
  and reject mutable user rebinding as authority. Recheck bindings, target and
  spell ID after actual status-comparer traversal. Use captured slots, Family and
  immutable Name at the original late calculation points.
- Preserve floor-monster early return and unrelated-spell optional Ninja absence.
  Migrate unsafe Rune/Ninja/name and shared-row failure expectations to explicit
  InvalidDataException with unchanged output, resources, queues, counters and RNG.
  Retain the separate packet-recycle fault unchanged; it is not a fixed claim.
- Add35 rows and split the combined name/recycle fact (+1 row). Shared tests-first
  93354:634pass/41fail of675,0skip;32actual combat assertion/fault failures plus
  9bank empty-response failures (FSB089). Corrected86856: **675/675**,0fail/skip,
  6s. Root read counters, all failed messages, all35new outcomes, the split name
  row, corrected Rune/Ninja/shared-row facts and retained recycle fact.
- Full clean-public LOCAL f3bfae4a passes above; GitHub CI/live remain unproved.
  Limits: focused offline caller proof.
  Nested mutable Ninja/Gear metadata, arbitrary post-admission raw writes, packet
  lease/recycle, complete cast/resource transactions and actual native dispatch
  adoption remain separate. Captured owner slots do not make whole subsystems
  immutable or authenticated. No live process, SQL, save baseline or endpoint changed.

### FSB-089: Retain genuinely emptied warehouse banks for their owner lifetime

- Source: native Warehouse.AddItem creates an outer bank key and last RemoveItem
  retains it. Show emits one empty header for that existing empty key, while
  missing keys emit nothing. Saving/loading persists only item rows and therefore
  loses empty keys at fresh owner birth. The managed flattened item snapshot
  omitted this runtime distinction and emitted no empty response after withdrawal.
- Add private immutable transient bank presence, minted only from an actual
  selected before/after row moving through the durable warehouse transfer.
  Bind physical connection, Store identity, account/character and actual native
  client/player references. Existing persisted Store CAS publishes item move and
  presence together; captured transfer identity is checked again inside persistence.
  Failed moves publish neither. No NPC catalog/eligibility-based bank birth,
  second Set, global registry or item-file/schema marker is introduced.
- Show still validates complete seven-store graph, owner, PIN/Poker, target and
  capacity. Only a matching retained empty bank emits the existing empty header;
  physical-sash behavior and nonempty/lock/extra bytes remain unchanged. Normal
  same-generation copies retain presence; fresh loaded sessions start without it.
  No lifetime IsShow suppression is enabled. Static D13 receive evidence supports
  supplied-row upsert/removal but does not establish full resynchronization.
- Add10 registered-handler rows: missing bank; ordinary/ownUID/returned65535 last
  withdrawal; actual sharing refusal+retry; unrelated update; retired generation;
  same-Store foreign connection; actual file/new-session reload with generation
  unchanged; malformed foreign tail. Tests-first93354 had9actual missing-empty-
  response failures; corrected86856 **675/675**,0fail/skip, all10new rows passed.
  Root inspected actual TRX rows and full source/test packets. One retained
  whole-session equality helper now requires the new marker while preserving
  every original item/file/money/reference/recovery/wire assertion.
- First full clean-public45d1ca16/27979 finished failed at the solution-test stage:
  build0; core24988pass/1fail+165existing exclusions, updater305/305. The sole
  failure was the older Frame withdrawal test expecting no Show response after
  emptying returned bank65535 (74frames expected,75actual). Root read its complete
  operation and actual TRX. Correct that stale oracle to require exactly one
  empty header, exact bytes/parsed target/capacity, and unchanged item file; retain
  its withdrawal persistence and subsequent redeposit assertions. No production
  change, test deletion or skip. Preserve this failed receipt; successor full
  acceptance remains pending, and later launcher/parser stages did not run.
- Second full341365ba/48421 passed the corrected warehouse Frame row but failed
  5unchanged native counter/INI cases: core24984pass/5fail+165exclusions,
  updater305/305; later stages not started. The first258-character fixture path
  had20/20 passes; the deeper262-character path had15/20. The fixture nested
  long verification names under the checkout, crossing the compatibility API's
  [Windows path limit](https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation).
  Move only that fixture to an owned short Temp directory, retain create-new,
  reparse/containment and nonrecursive cleanup guards, and reject unsupported
  fixture path lengths clearly. All expected counter values/order/fault assertions
  remain unchanged. In the SAME deep checkout, focused60531 passes **20/20**,
  0fail/skip,121ms; actual counters and formerly failed rows were read. Production
  INI, native-counter semantics and activation remain unchanged. This is test
  portability repair, not long-path support or durable/global UID completion.
- Third full f3bfae4a/61648 passes all8stages above:24989core/305updater/57launcher,
  parsers20+13,0failed. Both corrected old tests and all83new combat/bank rows pass.
- Limits: component reload, not public login/client proof. The nontransactional
  mailbox1000 claim and reserved-UID alias remain unmapped; cache/reset lifetime,
  true housing authority, arbitrary stale-record/raw-writer publication and wider
  crash/power-loss guarantees remain separate. No file format, game/client/SQL,
  original save, protected baseline or endpoint changed. Full LOCAL is accepted
  above; GitHub CI and current actual-client acceptance remain unproved.

### FSB-086: Validate and capture actual minimum-damage owners before physical calculation

- Source: native-derived Base.AdjustMinDamageUser2Monster and its managed
  Physical.OnMonster caller. Partial client birth leaves Equipment absent; the
  old formula dereferenced it after assigning output and consuming damage RNG.
  A foreign equipment/player with the same UID was also accepted. An earlier
  test deliberately required NullReferenceException.
- A focused owned-input capture requires the actual current player and actual
  equipment owner, reusing IsOwnedBy. Null client gets ArgumentNullException;
  unavailable/mismatched physical owners get InvalidDataException. Capture the
  real level and both weapon IDs, then compute the unchanged minimum formula:
  1 + level/10 + left%10 + right%10. No replacement owner is fabricated.
- Existing physical admission captures those inputs and rechecks references and
  scalar values after real status-comparer traversal, before output/RNG. The
  captured integer minimum is used at the original Math.Max point. Actual later
  ExtraStatus callbacks can no longer cause this minimum step to reread missing
  equipment or change its sampled floor. No monitor is held across RNG/sends.
  The genuine floor-monster early return remains before this unused admission.
- Add24 owner/formula/caller/drift rows and migrate the old null oracle. Shared
  tests-first93629:494pass/24fail of518,0skip; failures are actual acceptance,
  exception-type or value assertions, not setup/cleanup faults. Corrected4347:
  **518/518**,0fail/skip; all38new rows across FSB086/087 and both corrected
  existing facts passed. Root read actual TRX counters, failed messages, new
  outcomes and both corrected facts.
- Limits: offline component/caller proof, not a full cast/resource transaction.
  Other combat owners, arbitrary raw writers, all mutable combat statistics,
  packet-recycle failure, native Range/Magic adoption and actual-client gameplay
  remain separate work. No host, SQL, original save, protected client or endpoint
  changed. Full clean-public LOCAL f3bfae4a passes above; GitHub CI/live remain open.

### FSB-087: Restore the explicit full-adjustment sentinel branch

- Source: Base declares AdjustFull=-32768 and contains return maxData for it, but
  the preceding <=-30000 branch made that equality unreachable and returned2768.
  Earlier tests intentionally enforced that defect.
- Test the exact AdjustFull equality before the broader absolute-set branch.
  Preserve other percent/add/set arithmetic and neighboring sentinel values;
  migrate the two old expectations to supplied maxData. Add14 direct/boundary/
  wrapper controls, including zero and int bounds.
- Verified in the same tests-first93629/corrected4347 cohort as FSB086:
  **518/518**,0fail/skip, all38new rows and both migrated facts passed. This is
  an intentional server-logic correction based on the explicit constant/branch,
  not a new client-confirmed spell rate or wire-format change.
- Limits: managed production callers currently remain internal adjustment
  wrappers with no external managed production call found. Native Range has a
  data-driven spell12850 call, but no literal Full use was proven there. Other
  unchecked arithmetic was not normalized. Full LOCAL f3bfae4a passes above;
  GitHub CI/live proof remains pending.

### FSB-084: Reject incoherent account envelopes and malformed credential fallback

- Source: previous managed legacy account parsers/AccountServerFrameHandler and
  AccountServer startup. Seed/forward trusted declared metadata while reading a
  different embedded header; authentication ignored the physical frame. Rejected
  parsing could enter a saved-credential fallback and issue Ready/a handoff ticket.
  These are unsafe server behaviors, not client compatibility requirements.
- A focused shared helper requires exact declared/embedded/physical/payload
  coherence, actual enumerated byte totals and bounded copying/comparison. Fixed
  frame sizes remain8/472/52; cap4096 segment visits per view, including empty
  segments. Preserve coherent independent backing and every normal split, existing
  Latin1/password-candidate behavior and all retained packet goldens. D13 binary
  evidence independently confirms seed opcode1059/offset4 only; this does not
  establish the protected native forward ABI or all three client wire contracts.
- Validate the envelope and fields before capture override or missing-repository
  shortcuts. Every malformed authentication emits InvalidAuthenticationProtocol
  with zero identifier/port/address, without repository reads, tickets or Ready.
  Remove the saved-credential fallback method/field entirely. Retain its constructor
  parameter as ignored source compatibility only; startup no longer constructs or
  passes credentials and emits a fixed disabled notice for deprecated settings.
  Rejection of an incoherent sequence does not traverse it again for diagnostics.
- Replace two old tests requiring malformed Ready/ticket/success-disconnect with
  safe rejection invariants. New tests cover93 envelope,12 segment-limit and52
  counted real-handler rows. Direct handler sends are an in-memory connection
  fixture, NOT TCP/client proof. Valid diagnostic capture remains explicitly
  diagnostic, not authenticated admission. No accepting fallback was invented.
- Parser-only61756:50pass/55fail of105; all12 existing packet goldens passed.
  Expanded tests-first31343:143pass/105fail of248,0skip:55parser,6segment-limit,
  43handler and1adjacent DBNpc failure (FSB085), all actual behavior/assertions.
  Corrected46691: **248/248**,0fail/skip; account/network regression30891:
  **629/629**,0fail/skip,67s, including actual TCP/shutdown/handoff test families.
  Root inspected counters/outcome groups and changed-case outcomes. Startup source
  compiled in this build; deprecated-variable startup notice was not process-tested.
- Real encrypted TCP addendum58042: **4/4**,0fail/skip (two retained wire facts
  and two new malformed/retry rows),179ms. A fresh tracked-only4d22ea7f archive
  plus the reviewed test-only patch receives the actual seed, encrypts coherent
  malformed472-byte and honestly short471-byte requests, and reads real socket
  rejection bytes. After awaited handler completion: zero repository reads/tickets;
  both continuously advanced cipher counters and the SAME socket then carry a
  valid request yielding exactly one read/ticket. Root inspected actual TRX rows.
  No production change or new tests-first RED is claimed by this addendum.
- Limits: valid capture override, saved-password owner rebind and opaque legacy
  uint/world handoff remain separate authority risks, not secure login completion.
  Byte coherence is not cryptographic authentication or hostile in-process
  MemoryManager/concurrent-buffer safety. A client depending on the removed
  malformed-credential shortcut must obtain genuine decoding/authentication; do not
  restore the bypass to pass a playtest. No running process/configuration, live
  SQL/client, original save or endpoint changed. Full clean-public LOCAL c1ee382d
  is accepted above; current GitHub CI and real-client acceptance remain unproved.

### FSB-085: Reject a null required DBNpc conversion input explicitly

- Native-derived DBNpc.Create dereferenced a required null NPC and the earlier
  conversion test enforced NullReferenceException. Add ArgumentNullException
  with parameter `npc` before field reads. Both actual SaveCapture callers already
  exclude null and do not treat the previous exception as permission/absence.
- Migrate the existing ten-field conversion fact to the named rejection and
  unchanged real HousePoll/BlockAttackMap snapshots. All fields,28-byte struct
  layout and binary goldens remain intact; no new owner/case or gameplay behavior.
- That fact failed at the exact exception assertion in31343, then passed with
  all54 house rows in46691/30891. This is invalid-input safety only, not house
  authority, furniture transaction, client or full parity proof.

### FSB-083: Validate and atomically publish complete house saves

- Source: native `MrRayzo.Role.Instance/House.cs:Save` and retained managed
  `LegacyNativeHouseRuntime.Save`. The old path truncated the destination before
  validating ownership/rows, silently returned on failed open, and could leak an
  open file when a null furniture graph threw. Count/enumeration could disagree.
- Capture the real physical client/player, immutable owner UID, exact current
  HousePoll/house/furniture references and all ten modeled fields. Require valid
  level1..6, nonzero key==UID rows, bounded complete image and unchanged capture.
  Bind the save path at construction or validated load; a later configured-root
  change cannot redirect that house. Existing cached load remains usable.
- Prepare five-byte header plus28-byte rows before touching the destination;
  preserve the exact ten-field golden. Alignment byte17 is encoded as0, not an
  arbitrary original-byte-preservation claim. Existing duplicate-loader fixtures
  now construct malformed bytes explicitly instead of requiring Save to emit them.
- Under the actual shared file coordinator, validate an existing complete image
  and reuse captured compare/atomic replacement. First save uses the same writer's
  create-only move, never replacement of an appeared leaf. No directory creation,
  fake owner, economic callback or accepting fallback. Absent house remains no-op;
  invalid present state throws clearly, I/O refusal reports IOException, uncertain
  publication fences later saves for explicit recovery rather than claiming success.
- Tests-first36395:33passed/17failed of50,0skip;22new rows were5pass/17fail.
  Fifteen reached direct behavior assertions; null-row/null-furniture failures
  were masked by cleanup of the old leaked file handle, not claimed clean RED.
  First corrected66410: **50/50**,0failed/skipped. Shared-writer/Trade/vendor/
  warehouse regression97350: **990/990**,0failed/skipped,17s. Root inspected actual
  TRX counters/groups, every house outcome and all changed source/glue/tests.
- Separate addendum97063: **994/994**,0fail/skip,18s, including all54house rows.
  Constructed/loaded path-redirection refusal and same-owner retry, malformed
  beforeimage refusal, and actual create-only first-save/existing-identical/
  existing-different controls pass. No concurrent creator/uncertain/crash injection
  is claimed by those deterministic controls.
- Limits: no current production caller of house Save is activated by this change.
  Full clean-public LOCAL successor is accepted above; CI/live housing still need acceptance. This is
  one-file replacement, not crash-safe furniture/economy coordination, SQL,
  power-loss durability, all raw-alias/ABA writers, hostile namespace/multiprocess
  safety, authenticated house entry or complete housing. Adjacent DBNpc null
  conversion is corrected separately inFSB085. No live/original-save/D:/routing
  change. Latest full LOCAL gate is exactc1ee382d above.

### FSB-082: Persist expired warehouse locks before client notification

- Managed Show omitted native SendItemLocked clearing; the reference mutated live
  Locked/Mode and sent an update without a durable item transaction. A failed save
  or stale owner could therefore disagree with what the client had been shown.
- Reuse the existing pure lock projection: selected displayed roots only,
  Locked2 with timer0 or strictly past YYYYMMDD midnight clears Locked to0;
  preserve timer/children/other banks/all unrelated bytes. Invalid required
  calendar state rejects the complete action before any partial output.
- Genuine private own-server identity issued through native startup is required
  for clearing. Missing/foreign/wrapped identity keeps existing read-only locked
  and future-date presentation; scalar0==0/catalog rows are not authority. Physical
  bags remain read-only and never refresh their deposited locks from Show.
- The real wrapper fully releases its Shared probe before fresh Exclusive
  selection and actual Trade/PIN/Poker/recovery/graph validation. Prepare all
  original locked Warehouse chunks, Update-mode cleared item frames, future
  dates and extras, then reuse existing full item-file persistence and Store CAS.
  Item file and Store are committed before the original chunk is sent. Release
  Exclusive before checked delivery; do not relock/refund after send failure.
- Root read all seven production postimages/glue, fixture change and nine rows.
  Tests-first67276:3passed/6failed of9,0skipped,852ms; all six failures reached
  handler/output assertions, not setup. First integration96413:1172/1172,0fail/
  skip,9s. Expanded broad73290: **2794/2794**,0failed/skipped,64s. Root inspected
  actual counters/outcomes, all nine new rows and zero missing prior1217rows.
  Independent DBItem byte oracle and per-frame observers verify save/Store before
  delivery, complete other-file/child preservation, sharing refusal/retry,
  malformed fifth-row refusal, owner-replacement suppression and durable replay.
- Limits: paused-send/second-writer interleaving is not directly tested by this
  synchronous observer fixture. Active-Trade/uncertain-IO/cancellation composition,
  IsShow cache coherence, genuine house authority, authenticated login, SQL,
  power loss/global UID/hostile namespace/raw aliases and actual-client proof
  remain open. This is focused offline acceptance; the latest full clean-public
  gate above now passes exact0dda7410; current GitHub CI remains unproven.

### FSB-081: Capture and validate warehouse Show before publishing any frame

- Ordinary/house-labelled/own-UID Show previously trusted stale Context state,
  could publish an early chunk before a later invalid row, and did not consistently
  enforce the actual current native PIN/Poker owner. Physical bags also lacked a
  complete detached view and checked owner lifetime across asynchronous sends.
- Select current Store/generation and actual physical client/player; validate the
  full seven-store graph, target/capacity and real main-password admission. Prepare
  every sorted chunk, future lock-date and aggregate extra frame before the first
  send; recheck captured owner/graph/attributes/PIN/target before each delivery.
  Read-only Show needs no invented Trade/economic/recovery owners or save call.
  Bank order remains chunks of four, per-row future dates, aggregate extras;
  physical bags retain their distinct single chunk plus extras and empty form.
- Root read all source/glue and nineteen new cases. Initial13210 was NOT nineteen
  behavioral failures: fifteen failed the strict pre-action save fixture because
  deliberately unsorted rows differed from its canonical parsed image; four were
  genuine handler failures. First integration91584 was1202passed/15setup failures.
  Corrected only test setup: save canonical rows through the unchanged strict
  oracle, then publish unsorted projections through the real session store. No
  existing assertion or shared save helper was weakened. Corrected tests against
  owned old50d production22783 measured2passed/17failed, all reaching handler/output
  assertions. The two existing-success controls remain meaningful.
- Corrected broad integration99870 passed **1217/1217**,0failed/skipped,16s. Actual
  terminal counters and all19new outcomes were inspected. Cases retain rich4+1
  chunk/future-date/extras bytes, current/stale owners, malformed late rows before
  any output, missing economic owners, real native PIN issuance/Poker and a fresh
  native owner after password change. Files and saved JSON remain unchanged.
- Limits: this Show batch alone did not implement expired-lock mutation (see
  FSB082) or IsShow caching,
  genuine house entry/furniture authority, authenticated account-to-world proof,
  SQL, live-client rendering or full warehouse/game parity. The latest full LOCAL
  Show gate above now passes exact5d78a448; current GitHub CI remains unproven.

### FSB-079: Commit all warehouse item moves before publishing success

- Ordinary2561/2562, house-labelled5121/5122 and own-UID10241/10242 previously
  sent success before unconditional stale-session Store.Set and had no guaranteed
  durable save. Native Warehouse.AddItem also ignored the advertised60/purchased
  capacity, and root-only ArrestToken checks could be bypassed by a stored child.
- Reuse one genuine exclusive/current-owner/recovery/Trade/PIN/Poker/full-graph
  transaction for all8item mutations, replacing the sash-only implementation,
  not adding a second save mechanism. Validate exact account/item baselines and
  replace the complete item image before Store CAS, then release the operation
  lease before checked success sends. Retain bank withdrawal declared-count0 and
  physical-bag count1; no successful packet layout/order changed.
- Deposit counts destination roots: NPC60 or strict saved/live purchased0..300;
  own-UID aliases share that cap. Reject ArrestToken711624 anywhere in incoming
  bank closure, not physical bags. Allow safe existing-item withdrawal despite
  malformed/overfull purchased capacity; do not trap already stored tokens.
- Root read both complete native warehouse files, actual owners and all proposed
  postimages/tests. Tests-first4491:9passed/28failed of37,0skipped. First broad64854
  passed1166/1166,0failed/skipped,17s, including all37new routes/capacity/refusal/
  retry/current-D13Frame cases plus retained physical bags, Trade/booth and files.
  Four additional genuine escrow300 composition rows pass in combined95817:
  offered-root deposit refunds; unrelated withdrawal preserves an unchanged
  offer's successful exchange. Combined1198/1198,0failed/skipped,13s, retains
  all41new warehouse/Trade rows, all28house rows and22native-INI rows. Actual
  terminal counters and individual new-family outcomes were inspected.
- Limits: one complete item-file cooperating-writer transaction, not SQL,
  power-loss, global UID, hostile namespace/raw aliases, durable request nonce,
  outbox/client delivery or complete housing. Actual loaded-house/entry/furniture
  authority remains required; arbitrary DynamicID/packet labels grant no access.
  Full clean-public LOCAL gate passes above; GitHub CI and live-client proof
  remain unaccepted.

### FSB-080: Parse a complete valid house before publishing its owner

- Native/current House.Load published MyHouse/HousePoll/BlockAttackMap before
  checking complete reads, trusted unchecked counts and silently kept the first
  duplicate furniture UID. Malformed files could replace previous state and
  huge declared counts could loop after EOF. Cached entries lacked owner binding.
- New bounded parser validates exact5byteheader +28byteDBNpc records, level1..6,
  complete count/length, unique nonzero furniture IDs and ordinary absolute
  server-selected file before publishing. An immutable constructor owner UID
  binds cache adoption; invalid state preserves previous client/pool/blocked-map
  and bytes. No house-entry or warehouse permission is inferred.
- Safe tests-first40008 measured0passed/5failed,35ms. Truncated headers and extreme
  counts were deliberately not executed against the old unchecked native loop;
  all16new rows remain for postproduction verification, none skipped/weakened.
  Root read actual native/current owner, complete source/test proposal and retained
  binary goldens, then integrated. Combined95817 passed1198/1198,0failed/skipped,
  13s, including all28house rows (16new) and the retained exact DBNpc golden.
  All truncation/count/duplicate/invalid-owner cases preserve prior state; repaired
  files retry successfully. This verifies the component loader, not login adoption.
- Required work remains: real authenticated login adoption/house-entry/furniture
  authority, raw mutable-owner lifetime, atomic saves/house economy, SQL and live
  client. This component parser is not complete housing or deployed behavior.

### FSB-078: Persist physical-sash transfers before Store and success delivery

- Actual registered2080 actions7681/7682 send success before unconditional Store
  publication; Frame autosave follows that publication and can only log failure.
  Direct Registry calls have no guaranteed save. Stale Context and awaited sends
  can overwrite a fresher owner; main SecurityPass/native IsCheckedPass is missing.
- Corrected invariant: real Exclusive/current owner/initialized exact-root recovery,
  complete seven-store graph and bounded full Character/item baselines, actual
  secondary-password and physical Poker admission, prepare frames then replace
  only the complete item file before persisted-session CAS. Coherent real Trade
  claims remain read-only; final Trade validation retains changed-offer refund.
  Release mutation ownership before slow sends and stop later stale-owner output.
- Tests-only52505 measured **0passed/31failed/0skipped**,1s, before production
  changes. Actual terminal TRX counters, groups and first failures were read;
  these are31 failing cases, not31 distinct bugs. Separate15570 measured0/3of3,
 243ms; all three orphan/malformed claim refusals failed before production changes.
  Root fully read frozen7file production and2file migration packets plus all new
  tests and actual owners, then intook7source paths and4legitimate caller-test
  migrations. The old paused-writer Fact becomes two controls (net+1) for actual
  Shared->Sash->Buy FIFO and postcommit notification not blocking a saved purchase.
  Existing four real changed-offer/refund tests stay unchanged. Three obsolete
  outer Shared/manual-save substitutes are removed; saved-image assertions stay.
  The test transport List Add is briefly locked, never across its awaited observer.
  First broader integration65642 passed **1107/1107**,0failed/skipped,17s. Actual
  terminal counters/outcomes were read, including all34new transfer cases, both
  replacement Booth concurrency controls and all four genuine changed-offer
  Trade/refund cases unchanged. Exact successful wire/full-file and password-issuer,
  stale-disk/current-owner, missing-owner/roots, orphan/malformed escrow, sharing
  refusal/retry and committed-send-fault/retirement cases pass. This is focused
  offline integration proof, not full Windows or live-client acceptance.
- Limits remain open: full Windows CI, live-client, native transport/Host/global UID,
  genuine poker/table lifecycle, SQL, power-loss, hostile namespace,
  arbitrary raw-alias or automatic uncertain-backup reconciliation proof. Existing
  public slot-source Windows run40248699439 was cancelled while queued/offline;
  no runner listener was started and no CI acceptance was inferred.

### FSB-077: Save the 90-CP sash-slot purchase before publishing success

- The registered action256 published a new session and all three success updates
  before attempting the character save; write failure only logged. It could spend
  from stale context, overwrite a foreign wallet/count baseline, ignore active
  trade/escrow or reinterpret a malformed present count as zero.
- Preserve native cost90, cap300, no-trade eligibility and the established
  CP/max300/count wire sequence. Take the real exclusive world operation before
  fresh current-owner selection, require actual initialized same-Users recovery,
  validate participant/generation/attributes/escrow and prepare all frames first.
- Under real Trades/Store/file locks, bind AccountId filename to Character UID,
  validate the complete bounded original file and economic baseline, and atomically
  replace only CP/count before persisted-session CAS publication. The new exact-image
  entry point shares the existing file writer; old callers keep their contracts.
  Verified afterimage commits, verified original rejects, uncertain disk outcome
  retains backup evidence and fences the real world instead of claiming unchanged.
- Tests-only9980 measured2passed/20failed/0skipped of22 (not20 distinct bugs).
  First integration90011 passed67/67,0failed/skipped,654ms, including all22new
  cases and unchanged old Frame/dispatch/action-codec controls. Terminal TRX
  counters/outcome groups read. Broad76790 passed **971/971**,0failed/skipped,12s,
  including all27new sash cases, the four booth/WAL recovery extensions and retained
  character-file, Trade, vendor, item-loader, Warehouse, dispatch and action-codec
  controls. All three exact encoding/newline cases pass (UTF8 BOM/CRLF, Windows1252
  without final newline, and missing count with CR-only delimiters). The actual
  send delegate observes complete saved bytes/current CP150->60/count0->1 before
  its awaited IOException; no refund occurs and an unaffordable retry sends only
  failure talk. A genuine ended marker after the first send stops remaining stale
  frames without undo. Actual terminal counters/outcomes were read. Public source
  export/full Windows integration remain pending; no new live-client proof.
- Limits: cooperating-writer, single-character-file managed transaction. No new
  item-file/SQL/multi-file journal, live-client, raw-alias/ABA, hostile namespace,
  power-loss or automatic retained-backup reconciliation proof. This action has
  no nonce: a later valid funded request intentionally buys another slot; this
  is not durable request-replay deduplication. No success-send fault may refund
  an already committed CP/count pair. Sash deposit/withdraw remain separate work.

### FSB-076: Validate and durably settle complete player-booth purchases

- Root-only booth admission allowed restricted deposited children, descendant UID
  collisions and malformed save tails. Independent session publication could
  overwrite an awaited ordinary writer or a stale item/wallet file; transferred
  children retained mutable seller aliases. Pending escrow could be overwritten.
- Reuse the existing complete save-graph capture; keep Trade's locked-partner
  permission separate from booth policy. Validate transferred roots/children at
  listing and purchase, including current Anima authorization. Nonzero Bound is
  forbidden; Inscribed2 compatibility and unrelated bound stock remain legal.
- Buy takes the real exclusive world-operation lease before lookup. Valid D11/D13
  Frame buys narrowly skip their outer shared lease. Prepare detached item rows
  and original six response frames, then use real participant identities, ordered
  pair CAS, baseline checks and the existing four-file v1 journal. Vendor mode
  requires absent escrow instead of using Trade's claim-clearing policy.
- Preserve 64-bit positive account-file IDs and 32-bit character wire UIDs. Recheck
  complete graph/attributes/permissions under the real commit locks. Send faults
  after commitment propagate without refund/resale; uncertain durable outcomes
  retain the journal and fence recovery rather than claiming unchanged files.
- Tests-first: initial7564 measured8passed/15failed of23. Settlement21158 measured
  12passed/25failed of37; exact TRX counters/outcomes were read. These are failing
  cases, not25 distinct bugs. First integration31702/78453 failed compilation on a
  missing Data import in the new journal partial; no test execution from those runs.
  The import is corrected. First executed integration47729 measured83passed/3failed
  of86: all37 new cases passed. Only two obsolete Bound2 acceptance assertions and
  the old D11 received-row alias assertion failed. Those now require rejection or
  detached full-byte equality; unrelated-row references, six packets, four files
  and exact economics remain. The pure planner's alias contract stays unchanged.
  Broad41883 passed **706/706**,0failed/skipped,9s; actual terminal TRX counters and
  outcome groups were read, including all41new booth cases. Four genuine registered
  PIN controls cover protected descendants, actual creation/activation and PIN
  creation after listing requiring current authorization at purchase. Registered
  Frame and sharing-failure/retry controls pass with retained Trade journal/recovery,
  vendor, item-loader, Warehouse and PIN regression families. Public full Windows
  40051408447 passed exactb8cc5cfa with24569core passes, as recorded above.
- Recovery extension:46762 passed **88/88**,0failed/skipped,3s,
  including all45booth cases and retained journal/restart controls. Four added
  cases feed real registered booth before/after images through actual v1 prefixes
  0/2/4 and fresh Frame admission, plus unknown-valid-image evidence preservation
  and both-admission fencing. This is focused component composition proof, not a
  production crash/restart or new fixed source defect. The earlier accepted booth
  full Windows run predates these four cases. Their export is batched with the
  sash behavioral increment, not a separate full CI run for four tests; broad76790
  also retains all four controls. Public integration of that batch is pending.
  Initial91833 measured85/3of88; diagnostic19981/53219 each1/3of4. The original
  zero-stat/HP1 seed legitimately clamped HP during startup, changing its INI.
  Only this new fixture now seeds real parsed Strength1 before any WAL capture;
  original wire/raw-file assertions remain. Callback diagnostics are raised in
  the test body so the production runtime boundary cannot swallow test assertions.
- Limits: offline cooperating-writer managed paths only. Live client, SQL, complete
  native host, global UID ownership, raw-alias/ABA, hostile filesystem, multiprocess/
  power-loss and durable notification delivery are not established by this batch.

### FSB-075: Validate complete Trade item closures before transfer or escrow

- Source: registered Trade Exchange/Persistence and Warehouse.Sash, corresponding
  native Trade/MsgWarehouse branches, and the complete PlayerItemSnapshot codec.
  Sash deposit accepts restricted children; the old Trade root-only check then
  transferred those children. Incoming descendant UIDs were also absent from
  collision checks, and malformed unrelated stores could enter new currency escrow.
- Fix: validate both complete seven-store save graphs directly, including unique
  positive identities, parser membership, one encoded child level, non-truncating
  32-byte UTF-8 perfection text and the existing 16MiB image bound. Do not bless
  malformed state by parsing a normalized/lossy serialization.
- Apply permissions only to transferred roots/children; retain the directional
  locked-item partner exception. Recheck captured physical/scalar owners and
  permissions inside genuine ordered pair/vendor/file commit boundaries. Check
  incoming descendant UIDs against all recipient stores. Detach incoming children
  and private persistence images; preserve unrelated published container references.
- Gold/CP admission now validates both current graphs before the real wallet/claim
  debit. Final malformed state retains escrow and resets confirmations; healthy
  stale/forbidden/colliding offers use the existing refund path. Shared cancel/
  disconnect persistence requires save validity, not preview width: Effect256
  refunds remain valid. Existing escrow writer, WAL, capacity rules and wire order
  are retained; no accepting provider, new UID authority or replacement journal.
- Verification: tests-only37423 measured13passed/33failed/0skipped of46 in2s.
  Many failures stopped at the first unwanted preview, not33distinct bugs.
  First production18841 measured45/1; the remaining test appended a warehouse
  row out of the loader's canonical order. Only that setup order was corrected;
  exact refund, reference, byte and wire assertions remain. Broad91557 passed233/233.
  Final30087 passed **245/245**,0failed/skipped,4s; actual TRX counters/outcomes
  read, including58new registered D13 cases and the old Effect256 refund control.
  Coverage retains journal/failure/restart/disconnect/escrow/transport, vendor
  persistence, Trade/TradeInfo/Warehouse codecs and all item-loader controls.
- Public full Windows40048716539 passed exact3f7a5a9f as recorded above.
- Limits: focused/offline managed and public integration proof, not all-trade safety.
  No live client/SQL/original-save/routing/deployment action. Warehouse transactions,
  arbitrary raw-alias mutation after checks, ABA, global cross-player UID ownership,
  hostile collections, multiprocess/power-loss and durable delivery remain open.

### FSB-074: Require Pounce's selected shield before admission

- Matching Pounce.Execute returns before Shift/damage/animation when either
  CanUseSpell or the selected-left shield requirement fails. The old managed
  executor checked the shield only after spending50 stamina/arming800ms cadence.
  The actual source row12770/sort84 and unchanged shield900000..900309 predicate
  establish this required-cast boundary, unlike the independent Tharn proc.
- Generic plan construction now checks the existing equipment selector against
  its captured CURRENT owner before preparing cadence/costs. Main/alternate
  override and absent-alternate fallback rules are unchanged. Ordinary Store
  replacement is fenced by the existing exact-record/native-generation CAS.
  The late executor check remains after awaited publication; shield loss then
  is logged as an admitted stop/no refund, not an unchanged precommit rejection.
- Tests-only94577 measured5passed/6failed/0skipped of11,587ms before production.
  Five rows fail first unchanged-owner assertions; the receive-envelope row
  directly observes the unsafe50 vs100 stamina. All7 retained intentions remain,
  with4 new registered D13 current-vs-stale and alternate-fallback controls.
  After two production paths, Release63487 passed **2833/2833**,0failed/0skipped,
  17s; actual TRX counters/outcomes read, including complete Pounce family and
  the prior2829 selection. Successful empty casts, exact source animation fields,
  damage/progression and receive-envelope ordering controls are retained.
- Limits: raw aliased equipment changes are not versioned/locked by this check;
  postcommit equipment/target/HP/experience/delivery and full-cast transactions
  remain open. No new movement/target/RNG phase or wire format was added. No
  live client, SQL, original saves or deployment. Full Windows verification above
  now accepts this published successor batch.

### FSB-073: Prepare generic magic and Recruit admission before resource publication

- Generic preparation formerly committed AttackStamp before affordability, spent/
  sent stamina before mana rejection, then spent both before active-cold refusal.
  Recruit independently composed the same split resource/cold mutation helpers.
  Five production paths now prepare the complete generic cadence, priced costs,
  cold dictionary and Stamina -> Mana -> ColdTime frames before one genuine
  native-holder-generation/exact-record CAS. No-Store compatibility stays an
  explicit local record, not a fabricated transaction. Shared Root costs are not
  reused, and an already prepared Root invocation is not charged twice.
- Keep generic Scurvy/DragonPierce saturation, alternate MPMaster, map1039,
  Grace's sufficient-priced-mana/dual-epic rule, status cold bypasses, excluded IDs,
  cadence durations and successful wire order. Recruit still ignores cadence.
  The generic clock remains real UTC, sampled once for the coherent plan.
  Negative cold/cadence and preparation arithmetic/date/frame faults refuse
  before commitment. Zero raw mana skips only the irrelevant rune dependency;
  real stamina surcharges still apply. No accepting catalog/provider is invented.
- Postattempt observer/send/cancellation faults propagate with committed/unproven
  diagnostics, never fake rollback/refund. Prepared frames do not reprice or
  overwrite same-client callback edits; actual generation retirement stops later
  publication. This reuses the tested real admission owner/CAS, not a saver stub.
- Tests-only81329:60passed/27failed/0skipped of87,659ms, before production edits:
  20new failures plus7 corrected old defect assertions. Earlier48477 stopped at
  a test lambda `_`/discard name collision; no tests ran; root renamed only that
  parameter. After production, broad23155:2825passed/4failed/0skip of2829,19s;
  all87 generic/class tests and8 retained player-denial corrections passed.
  Four other old debit/stamp assertions were then corrected, retaining all7
  original intentions and physical positive/retirement wire controls. Corrected
  Release78100 passed **2829/2829**,0failed/0skipped,18s; actual TRX read.
- All135 external generic callers were source-audited. Most null paths stop the
  chosen cast, but physical FlashingName/cadence and some token prefixes occur
  earlier; the physical regression deliberately retains its real FlashingName
  output. Native Tharn follows the void spell attempt independently, so its
  affordability dependency is unproven: the proposed physical-tail change stays
  HOLD. Later executor/target/equipment/HP/receive/delivery, raw mutable graph,
  SQL, persistence, live-client and whole-cast transaction boundaries remain open.
  Full Windows verification above now accepts this published successor; test
  totals are not parity.

### FSB-072: Check basic player eligibility before root1000/1001 admission

- Existing Root1000/1001 player casts paid costs and ran manual effects before
  the real executor's deterministic eligibility denial. The source reference is
  CanAttackPlayer.Verified; successful wire formats/rates are unchanged.
- One shared pure block retains existing safe-zone, HP/Dead/Ghost, Invisible,
  NeptuneCurse, AttackInFly, watching, Protect, map700/dynamic0 and Peace checks.
  The full effectful gate calls it at its exact old position; root calls it before
  preparation and during final pure revalidation. CombatGear/rune/counter/RNG
  phases are never preinvoked or duplicated. StarFlow's denial-effect outputs remain.
- Tests-first63354:5passed/26failed/0skipped of31,1s: four healthy/permitted-flying
  controls and the corrected retained low-mana test pass;26 basic/manual-prefix
  denials expose the late admission. Only tests/declared1001 inputs were intaken.
  After four production paths, Release38082 passed496/496,0fail/skip,12s:
  prior401 +30new eligibility +1 corrected low-mana +64 StarFlow controls.
- The retained low-mana Fact keeps the same registered monster cast and costs,
  but now requires unchanged complete session/native/catalog/monster state and
  no output, rather than the rejected stamina debit. The exact one-test public
  CI failure is recorded in FSB071. Successor public230ddf34755598c72cdd9aa33405175f3de1d2f6
  full Windows40045106740 failed eight retained basic-player rejection rows
  that still expected mana493 rather than the unchanged500. Core24432passed/
  8failed plus165existing NotExecuted (24605total); updater305/305. Build191
  warnings/0errors; launcher and later stages did not run. Actual receipt
  20261024T002943194Z-f83e485b finished2026-10-24T00:33:29.8260301Z;
  job120724937902 completed00:33:39Z. Root read exact HEAD, receipt, TRX failures,
  logs and terminal GitHub status. Five retained methods/eight original rows
  now require exact caster/target state, untouched native catalog and no frames;
  their setups, rows, HP/animation and diagnostic assertions are retained.
  Their corrected successor is now verified by full Windows CI above; this
  historical public230 run remains NOT GREEN.
- Limits: only these basic player gates for root1000/1001. Relation/event/final-map
  policies, effectful counter decisions, changing targets/raw metadata and later
  execution/delivery remain open. This is not a target lock, whole-cast/SQL
  transaction, live-client proof or full CanAttackPlayer completion.

### FSB-071: Prepare shared root/CounterKill/Rise admission before commitment

- Reference: matching community MsgAttackPacket.ProcescMagic and
  AttackHandler.CheckAttack.CanUseSpell.Verified. The old local Entry normalized
  shared native cooldown rows, stamped and ran item effects before later refusal.
  Its shared CanUse spent stamina before mana refusal and both costs before cold
  refusal. Source also permitted negative/wrapped cold arithmetic.
- Tests-only RED58227:32 new registered root cases =8passed/24failed,0skipped,1s.
  RED11445 adds2 genuine direct Rise-consumer cases:34=8passed/26failed,0skip,1s.
  Most failures occur at the first unchanged-owner reference assertion; later
  full-value/wire assertions on those rows were not reached. Two caster cases
  directly fail shared catalog-value assertions. Production was unchanged.
- After complete caller/source review, root integrated nine focused production
  paths: pure local cadence/metadata/range/cost/MP preparation, actual holder
  generation plus exact-session CAS, then source-order prepared publication.
  Complete costs/deadline/stamp/token are staged before gameplay mutation.
  Optional MPMaster is skipped for zero mana; valid MP rescue remains intended.
- Old partial-debit/catalog-mutation tests are migrated without removing rows or
  successful wire goldens. Cost callbacks now see the complete admitted image;
  prepared notifications do not reprice or overwrite later same-client changes.
  Focused Release78562 passed365/365,0failed/0skipped,10s. The34 new rejection
  cases and331 retained cases pass. Additional34 metadata/zero-cost and2 actual
  Store component controls pass in combined74649:401/401,0fail/skip,12s. The first
  addendum attempt5522 stopped at one missing test namespace import (no tests
  ran); root added that import, not a production or assertion workaround.
  Public98d0962e full Windows40043985349 failed one old defect-enforcing test:
  HandlerMagicAttackSpendsStaminaBeforeLowManaReject required a stamina frame
  that correctly is now absent (Assert.Single at8505). Actual results:24409core
  passed/1failed,165 existing NotExecuted; updater305/305. Launcher/later stages
  did not run. Build191warnings/0errors. Receipt20261024T001614227Z-3011f1f7
  finished2026-10-24T00:19:53.142736Z; job120721335395 completed00:20:02Z.
  Exact HEAD, receipt, terminal TRXs/logs and GitHub failure were read. This is
  NOT full CI GREEN; the old assertion must be migrated without restoring the bug.
- The34 additional boundary controls include9 registered6003 cases and25 exact
  private-component sigil cases retaining real sorts90/21 and parsed metadata,
  not invented dispatcher routes. Real Store controls prove same-record reinsertion
  retires captured generation, and an observer FormatException after publication
  propagates with complete costs/deadline committed, not null rejection/refund.
  UTC maximum has exact accepted/refused representability controls; no all-zone claim.
- Limits: no-Store local compatibility is explicitly nontransactional. Raw
  in-place writers, outer Focused/Intensify/Ride policies, later target/effect/
  clock/executor faults, delivery, SQL and durable whole-cast safety remain open.
  Real draws are not rolled back; a postcommit fault is not an unspent rejection.
  CodeFactor originally reported complexity here, not this gameplay diagnosis.

### FSB-069: Bound quest structures before allocation and load before expiry writes

- Former PlayerQuestSnapshotLoader.Parse allocated the declared row array before
  proving headers fit; late invalid tails materialized earlier rows, and impossible
  intention sizes escaped as OverflowException. A complete allocation-free
  structural pass now bounds every count by actual remaining bytes before records
  are created. Materialization keeps defensive bounds; valid bytes/order/cache and
  the existing format range are retained, including a valid image above16MiB.
- CharacterSnapshotLoader previously restored/persisted expired nobility before
  quest loading, then let quest faults escape its structured result. Quest load
  now precedes that save and uses existing read/parse error statuses; failed
  snapshots are null. UID mismatch precedence and healthy/missing quest behavior
  are unchanged. All real production callers were inspected for these statuses.
- Tests-first current:26passed/13failed/0skipped,361ms. Four bounded allocation
  controls failed, three parser/loader overflow controls failed, five real
  character-byte preimage controls caught writes before quest failure, and one
  reload fault escaped. Later assertions on those RED rows were not reached.
- Private Release776/776 passed,0failed/0skipped,5s: retained717, new39, existing19
  Character loader and1 legacy-login error-admission control. This is a scoped
  loader repair, not a file-size cap, immutable borrowed-span guarantee, all-sidecar
  transaction, handoff rollback or new-character creation rollback.
- Successor778/778 passed,0failed/0skipped,5s, adds two actual D13 Frame admission
  cases over the existing real fixture: malformed quest count/final intention plus
  expired paid nobility yield exactly one SnapshotError, no session admission and
  unchanged complete User/item/injected-quest files. Real recovery is ready, not
  a missing-owner refusal. No new fixture/provider or ticket rollback claim.
- Public999ce326 and full Windows40041857100 are verified above: all41 added
  core cases retained alongside24299 prior passes and165 existing exclusions.

### FSB-070: Make updater parent-directory rename pins genuine

- ClientDirectoryJunction.DirectoryPins retained metadata-only0x80 handles.
  No-delete sharing alone did not stop tested ordinary link-parent/target renames.
  Parent pins now request list+attribute read0x81 with read/write sharing and no
  delete sharing. Missing list permission refuses; no fallback or elevation.
  Leaf create/delete access, exact-path/reparse checks and manifest logic stay unchanged.
- Genuine private ValidatePair lease tests-first:2passed/2failed/0skipped,95ms.
  Link-parent and target rename unexpectedly succeeded (restored before failure);
  target-ancestor and real child replacement/junction compatibility passed.
  After the two small source hunks, complete updater Release305/305 passed,
  0failed/0skipped,1m10s; all301 prior cases retained plus4 real controls.
- Scope: ordinary directory rename exclusion for that synchronous lease only.
  This is not a child namespace/reparse-metadata lock, native alias closure,
  hostile-writer defense, live updater deployment or whole-update safety proof.
  [Windows sharing rules](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-fsa/8c0e3f4f-0729-49f4-a14d-7f7add593819)
  distinguish metadata access from participating read/write/delete access.
  Public999ce326 and full Windows40041857100 passed all305 updater cases;
  exact shared receipt and remaining verification boundaries are recorded above.

Current work does not close whole managed reward transactions. SuperGuildWar's
checked preparation now precedes its claim decision as verified above, but the
claim/session/two-file durable transaction remains open. Lottery cadence can
still precede cancellation/persistence. These remain FSB009 work, not safety
proved by the isolated UID-adoption proposals.

| ID | Defect / affected owner | Required invariant | Status |
| --- | --- | --- | --- |
| FSB-001 | CanUseSpell spends stamina/mana and mutates authorization/soul before late rejection; exposes rows on false | Validate and prepare first; rejection is side-effect free; serialize competing admissions | Admission fixed; full executor transaction still open |
| FSB-002 | Hairface storage retains null owner and fails later | Reject invalid owner at construction; valid ownership remains stable | Constructor fixed; adjacent storage audit remains |
| FSB-003 | Guild donation narrows uint to signed int, allowing a wallet credit | Positive bounded amount; wallet/fund/donation commit together | Open, not integrated |
| FSB-004 | League receiver uses a recycled packet after its wrapper was disposed | Keep exclusive packet ownership through final use | Open, joint graph held |
| FSB-005 | Trade/booth/inventory disconnect, duplicate UID and retry windows | Validated atomic/idempotent transfer with durable reconciliation | Scoped managed Trade closure FSB075 and cooperating-writer booth settlement FSB076 verified offline; broader inventory/global/delivery lifecycle audit remains open |
| FSB-006 | SQL multi-row save/load and native eager lifecycle are incomplete | Real transaction boundaries, constraints and recovery proof | Open |
| FSB-007 | Guild/league/title/catalog loaders publish partial state; file saves rewrite in place | Stage valid state; coherent publication and recoverable file replacement | Implementation/audit in progress |
| FSB-008 | Name-based GM/PM privileges and inconsistent title4001 equip/remove classification | Server-owned authority and validated consistent title metadata | Implementation/audit in progress |
| FSB-009 | Rewards record claim/MAC before grants; partial failure loses claim; IP/MAC indexing differs | Validate full reward; atomic/idempotent claim and grant; correct identity checks | Scoped SuperGuild preparation verified468 private/464public+4external skips; Lottery and durable whole claims remain open |
| FSB-010 | Guild/league lookups, appointment bounds and caller-selected guild IDs lack validation | Validate request, role, capacity and own-guild authority before effects | Implementation/audit in progress |
| FSB-011 | Packet byte-array constructor can overrun fixed allocation | Allocate complete input and expose truthful capacity before unsafe use | Constructor fixed; wider bounds audit open |
| FSB-012 | General native packet readers/writers trust mutable Size or MAX_SIZE instead of actual allocation | Check allocation, cursor, frame and lifetime before every unsafe access | Open |
| FSB-013 | Existing spell executors have late eligibility gates, resource wraps and shared metadata mutations; old tests preserve these | Full-cast preflight, bounded arithmetic and no shared metadata mutation on cast | Retroactive executor audit open |
| FSB-014 | Active managed title receiver changes memory before save success, mishandles wing4001 and untrusted subtype/catalog fields | Validated save-before-session commit, authoritative metadata and lifecycle-bound publication | Scoped opcode2313 fixed; durable recovery/native lifecycle open; classic route tracked separately |
| FSB-015 | Repeated ranking scans crash on invalid rows; Rune.Free can reopen occupied211 after byte-count wrap | Single current-owner queries and fail-closed capacity/occupancy checks | Affected queries fixed; broader owner audit open |
| FSB-016 | Updater relies on transient MainModule image enumeration; synthetic pipeline names collide with a real owned namesake test | Retained read-only image/exit attribution; unknown live state refuses; isolated signed fixture identities | Reader/isolation fixed301/301 both trees; point-in-time scan limits remain |
| FSB-017 | Classic opcode2152 Change uses unconditional Set from a potentially stale context | Current authenticated owner and persisted commit before publication; no replacement-session overwrite | Scoped managed route fixed; native lifecycle/durable recovery open |
| FSB-018 | Weapon slot aliases/corrupt tails and seal growth can authorize partial state or exceed response allocation | Complete physical admission and captured bounded packet before mutation | Scoped slot methods fixed; full2353/2354 receivers held |
| FSB-019 | Rune effects accept foreign owners, incomplete/duplicate slots and fault on missing metadata | Genuine current complete ownership and safe deterministic query | Equipped queries fixed; writers/full lifecycle open |
| FSB-020 | Shop refresh can loop forever or lose stock; counters wrap and Load publishes prefixes | Bounded staged book operations and truthful prepared publication | Native component fixed; economy receivers/durability held |
| FSB-021 | Shop countdown targets midnight plus a minute using three clocks | One bounded local-calendar countdown sample | Display calculation fixed; scheduler/reset transaction open |
| FSB-022 | Name policy regex checks only the final character; rename and uniqueness policies differ | Complete validated name policy and consistent authoritative uniqueness | Whole-string repertoire check fixed; rename/uniqueness and stored-name migration remain open |
| FSB-023 | Flag registration/reset/publication can mutate before a null bitmap/view or later failure | Validate/prepare before coherent current-owner registration; duplicates reject without side effects | Scoped component locally verified1931; full lifecycle/host transactions remain open |
| FSB-024 | Broader prestige rebuild still has legacy partial-publication, foreign-owner, duplicate-slot and arithmetic-wrap assumptions | Validate the whole genuine graph and prepare complete rank/guild state before coherent publication | Audit required; missing named-rune metadata is handled, not the whole rebuild |
| FSB-025 | RandomLite inclusive-range arithmetic can overflow or fault after consuming a draw; full-width sampling has limited domain and modulo bias | Bounded representable results, validation before draws and an explicit distribution/domain policy | Arithmetic bounds fixed; full-domain/distribution/fairness and concurrency remain open |
| FSB-026 | Potion bonus/timer precede fallible flag/message work; duration text always30 | Prepare all scoped effects before commitment; truthful duration | Scoped component locally verified1931; expiry/item/durable lifecycle remains open |
| FSB-027 | XP duration mutates escaped row unchecked and from stale owner | Checked selected duration, complete graph and final current ownership | Scoped component locally verified1931; whole XP/receive lifecycle remains open |
| FSB-028 | Physical calculation dirties outputs/RNG before bad status; mutates shared spell factor | Admission before effects; immutable catalog input | Scoped calculation locally verified1931; full cast/HP transaction remains open |
| FSB-029 | Invalid status can leave stale AI/partial ranks or false-success effects | Explicit unavailability before dependent writes/publication | Scoped callers locally verified1931; broader caller/host recovery remains open |
| FSB-030 | XP20 precedes required goal admission; malformed goal loads publish prefixes; stage count wraps | Complete validated owned goal/counter publication | Scoped component verified2392; durable claims/whole lifecycle open |
| FSB-031 | Sage flag precedes archive preparation; typed plans/frames can mismatch | Both genuine frames and exact current plan before flag commit | Scoped Sage verified2392; cast/host/delivery recovery open |
| FSB-032 | Shield/Stigma flag precedes animation/caller preparation | Complete bounded frames and caller before typed acceptance | Scoped effects verified2392; full cast/HP/durability open |
| FSB-033 | Final comparer can change earlier state or prepared native loan metadata | Pure complete state and exact live frame checks after last comparer | Scoped status/flag/potion paths verified2392; general packet/raw-writer audit open |
| FSB-034 | Rune insertion/cache publication can split; Count wraps; public arrays corrupt ownership | Complete owned staged publication, exact count and explicit unavailable queries | Scoped storage verified2896; full equip/load/save/economy/host remains open |
| FSB-035 | Refinement publishes before late status failure or spends chance/stamina before preparation | Complete real state/frames before scoped proc acceptance | Scoped procs verified3065 both trees and full Windows39958461749; full cast/HP remains open |
| FSB-036 | Per-session max+1 and deterministic starter item IDs can collide across accounts, deposits and restarts | One durable forward-only domain allocator adopted by every real producer before effects | Allocator169/169 incl51durable-intent cases; v3 remains a component prerequisite. Host activation, trusted migration, reward-journal reconciliation and all-producer adoption HOLD |
| FSB-037 | Native save truncates before validation, persists prefixes or loses nested deposits | Complete owned image and truthful atomic file outcome before acknowledgement | Scoped native save verified3116 both trees and full Windows39959714814; host/loader/SQL recovery open |
| FSB-038 | Fixed32 text reads/writes escape buffers or retain stale tails | Bounded field access, validate before write and explicit null admission | Scoped record verified34cases within3116 both trees; full load/postlude audit open |
| FSB-039 | Selected DivineGuard resets cooldown and publishes refinement before fallible status work | Complete real preparation and final ownership before stamp/status/draw effects | Scoped proc verified3214 both trees; outer authorization/combat/HP/host remains open |
| FSB-040 | Quest saves truncate; loads accept partial/default rows and split canonical aliases | Complete real image and coherent validated merge before file/map publication | Scoped persistence verified3465 both trees; host/SQL/raw lifecycle recovery open |
| FSB-041 | Rune admission filters Bound but debit consumes either binding | Debit exactly admitted ItemId+Bound and preserve opposite stock | Scoped direct/NPC debit verified236 private;229 public plus5existing external skips; whole reward/UID transactions open |
| FSB-042 | Quest objective mutation precedes unsafe packet work; counter wrap and split aliases | Complete current canonical graph, checked counters and real bounded frame before writes | Scoped whole operations verified292 both trees; native callers/full awards/delivery recovery open |
| FSB-043 | Normal and point shops append/debit without full modeled capacity | Complete required inventory rows fit before costs/publication | Scoped guards verified334 private;325public plus9existing external skips; native stack/partial/whole transaction work open |
| FSB-044 | Quest acceptance mutates shared metadata, can split indexes or throw after committed send failure | Prepare complete canonical maps/frame first; truthful accepted result after commit | Scoped acceptance verified418 both trees; native callers/awards/durable lifecycle remain open |
| FSB-045 | Duplicate exchange requirements reuse stock; partial consumption still awards; products can overflow | Complete summed material admission and private ordered full debit before reward | Scoped material repair verified446 private;442public plus4existing external skips; whole economy/UID lifecycle open |

## CodeFactor cleanup

2026-10-26 prepared xUnit2031 batch: the actual preceding full build reported36
unique diagnostics (72printed with summary repeats). Root reviewed/intook33
direct Single(predicate) overload changes across11public files/32across10private
files. The public-only publication test was not created in private. Every source
expression/predicate and expected value is retained;3complex Select/OfType cases
are deferred unchanged. Full91501b34 LOCAL/GitHub gates pass; build warnings fell
190->157 and actual unique xUnit2031 diagnostics36->3, matching the deferred set.
This is measured analyzer cleanup; no new CodeFactor dashboard count is claimed.

Current guardrail: defect-first batches, automated bounded cosmetics, no risky
packet-switch rewrites for complexity scores and no per-warning full-CI cycle.
Trade3f7a5a9f and boothb8cc5cfa repair gameplay safety, not a verified static-findings reduction.
The current dashboard count was not read for this batch; older totals below are
checkpoint observations, not the current queue. Passing CI is not backlog closure.

Earth/rank fullCI40002265954 passed with212 warnings. Root batches its8 new
test-only warnings across4 files:6 equivalent xUnit predicate assertions and2
explicit positive-result null checks. No production or protocol change, no new
suppression/exclusion, no separate fullCI per warning. Public Release focused578/578
passes,zero failed/skipped,2s; build204warnings/0errors after the batch. This is a
test-only follow-up to the exact fullCI code snapshot above, not another fullCI.
Latest remote CodeFactor count was not read; compiler warnings are not that count.

Exchange public8abeec78 is full-Windows verified above. Its two mechanical new
findings are corrected in the SuperGuild behavior batch; the new semantic
handler's complexity is deferred. That checkpoint report was3NEW/1FIXED,total2303
does not certify those uncommitted style corrections or full gameplay completion.

Acceptance public39eaaf45 is full-Windows verified above. Its3 new findings are
complexity in Acceptance, AcceptanceGraph and the isolated child scenario;0fixed,
global2301. They are deliberately deferred while economic defects are repaired.
The one observed xUnit2017 build warning was removed in the same behavior batch;
the build now reports204 warnings, not a zero-warning or zero-CodeFactor claim.

Latest shop checkpoint publica92b1fb0 passes full
[Windows39972176836](https://github.com/vgcman16/wolfconquer/actions/runs/39972176836)
in4m17s, completed2026-10-23T14:50:09Z. Core23475passed/0failed of23640
(165externalfixturecasesexcluded), updater301/301, launcher49/49;8stageexit0,
205build warnings. Receipt20261023T144607865Z-5325d992 finished14:50:02.2305785Z.
CodeFactor2NEW complexity (Honor shop admission and test wallet/wire assertions),
1FIXED objective arithmetic precedence,total2298. These methods are not rewritten
just for the score. The new xUnit2017 test-assertion style warning is queued with
the next behavior batch; no extra full-CI loop is started for that warning.

Previous objective checkpoint public8e5ff255 passes full
[Windows39971090546](https://github.com/vgcman16/wolfconquer/actions/runs/39971090546)
in4m50s, completed2026-10-23T14:41:57Z. Core23442passed/0failed of23607
(165externalfixturecasesexcluded), updater301/301, launcher49/49;8stageexit0,
204build warnings. Receipt20261023T143736718Z-b71804ed finished14:41:48.4630069Z.
CodeFactor4NEW (3complexity+1arithmeticprecedence),1FIXED,total2297. The one
mechanical parentheses correction joins the shop behavior checkpoint; validation
methods are not split for a score and no separate full-CI warning loop is run.

Previous rune-bound checkpoint public27e2ae9b passes full
[Windows39970166492](https://github.com/vgcman16/wolfconquer/actions/runs/39970166492)
in4m44s, completed2026-10-23T14:35:25Z. Core23372passed/0failed of23537
(165externalfixturecasesexcluded), updater301/301, launcher49/49;8stageexit0,
204build warnings. Receipt20261023T143059748Z-cab8dee5 finished14:35:18.3387670Z.
CodeFactor1NEW DuplicateCode in the existing batch inventory helper,0FIXED,
total2294. The separate bound-aware NPC batch retained packet order; shared-loop
cleanup is deferred to a coherent behavioral batch, not a score-only CI loop.

Previous quest checkpoint public0b0da909 passes full
[Windows39965399561](https://github.com/vgcman16/wolfconquer/actions/runs/39965399561)
in4m06s, completed2026-10-23T14:01:13Z. Core23360passed/0failed of23525
(165externalfixturecasesexcluded), updater301/301, launcher49/49;8stageexit0,
204build warnings. Receipt20261023T135722313Z-95cce8c2 finished14:01:06.3980602Z.
Both local3465 selections pass. CodeFactor4NEW complexity/0FIXED,total2293.
These validation/commit methods are not reshaped solely for the score. The debt
is still open; static findings and test totals are not gameplay completion.

Previous DivineGuard checkpoint publicaf3ca964 passes full
[Windows39962905914](https://github.com/vgcman16/wolfconquer/actions/runs/39962905914)
in4m43s, completed2026-10-23T13:44:48Z. Core23325passed/0failed of23490
(165externalfixturecasesexcluded), updater301/301, launcher49/49;8stageexit0,
206build warnings. Receipt20261023T134022374Z-6aac4289 finished13:44:41.9366811Z.
Both local3214 selections pass. CodeFactor3NEW (2complexity+1duplication),
1FIXED (the deferred testconstant),total2289. No cosmetic control-flow rewrite
is started; none of this proves completed gameplay or host activation.

Previous verified behavioral checkpoint: public01431fe3,
[Windows39959714814](https://github.com/vgcman16/wolfconquer/actions/runs/39959714814)
passed4m45s, completed2026-10-23T13:20:50Z. Core23227passed/0failed of23392
(165externalfixturecasesexcluded), updater301/301, launcher49/49, all8stageexit0.
Receipt20261023T131620699Z-d343aea0 finished13:19:58.8004245Z;206build warnings
remain. This is offline automated proof, not deployment/live-client/full parity.

CodeFactor on that exact commit reports4NEW (3complexity+1testconstant),11FIXED,
total2287. All10precedence targets from256d7a18 are confirmed cleared, plus1older
native-save existence finding. That one testconstant is now included with the
next real behavior batch, not a separate edit/test/commit/fullCI ceremony. Complexity debt
is retained, not hidden or reshaped solely for a grade. No zero-issues or finished
gameplay claim is made.

Previous256d7a18 reports19NEW=10readability+9complexity,0FIXED,total2294. Its
Windows39958461749 passed4m23s; all10readability targets now cleared by01431fe3.

Previous public7879381d reports2NEW complexity/21FIXED,total2275. All
nineteen targeted precedence/readability records across10C#files are confirmed
cleared, plus2older findings. The two new complex methods and older nine complex
findings are deferred per the defect-first guardrail. No packet switch/control
flow is rewritten merely for a score; no zero-issues/gameplay-completion claim.

Rune plus the single19-record parentheses batch now passes private/public
Release2896/2896 each,0failed/0skipped. Parentheses only make existing operator
precedence explicit; no formula, ordering or packet-switch body changed. One
fullCI passed for this coherent Rune behavior checkpoint, not each warning.
Actual remote analysis confirms the claimed targets; complexity debt stays explicit.

Latest verified public code4727d8b3:

[Windows39949964185](https://github.com/vgcman16/wolfconquer/actions/runs/39949964185)
passed4m02s on exact4727d8b38e48535505a602ad32d288bdcde83e2a, completed
2026-10-23T12:03:34Z. Actual TRX: core22524 passed/0failed of22689 (165 external-
fixture cases excluded), updater301/301, launcher49/49; all8 stages exit0.
Receipt20261023T115947032Z-230b177a finished12:03:27.4697567Z. Build204 warnings
remain. CodeFactor:6NEW (all method complexity),7FIXED,total2267. The six complex
safety methods are recorded as maintainability debt, not reshaped for the score.
This proves the committed offline batch, not live-client/deployment/full parity.

Previous verified public code7a79ebc5: **0NEW/4FIXED**, **2268 total findings**,
net59 below2327. The three targeted follow-ups and one older RNG arithmetic
record are inactive. Full Windows39945614990 passed4m06s on exact
7a79ebc5c0098e6a78f88d0b6ebde752fca50313, completed2026-10-23T11:27:05Z:
core22196 passed/0failed of22361 (165 excluded), updater301/301, launcher49/49,
all8 stages0. Receipt20261023T112314959Z-b46ced1a finished11:26:58.3548561Z.
Build203 warnings remain; this is offline CI, not live-client or deployment proof.
Cosmetic follow-up loop stops here; next work is real flag/AI/caller safety.

Earlier published5a62e0d4: **8FIXED/3NEW**, **2272 total**. All seven older API
targets cleared; the eighth fixed record is an older duplicate that was reissued
under a new key. Two new findings are nameof suggestions in test expectation
constructors; literal ParamName assertions remain independent. The third is the
existing HW/Ninja Rank mutation duplication, not new gameplay functionality.

Current combined defect-first successor passes private/public2646/2646,0fail/
0skip. It includes the real FSB025 range repair, two test-expression fixes and one
shared typed HW/Ninja capped-ranking algorithm with19 new controls. Genuine
fields/constructor/gate/ref dictionary/ref cap remain; UID is captured once and
upsert uses one lookup. Stable unsigned ties/caps/row identity and late cap reads
remain tested. Mutation-before-fallible-sort/raw-race safety is still open.
No packet switch, protocol layout or handler ordering was refactored for score.
Remote analysis and full CI now verify this combined behavioral checkpoint above;
no per-warning full-CI loop is intended. Defect-first batching is now explicit in
the engineering policy.

Full Windows39943323494 passed on exact5a62e0d47751583f866a52c4fb910afec532f532
in4m09s: core22146 passed/0failed of22311 (165 excluded), updater301/301,
launcher49/49, all8 stages0. Receipt20261023T110433716Z-671600c3 finished
11:08:20.2704671Z. This verifies the preceding name/API batch, not the current
uncommitted successor, deployment or live-client play.

Earlier published checkpoint1990e8bc reports **0NEW/15FIXED**, **2277 total**
findings, net50 below the2327 baseline. All15 complexity/arithmetic records
introduced by2b are confirmed inactive. Full Windows39941895742 passed on exact
1990e8bccbc33bfede505b567ef3aaf5698a5db5: core22083 passed/0failed of22248
(165 fixture-dependent cases excluded), updater301/301, launcher49/49, all8
check stages exit0. Receipt20261023T105105363Z-d2463376 finished10:55:38.2705674Z;
the job completed10:55:45Z in4m55s. Build warnings remain; this is not deployment.

The next older-quality batch passes private/public Release2596/2596,0fail/0skip.
It includes the FSB022 correction above, five CA1853 ranking Remove lookups and
two CA1507 parameter-name literals. Four ranking owners retain successful-removal
sorting/caps and missing-key reference identity; Nobility still refreshes real
remaining rows even when the key is absent. Fourteen genuine owner/API controls
check single lookup, ordering and row identity. Nine exception/seeded-state cases
retain exact parameter messages and no draw on invalid ranges. The known
RandomLite full-range bug is separate FSB025 debt, not new safety acceptance.
Its actual full CI and remote analysis are now recorded above.

Earlier CodeFactor analyzed public2b993ece: **18FIXED/15NEW**, total **2292 findings**.
All seven targeted CA1827 and six CA1847 records cleared, plus five older records.
The15 new records are12 complexity and3 arithmetic-readability findings. They
were addressed by1990e8bc through semantic preparation/admission/parsing phases,
not hidden by suppressions/exclusions; all15 are confirmed cleared above.

Private and independent public Release each passed **2255/2255**,0fail/0skip,
including22 new classic/shop phase controls and the expanded full XP-flag/
prestige families. Validation order, reasons, physical ownership, copy-on-write
publication, prepared packet bounds and wire goldens remain checked. The prior
2164/2164 selection and31 name-search controls are retained. Full name policy
FSB022 remains open; tests do not depend on uncommitted Guild/Union APIs.

Full Windows39939873007 on exact2b993ece failed: core22056 passed/5failed,
165 excluded; updater301/301 passed; launcher/later checks were not run.
Receipt20261023T103337163Z-06e30071 finished2026-10-23T10:37:01Z. The five
failures were old tests requiring missing-rune exceptions. They now assert
coherent XP replacement/addition with zero bonus, complete status/timer/packet
state, and complete prestige/rank/guild updates with no named rune bonus.
No malformed vector was made a valid fixture; the broader flag transaction and
prestige rebuild remain separate audit work. The current2255-case selections
pass all five corrected cases. Full CI1990e8bc now passes as recorded above.

The unchanged native CRT controls require a measured PowerShell7 binding. An
earlier missing/array-valued environment caused8 probe failures in both trees;
the exact measured executable passed all eight without changing/skipping tests.

Previous verified checkpoint: full Windows39933407353 passed in4m06s on exact
`eae56f67e7ba11216b605c108a32abd54d40c73f`: core21682 passed/0failed
(165 excluded), updater301/301, launcher49/49 and all eight check steps exit0.
This is offline/synthetic CI, not deployment or complete gameplay parity.

Earlier remote snapshot: **2295 findings**, net32 fewer than the2327 baseline.
Commit8f7ea7 reports0NEW/7FIXED; follow-up eae56f67 reports0NEW/3FIXED. The ten
fixed records include all seven newly introduced warnings and three older ones.
The entire TitleStorage.cs file report now has zero active findings. Its handler
and flag-update phases remain behaviorally covered, not suppressed or excluded.
Private/public Release each passed1119/1119 again, zero failures/skips, after
the one-file follow-up, now also full-CI verified above. The15 CA1865 fixes
were already confirmed absent; their group contains only the held Guild record.

The seven-warning successor is now locally verified: private and independent
public Release each passed **1119/1119** selected core tests and **301/301**
complete updater tests, zero failures/skips. Named validation/commit/parsing
phases preserve short-circuit order, reasons, canonical metadata and real
save-before-publication safeguards; explicit arithmetic parentheses retain bytes.
The standard PID guard still rejects nonpositive IDs before native calls.
Link-control cleanup now attempts every exact-owned cleanup, preserves the
primary assertion/skip and every cleanup error in order, and retains the original
stack for a sole failure. Seven new failure/skip/stack/order controls pass.
No blanket suppression, protocol change or weakened test was used. Both8f7ea7
and its later one-file follow-up passed full Windows CI; the remote report also
confirms the targeted findings cleared. This is not a completed backlog audit.

Authorized alongside behavioral repairs. The public report was retrieved on
2026-10-23: grade A, 2,327 findings across 4,864 files. Category counts: complexity
822; duplication 196; performance 106; maintainability 1,203. This is a snapshot,
not proof that every reported issue has been inspected.

Later same-day report: grade A, **2,325 findings across 4,867 files**. The two
Hairface CA1827 records are no longer listed: existence uses `Any`, and catalog
resolution uses one `FirstOrDefault` instead of counting then repeating a lookup.

Three CA2244 findings are now addressed in test fixtures: two differently named
constants intentionally share one roll key and both assigned the same value.
Removed only the redundant second writes; a new alias Fact plus shared assertions
strengthen all 71 affected existing cases. All 72 are in the **510/510** selections.
No gameplay or packet field was changed. The refreshed CA2244 report now returns
zero active findings; overall snapshot is **2,322 findings across 4,875 files**.

The eight CA1827 findings across five ranking/rune owners are repaired and
verified in both 754-case selections. The refreshed report now shows **2314**
findings: the eight selected records are gone, in addition to the earlier five.
Only the named findings were deeply inspected; this is not a full debt audit.
Another15 CA1865 character-overload fixes are verified in private/public Release:
**1112/1112** selected core tests and **301/301** complete updater tests, all green.
The78 new controls retain path/comment/status/UTC/URL decisions and actual parser
outputs; no measured speedup is claimed. One held Guild-persistence finding is
excluded from this batch. The newer remote snapshot confirms these15 absent.
Report: https://www.codefactor.io/repository/github/vgcman16/wolfconquer/issues

No blanket warning suppression or exclusion is authorized. An A grade is not
gameplay completion; retain behavior and protocol tests through quality refactors.
