# D13-SPELL-WRITERS-1: Current-row soul, XP and spell-file coordination

## Identity and scope

Revision 3, 2026-10-27; Root-approved extension of D13-NINJA-LOAD-1. Target is
VisibleDx8D13. Evidence is current managed/reference source and retained synthetic
wire/file fixtures, not new client traffic. Scope is HandlerSpell, CheckUpdate
and spell-file Load/Save after SpellStorage gains copy-on-write publication.
No dispatcher, gameplay cost, magic balance, profile or host adoption changes.

## Packet contract

Existing opcode2101 retains its complete21-byte payload and optional captured
seal. HandlerSpell echoes the incoming fields with Type3 when the current
stored SoulLevel permits the incoming UseSpellSoul; the current row receives
UseSoul only. CheckUpdate retains first proficiency pass, optional level-up
message, spell frame, then the independent physical proficiency pass. Existing
strict-greater XP threshold, checked Int32 credit, previous-level rule and raw
wire widths remain. Prepared frame/storage helpers supply the existing bounds.
Unknown packet meanings and actual D13 live receipt remain unproven.

## Catalog/table contract

Spell files use one little-endian Int32 count followed by complete physical
12-byte DBSpell records: ID0/u16, Level2/u16, Experience4/i32, previous8/u8,
soul9/u8, selected soul10/u8 and padding11. Count is0..65536 as an explicit
defensive ingestion bound derived from the UInt16 keyspace, not a client-confirmed
cardinality fact; the legacy grammar can contain redundant rows. Actual length
must equal4+12*count; every existing
ReadCount/ReadSpell bool must succeed. No inherited short-read fields/default
spell0, extra tail, negative count or header-sized allocation is accepted.

An ID absent from the actual Magic catalog remains optional opaque learned
membership. For a known ID its level dictionary and exact selected row are
required, regardless of Count. A rune row requires actual coherent reverse and
permission metadata: matching declared item ID and exact RuneSpellID. Missing,
null, conflicting or fallback-only metadata refuses the complete load. Existing
job policy determines legitimate rune omission; no class eligibility is invented.

## Validation and transaction rules

All direct writers reject entry while the actual current SpellGate or genuine
current Ninja preparation gate is already held. Perform this check before actor
locks and repeat it after acquiring client/player gates. Acquire client then
captured physical player gates before SpellGate and
before resolving the current ClientSpells map/row. Reuse complete raw row capture
and plain final comparison from SpellStorage; original comparer lookups precede
the last callback-free owner/map/row fence.

For XP training that actually consumes a current Magic row, the selected level
key must resolve to a row whose declared ID equals the selected spell ID and
whose declared Level equals the captured original spell level. MagicType.Load
reads ID from array[1] and Level from array[8], storing under those declared keys.
Missing/null or mismatched consumed rows retain the existing fail-soft return
before either proficiency pass. The final metadata fence repeats both identity
checks after callouts and before XP commit. A callback may already have credited
proficiency; that earlier genuine effect is not rolled back or relabeled as a
whole transaction. Stop remaining work when the required identity changes.

The existing Count-1 cap return remains before current-row lookup; an unused
capped row is not newly validated. No next-level identity rule, sparse-key cap
repair, previous-level policy or new table default is introduced.

Training requires the current equipment's genuine constructor owner. A nonzero
weapon proficiency pass also requires its real constructor/current MyProfs
binding; absent or foreign unused proficiency storage is not required. Recheck
these physical inputs after callouts, without changing proficiency formulas.

Soul preparation bounds the actual readable input, privately decodes its fixed
fields, prepares the exact response and validates real output storage before a
plain current-row UseSoul store. Valid missing/ineligible requests retain normal
input consumption; required-state/preparation failures preserve packet output.
No redundant fallible indexer write follows the row store. Send is outside the
SpellGate, with postcommit current-owner/storage checks.

XP resolves the current row under the gate, prepares progress/frame outside it,
and validates before proficiency callouts. It reacquires gates and
revalidates the actual map/row/progress and metadata after those callouts, before
plain XP/level stores. It never writes an escaped pre-COW row. No proficiency,
frame serialization, file parsing or send callout runs inside SpellGate. Required
metadata/original-map/candidate comparer work runs under the actual gate so
typed reentry cannot commit/send before an outer failure. Prior
proficiency side effects cannot be rolled back by this bounded spell repair;
a full proficiency/XP transaction remains separate work.

Load parses/validates and closes the complete real file before entering SpellGate.
Under that gate it captures the actual current map, validates required metadata
and prepares a complete candidate. Existing
entries win, and duplicate file IDs retain the first permitted record. Final
owner/map/row/metadata comparison precedes one map-reference publication. Rejected
loads leave the owned graph unchanged and close the actual file, preserving
primary plus cleanup errors. Public Load retains its fail-soft diagnostic wrapper;
opened-file Load propagates InvalidDataException for malformed bytes and
InvalidOperationException for incoherent owner/required metadata. Failed Open
remains a no-op. Null public Load is caught before path access.

Save captures and validates one current map and all row values before public
FileMode.Create. Null client uses ArgumentNullException; invalid owner/graph uses
InvalidOperationException. Opened-file Save captures before its first count write
and closes on every outcome; its caller may already have truncated the file.
False typed writes propagate IOException. No crash-safe/per-file atomic save or
whole login/file transaction is claimed.

## Expected wire behavior and fixtures

Retain non-null spell/User byte goldens, valid soul echo and training order,
normal saved rows, duplicate-first and coherent rune allow/deny. Replace short
header/truncated-row invention, metadata Count bypass, wrong-row rune authority,
key/value-ID mismatch and leaked-handle/prefix acceptances. Add existing-API COW
reentry/current-row controls; tiny unsafe old-buffer cases stay excluded.

## Evidence alignment

Observed: existing typed serializers, DBSpell layout/read bools and current
callers. Typed source callers of CheckUpdate are AddMana, BreathFocus and two
CallPet branches. The source tree has no external managed callers of HandlerSpell
or these Load/Save APIs; opened-file helpers serve the public wrappers and tests.
Derived correction: complete-load/COW admission and final row fences.
Unresolved: full proficiency resource transaction, file replacement durability,
arbitrary raw mutation/disposal, runtime adoption and client delivery.

The XP eligibility expression `Level < metadata.Count - 1` is an exact retained
progression gap. The Magic loader inserts declared level keys and does not prove
zero-based contiguity; a sparse table such as keys5/6 can wrongly treat level5 as
capped. No current target table/traffic proof establishes that assumption. This
batch does not repair the progression rule or turn it into an acceptance test.
The complete file loader's exact-level admission is a separate corrected rule.

## Implementation and qualification

Revision3 composes only DirectPreparation/DirectCommit overrides from
helper-spell-training-metadata-next over that frozen writer proposal. Its
existing-API tests cover consumed identity refusal, real proficiency lookup
identity changes, unchanged capped timing and independent spell/proficiency bytes.

Source is isolated in helper-ninja-loading-writers-next. Shared frame helpers
and coordinated SpellStorage come from the separate reviewed loading lanes.
Root owns Load/Save test adaptations and all actual verification. No build/test,
native analysis, live action, main/Git edit or original-data access occurred.
