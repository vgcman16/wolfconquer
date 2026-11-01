# VisibleDx8D13: earned XP continuation and ordinary Physical serialization

## Identity and scope

Revision2026-11-01. Target is the existing VisibleDx8D13 server profile, not a
newer client schema. This is offline regression coverage of the real Frame
pipeline. Root owns integration and execution; these source files are unbuilt
and unrun. No client/updater, original save, route or host setting is changed.

The requirement is coherent gameplay while an already-earned XP continuation
is publishing a level transition. An ordinary player hit must not overlap that
continuation or run from its intermediate level/query state. After the actual
continuation completes, a valid queued request should execute normally.

## Packet contract

Use actual Physical MsgAttack requests through WorldServerFrameHandler, with
authenticated requester UID, distinct admitted target UID, real coordinates,
zero incoming damage, and the existing D13 framing. Production schemas/IDs are
unchanged. Level and final experience2176 updates retain their current ordering;
the queued PVP interaction must occur after the continuation's final experience
update. The existing2135 interaction display scales only the display value:
the fixture's100 applied HP damage displays floor(100*22/27)=81.

## Catalog/table contract

The owned saved fixtures admit actual class7051/reborn1 actors, a zero-stat sword
410009 in native slot4, and proficiency410/level5/XP100/needed1600000. Both sides
have strength100, agility0, vitality447, spirit0, map1000/dynamic77, PkMode0 and
zero mana. Actual monster family910 has life2500000 and level137, registered
through the real Frame MonsterRuntimeStateStore birth/range APIs.

Declared level thresholds are137->1,138->1,139->1000000000. They enter the real
catalog before configured admission, not a replacement runtime lookup. The
actor starts137 and the peer139, so both are139 when the queued hit runs.

## Validation and transaction rules

The actual D13 Physical ingress takes WorldOperationBarrier.Exclusive before
capturing any action/hit request. The genuine monster hit earns its real award;
the driver durably records level138/step1/QueryPending before awaiting its first
level138 output. The handler keeps the same real Exclusive across this await.

Queue a second actual Physical request while this real sender is suspended.
Read-only inspection must show one held writer, zero readers and one real
exclusive waiter. Neither a test nor a callback may create/register an operation
lease, pending owner, request token or successful outcome.

While queued, preserve both exact holders, binding/saved-image pointers, native
resources/level/XP/action/stamp, flag/spell/proficiency maps and row values, real
Frame hit slots/generations/repeats, every saved file and all accepted outputs.
Advance only the configured actual clock beyond ordinary cadence. Releasing
the real sender lets the original continuation clear itself; then the genuine
queued request can validate and commit against the completed state.

## Expected wire behavior and fixtures

The two theory cases place the earned pending recipient first as the queued
attacker, then as the queued target. The first real monster response proves the
award amount; no pending attribute, ledger row or owner is synthesized.

After two actual level steps, the actor has level139,18 attribute points and
remainingXP=originalXP+creditedXP-2. Its independently calculated maximumHP is
447*24+100*3=11028. The later real PVP hit applies100 damage: peer2000->1900 when
the earned recipient attacks; restored actor11028->10928 when it is targeted.
Both sides receive exactly one matching PVP interaction, displayed81, and saved,
managed and native proficiency images agree after the real Frame postlude.

Cleanup always releases/observes real handlers and preserves the original
assertion/exception if a later cleanup also fails.

## Evidence alignment

| Requirement | Existing source evidence | Fixture observation | Scope |
| --- | --- | --- | --- |
| Actual Exclusive at Physical ingress | WorldServerFrameHandler.Dispatch.cs and CombatOperations.cs | actual writer/readers/queue, read only | source authored; execution pending |
| Earned marker before level prefix | WorldPacketHandlers.ExperienceContinuation.Driver.cs and existing driver tests | real monster response and saved QueryPending marker | source authored; execution pending |
| No intermediate-state hit | held actual ingress operation | unchanged complete pending snapshot | source authored; execution pending |
| Completion permits hit | actual owner clear and subsequent packet dispatch | literal level/XP/HP/display and saved/native book checks | source authored; execution pending |

## Implementation and qualification

New partials are LegacyOwnedExperienceContinuationDriverTests.PhysicalSerialization,
PhysicalSerialization.Fixture and PhysicalSerialization.Observation under the
existing real test class/collection. No production code is altered.

The direct ordinary marker-fence refusal is NOT exercised by this serialized
Frame path: the second request cannot reach that guard before the original
owner completes. This coverage proves real serialization/no overlap and valid
postcompletion execution, not reachability of every lower-level fence. Existing
earned output-failure/reconnect/restart tests remain separate. No live Twin City,
SQL, mailbox, process-crash, host-trust or complete parity proof is claimed.
