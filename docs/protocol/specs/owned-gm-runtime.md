# Owned GM test command runtime

## Identity and scope

Revision1, 2026-10-30. User-authorized development tools. Inbound and private
outbound `VisibleDx8D13` chat; the retained Chinese7899 protocol implementation
is the current wire evidence. Chinese7937 client/menu acceptance remains open.
This specification adds server command semantics, not a newly discovered opcode.

## Packet contract

Use the existing `LegacyMsgMessagePacket` protobuf codec and registered opcode.
The message is content index3. Only Talk channel2000 can execute `@wolf` commands.
Do not trust sender text or message UID fields as authority. Preserve ordinary
nonreserved chat. Malformed reserved commands are privately refused. Responses
reuse `SystemWhiteTalk`; no new fields, opcodes or broadcast destinations.

## Catalog/table contract

No new table layout. Three existing `LegacyOperatorTestKits` recipes generate
requests only. Actual item rows, metadata, required Rune data and route/domain
owners are validated by the existing operator-mail pipeline. Unknown/missing
catalog state refuses; recipe literals do not establish current item availability.

## Validation and transaction rules

Load bounded immutable strict policy once, disabled by default. Match exact
domain/account/character and closed capability. Capture original real Frame,
Store, transport, native issuance/binding/item image and admitted secondary PIN
before waiting for the exclusive world operation; revalidate the same actor
after queueing. Replacement with matching IDs cannot inherit the request.

Use actual runtime worker lifetime, world/trade gates and mailbox debt admission.
Do not call public exclusive operator APIs while ordinary chat holds Shared.
Closed internal inspection/mail cores reuse the genuine owners. Mail additionally
matches its actual selected owner against the retained command actor before
preparation/reservation. Existing journal, UID, unique-target and recovery checks
remain. Send after commitment and outside world/trade/mailbox locks; retain
truthful committed, uncertain and recovery outcomes.

Do not expose name-based legacy admin actions. Inspection copies bounded runtime
diagnostics only; cached mail reads never change cache or files. Game rewards
and authority cannot originate from Discord levels.

## Expected wire behavior and fixtures

Private help/inspection, at most12 lines of180 ASCII characters, collection output
at most9 rows. No command text or private paths echoed on refusal. Kit replies
identify operation and exact result enum; actual attachments are notified using
the existing mail codec. No implicit retry, automatic claim or reminted request.

Actual ordinary-login fixtures must exercise registered Frame chat ingress,
explicit grants, rejected channel/syntax/identity, original-actor loss, native
graph/PIN/retirement, catalog refusal, persisted mail/claim and repeat submission.
Retain operator pipe/inspection neighbors and valid existing wire goldens.

## Evidence alignment

| Requirement | Evidence | Status |
| --- | --- | --- |
| D13 chat framing/channel | Existing LegacyMsgMessagePacket and chat tests | Existing profile evidence; unchanged codec. |
| Kit catalog identity | Existing recipe spec and real catalog validation | Runtime validation required, not inferred from literal IDs. |
| Owned delivery | Existing UID registry, operator-mail WAL and ordinary admission tests | Reused real owners; GM ingress qualification pending. |
| 7937 rendered GM/mail UI | No current live run | Unverified; client execution hold remains. |

## Implementation and qualification

Focused policy/parser/startup, Frame GM partials, readonly cached-mail diagnostic
and dedicated tests. Root alone integrates and executes gates. Exact outcomes,
public commit and full gate are recorded in the current handoff; source authoring
does not qualify live deployment, SQL or complete gameplay parity.
