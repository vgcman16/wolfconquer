# Prestige class resolution and reborn cleanup

Prestige uses a dedicated resolver for the eleven existing leaderboard families.
The VisibleDx8D13 primary profession table contains 634 supported IDs: families
1/2/4/5/6/7/8/9/16 cover suffixes 000–057; Water 13 and Fire 14 cover 002–057.
Stages 52–57 therefore retain their actual family instead of falling through to
Water. No new prestige category is inferred from other job metadata. The numeric fixture
uses the current staged primary with edited display values; its known IDs and
non-display asset columns also match the newer Chinese7930 intersection, which
does not extend this target's boundary.

`TryGetIndex` returns the known category, or false with `Type.Count`.
`GetIndex` throws `ArgumentOutOfRangeException` when no class category exists.
The 17 observed unmodeled IDs—11002–11005, 12002–12005, 15002–15005 and
19000–19004—remain unsupported, as do arbitrary values such as zero or
`uint.MaxValue`. Other table variants do not extend this boundary beyond 57.

Classes 10000 and 10001 remain World-only. The existing literal exclusion 10002
is also retained for compatibility; it is absent from the examined target
primary table and is not a newly confirmed client job. These entries explicitly
carry `Type.World`, with no class publication. Unsupported rebuild inputs refuse
before client points, perfection fields, guild totals or ranks change. Publisher
admission checks Class/category/includeClass agreement. Both spawn badge paths
use nonthrowing resolution: World-only or unsupported classes receive no class
badge rather than borrowing a Water or World rank.

## Why historical cleanup must preserve the current family

The plain reference records FirstClass and assigns the new class/reborn state
in `RebornInfomations.cs:152–154`; the next rebirth uses SecoundeClass at
195–197. Its equipment refresh at 447 follows
`QueryEquipment → OnDequeue → UpdateStats → CreatePrestigePoints`
(`Equip.cs:2376`, 2181, 2271 and 1549). `CheckReborn` then runs at
`RebornInfomations.cs:448`. An eligible rebuild can therefore publish the current
family immediately before unconditional historical cleanup deletes that fresh
entry. Same-family deletion is a source bug.

Cleanup requires the client's constructor-bound physical Player. Genuine
non-reborn inputs return before unrelated UID/rank checks. Active reborn inputs
require a positive full UInt32 UID and select FirstClass for reborn 1 or
SecoundeClass for reborn 2. A World-only prior class has no class bucket to
remove and returns before graph access. Unsupported prior/current classes
refuse. A known current family matching the known prior family remains intact;
only an obsolete prior-family bucket is cleaned.

Under the shared gate, cleanup admits the actual selected owner, matching
category, positive cap and complete rows, including capped tails. Raw UID keys
must be positive, unique and match their rows. Physical Rank aliases refuse;
distinct owners sharing Items remain valid. Removal prepares a replacement map
and next/null champion before stores, preserving escaped maps and entry arrays.
A targeted cached-only champion is refreshed; genuine absence does not heal
unrelated caches. Stable ties, thresholds, caps and no-backfill behavior remain.

## Qualification and limits

The tests-only baseline had 290 failures and 553 passes across 843 cases, with
zero skips. Corrected focused qualification passed 932/932, including every
baseline case. The combined private selection passed959/959, including the raw
D13 codec's27 cases. Public raw-codec qualification separately passed27/27.
This is managed source evidence, not live-client or deployment proof.

Global `Atributes` predicates, Load/Save grammar and stored-category admission
are unchanged. Historical saved rows are not reclassified, and the immutable
catalog is not dynamically refreshed. No managed production rebirth caller is
added. The whole equipment/rebirth transaction, arbitrary raw-writer concurrency,
crash recovery and exact-source full-CI qualification remain separate work.
