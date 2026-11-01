# Managed Character origin and query persistence

Status: OPEN, source implementation under isolated qualification. Profile:
VisibleDx8D13. No client binary, packet layout or live route is changed.

The configured account loader captures a complete bounded Character image under
the real file coordinator. Its actual private Frame startup binds that image to
the connection and holder identity. SourceFile strings and caller-supplied bytes
do not grant persistence authority. Native admission keeps its existing owners.

Starter/stat scalar writes use captured before/after replacement receipts.
Startup accepts their exact committed images; failed writes cannot claim Applied.
Starter Character/Items are ordered per-file phases with recovery fencing after
partial or uncertain commitment; this is not a multi-file crash-atomic journal.

Actual query HP and Vigor phases issue invocation-local registered purposes.
The Store prepares the exact holder and image, saves HP before publication, and
advances the private image only through its completed concrete writer. Vigor's
saved seed remains separate from its runtime clamp. Item/spell before-images
must match that saved origin; an actual committed journal receipt advances it.
There is no second generic Character save after the query's typed completion.

Scalar projection preserves encoding, unrelated raw lines, key comparer and
opaque case-distinct fields. Ambiguity checks cover this scalar schema and its
runtime aliases; they do not validate every subsystem's unknown fields or fix
all legacy parser collapsing. Default strict admission text projection remains.

Required controls: real InternalWorldEnter missing-default/composite positives,
item/spell and complete file/wire checks, saved Vigor clamp versus external drift,
external Money and snapshot-to-bind drift, real Windows write-sharing refusal,
and independent scalar byte/encoding regressions. No test has qualified this new
batch yet. SQL, live-client acceptance, sidecar crash recovery and overall parity
remain separate open requirements.