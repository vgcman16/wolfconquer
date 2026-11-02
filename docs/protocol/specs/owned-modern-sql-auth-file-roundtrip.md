# Owned modern SQL authentication to native file transaction

Status: implementation/fixture source, unqualified. This is not a production
schema migration or a claim that characters/items use SQL.

Target: MySqlConnector2.6.1 modern account repository, existing ordinary legacy
AccountServer forward and VisibleDx8D13 native World admission. Characters/items
remain their actual INI/BIN files. Original databases/configuration are outside
the fixture. All schema rows and credentials are synthetic.

## Required observed flow

1. Create a fresh unique owned test schema, with only the columns selected by
   the real repository. Seed two positive distinct accounts.ID values; EntityID
   deliberately differs. No production default, allocator or uniqueness rule
   follows from this test-only table.
2. Send real LegacyAccountAuthentication through the registered AccountServer.
   Parse Ready.Identifier and prove exact accounts.ID, not EntityID. Observe the
   actual handoff issuance. Existing legacy forwarding uses the identifier;
   this test does not establish redemption of the separately issued ticket.
3. Feed that exact forwarded identifier into real LegacyMsgLoginGame and the
   actual owned admission Runtime/Frame/native binding. Use no fake repository,
   accepting saver, owner injection or internal-managed fallback.
4. Sell one real inventory item through the existing NPC sale operation. Require
   complete file/native/holder convergence before each of its remove and two
   money frames, one payout, unchanged second actor and unrelated saved bytes.
5. Close actual peers/runtime, authenticate again against SQL, reopen file/native
   state, and require the saved result and no replay payout or item resurrection.

## Refusals and ownership

An actual exclusive character-file lease refuses the joined item/money change;
release then permits one normal retry. Wrong/missing accounts and real provider
missing-table failure issue no successful handoff or file change. Multiple SQL
rows for one username are an ambiguous principal even when only one password
matches; existing AmbiguousAccount response handling is retained. No new packet
IDs/layouts are introduced. Single-account password/ban/online semantics remain.

The disposable schema requires literal/local loopback and an explicit nondefault
port, CREATE receipt, fresh name and owned marker. Cleanup verifies ownership;
failure retains the owned evidence rather than targeting another schema. Real
server lifecycle qualification requires a separate fresh owned datadir/child,
synthetic credentials and bounded exact cleanup, never the original SQL service.

## Qualification

WOLF_SQL_AUTH_FIXTURE_ENABLED=1 and private
WOLF_SQL_AUTH_FIXTURE_ADMIN_CONNECTION opt into actual execution. Default skips
are not SQL evidence. A requested qualification must have a fresh terminal TRX,
all required provider/native facts executed and passed, no skipped/failed/zero
selection, then the appropriate complete source gate. No current passing SQL,
live client, stronger legacy-ticket security, SQL character persistence or
cross-process/power-loss guarantee is claimed by these authored tests.
