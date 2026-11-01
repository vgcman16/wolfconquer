# VisibleDx8D13: first-use Profs and Spells saved origins

Revision1, 2026-11-01. OPEN FSB-268 follow-on, SOURCE ONLY, UNBUILT/UNRUN.
This implements the standing journal24 missing-before requirement through real
ordinary admission; it does not introduce another hit, protocol or live owner.
The effective base is current Root proc/lifecycle source plus the frozen
`root-hit-proficiency-rebase-20261101` delta. Preserve their current managed
origins, actual receipts, closed basic/proc death purpose, native generations,
XP/EOF fences and independent damage/file/wire goldens.

## Evidence and legitimate empty state

The original `MrRayzo.Database/ServerDatabase.cs` under
`征服-龙拳天下服务端-已修复/lqtxfwd/DecompiledSource/GameServer` returns without
adding rows if PlayersProfs cannot be opened (LoadClientProfs, line1411 onward),
and returns if PlayersSpells cannot be opened (LoadClientSpells, line1451 onward).
The genuine client constructors already own empty maps; no later placeholder
owner is required. Current `PlayerProficiencySnapshotLoader` and
`PlayerSpellSnapshotLoader` likewise map an absent configured file to Empty.
Malformed/truncated/padded/duplicate images remain invalid; missing is not a
blanket substitute for unreadable or corrupt required state.

Ordinary admission already retains absent unchanged Spells as a read-only null
input. Required login grants still create their actual nonempty Spells image in
the admission transaction. Eagerly writing a four-byte empty Profs file was not
a required loader or client contract. An absent empty Profs book now remains an
explicit null admission input until a real progression writer creates it.

## Captured ownership contract

| Participant | Actual capture boundary | Before state | Allowed later advancement |
| --- | --- | --- | --- |
| Profs | Configured admission input images, exact real detached spell/proficiency admission owner | Immutable bytes, or null only for genuine absence matching the constructor-bound empty snapshot | Actual committed journal24 writer or actual Frame proficiency file postlude |
| Spells | Existing configured admission images and SavedSpells anchor | Immutable bytes, or genuine null if no required login grant changed it | Existing committed spell/death/query writer owners |
| File root | Admission's configured canonical root, retained on the real binding | Explicit root and bound player UID | No caller-selected root or account-equals-UID assumption |

`LegacySavedProficiencyImage` is immutable DATA. Its bytes do not authorize a
native mutation or transaction. Only the actual detached admission owner can
initialize that saved origin on its constructor-bound binding. The prepared
joint writer captures the same anchor pointer, requires its exact bytes against
the live configured file, proves the snapshot match, and rechecks that pointer
at its real journal boundaries. A foreign empty-file insertion, deletion of a
previously present empty file, or same-value saved-origin substitution cannot
become a newly discovered first-use owner.

All prepared after-image evidence is allocated before the real writer. The
actual committed journal24 receipt advances the binding anchor to its captured
after-image, not to freshly recaptured arbitrary disk bytes. The existing Frame
postlude for other cooperating proficiency saves now validates the old anchor
and current Frame/Store binding under the real file lock, atomically writes its
known current snapshot, verifies the known after-image, then advances the anchor.
Its failures escape to the existing committed-publication fence. This postlude
is not a newly claimed globally atomic native/file transaction.

## Journal and wire behavior

Journal24's existing closed schema permits nullable Before for binary Profs and
Spells. Target order remains Character(target account), optional death
Spells(target player UID), then Profs(actor player UID). No journal version or
packet field changes. Actual first-hit new proficiency retains its independently
pinned level0/XP0/needed0 row; an existing row retains the existing growth rules.
Neutral/no-weapon hits do not create an unrelated empty Profs file.

The next hit must progress that real level0 book. The actual native
Proficiency.CheckUpdate loop and existing native zero-threshold regression
promote level0 across its zero threshold without consuming experience. The
typed helper's zero-threshold early break was an implementation defect; remove
that break while retaining its finite level20 bound and existing saturation.
An actual saved previous-level jump beyond20 refuses, matching the genuine
native malformed-state guard. Isolated DATA controls pin raw1 -> level1/XP100,
and the real second basic hit's rated300,000 -> level8/XP13,440,800/needed22,000,000.
The existing first-hit new-row and level5/XP50,100 goldens remain unchanged.

The existing staged create/replacement and configured custody recovery validate
all images, complete historical recipe, distinct identities, saved Items, safe
roots, legal replacement prefixes and owned bounded stage files. Null and a
four-byte empty image are different physical before states. Recovery must not
adopt a foreign empty file as a valid absent prefix, even if both parse to an
empty logical book. Once an intent is published, finish the retained result;
do not roll back a real hit to absence. A pre-intent refusal/known write denial
leaves the original missing files, native state, authorization and output intact.

Client profile remains VisibleDx8D13. Existing successful Interaction2135,
HP/death2176 and proficiency2041 order and bytes are preserved. This source work
does not establish a newer client-build identity or new live acceptance.

## Genuine authored controls and remaining limits

The new owned-fixture tests declare missing inputs before Bootstrap/read/native
birth, then use actual configured runtime open, ordinary admission and Physical
packets. No bound file is deleted to manufacture a positive. They cover:

- First-use missing Profs, missing Spells and both missing; actual native books
  and all durable after-images exist before first output, plus two runtime reopens.
- The created Profs anchor authorizes the next real hit; a real generic Store
  publication/Frame file writer also advances the origin for the next hit.
- An unarmed neutral hit preserves the admitted absence without a proficiency
  packet or fabricated blank-file writer.
- Every two/three-target replacement prefix and every flushed-stage cut, with
  exact nullable Before, independently constructed after-images, unchanged native
  pre-publication state and two configured recovery opens without re-award.
- Actual late empty-file insertion/deletion refusal; real writable-preflight
  denial preserves both missing files, then a restored valid input succeeds.
- Foreign empty/reverse-prefix/stage inputs, same-semantic Spells Before
  substitution and altered Profs After refuse recovery twice without mutation.

These are source-authored controls, not compiler or runtime evidence. Root alone
integrates and qualifies. Existing native component mutation/training routes
which publish only their native/holder lease without the Frame file postlude
remain a separate durability frontier; this batch does not declare them saved.
SQL, fresh-request deduplication, output outbox/reconnect resync, multi-process
or power-loss safety, live-client acceptance and full parity remain unproven.
Preserve client execution hold, original data/saves/routes/backups/mail and AV.
