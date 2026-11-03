# Retained SQL authentication ownership

2026-11-03. Reviewed source is integrated on base `e6f1361c` in an isolated
worktree: **22 C# paths, 32 actual provider Facts passed** as part of the combined
authentication/handoff family: **119 passed, 0 failed, 0 skipped**. Ordinary
account/Frame/handoff neighbors also built and passed **124/124**, with no failed
or skipped cases. A new full Windows gate remains required for this successor.

## Contract implemented in source

- Only the concrete repository may acquire an owner, from a complete real SQL
  candidate scan and strict password validation on a real nonpooled connection.
  Duplicate usernames refuse before credential selection. Initial username lookup
  keeps database collation; the owner retains the actual canonical row name.
- Required identity/permission values cannot be NULL or coerced strings/bools.
  BannedID must be 2; any nonzero Online remains online. EntityID may be NULL/0
  for an uncreated character, otherwise it must fit UInt32. Empty stored passwords
  retain exact existing comparison semantics. No saved-owner rebind is added.
- Actual account and private random claims, selected database and physical
  connection remain bound to the owner. Revalidation checks current row identity,
  permissions and a salted credential fingerprint without retaining plaintext.
  Changed/malformed state or lost local ownership retires the owner before close.
  Failed owner close retains cleanup-only disposal custody; I/O errors remain errors.
- Domain alignment uses a distinct actual target connection and real claims.
  A foreign schema or target-only fault cannot grant admission or silently replace
  a healthy local owner. No caller-supplied row, accepting callback or ticket issues it.
  A provided transaction must belong to that exact target connection before any
  SQL is issued, independently of the provider's IgnoreCommandTransaction option.

Root read all source/test bodies and verified predecessor bytes before intake.
Email and password-diagnostic method bodies remain byte-identical. The tests use
owned, nonce-marked MariaDB schemas and concrete repositories; no fabricated owner
or provider replies. Scope includes credentials, invalid state, conflicts,
cancellation, disposal/KILL, retirement, schema alignment and actual TINYINT fields.
Two later controls cover closed-target and actual mismatched-transaction faults
without retiring a healthy local owner. Source review of the package-pinned
[provider guard](https://github.com/mysql-net/MySqlConnector/blob/5802bd06d4e0787215b9f7acc4c8f8cf4ee8de8a/src/MySqlConnector/MySqlCommand.cs#L403)
found its configurable bypass. The integrated application guard is unconditional;
its authored regression uses a fresh owned fixture for both false/true settings,
preserving valid actual transactions, local ownership, exclusion and account rows.
The actual provider regression passed for both settings; FSB-347 is qualified
within that component/provider boundary.
Three further Facts cover actual AccountLoginService strict/legacy positive paths,
NULL BannedID/Online refusal in all three comparison modes, unchanged rows and
successful login after restoring only the owned fixture cell. These are authored
controls that passed in the combined family. The authentication-only source
inventory was91 methods; the additional28 handoff Facts make119. Root matched
all119 unique source methods to actual passing results, not just source counts.

## Required next verification and remaining horizon

The completed full-provider receipt is qualified and marks the complete family
as executed. Root checked actual child/command settlement, removal of owned data
and private options, and closure of port34397. No primary/cleanup errors occurred.
Run the appropriate full Windows integration gate on the new published source.
No second physical-server proof or teardown-failure retry is claimed.

The [authenticated SQL handoff](SQL-AUTHENTICATED-HANDOFF-20261103.md) now joins
the real issuer to current SQL-world six-image ownership at a component boundary.
Frame/transport lifetime, normalization/CAS/recovery, native/Store/all later
writers and global UID custody remain required before backend activation. Existing deny-only
markers stay deny-only; native gameplay still saves files. Full 7937 session,
Echo/item actions, English rendering and live acceptance remain open.
Current legacy internal ticket issuance passes GetHandoffCharacterId(account)
into its AccountId field. The future SQL join must retain the true authentication
account ID separately from character UID and bind the real issuer/Frame lifetime;
that legacy ticket value alone cannot authorize the SQL account.
No routing, client/updater execution, original saves, backups or AV settings changed.
