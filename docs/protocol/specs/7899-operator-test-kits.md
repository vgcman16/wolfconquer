# 7899 offline operator test-kit requests

Revision 1, 2026-10-28. Companion to the local gear-mail transport. The generator
creates an ordinary request file; it allocates no item UID, grants no privilege,
writes no account/mail/save state and never connects or sends automatically.
The existing real Frame producer still validates the actual owner, domain,
account/character, catalog, physical rows and persistence before any grant.

`create-operator-test-kit <NEW-absolute-request.json> <domainN> <accountId> <characterUid> <basic-trojan|socket-materials|consumables>`
requires nonzero IDs, an explicit nonempty lowercase GuidN domain and a new .json
file in an existing ordinary local parent. Reparse parents, UNC paths, existing
files/directories and unknown kit names refuse. Parent folders are never created.

The generator uses the actual shared LegacyOperatorGearMailRequest/Item DTOs,
serializes case-sensitive camelCase JSON, and consumes the real strict pipe parser
before writing. One new OperationId is generated once for that file and printed
only after publication. Preserve the file and ID after an uncertain delivery;
generating a new request would represent a different operation. Sending remains
the separate explicit `send-operator-gear-mail` command against the matching
`wolfconquer-operator-mail-<domainN>` pipe. Neither step retries automatically.

Complete bounded bytes are written with FileMode.CreateNew to a GUID temporary
file in the same parent, exclusive sharing and Flush(true), then published with
File.Move(overwrite:false). The final path appears only after the complete write;
an existing/concurrently created destination is never replaced. A failed owned
temporary is cleaned up, preserving a primary and cleanup failure together.
This is create-only filesystem publication, not a journaled grant or proof of
directory-entry survival after a machine crash. Filesystem checks are observations
and do not confer authority against an unrelated external path replacement.

## Confirmed current catalog recipes

Read-only provenance: the items.json linked by the fresh World's actual
world-start.parameters.psd1, under its source/.setup_work/seed-json directory.
The following exact id/name rows were read on 2026-10-28; no binary inspection or
catalog guessing was used. Every requested item is a separate physical stack1
row, plus0, unbound, with neither socket pre-opened.

| Kit | Item ID | Seed name |
| --- | --- | --- |
| basic-trojan | 118003 | GuardCoronet |
| basic-trojan | 120003 | LightNecklace |
| basic-trojan | 130003 | LeatherArmor |
| basic-trojan | 150003 | IronRing |
| basic-trojan | 160013 | OxhideBoots |
| basic-trojan | 410003 | SteelBlade |
| basic-trojan | 420003 | VanSword |
| both | 1088000 | DragonBall |
| both | 1088001 | Meteor |
| socket-materials | 700011 | DragonGem |
| socket-materials | 700031 | RainbowGem |
| socket-materials | 1200005 | ToughDrill |
| socket-materials | 1200006 | StarDrill |

Basic kit has 9 rows; socket-materials has 6. Presence/name evidence does not prove
NPC socket costs, gem installation, upgrades or successful equipment rendering.
The real grant rechecks the active catalog and derives durability there. A changed
catalog can reject a recipe; the generator does not waive those checks.

The `consumables` extension is specified and catalog-cross-referenced in
[7899-consumables-test-kit.md](7899-consumables-test-kit.md). It has three rows:
Stancher1000000, Agrypnotic1001000 and LifeFruit723726. The real current grant
checks still apply; consumption and client visuals are separate acceptance.

## Qualification

Recipe/default, independent-request, invalid-ID/kit, real DataTool child-process,
strict parser, preservation and concurrent no-clobber controls pass in the
76-case operator/testing-tools selection. Existing mailbox/UID/admission
regression checks separately pass284/284. Full CI for this batch is pending.
Actual game-client mailbox delivery/claim and no-relog behavior remain separate
from generation; output metadata explicitly reports sent=false.
