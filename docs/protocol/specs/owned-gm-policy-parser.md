# Owned GM policy and command foundation

## Identity and scope

- Revision 1, 2026-10-30. Root-approved pure foundation for the first GM toolkit.
- Target ingress is existing Chinese/English staged `VisibleDx8D13` chat parsing;
  this package does not register a dispatcher, change packets or run a client.
- Policy and command values are immutable data. A matching policy predicate is
  **not runtime admission**: actual Frame/Store/domain/connection/binding/PIN,
  lifetime, trade, recovery, catalog and transactional owners remain required.
- No username, character attributes, Discord roles, community levels or numeric
  UID alone grants runtime authority. No foreign-target or mutation placeholders.

## Packet contract

Not applicable to this pure package. Root will consume already-parsed chat text
at a separately reviewed real Frame ingress before acquiring ordinary leases.
Existing successful client wire remains unchanged; no fabricated response bytes.

## Policy document contract

UTF-8 JSON without BOM, at most 16,384 bytes and depth 8. Comments, trailing commas, malformed
UTF-8/JSON, unknown or duplicate decoded property names and incorrect JSON types
refuse with `InvalidDataException`. Property names and capability strings are
case-sensitive. All properties below are mandatory; property order is immaterial.

| Location | Property | Required value |
| --- | --- | --- |
| root | schemaVersion | JSON integer 1 |
| root | expectedDomain | nonzero GUID in exact D syntax |
| root | grants | array of 0..32 grants |
| grant | accountId | JSON integer 1..4294967295 |
| grant | characterUid | JSON integer 1..4294967295 |
| grant | capabilities | array of 1..2 distinct closed strings |

Only `inspect.self` and `mail.testkit.self` are known capabilities. Duplicate
account/character pairs refuse even if their capability subsets differ. Empty
grants is a valid non-granting policy. GUID hexadecimal case is immaterial; no
braced, compact, zero or whitespace-padded domain forms. Numbers are not strings,
floats, exponents, booleans or signed negative values.

`LegacyGmPolicy.Parse(ReadOnlySpan<byte>)` snapshots the supplied bytes into
immutable values. `ExpectedDomain`, `Grants` and each grant's `Capabilities` expose
no mutable backing. `Allows(domain, accountId, characterUid, capability)` returns
true only for the exact positive domain/account/character tuple and one single
defined capability. Undefined enum values/composites, zero IDs and mismatches
return false. The predicate does not capture a live actor or issue a capability.
Missing startup policy is disabled by Root's loader; malformed explicitly
configured policy must fail startup, never fall back to a default grant.

## Command grammar

`LegacyGmCommandParser.Parse(string?)` returns an immutable result with status
`NotReserved`, `Rejected` or `Parsed`, nullable typed command and bounded error
enum. It does not save, allocate game item IDs, enqueue, send or authorize.

- Namespace and command/section/kit tokens match `OrdinalIgnoreCase`; typed
  results and kit names are normalized to the documented forms.
- Reserved namespace begins at character zero with exact token `@wolf`, followed
  by end-of-input or ASCII whitespace (space/tab/CR/LF/formfeed/vertical-tab).
  `@wolfish`, `@wolf-kit`, ordinary/null/empty chat and leading-space chat are
  `NotReserved`. Root preserves normal chat routing for that status.
- Classify the reserved namespace before content/length checks. Reserved input
  over 256 UTF-16 code units or containing anything outside ASCII 0x20..0x7e is
  rejected. Tabs/newlines following the namespace are reserved malformed input.
- Token separator is ASCII space only. Repeated/trailing spaces are accepted;
  whitespace, quoting, escapes and extra tokens do not introduce new arguments.

Accepted forms:

1. `@wolf help` (exactly two tokens).
2. `@wolf inspect` or `@wolf inspect <section>`; default section `overview`.
   Sections: overview, position, vitals, inventory, equipment, spells,
   proficiencies, quests, mail.
3. `@wolf kit <recipe> <retained-operation-id>`; recipes: basic-trojan,
   socket-materials, consumables. Operation ID is exactly 32 hexadecimal digits,
   nonzero GUID N syntax, parsed without regenerating or altering its value.

Unknown commands/sections/recipes, missing arguments, extra target/UID arguments,
bad or zero operation IDs and bare `@wolf` are `Rejected`. Errors carry no raw
input or supplied identifiers. There are no heal/teleport/grant/target stubs.

## Validation and transaction rules

This foundation has no mutation or durable transaction. Parsing prepares closed
data only and leaves input bytes/text unchanged. Root's later ingress must retain
the original admitted actor and recheck it under the actual operation lease.
Test-mail operation IDs must survive uncertainty/retry; parsing never mints one.

## Expected fixtures and evidence alignment

Policy tests cover exact schema/types/bounds, duplicate decoded keys/grants/caps,
closed capabilities, maximum IDs/grants/byte boundary, source mutation isolation,
read-only collections and exact tuple matching. Parser tests cover every form,
section/kit, case/spacing, retained operation ID, namespace distinction, malformed
reserved ASCII/length/argument cases and ordinary chat.

Existing `LegacyOperatorTestKits.cs` supplies the three real recipe names. Existing
`WorldPacketHandlers.Chat.cs` currently treats leading `@` as an unmodeled command;
this pure package neither changes that behavior nor establishes a running tool.
Catalog/table and wire fixture cross-references are not applicable to these data
parsers. Root must qualify actual owned inspection/mail integration separately.

## Implementation and qualification

- `World/LegacyGmPolicy.cs`, `World/LegacyGmCommand.cs`.
- `LegacyGmPolicyTests.cs`, `LegacyGmCommandTests.cs`.
- Authored under isolated `author-c`; no build/test/live/config/Git execution.
- No corrected legacy-source defect claim is made by this pure new foundation.
  Legacy nickname/attribute admin-NPC authority is a separate Root-owned decision.
