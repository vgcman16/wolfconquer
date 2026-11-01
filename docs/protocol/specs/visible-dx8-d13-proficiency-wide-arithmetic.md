# VisibleDx8D13 proficiency arithmetic before thresholds

Revision1, 2026-11-01. OPEN FSB-268 follow-on. SOURCE ONLY, UNBUILT/UNRUN.
Apply after journal24 rebase56 and first-use25; do not replace their real saved
origins, native/managed publication joins, closed purpose, XP/EOF or wire proofs.

## Confirmed source mismatch

The actual `LegacyNativeProficiencyStorage.ProgressProjection.ApplyCheck` uses
UInt128 for scaled gain, prior experience and violet bonus before subtracting
actual thresholds. Existing genuine native controls already pin widened
multiply, addition and violet cases. Typed `LegacyProficiencyPacket.ApplyExperience`
instead saturated both scaled gain and accumulated experience to uint before
the threshold loop. Removing the level0 stall alone therefore still discarded
legitimate gain: rated uintmax at level0 incorrectly stopped at level18.

Use UInt128 for the complete internal calculation: scale by100 below level5,
divide by100 above13, divide by level+1, add the integer violet percentage of
that scaled gain, then add prior experience. Retain the existing finite level20
loop, zero-threshold promotion, previous-level jump validation and max-level
experience reset. Narrow only the final post-threshold saved/wire remainder,
explicitly refusing an unrepresentable remainder instead of wrapping.

All uint input combinations fit UInt128, including the typed percent domain.
The existing public uint AppliedExperience remains a bounded diagnostic summary;
its saturation must never feed the actual progression calculation. No public
field or wire width changes. The source basic damage-to-rate clamp stays uint
and unchanged; a new row's first-hit level0/XP0 rule also stays unchanged.

## Independent aligned boundaries

| Starting level/XP | Raw input | Violet | Result level/XP | Current wire threshold |
| --- | --- | --- | --- | --- |
| 0 /0 | uintmax | 0 | 20 /0 | 3,200,000,000 |
| 4 /123 | 42,949,673 | 0 | 14 /239,503,583 | 320,625,000 |
| 5 /(uintmax-3) | 60 | 0 | 18 /1,071,039,177 | 1,623,164,062 |
| 5 /4,261,412,963 | 201,326,598 | 128 | 18 /1,113,988,945 | 1,623,164,062 |
| 4 /0 | uintmax | ushortmax | 20 /0 | 3,200,000,000 |

The new component theory runs the actual native constructor-owned progression
and mutable gem input and the typed helper against literal boundary results,
then compares their independent2041 frame goldens. It preserves the native
component's existing stale-needed saved-field991 behavior separately from the
typed helper's existing current-needed snapshot contract. No saved/native owner
or successful callback is invented to obtain a result.

The old `RateSaturatesAndExistingLevelZeroUsesExistingFormula` test enforced a
stall. Its replacement keeps the rate clamp and opaque1/2/3 bytes, but requires
the real widened level20/XP0 result and exact saved/wire images. A separate typed
max-percent control proves the bounded diagnostic does not clip actual gain.
Existing first-use next-hit level8/XP13,440,800, ordinary level5/XP50,100, native
message ordering, malformed previous-level and cap controls remain unchanged.

Packet2041 still carries four UInt32 fields at payload offsets0/4/8/12. Native
2199 control frames and all unrelated behavior retain their actual owners. This
source correction does not grant a hit, choose a weapon, persist a file, prove
client integration or qualify journal recovery. Root must compile/run the typed,
actual native component, journal24 and genuine first-use tests plus affected/full
gates before a fixed claim. No live/host, SQL, power-loss or parity claim follows.
