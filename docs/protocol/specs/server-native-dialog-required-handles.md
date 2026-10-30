# FSB-217: Required native dialog handles

## Identity and scope

2026-10-29. Existing native Dialog owner over NPC2255 replies; old7899/DX8D13
source/fixture evidence. No new client profile, host dispatcher or live activation.

## Packet contract

No wire changes. Each successful dialog method retains the existing prepared
2255 reply bytes, aliases and per-call send order. Messagebox finalization remains
a no-op; ordinary finalization emits the existing Finish reply.

## Catalog/table contract

Not applicable: no catalogs or new field meanings.

## Validation and transaction rules

The constructor requires actual nonnull client and packet handles before creating
the owner; retain the original client binding. No encoded reply may be produced
through a dialog constructed with a missing recipient. The exposed mutable stream
can later be replaced; the underlying prepared reply codec rejects a missing stream
before changing a packet or queuing that call. No fallback packet/client is supplied.

Earlier successful method calls are individually committed. A rejected later call
must preserve their existing queued bytes, with no fabricated Finish message or
pretend rollback of already sent data. Real send failures after frame commitment
are not delivery/transaction rollback proof. Fake-recipient filtering keeps its
existing actual sender semantics; it provides no authentication or gameplay proof.

## Expected behavior and fixtures

Retain the independent full-chain2255 goldens, original client/stream alias tests
and alternate valid stream behavior. Replace null-client encoding and invalid
constructor acceptance tests with ArgumentNullException before any owned packet,
queue or client-state mutation. Preserve the prior valid queued reply on a later
null-stream refusal. Source map finds one actual production constructor caller in
GameClientRuntime.BoxDialog, which passes this and its real recycled packet.

## Evidence alignment and qualification

Combined02 retained two null-stream exception mismatches. Source review also found
a passing test requiring reply encoding before a missing-client send failure.
New source corrects these owner failures; managed qualification is pending Root's
combined build. This does not establish all-dialog atomicity, live rendering,
SQL, new7937 acceptance or whole-server parity.
