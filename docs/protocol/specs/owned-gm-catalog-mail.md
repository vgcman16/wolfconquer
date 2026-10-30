# Explicit GM catalog-item mail

2026-10-30. Spec-first extension of the owned GM toolkit, based on source2ff18f12.
Target protocol remains VisibleDx8D13 LegacyMsgMessage Talk. This adds no packet
layout or guessed catalog fields. Existing strict item catalog and mailbox wire
contracts remain authority; Chinese7937 live acceptance is not established here.

## Command and policy contract

```
@wolf item <positive-uint-itemId> <1..65535-stack> <retained-nonzero-GuidN>
```

The exact reserved namespace, ASCII256-character bound, whitespace/case behavior,
Talk-only gate, authenticated original connection and PIN rules remain unchanged.
The new parsed command kind is Item, with typed uint ItemId, ushort StackSize and
Guid OperationId. Flags, arbitrary UIDs, plus/bound/socket selectors, target names
and extra tokens are refused. The request is for exactly one catalog item row.

New closed capability mail.catalogitem.self = LegacyGmCapability.MailCatalogItemSelf
(enum3) is required. Inspect, mail.testkit.self and existing both policies do NOT
grant this capability. A policy may contain at most three distinct known closed
capabilities; all unknown/duplicate keys, capabilities, identities and types stay
rejected. Help advertises the item command only to its explicitly authorized actor.

## Actual operation and transaction rules

Retain the required typed mail capability in the closed GM actor captured from
the parsed command kind. The genuine selected OperatorMailOwner must match the
original actor and the exact required kit/catalog capability before plan/UID
reservation. A numeric ID, nickname, policy alone or caller-supplied callback is
never live authority. Existing OS-local operator mail API keeps its prior scope.

Construct one LegacyOperatorGearMailRequest from the actual actor/domain/operation
with fixed ASCII title, sender and content and one LegacyOperatorGearMailItem.
Reuse the same strict catalog/request preparation, stack/rune/unsupported-item
checks, global UID reservation, complete Character/Items/Mailbox WAL, canonical
Store receipt and mailbox notification pipeline. Do not directly insert items,
invent UID authority, skip missing metadata or auto-claim the mail.

The shared closed OperatorMailRequest retains real
LegacyOwnedNativeItemImage.ValidateItemIds CatalogEvidence for required native
Rune fields2/25. Request preparation refuses invalid evidence; plan preparation
rechecks that evidence, completes callback-capable catalog/baseline/owner checks,
then uses the pure retained evidence check immediately before UID reservation.
This also hardens the existing OS-local operator entrypoint. It does not promise
immunity to later uncooperative raw mutation; a later failure may still burn a
real reserved operation and must retain the existing uncertainty/no-remint rules.

Unknown/unsupported catalog rows, missing required rune metadata, excessive stack,
wrong capability, invalid actor, trade, recovery/debt and changed ownership must
refuse before any reservation or saved-state mutation where the existing pipeline
requires that preflight. Committed notification failure is not rollback. Retry
retains identical operation ID and payload; no automatic new ID/remint/replay.

## CLI and backward compatibility

create-gm-policy gains explicit catalog and all selections. catalog writes only
mail.catalogitem.self; all writes the three distinct capabilities. Its existing
default both, explicit both, inspect and mail values remain exactly their prior
grants. Creation still requires actual configured domain and existing saved numeric
identity, creates only a new reviewable inert file and never activates a server.

## Qualification and limits

Add actual registered Frame tests using owned runtime/registry/mail fixtures for
delivery, claim, retry, old-capability refusal, grammar/stack bounds and unknown
catalog. Keep the existing GM suites and strict invalid-policy coverage. Focused
parsers/policy tests verify new typed contracts and legacy non-widening; real CLI
tests verify catalog/all while default both remains unchanged.

Root alone builds, tests, integrates, commits/pushes or configures runtime grants.
Source authoring does not establish execution, deployment, safe host, full client
support, globally durable persistence or complete arbitrary GM mutation parity.
