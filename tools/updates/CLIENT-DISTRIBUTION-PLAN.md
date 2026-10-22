# Offline client distribution planning

PowerShell 7.4+; no game, server, signing key, .NET build or package publisher is started. The synthetic example is not an approved policy for a real client. Review the actual folder names, exact files, exclusions and publication metadata first.

```powershell
pwsh -NoProfile -File tools/updates/New-ClientDistributionPlan.ps1 -SourceRoot 'C:\ReviewedClient' -PolicyPath 'C:\Review\policy.json' -PlanPath 'C:\Review\plan-new.json' -ReportPath 'C:\Review\report-new.json'
pwsh -NoProfile -File tools/updates/Test-ClientDistributionPlan.ps1
```

All four paths are explicit absolute local paths. The policy must be outside the source. Output parent directories must already exist; plan/report must be NEW files outside both source and served publication roots. Existing outputs are never overwritten. Each artifact is written with an adjacent owned temporary file and atomic no-overwrite rename. There is no multi-file transaction: an I/O/race failure may leave the report without a plan.

The policy accepts only schemaVersion, assetDirectories, files, optional excludePaths, and publication. Folder approvals are exact top-level names, recursively covering ordinary assets. File approvals are exact source-relative paths. Exclusions are exact paths: an excluded directory omits that entire subtree. Use forward slashes and exact source spelling; no globs, traversal or case aliases. Every specified policy path must exist. A root folder approval cannot override quarantine rules.

An explicitly excluded final entry may itself be a junction or symbolic link.
Its exact name and ordinary ancestors are checked, then the leaf is omitted
without resolving, traversing or reading its target. This is omission only:
selected/unlisted links, linked source roots, and any explicit path beneath a
linked ancestor remain refusals. Missing or incorrectly cased exclusions and
exact approval/exclusion conflicts are rejected. The generated plan contains no
link entries; staging and publishing keep their ordinary-file-only rules.

Legacy `AutoP.exe` / `._cache_AutoP.exe` names are also quarantined by this
project's filename policy, even under an approved asset directory. The owned
updater replaces that legacy bootstrap chain. Explicitly exclude those entries;
do not rename them to evade review. This conservative name rule is not a malware
verdict or proof that any other file is clean. Direct staging plans receive the
same existing quarantine check before any copies.

Dated backup/temp extension segments are quarantined too: for example,
`Server.dat.bak-codex-date` and `GuardShield.dat.bak.codex-date` cannot enter
approved payloads. Exact exclusions omit them without deleting original files.
The rule also covers suffixed `.old`, `.orig`, `.tmp` and `.temp` segments; it
does not treat an ordinary name such as `oldworld.dat` as a backup.

Unknown top-level entries—and unknown siblings in partially selected folders—require explicit approval or exclusion. A review-required report is written, the command throws, and no publisher plan is created. It never silently approves an unknown item. A selected private/generated path blocks the plan until explicitly excluded. Private-key extensions, credentials, accounts/profiles, logs, caches, screenshots, temporary/debug/backup files, Git metadata and updater-control/trust files are conservatively quarantined by name anywhere. False positives require separate human review/curation; there is no bypass switch. Excluded/quarantined directories are not traversed.

Only names, attributes and file lengths are inspected; client/configuration contents are not opened or hashed. The policy itself is read as strict JSON. Name filtering cannot detect a secret renamed to an innocent asset name or hidden inside an approved file. Keep the source quiescent during review; this metadata scan is not a security/content audit or immutable snapshot.

On success, plan JSON matches the updater `publish --plan` PublicationPlan contract: explicit sourceRoot/publication metadata and a sorted files list of path/baseSha256:null. It is a fresh/full-snapshot plan, not an authorized patch-base map. No blobs, ZIP, signed release, feed or publication directory is created. The actual publisher independently revalidates paths and hashes source bytes later; a successful plan is not permission to redistribute game assets.

The report summarizes each top-level item, included file/byte totals and review/quarantine/exclusion counts. Detail lists are capped at 200, with complete counts retained; use narrower reviewed policies to resolve larger cases. No secret contents or private keys are included. Self-tests create only GUID-owned synthetic temporary trees, never execute their synthetic .exe bytes, and clean up owned Windows junctions without recursion before removing their own temporary root.

`Test-ClientDistributionExclusions.ps1` separately exercises real excluded leaves
and the planner-to-stager path with exclusively locked outside bytes. Its
file-symbolic-link group requires existing Windows privilege and reports an
explicit skip if unavailable; it never changes system permissions. The baseline
planner and staging suites remain separate commands.
