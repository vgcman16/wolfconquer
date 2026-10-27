# VisibleDx8D13 NinjaRank file persistence

## Identity and scope
- Spec revision/date and requirement owner: revision 1, 2026-10-27, sanitized-parity director task.
- Target client build/profile and evidence identity: current Chinese 7899 / `VisibleDx8D13` server reconstruction; this is server file ownership, not new client-wire evidence.
- Data/catalog version: legacy `NinjaRank.txt` text image.
- Direction and lifecycle stage: server startup/load and server save prerequisite for ordinary login ownership.
- Intended behavior: admit and publish one complete six-category ranking graph, or retain the prior whole graph. Preserve the successful file record. This does not install login startup wiring, change packets, or prove live-client behavior.

## Packet contract
Not applicable. This change does not alter a packet, opcode, field, ordering, encoding, or successful wire sequence.

## Catalog/table contract
- Exact path contract: `LegacyNativeServerConfig.DbLocation + "NinjaRank.txt"`; no separator repair or alternate location.
- Text encoding: strict UTF-8 without a BOM or BOM-identified UTF-8, UTF-16 LE/BE, or UTF-32 LE/BE, matching the BOM families recognized by the prior `StreamReader` owner while rejecting undecodable bytes.
- Header: exactly one control-free `Count=<integer>` line. The integer uses a captured `CurrentCulture`, is in `0..60`, and must equal the number of physical record lines. Ordinary culture-valid spaces and positive signs remain accepted; embedded control characters such as NUL are invalid.
- Record: exactly 21 slash-separated fields followed by the successful trailing slash. Extra and missing fields are invalid.
- Operational bounds: at most 1 MiB input, 60 physical rows, and 4,096 characters per line. These are sanitized file-intake limits derived from six retained top-ten categories; they are not client, native, or gameplay limits.
- Category: numeric byte value `0..5`, corresponding to Overall, Fire, Water, Earth, Wind, and Lightning.
- Key: UID is nonzero and unique within its category. The same UID may validly occur in different categories.
- Ranking: every detached category has `MaxItems=10`; rows are applied in physical order through the real rank owner, retaining descending unsigned points, a top-ten cap, and stable input order for ties.

| Column | Name | Type | Validation |
| ---: | --- | --- | --- |
| 0 | UID | UInt32 | Current-culture integer; nonzero; unique within category |
| 1 | Name | text | May be empty; no slash, control character, or invalid Unicode |
| 2 | Category | Byte | Current-culture integer; `0..5` |
| 3 | TotalPoints | UInt32 | Current-culture integer |
| 4 | Class | UInt32 | Current-culture integer |
| 5 | Level | Byte | Current-culture integer |
| 6..19 | Mesh through Flag | UInt32 | Current-culture integer |
| 20 | GuildName | text | May be empty; no slash, control character, or invalid Unicode; trailing slash follows |

## Validation and transaction rules
- Load and Save capture the exact literal prefix/path at operation entry and serialize their complete read/publication or capture/write operation through the existing `SnapshotFileLockCoordinator` path owner. This orders cooperating operations; raw writers that bypass that coordinator remain outside the guarantee.
- Before waiting for the path owner, entry is rejected if the calling thread already holds the current registry or one of its six category gates. Inside the path owner the actual current registry is captured and validated, establishing path-before-registry/rank lock order without a test-only callback or accepting substitute.
- Load captures the current registry owner inside the path coordinator. Every failure publishes nothing and retains the exact prior graph. Malformed, truncated, duplicate, zero-UID, invalid-category, overflow, decoding, and operational-bound failures throw `InvalidDataException`; missing/unreadable files preserve their actual `IOException`/`FileNotFoundException`/`UnauthorizedAccessException`; a missing or changed publication owner or changed literal location throws `InvalidOperationException`. Void success therefore means that a valid complete graph was published.
- Load constructs a detached registry containing six distinct real `Rank` owners. It does not call destructive `Create`, publish rows incrementally, or convert malformed values to defaults.
- Successful publication locks the captured registry gate, rechecks that it is still the current owner, then performs one registry-reference publication. A replaced owner wins; the prepared graph is discarded.
- Save accepts only the current complete six-owner graph: exact category ownership, distinct owners, `MaxItems=10`, at most ten valid rows per category, key/UID/category identity, nonzero UIDs, non-increasing score order, and serializable strings.
- Save captures the graph and completes its write while retaining the target-path coordinator, preventing cooperating Saves from publishing snapshots out of capture order and serializing Load with Save. It revalidates the literal location before stage creation and again before target replacement.
- Save prepares the complete image before destination mutation, writes and flushes an owned same-directory stage, verifies it, then uses `File.Move` for a new destination or `File.Replace` for an existing destination. Pre-commit failure leaves the destination untouched. No multi-file, raw-writer, directory-flush, power-loss, or process-crash durability claim is made.

## Expected behavior and fixtures
- Valid row: `41/Hero/2/404/505/206/707/808/909/1010/1111/1212/1313/1414/1515/1616/1717/1818/1919/2020/Guild/`.
- Valid empty image: `Count=0` followed by the platform newline; it publishes six empty real owners. A missing file is a failure and never authorizes empty defaults.
- Invalid examples include a missing trailing slash, fewer/more than 21 fields, count/EOF mismatch, tail rows, UID zero, duplicate UID in one category, category 6, and numeric values outside UInt32/Byte.
- Owned temporary files are required for tests. No original save, running session, native executable, or live server is used by this helper scope.

## Evidence alignment
| Requirement | Evidence | Status |
| --- | --- | --- |
| 21 fields and trailing delimiter | tracked `LegacyNativeNinjaRank.Persistence.cs`; `LegacyNativeNinjaRankTests.Fixture.cs` | preserved successful format |
| Exact path prefix | tracked `LegacyNativeDbRead.cs` and `LegacyNativeDbWrite.cs` | preserved |
| Current-culture numerics | tracked read/write helpers and persistence tests | preserved for valid values |
| Six categories, top ten, stable ties | tracked `Ranking.cs`, capped mutation owner, refresh safety tests | preserved through real detached ranks |
| Whole-graph retention on invalid input | sanitized-parity policy and director requirement | corrected invariant |

## Implementation and qualification
- Postimage implementation paths: `src/WolfConquer.Core/Data/LegacyNativeNinjaRank.Persistence*.cs`.
- Tests are independently authored by the assigned test helper and executed only by the coordinating Windows CI owner.
- This source/text helper performs no build, test, Git, native, client, or live-server action.
- Remaining integration gap: coordinating review, isolated tests, full CI, and actual startup/login ownership wiring remain outside this helper artifact.
