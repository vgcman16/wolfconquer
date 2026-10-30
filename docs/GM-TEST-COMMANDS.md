# GM test commands

This toolkit is disabled by default. The verified wire profile is
`VisibleDx8D13`; this does not establish Chinese7937 menu or client acceptance.
The current local client/updater execution hold still applies.

Use Talk chat from an explicitly authorized, genuinely admitted test character.
Replies go to that connection only. The account key is the numeric server key,
which may differ from the text entered at login. A character name containing
`[GM]`, a saved Role field, or a Discord rank grants no command permission.

| Command | Purpose |
| --- | --- |
| `@wolf help` | List the invoking character's permitted commands. |
| `@wolf inspect` | Character, position, resources and collection overview. |
| `@wolf inspect position` | Current and previous map coordinates. |
| `@wolf inspect vitals` | Observed managed and native HP/mana. |
| `@wolf inspect inventory` | Inventory root UIDs, item types, stacks and durability. |
| `@wolf inspect equipment` | Equipped root UIDs, positions, upgrades and durability. |
| `@wolf inspect spells` | Known spell IDs, levels, experience and soul fields. |
| `@wolf inspect proficiencies` | Weapon skill levels and experience. |
| `@wolf inspect quests` | Quest IDs, status, timer and objective counts. |
| `@wolf inspect mail` | Already-cached live mail IDs, attachments and rewards. |
| `@wolf kit basic-trojan <operationGuidN>` | Mail the existing low-level gear, DragonBall and Meteor kit. |
| `@wolf kit socket-materials <operationGuidN>` | Mail gems, drills, DragonBall and Meteor. |
| `@wolf kit consumables <operationGuidN>` | Mail health, mana and FullRestore consumables. |

`overview` is also accepted explicitly. Inspection is bounded to twelve private
ASCII lines, with at most nine rows per collection. Counts can exceed the shown
rows. Inspection does not certify saved-file equality, derived native maxima,
SQL durability, or atomic behavior of noncooperating raw native writers. Mail
inspection never loads a file, prunes a cache or reports an unavailable cache as
an empty mailbox.

Gear uses the actual catalog, owned UID registry and Character/Items/Mailbox
journal. It is delivered to the real mailbox; claim it through the normal mail
window. No reconnect is required for a subsequent delivery. There is no automatic
claim, arbitrary caller-selected UID, direct inventory insertion or max+1 grant.

Create one nonzero GUID in N format (32 hexadecimal digits) for each intended
delivery, and retain it. Retry the same request with the same ID. Never create a
fresh ID to recover from an uncertain result: that would be a different grant.
`PreviouslySubmittedOutcomeUnproven` is not a delivery confirmation or a refund.
Committed notification failure is not rollback. A recovery fence may require
repair before further world actions; a retired connection can receive no reply.

The startup variable `WOLFCONQUER_GM_POLICY_FILE` names one explicit ordinary
local policy file. A specified invalid file refuses startup. Missing configuration
disables the toolkit; no default account is privileged. The strict policy binds
one actual owned-admission domain to explicit positive account/character pairs
and `inspect.self` and/or `mail.testkit.self`. It is loaded once at startup.
Changing it requires a reviewed staged-server restart; this document does not
authorize restarting a preserved live session.

To prepare a new policy for review with the built DataTool:

```powershell
dotnet .\src\WolfConquer.DataTool\bin\Release\net9.0\WolfConquer.DataTool.dll create-gm-policy C:\Tests\gm-policy.json C:\Tests\owned-admission.json <numeric-account-key> <character-uid> both
```

Replace the example paths and numeric placeholders with the actual owned fixture.
The tool checks the existing domain and saved character, creates a new file only,
and leaves live permissions and environment unchanged. Use `inspect` or `mail`
instead of `both` to restrict the grant. It never bootstraps a UID registry.

The old Stuff/Stuff12 admin NPC privilege routes are disabled. Their nickname
and saved-attribute checks are not an alternate authorization path. Ordinary
Inner Power NPC behavior remains separate.

Teleport, free healing, level/class changes, spell/quest mutation, event control
and arbitrary rewards are not registered commands in this release. Their next
implementation requires genuine transaction ownership and persistence rather
than disguised potion consumption or raw saved-field edits.

Qualification is recorded in `CONQUER-HANDOFF.md` and the current work checklist.
Authored commands and isolated tests do not establish a deployed server or a
live client test.
