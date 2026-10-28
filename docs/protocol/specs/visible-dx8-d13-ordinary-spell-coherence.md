# VisibleDx8D13 ordinary spell and proficiency coherence

## Identity and scope

Revision 3, 2026-10-28. This replaces the earlier read/mirror-only proposal.
The target remains the actual VisibleDx8D13 ordinary managed session and its
constructor-owned native client/Player, Spells and Proficiencies. The map lane
postimage is source only and has not been compiled, tested, integrated, activated
or published by its author. Root owns qualification and the actual Store join.

The implementation joins native spell Add/Remove/Reborn/Clear/Update, selected
soul, XP, Ninja Loading/Alternate, rooted spell load/save and SendAll to the
Store-issued binding. Native proficiency Add/CheckUpdate/SendAll use the same
real owner and gates. This does not implement SQL, all casting/progression rules,
full native SaveClient, or proficiency file durability.

## Packet contract

No new opcode/schema is invented. Retain existing successful ordering and framing.

| Output | Existing native authority | Prepared behavior |
| --- | --- | --- |
| Spell2101 | LegacyNativeSpellPacket.PrepareSpellFrame | Full21-byte payload; source soul/hash fields |
| Removal action109 | LegacyNativeActionPacket | Actual player UID and removed ID |
| Proficiency2041 | Native ProficiencyCreate | ID, level, XP, actual threshold |
| Proficiency XP2199 | Native UpdateProfExperienceCreate | XP, actual threshold, ID, eight zero bytes |
| System2179 | Native MsgMessage.GetArray | Actual protobuf serializer, SYSTEM/ALL, red, TopLeftSystem |
| Ninja2393/2395/badge | Existing Ninja sequence codecs | Preserve the source sequence after complete preparation |

Frames are fully owned byte arrays before commitment. The progression system
message uses a genuine recycled native packet only during preparation, copies the
complete frame, and releases that rental before any send. It does not hand a
pooled pointer to an outstanding transport task.

The spell-associated proficiency pass precedes the spell-level message/spell
frame; a physical attack's proficiency pass follows it. Each pass handles right
then left weapon. First-call proficiency initialization still creates level0/XP0;
actual later training uses the real threshold table and violet-gem input.
Widened arithmetic prevents the native unsigned award wrap. No absent gem owner
is queried for Add, zero awards, missing-row initialization or level20 no-op.

## Catalog and persisted layouts

Saved Spells are complete12-byte little-endian rows: ID0/UInt16, Level2/UInt16,
XP4/Int32, Previous8/byte, SoulLevel9/byte, selected soul10/byte and opaque byte11.
IDs must be positive/unique, XP nonnegative, and selected soul must roundtrip0..4
through the genuine native setter. SoulLevel retains its byte storage domain.

Saved Proficiencies are the actual20-byte rows: ID0/UInt32, Level4/UInt32,
Previous8/byte, opaque9..11, XP12/UInt32, Needed16/UInt32. The in-memory participant
preserves the five modeled native fields; it does not claim to save opaque bytes
or invent a profile file route.

Existing native spell rows also contain cooldown/packet fields, Soul,
HashLevelSoul and StampSpell. Equal persisted rows retain physical identity.
Changed rows copy all actual transients. SoulLevel uses the real setter's OR
behavior; changing only XP/level/selection preserves the existing hash mask.
New rows use the real native row type. Unknown saved IDs remain opaque membership,
never invented cast permission. Actual Magic/rune metadata controls file filtering.

## Ownership and preparation

Only genuine ordinary admission may issue the binding. Its exact Store holder,
connection/native identity, physical client/Player/UID, real Spells/Proficiencies,
configured Recovery and canonical roots remain pinned. The client keeps the
binding privately after retirement, even if MySpells becomes null or is replaced;
the same client cannot regain unbound mutation or ordinary admission by that edit.
A new physical client follows normal admission. No UID-only authority, fake owner,
native Socket acceptance route or test-only comparer injection is introduced.

Ordinary admission constructs default-comparer storages. There is no genuine
pre-admission custom-comparer adoption seam. Detached/unbound map preparation
preserves and validates the actual existing comparer; tests must not install a
new comparer after admission and call it the original ordinary map.

All joined writers take Store/package/connection authority before client, Player,
Spell, then Proficiency gates. Each Store publication prepares and freezes both
snapshots before any durable writer. Native reverse writers also prepare/prove
the unchanged counterpart, so profile changes cannot drift behind spell writes.
Closed typed immutable intents carry Add/Remove/Reborn/Clear, Soul, Progress,
loaded values and Ninja choices; no object bag or arbitrary accepting callback.

Private map/key/row-identity anchors reject raw dictionary replacement between
operations. Complete witnesses additionally compare every captured scalar during
an operation. For original AND candidate dictionaries, enumerators are allocated
before callbacks and first advanced afterward. The prewriter and postwriter
witnesses are distinct one-use proofs. Raw same-count key/row replacement and
changes visible after resize are checked without comparer lookup or allocation.
Candidate coverage includes genuinely exposed Ninja.Added native rows. A final
proof never accepts count/generation alone. This is cooperating-owner coherence,
not a universal snapshot claim against arbitrary concurrent field writes.

Ninja prepares spells, ranks, frames and storage before rank/Character badge/map
stores. All fallible checks finish before the first store. Compound XP prepares
real proficiency and spell projections and every frame before either map/holder
changes. It does not invoke a fallible proficiency mutation after a spell commit.

## Rooted file authority and recovery

A genuine binding has a last-proven saved Spells image independent of current
memory. Native Add/XP/Ninja may legitimately be unsaved. Rooted Load/Save consume
LegacyPreparedSpellFileWrite captured from the actual Store lease, configured
Recovery and pinned roots. They must match the exact saved-image anchor rather
than rebasing to arbitrary newer disk bytes or requiring unsaved memory to equal
disk. Public Load holds the actual file lock through safe image revalidation,
metadata/map preparation, final raw proof and plain publication.

Native Save prepares the final Spells image, preserving surviving row byte11,
and a genuine unchanged Character identity participant read from the actual
AccountId file. The real Character+Spells v8 journal commits both under its actual
domain. The saved anchor advances only after that real writer succeeds. A failed
postwriter proof blocks the actual Recovery and retires the attached binding;
it is committed recovery work, never unchanged rejection or unbound fallback.

Arbitrary opened spell helpers cannot prove their source belongs to the pinned
root. They close their owned handle and explicitly refuse a bound owner.
Unbound opened compatibility remains separate. Native bound proficiency file
Load/Save explicitly refuse before I/O: no configured profile saved-image domain
exists in this batch. Opened refusals close their owned file. Source audit finds
no actual SaveClient aggregator/caller of SaveClientProfs in the current core;
a future aggregate must preflight all participants before any file write.
Legacy unbound profile-file parsing/durability is not qualified by this join.

## Notification lifecycle

Joined output calls the actual ConquerTcpConnection.SendAsync outside all Store,
client/Player/Spell/Proficiency mutation gates. Ordinary binding requires actual
send and disconnect routes. One total ten-second batch deadline bounds writes;
each receives its remaining-deadline cancellation token and an independent
bounded observer. Failure invokes a real disconnect with a bounded five-second
observer and a bounded five-second write drain. A route ignoring cancellation
cannot hold the caller indefinitely after returning its asynchronous task.

Outstanding write/close tasks remain observed until terminal completion. Their
owned arrays/token lifetimes are retained; no pooled storage is released early.
Skipped remaining transport after retirement or graph change is an explicit
committed notification exception. Completed SendAsync is transport completion,
not proof the client rendered or received the change. The bridge preserves each
batch's source order; it is not a global interleaving queue or scalable async
pipeline, and it does not bound an arbitrary delegate blocking synchronously
before returning its ValueTask.

## Qualification and limits

Required controls use actual ordinary admission, genuine loaded Spells and
Proficiencies, real Store APIs/native entrypoints, and the actual connection
capture route. Cover malformed/foreign owners, retirement/recreation, null and
replaced slots, mutable snapshots, same-count raw row/key changes and resize,
exposed candidate mutations, inverse gates/comparer reentry, complete frame
preparation failures, compound XP, rooted load/save and real journal failure.
Run source-independent packet goldens and positive success controls.

The map author ran no builds/tests, changed no main/public/Git/live files, and
claims no passing count. Root must reconcile this postimage with the Store,
test and v9 book/rune lanes, then run focused and full appropriate gates.

Known limits remain explicit: proficiency file/SQL durability, full native
aggregate saving, count-minus-one/contiguous spell-level metadata assumptions,
the wider cast transaction, raw public fields/uncooperative external writers,
global cross-batch notification ordering, client rendering and gameplay parity.
Those are not completed by an in-memory join or static review.
