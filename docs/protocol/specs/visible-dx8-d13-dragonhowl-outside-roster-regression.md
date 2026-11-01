# Dragonhowl outside-line Team recipient regressions

## Identity and scope

SOURCE ONLY / UNBUILT / UNRUN, 2026-11-01. Target VisibleDx8D13; producer
snapshot08 plus the frozen overlap/award fixture inputs. This adds tests only.
Real Physical17490/17650 selection, saved gear201, complete native19 metadata,
ordinary admission and Team create/invite/accept packets supply the owners.
No supplied roster, query receipt, future holder or accepting saver is used.

## Packet and calculation contract

Actor (830,584) attacks actual family910 monsters at (831,584), optionally
(832,584). The only Team recipient is (830,594), outside both horizontal proc
lines and within the source Team share distance18. It must receive XP without
appearing in animation targets or losing HP. The life500 target yields10 Team
XP (500/50), with no newbie, spouse or rate multiplier. Selected monster damage
is4200 for both spells. CP is5 for each committed kill; actor cost is7 mana and
5 stamina. No player target means no PK flashing prefix is earned.

Scalar input uses threshold1000000: recipient level10/XP10, HP30, maximum
HP/mana/magic30/300/11. Threshold input uses threshold1: level11/XP9, automatic
Vitality20/Spirit20, actual full query maximum540/600/21 and HP restored540.
Independent2176 literals assert EXP10/9, level11 and HP540; 2301 precedes final
EXP only for a genuine threshold query. Final2239 contains only real monsters.
The complete saved file set is compared against declared scalar changes.
Missing Spells is a pre-bootstrap input and remains absent throughout: no player
death or derived-spell grant is selected here.

## Ownership and failure boundaries

Pre-cost control awaits actual recipient CompleteTransportCloseAsync after real
Team formation. Current cleanup removes/retire its holder but leaves the actual
Team row retained; the test proves both facts, without reconstructing that row.
The next physical request must refuse before any actor cost/stamp, monster
damage, file write or successful output. If real cleanup later removes membership,
this setup must be reconsidered rather than silently claiming stale-member proof.

Postaward control starts actual recipient transport close during the first real
EXP output, after checking durable/native award, CP and the first killed monster.
The installed close callback synchronously marks the creation/trade lifetime
ended; its removal waits for the current exclusive Frame. The test never waits
inside the send callback. Actual recipient receipt/currentness checks must fence
later targets; the queued close is awaited after the Frame returns. Earlier
committed XP/CP/kill are retained, not rolled back. A second monster remains500HP,
no final animation is emitted, and another Physical cannot replay the prefix.

## Validation limits

Ten desired actual-Frame cases: four positive, two pre-cost retired-member and
four postaward EOF cases, covering both spells and scalar/threshold outcomes.
No execution occurred. New client captures, full-suite verification, crash-wide
reward atomicity, floor edge cases and additional event/map owners are outside
this test intake. The production snapshot remains independently unqualified.
