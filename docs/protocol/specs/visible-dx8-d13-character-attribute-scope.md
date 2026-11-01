# VisibleDx8D13: preserve unrelated character attribute data

## Identity and scope

Revision2026-11-01. This corrects server-side DATA projection, with no packet or
catalog layout change. RealJointHitPreservesCaseDistinctOpaqueAttributesOnBothOwners
exposed an actual ordinary-hit clobber: generic WithCharacterAttribute copied
an Ordinal map into OrdinalIgnoreCase, replacing Other=upper with other=lower.

The selected correction is the shared copy/edit boundary rather than a special
Physical-only replacement. The shared With/Without helpers are used by combat,
status, economy and UI projections. All ten direct bulk-copy callers were also
reviewed: Instance, MonsterDeath.SkyTower, BattleField.Advance/PointPayout,
Labyrinth.FloorAdvance/FrozenGrottoMutations/SimonEntryMutations, TopHours,
BotJailRelease, and Teleport.Destination. Their proposed owned-key writes/removal
must use the same case-insensitive unique-alias edit when preserving the source
comparer. StanceProjection has the same forced-comparer clone and joins this fix.

Stance locally reads ActivePick through the same unique-key resolution before
its existing true/yes/nonzero rule. A lowercase alias is interrupted and removed;
ambiguous aliases refuse before publication. Other generic readers stay separate.

## Packet and catalog contract

Not changed. Existing D13 Physical/stance packet byte goldens and independently
established HP/proficiency results remain acceptance criteria. No new opcode,
table, formula, authority, receipt or runtime participant is created.

## Validation and transaction rules

Use the existing real LegacyNativeAttributeMapProof.CaptureRaw contract: exact
Dictionary or ReadOnlyDictionary backed by exact Dictionary, with Ordinal,
OrdinalIgnoreCase or EqualityComparer<string>.Default. Preserve that comparer,
all original key spellings and every unrelated value. Unsupported comparer/map
shapes remain refused, without invoking arbitrary dictionary callbacks. Check
the existing raw witness before returning the copied DATA.

Only the key being set/removed is owned by that operation. Match its existing
spelling case-insensitively; retain a unique spelling, append the requested name
only when absent, and reject multiple matching spellings even if their values
agree. Unrelated case-distinct keys are valid opaque DATA. A remove of an absent
field leaves the original CharacterSnapshot untouched. This is not a license
to normalize every runtime reader or accept ambiguous persistence fields.

Copies are proposed DATA only. Current real Store/native/item/spell/metadata/
HP/proficiency/operation proofs remain responsible for committing and fencing;
no new fallback owner, recapture, accepting callback or successful outcome is
introduced. Actual bound Physical/stance controls require ambiguous used fields
to fail before cost, stamp, files, native state or success output changes.

The reviewed outer bulk workflows validate their edited keys before effectful
PrepareLegacyTeleportAsync, which invokes entry side effects despite its name.
Their later actual current-holder/file commit checks remain required. Instance
prepares its proposed Sky Tower fields before removing old dynamic monsters;
Sky Tower completion prepares them before suppressing respawns. Suppression
still precedes GetVisibleDynamicMonsters because that method refreshes respawns.
These edits do not establish globally atomic teleport/reward transactions.

## Expected behavior and fixtures

Pure DATA controls cover all three actual supported comparers and direct/wrapped
maps, independent unrelated key/value/spelling expectations, unique owned alias
updates/removal, ambiguous aliases and unsupported callback-shaped maps/comparers.
Existing raw-witness drift and owned output-currentness controls remain required
neighbor checks; they are not replaced by these DATA tests. The existing actual
joint-hit regression stays unchanged.

Actual configured-admission controls publish unrelated fields and transient pick
DATA through the real Store, execute changed stance50 then real Physical ingress,
require a new holder and real stance/interruption outputs, retain comparer and
opaque values on both owners, preserve existing wire/HP/proficiency goldens and
saved files for stance. Separate actual attacker/target ambiguity controls use
ThundercloudSight aliases and require preflight refusal with unchanged snapshots.

Five additional existing-component Sky Tower cases use actual Info registration,
Physical death and real monster lifecycle APIs. Four completion-field ambiguity
controls preserve completion/attempt state and genuine respawn eligibility; one
positive retains exact2382 progress476/1 then attempts476/9 wire goldens and real
respawn suppression. The accepted lethal hit precedes completion preflight in
that component route; these controls do not claim native admission or whole-hit
atomicity.

## Evidence alignment and qualification

Evidence is current WorldPacketHandlers.Common.Attributes.cs, the ten named
bulk-copy callers, StanceProjection, existing raw-map/admission/runtime-tick
editors, and the real focused04 failure. No new client observation is needed for
this server DATA defect. Source authored offline only; Root owns build/test and
integration gates. SQL, crash safety, live client stability and full parity are
not established by these controls. In particular, LegacyTeleportDestinationReducer
still forces OrdinalIgnoreCase in its separate CopyAttributes. Several reviewed
NPC callers pass through it, so end-to-end teleport comparer preservation remains
OPEN. Broader projection constructors, reader aliases and unjoined old generic
caller transaction timing also remain separate audit frontiers.

## Root focused qualification - 2026-11-01

Release core-test build10 passed0errors/103reported warnings in39.84s after
adding the missing Runtime namespace to the new SkyTower fixture. Earlier
no-restore solution build09 remains failed (that import plus three unrestored
updater asset files); it is not a passed full build. Focused11 actual TRX has
169Passed/0Failed/0NotExecuted in18s, including all25new cases, the original
opaque joint-hit regression and existing Instance/SkyTower/NPC/teleport neighbors.
This is local focused evidence. Full Windows CI, the separate teleport reducer,
SQL/global atomicity and actual-client acceptance remain open.
