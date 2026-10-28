# HP saved mirror and physical Rune unit encoding

Revision1, 2026-10-28, isolated exact4d928 CI repair. This is source-only;
Root's candidate has the prior56-file producer cut, with no v10 APIs in this addendum.

## Evidence and target

Current VisibleDx8D13/native source profile; existing managed Frame fixtures retain
RootD11_7899 exactly. No packet layout/opcode changes or new client-binary claims.
Diagnostic01 actual first book log: loaded HP61258, action HP10836, then real
itemSpellCharacterBaselineStale. Source EquipmentQuery assigns the typed HP value
without mirroring Attributes; Frame scalar save writes BuildPersistedValues.
The disk can therefore contain10836/new scalar fields while current Attributes
still holds61258/misses newly written defaults. Strict saved/current validation
is correct; the producer must publish a coherent typed+saved-field projection.

On a real HP scalar CHANGE, clone current attributes, apply the exact concrete
CharacterSnapshotScalarPersistence.BuildPersistedValues map and publish that
same changed Character. Preserve all other/opaque entries. No global normalization,
no standalone no-change write, no weakening of raw/typed baseline comparisons.
The actual Frame callback remains the save consumer, with its existing failure
reporting. This fixes the mirror gap, not universal HP transaction/rollback.

Rune diagnostic01 fails even at HP10000<=max11328. Both new v9 Rune projections
incorrectly reject StackSize0. Actual native ContainItemWithStack accepts raw0 for
Count1; managed Rune compose/decompose likewise treat raw0 as one. A single
physical Rune slot accepts raw0 or1, retains the raw field byte-for-byte, and
rejects>1 until a separate split transaction exists. Keep exact UID membership,
locks, deposited-child, class/level/family/slot and real v9 ownership checks.
Do not generalize this rule to other inventory systems.

## Prepared behavior and controls

Existing Rune independent byte/wire/current-owner/rank assertions remain. Their
old handled log label changes to the actual committed label. Two new actual Frame
cases observe current/saved/attribute agreement BEFORE an HP sender, preserve an
opaque Character sentinel and retained spell row byte11, prove legitimate book
consumption, and reject a later external saved Money drift without overwrite or
success. Three Rune boundary cases exercise raw0/raw1 success and raw2 refusal,
including unchanged raw StackSize, Character bytes and exact rejected holder.

No test was executed here. Root serializes integration/build/focused/full checks.
No live client, SQL, multi-process/power-loss, complete initial normalization or
all scalar mutation safety claim. Native ordinary v7 admission is unchanged.
