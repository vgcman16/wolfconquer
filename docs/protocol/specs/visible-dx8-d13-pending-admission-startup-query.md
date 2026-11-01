# Pending admission startup query

Status: OPEN source implementation, unbuilt and unrun by the author.
Target: VisibleDx8D13; existing startup packet layouts and order are retained.

An XP continuation marker cannot authorize ordinary startup mutations. The
actual ordinary admission plan must first validate the saved marker before its
native prefix, commit and publish its real owner, and issue its exact
`ExperienceAdmissionOrigin`. Startup retains the same registered exclusive
login operation; it does not acquire or upgrade a nested operation.

`PreparedAdmissionContinuationQuery` consumes that actual origin and the
Frame's registered login context. Preparation runs under the established
lifecycle/package/connection/native actor gates, with the actual Character and
optional Spells participants rechecked in sorted file-lock order. It uses the
existing selected metadata, full item image and shared equipment-query
preflight. It does not manufacture a completed cache or simulate publication.

AnswerOk and the required hero/startup prefix retain their original position.
At the real initial equipment-query location, the prepared owner registers one
private invocation of the existing full `InitialWorldEntry` query, with
`CallItems:true`. Each pending mutation uses the private execution's concrete
one-use proposal and the actual Store/writer ownership described in
[continuation query mutations](visible-dx8-d13-continuation-query-mutations.md).
The saved marker, level, XP and allocated stats remain unchanged by startup.

Only actual full query completion issues `CompletedAdmissionContinuationQuery`.
It retains the admission origin as historical provenance and the exact current
holder/native completed-maximums proof as its successor. The original origin
is not refreshed to an arbitrary holder or new file image. Continuation resume
begins from this closed completion; workers begin only after startup and resume
both complete. A failure after committed admission must retain its real
recovery fence and pending durable work, not report a clean rejected login.

The newer-client Pirate Revelator startup branch can require a second actual
equipment query. The implemented successor factory consumes the actual first
completed receipt once, captures its current saved image, and permits only
the second source invocation. Its real Frame callsite must still prove that
conditional branch; a copied first-query scope is not authority. Required
derived-spell changes retain journal23 and its actual participants, now joined
by the source-only [pending participant](visible-dx8-d13-pending-derived-spell-journal23.md).
Neither a standalone Spells image nor this Character proposal grants journal
authority. Detached-candidate feasibility and login spell-publication succession
remain required before pending startup activation.

Team owns admission/startup routing, Store guards, file permits and resume.
Root owns the Frame's registered login operation. This producer adds no global
exactly-once delivery, all-startup crash transaction, or live-client proof.
Qualification must include actual awarded pending files admitted through the
configured path, startup and resume interruption, no worker release on failure,
and missing/stale/copied origin or invocation refusal.
