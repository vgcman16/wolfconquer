# XP Shield continuation after reentrant death

VisibleDx8D13, offline managed runtime fixture; no wire/schema change. Source AttachStatus 1020 removes earned XPList before shield and animation. Sanitized Parity overrides the decompiled unsafe tail when a real send callback kills the caster. XPList removal already committed is not rolled back; preserve the real death and its output, stop the stale continuation, and publish no new shield, timer or success animation. Capture the real Store native identity and exact session at each awaited boundary. A same UID, revived/replaced or intervening session is not the accepted continuation. The real shield commit must be exact-current and keep status and native clock coherent; no timer may be installed for a refused commit.

The fixture must wait for each genuine player's XPCircle frame, current XPCount and re-armed one-shot timer after advancing the manual clock, keeping 100 separate eight-second advances. It must not seed XP or use a fake worker. Capture full output and legitimate death effects; existing live success tests remain. No source execution in this work; focused, full CI and client evidence remain separate.

When a new Shield is required, validate the used duration before any cost/cooldown or XP admission: signed clock seconds and both actual UTC/local deadlines must be representable. Preserve zero as a valid immediate expiry and revalidate at clock preparation. Copy character maps using their captured comparer, reject ambiguity only for owned keys, preserve unrelated exact-case variants, and compare canonical maps by exact key/value membership rather than enumeration order.

Shield uses the existing captured-owner XP admission gate. Before generic cost/cooldown commit, validate the actual selected status representation and XP-removal registration; if a new shield is needed, also validate the actual clock/flag keys and representable duration/deadline. An existing Shield does not consume its timer fields or supplied duration and must keep its actual timer. User-owned malformed/ambiguous used fields fail with XP and resources untouched. PreparedInstall owns this operation; the older Install path used elsewhere has no new atomicity claim.

## Real worker expiry validation

The actual global XP expiry producer must preserve unrelated case-variant fields.
It resolves only its owned expiry/auxiliary/vector keys, refuses ambiguous or
malformed complete projections without consuming an earlier local expiry, and
updates both agreeing status representations. Never silently parse invalid words
as zero. The earned XP fixture labels the real worker timers and observes their
newer one-shot generations; it must not grant XP or manufacture worker progress.
Build07 and focused08 passed 17/17 bounded XP expiry/Shield checks. Full Windows,
complete native/saved admission and live acceptance remain separate gates.

Focused09 widened to actual XP activation/damage/expiry and real owned admission
plus omitted public Shield negative/active-timer methods. Actual result: 505
passed, zero failed, two existing external-fixture skips (507 total), 1m17s.
Its generated filter also matched broader Advance/Dispose/Fire names; do not
present that total as exclusively XP tests or add it to overlapping focused08.
