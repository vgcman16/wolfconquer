# Prestige client calculation

This managed C# increment applies [Sanitized Parity](engineering/SANITIZED-PARITY.md)
to prestige calculation and perfection-effect publication. It changes no packet
ID, layout, encoding, or confirmed client sequence; VisibleDx8D13 remains the target.

Qualification: tests-only old42a baseline was 35 failed/207 passed/zero skipped
(242 total). The wardrobe-save caller baseline was two failed/six passed.
Corrected public-derived Release passed735/735; independent private Release
passed740/740, including five compact-English controls. All baseline names pass.
These are focused offline checks. The new exact-public full Windows gate is pending;
preceding42a1116b passed Windows40466127994 as recorded in the status documents.

## Prepare before publishing

CreatePrestigePoints now prepares a private point array, total, Entry, copied Entry
points, and effect value before changing the actual client. Real Entry.AddInfo
reads the physical appearance/flag owners into the unpublished Entry. Text must
fit the existing saved-row rules: nonblank name, nonnull guild name (empty allowed),
no slash/control characters, and valid Unicode; no new name-length cap is imposed.

Required owners include the actual player, equipment, HundredWeapons, wardrobe,
rune, beast, physical PerfectionStatus, and configured server identity. Equipment
array/item references and ItemPoints values are captured and rechecked. Optional
Chi, InnerPower and JiangHu may be absent; present containers must match the player
UID and retain their captured references. SubClass has no UID/owner API, so its
actual player-slot reference is captured and rechecked without invented membership.

When guild and member both exist, the member UID must match the player and that
exact object must be registered in the captured real Guild.Members dictionary.
The same binding is rechecked before publication. Missing guild/member retains
the source optional skip; no replacement member is created.

Wardrobe retains its mutable public Owner, but owned use now also requires its
actual constructor owner and current client slot to agree. The item-save graph
also requires this guard. Foreign-born and null-born wardrobes cannot gain save
authority by rewriting public Owner; both real-file refusals are in the checks.

Local publication holds the existing rank SynRoot, rechecks captured targets, and
calls the rank publisher, which prepares both rank outcomes before assigning them.
Client field assignment follows successful rank publication.
Only field stores, the sixteen-field ApplyPrepared copy into the captured physical
effect owner, and the genuine guild-member total write follow rank commitment.
The physical PerfectionStatus object is retained. Foreign-server rebuilds skip
rank publication and do not consult the rank graph or rank gate. Classes 10000..10002
retain world-rank publication without class-rank publication.

Public PerfectionEffect.Update also prepares before copying. Its existing threshold
and real Rune lookup calculation remains intact in CalculateUpdate. Missing optional
named Rune metadata still grants no bonus; no accepting provider is substituted.

## Source-shaped calculations retained

The duplicated equipment contribution and both wardrobe slots using Items[1] are
observations from the old source, not independently confirmed client balance rules.
This safety change retains them pending specific client/data evidence; it does not
certify those formulas or declare every source quirk correct.

| Slots | Calculation |
| --- | --- |
| 0 | JiangHu's existing seven bands, integer strength/100 before the multiplier. |
| 1 | Actual Chi score times 30, plus times 10 when rank < 50; zero/negative ranks retain that rank rule. |
| 2 | 100 per unlocked real InnerPower stage. |
| 3 and total | Equipment ItemPoints is added in both source loops; aliases are not deduplicated. The second loop adds again after the initial total sum. |
| 4,6,7 | Existing level multipliers; combined base stats times 5; reborn times 1000. |
| 15,18 | SubClass level times 80 plus phrase times 20; nobility rank times 1000. |
| 16,17 | Both use wardrobe Items[1], with 50 points at each exceeded threshold 0/15/30/45/60. |
| 20..22 | The actual red, blue and yellow Rune properties. |
| 37 | Activated beast base 300 plus the inclusive 0..Level loop: 200 below 50, 100 thereafter. |
| 38 | Real HundredWeapons total/50 plus equipped first-slot score minus its integer 2% reduction. Locked/ineligible owners do not consult unused metadata. |

The point array keeps its actual length. At least 39 slots are required because the
highest source index is 38; the default remains 43 and valid extra slots remain zero.
This API is distinct from the separate fixed 43-component catalog projection.

Checked derived-score arithmetic refuses overflow instead of publishing wrapped
scores. HundredWeapons retains signed Int32 coefficient operations, long total
accumulation and uint result/bonus widths; signed coefficients are allowed when
the exact result remains nonnegative and representable. Unlocked Chi requires a
declared non-None attribute and actual ChiMinValues<=Value<=ChiMaxValues, then finite,
nonnegative contributions and representable sums/multipliers. Null Chi and locked
powers remain zero before attribute/bound lookup. No new balance cap is guessed.
Client PrestigeLevel must fit the real int effect API; Entry.Level must fit its byte
field. These are representation bounds. Standalone signed-int effect thresholds
and unrelated old unchecked HundredWeapons getters remain separate APIs.

## Limits

This is preparation and rejection/output safety within the managed owner model.
It establishes no authenticated runtime adoption, complete gameplay authority,
whole concurrent snapshot, lock-free reader atomicity, crash/database transaction,
restart recovery, deployment, rendered client behavior, or full client parity.
Public mutable inputs do not all share the rank gate. Low-score same-UID stale rank
retention and other ranking APIs remain separate. Exact-source full checks and
live acceptance remain separate evidence; no running server/client was changed.
