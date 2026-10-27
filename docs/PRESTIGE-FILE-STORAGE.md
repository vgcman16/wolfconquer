# Managed prestige file storage

The final Save successor passed765/765 public-derived and770/770 private cases,
zero failures/skips. The loader/publication/numeric batch passed1,295 cases with129
existing exclusions and 698/698 private cases. All 576 baseline cases now pass;
the old implementation failed177/passed399. Full Windows40456938513 passed on
prior source adcae028 and does not qualify this later batch. No live deployment
or complete gameplay/parity claim follows.

## File load

The literal path remains DbLocation + "PrestigeRanking.txt". The header is Count=
followed by a nonnegative Int32 decimal. Exactly that many physical rows must
follow; early EOF and extra lines, including blank lines, reject the whole file.

Rows have 22 fixed slash-separated fields, exactly their declared number of uint
point components, and a final slash. Field order remains UID/name/category/class/
level/mesh/total, appearance fields7..19, guild20, component count21, then points.
Positive UIDs use the full UInt32 domain; category is1..11 and level fits a byte.
Numeric fields require ASCII digits and valid widths. Names must be nonblank;
name/guild text rejects controls and malformed Unicode. Empty guild is valid.

Strict UTF8 with or without BOM and BOM-marked UTF16LE/BE or UTF32LE/BE are accepted.
Variable point counts remain valid. The separate immutable catalog projection
requires43 components and positive Int32-domain catalog IDs; it is not reused here.

Load holds SynRoot through read, validation, disposal and private preparation.
Only complete success assigns a fresh graph with twelve standard buckets.
Threshold20,000, world100/class30 and descending totals remain; saved row order
breaks ties deterministically. Class/world buckets share the newly parsed entries.
Valid Count=0 publishes a fresh empty graph. Escaped previous owners stay unchanged.

Missing files and actual I/O/access failures propagate their real exceptions.
Malformed grammar/data or decoding throws FormatException with path/read context.
Buffered decoding diagnostics do not pretend to identify an exact corrupt-byte line.
Failure preserves prior graph, caches, champions, rows, points and source bytes.

## Rank publication and remaining work

The current CreatePrestigePoints refresh admits every actual present owner and
complete UID map, prepares World/current-class updates and removes obsolete
memberships before reference stores. Below20,000 it removes retained same-UID
memberships. Excluded classes10000..10002 remain World-only. Detached/global
rank mutations use the same preparation rules; see [rank refresh](PRESTIGE-RANK-REFRESH.md).

Client-field preparation is covered by FSB-135 and the
[calculation contract](PRESTIGE-CLIENT-CALCULATION.md); refresh/mutations are
FSB-137..138. Scoped CheckReborn cleanup has a later qualified successor;
remaining query/diagnostic behavior and complete leaderboard backfill stay open.
Save now validates/serializes current class rows completely
before creating a same-directory exclusive flushed stage. Existing files use
Replace with an owned backup; missing destinations use create-only Move. Valid
sparse graphs, fulluint, variable point arrays, UTF8 without BOM and the exact
saved layout are retained. Malformed state rejects before destination mutation.
Complete outcomes are checked; an uncertain operation retains remaining sidecars
and fences that path in-process. Cleanup cannot turn a verified commitment into
rejection or mask primary refusal.

Five filesystem controls cover real create/replace, missing parent, Windows sharing
and directory refusals with unchanged graph/old bytes and no ordinary residue.
Three real-file observer cases distinguish consumed from retained stages, including
identical images. The real Replace returns in these tests; the unacknowledged flag
selects observer state rather than reproducing a thrown filesystem call.

This is not a durable journal or automatic startup recovery. Consumed stages cannot
be retained; cleanup failures may leave sidecars. No directory flush, power-loss
durability, hostile namespace race or cross-process CAS proof is established.
The generic DbWrite for other save families is unchanged. No production caller of this managed Load
was found in the reviewed source. Runtime adoption, durable/crash recovery,
dynamic refresh, live rendering and full persistence remain separate work.

Later qualification: [class resolution and reborn cleanup](PRESTIGE-CLASS-AND-REBORN.md)
corrects supported stages52..57, removes default-Water authority and preserves a
fresh same-family row. FSB-139..140 cover that scoped successor; global class
rules, remaining queries, runtime adoption and full persistence remain open.
