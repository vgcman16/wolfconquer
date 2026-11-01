# VisibleDx8D13 money-drop retry origin

Status: SOURCE ONLY / UNBUILT / UNRUN (2026-11-01). This is a pending Sanitized Parity correction, not a verified fix or live-client acceptance claim.

## Scope and source evidence

The target profile remains `VisibleDx8D13`; ordinary floor visibility uses2164, native death uses2135 and the final learned-proc animation uses2239. No wire shape, successful sequence, drop roll, item choice or amount rule changes here.

Both the owned Dragonhowl killed-monster planner (`WorldSessionStore.OwnedDragonhowl.MonsterDrops.cs`) and the generic killed-monster body (`WorldPacketHandlers.MonsterDeath.Drops.cs`) copied a failed-placement coordinate reset from the emulator reference. A rejected money pile changed the next search origin to(0,0). Although money correctly forbids x0/y0, its radius1 search could still place a subsequent pile at walkable(1,1), unrelated to the killed monster. That is an unsafe source bug, not client protocol authority.

## Corrected retry invariant

- Begin at the real monster coordinate.
- After a successful ordinary placement, advance to that actual placed coordinate as before.
- After a rejected money placement, retain the last valid origin for every remaining selected pile. Do not reset to(0,0), add a fallback location or expand the search radius.
- Retain all four selected attempts for saved roll49. A no-ground money attempt must not demand its amount or consume a floor UID. A selected ordinary item still consumes its existing selected UID and stops on placement failure.
- Preserve special-bundle origin behavior, zero-edge rejection, strict visible/reserved UID collision refusal, capacity/pickup ownership and existing committed-output failure fences. No expired UID is adopted or evicted merely because its lifetime elapsed.

## Real owned regression setup

Use actual saved native19 gear201, real ordinary admission, a learned17490/17650 spell, a registered family910 monster at(11,10), actor(10,10), and complete walkable64x64 terrain. Do not invalidate terrain merely to force no-ground:17650 requires a genuine line endpoint before floor planning.

The saved magic row has range4. Its real Ironbone17650 line endpoint is(14,10); the17490 animation remains anchored at(11,10). Both independently retain4200 target damage. Preserve the D13 death2135 assertion and diagnostic committed-mana assertion already in the Root candidate.

For ordinary non-capacity no-ground controls, use real `Floor.AddVisible` entries at every tile in x10..12/y9..11: exactly nine occupied legal radius1 candidates around the monster. Keep walkable(1,1) free. Assert no emitted floor packet, no new floor entry, exact retained entry identities, and `Available` status for every seed. This distinguishes a legitimate no-ground outcome from an unrelated origin reset. Saved money roll49 keeps four attempts with UID delta0; selected item retains UID delta1.

For the capacity branch, preserve4096 real visible entries and genuine active pickup reservations. Assert exact retained identities and `Busy`; release only the test-owned pickup reservations in fixture cleanup. Do not substitute a smaller private capacity or fake reservations.

Retain exact actor/native cost, attack stamp, death/CP/final-animation ordering, output fencing, full character/spell/gear/item/proficiency file checks and independent2164 bytes from the existing floor suite. This correction does not weaken existing output-loss or expired-visible-UID collision controls.

## Retroactive test correction

Three old generic-body/UID tests explicitly expected a remote(1,1) money pile after an unrelated failed local search. Replace their unsafe expectations with no payload read, no packet/visible entry and no UID consumption. Preserve successful-placement amount/UID sequences. Keep the zero-edge control genuinely reachable using optional-at-end fixture DATA coordinates: register that test's monster at(1,1) while only x0/y0 terrain is walkable. This uses the real existing body and floor store, not a copied placement algorithm or minted admission authority.

## Qualification boundary

Root must integrate this complete delta and run the actual existing floor and generic deferred-money suites, then the appropriate broader gate. No compiler or tests were run while authoring this intake. The generic body instrumentation is a bounded source-body regression; it is not ordinary admission, a native capability or a live client. The owned proc tests retain the genuine admission path. This work does not claim full reward transaction closure, host trust, deployment or total client parity. The live legacy executable hold stays in force.
