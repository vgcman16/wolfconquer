# Owned interaction movement journal21

Revision2,2026-10-31. Source/API contract precedes implementation. Target remains
VisibleDx8D13/selected7899; no new packet/profile or live-client claim.

## Packet and operation contract

Existing interaction walk/jump validation and successful wire order remain.
Journal21 records exactly two complete Character before/after images, sorted by
positive distinct ACCOUNT FileId. Parsed nonzero UInt32 Character UIDs are distinct
and need not equal account IDs. No Items/Spells overlay, UID birth or grant occurs.
Root JSON adds only PairMovement to Version/TransactionId/Changes. PairMovement
has exactly twelve Pascal-case fields: Kind(1Walk/2Jump), ActorAccountId,
ActorUid, PartnerAccountId, PartnerUid, MapId, DynamicId, FromX, FromY, ToX, ToY,
DirectionOrFacing. Account IDs are positive Int64; UIDs/map/dynamic are UInt32;
coordinates/facing are UInt16. Constructor/decoder rejects malformed shapes.

Walk direction is normalized0..7 with actual current offsets0(0,+1),1(-1,+1),
2(-1,0),3(-1,-1),4(0,-1),5(+1,-1),6(+1,0),7(+1,+1). Only non-Steed actual pair
walk can issue this operation. Jump requires each axis delta<17 and destination
not0/0, matching the existing jump admission. Facing stays the captured UInt16.

## Full-image projection and validation

Parse the complete Character through the existing UTF8/optional-BOM or legacy1252
encoding API, require exact byte roundtrip, and retain all unselected bytes.
Require one Character section and no duplicate case-insensitive keys there;
strict UID/map/X/Y agree with the recorded participant and common source cell/map.
The ordinary scalar writer does not persist DynamicID. If present, this legacy
key must contain a valid UInt32, but it may be stale or absent and is preserved
byte-exact. It is not current instance authority. Both exact afterimages replace
only X/Y numeric value spans; no scalar/default normalization,
Action/Angle/status/vigor write or other raw key/encoding/order change occurs.

## Actual authority and durable recovery

The immutable DTO/projection grants no live authority. Only the actual Frame/
Store prepared pair writer can supply its private JournalOwner, captured full
images, exact bindings, reciprocal interaction and ordered current participants.
Both actual current Frame/Store/native map and instance contexts must match the
operation; raw saved DynamicID cannot establish that proof. The real native
position/map/instance participant is required rather than comparing native zeros.
TryCommitPairMovement repeats concrete RequireJournalTargets/IsCurrent at write
boundaries; it accepts no saver/validator callback or accepting bool.

Use the existing genuine flushed pending/stage/replacement engine. Generic
recovery refuses21; owned startup uses the configured roots/domain and same
exclusive lease, exact record/stage/current-before-or-after proof, complete raw
scan and known history joins before readiness. Recovery completes both images
without output, runtime Action/link/query/repeat replay or UID changes. This is
restart recovery; no unverified power-loss/cross-process guarantee is asserted.
Recovery retains the recorded instance intent but does not adopt an instance.
There is no independent saved anchor for detecting a valid DynamicId-only change
inside an otherwise valid journal record; do not claim that corruption control.
Malformed fields, widths, identities, geometry, stages and current saved targets
must still be rejected through their actual independent checks.

Both native/holder participants prepare before the real joint journal commit.
Distinct typed CharacterPublication consumes only that same committed writer's
exact image; generic single-character commit cannot use it or save a second time.
Uncertain commit fences both participants. Normal direction/action and pair
asymmetry remain source-derived, not inferred from a journal alone.

## Qualification

Required genuine Frame handshake/walk/jump, complete before-based raw oracles,
prefix0/1/2 and stage-flush interruptions, current/stage corruption refusal,
configured startup and second stable restart, actual native/Store publication,
zero UID births and no output replay. Source is UNBUILT/UNRUN until Root's actual
gate completes; current full Windows41174375894 owns the host. Existing runtime
holds/originals/restore point remain unchanged. Full Action/auto-attack/native/
SQL/7937/English/live horizons stay ACTIVE.
