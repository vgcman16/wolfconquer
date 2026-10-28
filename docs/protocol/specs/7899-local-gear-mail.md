# 7899-local-gear-mail: local operator test equipment delivery

## Identity and scope

- Revision 1, 2026-10-28; requested by the project owner for disposable gear tests.
- Target Chinese client 7899, `VisibleDx8D13`; the staged DX8 D13 executable remains unchanged.
- Send catalogued equipment/materials to an already admitted online test character's mailbox, then claim through the existing client mail flow without relogging.
- A new server build/start is required to install the sender. Original saves, routes and restore archives are excluded.
- This is an explicitly enabled local operator facility, not a new gameplay packet or player privilege.

## Packet contract

- Preserve existing server mail list 2183, content 2058, mailbox mode 2264 and notification 2066 codecs.
- Use their existing D13 encoders; no new client wire fields are inferred.
- A mail attachment's `ItemId` is a physical item UID, not the catalog type ID.
- Saved expiry uses positive local wall-clock ticks, matching the existing mail
  writer; local `DateTime.ToBinary()` kind bits are not the saved-mail contract.
- Item claim moves the existing recipient-owned warehouse-1000 item through the existing claim transaction.
- New-mail publication follows durable delivery and current-session revalidation. Transport failure never refunds or repeats a committed grant.

## Catalog/table contract

- Resolve every item type through the configured `WorldDataCatalog.ItemTypes` authority before reserving UIDs.
- Reject missing item metadata, invalid durability/stack/count and unsupported overrides. Do not accept arbitrary serialized item bytes or caller-selected item UIDs.
- Initial real seed candidates: 118003 GuardCoronet, 120003 LightNecklace, 130003 LeatherArmor, 150003 IronRing, 160013 OxhideBoots, 410003 SteelBlade and 420003 VanSword. Materials 1088000 DragonBall and 1088001 Meteor also exist. Catalog presence is not proof of client rendering or forge support.
- Body 1008, class 1000, level 120 has modeled eligibility for these low-level gear candidates; validate actual current state for each test.

## Validation and transaction rules

- Require explicit host opt-in, the genuine already-open owned-admission domain, its bound Frame, a current online authenticated target and the actual item/mailbox/recovery owners.
- Local transport must be available only to the server's current OS user; no public TCP administrative listener, chat command, name-based GM authority or authentication bypass.
- Requests carry a nonempty operation GUID, expected domain, target character UID and 1..32 catalog item rows. Bounds and catalog checks precede UID issuance.
- Use the current leased registry's durable `ReserveForOperation` with writer `local-gear-mail-v1` and a deterministic request digest. Never open a second writer or manufacture UIDs.
- A previously recorded operation never issues another materialization token. Report a previous submission with an unproven delivery outcome; do not call it a newly delivered success. A fault after reservation may burn unused UIDs. Automatic recovery/rematerialization of such grants is outside revision 1.
- Serialize using existing world-operation, trade, mailbox, session CAS and file coordination. Revalidate the target, exact Character/Items/Mailbox baselines and recovery readiness at commit.
- Prepare real warehouse-1000 items and mail rows together. Use the existing v4 Character/Items/Mailbox journal, then publish the session/native item graph and mailbox cache. No cache-only grant or independent item/mail writes.
- An absent mailbox may be initialized as a genuine empty mailbox before reserving any reward; record that initialization separately from the subsequent journaled grant. Do not equate absent and empty saved files.
- Rejection leaves balances, existing items, mail and success queues unchanged. Never debit the test character for operator grants.

## Expected wire behavior and fixtures

The local operator transport is a Windows CurrentUserOnly named pipe named
`wolfconquer-operator-mail-<explicit-domainN>`, enabled only by
`WOLFCONQUER_OPERATOR_MAIL=1` with the actual bound runtime. It uses a positive
little-endian32 length followed by strict camelCase JSON request (16KiB max)
and response (64KiB max). Unknown/duplicate fields, malformed objects and domain
disagreement refuse. The real worker is canceled and joined before its runtime
is disposed. Client/per-connection IO has a ten-second total deadline; timeout
is an unknown outcome, not rollback or completed-drain proof.

- Valid path: operator submit -> durable unique item reservation -> journaled attachments and mail -> live mail notification -> list/detail -> claim -> inventory -> equip -> stat/model refresh.
- Verify duplicates, unknown types, bounds, stale/disconnected target, stale files/cache, insufficient inventory on claim, journal failure and repeated request GUIDs.
- Use genuine new owned temporary fixtures and the real registry, serializers, journal and session owners. Tests must not substitute accepting save callbacks or fake allocation queues.
- A live claim and equip are separate acceptance checks after implementation tests; menu opening alone does not qualify them.

## Evidence alignment

| Requirement | Existing evidence | Remaining check |
| --- | --- | --- |
| Gear menus render | Local D13 equipment, inventory and ten forge category views observed 2026-10-28 | Real eligible item actions and persistence |
| Mail is cached in-process | `LegacyMailboxStore.cs` | Sender publishes the actual cache after commit |
| Attachment ownership | `WorldPacketHandlers.Mail.Claims.Plan.cs` | Live claim of newly issued gear |
| Genuine UID issuance | `LegacyItemUidRegistry.OperationReservations.cs` | Duplicate/failure tests and live receipt |
| Joint save | `LegacyTradeJournal.MailboxOwner.cs` | Real transaction failure tests |

## Implementation and qualification

- Revision1 implementation builds and passes the76-case operator/tools selection,
  plus284 existing mailbox/UID/admission cases. This is isolated server/tool
  evidence; no real game-client hot delivery or new deployment is claimed yet.
- Keep notifications, focused tests, full CI, source publication and live-client acceptance recorded independently.
- Known UI issue from the current inspection: subclass labels contain mojibake; numerous forge labels remain Chinese. This sender does not claim to repair localization or implement every forge handler.
