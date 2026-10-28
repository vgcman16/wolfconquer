# VisibleDx8D13 Character and Spells journal v8

## Identity and scope
- Revision/date/owner: revision 1, 2026-10-27, sanitized-parity director task.
- Target: current `VisibleDx8D13` server reconstruction persistence lifecycle. This adds no packet or client-wire claim.
- Domain: one existing Character account file plus one Character-UID Spells file under explicit configured roots.
- Purpose: reuse the shared pending journal, staging, replacement, rollback, and fresh-owner recovery engine for a complete two-file mutation.
- Exclusions: no Items directory or item admission, no arbitrary interruption callback, no SQL, multi-process, power-loss, or deployment claim.

## Packet contract
Not applicable. No opcode, payload, ordering, or successful wire behavior changes.

## Catalog/table contract
- Users and Spells roots must be explicit canonical absolute paths, logically and physically disjoint, free of reparse ancestors/leaves, and disjoint from the shared journal except that the journal is intentionally owned below Users. Users must already exist; an absent configured Spells directory may be created only after pending publication for genuine first-save.
- Shared journal slot: `<Users>/.managed-trade-journal/pending.json`.
- Character target: `<Users>/<positive AccountId>.ini`.
- Spells target: `<Spells>/<positive CharacterUid>.bin`.
- AccountId and CharacterUid are independent positive identities and may differ.
- Each before/after image uses the existing 16 MiB per-target bound and the shared 192 MiB journal bound.

## Validation and transaction rules
- API: `TryCommitCharacterSpells(usersRoot, spellsRoot, changes, out reason)`.
- Bounded interruption overload: same arguments plus `LegacyCharacterSpellsJournalInterruption` before `out reason`; values are `None`, `AfterPending`, `AfterCharacter`, and `AfterSpells`. Invalid enum values fail before any I/O.
- Exactly two changes are required in canonical Character-then-Spells order after record construction and when reading v8 JSON.
- Character before and after are required; Spells before is nullable for genuine first-save; Spells after is required.
- Character images are decoded through the real Character encoding family, scanned before dictionary materialization for exactly one Character section, unique nonempty case-insensitive keys, and well-formed active noncomment lines, then completely parsed through `CharacterSnapshotParser`. Opaque keys and other sections remain permitted. Both embedded Character UIDs must be positive UInt32 values equal to the Spells change FileId. They are not required to equal the Character AccountId filename.
- Present Spells images parse through `PlayerSpellSnapshotLoader` and require positive unique spell IDs and nonnegative experience.
- Commit retains the existing mutation fences and shared pending publication, stage, replace, verification, rollback, and cleanup behavior. Version 8 independently creates the configured Spells parent only after pending publication, then revalidates logical/physical ownership before staging; it never creates or borrows an Items root.
- Before deleting a completed v8 pending record, cleanup must remove every same-transaction target stage, including a retained stage whose target already equals its after image. Cleanup failure refuses record deletion and preserves recovery evidence.
- Interruption after pending or either ordered target deliberately returns false with the recovery-required pending record retained.

## Strict v8 JSON domain
- Root properties are exactly `Version`, `TransactionId`, `Changes`, and `CharacterSpellsDomain`; unknown, duplicate, or missing properties fail.
- `Version` is exactly 8.
- `CharacterSpellsDomain` properties are exactly `Users` and `Spells`, both strings retaining the canonical absolute roots.
- Change properties and base64 image encoding remain the unchanged strict shared format.
- Versions 1 through 7 retain their exact existing property sets, target validation, ordering, and behavior.

## Recovery and readiness
- Fresh startup recovery reads the same pending slot and reuses the existing completed recovery engine.
- A v8 retained domain is admitted only when it matches the current actual seven-route recovery owner's `_admissionRoots.Users` and configured `_admissionRoots.Spells` routes.
- Pair-only recovery owners have no Spells authority and cannot recover v8 or report Character+Spells readiness.
- `RecoveryState.IsReadyForCharacterSpells(usersRoot, spellsRoot)` requires completed startup recovery, no failure/pending record, exact current configured Users+Spells routes, and a still-valid physical domain.
- Scope is process-restart reconciliation under cooperating path locks. It is not multiprocess serialization, directory-flush proof, or power-loss durability.

## Expected wire behavior and fixtures
- Positive: distinct AccountId and CharacterUid; existing Character before/after with matching embedded UID; absent Spells before and valid complete Spells after.
- Interruptions: after pending publication, after Character replacement, and after Spells replacement; fresh configured startup owner completes recovery.
- Refusals: wrong kinds/count, noncanonical retained target order, nonpositive IDs, missing Character before, missing after, malformed image, Character UID mismatch, duplicate/invalid spell rows, oversized images, foreign/overlapping/reparse roots, foreign current bytes, malformed/unknown/duplicate JSON properties, retained-domain mismatch, pair-only recovery owner.

## Evidence alignment
| Requirement | Existing source evidence | Catalog/wire evidence | Status |
| --- | --- | --- | --- |
| Separate account and character keys | FullAccount.Class.Persistence.cs routes Users by AccountId and Spells by Character.Uid | Not applicable to server-side file identity | Observed existing layout |
| Complete Character and spell images | CharacterSnapshotParser, CharacterSnapshotFileEncoding, PlayerSpellSnapshotLoader | No new client schema inferred | Existing parsers plus stricter ambiguous-image rejection |
| Joint restart recovery | LegacyTradeJournal existing pending/stage/rollback engine and explicit configured RecoveryState routes | No client acceptance follows from file fixtures | 49 direct v8 controls in the shared 354-case qualification |
| Unchanged earlier journal domains | Shared Format/Storage/Recovery v1..7 branches | No packet changes | Adjacent earlier-domain regressions passed in the shared selection |

This spec describes a server persistence extension. It does not infer catalog
fields or wire behavior. A fixture simulating a retained journal prefix proves
only the exercised reconciliation path, not OS power-loss or multi-process safety.

## Implementation and qualification

The integrated implementation is consumed by the existing FullAccount class-reset
path. It passes the shared354-case public/private selection, including49 direct
v8 controls and53 FullAccount controls. All52 old-source baseline names remain;
the corrected-fixture baseline was39passed/13failed. The earlier15 setup failures
are not business-defect evidence. Final public staging cleanup controls pass2/2.

See [actual scope and limits](../../FULLACCOUNT-CLASS-TRANSACTION.md). A new
exact-public full gate is pending. No deployment, live-client, SQL, native graph,
power-loss or multi-process completion follows. The ordinary account=UID loader
invariant is unchanged; distinct-key fixtures exercise the direct file API only.
