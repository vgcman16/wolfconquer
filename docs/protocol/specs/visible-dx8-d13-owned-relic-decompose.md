# D13 owned relic decomposition

## Identity and scope

Revision2026-11-02. Target: the registered `VisibleDx8D13` MsgItemUsage action67.
This replaces unsafe sequential mutation in the community-server reference with
one owned batch. Existing legacy codec/source goldens supply the packet contract;
this action has no new live-client acceptance claim. Chinese7937 Resolve/bGold
semantics and a new7937 runtime/profile are separate unresolved work.

## Packet contract

The request opcode is2137. Offsets below are payload-relative; the frame has its
usual4-byte header. The complete captured frame, header and payload must agree.

| Field | Offset | Width | Rule |
| --- | --- | --- | --- |
| Action | 12 | UInt16LE | 67 |
| Declared selected count | 18 | UInt32LE | 1..49 |
| Selected inventory UIDs | 87 | count*UInt32LE | Nonzero, distinct, ordered |

The fixed payload prefix is87bytes. Refuse a short prefix, missing declared UID,
partial trailing word, invalid count or inconsistent frame. Complete surplus
UInt32 words outside the declared prefix are ignored and omitted from the echo.
Other parsed base fields remain unchanged in that echo.

Successful legacy order for each selected UID is one2137 RemoveInventory/action3
frame followed by nine2116 AddItem/mode1 frames, then one final action67 echo for
the declared prefix. Each reward3306885 has stack1 and a distinct new UID. Output
count is10*N+1. Keep independent complete byte goldens, not writer-only roundtrips.

## Catalog/table contract

Capture real constructor-owned legacy ItemType metadata: selected relics4100001
through4100005 and material3306885, with parsed UInt16 durability. The current
legacy66-field rows are independent of optional compose/random/Echo metadata.
No caller metadata, permissive fallback or guessed current7937 field mapping
can replace that owner. The journal retains recorded scalar facts, not authority.

## Validation and transaction rules

- Require the real authenticated paid owner, secondary-password admission,
  registered connection, Frame/exclusive operation, native actors, Store holder,
  current saved files, recovery domain and shared UID registry. The actual Frame
  trade store must be held and contain no active trade or escrow claim.
- Capture the complete physical item graph before any reservation. Selected UIDs
  must be actual inventory relics, single units, unlocked, nonsuspicious and have
  no deposited children. Refuse invalid placement, duplicate identities, lossy
  saved text, unsupported nesting and bad unrelated tails. No UID-to-type fallback.
- Preserve the established legacy single-unit StackSize0or1 representation:
  actual native ContainItemWithStack(uid,1) accepts zero as one. Do not normalize
  the source before validation; new rewards use canonical StackSize1. A genuine
  registered control checks saved, managed and native zero before the action.
- Validate the full batch's post-removal40-slot capacity and persistable size.
  Nine separate reward units are required per input; a partial grant is refused.
- Preserve each input's valid0/1 Bound flag on its own nine rewards. This explicit
  no-laundering correction does not assign a meaning to7937 bGold.
- The real registry pins first/count while immutable output frames, complete
  save images and the actual native prepared publication are built. Advance one
  contiguous9*N reservation only after those preparations succeed.
- Commit character/items/spells through typed journal27 and retire the distinct
  registryv8 RelicDecompose namespace using the actual committed saved successor.
  Character/spell contents stay unchanged except canonical creation of an absent
  empty spell file where the existing saved-owner contract permits it.
- Publish native state, holder and any configured real rank cache before sealed
  output handoff. Missing configured rank ownership refuses before allocation;
  absence of the optional ranking system is not an unrelated prerequisite.

Rejection before allocation preserves state, resources, outputs and authorization.
Issued identities are never recycled. Uncertain allocation/commit/publication
fences the actual owners and recovery. Failure after commitment preserves the
durable outcome; it does not become a rejected grant, rollback or free retry.

## Expected wire behavior and fixtures

Only private committed Store evidence grants ordered registered output. Existing
Fuse/return outputs still require exactly two frames; decomposition derives its
variable count from its own private recipe. Each complete registered write must
settle before the next. Release the real Frame lease before socket waits and
retain holder/native/writer/connection successor checks on every write.

Use genuine owned fixtures for successful1/2/4-input and mixed-bound batches,
all invalid-state refusals, two-player B-first ranking observation and real trade
opening. Test gate-wait and sustained started-write cancel/deadline/disconnect,
independent authenticated progress, and cold reopen without UID reissue.

## Evidence alignment

| Requirement | Evidence | Current qualification |
| --- | --- | --- |
| Action67 prefix and remove/add/echo order | Existing legacy codec/reference and independent synthetic goldens | Source reviewed; new controls unrun |
| Whole physical/native/saved ownership | Actual Store/Frame/admission/file/registry owners | Composed source; unrun |
| Shared UID and recovery | v8 namespace, v27 journal, mixed producers and interruption controls | Composed source; unrun |
|7937 Resolve rewards/current action | Three recorded Resolve literals and opaque current data | Unknown; no activation |
| Actual client behavior | Matching live action67/7937 traffic and rendering | Missing |

## Implementation and qualification

Implementation is split across `LegacyRelicDecompose*`, actual Runtime/Frame,
prepared writer, Store/ranking/output and typed registry/journal partials. New
tests include `LegacyRelicDecomposeRequestTests`,
`LegacyRelicDecomposePreparationTests`, `LegacyOwnedRelicDecomposeTests` and real
mixed-producer/Rune-after-v8 controls. Old unsafe test oracles are migrated to
genuine owners; distinct refusals must not collapse into missing-owner checks.

This batch is UNBUILT/UNRUN. The prior fe3e full run is separate and remains
frozen. Root must qualify focused actual owners, shared callers and the full
publication gate. The injected BeforeRanking failure proves only that configured
window; concurrent rank-owner drift, universal deadlock safety, SQL, power loss,
English rendering and full gameplay parity remain unproven. See FSB-315.
