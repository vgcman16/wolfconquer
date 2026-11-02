# Jump attribute-map preservation

## Identity and scope

Revision2, 2026-11-01. SOURCE ONLY / UNBUILT / UNRUN. Root-assigned correction on current598e2be1; VisibleDx8D13 action137 (opcode2046) ordinary Jump and cooperating movement cleanup. Preserve current packet layout, destination/facing fields, effects/resource order and actual movement transaction/retirement. No new public mutation API or gameplay owner. This scope depends on the separately frozen Intensify3 correction to `TryCancelLegacyIntensifyArm`; its Common.cs hunk is retained, not replaced by a new implementation.

## Packet and source contract

The existing successful generic Jump validates destination and less-than17 delta, applies source movement side effects, prepares Angle and full runtime Action100, conditionally charges Ride vigor, cancels armed focus/cleared movement flags, then publishes through actual single-owner or paired movement. Existing echo, visible peer, cancel, status and resource packets remain in the same order. This task changes attribute representation and scoped field validation only.

## Validation and publication

The earlier Jump dictionary clone uses OrdinalIgnoreCase over the whole map and rejects otherwise supported Ordinal `Opaque`/`opaque`. Use the existing comparer-preserving map helper and update only the unique existing Angle/Action spelling. The selected Ride branch must read/write the unique Vigor alias using the same narrow map rules and preserve existing UInt64 parse/default, UInt32 bound and source distance/cost calculation. Duplicate aliases of an operated key refuse before publication, debit, action/repeat mutation or output.

The actual OwnedMovementProjection also used globally normalized attribute proofs. Its map witnesses now use CaptureRaw while retaining full before/after currentness, scalar/Paid checks, exact Action validation and actual private handler-issued purpose. The Character publication comparison checks exact ordinal key/value membership independent of enumeration order; it does not require the same dictionary object or comparer instance. A plain-map proof is not authority and cannot substitute for the real Frame/store/native/file owner.

Away clearing uses the same comparer-preserving copy and unique alias operations. Its existing bool/UInt64 nonzero interpretations and no-op original Character reference remain unchanged. The private movement-status DATA helper resolves each StatusFlags/BitVector name uniquely, validates the existing bounded UInt64-vector representation, and requires both representations to agree semantically. Cleanup changes every present agreeing representation while retaining its key spelling and unrelated formatting; conflicting vectors are never normalized into agreement. It removes only the two applicable Auras MagicDefender lifetime/policy attributes through unique alias operations. Unchanged cleanup returns the original map reference.

Ordinary Jump reads Focused, MagicDefender, Ride and ActivePick through these scoped rules. Ordinary Walk's corresponding read-only notification predicates use the same parsed inputs so the shared cleanup and its output decisions agree. Global status/attribute readers remain unchanged. Jump's real Away refresh also uses unique Angle then fallback Facing lookup in the spawn codec, preserving the existing signed numeric/default/width semantics. The Jump reducer validates its actually written Angle before committing; ambiguous used facing sources are not silently selected by the codec.

If real removed-Focused or cancelled-Intensify state selects a movement cancellation packet, a private DATA preparation resolves the one conceptual FocusClientSpellId/ID key and the original Angle before the writer. It retains the original UID/coordinates/facing, previous UInt64-to-UInt32 ID and UInt16-facing/default rules, and output-time Environment.TickCount. The movement-only sender uses those retained values, so a used-key ambiguity cannot first throw after committed movement. It does not arm, cancel or publish anything; those actions remain owned by the existing real movement writer. Unrelated SendLegacyFocusedCancel callers and Neptune's separate route remain unchanged.

All changed helpers prepare DATA before the existing movement writer, cancellation publication, native/holder publication and successful outputs. Locks remain the real Frame lifecycle, package, connection and native/actor scopes followed by the actual captured Character writer. This work adds no transaction, no rollback or retry guarantee, and no fresh-request deduplication claim.

## Fixtures and limits

New actual admitted Frame controls require opaque case-distinct preservation, unique used aliases, native position/Action and full saved-file success, independent complete2046 bytes, and whole-state refusal for ambiguous operated input. Away is earned by real action161 before representation-only ordinary Store publication. Single-vector and both-agreeing zero-vector cases cover representation, not live flag retirement. Escaped negative cases reversibly introduce invalid DATA after a valid Store publication; separate actual Store refusals retain the earlier native/action/vector boundary instead of bypassing it.

The additional live-effect test uses the actual Auras11200 producer for movement-cleared flag128 and its expiry/remove-on-death metadata. AttachStatus30000 produces a different flag93 and cannot stand in for this prerequisite. The test must retain the accepted real cast, actual file/native/binding evidence and cancellation-before-movement output. A producer refusal remains a failing prerequisite, not permission to inject a status.

Paired Jump retains its separate whole-map OrdinalIgnoreCase projection and is OPEN. Pair callers benefit only from the shared DATA cleanup; no complete pair compatibility claim follows. Neptune's dead-position/status branches and Walk's selected Steed Vigor reader remain separate OPEN scopes. Genuine owned Ride earning/cost and other movement-cleared flag lifecycles remain unqualified by the zero-vector cases. Native movement publishes position/Action, not a new native Angle owner; the facing assertions concern the actual holder and wire. No compiler/test or host work occurs here, no live client was run, and broader StarFlow closure is outside this intake.

## Root integration qualification

The authored scope above was subsequently integrated into the movement candidate.
Release build04 passed; the selected owned-action controls, including the nine
initially failing movement cases, all pass in focused05. That receipt still has
five failures elsewhere; no full or live acceptance follows. The Auras producer
stores ten words; its independently corrected fixture expectation retains that
actual representation. Existing PairJump/Neptune/Steed limits above remain open.
