# Root item-effect clock lifecycle

## Current transport integration proposal

The combined candidate now releases the global Shared lease before Root expiry
network sends. Capture and Store commit retain short, actual leases and the
private tick/holder authority. HookMoon, Root expiry and CleanSweep retain their
existing family order. Other timer families and ordinary frame sends have not
been generalized by this change.

Each successful Store expiry also returns a raw-map witness prepared over its
actual final session before the writer. The next ordered entry uses that map
with the actual successor holder, rather than the initial capture's old map.
Moving a later clockless deadline in place must stop the captured continuation;
the next genuine timer scan can later expire it at its changed deadline. The
new regression checks both unchanged completion and this future-deadline case.

A committed expiry issues one-use recipient permits. The real TCP connection
registers its writer, using the same gate and encrypted stream as ordinary sends.
After taking that gate, a private non-null active-start token authorizes a fresh
current-holder vector under ordered Store/native gates. Both status aliases must
be unambiguous. Current same-identity successors receive their current vector;
an old committed vector is never replayed over a newly installed generation.
The actual write starts before Store gates are released; asynchronous completion
is awaited afterward. Ten seconds of blocked transport retires that connection;
an interrupted encrypted write also retires its stream/cipher. This is bounded
slow-client handling, not delivery recovery or a durable notification outbox.

Local TCP fixtures exercise the shared writer gate, independent frame reader,
ordered observers, real backpressure, native-owned successors and refusal paths.
Null/ungated/unregistered starts and ambiguous fresh status must not invoke the
writer. Broader Stigma/Shield and XP-status expiry fixtures now use real sockets.
Build16 passed with zero errors. WIRE11 completed30passed/5failed/0skipped;
the eight malformed-succession and two clockless successor controls passed.
After correcting its deliberately stalled recipient to a third real viewer,
the public Pirate dispel/reinstall and later expiry control passed1/1 in
OTHER-CLOCK17. Four owned timer stalls remain open and the complete35-case
selection has not been rerun after that fixture change. These bounded results
do not prove an encrypted login handshake, full timer safety or live behavior.
The retained sections below describe earlier source-only proposal history.

This source-only bundle targets the VisibleDx8D13 managed/offline path, based on the frozen 218d5d70574c4de75d01362aa3b6ef9510312653 candidate with its Vigor15 and XP Shield admission changes. It composes the four prior isolated scopes; it is not an execution, live-client or full-parity claim.

A real manual item proc issues a one-use private admission associated with its exact Store, connection, expected holder, native identity, flag, duration and source provider. The prepared Store operation checks it before preparation and consumes it before its writer. XP Shield keeps its distinct real XP-removal admission. Both prepare the clock and canonical holder under Store then clock gates. Their preallocated clock/map before-image is joined to the real prepared Store publisher's pre-writer and pre-native currentness checks; the actual publisher privately issues and consumes a clock-publication permit. A detached/no-Store producer only reaches a separate detached clock table. The obsolete UID-only Store clock removal and direct Store clock installation APIs are absent.

A clock registration records the actual published holder, native identity and raw-map witness. Only an actual prepared Store expected-to-canonical-to-published transition can retain it, with the flag and its narrowly owned marker, deadline and death policy unchanged. That participant uses preallocated proofs and private publication association. A legitimate projection change retires the exact old clock. Expiry preparation retains preallocated key/object witnesses so its final plain map publication cannot restore an entry the same actual holder publisher retired. This reconciliation is defensive; no reachable same-publication cross-clock retirement has been demonstrated.

Root's candidate corrects an unsafe proposed exception path: malformed operated projection data must reject the Store publication before its writer, not be classified as a legitimate removal. Otherwise the real clock could be retired while the published Shield flag remained visible. Four proposed actual-producer/Store controls retain the original holder and clock after ambiguous marker, deadline, death-policy or vector inputs, then observe that clock's genuine expiry. Separate malformed global-subject coverage uses explicit external DATA corruption and does not claim Store acceptance. FSB-301 remains OPEN until compilation and runtime qualification.

The real global worker privately issues a tick for its configured provider, sampled loop time, cancellation and active world-operation lease. Its own scan reads the real subject time and associates each ordered due observation with that tick, the Store, connection, identity, exact expected holder, raw map and actual clock object or established absence. Clockless capture and preparation observe true absence under the real Store connection gate rather than creating an empty clock owner. The Store accepts and consumes only this one-use private authorization before its writer. Data records, arbitrary clock values, UID matches, empty tokens and non-8/9 flags do not confer expiry authority. A visible future clock or clockless deadline is not due. Actual missing-flag cleanup is a separate captured classification. Existing subject-local handling rejects malformed/ambiguous owned keys without mutating that subject, while continuing other subjects.

Expiry uses the existing unique case-insensitive matching rule for operated marker and deadline keys while preserving the source dictionary comparer. It checks both status-vector representations and updates only known keys. A Store commit holds the actual connection and clock gates; reentrant send continuation checks the exact committed holder and native identity after every await. The transport invokes its delegate outside the Store gate, leaving a concurrent check-to-send-entry race. Uncooperative raw in-place mutations changed and restored between witnesses are unversioned. If a raw mutation occurs between the prepared publisher's last currentness check and its subsequent clock publish, the defensive post-publication proof can still refuse after the holder/native write; no crash-safe rollback is claimed. These boundaries remain open.

Real managed/runtime test proposals cover actual 15/20-second clocks, two due entries, same-UID holder succession, reentrant processed and other-flag reinstallation via actual Pirate Dark and Root procs, unique-case keys, an ambiguous subject alongside an independently expiring real Classic Stigma, and Opaque/opaque retention. Owned fixture proposals additionally use saved item/spell/gear inputs, the real native19 CombatGear loader, actual admission and Frame routing, complete file snapshots and native/player checks. Future/unassociated/invalid authorizations and an unassociated manual-proc token are tested as negative controls. The actual owned Pirate Dark gate and test sequencing remain unverified; if it refuses, preserve and prove that real refusal instead of bypassing it. Classic Stigma's actual producer uses wall UTC, so the fixture observes the actual deadline before advancing its existing clock.

No compiler, restore, tests, Git, client, native process or runtime job was run for this bundle. FSB notes are open source observations, not fixed claims. Integrating changes requires a narrow hunk merge with Root's current shared Store publisher, XP Shield, global worker, fixture and other active branches; qualification remains Root's responsibility.
