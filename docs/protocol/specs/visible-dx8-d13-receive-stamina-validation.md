# Visible DX8 D13 receive-effect stamina validation

## Identity and scope

2026-10-29, source-only proposal against private 202f/public 970. Target profile
is `VisibleDx8D13`. This covers the Caprice stamina-transfer branch and Light of
Stamina restore, not the independent attack admission, damage or other procs.
Existing community-source behavior is evidence for successful sequencing only.

## Packet and catalog contract

No packet layout or catalog change. Stamina remains update opcode 2176, protobuf
field 1 owner UID and repeated field 2 entry (type 8, unsigned value). Light of
Stamina remains opcode 2319, UID/Param target UID, effect 7. Keep existing effect
and scalar ordering. Caprice requires equipped rune 4028001..4028027; the existing
Consolidation chance and Caprice branch/rate rules remain unchanged.

## Validation and transaction rules

Use the shared private `TryReadLegacyRuntimeStamina(CharacterSnapshot,out ulong)`
API. A present Stamina value must parse with Integer/Invariant unsigned rules.
Present negative, malformed, overflow and empty strings are invalid. Only genuine
absence uses the existing 100/150 blessing default. No new gameplay maximum.

Caprice branch 1 requires valid stamina for both participants and
attacker <=ulong.MaxValue-20 before either prepared balance or effect stamp is
changed. The representability check is arithmetic safety, not a gameplay cap.
Invalid input leaves both prepared characters unchanged. Target stamina >=20
is still checked after the existing attempt stamp; this batch does not qualify
that insufficient-target/attempt-throttling rule as client-confirmed behavior.
No new test requires that ambiguous stamp behavior. Branch 2 does not read
stamina and remains independent. Existing valid chance sequencing remains.

Light of Stamina requires a valid stamina value before accepting the passive
effect when an attacker session is present. It restores values below100 to100;
other valid values keep the existing effect-without-restore behavior. A missing
session remains the existing non-mutating component case, not a fabricated owner.

An invalid receive effect must not undo independently accepted outer attack costs
or damage and must emit no stamina/refine success for that rejected effect. The
PlayerAttacks output sites also use the strict reader: Caprice reads both values
before its first scalar send, and Light validates before its refine/scalar sends.

## Ownership and persistence boundary

The actual Caprice preparation currently occurs in PlayerReceiveDamageModifiers;
PlayerAttacks publishes its two participants separately. The existing pair Store
API requires a real persisted writer, while Stamina is deliberately absent from
the current scalar persistence projection. A callback returning true cannot stand
in for that writer. This batch deliberately does not alter Store/DamageModifiers
or claim an atomic transfer. A later separately specified typed runtime pair
publication must pin both exact Store records/issuances and publish them together
before either external observer. Saved-stamina durability is also OPEN.

## Expected fixtures and qualification

Use actual saved Character attributes, registry/bootstrap, ordinary admission,
Frame Store, native owner, PIN and Recovery. Cover invalid attacker/target inputs,
absence defaults, successful20 transfer, Light restore and literal2176/2319 bytes.
Retain meaningful outer-attack assertions separately from pure receive preparation
controls. No fixture callback, placeholder saver or direct private backing writes.

New ReceiveStamina test partials contain26 authored cases.18 are explicitly
receive-preparation component controls on genuine admitted saved inputs;8 use
the actual attack handler or verify its absence defaults. Six actual-handler
cases cover successful Caprice, successful Light and four invalid-target Caprice
attacks retaining outer Mana/damage. Literal wire bytes are fixed independently
of packet builders. These are authored expectations, not execution results.

This isolated proposal has not been compiled, executed or observed in the client.
The coordinating agent owns integration and qualification. Valid transfer
publication atomicity and exact handler coverage remain open until implemented
and qualified; this document is not a fixed-bug or live acceptance claim.

## Root local qualification (supersedes proposal status above)

Root local qualification2026-10-29: all39 selected old-API numeric
controls first failed on exact c6c product, alongside two separately OPEN stale-Mana
output controls. After the reviewed numeric23 composition, sequential stable
build05 completed with0errors. Fresh numeric02 passed93/93,0skips/failures.
The wider combat/admission selection passed2749/2749,0skips/failures; selections
overlap. Its filter explicitly omits only the two separate unimplemented output
controls. That is not a whole-suite or output-freshness success claim. The earlier
overlapping build04/test attempt was interrupted and discarded. Actual TRXs/logs
remain under root-stamina-safety-next. Full successor CI and live remain pending.

## Latest combined qualification

Root local qualification2026-10-29: final combined magic/admission/required-
stamina/Frame selection passed2804/2804,0failures/skips. Shared Store/native-
vitals/ordinary-publication/testing-tool neighbors passed289/289,0failures/skips.
Selections may overlap. Build10 passed with0errors/157historical warnings, none
in changed paths. The original41 safety controls first failed on old product
(39numeric and2pending-output), and18 XP admission controls separately reproduced
their old paid rejection behavior. All applicable controls now pass. Full
successor Windows CI and live client acceptance remain pending; the last full
is exact c6c/40853051675, whose actual outputs/results were already preserved.

This supersedes earlier proposal/pending-output scope notes for this published
checkpoint. FSB193/194 record the current output/XP boundaries; Caprice pair,
remaining readers and whole-action/native/SQL/client acceptance remain open.

Latest full qualification, superseding pending full-gate notes above:

Full Windows [40867334704](https://github.com/vgcman16/wolfconquer/actions/runs/40867334704)
passed on exact531a9aa3d8e17c18ccd9f5bf68d480cbc5295319, containing source5daa0fe4:
Core29,833 passed/152 existing fixture skips, Updates446/446 and Launcher57/57,
zero failures; all eight stages passed. Actual local20261029T114153515Z-2d796ae9
finished2026-10-29T12:24:15.2854218Z. Workflow/job/local phase logs and all3TRXs
are retained; matching complete World/DataTool runtime files were preserved
outside the runner before any next candidate build. This qualifies the published
required-stamina, XP eligibility and pending-cost-output source changes.
Caprice paired transfer/shared journal successors, live deployment/client acceptance,
SQL/power-loss safety and complete English/gameplay parity remain unqualified.
No original-save/route edit, client relaunch or retained gear resend followed.
